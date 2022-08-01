using Amazon.AugmentedAIRuntime;
using Rydr.Api.Core.Models.Supporting;

namespace Rydr.Api.Core.Extensions
{
    public static class AwsExtensions
    {
        public static bool IsFinal(this HumanLoopStatus status)
            => status != null && (status.Value.EqualsOrdinalCi(HumanLoopStatus.Completed.Value) ||
                                  status.Value.EqualsOrdinalCi(HumanLoopStatus.Failed.Value) ||
                                  status.Value.EqualsOrdinalCi(HumanLoopStatus.Stopped.Value));

        public static bool IsFinal(this HumanLoopInfo info)
            => info != null && (info.Status.EqualsOrdinalCi(HumanLoopStatus.Completed.Value) ||
                                info.Status.EqualsOrdinalCi(HumanLoopStatus.Failed.Value) ||
                                info.Status.EqualsOrdinalCi(HumanLoopStatus.Stopped.Value));

        public static bool IsSuccessful(this HumanLoopStatus status)
            => status != null && status.Value.EqualsOrdinalCi(HumanLoopStatus.Completed.Value);

        public static bool IsSuccessful(this HumanLoopInfo info)
            => info != null && info.Status.EqualsOrdinalCi(HumanLoopStatus.Completed.Value);
    }
}
