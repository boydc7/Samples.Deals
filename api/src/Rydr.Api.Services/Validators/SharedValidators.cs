using Rydr.Api.Core.Configuration;
using Rydr.Api.Core.Enums;
using Rydr.Api.Core.Extensions;
using Rydr.Api.Core.Interfaces.Services;
using Rydr.Api.Core.Models.Doc;
using Rydr.Api.Core.Services.Internal;
using Rydr.Api.Dto.Enums;
using Rydr.Api.Dto.Interfaces;
using Rydr.Api.Dto.Shared;
using Rydr.Api.Services.Helpers;
using ServiceStack;
using ServiceStack.Aws.DynamoDb;
using ServiceStack.FluentValidation;
using LongRange = Rydr.Api.Dto.Shared.LongRange;

namespace Rydr.Api.Services.Validators;

public class LongRangeValidator : AbstractValidator<LongRange>
{
    public LongRangeValidator()
    {
        RuleFor(e => e.Min)
            .GreaterThanOrEqualTo(0)
            .LessThanOrEqualTo(e => e.Max);

        RuleFor(e => e.Max)
            .GreaterThanOrEqualTo(0)
            .GreaterThanOrEqualTo(e => e.Min);
    }
}

public class DoubleRangeValidator : AbstractValidator<DoubleRange>
{
    public DoubleRangeValidator()
    {
        RuleFor(e => e.Min)
            .GreaterThanOrEqualTo(0)
            .LessThanOrEqualTo(e => e.Max);

        RuleFor(e => e.Max)
            .GreaterThanOrEqualTo(0)
            .GreaterThanOrEqualTo(e => e.Min);
    }
}

public class DateTimeAttributeValidator : AbstractValidator<DateTime>
{
    public DateTimeAttributeValidator(string name = null, bool canBeEmpty = false)
    {
        var fieldName = name ?? "DateTime value(s)";

        RuleFor(d => d)
            .GreaterThanOrEqualTo(DateTimeHelper.MinApplicationDate)
            .When(d => !canBeEmpty)
            .WithErrorCode(ErrorCodes.MustBeValid)
            .WithName(fieldName)
            .WithMessage($"DateTime value for [{fieldName}] must specified and be a valid value (too small)");

        RuleFor(d => d)
            .Equal(default(DateTime))
            .When(d => canBeEmpty && d < DateTimeHelper.MinApplicationDate)
            .WithErrorCode(ErrorCodes.MustBeValid)
            .WithName(fieldName)
            .WithMessage($"DateTime value for [{fieldName}] must be valid if specified (too small), or not specified at all");

        RuleFor(d => d)
            .LessThanOrEqualTo(DateTimeHelper.MaxApplicationDate)
            .WithErrorCode(ErrorCodes.MustBeValid)
            .WithName(fieldName)
            .WithMessage($"DateTime value for [{fieldName}] must specified and be a valid value (too large)");

        RuleFor(d => d)
            .Must(d => d.Kind == DateTimeKind.Utc)
            .When(d => d != default)
            .WithErrorCode(ErrorCodes.MustBeValid)
            .WithName(fieldName)
            .WithMessage($"DateTime value for [{fieldName}] must be a UTC value - use the ISO 8601 string format if this is a string value");
    }
}

public class GenericListNotNullOrEmptyValidator<T> : AbstractValidator<List<T>>
{
    public GenericListNotNullOrEmptyValidator(string name = null)
    {
        var fieldName = name ?? "List value(s)";

        RuleFor(l => l)
            .Must(l => !l.IsNullOrEmpty())
            .WithName(fieldName)
            .WithErrorCode(ErrorCodes.MustBeSpecified)
            .WithMessage($"Value for [{fieldName}] must be specified and contain 1 or more values");
    }
}

public class IdListValidIfNotNullOrEmptyValidator : AbstractValidator<List<long>>
{
    public IdListValidIfNotNullOrEmptyValidator(string name = null)
    {
        var fieldName = name ?? "List value(s)";

        RuleFor(l => l)
            .Must(l => l.TrueForAll(i => i > 0))
            .When(l => l != null && l.Count > 0)
            .WithName(fieldName)
            .WithErrorCode(ErrorCodes.MustBeValid)
            .WithMessage($"Values specified for [{fieldName}] must all be valid (>0)");
    }
}

public class GenericListNullOrNotEmptyValidator<T> : AbstractValidator<List<T>>
{
    public GenericListNullOrNotEmptyValidator(string name = null)
    {
        var fieldName = name ?? "List value(s)";

        RuleFor(l => l)
            .Must(l => l == null || l.Count > 0)
            .WithName(fieldName)
            .WithErrorCode(ErrorCodes.MustBeValid)
            .WithMessage($"Value for [{fieldName}] must either not be specified OR be specified and contain 1 or more values");
    }
}

