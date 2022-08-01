using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Rydr.ActiveCampaign.Models;

namespace Rydr.ActiveCampaign
{
    public partial class ActiveCampaignClient
    {
        public async IAsyncEnumerable<IReadOnlyList<AcAutomation>> GetAutomationsAsync()
        {
            await foreach (var automations in GetPagedAsync<GetAcAutomations, AcAutomation>("automations", 30).ConfigureAwait(false))
            {
                yield return automations;
            }
        }

        public async IAsyncEnumerable<IEnumerable<AcAutomation>> GetAutomationsContainingAsync(string nameContaining)
        {
            await foreach (var automations in GetPagedAsync<GetAcAutomations, AcAutomation>("automations", 30, new
                                                                                                               {
                                                                                                                   name = nameContaining
                                                                                                               }).ConfigureAwait(false))
            {
                yield return automations.Where(a => a.Name.Contains(nameContaining, StringComparison.OrdinalIgnoreCase));
            }
        }

        public Task PostContactAutomationAsync(AcContactAutomation contactAutomation)
            => PostAsync("contactAutomations", bodyContent: new PostAcContactAutomation
                                                            {
                                                                ContactAutomation = contactAutomation
                                                            });
    }
}
