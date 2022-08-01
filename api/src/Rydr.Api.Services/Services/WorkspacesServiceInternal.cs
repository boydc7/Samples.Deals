using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Rydr.ActiveCampaign;
using Rydr.ActiveCampaign.Models;
using Rydr.Api.Core.Extensions;
using Rydr.Api.Core.Interfaces.DataAccess;
using Rydr.Api.Core.Interfaces.Internal;
using Rydr.Api.Core.Interfaces.Services;
using Rydr.Api.Core.Models.Doc;
using Rydr.Api.Core.Models.Rydr;
using Rydr.Api.Core.Transforms;
using Rydr.Api.Dto;
using Rydr.Api.Dto.Enums;
using Rydr.Api.Dto.Messages;
using Rydr.Api.Dto.Publishers;
using ServiceStack;
using ServiceStack.OrmLite;

namespace Rydr.Api.Services.Services
{
    public class WorkspacesServiceInternal : BaseInternalOnlyApiService
    {
        private readonly IWorkspaceService _workspaceService;
        private readonly IPublisherAccountService _publisherAccountService;
        private readonly IAssociationService _associationService;
        private readonly IAuthorizeService _authorizeService;
        private readonly IDeferRequestsService _deferRequestsService;
        private readonly IRydrDataService _rydrDataService;
        private readonly IOpsNotificationService _opsNotificationService;
        private readonly IUserService _userService;
        private readonly IWorkspaceSubscriptionService _workspaceSubscriptionService;

        public WorkspacesServiceInternal(IWorkspaceService workspaceService,
                                         IPublisherAccountService publisherAccountService,
                                         IAssociationService associationService,
                                         IAuthorizeService authorizeService,
                                         IDeferRequestsService deferRequestsService,
                                         IRydrDataService rydrDataService,
                                         IOpsNotificationService opsNotificationService,
                                         IUserService userService,
                                         IWorkspaceSubscriptionService workspaceSubscriptionService)
        {
            _workspaceService = workspaceService;
            _publisherAccountService = publisherAccountService;
            _associationService = associationService;
            _authorizeService = authorizeService;
            _deferRequestsService = deferRequestsService;
            _rydrDataService = rydrDataService;
            _opsNotificationService = opsNotificationService;
            _userService = userService;
            _workspaceSubscriptionService = workspaceSubscriptionService;
        }

        public async Task Post(WorkspaceUserLinked request)
        {
            await _rydrDataService.SaveIgnoreConflictAsync(new RydrWorkspaceUser
                                                           {
                                                               WorkspaceUserId = request.WorkspaceUserId,
                                                               WorkspaceId = request.InWorkspaceId,
                                                               UserId = request.RydrUserId,
                                                               WorkspaceRole = request.WorkspaceUserId == request.RydrUserId
                                                                                   ? WorkspaceRole.Admin
                                                                                   : WorkspaceRole.User,
                                                               DeletedOn = null
                                                           },
                                                           r => r.Id);
        }

        public async Task Post(WorkspaceUserUpdated request)
        {
            await _rydrDataService.SaveIgnoreConflictAsync(new RydrWorkspaceUser
                                                           {
                                                               WorkspaceUserId = request.WorkspaceUserId,
                                                               WorkspaceId = request.InWorkspaceId,
                                                               UserId = request.RydrUserId,
                                                               WorkspaceRole = request.WorkspaceRole,
                                                               DeletedOn = null
                                                           },
                                                           r => r.Id);
        }

        public async Task Post(WorkspaceUserDelinked request)
        {
            await _rydrDataService.SaveIgnoreConflictAsync(new RydrWorkspaceUser
                                                           {
                                                               WorkspaceUserId = request.WorkspaceUserId,
                                                               WorkspaceId = request.InWorkspaceId,
                                                               UserId = request.RydrUserId,
                                                               DeletedOn = _dateTimeProvider.UtcNow
                                                           },
                                                           r => r.Id);
        }

        public async Task Post(WorkspaceUserPublisherAccountLinked request)
        {
            await _rydrDataService.SaveIgnoreConflictAsync(new RydrWorkspaceUserPublisherAccount
                                                           {
                                                               WorkspaceUserId = request.WorkspaceUserId,
                                                               PublisherAccountId = request.ToPublisherAccountId,
                                                               WorkspaceId = request.InWorkspaceId,
                                                               UserId = request.RydrUserId,
                                                               DeletedOn = null
                                                           },
                                                           r => r.Id);
        }

        public async Task Post(WorkspaceUserPublisherAccountDelinked request)
        {
            await _rydrDataService.SaveIgnoreConflictAsync(new RydrWorkspaceUserPublisherAccount
                                                           {
                                                               WorkspaceUserId = request.WorkspaceUserId,
                                                               PublisherAccountId = request.FromPublisherAccountId,
                                                               WorkspaceId = request.InWorkspaceId,
                                                               UserId = request.RydrUserId,
                                                               DeletedOn = _dateTimeProvider.UtcNow
                                                           },
                                                           r => r.Id);
        }

