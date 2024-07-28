namespace Rydr.Api.Core.Interfaces.Services;

public interface ISendEmailService
{
    void SendEmail(string toAddress, string toName, string subject, string plainTextBodyPart, string htmlBodyPart,
                   string fromAddress = null, string fromName = null);
}
