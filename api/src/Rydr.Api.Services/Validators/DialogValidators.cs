using Rydr.Api.Core.Enums;
using Rydr.Api.Core.Extensions;
using Rydr.Api.Core.Models.Doc;
using Rydr.Api.Core.Transforms;
using Rydr.Api.Dto.Enums;
using Rydr.Api.Dto.Messages;
using Rydr.Api.Services.Helpers;
using ServiceStack.Aws.DynamoDb;
using ServiceStack.FluentValidation;

namespace Rydr.Api.Services.Validators;

public class PostMessageValidator : BaseRydrValidator<PostMessage>
{
    public PostMessageValidator(IPocoDynamo dynamoDb)
    {
        RuleFor(e => e.Message)
            .NotEmpty();

        RuleFor(e => e.To)
            .NotNull();

        RuleFor(e => e.From)
            .IsValidRecordTypeId("From")
            .When(e => e.From != null);

        // When not a contact/account being sent to, validate access to the object
        RuleFor(e => e.To)
            .IsValidRecordTypeId("To")
            .When(e => e.To != null && e.To.Type != RecordType.User && e.To.Type != RecordType.PublisherAccount);

        // When a contact/account being sent to, have to already have an existing dialog, or cannot message
        RuleFor(e => e.To)
            .MustAsync(async (r, i, t) =>
                       {
                           var sendMsg = await r.ToSendMessageAsync();

                           var existingDialog = await dynamoDb.GetItemByEdgeIntoAsync<DynDialog>(DynItemType.Dialog, sendMsg.Members.ToDialogKey(), true);

                           if (existingDialog != null && !existingDialog.IsDeleted())
                           {
                               return true;
                           }

                           // No existing dialog...if the message is for a deal, see if there is an open request for the deal from the to
                           if (sendMsg.ForRecord != null && sendMsg.ForRecord.Type == RecordType.Deal && r.To.Type == RecordType.PublisherAccount)
                           {
                               var existingDealRequest = await dynamoDb.GetItemAsync<DynDealRequest>(sendMsg.ForRecord.Id, r.To.Id.ToEdgeId());

                               return existingDealRequest != null && !existingDealRequest.IsDeleted();
                           }

                           return false;
                       })
            .When(e => e.To != null && (e.To.Type == RecordType.User || e.To.Type == RecordType.PublisherAccount))
            .WithMessage("Resource (To) does not exist or you do not have access to it (code: [noxd]")
            .WithErrorCode(ErrorCodes.MustBeAuthorized);
    }
}

public class PutMessageReadValidator : BaseRydrValidator<PutMessageRead>
{
    public PutMessageReadValidator()
    {
        // NOTE: not checking for access/record existence purposely here, it doesn't modify/access any state
        RuleFor(e => e.DialogId)
            .GreaterThan(0);

        RuleFor(e => e.Id)
            .GreaterThan(0);
    }
}

public class PutDialogReadValidator : BaseRydrValidator<PutDialogRead>
{
    public PutDialogReadValidator()
    {
        // NOTE: not checking for access/record existence purposely here, it doesn't modify/access any state
        RuleFor(e => e.Id)
            .GreaterThan(0);
    }
}

public class PostDialogMessageValidator : BaseRydrValidator<PostDialogMessage>
{
    public PostDialogMessageValidator()
    {
        RuleFor(e => e.Message)
            .NotEmpty();

        RuleFor(e => e.DialogId)
            .GreaterThan(0);

        RuleFor(e => e.ToDynItemValidationSourceByRef(e.DialogId, e.DialogId, DynItemType.Dialog, ApplyToBehavior.MustExistNotDeleted))
            .IsValidDynamoItem()
            .When(e => e.DialogId > 0);
    }
}

public class GetDialogMessagesValidator : BaseRydrValidator<GetDialogMessages>
{
    public GetDialogMessagesValidator()
    {
        Include(new IsValidSkipTakeValidator());

        RuleFor(e => e.DialogId)
            .GreaterThan(0);

        RuleFor(e => e.ToDynItemValidationSourceByRef(e.DialogId, e.DialogId, DynItemType.Dialog, ApplyToBehavior.MustExistNotDeleted))
            .IsValidDynamoItem()
            .When(e => e.DialogId > 0);
    }
}

public class GetDialogsValidator : BaseRydrValidator<GetDialogs>
{
    public GetDialogsValidator()
    {
        Include(new IsValidSkipTakeValidator());

        RuleFor(e => e.ForRecord)
            .IsValidRecordTypeId("ForRecord")
            .When(e => e.ForRecord != null);

        Unless(e => e.IsSystemRequest,
               () =>
               {
                   Include(new IsFromValidRequestWorkspaceValidator<GetDialogs>());

                   RuleFor(e => e.ForWorkspaceId)
                       .Empty()
                       .WithErrorCode(ErrorCodes.CannotBeSpecified);
               });
    }
}

public class GetDialogValidator : BaseGetRequestValidator<GetDialog>
{
    public GetDialogValidator() : base(false)
    {
        RuleFor(e => e.Id)
            .GreaterThan(0)
            .When(e => e.From == null || e.To == null)
            .WithErrorCode(ErrorCodes.MustBeSpecified);

        RuleFor(e => e.From)
            .IsValidRecordTypeId("From")
            .When(e => e.From != null);

        RuleFor(e => e.To)
            .IsValidRecordTypeId("To")
            .When(e => e.To != null);

        RuleFor(e => e.ToDynItemValidationSourceByRef(e.Id, e.Id, DynItemType.Dialog))
            .IsValidDynamoItem()
            .When(e => e.Id > 0);

        RuleFor(e => e.ToDynItemValidationSource(new[]
                                                 {
                                                     e.From, e.To
                                                 }.ToDialogKey(), DynItemType.Dialog, ApplyToBehavior.Default))
            .IsValidDynamoItem()
            .When(e => e.From != null && e.To != null);
    }
}
