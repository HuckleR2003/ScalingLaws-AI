using System;

namespace ScalingLaws.Data
{
    /// <summary>
    /// What it costs to walk away from something already running.
    ///
    /// **Reported by a tester, with the numbers, and the numbers are his.** *"I missclicked and I
    /// started training a new model but it had the same stats as the one I had before. I think it
    /// would be cool to be able to cancel any research, training and upgrade. However, to balance
    /// things out, I think you should add some penalties."*
    ///
    /// Until now all three could be abandoned for nothing at all, which is the opposite mistake: a
    /// two hundred day run thrown away at no cost is a free reroll on every decision in the game.
    ///
    /// **The two penalties are different shapes because the two things are different.** A research
    /// node is a named thing on a tree that the company will almost certainly come back to, so what
    /// it loses is progress. A training run or an upgrade is a bespoke plan that will never exist
    /// again, so there is nothing to bank and what it costs is money.
    ///
    /// **Neither of them rewinds the calendar, and that is what keeps this inside the spine.** The
    /// days already spent on the cluster are gone whatever you do. Cancelling buys you out of
    /// spending *more* of them on a plan you no longer want; it does not hand any back.
    /// </summary>
    public static class CancellationPolicy
    {
        /// <summary>
        /// How much of a cancelled node's progress is still there next time.
        ///
        /// The tester's figure: "if you cancel it when you had spent 200 days the next time you try
        /// to finish the research it will start at 160 days". Twenty per cent is enough that
        /// stopping and starting is never free and small enough that a change of mind is survivable.
        ///
        /// **The cash is not banked with it.** Restarting the node charges its price again, which is
        /// the other half of what stops a player flipping between two nodes for nothing.
        /// </summary>
        public const double ResearchProgressKept = 0.80;

        /// <summary>
        /// The fee to abandon a run, an upgrade or a family programme.
        ///
        /// He offered five or ten. Ten, because it is charged on **what the thing has cost so far**
        /// rather than on what it was going to cost: a misclick noticed on the first day is almost
        /// free, which is the case he was actually describing, and walking away from a run six
        /// months in is expensive, which it should be.
        /// </summary>
        public const double AbandonFeeShare = 0.10;

        /// <summary>What walking away costs, on a bill of this size. Never negative, never a cent.</summary>
        public static long FeeOn(long spentUsd) =>
            spentUsd <= 0L ? 0L : (long)Math.Round(spentUsd * AbandonFeeShare);

        /// <summary>What a node comes back at, having been abandoned at this much progress.</summary>
        public static int DaysKept(int daysCompleted) =>
            daysCompleted <= 0 ? 0 : (int)Math.Floor(daysCompleted * ResearchProgressKept);

        /// <summary>The same for the compute, which `ResearchProject.IsComplete` also requires.</summary>
        public static double ComputeKept(double petaflopDays) =>
            petaflopDays <= 0.0 ? 0.0 : petaflopDays * ResearchProgressKept;
    }
}
