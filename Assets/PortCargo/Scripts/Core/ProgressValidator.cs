using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace PortCargo.Core
{
    /// <summary>Checks both saved modes before the A/B repository accepts a payload.</summary>
    public static class ProgressValidator
    {
        public static void Validate(PlayerSave save, LevelDefinition[] campaign)
        {
            LevelValidator.Require(campaign != null && campaign.Length > 0, "SAVE_CATALOG", "Missing campaign");
            LevelValidator.Require(save != null && save.profile != null && save.profile.completed != null,
                "SAVE_PROFILE", "Missing profile");
            LevelValidator.Require(save.profile.unlockedCount >= 1 && save.profile.unlockedCount <= campaign.Length,
                "SAVE_UNLOCKED", "Unlocked count exceeds campaign bounds");
            LevelValidator.Require(string.IsNullOrEmpty(save.mode) || save.mode == "campaign" || save.mode == "endless",
                "SAVE_MODE", save.mode);

            var completed = new HashSet<string>();
            foreach (var id in save.profile.completed)
                LevelValidator.Require(id != null && Regex.IsMatch(id, "^L(?:[0-9]{2}|1[0-9]{2}|200)$") && completed.Add(id),
                    "SAVE_COMPLETED", id);

            if (save.activeRun != null)
            {
                LevelValidator.Require(save.activeRun.levelId != null
                    && Regex.IsMatch(save.activeRun.levelId, "^L(?:[0-9]{2}|1[0-9]{2}|200)$"),
                    "SAVE_CAMPAIGN_LEVEL", save.activeRun.levelId);
                // An old catalog entry or revision may be replaced by the presenter. A matching
                // revision, however, must restore every current-state and undo-history invariant.
                foreach (var level in campaign)
                {
                    if (level.Id != save.activeRun.levelId) continue;
                    if (level.Revision == save.activeRun.levelRevision) GameSession.Restore(level, save.activeRun);
                    break;
                }
            }

            LevelValidator.Require(save.mode != "endless" || save.endless != null,
                "SAVE_ENDLESS_MISSING", "Endless mode requires a saved round");
            if (save.endless == null) return;
            var endless = save.endless;
            if (endless.recentLayouts != null)
            {
                LevelValidator.Require(endless.recentLayouts.Count <= EndlessGenerator.RecentLimit,
                    "SAVE_ENDLESS_HISTORY", "Too many recent layouts");
                var recent = new HashSet<string>();
                foreach (var signature in endless.recentLayouts)
                    LevelValidator.Require(signature != null && Regex.IsMatch(signature, "^[0-9a-f]{64}$") && recent.Add(signature),
                        "SAVE_ENDLESS_HISTORY", "Invalid or duplicate layout hash");
            }
            LevelValidator.Require(endless.generatorVersion >= 1 && endless.round >= 1,
                "SAVE_ENDLESS_VERSION", "Generator version and round must be positive");
            // All int seeds are valid. Do not regenerate or compare with the current generator:
            // the persisted definition owns this round, including after a generator update.
            LevelValidator.Require(endless.level != null && endless.level.id == "E01",
                "SAVE_ENDLESS_LEVEL", "Expected an E01 definition");
            var definition = new LevelDefinition(endless.level);
            GameSession.Restore(definition, endless.run);
        }
    }
}
