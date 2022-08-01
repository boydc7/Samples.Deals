namespace Rydr.Api.Core.Interfaces.Services
{
    public interface ISendEmailService
    {
        void SendEmail(string to, string subject, string plainTextBodyPart, string htmlBodyPart, string from = null);
    }
}