public class DynItemExistsValidator<T> : AbstractValidator<DynItemValidationSource<T>>
    where T : DynItem
{
    // ReSharper disable once PrivateFieldCanBeConvertedToLocalVariable
    private static readonly Dictionary<DynItemType, Func<DynItemValidationSource<T>, Task<T>>> _customLookupMap =
        new()
        {
            {
                DynItemType.PublisherAccount, s => PublisherExtensions.DefaultPublisherAccountService
                                                                      .TryGetPublisherAccountAsync(s.Id)
                                                                      .Then(p => p is T pt
                                                                                     ? pt
                                                                                     : null)
            },
            {
                DynItemType.User, s => UserExtensions.DefaultUserService
                                                     .TryGetUserAsync(s.Id)
                                                     .Then(p => p is T pt
                                                                    ? pt
                                                                    : null)
            },
            {
                DynItemType.Workspace, s => WorkspaceService.DefaultWorkspaceService
                                                            .TryGetWorkspaceAsync(s.Id)
                                                            .Then(p => p is T pt
                                                                           ? pt
                                                                           : null)
            },
        };

    public DynItemExistsValidator(IPocoDynamo dynamoDb)
    {
        RuleFor(e => e.Type)
            .NotEmpty()
            .NotEqual(DynItemType.Null)
            .WithErrorCode(ErrorCodes.MustBeSpecified);

        // Generally used when something is being GETed/PUTed/DELETEed - i.e. something needs to be looked up and ensure exists, or could exist, etc.
        void idLookupAttributesAreValidRules(ApplyToBehavior treatLike)
        {
            RuleFor(e => e.Id)
                .GreaterThan(0)
                .When(e => e.TreatLike == treatLike)
                .When(e => e.EdgeId.IsNullOrEmpty())
                .WithErrorCode(ErrorCodes.MustBeSpecified);

            RuleFor(e => e.EdgeId)
                .NotEmpty()
                .When(e => !e.ReferenceId.HasValue())
                .When(e => e.TreatLike == treatLike)
                .WithErrorCode(ErrorCodes.MustBeSpecified)
                .WithMessage("Edge or Ref required");

            RuleFor(e => e.ReferenceId)
                .NotEmpty()
                .When(e => !e.EdgeId.HasValue())
                .When(e => e.TreatLike == treatLike)
                .WithErrorCode(ErrorCodes.MustBeSpecified)
                .WithMessage("Edge or Ref required");
        }

        // Default for POST operations (i.e. inserting something) - other things must not exist already and be duplicated
        void mustNotExistRules(ApplyToBehavior treatLike)
        {
            // Must NOT exist
            RuleFor(e => e)
                .MustAsync(async (e, t) =>
                           {
                               var dynItemInfo = _customLookupMap.ContainsKey(e.Type)
                                                     ? await _customLookupMap[e.Type](e)
                                                     : await dynamoDb.GetItemAsync<T>(e.Id, e.EdgeId);

                               return dynItemInfo == null;
                           })
                .When(e => e.TreatLike == treatLike)
                .When(e => e.Id > 0 && e.EdgeId.HasValue())
                .WithErrorCode(ErrorCodes.AlreadyExists)
                .WithName("Resource")
                .WithMessage(e => $"Resource already exists (id/edge/[{(int)e.Type}]).");

            RuleFor(e => e)
                .MustAsync(async (e, t) =>
                           {
                               var dynItem = _customLookupMap.ContainsKey(e.Type)
                                                 ? await _customLookupMap[e.Type](e)
                                                 : await dynamoDb.GetItemByRefAsync<T>(e.Id, e.ReferenceId, e.Type, true, true);

                               return dynItem == null;
                           })
                .When(e => e.TreatLike == treatLike)
                .When(e => e.Id > 0 && e.ReferenceId.HasValue())
                .WithErrorCode(ErrorCodes.AlreadyExists)
                .WithName("Resource")
                .WithMessage(e => $"Resource already exists (id/ref/[{(int)e.Type}]).");

            RuleFor(e => e)
                .MustAsync(async (e, t) =>
                           {
                               // NOTE: Cannot use custom lookup unlesss and ID is included
                               // var dynIndexItem = _customLookupMap.ContainsKey(e.Type)
                               //                        ? await _customLookupMap[e.Type](e)
                               //                        : await dynamoDb.GetItemByEdgeIntoAsync<T>(e.Type, e.EdgeId, true);
                               var dynIndexItem = await dynamoDb.GetItemByEdgeIntoAsync<T>(e.Type, e.EdgeId, true);

                               return dynIndexItem == null;
                           })
                .When(e => e.TreatLike == treatLike)
                .When(e => e.Id <= 0 && e.EdgeId.HasValue())
                .WithErrorCode(ErrorCodes.AlreadyExists)
                .WithName("Resource")
                .WithMessage(e => $"Resource already exists (edge/[{(int)e.Type}]).");
        }

        // Default for PUT/DELETE operations (i.e. must be able to be updated or deleted, so must exist in a non-deleted state for example)
        void canOrMustExistNonDeletedRules(ApplyToBehavior treatLike)
        {
            // Must exist and not be deleted
            RuleFor(e => e)
                .MustAsync(async (e, t) =>
                           {
                               var dynItem = _customLookupMap.ContainsKey(e.Type)
                                                 ? await _customLookupMap[e.Type](e)
                                                 : await dynamoDb.GetItemAsync<T>(e.Id, e.EdgeId);

                               if (dynItem != null && (dynItem.DynItemType != e.Type || dynItem.IsDeleted()))
                               { // Applies everywhere...cannot be a mismatched type and cannot be deleted if it in fact exists
                                   return false;
                               }

                               // The default behavior, MUST exist and not be deleted
                               // Non-default...CAN exist (but does not have to), however if it does it must not be deleted and we must have access to it
                               var isValid = e.TreatLike == ApplyToBehavior.MustExistNotDeleted || e.TreatLike == ApplyToBehavior.Default
                                                 ? dynItem != null && (e.Request == null ||
                                                                       e.SkipAccessChecks ||
                                                                       e.Request.IsSystemRequest ||
                                                                       Request.IsRydrRequest() ||
                                                                       await e.Request.HasAccessToAsync(dynItem))
                                                 : dynItem == null || e.Request == null ||
                                                   e.SkipAccessChecks ||
                                                   e.Request.IsSystemRequest ||
                                                   Request.IsRydrRequest() ||
                                                   await e.Request.HasAccessToAsync(dynItem);

                               return isValid && (e.AlsoMust == null || e.AlsoMust(dynItem));
                           })
                .When(e => e.TreatLike == treatLike)
                .When(e => e.Id > 0 && e.EdgeId.HasValue())
                .WithErrorCode(ErrorCodes.MustBeAuthorized)
                .WithName("Resource")
                .WithMessage(e => $"Record was not found or you do not have access to it (id/edge/[{(int)e.Type}]).");

            RuleFor(e => e)
                .MustAsync(async (e, t) =>
                           {
                               var dynItem = _customLookupMap.ContainsKey(e.Type)
                                                 ? await _customLookupMap[e.Type](e)
                                                 : await dynamoDb.GetItemByRefAsync<T>(e.Id, e.ReferenceId, e.Type, ignoreRecordNotFound: true);

                               if (dynItem != null && (dynItem.DynItemType != e.Type || dynItem.IsDeleted()))
                               { // Applies everywhere...cannot be a mismatched type and cannot be deleted if it in fact exists
                                   return false;
                               }

                               // The default behavior, MUST exist and not be deleted
                               // Non-default...CAN exist (but does not have to), however if it does it must not be deleted and we must have access to it
                               var isValid = e.TreatLike == ApplyToBehavior.MustExistNotDeleted || e.TreatLike == ApplyToBehavior.Default
                                                 ? dynItem != null && (e.Request == null ||
                                                                       e.SkipAccessChecks ||
                                                                       e.Request.IsSystemRequest ||
                                                                       Request.IsRydrRequest() ||
                                                                       await e.Request.HasAccessToAsync(dynItem))
                                                 : dynItem == null ||
                                                   e.Request == null ||
                                                   e.SkipAccessChecks ||
                                                   e.Request.IsSystemRequest ||
                                                   Request.IsRydrRequest() ||
                                                   await e.Request.HasAccessToAsync(dynItem);

                               return isValid && (e.AlsoMust == null || e.AlsoMust(dynItem));
                           })
                .When(e => e.TreatLike == treatLike)
                .When(e => e.Id > 0 && e.ReferenceId.HasValue())
                .WithErrorCode(ErrorCodes.MustBeAuthorized)
                .WithName("Resource")
                .WithMessage(e => $"Record was not found or you do not have access to it (id/ref/[{(int)e.Type}]).");

            RuleFor(e => e)
                .MustAsync(async (e, t) =>
                           {
                               // NOTE: Cannot use custom lookup unlesss and ID is included
                               // var dynIndexItem = _customLookupMap.ContainsKey(e.Type)
                               //                        ? await _customLookupMap[e.Type](e)
                               //                        : await dynamoDb.GetItemByEdgeIntoAsync<T>(e.Type, e.EdgeId, true);
                               var dynIndexItem = await dynamoDb.GetItemByEdgeIntoAsync<T>(e.Type, e.EdgeId, true);

                               if (dynIndexItem != null && (dynIndexItem.DynItemType != e.Type || dynIndexItem.DeletedOnUtc.HasValue))
                               { // Applies everywhere...cannot be a mismatched type and cannot be deleted if it in fact exists
                                   return false;
                               }

                               // The default behavior, MUST exist and not be deleted
                               // Non-default...CAN exist (but does not have to), however if it does it must not be deleted and we must have access to it
                               var isValid = e.TreatLike == ApplyToBehavior.MustExistNotDeleted || e.TreatLike == ApplyToBehavior.Default
                                                 ? dynIndexItem != null && (e.Request == null ||
                                                                            e.SkipAccessChecks ||
                                                                            e.Request.IsSystemRequest ||
                                                                            Request.IsRydrRequest() ||
                                                                            await e.Request.HasAccessToAsync(dynIndexItem))
                                                 : dynIndexItem == null ||
                                                   e.Request == null ||
                                                   e.SkipAccessChecks ||
                                                   e.Request.IsSystemRequest ||
                                                   Request.IsRydrRequest() ||
                                                   await e.Request.HasAccessToAsync(dynIndexItem);

                               return isValid && (e.AlsoMust == null || e.AlsoMust(dynIndexItem));
                           })
                .When(e => e.TreatLike == treatLike)
                .When(e => e.Id <= 0 && e.EdgeId.HasValue())
                .WithErrorCode(ErrorCodes.MustBeAuthorized)
                .WithName("Resource")
                .WithMessage(e => $"Record was not found or you do not have access to it (edge/[{(int)e.Type}]).");
        }

        // Default for GET operations - i.e. get me a specific thing, can be deleted though
        void mustExistCanBeDeletedRules(ApplyToBehavior treatLike)
        {
            // Must exist, but can be deleted
            RuleFor(e => e)
                .MustAsync(async (e, t) =>
                           {
                               var dynItemInfo = _customLookupMap.ContainsKey(e.Type)
                                                     ? await _customLookupMap[e.Type](e)
                                                     : await dynamoDb.GetItemAsync<T>(e.Id, e.EdgeId);

                               var isValid = dynItemInfo != null &&
                                             dynItemInfo.DynItemType == e.Type &&
                                             (e.Request == null ||
                                              e.SkipAccessChecks ||
                                              e.Request.IsSystemRequest ||
                                              Request.IsRydrRequest() ||
                                              await e.Request.HasAccessToAsync(dynItemInfo));

                               return isValid && (e.AlsoMust == null || e.AlsoMust(dynItemInfo));
                           })
                .When(e => e.TreatLike == treatLike)
                .When(e => e.Id > 0 && e.EdgeId.HasValue())
                .WithErrorCode(ErrorCodes.MustBeAuthorized)
                .WithName("Resource")
                .WithMessage(e => $"Record was not found or you do not have access to it (id/edge/[{(int)e.Type}]).");

            RuleFor(e => e)
                .MustAsync(async (e, t) =>
                           {
                               var dynItem = _customLookupMap.ContainsKey(e.Type)
                                                 ? await _customLookupMap[e.Type](e)
                                                 : await dynamoDb.GetItemByRefAsync<T>(e.Id, e.ReferenceId, e.Type, true, true);

                               var isValid = dynItem != null &&
                                             dynItem.DynItemType == e.Type &&
                                             (e.Request == null ||
                                              e.SkipAccessChecks ||
                                              e.Request.IsSystemRequest ||
                                              Request.IsRydrRequest() ||
                                              await e.Request.HasAccessToAsync(dynItem));

                               return isValid && (e.AlsoMust == null || e.AlsoMust(dynItem));
                           })
                .When(e => e.TreatLike == treatLike)
                .When(e => e.Id > 0 && e.ReferenceId.HasValue())
                .WithErrorCode(ErrorCodes.MustBeAuthorized)
                .WithName("Resource")
                .WithMessage(e => $"Record was not found or you do not have access to it (id/ref/[{(int)e.Type}]).");

            RuleFor(e => e)
                .MustAsync(async (e, t) =>
                           {
                               // NOTE: Cannot use custom lookup unlesss and ID is included
                               // var dynIndexItem = _customLookupMap.ContainsKey(e.Type)
                               //                        ? await _customLookupMap[e.Type](e)
                               //                        : await dynamoDb.GetItemByEdgeIntoAsync<T>(e.Type, e.EdgeId, true);
                               var dynIndexItem = await dynamoDb.GetItemByEdgeIntoAsync<T>(e.Type, e.EdgeId, true);

                               var isValid = dynIndexItem != null &&
                                             dynIndexItem.DynItemType == e.Type &&
                                             (e.Request == null ||
                                              e.SkipAccessChecks ||
                                              e.Request.IsSystemRequest ||
                                              Request.IsRydrRequest() ||
                                              await e.Request.HasAccessToAsync(dynIndexItem));

                               return isValid && (e.AlsoMust == null || e.AlsoMust(dynIndexItem));
                           })
                .When(e => e.TreatLike == treatLike)
                .When(e => e.Id <= 0 && e.EdgeId.HasValue())
                .WithErrorCode(ErrorCodes.MustBeAuthorized)
                .WithName("Resource")
                .WithMessage(e => $"Record was not found or you do not have access to it (edge/[{(int)e.Type}]).");
        }

        // When PUT/GET/DELETE and asked to handle default (must exist, not deleted, in order to be acted on)
        // OR
        // ANY When asked to handle as if MustExistNotDeleted or CanExistNotDeleted - in each case, ID values must be set to look something up
        RuleSet(ApplyTo.Put | ApplyTo.Get | ApplyTo.Delete, () => idLookupAttributesAreValidRules(ApplyToBehavior.Default));
        RuleSet(ApplyTo.Put | ApplyTo.Get | ApplyTo.Delete | ApplyTo.Post, () => idLookupAttributesAreValidRules(ApplyToBehavior.MustExistNotDeleted));
        RuleSet(ApplyTo.Put | ApplyTo.Get | ApplyTo.Delete | ApplyTo.Post, () => idLookupAttributesAreValidRules(ApplyToBehavior.CanExistNotDeleted));

        // When POST and handle default
        RuleSet(ApplyTo.Post, () => mustNotExistRules(ApplyToBehavior.Default));
        RuleSet(ApplyTo.Put | ApplyTo.Get | ApplyTo.Delete | ApplyTo.Post, () => mustNotExistRules(ApplyToBehavior.MustNotExist));

        // When PUT/DELETE and handle default (default being MustExistNotDeleted)
        // OR
        // ANY When asked to handle as if MustExistNotDeleted or CanExistNotDeleted
        RuleSet(ApplyTo.Put | ApplyTo.Delete, () => canOrMustExistNonDeletedRules(ApplyToBehavior.Default));
        RuleSet(ApplyTo.Put | ApplyTo.Get | ApplyTo.Delete | ApplyTo.Post, () => canOrMustExistNonDeletedRules(ApplyToBehavior.MustExistNotDeleted));
        RuleSet(ApplyTo.Put | ApplyTo.Get | ApplyTo.Delete | ApplyTo.Post, () => canOrMustExistNonDeletedRules(ApplyToBehavior.CanExistNotDeleted));

        // When GET and handle default
        RuleSet(ApplyTo.Get, () => mustExistCanBeDeletedRules(ApplyToBehavior.Default));
        RuleSet(ApplyTo.Get | ApplyTo.Post | ApplyTo.Put | ApplyTo.Delete, () => mustExistCanBeDeletedRules(ApplyToBehavior.MustExistCanBeDeleted));
    }
}

