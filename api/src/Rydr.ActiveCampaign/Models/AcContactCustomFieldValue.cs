using System.Collections.Generic;

namespace Rydr.ActiveCampaign.Models
{
    public class AcContactCustomFieldValue
    {
        public string Contact { get; set; }
        public string Field { get; set; }
        public string Value { get; set; }
        public string Id { get; set; }
    }

    public class GetAcContactCustomFieldValue
    {
        public AcContactCustomFieldValue FieldValue { get; set; }
    }

    public class GetAcContactCustomFieldValues : AcCollectionBase<AcContactCustomFieldValue>
    {
        public IReadOnlyList<AcContactCustomFieldValue> FieldValues { get; set; }
        public override IReadOnlyList<AcContactCustomFieldValue> Data => FieldValues;
    }

    public class PostAcContactCustomFieldValue
    {
        public AcContactCustomFieldValue FieldValue { get; set; }
    }
}
