using ServiceStack.Aws.DynamoDb;

namespace Rydr.Api.Core.Interfaces.DataAccess;

public interface IDynamoGlobalSecondaryIndex
{
    DynamoId GetDynamoId();
}

public interface IDynItemGlobalSecondaryIndex : IDynamoGlobalSecondaryIndex
{
    public long Id { get; set; }
    public string EdgeId { get; set; }
    public long WorkspaceId { get; set; }
    public long OwnerId { get; set; }
    public long CreatedBy { get; set; }
    public long CreatedWorkspaceId { get; set; }
    public long ModifiedBy { get; set; }
    public long ModifiedWorkspaceId { get; set; }
    public int TypeId { get; set; }
    public string StatusId { get; set; }
    public long? DeletedOnUtc { get; set; }
}
