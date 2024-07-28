namespace Rydr.ActiveCampaign.Models;

public abstract class AcCollectionBase<T>
{
    public abstract IReadOnlyList<T> Data { get; }
    public AcMeta Meta { get; set; }
}
