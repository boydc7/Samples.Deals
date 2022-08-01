using Rydr.Api.Dto.Enums;

namespace Rydr.Api.Core.Interfaces.Services
{
    public interface ISmsService
    {
        void SendSms(string phoneNumber, string message, SmsType messageType = SmsType.Transactional, string countryCode = "+1");
    }
}
