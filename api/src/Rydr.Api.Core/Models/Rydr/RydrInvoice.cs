using System;
using Rydr.Api.Dto.Enums;
using ServiceStack.DataAnnotations;
using ServiceStack.Model;

namespace Rydr.Api.Core.Models.Rydr
{
    [PostCreateTable(@"
DROP TABLE Invoices;
CREATE TABLE Invoices
(
Id VARCHAR(50) NOT NULL,
CustomerId VARCHAR(50) NOT NULL,
SubscriptionId VARCHAR(50) NOT NULL,
WorkspaceSubscriptionId BIGINT NOT NULL,
WorkspacePublisherSubscriptionId BIGINT NOT NULL,
WorkspaceId BIGINT NOT NULL,
InvoiceNumber VARCHAR(50) NULL,
InvoiceStatus VARCHAR(50) NOT NULL,
AmountDue DECIMAL(18,4) NOT NULL,
AmountPaid DECIMAL(18,4) NOT NULL,
AmountRemaining DECIMAL(18,4) NOT NULL,
AttemptCount BIGINT NOT NULL,
IsPaid INT NOT NULL,
BillingReason VARCHAR(100) NOT NULL,
CreatedOn DATETIME NOT NULL,
ModifiedOn DATETIME NOT NULL,
PeriodStart DATETIME NOT NULL,
PeriodEnd DATETIME NOT NULL,
Subtotal DECIMAL(18,4) NOT NULL,
Tax DECIMAL(18,4) NOT NULL,
Total DECIMAL(18,4) NOT NULL,
LineItems INT NOT NULL,
LineItemsQuantity INT NOT NULL,
SubscriptionType INT NOT NULL,
LastInvoiceEvent VARCHAR(50) NOT NULL,
DueOn DATETIME NULL,
PaidOn DATETIME NULL,
VoidedOn DATETIME NULL,
FinalizedOn DATETIME NULL,
MarkedUncollectibleOn DATETIME NULL,
PrePaymentCreditAmount DECIMAL(18,4) NOT NULL,
PostPaymentCreditAmount DECIMAL(18,4) NOT NULL,
PRIMARY KEY (WorkspaceSubscriptionId, WorkspacePublisherSubscriptionId, Id)
);
CREATE UNIQUE INDEX IDX_Invoices__Id ON Invoices (Id);
CREATE UNIQUE INDEX IDX_Invoices__Wid_WSubId_WSubPubId_Id ON Invoices (WorkspaceId, WorkspaceSubscriptionId, WorkspacePublisherSubscriptionId, Id);
")]
    [Alias("Invoices")]
    public class RydrInvoice : IHasStringId
    {
        [Required]
        [StringLength(50)]
        [PrimaryKey]
        public string Id { get; set; }

        [Required]
        [StringLength(50)]
        public string CustomerId { get; set; }

        [Required]
        [StringLength(50)]
        public string SubscriptionId { get; set; }

        [Required]
        public long WorkspaceSubscriptionId { get; set; }

        [Required]
        public long WorkspacePublisherSubscriptionId { get; set; }

        [Required]
        public long WorkspaceId { get; set; }

        [StringLength(50)]
        public string InvoiceNumber { get; set; }

        [Required]
        [StringLength(50)]
        public string InvoiceStatus { get; set; }

        [Required]
        [DecimalLength(18, 4)]
        public double AmountDue { get; set; }

        [Required]
        [DecimalLength(18, 4)]
        public double AmountPaid { get; set; }

        [Required]
        [DecimalLength(18, 4)]
        public double AmountRemaining { get; set; }

        [Required]
        public long AttemptCount { get; set; }

        [Required]
        public int IsPaid { get; set; }

        [Required]
        [StringLength(100)]
        public string BillingReason { get; set; }

        [Required]
        public DateTime CreatedOn { get; set; }

        [Required]
        public DateTime ModifiedOn { get; set; }

        [Required]
        public DateTime PeriodStart { get; set; }

        [Required]
        public DateTime PeriodEnd { get; set; }

        [Required]
        [DecimalLength(18, 4)]
        public double Subtotal { get; set; }

        [Required]
        [DecimalLength(18, 4)]
        public double Tax { get; set; }

        [Required]
        [DecimalLength(18, 4)]
        public double Total { get; set; }

        [Required]
        public int LineItems { get; set; }

        [Required]
        public int LineItemsQuantity { get; set; }

        [Required]
        public SubscriptionType SubscriptionType { get; set; }

        [Required]
        [StringLength(50)]
        public string LastInvoiceEvent { get; set; }

        public DateTime? DueOn { get; set; }
        public DateTime? PaidOn { get; set; }
        public DateTime? VoidedOn { get; set; }
        public DateTime? FinalizedOn { get; set; }
        public DateTime? MarkedUncollectibleOn { get; set; }

        [Required]
        [DecimalLength(18, 4)]
        public double PrePaymentCreditAmount { get; set; }

        [Required]
        [DecimalLength(18, 4)]
        public double PostPaymentCreditAmount { get; set; }
    }
}
