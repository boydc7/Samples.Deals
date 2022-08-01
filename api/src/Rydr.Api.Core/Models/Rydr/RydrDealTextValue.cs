using Rydr.Api.Core.Models.Supporting;
using ServiceStack.DataAnnotations;
using ServiceStack.Model;

namespace Rydr.Api.Core.Models.Rydr
{
    [PostCreateTable(@"
DROP TABLE DealTextValues;
CREATE TABLE DealTextValues
(
Id BIGINT NOT NULL,
DealGroupId VARCHAR(50) NULL,
Title VARCHAR(250) NULL,
Description VARCHAR(500) NULL,
Tags VARCHAR(500) NULL,
Restrictions VARCHAR(500) NULL,
ApprovalNotes VARCHAR(500) NULL,
ReceiveNotes VARCHAR(500) NULL,
ReceiveTypes VARCHAR(500) NULL,
MetaData VARCHAR(500) NULL,
PRIMARY KEY (Id)
);
")]
    [Alias("DealTextValues")]
    public class RydrDealTextValue : IHasLongId
    {
        [Required]
        [PrimaryKey]
        [IgnorePopulateExisting]
        public long Id { get; set; }

        [StringLength(250)]
        public string Title { get; set; }

        [StringLength(500)]
        public string Description { get; set; }

        [StringLength(50)]
        public string DealGroupId { get; set; }

        [StringLength(500)]
        public string Tags { get; set; }

        [StringLength(500)]
        public string Restrictions { get; set; }

        [StringLength(500)]
        public string ApprovalNotes { get; set; }

        [StringLength(500)]
        public string ReceiveNotes { get; set; }

        [StringLength(500)]
        public string ReceiveTypes { get; set; }

        [StringLength(500)]
        public string MetaData { get; set; }
    }
}
