using System;
using System.Linq;
using Rydr.Api.Core.Models.Rydr;
using Rydr.Api.Core.Services.Internal;
using Rydr.Api.Dto.Enums;
using Stripe;

namespace Rydr.Api.Core.Extensions
{
    public static class InvoiceExtensions
    {
        public static RydrInvoice ToRydrInvoice(this Invoice source, long workspaceId, long workspaceSubscriptionId,
                                                long workspacePublisherSubscriptionId, SubscriptionType subscriptionType,
                                                string fromInvoiceEvent)
        {
            if (source == null)
            {
                return null;
            }

            return new RydrInvoice
                   {
                       Id = source.Id,
                       CustomerId = source.CustomerId,
                       SubscriptionId = source.SubscriptionId,
                       WorkspaceSubscriptionId = workspaceSubscriptionId,
                       WorkspacePublisherSubscriptionId = workspacePublisherSubscriptionId,
                       WorkspaceId = workspaceId,
                       InvoiceNumber = source.Number,
                       InvoiceStatus = source.Status,
                       AmountDue = Math.Round(source.AmountDue / 100.0, 4),
                       AmountPaid = Math.Round(source.AmountPaid / 100.0, 4),
                       AmountRemaining = Math.Round(source.AmountRemaining / 100.0, 4),
                       AttemptCount = source.AttemptCount,
                       IsPaid = source.Paid
                                    ? 1
                                    : 0,
                       BillingReason = source.BillingReason,
                       CreatedOn = source.Created.ToUniversalTime(),
                       ModifiedOn = DateTimeHelper.UtcNow,
                       PeriodStart = source.PeriodStart.ToUniversalTime(),
                       PeriodEnd = source.PeriodEnd.ToUniversalTime(),
                       Subtotal = Math.Round(source.Subtotal / 100.0, 4),
                       Tax = Math.Round(source.Tax.Gz(0) / 100.0, 4),
                       Total = Math.Round(source.Total / 100.0, 4),
                       LineItems = source.Lines?.Data?.Count ?? 0,
                       LineItemsQuantity = (int)(source.Lines?.Data?.Sum(d => d.Quantity ?? 0) ?? 0),
                       SubscriptionType = subscriptionType,
                       LastInvoiceEvent = fromInvoiceEvent,
                       PaidOn = source.StatusTransitions?.PaidAt?.ToUniversalTime(),
                       VoidedOn = source.StatusTransitions?.VoidedAt?.ToUniversalTime(),
                       FinalizedOn = source.StatusTransitions?.FinalizedAt?.ToUniversalTime(),
                       MarkedUncollectibleOn = source.StatusTransitions?.MarkedUncollectibleAt?.ToUniversalTime(),
                       PrePaymentCreditAmount = Math.Round(source.PrePaymentCreditNotesAmount / 100.0, 4),
                       PostPaymentCreditAmount = Math.Round(source.PostPaymentCreditNotesAmount / 100.0, 4),
                       DueOn = source.DueDate?.ToUniversalTime()
                   };
        }
    }
}
