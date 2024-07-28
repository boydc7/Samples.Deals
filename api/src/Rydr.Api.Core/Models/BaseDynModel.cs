using System.Runtime.Serialization;
using Amazon.DynamoDBv2.DataModel;
using Rydr.Api.Core.Extensions;
using Rydr.Api.Core.Models.Supporting;
using Rydr.Api.Core.Services.Internal;
using Rydr.Api.Dto.Enums;
using Rydr.Api.Dto.Interfaces;
using ServiceStack.DataAnnotations;
using ServiceStack.Model;

namespace Rydr.Api.Core.Models;

public abstract class BaseDynModel : IDateTimeDeleteTracked, IDateTimeTracked, ICanBeAuthorized
{
    [Required]
    [IgnorePopulateExisting]
    public long CreatedBy { get; set; }

    [Required]
    [IgnorePopulateExisting]
    public long CreatedWorkspaceId { get; set; }

    [Required]
    public long ModifiedBy { get; set; }

    [Required]
    public long ModifiedWorkspaceId { get; set; }

    [Required]
    [IgnorePopulateExisting]
    public long CreatedOnUtc { get; set; }

    [Required]
    public long ModifiedOnUtc { get; set; }

    public long? DeletedOnUtc { get; set; }

    public long? DeletedBy { get; set; }

    public long? DeletedByWorkspaceId { get; set; }

    [Required]
    [IgnorePopulateExisting]
    public long WorkspaceId { get; set; }

    [Required]
    [IgnorePopulateExisting]
    public long OwnerId { get; set; }

    [Ignore]
    [IgnoreDataMember]
    [DynamoDBIgnore]
    [IgnorePopulateExisting]
    public DateTime CreatedOn
    {
        get => CreatedOnUtc <= 0
                   ? default
                   : CreatedOnUtc.ToDateTime();
        set => CreatedOnUtc = value.ToUnixTimestamp();
    }

    [Ignore]
    [IgnoreDataMember]
    [DynamoDBIgnore]
    public DateTime ModifiedOn
    {
        get => ModifiedOnUtc <= 0
                   ? default
                   : ModifiedOnUtc.ToDateTime();
        set => ModifiedOnUtc = value.ToUnixTimestamp();
    }

    [Ignore]
    [IgnoreDataMember]
    [DynamoDBIgnore]
    public DateTime? DeletedOn
    {
        get => DeletedOnUtc.GetValueOrDefault() <= 0
                   ? null
                   : DeletedOnUtc.ToDateTime();
        set => DeletedOnUtc = value == null || value < DateTimeHelper.MinApplicationDate
                                  ? null
                                  : value.ToUnixTimestamp();
    }

    public bool IsDeleted() => DeletedOnUtc.HasValue && DeletedOnUtc.Value > 0;

    public virtual string UnsetNameToPropertyName(string unsetName) => unsetName;

    public virtual AccessIntent DefaultAccessIntent() => AccessIntent.Unspecified;
    public virtual bool IsPubliclyReadable() => false;
}

public abstract class BaseDynModel<TIdType> : BaseDynModel, IHasId<TIdType>
{
    [Required]
    [PrimaryKey]
    [IgnorePopulateExisting]
    public virtual TIdType Id { get; set; }
}

public abstract class BaseDynLongModel : BaseDynModel<long>, IHasLongId
{
    [Required]
    [PrimaryKey]
    [HashKey]
    [IgnorePopulateExisting]
    public override long Id { get; set; }
}
