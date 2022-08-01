using System;
using System.Collections.Immutable;
using System.Threading;
using Rydr.Api.Core.Extensions;
using Rydr.Api.Core.Interfaces.Internal;
using Rydr.Api.Core.Models.Internal;
using Rydr.Api.Dto.Enums;

namespace Rydr.Api.Core.Services.Internal
{
    public static class ContextStack
    {
        private static readonly AsyncLocal<ImmutableStack<ContextStackInfo>> _asyncLocalValue = new AsyncLocal<ImmutableStack<ContextStackInfo>>();

        private static ImmutableStack<ContextStackInfo> CurrentContext
        {
            get
            {
                var stack = _asyncLocalValue.Value ?? ImmutableStack.Create<ContextStackInfo>();

                return stack;
            }

            set => _asyncLocalValue.Value = value;
        }

        public static IDisposable Push(IRequestState requestState)
        {
            if (!CurrentContext.IsEmpty)
            {
                requestState.MergeWith(CurrentContext.Peek().RequestState);
            }

            CurrentContext = CurrentContext.Push(new ContextStackInfo
                                                 {
                                                     RequestState = requestState,
                                                     RequestProfiler = StatsProfilerFactory.Create
                                                 });

            return new PopOnDispose();
        }

        private static void Pop()
        {
            if (CurrentContext.IsEmpty)
            {
                return;
            }

            CurrentContext = CurrentContext.Pop();
        }

        private sealed class PopOnDispose : IDisposable
        {
            private bool _disposed;
            private readonly object _lockObject = new object();

            public void Dispose()
            {
                if (_disposed)
                {
                    return;
                }

                lock(_lockObject)
                {
                    if (_disposed)
                    {
                        return;
                    }

                    var currentContext = CurrentContext;

                    if (currentContext != null && !currentContext.IsEmpty)
                    {
                        currentContext.Peek().RequestProfiler?.TryDispose();
                    }

                    currentContext = null;

                    Pop();

                    _disposed = true;
                }
            }
        }

        private sealed class ContextStackInfo
        {
            public IRequestState RequestState { get; set; }
            public IStatsProfiler RequestProfiler { get; set; }

            public override string ToString() => string.Concat(RequestState.RequestId, "|", RequestState.WorkspaceId);
        }

        public static IStatsProfiler Profiler
        {
            get
            {
                var context = CurrentContext;

                return context.IsEmpty
                           ? NullStatsProfiler.Instance
                           : context.Peek().RequestProfiler;
            }
        }

        public static IRequestState CurrentState =>
            CurrentContext.IsEmpty
                ? new RequestState
                  {
                      UserId = -1,
                      WorkspaceId = -1,
                      RequestPublisherAccountId = -1,
                      ContextPublisherAccountId = -1,
                      RoleId = -1,
                      UserType = UserType.Unknown
                  }
                : CurrentContext.Peek().RequestState;

        public static string CurrentStackLogInfo
        {
            get
            {
                if (CurrentContext.IsEmpty)
                {
                    return null;
                }

                var state = CurrentContext.Peek().RequestState;

                if (state == null)
                {
                    return null;
                }

                return string.Concat(state.RequestId, "|", state.WorkspaceId, "|", state.UserId, "|",
                                     state.RequestPublisherAccountId, "|", state.HttpVerb.Coalesce("GET"), "|",
                                     state.ContextPublisherAccountId);
            }
        }
    }
}
