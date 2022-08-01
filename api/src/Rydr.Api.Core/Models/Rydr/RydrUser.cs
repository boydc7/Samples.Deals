using Rydr.Api.Dto.Enums;
using ServiceStack.DataAnnotations;

namespace Rydr.Api.Core.Models.Rydr
{
    [Alias("Users")]
    public class RydrUser : BaseLongDeletableDataModel
    {
        [StringLength(250)]
        [Required]
        [Unique]
        public string UserName { get; set; }

        [Required]
        public long RoleId { get; set; }

        [Required]
        [CheckConstraint("UserType > 0")]
        public UserType UserType { get; set; }

        [Required]
        [StringLength(250)]
        public string Email { get; set; }

        [Required]
        public bool IsEmailVerified { get; set; }

        [StringLength(250)]
        public string AuthProviderUserName { get; set; }

        // AuthProvider = Firebase for example
        [StringLength(250)]
        [Required]
        [Unique]
        public string AuthProviderUid { get; set; }

        [StringLength(50)]
        public string FirstName { get; set; }

        [StringLength(50)]
        public string LastName { get; set; }

        [Required]
        public long DefaultWorkspaceId { get; set; }

        [Required]
        public PublisherType LastAuthPublisherType { get; set; }
    }
}