public class IsValidSkipTakeValidator : AbstractValidator<IHasSkipTake>
{
    public IsValidSkipTakeValidator()
    {
        RuleFor(e => e.Skip)
            .GreaterThanOrEqualTo(0)
            .LessThanOrEqualTo(10000);

        RuleFor(e => e.Take)
            .GreaterThanOrEqualTo(0)
            .LessThanOrEqualTo(1000);

        RuleFor(e => e.Skip + e.Take)
            .LessThanOrEqualTo(12500)
            .WithName("TotalSkipTake")
            .WithMessage("Total skip/take evaluated is too large")
            .WithErrorCode(ErrorCodes.LimitReached);
    }
}

public class AddressValidator : AbstractValidator<Address>
{
    public AddressValidator()
    {
        RuleFor(e => e.Name)
            .MaximumLength(100);

        RuleFor(e => e.Address1)
            .MaximumLength(100);

        RuleFor(e => e.City)
            .MaximumLength(100);

        RuleFor(e => e.StateProvince)
            .MaximumLength(50);

        RuleFor(e => e.PostalCode)
            .MaximumLength(100);

        RuleFor(e => e.Latitude)
            .InclusiveBetween(-90, 90)
            .When(e => e.Latitude.HasValue);

        RuleFor(e => e.Longitude)
            .InclusiveBetween(-180, 180)
            .When(e => e.Longitude.HasValue);
    }
}

