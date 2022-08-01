using System.Runtime.Serialization;

namespace Rydr.FbSdk.Models
{
    [DataContract]
    public class FbPicture : FbData<FbPictureData> { }

    [DataContract]
    public class FbPictureData
    {
        [DataMember(Name = "height")]
        public int Height { get; set; }

        [DataMember(Name = "width")]
        public int Width { get; set; }

        [DataMember(Name = "is_silhouette")]
        public bool IsSilhouette { get; set; }

        [DataMember(Name = "url")]
        public string Url { get; set; }
    }

    [DataContract]
    public class FbCoverPhoto
    {
        [DataMember(Name = "id")]
        public string Id { get; set; }

        [DataMember(Name = "source")]
        public string SourceUrl { get; set; }
    }
}
