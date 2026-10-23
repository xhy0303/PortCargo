using System;

namespace PortCargo.Core
{
    public enum RewardedVideoResult { Completed, Cancelled, Failed, Unavailable }

    /// <summary>Future platform adapter invokes completion on Unity's main thread, once per Show.
    /// Availability must include SDK readiness and a configured ad unit; no synthetic completion.</summary>
    public interface IRewardedHintProvider
    {
        bool IsAvailable { get; }
        void Show(Action<RewardedVideoResult> completed);
    }

    /// <summary>One-use proof bound to the solved board. Neither cancelled ads nor stale callbacks grant rights.</summary>
    public sealed class RewardedHintTicket
    {
        readonly GameSession source;
        readonly string runId, boxId;
        readonly long version;
        bool resolved;
        public RewardedHintTicket(GameSession source, string boxId)
        { this.source=source; this.boxId=boxId; runId=source.RunId; version=source.Version; }
        public bool Resolve(GameSession current, RewardedVideoResult result)
        {
            if(resolved)return false;
            resolved=true;
            return result==RewardedVideoResult.Completed && ReferenceEquals(source,current)
                && current.UseHint(runId,version,boxId,true);
        }
    }
}