public class GeoQueryValidator : AbstractValidator<IGeoQuery>
{
    public GeoQueryValidator()
    {
        RuleFor(e => e.Latitude.Value)
            .InclusiveBetween(-90, 90)
            .When(e => e.Latitude.HasValue);

        RuleFor(e => e.Latitude)
            .NotNull()
            .When(e => e.Longitude.HasValue || e.Miles.HasValue || e.BoundingBox != null)
            .WithErrorCode(ErrorCodes.MustBeSpecified)
            .WithMessage("To perform a geo-query, the Latitude, Longitude, and Miles or a BoundingBox (distance) must all be specified and valid (or all left out)");

        RuleFor(e => e.Longitude.Value)
            .InclusiveBetween(-180, 180)
            .When(e => e.Longitude.HasValue);

        RuleFor(e => e.Longitude)
            .NotNull()
            .When(e => e.Latitude.HasValue || e.Miles.HasValue || e.BoundingBox != null)
            .WithErrorCode(ErrorCodes.MustBeSpecified)
            .WithMessage("To perform a geo-query, the Latitude, Longitude, and Miles or a BoundingBox (distance) must all be specified and valid (or all left out)");

        RuleFor(e => e.Miles.Value)
            .InclusiveBetween(0.1, 300.0)
            .When(e => e.Miles.HasValue);

        RuleFor(e => e.Miles)
            .NotNull()
            .When(e => e.BoundingBox == null && (e.Latitude.HasValue || e.Longitude.HasValue))
            .WithErrorCode(ErrorCodes.MustBeSpecified)
            .WithMessage("To perform a geo-query, the Latitude, Longitude, and Miles or a BoundingBox (distance) must all be specified and valid (or all left out)");

        RuleFor(e => e.BoundingBox.NorthEastLatitude)
            .InclusiveBetween(-90, 90)
            .When(e => e.BoundingBox != null && Math.Abs(e.BoundingBox.NorthEastLatitude) > 0);

        RuleFor(e => e.BoundingBox.NorthWestLatitude)
            .InclusiveBetween(-90, 90)
            .When(e => e.BoundingBox != null && Math.Abs(e.BoundingBox.NorthWestLatitude) > 0);

        RuleFor(e => e.BoundingBox.SouthEastLatitude)
            .InclusiveBetween(-90, 90)
            .When(e => e.BoundingBox != null && Math.Abs(e.BoundingBox.SouthEastLatitude) > 0);

        RuleFor(e => e.BoundingBox.SouthWestLatitude)
            .InclusiveBetween(-90, 90)
            .When(e => e.BoundingBox != null && Math.Abs(e.BoundingBox.SouthWestLatitude) > 0);

        RuleFor(e => e.BoundingBox.NorthEastLongitude)
            .InclusiveBetween(-180, 180)
            .When(e => e.BoundingBox != null && Math.Abs(e.BoundingBox.NorthEastLongitude) > 0);

        RuleFor(e => e.BoundingBox.NorthWestLongitude)
            .InclusiveBetween(-180, 180)
            .When(e => e.BoundingBox != null && Math.Abs(e.BoundingBox.NorthWestLongitude) > 0);

        RuleFor(e => e.BoundingBox.SouthEastLongitude)
            .InclusiveBetween(-180, 180)
            .When(e => e.BoundingBox != null && Math.Abs(e.BoundingBox.SouthEastLongitude) > 0);

        RuleFor(e => e.BoundingBox.SouthWestLongitude)
            .InclusiveBetween(-180, 180)
            .When(e => e.BoundingBox != null && Math.Abs(e.BoundingBox.SouthWestLongitude) > 0);

        RuleFor(e => e.BoundingBox)
            .Must(b => (
                           (Math.Abs(b.NorthEastLatitude) > 0 && Math.Abs(b.NorthEastLongitude) > 0) &&
                           (Math.Abs(b.SouthWestLatitude) > 0 && Math.Abs(b.SouthWestLongitude) > 0)
                       )
                       ||
                       (
                           (Math.Abs(b.NorthWestLatitude) > 0 && Math.Abs(b.NorthWestLongitude) > 0) &&
                           (Math.Abs(b.SouthEastLatitude) > 0 && Math.Abs(b.SouthEastLongitude) > 0)
                       ))
            .When(e => e.BoundingBox != null)
            .WithErrorCode(ErrorCodes.MustBeValid)
            .WithMessage("A bounding box must have at least one valid combination of NorthEast or SouthWest Lat/Lon values");

        RuleFor(e => e.BoundingBox)
            .NotNull()
            .When(e => !e.Miles.HasValue && (e.Latitude.HasValue || e.Longitude.HasValue))
            .WithErrorCode(ErrorCodes.MustBeSpecified)
            .WithMessage("To perform a geo-query, the Latitude, Longitude, and Miles or a BoundingBox (distance) must all be specified and valid (or all left out)");
    }
}

