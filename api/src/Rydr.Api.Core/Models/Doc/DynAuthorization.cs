using System.Runtime.Serialization;
using Amazon.DynamoDBv2.DataModel;
using Rydr.Api.Core.Extensions;
using Rydr.Api.Core.Interfaces.DataAccess;
using Rydr.FbSdk.Extensions;
using ServiceStack.Aws.DynamoDb;
using ServiceStack.DataAnnotations;

namespace Rydr.Api.Core.Models.Doc
{
    public class DynAuthorization : DynItem, IDynItemGlobalSecondaryIndex
    {
        // Hash/Id : Source of the authorization, usually a ContactId
        // Range/Edge : Combo of AuthorizationType and ToRecordId (target of the authorization), i.e. ContactId is authorized to access *****
        // RefId: Time last authorized

        public const string DefaultAuthorizationType = "AuthorizedTo";

        [Ignore]
        [IgnoreDataMember]
        [DynamoDBIgnore]
        public long FromRecordId
        {
            get => Id;
            set => Id = value;
        }

        public long ToRecordId { get; set; }
        public string AuthorizationType { get; set; }

        public long GetToRecordIdFromEdgeId() => GetToRecordIdFromEdgeId(EdgeId);

        public static string BuildEdgeId(long toRecordId, string authType = null)
            => string.Concat(authType.Coalesce(DefaultAuthorizationType), "|", toRecordId);

        public static long GetToRecordIdFromEdgeId(string edgeId) => edgeId.Right(edgeId.Length - edgeId.LastIndexOf('|') - 1)
                                                                           .ToLong();

        public DynamoId GetDynamoId() => new DynamoId(Id, EdgeId);
    }
}
