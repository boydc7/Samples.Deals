using System.Runtime.Serialization;

namespace Rydr.FbSdk.Models
{
    [DataContract]
    public class FbUser
    {
        [DataMember(Name = "id")]
        public string Id { get; set; }

        [DataMember(Name = "name")]
        public string Name { get; set; }

        [DataMember(Name = "short_name")]
        public string ShortName { get; set; }

        //        [DataMember(Name = "address")]
        //        public FbLocation Address { get; set; }
        //
        [DataMember(Name = "age_range")]
        public FbAgeRange AgeRange { get; set; }

        //
        //        [DataMember(Name = "birthday")]
        //        public string Birthday { get; set; }

        [DataMember(Name = "email")]
        public string Email { get; set; }

        [DataMember(Name = "gender")]
        public string Gender { get; set; }

        //        [DataMember(Name = "hometown")]
        //        public FbIdName Hometown { get; set; }
        //
        //        [DataMember(Name = "favorite_athletes")]
        //        public List<FbIdName> FavoriteAthletes { get; set; }
        //
        //        [DataMember(Name = "favorite_teams")]
        //        public List<FbIdName> FavoriteTeams { get; set; }
        //
        //        [DataMember(Name = "inspirational_people")]
        //        public List<FbIdName> InspirationalPeople { get; set; }
        //
        //        [DataMember(Name = "languages")]
        //        public List<FbIdName> Languages { get; set; }
        //
        //        [DataMember(Name = "location")]
        //        public FbIdName Location { get; set; }
        //
        //        [DataMember(Name = "meeting_for")]
        //        public List<string> InterestedInMeetingFor { get; set; }
        //
        //        [DataMember(Name = "quotes")]
        //        public string FavoriteQuotes { get; set; }
        //
        //        [DataMember(Name = "significant_other")]
        //        public FbIdName SignificantOther { get; set; }
        //
        //        [DataMember(Name = "sports")]
        //        public List<FbIdName> Sports { get; set; }

        [DataMember(Name = "picture")]
        public FbPicture Picture { get; set; }
    }
}