public class RecordTypeIdValidator : AbstractValidator<RecordTypeId>
{
    // ReSharper disable once PrivateFieldCanBeConvertedToLocalVariable
    private static readonly IRecordTypeRecordService _recordTypeRecordService = RydrEnvironment.Container.Resolve<IRecordTypeRecordService>();

    public RecordTypeIdValidator(string fieldName = null)
    {
        RuleFor(e => e.Type)
            .NotEqual(RecordType.Unknown)
            .WithErrorCode(ErrorCodes.MustBeValid);

        RuleFor(e => e.Id)
            .GreaterThan(0)
            .WithErrorCode(ErrorCodes.MustBeValid);

        RuleFor(e => e)
            .MustAsync((t, x) => _recordTypeRecordService.HasAccessToAsync(t))
            .WithName(fieldName.Coalesce("RecordTypeId"))
            .WithErrorCode(ErrorCodes.MustBeAuthorized)
            .WithMessage("Resource does not exist or you do not have access to it");
    }
}

public class IsFromValidSubscribedRequestWorkspace<T> : AbstractValidator<T>
    where T : IRequestBase
{
    public IsFromValidSubscribedRequestWorkspace()
    {
        Include(new IsFromValidRequestWorkspaceValidator<T>());

        // Only subscribed accounts can use this endpoint at all
        RuleFor(e => e)
            .MustAsync((e, t) => e.IsSubscribedWorkspaceAsync())
            .Unless(e => e.IsSystemRequest)
            .WithErrorCode(ErrorCodes.MustBeAuthorized)
            .WithMessage("Only paid workspaces are allowed access to this functionality");
    }
}

