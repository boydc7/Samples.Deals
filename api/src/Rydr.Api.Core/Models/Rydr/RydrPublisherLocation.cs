using System;
using ServiceStack.DataAnnotations;

namespace Rydr.Api.Core.Models.Rydr
{
    [PostCreateTable(@"
DROP TABLE PublisherLocations;
CREATE TABLE PublisherLocations
(
PublisherAccountId BIGINT NOT NULL,
AddressId VARCHAR(65) NOT NULL,
CreatedOn DATE NOT NULL,
PRIMARY KEY (PublisherAccountId, CreatedOn, AddressId)
);
CREATE UNIQUE INDEX IDX_PublisherLocations_Created_Paid_Aid ON PublisherLocations (CreatedOn, PublisherAccountId, AddressId);
")]
    [Alias("PublisherLocations")]
    public class RydrPublisherLocation
    {
        [Required]
        [StringLength(65)]
        public string AddressId { get; set; }

        [Required]
        public long PublisherAccountId { get; set; }

        [Required]
        public DateTime CreatedOn { get; set; }
    }
}
