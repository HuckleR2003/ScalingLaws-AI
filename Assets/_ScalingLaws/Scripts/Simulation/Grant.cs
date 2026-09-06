using System;
using ScalingLaws.Core;
using ScalingLaws.Data;

namespace ScalingLaws.Simulation
{
    /// <summary>
    /// An award the company has accepted and is now working off.
    ///
    /// **The baseline is captured on the day it is signed, never derived afterwards.** A grant that
    /// asks for three models means three more, and reading the released count at the close would
    /// hand the award to a company that had already shipped three before the body ever wrote to
    /// them. That is the same reasoning that makes `LastQuality` and the release-line shares causal
    /// state rather than derived: the number describes a day that has passed, so it has to be
    /// recorded on the day, and it has to be saved.
    /// </summary>
    public sealed class Grant
    {
        public Grant(GrantId id, GameDate startedOn, double baseline)
        {
            Id = id;
            StartedOn = startedOn;
            Baseline = SimUnits.Finite(baseline);
        }

        public GrantId Id { get; }
        public GameDate StartedOn { get; }

        /// <summary>Where the measured quantity stood when the award was signed.</summary>
        public double Baseline { get; }

        public int DaysElapsed { get; private set; }

        /// <summary>
        /// Whether the term has started running.
        ///
        /// **A sustained condition cannot be tested against a company that is not trading.** With
        /// nothing on sale there are no incidents, no load on the fleet and nobody being charged, so
        /// every one of those conditions holds perfectly and the term runs out into a payout for
        /// doing nothing. The clock starts on the first day the company has a product live.
        ///
        /// Causal, so it is saved. It is what tomorrow's tick reads to decide whether to age the
        /// award, and this project has now been caught seven times by state that looked derived.
        /// </summary>
        public bool HasBegun { get; private set; }

        /// <summary>
        /// Set the moment a sustained condition is broken, and never cleared.
        ///
        /// A sustained award is lost on the day it is broken rather than at the closing date,
        /// because that is what "every day" means, and because a player who has already lost it
        /// should be told at once instead of running an impossible term to its end.
        /// </summary>
        public bool IsBroken { get; private set; }

        public GrantDefinition Definition => GrantCatalog.Get(Id);

        public int TermDays => Definition.TermDays;
        public int DaysLeft => Math.Max(0, TermDays - DaysElapsed);
        public bool HasClosed => DaysElapsed >= TermDays;

        public double Progress => TermDays <= 0
            ? 1.0
            : Math.Clamp(DaysElapsed / (double)TermDays, 0.0, 1.0);

        public void Advance() => DaysElapsed = Math.Min(TermDays, DaysElapsed + 1);

        /// <summary>
        /// Starts the term. Once, and never taken back.
        ///
        /// Retiring the last model does not stop the clock again. A term that could be paused would
        /// be a term the player can hold open indefinitely, and the deadline is the entire pressure
        /// a grant applies.
        /// </summary>
        public void Begin() => HasBegun = true;

        public void Break() => IsBroken = true;

        /// <summary>
        /// Puts a saved award back.
        ///
        /// <paramref name="begun"/> defaults to true because that is the honest reading of every
        /// file written before the term could wait: those awards had been ageing since the day they
        /// were signed, and starting them again would hand the player back the days already spent.
        /// </summary>
        public void Restore(int daysElapsed, bool broken, bool begun = true)
        {
            DaysElapsed = Math.Clamp(daysElapsed, 0, TermDays);
            IsBroken = broken;
            HasBegun = begun;
        }
    }