public class IsFromValidSubscribedTeamRequestWorkspace<T> : AbstractValidator<T>
    where T : IRequestBase
{
    public IsFromValidSubscribedTeamRequestWorkspace()
    {
        Include(new IsFromValidRequestWorkspaceValidator<T>());

        // Only subscribed accounts can use this endpoint at all
        RuleFor(e => e)
            .MustAsync((e, t) => e.IsSubscribedTeamWorkspaceAsync())
            .Unless(e => e.IsSystemRequest)
            .WithErrorCode(ErrorCodes.MustBeAuthorized)
            .WithMessage("Only paid team workspaces are allowed access to this functionality");
    }
}

public class IsFromValidRequestWorkspaceValidator<T> : AbstractValidator<T>
    where T : IRequestBase
{
    public IsFromValidRequestWorkspaceValidator()
    {
        RuleFor(e => e.WorkspaceId)
            .GreaterThan(0)
            .WithErrorCode(ErrorCodes.MustBeSpecified)
            .WithMessage("Valid workspace identifier is required");

        RuleFor(e => e.ToDynItemValidationSource<DynWorkspace>(e.WorkspaceId, DynItemType.Workspace, ApplyToBehavior.MustExistNotDeleted))
            .IsValidDynamoItem()
            .When(e => e.WorkspaceId > 0);

        RuleFor(e => e.WorkspaceId)
            .MustAsync(async (wid, t) =>
                       {
                           var publisherAccount = await WorkspaceService.DefaultWorkspaceService.TryGetDefaultPublisherAccountAsync(wid);

                           if (publisherAccount != null && !publisherAccount.IsDeleted() && publisherAccount.IsTokenAccount())
                           {
                               return true;
                           }

                           // Can be a basic-only workspace without a proper token account associated
                           var workspace = await WorkspaceService.DefaultWorkspaceService.TryGetWorkspaceAsync(wid);

                           return workspace != null && !workspace.IsDeleted() && workspace.DefaultPublisherAccountId <= 0 &&
                                  !workspace.SecondaryTokenPublisherAccountIds.IsNullOrEmpty();
                       })
            .When(e => e.WorkspaceId > 0)
            .WithErrorCode(ErrorCodes.MustExist)
            .WithMessage("The workspace included has an invalid state - code [rsvrwdpantm]");

        RuleFor(e => e.RequestPublisherAccountId)
            .MustAsync((e, i, t) => AuthorizationExtensions.DefaultAuthorizeService.IsAuthorizedAsync(e.WorkspaceId, e.RequestPublisherAccountId))
            .When(e => e.RequestPublisherAccountId > 0)
            .WithErrorCode(ErrorCodes.MustBeAuthorized)
            .WithMessage("Publisher account and workspace included are invalid or are not authorized for use");
    }
}

