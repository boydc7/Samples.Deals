using System.Runtime.Serialization;
using Amazon.DynamoDBv2.DataModel;
using Rydr.Api.Core.Enums;
using Rydr.Api.Core.Extensions;
using Rydr.Api.Core.Models.Internal;
using Rydr.Api.Core.Models.Supporting;
using Rydr.Api.Core.Services;
using Rydr.Api.Dto;
using Rydr.Api.Dto.Enums;
using Rydr.Api.QueryDto.Shared;
using ServiceStack;
using ServiceStack.DataAnnotations;

// ReSharper disable ValueParameterNotUsed
// ReSharper disable UnusedMember.Global

namespace Rydr.Api.QueryDto;

// Deals (and request info) the user has requested (i.e. typically an influencer making this call)
// Used directly when getting requests for a specific deal (i.e. a DealId is specified), but not used when getting requests
// for the publisher over all deals (see QueryRequestedDealsByPublisherId below for that).
[Route("/query/requesteddeals")]
public class QueryRequestedDeals : BaseQueryDataRequest<DynItemIdTypeReferenceGlobalIndex>, IReturn<RydrQueryResponse<DealResponse>>, IGet
{
    private string[] _statusIds;

    [DynamoDBIgnore]
    public DealRequestStatus[] Status { get; set; }

    [Ignore]
    [IgnoreDataMember]
    public string[] StatusId
    {
        get => Status.IsNullOrEmpty()
                   ? null
                   : (_statusIds ??= Status.Select(s => s.ToString()).ToArray());
        set
        { /* nothing to do */
        }
    }

    [DynamoDBIgnore]
    public long? DealId
    {
        get => null;
        set => Id = value.HasValue && value.Value > 0
                        ? value.Value
                        : null;
    }

    [DynamoDBIgnore]
    public long? PublisherAccountId
    {
        get => null;
        set => EdgeId = value?.ToEdgeId();
    }

    [DynamoDBIgnore]
    public DateTime? RequestedOnBefore
    {
        get => null;
        set
        {
            if (value.HasValue)
            {
                TypeReferenceBetween = new[]
                                       {
                                           string.Concat((int)DynItemType.DealRequest, "|", value.Value.ToUnixTimestamp().ToStringInvariant()), string.Concat((int)DynItemType.DealRequest, "|3000000000")
                                       };
            }
            else
            {
                TypeReferenceBetween = DynamoDealRequestService.DealRequestTypeRefBetweenMinMax;
            }
        }
    }

    [Ignore]
    [IgnoreDataMember]
    public long? Id { get; set; }

    [Ignore]
    [IgnoreDataMember]
    public string EdgeId { get; set; }

    [Ignore]
    [IgnoreDataMember]
    public override string OrderByDesc
    {
        get => "TypeReference";
        set
        { /* nothing to do */
        }
    }

    [Ignore]
    [IgnoreDataMember]
    public string[] TypeReferenceBetween { get; private set; }

    // Interface/base abstract implementations
    [Ignore]
    [IgnoreDataMember]
    [DynamoDBIgnore]
    public override DynItemType QueryDynItemType => DynItemType.DealRequest;
}

// Used when hitting QueryRequestedDeals without a DealId specified - queries by EdgeId (PublisherAccount making the query request)
// for all deals requested by the user currently logged in (i.e. the current PublisherAccount). TypeReferenceStartsWith is specified
// to ensure we specify a filter value on the RangeKey for that index, which ensures records are returned in descending order
// of ReferenceId basically, which for DynDealRequests is the time that the request was made
public class QueryRequestedDealsByPublisherId : BaseQueryDataRequest<DynItemEdgeIdGlobalIndex>, IGet
{
    public string EdgeId { get; set; }

    [Ignore]
    [IgnoreDataMember]
    public string[] StatusId { get; set; }

    [Ignore]
    [IgnoreDataMember]
    [DynamoDBIgnore]
    public DateTime? RequestedBefore
    {
        get => null;
        set
        {
            if (value.HasValue)
            {
                TypeReferenceBetween = new[]
                                       {
                                           string.Concat((int)DynItemType.DealRequest, "|", value.Value.ToUnixTimestamp().ToStringInvariant()), string.Concat((int)DynItemType.DealRequest, "|3000000000")
                                       };
            }
            else
            {
                TypeReferenceBetween = DynamoDealRequestService.DealRequestTypeRefBetweenMinMax;
            }
        }
    }

    [Ignore]
    [IgnoreDataMember]
    public string[] TypeReferenceBetween { get; private set; }

    [Ignore]
    [IgnoreDataMember]
    public override string OrderByDesc
    {
        get => "TypeReference";
        set
        { /* nothing to do */
        }
    }

    // Interface/base abstract implementations
    [Ignore]
    [IgnoreDataMember]
    [DynamoDBIgnore]
    public override DynItemType QueryDynItemType => DynItemType.DealRequest;
}

[Route("/query/dealrequests")] // Requests for deals the user has created/owns (i.e. typically a business making this call)
public class QueryDealRequests : BaseQueryDataRequest<DynItemTypeOwnerSpaceReferenceGlobalIndex>, IReturn<RydrQueryResponse<DealResponse>>, IGet
{
    private string[] _statusIds;

    [DynamoDBIgnore]
    public DealRequestStatus[] Status { get; set; }

    [DynamoDBIgnore]
    public long? DealId
    {
        get => null;
        set => Id = value;
    }

    [DynamoDBIgnore]
    public long? DealRequestPublisherAccountId
    {
        get => null;
        set => EdgeId = value.ToEdgeId();
    }

    [DynamoDBIgnore]
    public bool WasInvited { get; set; }

    [DynamoDBIgnore]
    public string Search { get; set; }

    [Ignore]
    [IgnoreDataMember]
    public string[] StatusId
    {
        get => Status.IsNullOrEmpty()
                   ? null
                   : (_statusIds ??= Status.Select(s => s.ToString()).ToArray());
        set
        { /* nothing to do */
        }
    }

    [Ignore]
    [IgnoreDataMember]
    public string[] ReferenceIdBetween { get; set; }

    // Id on DynDealRequest == DealId...when used we convert the query to a QueryRequestedDeals request
    [Ignore]
    [IgnoreDataMember]
    public long? Id { get; set; }

    // EdgeId on DynDealRequest == publisher account id of the deal requester (i.e. the influencer)
    [Ignore]
    [IgnoreDataMember]
    public string EdgeId { get; set; }

    // OwnerId on DynDealRequest == publisher account id of the deal creator (i.e. the business)
    [Ignore]
    [IgnoreDataMember]
    public long? OwnerId { get; set; }

    [Ignore]
    [IgnoreDataMember]
    public string TypeOwnerSpace { get; set; }

    // Interface/base abstract implementations
    [Ignore]
    [IgnoreDataMember]
    [DynamoDBIgnore]
    public override DynItemType QueryDynItemType => DynItemType.DealRequest;

    [Ignore]
    [IgnoreDataMember]
    public override string OrderByDesc
    {
        get => "ReferenceId";
        set
        { /* nothing to do */
        }
    }
}
