using System.Security.Authentication;
using MailKit.Net.Smtp;
using MimeKit;
using Rydr.Api.Core.Configuration;
using Rydr.Api.Core.Extensions;
using Rydr.Api.Core.Interfaces.Services;
using Rydr.Api.Core.Models.Supporting;
using Rydr.FbSdk.Extensions;
using ServiceStack;
using ServiceStack.Logging;

namespace Rydr.Api.Core.Services.Internal;

public class MailKitEmailService : ISendEmailService
{
    private readonly bool _disable = RydrEnvironment.GetAppSetting("MailServer.Disable", false);
    private readonly string _debugSendTo = RydrEnvironment.GetAppSetting("MailServer.DebugSendTo");
    private readonly string _from = RydrEnvironment.GetAppSetting("MailServer.From", "noreply@getryder.com");
    private readonly MailServerSmtpInfo _smtpServerInfo;

    private readonly ILog _log = LogManager.GetLogger("MailKitEmailService");

    public MailKitEmailService()
    {
        _smtpServerInfo = RydrEnvironment.GetAppSetting("MailServer.SmtpInfo").FromJsv<MailServerSmtpInfo>();
    }

    public void SendEmail(string toAddress, string toName, string subject, string plainTextBodyPart, string htmlBodyPart,
                          string fromAddress = null, string fromName = null)
    {
        if (_disable)
        {
            _log.DebugInfoFormat("MailServer disabled - would have sent to [{0}], subject [{1}], body [{2}]", toAddress, subject, plainTextBodyPart.Coalesce(htmlBodyPart).Left(100));

            return;
        }

        if (_debugSendTo.HasValue())
        {
            toAddress = _debugSendTo;
        }

        using(var smtpClient = new SmtpClient())
        {
            if (!RydrEnvironment.IsReleaseEnvironment)
            {
                smtpClient.ServerCertificateValidationCallback = (s, c, h, e) => true;
            }

            smtpClient.SslProtocols = SslProtocols.Tls12;
            smtpClient.Timeout = _smtpServerInfo.TimeoutSeconds * 1000;
            smtpClient.Connect(_smtpServerInfo.Host, _smtpServerInfo.Port);

            if (_smtpServerInfo.UserName.HasValue())
            {
                smtpClient.Authenticate(_smtpServerInfo.UserName, _smtpServerInfo.Password);
            }

            var from = fromAddress.HasValue() && fromName.HasValue()
                           ? new MailboxAddress(fromName, fromAddress)
                           : new MailboxAddress("Rydr App", _from);

            var bodyParts = new BodyBuilder
                            {
                                HtmlBody = htmlBodyPart.ToNullIfEmpty(),
                                TextBody = plainTextBodyPart.ToNullIfEmpty()
                            };

            var msg = new MimeMessage
                      {
                          From =
                          {
                              from
                          },
                          To =
                          {
                              new MailboxAddress(toName, toAddress)
                          },
                          Subject = subject,
                          Body = bodyParts.ToMessageBody()
                      };

            smtpClient.Send(msg);
            smtpClient.Disconnect(true);
        }
    }
}
