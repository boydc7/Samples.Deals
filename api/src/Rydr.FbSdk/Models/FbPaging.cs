using System.Runtime.Serialization;

namespace Rydr.FbSdk.Models
{
    [DataContract]
    public class FbPaging
    {
        [DataMember(Name = "previous")]
        public string Previous { get; set; }

        [DataMember(Name = "next")]
        public string Next { get; set; }

        [DataMember(Name = "cursors")]
        public FbCursor Cursors { get; set; }
    }
}