public class IsFromValidRequestPublisherAccountValidator<T> : AbstractValidator<T>
    where T : IRequestBase
{
    public IsFromValidRequestPublisherAccountValidator(Func<IRequestBase, DynPublisherAccount, bool> alsoMust = null,
                                                       bool requestPublisherAccountIdCanBeEmpty = false)
    {
        RuleFor(e => e.RequestPublisherAccountId)
            .GreaterThan(0)
            .When(e => !requestPublisherAccountIdCanBeEmpty)
            .WithErrorCode(ErrorCodes.MustBeSpecified)
            .WithMessage("Valid publisher account identifier is required");

        RuleFor(e => e.ToDynItemValidationSourceByRef<DynPublisherAccount>(e.RequestPublisherAccountId, e.RequestPublisherAccountId.ToStringInvariant(),
                                                                           DynItemType.PublisherAccount, ApplyToBehavior.MustExistNotDeleted))
            .IsValidDynamoItem()
            .When(e => e.RequestPublisherAccountId > 0);

        RuleFor(e => e)
            .MustAsync(async (e, t) =>
                       {
                           var publisherAccount = await PublisherExtensions.DefaultPublisherAccountService
                                                                           .TryGetPublisherAccountAsync(e.RequestPublisherAccountId);

                           return publisherAccount != null && !publisherAccount.IsDeleted() &&
                                  alsoMust(e, publisherAccount);
                       })
            .When(e => e.RequestPublisherAccountId > 0 && alsoMust != null)
            .WithName("PublisherAccount Identifier")
            .OverridePropertyName("PublisherAccount Identifier")
            .WithErrorCode(ErrorCodes.InvalidState)
            .WithMessage("PublisherAccount Identifier must be valid - code [rivrpavam]");
    }
}

public class IsValidPublisherAccountIdValidator<T> : AbstractValidator<T>
    where T : IRequestBase, IHasPublisherAccountId
{
    public IsValidPublisherAccountIdValidator(Func<IRequestBase, DynPublisherAccount, bool> alsoMust = null)
    {
        RuleFor(e => e.PublisherAccountId)
            .GreaterThan(0)
            .WithErrorCode(ErrorCodes.MustBeSpecified)
            .WithMessage("Valid publisher account identifier is required");

        RuleFor(e => e.ToDynItemValidationSourceByRef<DynPublisherAccount>(e.PublisherAccountId, e.PublisherAccountId.ToStringInvariant(),
                                                                           DynItemType.PublisherAccount, ApplyToBehavior.MustExistNotDeleted))
            .IsValidDynamoItem()
            .When(e => e.PublisherAccountId > 0);

        RuleFor(e => e)
            .MustAsync(async (e, t) =>
                       {
                           var publisherAccount = await PublisherExtensions.DefaultPublisherAccountService
                                                                           .TryGetPublisherAccountAsync(e.PublisherAccountId);

                           return publisherAccount != null && !publisherAccount.IsDeleted() &&
                                  alsoMust(e, publisherAccount);
                       })
            .When(e => e.PublisherAccountId > 0 && alsoMust != null)
            .WithName("PublisherAccount Identifier")
            .OverridePropertyName("PublisherAccount Identifier")
            .WithErrorCode(ErrorCodes.InvalidState)
            .WithMessage("PublisherAccount Identifier must be valid - code [rivpavam]");
    }
}

public class IsFromValidNonTokenRequestPublisherAccountValidator : IsFromValidRequestPublisherAccountValidator<IRequestBase>
{
    public IsFromValidNonTokenRequestPublisherAccountValidator(bool requestPublisherAccountIdCanBeEmpty = false)
        : base((r, p) => !p.RydrAccountType.HasFlag(RydrAccountType.TokenAccount) &&
                         !p.AccountType.IsUserAccount(),
               requestPublisherAccountIdCanBeEmpty) { }
}

public class IsValidPublisherAccountIdOrRequestPublisherIdValidator<T> : AbstractValidator<T>
    where T : IRequestBase, IHasPublisherAccountId
{
    public IsValidPublisherAccountIdOrRequestPublisherIdValidator()
    { // PublisherAccountId OR Request must be valid pubaccount identifier
        RuleFor(e => e.PublisherAccountId)
            .GreaterThan(0)
            .When(e => e.RequestPublisherAccountId <= 0)
            .WithErrorCode(ErrorCodes.MustBeSpecified)
            .WithMessage("PublisherAccountId info must be included and valid - code [vpavpaid]");

        RuleFor(e => e.RequestPublisherAccountId)
            .GreaterThan(0)
            .When(e => e.PublisherAccountId <= 0)
            .WithErrorCode(ErrorCodes.MustBeSpecified)
            .WithMessage("RequestPublisherAccountId info must be included and valid - code [vpavpaid]");

        RuleFor(e => e)
            .SetValidator(new IsValidPublisherAccountIdValidator<T>())
            .When(e => e.PublisherAccountId > 0);

        RuleFor(e => e)
            .SetValidator(new IsFromValidRequestPublisherAccountValidator<T>())
            .When(e => e.RequestPublisherAccountId > 0);
    }
}

