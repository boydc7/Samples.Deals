namespace Rydr.Api.Dto.Files;

public class ValueWithConfidence
{
    public string Value { get; set; }
    public string ParentValue { get; set; }
    public double Confidence { get; set; }
    public long Occurrences { get; set; }
}
