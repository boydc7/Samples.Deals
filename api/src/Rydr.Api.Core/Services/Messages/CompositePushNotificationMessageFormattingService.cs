using System.Collections.Generic;
using System.Linq;
using Rydr.Api.Core.Interfaces.Services;
using Rydr.Api.Core.Models;
using Rydr.Api.Dto.Enums;
using ServiceStack.OrmLite.Dapper;

namespace Rydr.Api.Core.Services.Messages
{
    public class CompositePushNotificationMessageFormattingService : IPushNotificationMessageFormattingService
    {
        private readonly List<IPushNotificationMessageFormattingService> _services;

        public CompositePushNotificationMessageFormattingService(IEnumerable<IPushNotificationMessageFormattingService> services)
        {
            _services = services.AsList();
        }

        public PushNotificationMessageParts GetMessageParts<T>(T message)
            where T : class
        {
            if (message == null)
            {
                return null;
            }

            var parts = _services.Select(s => s.GetMessageParts(message))
                                 .FirstOrDefault(m => m != null);

            return parts;
        }

        public string FormatMessage<T>(T message, ServerNotificationMedium medium)
            where T : class
        {
            if (message == null || medium == ServerNotificationMedium.Unspecified)
            {
                return null;
            }

            var formattedMessage = _services.Select(s => s.FormatMessage(message, medium))
                                            .FirstOrDefault(m => m != null);

            return formattedMessage;
        }
    }
}
