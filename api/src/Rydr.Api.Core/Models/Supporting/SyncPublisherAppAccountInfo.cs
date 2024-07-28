using Rydr.Api.Core.Models.Doc;

namespace Rydr.Api.Core.Models.Supporting;

public class SyncPublisherAppAccountInfo
{
    public SyncPublisherAppAccountInfo() { }

    public SyncPublisherAppAccountInfo(DynPublisherAppAccount fromAppAccount)
    {
        PublisherAppAccount = fromAppAccount;
        PublisherAccountId = fromAppAccount.PublisherAccountId;
        PublisherAppId = fromAppAccount.PublisherAppId;
        EncryptedAccessToken = fromAppAccount.PubAccessToken;
        SyncStepsLastFailedOn = fromAppAccount.SyncStepsLastFailedOn;
        SyncStepsFailCount = fromAppAccount.SyncStepsFailCount;
        TokenLastUpdated = fromAppAccount.TokenLastUpdated;
    }

    public long PublisherAccountId { get; }
    public long PublisherAppId { get; }
    public string EncryptedAccessToken { get; set; }
    public string RawAccessToken { get; set; }
    public Dictionary<string, long> SyncStepsLastFailedOn { get; set; }
    public Dictionary<string, long> SyncStepsFailCount { get; set; }

    public long TokenLastUpdated { get; }
    public DynPublisherAppAccount PublisherAppAccount { get; }

    public bool TokenUpdated { get; set; }
}
