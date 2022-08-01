using System;
using Rydr.Api.Core.Enums;
using Rydr.Api.Core.Extensions;

namespace Rydr.Api.Core.Services.Internal
{
    public static class Guard
    {
        public static void Against(bool assert, string message)
        {
            if (!assert)
            {
                return;
            }

            throw new InvalidOperationException(message);
        }

        public static void Against<TException>(bool assert, string message) where TException : Exception
        {
            if (!assert)
            {
                return;
            }

            throw (TException)Activator.CreateInstance(typeof(TException), message);
        }

        public static void AgainstNullArgument(bool assert, string argumentName)
        {
            if (!assert)
            {
                return;
            }

            throw new ArgumentNullException(argumentName);
        }

        public static void AgainstArgumentOutOfRange(bool assert, string argumentName)
        {
            if (!assert)
            {
                return;
            }

            throw new ArgumentOutOfRangeException(argumentName);
        }

        public static void AgainstUnauthorized(bool assert, string message = null)
        {
            if (!assert)
            {
                return;
            }

            throw new UnauthorizedException(message);
        }

        public static void AgainstUnauthorized(bool assert, Func<string> messageFactory)
        {
            if (!assert)
            {
                return;
            }

            throw new UnauthorizedException(messageFactory.Invoke());
        }

        public static void AgainstRecordNotFound(bool assert, string recordId = null)
        {
            if (!assert)
            {
                return;
            }

            var msg = string.Concat("Record was not found or you do not have access to it",
                                    recordId.HasValue()
                                        ? $" - code [{recordId}]"
                                        : string.Empty);

            throw new RecordNotFoundException(msg);
        }

        public static void AgainstInvalidData(bool assert, string message)
        {
            if (!assert)
            {
                return;
            }

            throw new InvalidDataArgumentException(message);
        }

        public static void AgainstInvalidData<T>(T valueShouldMatch1, T valueShouldMatch2, string message = null)
            => AgainstInvalidData(!valueShouldMatch1.Equals(valueShouldMatch2), message ?? "Data and/or arguments passed are invalid");
    }
}
