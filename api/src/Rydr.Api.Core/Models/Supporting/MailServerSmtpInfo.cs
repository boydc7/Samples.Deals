namespace Rydr.Api.Core.Models.Supporting
{
    public class MailServerSmtpInfo
    {
        public string Host { get; set; }
        public int Port { get; set; } = 587;
        public int TimeoutSeconds { get; set; } = 20;
        public bool UseSsl { get; set; } = true;
        public string UserName { get; set; }
        public string Password { get; set; }
    }
}
