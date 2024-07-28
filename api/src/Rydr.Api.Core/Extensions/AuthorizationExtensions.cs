using Rydr.Api.Core.Configuration;
using Rydr.Api.Core.Enums;
using Rydr.Api.Core.Interfaces.Internal;
using Rydr.Api.Core.Interfaces.Services;
using Rydr.Api.Core.Models.Doc;
using Rydr.Api.Core.Services.Internal;
using Rydr.Api.Dto.Enums;
using Rydr.Api.Dto.Interfaces;
using ServiceStack;

namespace Rydr.Api.Core.Extensions;

public static class AuthorizationExtensions
{
    public static readonly IAuthorizationService DefaultAuthorizationService = RydrEnvironment.Container.Resolve<IAuthorizationService>();
    public static readonly IAuthorizeService DefaultAuthorizeService = RydrEnvironment.Container.Resolve<IAuthorizeService>();
    private static readonly IRecordTypeRecordService _etRecordService = RydrEnvironment.Container.Resolve<IRecordTypeRecordService>();

    public static async Task AuthorizeDuplexedAsync(this IAuthorizeService authorizeService, long firstId, long secondId, string authPrefix = null)
    {
        await authorizeService.AuthorizeAsync(firstId, secondId, authPrefix);
        await authorizeService.AuthorizeAsync(secondId, firstId, authPrefix);
    }

    public static long GetWorkspaceIdFromIdentifier<T>(this T request)
        where T : IRequestBase, IHasWorkspaceIdentifier
        => string.IsNullOrEmpty(request.WorkspaceIdentifier) || request.WorkspaceIdentifier.EqualsOrdinalCi("me")
               ? request.WorkspaceId
               : request.WorkspaceIdentifier.ToLong(request.WorkspaceId);

    public static long GetPublisherIdFromIdentifier<T>(this T request)
        where T : IRequestBase, IHasPublisherAccountIdentifier
        => string.IsNullOrEmpty(request.PublisherIdentifier) || request.PublisherIdentifier.EqualsOrdinalCi("me")
               ? request.RequestPublisherAccountId
               : request.PublisherIdentifier.ToLong(request.RequestPublisherAccountId);

    public static long GetUserIdFromIdentifier<T>(this T request)
        where T : IRequestBase, IHasUserIdentifier
        => string.IsNullOrEmpty(request.UserIdentifier) || request.UserIdentifier.EqualsOrdinalCi("me")
               ? request.UserId
               : request.UserIdentifier.ToLong(request.UserId);

    public static async Task VerifyAccessToAsync<TToObject>(this IAuthorizationService authorizationService, TToObject toObject,
                                                            Func<TToObject, bool> isValid = null, string invalidMsg = null,
                                                            IHasUserAuthorizationInfo state = null)
        where TToObject : ICanBeAuthorized
    {
        var result = await DoVerifyAccessToAsync(authorizationService, state, toObject);

        if (isValid == null)
        {
            return;
        }

        Guard.AgainstInvalidData(!isValid(result), invalidMsg ?? "Invalid data in authorization/verification of data");
    }

    public static async Task VerifyAccessToAssociatedAsync(this DynAssociation source, IHasUserAuthorizationInfo state = null)
    {
        await VerifyAccessToAssociatedToAsync(source, state);

        await VerifyAccessToAssociatedFromAsync(source, state);
    }

    public static async Task VerifyAccessToAssociatedToAsync(this DynAssociation source, IHasUserAuthorizationInfo state = null)
    {
        var edgeId = source.EdgeId.ToLong(0);

        if (edgeId > 0 && !source.EdgeRecordType.IsAnAssociation())
        {
            await _etRecordService.ValidateAsync(source.EdgeRecordType, edgeId, state);
        }
    }

    public static Task VerifyAccessToAssociatedFromAsync(this DynAssociation source, IHasUserAuthorizationInfo state = null)
        => _etRecordService.ValidateAsync(source.IdRecordType, source.Id, state);

    public static Task<TToObject> VerifyAccessToByAsync<TToObject, TState>(this TToObject toObject, TState state)
        where TToObject : ICanBeAuthorized
        where TState : IHasUserAuthorizationInfo
        => DoVerifyAccessToAsync(DefaultAuthorizationService, state, toObject);

    public static async Task<bool> HasAccessToAsync<TTo, TState>(this TState state, TTo toObject)
        where TTo : ICanBeAuthorized
        where TState : IHasUserAuthorizationInfo
    {
        try
        {
            await DoVerifyAccessToAsync(DefaultAuthorizationService, state, toObject);

            return true;
        }
        catch(RydrAuthorizationException)
        {
            return false;
        }
    }

    private static async Task<TToObject> DoVerifyAccessToAsync<TState, TToObject>(IAuthorizationService authorizationService, TState state, TToObject toObject)
        where TState : IHasUserAuthorizationInfo
        where TToObject : ICanBeAuthorized
    {
        await authorizationService.VerifyAccessToAsync(toObject, state);

        return toObject;
    }

    public static bool IsValidPassword(this string password)
        => password.HasValue() &&
           password.ContainsAny("1", "2", "3", "4",
                                "5", "6", "7", "8",
                                "9", "0", " ", ".",
                                "!") &&
           password.ContainsAny("a", "A", "b", "B",
                                "c", "C", "d", "D",
                                "e", "E", "f", "F",
                                "g", "G", "h", "H",
                                "i", "I", "j", "J",
                                "k", "K", "l", "L",
                                "m", "M", "n", "N",
                                "o", "O", "p", "P",
                                "q", "Q", "r", "R",
                                "s", "S", "t", "T",
                                "u", "U", "v", "V",
                                "w", "W", "x", "X",
                                "y", "Y", "z", "Z") &&
           (
               password.Length >= 20 ||
               (password.Length >= 10 &&
                password.ContainsAny("!", "~", "_", "-",
                                     "=", "+", " ", ")",
                                     "(", "*", "&", "^",
                                     "%", "$", "#", "@"))
           );
}
