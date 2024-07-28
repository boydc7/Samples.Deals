namespace Rydr.Api.Core.Models.Supporting;

public class SentimentResult
{
    public string Sentiment { get; set; }
    public double MixedSentiment { get; set; }
    public double PositiveSentiment { get; set; }
    public double NeutralSentiment { get; set; }
    public double NegativeSentiment { get; set; }
}
