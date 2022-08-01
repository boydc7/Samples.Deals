using System.Collections.Generic;

namespace Rydr.ActiveCampaign.Models
{
    public class AcContactCustomField
    {
        public string Type { get; set; } // dropdown, hidden, checkbox, date, text, textarea, NULL, listbox, radio
        public string Title { get; set; }
        public string Descript { get; set; }
        public int IsRequired { get; set; }
        public string Perstag { get; set; }
        public string Defval { get; set; }
        public int Visible { get; set; }
        public int Ordernum { get; set; }
        public string Id { get; set; }
    }

    public class GetAcContactCustomField
    {
        public AcContactCustomField Field { get; set; }
    }

    public class GetAcContactCustomFields : AcCollectionBase<AcContactCustomField>
    {
        public IReadOnlyList<AcContactCustomField> Fields { get; set; }
        public override IReadOnlyList<AcContactCustomField> Data => Fields;
    }

    public class PostAcContactCustomField
    {
        public AcContactCustomField Field { get; set; }
    }
}
