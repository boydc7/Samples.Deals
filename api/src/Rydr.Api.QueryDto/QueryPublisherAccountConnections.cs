using System;
using System.Runtime.Serialization;
using Amazon.DynamoDBv2.DataModel;
using Rydr.Api.Core.Enums;
using Rydr.Api.Core.Models.Doc;
using Rydr.Api.Dto.Enums;
using Rydr.Api.Dto.Publishers;
using Rydr.Api.QueryDto.Shared;
using ServiceStack;
using ServiceStack.DataAnnotations;

namespace Rydr.Api.QueryDto
{
    [Route("/query/pubacctconnections")]
    public class QueryPublisherAccountConnections : BaseQueryDataRequest<DynAuthorization>, IReturn<QueryResponse<PublisherAccountInfo>>, IGet
    {
        [DynamoDBIgnore]
        public long? FromPublisherAccountId { get; set; }

        [DynamoDBIgnore]
        public long? ToPublisherAccountId { get; set; }

        [DynamoDBIgnore]
        public DateTime? LastConnectedAfter { get; set; }

        [DynamoDBIgnore]
        public DateTime? LastConnectedBefore { get; set; }

        [DynamoDBIgnore]
        public PublisherAccountConnectionType[] ConnectionTypes { get; set; }

        [Ignore]
        [IgnoreDataMember]
        public string[] ReferenceIdBetween { get; set; }

        [Ignore]
        [IgnoreDataMember]
        public long Id { get; set; }

        [Ignore]
        [IgnoreDataMember]
        public string EdgeId { get; set; }

        [Ignore]
        [IgnoreDataMember]
        public string EdgeIdStartsWith { get; set; }

        [Ignore]
        [IgnoreDataMember]
        public override string OrderByDesc
        {
            get => "ReferenceId";
            set
            { /* nothing to do */
            }
        }

        // Interface/base abstract implementations
        [Ignore]
        [IgnoreDataMember]
        [DynamoDBIgnore]
        public override DynItemType QueryDynItemType => DynItemType.Authorization;
    }
}
