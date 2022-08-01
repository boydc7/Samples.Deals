using Rydr.Api.Core.Models.Internal;

namespace Rydr.Api.Core.Interfaces.Services
{
    public interface IDistributedLockService
    {
        CacheLockItem GetExistingKeyLock(string keyId, string keyCategory);
        CacheLockItem GetExistingKeyLock(string cacheKey);
        CacheLockItem TryGetKeyLock(string keyId, string keyCategory, int lockDurationSeconds);
        CacheLockItem TryGetKeyLock(string cacheKey, int lockDurationSeconds);
        void RemoveLock(CacheLockItem ownedLock);
        void RemoveLock(string ownedLockKey);
        CacheLockItem UpdateLockIfExpiring(CacheLockItem ownedLock, decimal updateIfUnderPercentRemaining);
        CacheLockItem UpdateLock(CacheLockItem ownedLock, int lockDurationSeconds = 0);
    }
}
