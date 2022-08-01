using System;
using System.Runtime.Serialization;
using Rydr.Api.Core.Extensions;
using Rydr.Api.Core.Interfaces.Services;
using Rydr.Api.Core.Services.Internal;
using ServiceStack.DataAnnotations;

namespace Rydr.Api.Core.Models.Internal
{
    public class CacheLockItem : IDisposable
    {
        public CacheLockItem(string key, int lockDurationSeconds = 600)
        {
            var now = DateTimeHelper.UtcNowTs;

            Key = key;
            OwnerToken = Guid.NewGuid().ToStringId();

            LockDurationSeconds = lockDurationSeconds > 0
                                      ? lockDurationSeconds
                                      : 600;

            CreatedOn = now;
            LastHeartbeatOn = now;
            MachineName = Environment.MachineName;
        }

        public string Key { get; set; }
        public string MachineName { get; set; }
        public long CreatedOn { get; set; }
        public long LastHeartbeatOn { get; set; }
        public string OwnerToken { get; set; }
        public int LockDurationSeconds { get; set; }

        public bool MatchesTokensWith(CacheLockItem other) => OwnerToken.EqualsOrdinalCi(other?.OwnerToken);

        public void Update()
        {
            LastHeartbeatOn = DateTimeHelper.UtcNowTs;
        }

        [Ignore]
        [IgnoreDataMember]
        public long SecondsRemaining => LockDurationSeconds - (DateTimeHelper.UtcNowTs - LastHeartbeatOn);

        [Ignore]
        [IgnoreDataMember]
        public IDistributedLockService LockService { get; set; }

        public bool IsValid() => SecondsRemaining > 0;

        public void Dispose()
        {
            LockService?.RemoveLock(this);
            LastHeartbeatOn = int.MinValue;
        }
    }

    public class MemoryLockItem : IDisposable
    {
        public MemoryLockItem(string key, int lockDurationSeconds = 0)
        {
            LockItem = CacheExtensions.InMemoryLockService.TryGetKeyLock(key, lockDurationSeconds.Gz(int.MaxValue));
        }

        public bool HaveLock => LockItem != null;

        public void Update()
        {
            if (LockItem == null)
            {
                return;
            }

            LockItem.LastHeartbeatOn = DateTimeHelper.UtcNowTs;
        }

        public CacheLockItem LockItem { get; }

        public void Dispose()
        {
            CacheExtensions.InMemoryLockService.RemoveLock(LockItem);
        }
    }
}
