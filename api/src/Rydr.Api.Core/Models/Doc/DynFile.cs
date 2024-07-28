using System.Runtime.Serialization;
using Amazon.DynamoDBv2.DataModel;
using Rydr.Api.Dto.Enums;
using Rydr.Api.Dto.Interfaces;
using ServiceStack.Aws.DynamoDb;
using ServiceStack.DataAnnotations;

namespace Rydr.Api.Core.Models.Doc;

public class DynFile : DynItem, IHasNameAndIsRecordLookup
{
    // Hash / Id : Unique id for the file
    // Range / Edge: Unique id for the file (same as hash)
    // RefId: UnixTimestamp when the file was created/modified
    // OwnerId:
    // WorkspaceId: Workspace that owns the file
    // StatusId:

    [Required]
    [StringLength(100)]
    [ExcludeNullValue]
    public string Name { get; set; }

    [StringLength(1000)]
    [ExcludeNullValue]
    public string Description { get; set; }

    [StringLength(10)]
    [ExcludeNullValue]
    public string FileExtension { get; set; }

    [Required]
    [StringLength(100)]
    [ExcludeNullValue]
    public string OriginalFileName { get; set; }

    [Required]
    [CheckConstraint("FileType > 0")]
    public FileType FileType { get; set; }

    [Required]
    public long ContentLength { get; set; }

    public FileConvertStatus ConvertStatus { get; set; }

    [Ignore]
    [IgnoreDataMember]
    [DynamoDBIgnore]
    public bool Uploaded => ExpiresAt <= 0;
}
