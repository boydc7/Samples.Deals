using System;
using Rydr.Api.Core.Extensions;
using ServiceStack;

namespace Rydr.Api.Core.Enums
{
    public class MonitorResponseDisabledException : Exception
    {
        public MonitorResponseDisabledException() : base("Monitor responses are currently disabled, to re-enable use the Respond member and proper value on monitor request") { }
    }

    public class NoRouteException : Exception
    {
        public NoRouteException(string route) : base($"Invalid URL route specified - attempted [{route}]") { }
    }

    public abstract class RydrApiException : Exception
    {
        protected RydrApiException(Exception throwException, bool isHandled = false)
            : this(null, throwException, isHandled) { }

        protected RydrApiException(string message, bool isHandled = false)
            : this(message, null, isHandled) { }

        protected RydrApiException(string message, Exception throwException, bool isHandled = false)
            : base(message.Coalesce(throwException?.Message))
        {
            ThrowException = throwException;
            IsHandled = isHandled;
        }

        public bool IsHandled { get; set; }
        public Exception ThrowException { get; set; }
    }

    public abstract class RydrAuthorizationException : RydrApiException
    {
        public RydrAuthorizationException(string message, Exception throwException, bool isHandled = false)
            : base(message, throwException, isHandled) { }
    }

    public class RecordNotFoundException : RydrAuthorizationException
    {
        public RecordNotFoundException(string message = "Record was not found or you do not have access to it", bool isHandled = true)
            : base(message.Coalesce("Record was not found or you do not have access to it"), HttpError.NotFound(message), isHandled) { }
    }

    public class ResourceUnvailableException : RydrApiException
    {
        public ResourceUnvailableException(string message = "Resource is not available for use", bool isHandled = false)
            : base(message.Coalesce("Resource is not available for use"), isHandled) { }
    }

    public class DuplicateRecordException : RydrApiException
    {
        public DuplicateRecordException(string message) : this(message, true) { }

        public DuplicateRecordException(string message = "Record already exists", bool isHandled = true)
            : base(message.Coalesce("Record already exists"), HttpError.Conflict(message), isHandled) { }
    }

    public class UnauthorizedException : RydrAuthorizationException
    {
        public UnauthorizedException(string message = "You do not have access to the resource requested", bool isHandled = true)
            : base(message.Coalesce("You do not have access to the resource requested"), HttpError.Unauthorized(message), isHandled) { }
    }

    public class ApplicationInShutdownException : Exception
    {
        public ApplicationInShutdownException() : base("Application currently in progress of shutting down, try again later") { }
    }

    public class InvalidDataArgumentException : RydrAuthorizationException
    {
        public InvalidDataArgumentException(string message, bool isHandled = false)
            : base(message, new ArgumentException(message), isHandled) { }
    }

    public class ApiArgumentException : RydrApiException
    {
        public ApiArgumentException(string message, bool isHandled = false)
            : base(message, new ArgumentException(message), isHandled) { }
    }

    public class LocalWorkerBackoffException : Exception
    {
        public LocalWorkerBackoffException(Exception inner) : base("Thread backoff required", inner) { }
    }

    public class InvalidApplicationStateException : Exception
    {
        public InvalidApplicationStateException(string message) : base(message) { }
    }

    public class OperationCannotBeCompletedException : RydrApiException
    {
        public OperationCannotBeCompletedException(string message, bool isHandled = true)
            : base(message, new ArgumentException(message), isHandled) { }
    }
}
