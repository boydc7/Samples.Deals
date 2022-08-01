using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using Amazon.DynamoDBv2.DataModel;
using Rydr.Api.Dto.Enums;
using Rydr.Api.Dto.Interfaces;
using ServiceStack.Auth;
using ServiceStack.Aws.DynamoDb;
using ServiceStack.DataAnnotations;

namespace Rydr.Api.Core.Models.Doc
{
    public class DynUser : DynItem, IUserAuth, IHasUserAndWorkspaceId
    {
        // Hash / Id: Unique Id for the user
        // Range / Edge: Username (which is the email ideally, or the firebaseId secondarily)
        // RefId: UserId (same as Id)
        // OwnerId:
        // WorkspaceId:
        // StatusId:
        // DynItemMap for firebase auth uid (using longHashCode of the uid as the Id - see UserService for example)

        [Ignore]
        [IgnoreDataMember]
        [DynamoDBIgnore]
        public long UserId
        {
            get => Id;
            set => Id = value;
        }

        [Ignore]
        [IgnoreDataMember]
        [DynamoDBIgnore]
        public string UserName
        {
            get => EdgeId;
            set => EdgeId = value.ToLowerInvariant();
        }

        [Required]
        public long RoleId { get; set; }

        [Required]
        public UserType UserType { get; set; }

        [Required]
        public PublisherType LastAuthPublisherType { get; set; }

        [Required]
        [StringLength(50)]
        [ExcludeNullValue]
        public string FirstName { get; set; }

        [Required]
        [StringLength(50)]
        [ExcludeNullValue]
        public string LastName { get; set; }

        [StringLength(50)]
        [ExcludeNullValue]
        public string Company { get; set; }

        [StringLength(200)]
        [ExcludeNullValue]
        public string Avatar { get; set; }

        [StringLength(200)]
        [ExcludeNullValue]
        public string PhoneNumber { get; set; }

        [Required]
        [StringLength(250)]
        [ExcludeNullValue]
        public string Email { get; set; }

        public bool IsEmailVerified { get; set; }

        [Required]
        [StringLength(250)]
        [ExcludeNullValue]
        public string AuthProviderUserName { get; set; }

        // AuthProvider = Firebase for example
        [ExcludeNullValue]
        public string AuthProviderUid { get; set; }

        [ExcludeNullValue]
        public string FullName { get; set; }

        [Required]
        public long DefaultWorkspaceId { get; set; }

        // NOTE: Following are required attributes to implement/use the IUserAuth interface, but we simply ignore thense
        [Ignore]
        [IgnoreDataMember]
        [DynamoDBIgnore]
        [ExcludeNullValue]
        public string DisplayName { get; set; }

        [Ignore]
        [IgnoreDataMember]
        [DynamoDBIgnore]
        [ExcludeNullValue]
        public DateTime? BirthDate { get; set; }

        [Ignore]
        [IgnoreDataMember]
        [DynamoDBIgnore]
        [ExcludeNullValue]
        public string BirthDateRaw { get; set; }

        [Ignore]
        [IgnoreDataMember]
        [DynamoDBIgnore]
        [ExcludeNullValue]
        public string Address { get; set; }

        [Ignore]
        [IgnoreDataMember]
        [DynamoDBIgnore]
        [ExcludeNullValue]
        public string Address2 { get; set; }

        [Ignore]
        [IgnoreDataMember]
        [DynamoDBIgnore]
        [ExcludeNullValue]
        public string City { get; set; }

        [Ignore]
        [IgnoreDataMember]
        [DynamoDBIgnore]
        [ExcludeNullValue]
        public string State { get; set; }

        [Ignore]
        [IgnoreDataMember]
        [DynamoDBIgnore]
        [ExcludeNullValue]
        public string Country { get; set; }

        [Ignore]
        [IgnoreDataMember]
        [DynamoDBIgnore]
        [ExcludeNullValue]
        public string Culture { get; set; }

        [Ignore]
        [IgnoreDataMember]
        [DynamoDBIgnore]
        [ExcludeNullValue]
        public string Gender { get; set; }

        [Ignore]
        [IgnoreDataMember]
        [DynamoDBIgnore]
        [ExcludeNullValue]
        public string Language { get; set; }

        [Ignore]
        [IgnoreDataMember]
        [DynamoDBIgnore]
        [ExcludeNullValue]
        public string MailAddress { get; set; }

        [Ignore]
        [IgnoreDataMember]
        [DynamoDBIgnore]
        [ExcludeNullValue]
        public string Nickname { get; set; }

        [Ignore]
        [IgnoreDataMember]
        [DynamoDBIgnore]
        [ExcludeNullValue]
        public string PostalCode { get; set; }

        [Ignore]
        [IgnoreDataMember]
        [DynamoDBIgnore]
        [ExcludeNullValue]
        public string TimeZone { get; set; }

        [Ignore]
        [IgnoreDataMember]
        [DynamoDBIgnore]
        [ExcludeNullValue]
        public Dictionary<string, string> Meta { get; set; }

        [Ignore]
        [IgnoreDataMember]
        [DynamoDBIgnore]
        [ExcludeNullValue]
        public string PrimaryEmail { get; set; }

        [Ignore]
        [IgnoreDataMember]
        [DynamoDBIgnore]
        [ExcludeNullValue]
        public string Salt { get; set; }

        [Ignore]
        [IgnoreDataMember]
        [DynamoDBIgnore]
        [ExcludeNullValue]
        public string PasswordHash { get; set; }

        [Ignore]
        [IgnoreDataMember]
        [DynamoDBIgnore]
        [ExcludeNullValue]
        public string DigestHa1Hash { get; set; }

        [Ignore]
        [IgnoreDataMember]
        [DynamoDBIgnore]
        [ExcludeNullValue]
        public List<string> Roles { get; set; }

        [Ignore]
        [IgnoreDataMember]
        [DynamoDBIgnore]
        [ExcludeNullValue]
        public List<string> Permissions { get; set; }

        [Ignore]
        [IgnoreDataMember]
        [DynamoDBIgnore]
        [ExcludeNullValue]
        public int? RefId { get; set; }

        [Ignore]
        [IgnoreDataMember]
        [DynamoDBIgnore]
        [ExcludeNullValue]
        public string RefIdStr { get; set; }

        [Ignore]
        [IgnoreDataMember]
        [DynamoDBIgnore]
        [ExcludeNullValue]
        public int InvalidLoginAttempts { get; set; }

        [Ignore]
        [IgnoreDataMember]
        [DynamoDBIgnore]
        [ExcludeNullValue]
        public DateTime? LastLoginAttempt { get; set; }

        [Ignore]
        [IgnoreDataMember]
        [DynamoDBIgnore]
        [ExcludeNullValue]
        public DateTime? LockedDate { get; set; }

        public DateTime CreatedDate { get; set; }
        public DateTime ModifiedDate { get; set; }
    }
}
