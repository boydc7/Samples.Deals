using Rydr.Api.Core.Enums;
using Rydr.Api.Dto;
using Rydr.Api.Services.Helpers;
using ServiceStack.FluentValidation;

namespace Rydr.Api.Services.Validators;

public class GetActiveWorkspaceSubscriptionValidator : BaseGetRequestValidator<GetActiveWorkspaceSubscription>
{
    public GetActiveWorkspaceSubscriptionValidator()
    {
        RuleFor(e => e.ToDynItemValidationSource(e.Id, DynItemType.Workspace, ApplyToBehavior.Default))
            .IsValidDynamoItem()
            .When(e => e.Id > 0);
    }
}

// INTERNAL - simple
public class WorkspaceSubscriptionDeletedValidator : BaseRydrValidator<WorkspaceSubscriptionDeleted>
{
    public WorkspaceSubscriptionDeletedValidator()
    {
        RuleFor(e => e.SubscriptionId)
            .NotEmpty();

        RuleFor(e => e.SubscriptionWorkspaceId)
            .GreaterThan(0);
    }
}

public class WorkspacePublisherSubscriptionDeletedValidator : BaseRydrValidator<WorkspacePublisherSubscriptionDeleted>
{
    public WorkspacePublisherSubscriptionDeletedValidator()
    {
        RuleFor(e => e.PublisherAccountId)
            .GreaterThan(0);

        RuleFor(e => e.SubscriptionWorkspaceId)
            .GreaterThan(0);

        RuleFor(e => e.DynWorkspaceSubscriptionId)
            .GreaterThan(0);
    }
}

public class WorkspaceSubscriptionUpdatedValidator : BaseRydrValidator<WorkspaceSubscriptionUpdated>
{
    public WorkspaceSubscriptionUpdatedValidator()
    {
        RuleFor(e => e.SubscriptionId)
            .NotEmpty();

        RuleFor(e => e.SubscriptionWorkspaceId)
            .GreaterThan(0);
    }
}

public class SubscriptionUsageIncrementedValidator : BaseRydrValidator<SubscriptionUsageIncremented>
{
    public SubscriptionUsageIncrementedValidator()
    {
        RuleFor(e => e.SubscriptionId)
            .NotEmpty();

        RuleFor(e => e.CustomerId)
            .NotEmpty();

        RuleFor(e => e.SubscriptionWorkspaceId)
            .GreaterThan(0);

        RuleFor(e => e.ManagedPublisherAccountId)
            .GreaterThan(0);

        RuleFor(e => e.UsageTimestamp)
            .GreaterThan(0);
    }
}

public class WorkspaceSubscriptionCreatedValidator : BaseRydrValidator<WorkspaceSubscriptionCreated>
{
    public WorkspaceSubscriptionCreatedValidator()
    {
        RuleFor(e => e.SubscriptionId)
            .NotEmpty();

        RuleFor(e => e.SubscriptionWorkspaceId)
            .GreaterThan(0);
    }
}