        public async Task Post(DeleteWorkspaceInternal request)
        {
            var existingWorkspace = await _workspaceService.TryGetWorkspaceAsync(request.Id);

            // Delink all publisher accounts that are linked to this workspace
            var allWorkspacePublisherAccounts = await _workspaceService.GetWorkspaceUserPublisherAccountsAsync(existingWorkspace.Id, existingWorkspace.OwnerId, true)
                                                                       .ToListReadOnly();

            foreach (var workspacePublisherAccount in allWorkspacePublisherAccounts)
            {
                var worksapceLinkedPublisherAccounts = await _publisherAccountService.GetLinkedPublisherAccountsAsync(workspacePublisherAccount.PublisherAccountId)
                                                                                     .ToHashSetAsync(DynPublisherAccount.DefaultComparer);

                // Workspace linked is the intersection of accounts linked to the given publisher account AND the workspace
                worksapceLinkedPublisherAccounts.IntersectWith(allWorkspacePublisherAccounts);

                foreach (var worksapceLinkedPublisherAccount in worksapceLinkedPublisherAccounts)
                {
                    var delinkRequest = workspacePublisherAccount.IsTokenAccount()
                                            ? new DelinkPublisherAccount
                                              {
                                                  FromWorkspaceId = existingWorkspace.Id,
                                                  FromPublisherAccountId = workspacePublisherAccount.PublisherAccountId,
                                                  ToPublisherAccountId = worksapceLinkedPublisherAccount.PublisherAccountId
                                              }
                                            : new DelinkPublisherAccount
                                              {
                                                  FromWorkspaceId = existingWorkspace.Id,
                                                  FromPublisherAccountId = worksapceLinkedPublisherAccount.PublisherAccountId,
                                                  ToPublisherAccountId = workspacePublisherAccount.PublisherAccountId
                                              };

                    _deferRequestsService.DeferRequest(delinkRequest.WithAdminRequestInfo());
                }

                // If a token account, delink the token account from the worksapce itself
                if (workspacePublisherAccount.IsTokenAccount())
                {
                    await _workspaceService.DelinkTokenAccountAsync(existingWorkspace, workspacePublisherAccount.PublisherAccountId);
                }
            }

            // Delink all workspace users from the workspace
            await foreach (var workspaceUser in _workspaceService.GetWorkspaceUsersAsync(existingWorkspace.Id)
                                                                 .Where(u => u.UserId != existingWorkspace.OwnerId))
            {
                await _workspaceService.DelinkUserAsync(existingWorkspace.Id, workspaceUser.UserId);
            }

            await _associationService.DeleteAllAssociationsAsync(RecordType.Workspace, request.Id);

            await _authorizeService.DeAuthorizeAllToFromAsync(request.Id);

            await _dynamoDb.SoftDeleteAsync(existingWorkspace);

            if (request.DeleteWorkspacePublisherAccount &&
                existingWorkspace != null &&
                existingWorkspace.DefaultPublisherAccountId > 0)
            {
                _deferRequestsService.DeferLowPriRequest(new DeletePublisherAccountInternal
                                                         {
                                                             PublisherAccountId = existingWorkspace.DefaultPublisherAccountId
                                                         });
            }

            _deferRequestsService.DeferLowPriRequest(new WorkspaceDeleted
                                                     {
                                                         Id = request.Id
                                                     });
        }

        public async Task Post(WorkspaceDeleted request)
        {
            await _workspaceSubscriptionService.TryDeleteActiveWorkspaceSubscriptionAsync(request.Id);

            // Cancel deals owned by the workspace (which implicitly also then deletes associated active reqeusts
            var workspaceOwnedDealIds = await _rydrDataService.QueryAdHocAsync(db => db.ColumnAsync<long>(@"
SELECT    DISTINCT d.Id
FROM      Deals d
WHERE     d.WorkspaceId = @WorkspaceId
          AND d.DeletedOn IS NULL
          AND d.Status < @MinStatusToIgnore;
",
                                                                                                          new
                                                                                                          {
                                                                                                              WorkspaceId = request.Id,
                                                                                                              MinStatusToIgnore = (int)DealStatus.Completed
                                                                                                          }));

            if (!workspaceOwnedDealIds.IsNullOrEmpty())
            {
                foreach (var workspaceOwnedDealId in workspaceOwnedDealIds)
                {
                    _deferRequestsService.DeferDealRequest(new DeleteDealInternal
                                                           {
                                                               DealId = workspaceOwnedDealId,
                                                               Reason = "Workspace that owns the deal was removed"
                                                           }.PopulateWithRequestInfo(request));
                }
            }

            var workspace = await _workspaceService.TryGetWorkspaceAsync(request.Id);

            await _workspaceService.TrackEventNotificationAsync(request.Id, nameof(WorkspaceDeleted), workspace.WorkspaceType.ToString(),
                                                                new ExternalCrmUpdateItem
                                                                {
                                                                    FieldName = workspace.WorkspaceType == WorkspaceType.Personal
                                                                                    ? "PersonalWorkspaceIds"
                                                                                    : "TeamWorkspaceIds",
                                                                    FieldValue = workspace.Id.ToStringInvariant(),
                                                                    Remove = true
                                                                });
        }

