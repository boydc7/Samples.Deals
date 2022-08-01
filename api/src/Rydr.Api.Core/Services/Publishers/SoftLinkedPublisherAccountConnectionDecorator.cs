using System.Linq;
using System.Threading.Tasks;
using Rydr.Api.Core.Extensions;
using Rydr.Api.Core.Interfaces.DataAccess;
using Rydr.Api.Core.Interfaces.Services;
using Rydr.Api.Core.Models.Internal;
using Rydr.Api.Core.Transforms;
using Rydr.Api.Dto.Enums;
using ServiceStack.Logging;
using ServiceStack.OrmLite.Dapper;

namespace Rydr.Api.Core.Services.Publishers
{
    public class SoftLinkedPublisherAccountConnectionDecorator : IPublisherAccountConnectionDecorator
    {
        private static readonly ILog _log = LogManager.GetLogger("SoftLinkedPublisherAccountConnectionDecorator");
        private readonly IRydrDataService _rydrDataService;

        public SoftLinkedPublisherAccountConnectionDecorator(IRydrDataService rydrDataService)
        {
            _rydrDataService = rydrDataService;
        }

        public async Task DecorateAsync(PublisherAccountConnectInfo publisherAccountConnectInfo)
        {
            if (!publisherAccountConnectInfo.IncomingPublisherAccount.Type.IsWritablePublisherType())
            { // We only handle soft-linked to full link status here, other decorators deal with soft-linked to basic (soft-to-basicIg for example)
                return;
            }

            // Can get a RydrSoftLink match in a couple ways, might have the rydrPublisherId already included, in which case we'll have the existing
            // and everything we need to convert. Or we might not have an existing match (if not coming from api hit or not the app), in which case we have
            // to run a query to find a potential match
            if (publisherAccountConnectInfo.ExistingPublisherAccount != null &&
                publisherAccountConnectInfo.NewPublisherAccount.IsRydrSoftLinkedAccount() &&
                !publisherAccountConnectInfo.IncomingPublisherAccount.AccountId.StartsWithOrdinalCi("rydr_"))
            { // Likely the first real linking of an FbIg account that had previously been soft-linked for running deals with minimal onboarding friction
                // If so, rewrite the existing publisherAccount with the proper identifiers
                var incomingDynPublisherAccount = publisherAccountConnectInfo.IncomingPublisherAccount.ToDynPublisherAccount();

                if (!incomingDynPublisherAccount.IsRydrSoftLinkedAccount())
                { // Take the existing one and rewrite the identifiers, delete the old version, add the new one
                    publisherAccountConnectInfo.NewPublisherAccount.PublisherAccountId = publisherAccountConnectInfo.ExistingPublisherAccount.PublisherAccountId;
                    publisherAccountConnectInfo.NewPublisherAccount.AccountId = incomingDynPublisherAccount.AccountId;
                    publisherAccountConnectInfo.NewPublisherAccount.PublisherType = incomingDynPublisherAccount.PublisherType;
                    publisherAccountConnectInfo.NewPublisherAccount.EdgeId = publisherAccountConnectInfo.NewPublisherAccount.GetEdgeId();

                    publisherAccountConnectInfo.ConvertExisting = true;

                    _log.Info($"  Incoming connect request will up-convert existing soft-linked PublisherAccount [{publisherAccountConnectInfo.ExistingPublisherAccount.DisplayName()}] to full status");
                }
            }
            else if (publisherAccountConnectInfo.ExistingPublisherAccount == null &&
                     !publisherAccountConnectInfo.NewPublisherAccount.IsRydrSoftLinkedAccount() &&
                     !publisherAccountConnectInfo.IncomingPublisherAccount.AccountId.StartsWithOrdinalCi("rydr_"))
            {
                var existingRydrSoftPubAcctIds = await _rydrDataService.QueryAdHocAsync(db => db.QueryAsync<Int64Id>(@"
SELECT  pa.Id AS Id
FROM    PublisherAccounts pa
WHERE   pa.UserName = @UserName
        AND pa.PublisherType = @PublisherType
        AND pa.AccountType = @AccountType
        AND pa.RydrAccountType = @RydrAccountType
        AND pa.AccountId LIKE 'rydr_%'
        AND pa.DeletedOn IS NULL
LIMIT   1;
",
                                                                                                                     new
                                                                                                                     {
                                                                                                                         publisherAccountConnectInfo.IncomingPublisherAccount.UserName,
                                                                                                                         PublisherType = publisherAccountConnectInfo.IncomingPublisherAccount.Type,
                                                                                                                         publisherAccountConnectInfo.IncomingPublisherAccount.AccountType,
                                                                                                                         publisherAccountConnectInfo.IncomingPublisherAccount.RydrAccountType
                                                                                                                     }));

                var existingRydrSoftPubAcctId = existingRydrSoftPubAcctIds?.FirstOrDefault()?.Id ?? 0;

                publisherAccountConnectInfo.ExistingPublisherAccount = await PublisherExtensions.DefaultPublisherAccountService
                                                                                                .GetPublisherAccountAsync(existingRydrSoftPubAcctId);

                // In this case, if we have an existing one matching the incoming identifiers, we just need to give the one to be newly created the id of the existing one
                if (publisherAccountConnectInfo.ExistingPublisherAccount != null)
                {
                    publisherAccountConnectInfo.NewPublisherAccount.PublisherAccountId = publisherAccountConnectInfo.ExistingPublisherAccount
                                                                                                                    .PublisherAccountId;

                    publisherAccountConnectInfo.NewPublisherAccount.ReferenceId = publisherAccountConnectInfo.ExistingPublisherAccount
                                                                                                             .PublisherAccountId
                                                                                                             .ToStringInvariant();

                    publisherAccountConnectInfo.NewPublisherAccount.AlternateAccountId = publisherAccountConnectInfo.NewPublisherAccount
                                                                                                                    .AlternateAccountId
                                                                                                                    .Coalesce(publisherAccountConnectInfo.ExistingPublisherAccount
                                                                                                                                                         .AlternateAccountId);

                    publisherAccountConnectInfo.NewPublisherAccount.PageId = publisherAccountConnectInfo.NewPublisherAccount
                                                                                                        .PageId
                                                                                                        .Coalesce(publisherAccountConnectInfo.ExistingPublisherAccount
                                                                                                                                             .PageId);

                    publisherAccountConnectInfo.ConvertExisting = true;

                    _log.Info($"  Incoming connect request will up-convert existing soft-linked PublisherAccount [{publisherAccountConnectInfo.ExistingPublisherAccount.DisplayName()}] to full status");
                }
            }
        }
    }
}
