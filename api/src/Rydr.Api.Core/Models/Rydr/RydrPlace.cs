using Rydr.Api.Dto.Enums;
using ServiceStack.DataAnnotations;
using ServiceStack.Model;

namespace Rydr.Api.Core.Models.Rydr
{
    [Alias("Places")]
    public class RydrPlace : BaseLongDeletableDataModel
    {
        [StringLength(65)]
        public string AddressId { get; set; }

        [StringLength(100)]
        public string Name { get; set; }

        [StringLength(100)]
        public string PublisherId { get; set; }

        public PublisherType PublisherType { get; set; }
    }

    [PostCreateTable(@"
DROP TABLE Addresses;
CREATE TABLE Addresses
(
Id VARCHAR(65) NOT NULL,
Name VARCHAR(100) NULL,
Address1 VARCHAR(100) NULL,
Address2 VARCHAR(100) NULL,
City VARCHAR(100) NULL,
StateProvince VARCHAR(50) NULL,
CountryCode VARCHAR(50) NULL,
PostalCode VARCHAR(100) NULL,
Latitude DECIMAL(18,14) NULL,
Longitude DECIMAL(18,14) NULL,
PRIMARY KEY (Id)
);
CREATE UNIQUE INDEX IDX_Addresses__Zip_Id ON Addresses (PostalCode, Id);
CREATE UNIQUE INDEX IDX_Addresses__City_Zip_Id ON Addresses (City, PostalCode, Id);
")]
    [Alias("Addresses")]
    public class RydrAddress : IHasStringId
    {
        [Required]
        [PrimaryKey]
        [StringLength(65)]
        public string Id { get; set; }

        [StringLength(100)]
        public string Name { get; set; }

        [StringLength(100)]
        public string Address1 { get; set; }

        [StringLength(100)]
        public string Address2 { get; set; }

        [StringLength(100)]
        public string City { get; set; }

        [StringLength(50)]
        public string StateProvince { get; set; }

        [StringLength(50)]
        public string CountryCode { get; set; }

        [StringLength(100)]
        public string PostalCode { get; set; }

        [DecimalLength(18, 14)]
        public double? Latitude { get; set; }

        [DecimalLength(18, 14)]
        public double? Longitude { get; set; }
    }
}
