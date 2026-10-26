using System;

namespace PortCargo.Core
{
    public enum RewardedVideoResult { Completed, Cancelled, Failed, Unavailable }

    /// <summary>Platform adapter invokes completion on Unity's main thread, once per Show.
    /// Availability must include SDK readiness and a configured ad unit; no synthetic completion.</summary>
    public interface IRewardedVideoProvider
    {
        bool IsAvailable { get; }
        void Show(Action<RewardedVideoResult> completed);
    }

    /// <summary>Optional cancellation; adapters may retain native exclusivity until an open ad closes.</summary>
    public interface ICancellableRewardedVideoProvider : IRewardedVideoProvider
    {
        /// <summary>Whether the player can leave without a native ad still opening or playing.</summary>
        bool CanCancel { get; }
        /// <summary>Invalidates the reward even when a native ad can no longer be dismissed.</summary>
        void Cancel();
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

    /// <summary>One-use proof for skipping the unchanged endless round. The caller owns the transition.</summary>
    public sealed class RewardedSkipTicket
    {
        readonly GameSession source;
        readonly string runId;
        readonly long version;
        readonly int round;
        bool resolved;

        public RewardedSkipTicket(GameSession source, int round)
        {
            this.source=source ?? throw new ArgumentNullException(nameof(source));
            this.round=round; runId=source.RunId; version=source.Version;
        }

        public bool TryResolve(GameSession current, bool isEndless, int round, RewardedVideoResult result)
        {
            if(resolved)return false;
            resolved=true;
            return result==RewardedVideoResult.Completed && isEndless && this.round>0
                && this.round<int.MaxValue && this.round==round && ReferenceEquals(source,current)
                && current.RunId==runId && current.Version==version && current.Status!=BoardStatus.Completed;
        }
    }
}
