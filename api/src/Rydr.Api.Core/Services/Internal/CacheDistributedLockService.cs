using System;
using Rydr.Api.Core.Extensions;
using Rydr.Api.Core.Interfaces.Services;
using Rydr.Api.Core.Models.Internal;
using ServiceStack.Caching;

namespace Rydr.Api.Core.Services.Internal
{
    public class CacheDistributedLockService : IDistributedLockService
    {
        private readonly ICacheClient _cacheClient;

        public CacheDistributedLockService(ICacheClient cacheClient)
        {
            _cacheClient = cacheClient;
        }

        public CacheLockItem GetExistingKeyLock(string keyId, string keyCategory)
            => GetExistingKeyLock(CacheExtensions.GetLockCacheKey(keyId, keyCategory));

        public CacheLockItem GetExistingKeyLock(string cacheKey)
        {
            if (string.IsNullOrEmpty(cacheKey))
            {
                return null;
            }

            var cachedLock = _cacheClient.Get<CacheLockItem>(cacheKey);

            if (cachedLock != null && cachedLock.IsValid())
            {
                cachedLock.LockService = this;

                return cachedLock;
            }

            return null;
        }

        public CacheLockItem TryGetKeyLock(string keyId, string keyCategory, int lockDurationSeconds)
            => TryGetKeyLock(CacheExtensions.GetLockCacheKey(keyId, keyCategory), lockDurationSeconds);

        public CacheLockItem TryGetKeyLock(string cacheKey, int lockDurationSeconds)
        {
            var cachedLock = GetExistingKeyLock(cacheKey);

            if (cachedLock != null)
            {
                return null;
            }

            var newLock = new CacheLockItem(cacheKey, lockDurationSeconds);
            var expiresIn = new TimeSpan(0, 0, lockDurationSeconds);

            if (!_cacheClient.Add(cacheKey, newLock, expiresIn))
            {
                return null;
            }

            // Go get it again and ensure the stored item is ours
            cachedLock = _cacheClient.Get<CacheLockItem>(cacheKey);

            if (newLock.MatchesTokensWith(cachedLock))
            {
                newLock.LockService = this;

                return newLock;
            }

            return null;
        }

        public void RemoveLock(CacheLockItem ownedLock)
        {
            if (ownedLock == null)
            {
                return;
            }

            RemoveLock(ownedLock.Key);
        }

        public void RemoveLock(string ownedLockKey)
        {
            if (!ownedLockKey.HasValue())
            {
                return;
            }

            try
            {
                _cacheClient.Remove(ownedLockKey);
            }
            catch
            {
                // ignored
            }
        }

        public CacheLockItem UpdateLockIfExpiring(CacheLockItem ownedLock, decimal updateIfUnderPercentRemaining)
        {
            if (ownedLock == null)
            {
                return null;
            }

            var percentRemaining = ownedLock.SecondsRemaining / (decimal)ownedLock.LockDurationSeconds;

            ownedLock.LockService = this;

            return percentRemaining > updateIfUnderPercentRemaining
                       ? ownedLock
                       : UpdateLock(ownedLock);
        }

        public CacheLockItem UpdateLock(CacheLockItem ownedLock, int lockDurationSeconds = 0)
        {
            if (ownedLock == null)
            {
                return null;
            }

            var updateWithLockDurationOf = lockDurationSeconds > 0
                                               ? lockDurationSeconds
                                               : ownedLock.LockDurationSeconds;

            // Go get a lock with the key asked for
            var cachedLock = _cacheClient.Get<CacheLockItem>(ownedLock.Key);

            if (cachedLock == null)
            { // Trying to update an owned lock that doesn't exist - try to re-get it
                cachedLock = TryGetKeyLock(ownedLock.Key, updateWithLockDurationOf);

                if (cachedLock == null)
                { // Still null, couldn't get it, something is wrong
                    throw new ArgumentOutOfRangeException(nameof(ownedLock), ownedLock.Key, "Attempted to update owned lock that does not exist");
                }

                // Got a new lock, nothing to update really, return the new lock
                return cachedLock;
            }

            if (!ownedLock.MatchesTokensWith(cachedLock))
            {
                throw new ArgumentOutOfRangeException(nameof(ownedLock), ownedLock.OwnerToken,
                                                      "Attempted to update owned lock that exists with a mismatched OwnerToken value");
            }

            var expiresIn = new TimeSpan(0, 0, updateWithLockDurationOf);
            ownedLock.Update();

            _cacheClient.Set(ownedLock.Key, ownedLock, expiresIn);
            cachedLock = _cacheClient.Get<CacheLockItem>(ownedLock.Key);

            if (!ownedLock.MatchesTokensWith(cachedLock))
            {
                throw new ArgumentOutOfRangeException(nameof(ownedLock), ownedLock.OwnerToken,
                                                      "Updated owned lock that exists but after update there is a mismatched OwnerToken value");
            }

            ownedLock.LockService = this;

            return ownedLock;
        }
    }
}
