using System;
using ScalingLaws.Core;

namespace ScalingLaws.Simulation
{
    /// <summary>
    /// What a government sees when it looks up your company's safety record.
    ///
    /// **Five years, because that is the point of it.** Every other safety number in this game is
    /// about today: the daily incident risk, the modules a model shipped with, whether an inspection
    /// is open. A state deciding whether to run its own bureaucracy on your models is not asking
    /// what your risk is this afternoon, it is asking what you have done for five years. That is a
    /// question a company cannot answer by buying anything, and it is the only gate in the game that
    /// money genuinely cannot move.
    ///
    /// **Derived, never stored.** It is a pure function of the incident list and the date, both of
    /// which are already saved, so it replays identically, needs no migration, and cannot drift from
    /// the incidents the news reported. The fourth mechanism in this project built this way, after
    /// `RivalExpansion`, `LabTraits` and `WorldEventCatalog`.
    ///
    /// The record is unforgiving in one direction and patient in the other, which is the whole shape
    /// of the endgame gate: a clean company reaches the threshold by doing nothing wrong for a long
    /// time, and one bad severe incident costs about three years of that.
    /// </summary>
    public static class SafetyRecord
    {
        /// <summary>The window a state looks back over, in days.</summary>
        public const int WindowDays = 5 * 365;

        /// <summary>
        /// What a state wants to see before it will talk about a contract.
        ///
        /// Ninety per cent, as the author specified. In this scale that is a company which has had
        /// nothing severe in five years and at most a couple of minor incidents.
        /// </summary>
        public const double ContractThreshold = 0.90;

        /// <summary>
        /// What each severity costs the record, at the moment it happens.
        ///
        /// **A severe incident costs a third of the whole record.** Deliberately brutal: the
        /// threshold is 0.90, so one severe event puts the company below it and the only way back is
        /// to wait most of the window out. That is the correct price for the thing the contract is
        /// buying, which is confidence that the models will not do something in public.
        /// </summary>
        public const double SevereCost = 0.34;

        public const double MajorCost = 0.15;
        public const double MinorCost = 0.05;

        /// <summary>
        /// How much of an incident has faded by the far edge of the window.
        ///
        /// **Not all of it.** An event that is four years and eleven months old still counts for a
        /// tenth, and then leaves entirely. Without the residue the record would step upward on a
        /// particular day for no reason the player could see, and a gate that opens on an invisible
        /// anniversary is a gate that reads as a bug.
        /// </summary>
        public const double FadedTo = 0.10;

        /// <summary>
        /// The record, from nothing to perfect.
        ///
        /// Reads the incidents the company has actually had rather than the risk it is running.
        /// A company can be one bad week from disaster and still show a spotless five years, which
        /// is true of real companies and is exactly the trap this endgame is built on: the contract
        /// is signed on history and paid out on what happens next.
        /// </summary>
        public static double For(CompanyState state, GameDate today)
        {
            if (state == null)
            {
                return 0.0;
            }

            var record = 1.0;

            foreach (var incident in state.Incidents)
            {
                var age = today.DayIndex - incident.Date.DayIndex;

                if (age < 0 || age >= WindowDays)
                {
                    continue;
                }

                record -= CostOf(incident.Severity) * Weight(age);
            }

            return Math.Clamp(record, 0.0, 1.0);
        }

        /// <summary>Would a state sign today.</summary>
        public static bool MeetsTheBar(CompanyState state, GameDate today) =>
            For(state, today) >= ContractThreshold;

        /// <summary>
        /// How long until an incident of this age stops mattering, in days.
        ///
        /// Shown on the screen, because a company sitting below the bar needs to know whether it is
        /// three months or three years from being able to sign. "Not yet" with no number attached is
        /// the difference between a goal and a wall.
        /// </summary>
        public static int DaysUntilItClears(int age) => Math.Max(0, WindowDays - age);

        private static double CostOf(IncidentSeverity severity) => severity switch
        {
            IncidentSeverity.Severe => SevereCost,
            IncidentSeverity.Major => MajorCost,
            IncidentSeverity.Minor => MinorCost,
            _ => 0.0
        };

        /// <summary>
        /// How much an incident of a given age still counts.
        ///
        /// Linear from full weight on the day to <see cref="FadedTo"/> at the edge of the window.
        /// Linear rather than exponential on purpose: the player has to be able to look at a date
        /// and work out roughly when they will be clear, and an exponential decay is a curve nobody
        /// reads off a screen.
        /// </summary>
        private static double Weight(int age)
        {
            var through = Math.Clamp(age / (double)WindowDays, 0.0, 1.0);

            return 1.0 - (1.0 - FadedTo) * through;
        }
    }
}
