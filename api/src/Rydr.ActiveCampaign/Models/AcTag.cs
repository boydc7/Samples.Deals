namespace Rydr.ActiveCampaign.Models;

public class AcTag
{
    public string Tag { get; set; }
    public string TagType { get; set; } // template, contact
    public string Description { get; set; }
    public string Id { get; set; }
}

public class GetAcTag
{
    public AcTag Tag { get; set; }
}

public class GetAcTags : AcCollectionBase<AcTag>
{
    public IReadOnlyList<AcTag> Tags { get; set; }
    public override IReadOnlyList<AcTag> Data => Tags;
}

public class PostAcTag
{
    public AcTag Tag { get; set; }
}