public class IsValidPublisherIdentifierValidator<T> : AbstractValidator<T>
    where T : IRequestBase, IHasPublisherAccountIdentifier
{
    public IsValidPublisherIdentifierValidator(bool skipNonMeAccessChecks = false,
                                               Func<IRequestBase, DynPublisherAccount, bool> alsoMust = null)
    {
        RuleFor(e => e)
            .SetValidator(new IsFromValidRequestPublisherAccountValidator<T>())
            .When(e => e.PublisherIdentifier.EqualsOrdinalCi("me"));

        RuleFor(e => e.PublisherIdentifier)
            .NotEmpty()
            .WithErrorCode(ErrorCodes.MustBeSpecified);

        RuleFor(e => e.PublisherIdentifier)
            .Must(i => i.ToLong(0) > 0 || i.EqualsOrdinalCi("me"))
            .When(e => e.PublisherIdentifier.HasValue())
            .WithErrorCode(ErrorCodes.MustBeValid)
            .WithMessage("PublisherIdentifier must be a valid value");

        RuleFor(e => e.ToDynItemValidationSourceByRef<DynPublisherAccount>(e.PublisherIdentifier.ToLong(0), e.PublisherIdentifier.ToLong(0),
                                                                           DynItemType.PublisherAccount, ApplyToBehavior.MustExistNotDeleted,
                                                                           (skipNonMeAccessChecks && e.PublisherIdentifier.ToLong(0) != e.RequestPublisherAccountId)))
            .IsValidDynamoItem()
            .When(e => e.PublisherIdentifier.ToLong(0) > 0);

        RuleFor(e => e)
            .MustAsync(async (e, t) =>
                       {
                           var publisherAccountId = e.GetPublisherIdFromIdentifier();

                           var publisherAccount = await PublisherExtensions.DefaultPublisherAccountService
                                                                           .TryGetPublisherAccountAsync(publisherAccountId);

                           return publisherAccount != null && !publisherAccount.IsDeleted() &&
                                  alsoMust(e, publisherAccount);
                       })
            .When(e => alsoMust != null)
            .WithName("PublisherAccount Identifier")
            .OverridePropertyName("PublisherAccount Identifier")
            .WithErrorCode(ErrorCodes.InvalidState)
            .WithMessage("PublisherAccount Identifier must be valid - code [rivpivamzx]");
    }
}

public class IsValidWorkspaceIdentifierValidator<T> : AbstractValidator<T>
    where T : IRequestBase, IHasWorkspaceIdentifier
{
    public IsValidWorkspaceIdentifierValidator(ApplyToBehavior validationBehavior = ApplyToBehavior.Default)
    {
        RuleFor(e => e.WorkspaceIdentifier)
            .NotEmpty()
            .WithErrorCode(ErrorCodes.MustBeSpecified);

        RuleFor(e => e)
            .SetValidator(new IsFromValidRequestWorkspaceValidator<T>())
            .When(e => e.WorkspaceIdentifier.EqualsOrdinalCi("me"));

        RuleFor(e => e.WorkspaceIdentifier)
            .Must((e, i) => i.EqualsOrdinalCi("me") || i.ToLong(0) > 0)
            .When(e => e.WorkspaceIdentifier.HasValue())
            .WithErrorCode(ErrorCodes.MustBeValid)
            .WithMessage("WorkspaceIdentifier must be a valid value");

        RuleFor(e => e.ToDynItemValidationSource<DynWorkspace>(e.GetWorkspaceIdFromIdentifier(), DynItemType.Workspace, validationBehavior))
            .IsValidDynamoItem()
            .When(e => e.WorkspaceIdentifier.HasValue() && !e.WorkspaceIdentifier.EqualsOrdinalCi("me"));
    }
}

public class IsAuthorizedToAlterWorkspace<T> : AbstractValidator<T>
    where T : IRequestBase, IHasWorkspaceIdentifier
{
    public IsAuthorizedToAlterWorkspace(Func<T, DynWorkspace, bool> alsoMust = null)
    {
        RuleFor(e => e.WorkspaceIdentifier)
            .NotEmpty()
            .WithErrorCode(ErrorCodes.MustBeSpecified);

        RuleFor(e => e.WorkspaceIdentifier)
            .MustAsync(async (e, i, t) =>
                       {
                           var workspaceId = e.GetWorkspaceIdFromIdentifier();

                           var workspace = WorkspaceService.DefaultWorkspaceService.TryGetWorkspace(workspaceId);

                           // Only workspace owners can alter a workspace currently
                           return workspace != null && !workspace.IsDeleted() &&
                                  (workspace.OwnerId == e.UserId || e.IsSystemRequest ||
                                   (await WorkspaceService.DefaultWorkspaceService.IsWorkspaceAdmin(workspace, e.UserId))) &&
                                  (alsoMust == null || alsoMust(e, workspace));
                       })
            .When(e => e.WorkspaceIdentifier.HasValue())
            .WithErrorCode(ErrorCodes.MustBeAuthorized)
            .WithMessage("The workspace specified does not exist or you do not have access it (code [ratawnoid])");
    }
}
