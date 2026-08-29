using System;
using ScalingLaws.Simulation;

namespace ScalingLaws.Persistence
{
    /// <summary>
    /// The one line a load screen needs about a slot, without opening it.
    ///
    /// **Written when the campaign is saved, never derived when the list is drawn.** A menu showing
    /// four slots would otherwise have to read, migrate and rebuild four whole campaigns to print
    /// four company names, and migration walks every version step from whatever the file is up to
    /// the current one. That is most of a second of work to draw a menu that has not been clicked
    /// yet.
    ///
    /// It is deliberately a copy rather than a reference into the save. A summary that had to stay
    /// in step with the file would be a second thing to get wrong on every version bump; this one
    /// is rewritten wholesale every time the slot is written, so it cannot drift.
    /// </summary>
    [Serializable]
    public sealed class SlotSummary
    {
        public string companyName = string.Empty;
        public string founderName = string.Empty;

        /// <summary>The in-game date, already formatted. Nothing reading this should parse it.</summary>
        public string dateText = string.Empty;

        /// <summary>Days survived, which is what sorts one campaign against another.</summary>
        public int day;

        public long cashUsd;
        public int modelsReleased;

        /// <summary>Unix seconds when the slot was written, for "saved 3 days ago".</summary>
        public long savedAtUnix;

        public static SlotSummary Of(CompanyState state)
        {
            if (state == null)
            {
                return new SlotSummary();
            }

            return new SlotSummary
            {
                companyName = state.CompanyName ?? string.Empty,
                founderName = state.FounderName ?? string.Empty,
                dateText = state.Date.ToString(),
                day = state.Date.DayIndex,
                cashUsd = state.CashUsd,
                modelsReleased = state.ReleasedModelCount,
                savedAtUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
            };
        }
    }
}