        public async Task Post(WorkspacePosted request)
        {
            var workspace = await _workspaceService.GetWorkspaceAsync(request.Id);

            // Ensure there's a trial subscription if not another paid already
            var existingActiveSubscription = await _workspaceSubscriptionService.TryGetActiveWorkspaceSubscriptionAsync(workspace.Id);

            if (!existingActiveSubscription.IsPaidSubscription())
            {
                await _workspaceSubscriptionService.AddSystemSubscriptionAsync(workspace.Id, SubscriptionType.Trial);
            }

            var workspaceOwner = await _userService.GetUserAsync(workspace.OwnerId);

            var defaultPublisherAccount = await _publisherAccountService.TryGetPublisherAccountAsync(workspace.DefaultPublisherAccountId);

            await _opsNotificationService.TrySendAppNotificationAsync($"New Workspace Created ({workspace.Id})", $@"
Name :          {workspace.Name}
Type :          {workspace.WorkspaceType.ToString()}
User Email :    {workspaceOwner.Email}
User FullName : {workspaceOwner.FullName}
CreatedVia :    {(defaultPublisherAccount != null || workspace.SecondaryTokenPublisherAccountIds.IsNullOrEmpty()
                      ? workspace.CreatedViaPublisherType.ToString()
                      : "InstagramBasic")}

TokenAccount Handle:   {defaultPublisherAccount?.UserName ?? "N/A"}
TokenAccount Email:    {defaultPublisherAccount?.Email ?? "N/A"}
TokenAccount FullName: {defaultPublisherAccount?.FullName ?? "N/A"}
");

            await HandleNewWorkspaceExternalCrmIntegrationAsync(workspace, workspaceOwner, defaultPublisherAccount);
        }

        public async Task Post(WorkspaceUpdated request)
        {
            await _workspaceService.AssociateInviteCodeAsync(request.Id);
        }

        private async Task HandleNewWorkspaceExternalCrmIntegrationAsync(DynWorkspace workspace, DynUser workspaceOwner, DynPublisherAccount defaultPublisherAccount)
        {
            if (!workspace.WorkspaceType.HasExternalCrmIntegration())
            {
                return;
            }

            var contactEmail = workspaceOwner.Email.Coalesce(defaultPublisherAccount?.Email);

            if (!contactEmail.HasValue())
            {
                return;
            }

            var acClient = ActiveCampaignClientFactory.Instance.GetOrCreateRydrClient();

            var acContact = await acClient.GetContactByEmailAsync(contactEmail);

            if (acContact == null || acContact.IsDeleted)
            { // Create/Update away
                acContact = await acClient.PostUpsertContactAsync(new AcContact
                                                                  {
                                                                      Deleted = "0",
                                                                      Email = contactEmail,
                                                                      FirstName = workspaceOwner.FirstName.Coalesce(defaultPublisherAccount?.FullName.LeftPart(' ')),
                                                                      LastName = workspaceOwner.LastName.Coalesce(defaultPublisherAccount?.FullName.LastRightPart(' ')),
                                                                      Phone = workspaceOwner.PhoneNumber
                                                                  });

                await foreach (var acAutomations in acClient.GetAutomationsContainingAsync("RydrApi"))
                {
                    foreach (var acAutomation in acAutomations)
                    {
                        await acClient.PostContactAutomationAsync(new AcContactAutomation
                                                                  {
                                                                      Contact = acContact.Id,
                                                                      Automation = acAutomation.Id
                                                                  });
                    }
                }
            }

            if ((acContact?.Id).HasValue() && !workspace.ActiveCampaignCustomerId.EqualsOrdinalCi(acContact.Id))
            {
                await _workspaceService.UpdateWorkspaceAsync(workspace, () => new DynWorkspace
                                                                              {
                                                                                  ActiveCampaignCustomerId = acContact.Id
                                                                              });
            }

            _deferRequestsService.DeferFifoRequest(new PostTrackEventNotification
                                                   {
                                                       EventName = nameof(WorkspacePosted),
                                                       EventData = workspace.WorkspaceType.ToString(),
                                                       UserEmail = contactEmail,
                                                       RelatedUpdateItems = new List<ExternalCrmUpdateItem>
                                                                            {
                                                                                new ExternalCrmUpdateItem
                                                                                {
                                                                                    FieldName = workspace.WorkspaceType == WorkspaceType.Personal
                                                                                                    ? "PersonalWorkspaceIds"
                                                                                                    : "TeamWorkspaceIds",
                                                                                    FieldValue = workspace.Id.ToStringInvariant()
                                                                                }
                                                                            }
                                                   });
        }
    }
}
