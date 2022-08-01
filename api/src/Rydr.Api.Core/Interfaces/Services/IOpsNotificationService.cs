using System.Threading.Tasks;

namespace Rydr.Api.Core.Interfaces.Services
{
    public interface IOpsNotificationService
    {
        Task SendAppNotificationAsync(string subject, string message);
        Task SendApiNotificationAsync(string subject, string message);
        Task SendTrackEventNotificationAsync(string eventName, string userEmail, string extraEventInfo = null);
        Task SendManagedAccountNotificationAsync(string subject, string message);
    }
}
