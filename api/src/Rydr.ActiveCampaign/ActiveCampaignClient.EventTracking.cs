using System.Linq;
using System.Threading.Tasks;
using Rydr.ActiveCampaign.Models;

namespace Rydr.ActiveCampaign
{
    public partial class ActiveCampaignClient
    {
        public async Task<AcEventTrackingEvent> GetEventTrackingEventAsync(string eventName)
        {
            var events = await GetAsync<GetAcEventTrackingEvents>("eventTrackingEvents", new
                                                                                         {
                                                                                             name = eventName
                                                                                         }).ConfigureAwait(false);

            return events?.EventTrackingEvents?.FirstOrDefault();
        }
    }
}