    /// <summary>
    /// What a body is measuring, read off state the game already keeps.
    ///
    /// Nothing here invents a figure or stores one of its own. Every reading is something the
    /// simulation computes for its own reasons, which is the same rule the lawsuit grounds follow:
    /// a condition the player cannot point at on some other screen is a condition they cannot plan
    /// around.
    /// </summary>
    public static class GrantConditions
    {
        /// <summary>
        /// The quantity the award is measured on, today.
        ///
        /// `flagshipCapability` and `utilisation` are passed in rather than read off the state,
        /// because both are day-report figures the caller has already computed and recomputing them
        /// here would be a second opinion about the same day.
        /// </summary>
        public static double Reading(GrantGoal goal, CompanyState state, double flagshipCapability,
            double utilisation)
        {
            return goal switch
            {
                GrantGoal.ReleaseModels => state.ReleasedModelCount,
                GrantGoal.ReachCapability => SimUnits.Finite(flagshipCapability),
                GrantGoal.FinishResearch => state.UnlockedResearch.Count,
                GrantGoal.EmployPeople => state.Staff.Headcount,
                GrantGoal.SustainFreeTier => state.Monetization.Generosity,
                GrantGoal.SustainHeadroom => SimUnits.Finite(utilisation),
                GrantGoal.SustainReputation => state.Reputation,
                GrantGoal.ShipProtected => BestDataProtectionOnSale(state),
                GrantGoal.SustainOnSale => LiveModelCount(state),

                // The day something last went wrong. Compared against the day it had last gone
                // wrong when the grant was signed, which is the baseline: anything later is an
                // incident on this body's watch.
                GrantGoal.NoIncidents => state.LastTroubleDayIndex,
                _ => 0.0
            };
        }

        /// <summary>
        /// Whether the condition is being met right now.
        ///
        /// **Headroom is the one that reads the other way round**, because the body is asking the
        /// company to stay *below* a load rather than above a figure. Writing it as a separate
        /// branch rather than by negating the target keeps the catalog readable: 0.75 there means
        /// three quarters loaded, which is what a person would expect it to mean.
        /// </summary>
        public static bool IsMet(GrantGoal goal, double baseline, double target, double reading)
        {
            if (goal == GrantGoal.SustainHeadroom)
            {
                return reading <= target;
            }

            // **The other one that reads backwards**, and for a different reason: the body is
            // asking for the absence of something. Nothing has gone wrong since signing while the
            // last thing that went wrong is no newer than the day it was signed.
            if (goal == GrantGoal.NoIncidents)
            {
                return reading <= baseline;
            }

            if (goal == GrantGoal.ReachCapability)
            {
                return reading >= baseline + target;
            }

            if (goal is GrantGoal.ReleaseModels or GrantGoal.FinishResearch)
            {
                return reading >= baseline + target;
            }

            return reading >= target;
        }

        /// <summary>
        /// Whether the company is trading: something on sale, or money already taken for it.
        ///
        /// **What a sustained term waits for.** Both halves are needed and they are not the same
        /// day. A model goes live before anybody has paid for it, so the first half starts the
        /// clock on the day the product exists rather than on the day the first invoice clears; and
        /// a company that has retired its last model has still been selling, so the second half
        /// stops the clock being restartable by taking a product down.
        ///
        /// Read off figures the company already keeps, which is the rule every grant condition
        /// follows: a term that starts on something the player cannot see on another screen is a
        /// term they cannot plan around.
        /// </summary>
        public static bool IsTrading(CompanyState state) =>
            state != null
            && (LiveModelCount(state) > 0 || state.LifetimeRevenueUsd > 0L);

        /// <summary>
        /// Products actually on sale today.
        ///
        /// Live rather than released: a company that has shipped nine models and retired all of
        /// them is selling nothing, and a grant for keeping a service running has to mean the
        /// service is running.
        /// </summary>
        private static int LiveModelCount(CompanyState state)
        {
            var live = 0;

            foreach (var model in state.DeployedModels)
            {
                if (model.IsLiveOn(state.Date))
                {
                    live++;
                }
            }

            return live;
        }

        /// <summary>The strongest data protection on anything the company currently sells.</summary>
        private static double BestDataProtectionOnSale(CompanyState state)
        {
            var best = -1;

            foreach (var model in state.DeployedModels)
            {
                if (model.DataProtectionTier > best)
                {
                    best = model.DataProtectionTier;
                }
            }

            return best;
        }
    }
}
