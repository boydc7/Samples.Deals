using Rydr.Api.Dto.Files;

namespace Rydr.Api.Core.Models.Supporting;

public class MediaAnalysisEntity
{
    public string EntityText { get; set; }
    public string EntityType { get; set; }
    public long Occurrences { get; set; }
}

public class HumanLoopResponse
{
    public bool IsComplete { get; set; }
    public bool Failed { get; set; }
    public string Identifier { get; set; }
    public string Prefix { get; set; }
    public string ResponesS3Uri { get; set; }
    public int HumanResponders { get; set; }
    public List<ValueWithConfidence> Inputs { get; set; }
    public List<ValueWithConfidence> Answers { get; set; }

    public static HumanLoopResponse IncompleteResponse { get; } = new()
                                                                  {
                                                                      IsComplete = false
                                                                  };

    public static HumanLoopResponse FailedResponse { get; } = new()
                                                              {
                                                                  Failed = true
                                                              };
}

public class HumanLoopInfo
{
    public string FailureReason { get; set; }
    public string Name { get; set; }
    public string Status { get; set; }
}
