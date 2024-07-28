namespace Rydr.Api.Core.Models.Supporting;

public static class GlobalItemIds
{
    // NOTE: We use a starting global item value for all operations at 125150

    public const long FileObjectLabel = 100;
    public const long PublicWorkspaceId = 101;
    public const long AuthAdminUserId = 102;
    public const long AuthAdminWorkspaceId = 103;
    public const long AuthRydrWorkspaceId = 104;
    public const long PublicOwnerId = 105;
    public const long NullPublisherAppId = 106;

    // 120,000 and up are taken, do not use (static identifiers for test, local accounts)
    // 120001-120004 = workspaces...(see workspaceService.CreateAsync)
    // 120005-120010 = root publisher accounts...(see publisherAccountService.ConnectPublisherAccountAsync)

    public const long MinUserDefinedObjectId = 125151;
}
