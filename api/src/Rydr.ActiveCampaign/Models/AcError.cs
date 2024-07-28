namespace Rydr.ActiveCampaign.Models;

public class AcErrors
{
    public IReadOnlyList<AcError> Errors { get; set; }
}

public class AcError
{
    public string Title { get; set; }

    public override string ToString() => Title;
}
