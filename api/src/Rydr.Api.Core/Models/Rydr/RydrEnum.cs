using Rydr.Api.Dto.Interfaces;
using ServiceStack.DataAnnotations;

namespace Rydr.Api.Core.Models.Rydr
{
    [PostCreateTable(@"
DROP TABLE Enums;
CREATE TABLE Enums
(
Id BIGINT NOT NULL,
Name VARCHAR(100) NOT NULL,
PRIMARY KEY (Name)
);
CREATE UNIQUE INDEX IDX_Enums__Id ON Enums (Id);
")]
    [Alias("Enums")]
    public class RydrEnum : IHasSettableId
    {
        [Required]
        public long Id { get; set; }

        [Required]
        [StringLength(100)]
        public string Name { get; set; }
    }
}
