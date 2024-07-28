using System.Collections.Concurrent;
using Rydr.Api.Core.Extensions;
using Rydr.Api.Core.Models.Internal;
using ServiceStack.Caching;
using ServiceStack.Logging;

// ReSharper disable UnusedAutoPropertyAccessor.Local

namespace Rydr.Api.Core.Services.Internal;

public class LocalResourceManager
{
    private static readonly int _secondsBetweenResourceMaintenance = (60 * 11) + RandomProvider.GetRandomIntBeween(0, 99);
    private static readonly ILog _log = LogManager.GetLogger("LocalResourceManager");
    private static readonly object _cacheMaintainLockItem = new();
    private static long _resourcesLastMaintained;
    private static bool _processing;

    private readonly ConcurrentDictionary<Type, DefaultTaskInfo> _typeCallbackMap = new();

    private LocalResourceManager() { }

    public static LocalResourceManager Instance { get; } = new();

    public void RegisterManagementCallback<T>(T instance, Action<T> callback)
        => _typeCallbackMap.AddOrUpdate(typeof(T),
                                        new DefaultTaskInfo
                                        {
                                            ObjectRef = instance,
                                            Callback = o =>
                                                       {
                                                           if (!(o is T objT))
                                                           {
                                                               return Task.CompletedTask;
                                                           }

                                                           callback(objT);

                                                           return Task.CompletedTask;
                                                       }
                                        },
                                        (k, x) => new DefaultTaskInfo
                                                  {
                                                      ObjectRef = instance,
                                                      Callback = o =>
                                                                 {
                                                                     if (!(o is T objT))
                                                                     {
                                                                         return Task.CompletedTask;
                                                                     }

                                                                     callback(objT);

                                                                     return Task.CompletedTask;
                                                                 }
                                                  });

    public bool ShouldCheckResources()
    { // Not thread safe or certain, just a pre-check that returns true if the resource check should be attempted...
        if (_processing)
        {
            return false;
        }

        var nowTs = DateTimeHelper.UtcNowTs;

        if ((nowTs - _resourcesLastMaintained) <= _secondsBetweenResourceMaintenance)
        {
            return false;
        }

        return !_processing;
    }

    public void CheckAllResources(MemoryCacheClient cache)
    {
        var nowTs = DateTimeHelper.UtcNowTs;

        var myLastMaintained = _resourcesLastMaintained;

        if ((nowTs - myLastMaintained) <= _secondsBetweenResourceMaintenance)
        {
            return;
        }

        if (_processing)
        {
            return;
        }

        try
        {
            lock(_cacheMaintainLockItem)
            {
                _processing = true;
            }

            if (myLastMaintained < _resourcesLastMaintained)
            {
                return;
            }

            _resourcesLastMaintained = nowTs;

            ManageLocalCache(cache);

            ManageCallbacks();
        }
        finally
        {
            _processing = false;
        }
    }

    private void ManageCallbacks()
    {
        foreach (var taskInfo in _typeCallbackMap.Values)
        {
            try
            {
                taskInfo.Callback(taskInfo.ObjectRef);
            }
            catch(Exception x) when(_log.LogExceptionReturnTrue(x))
            {
                // Ignore, it's been logged, continue...
            }
        }
    }

    private void ManageLocalCache(MemoryCacheClient cache)
        => cache?.RemoveExpiredEntries();
}
