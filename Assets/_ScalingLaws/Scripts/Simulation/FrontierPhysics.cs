using System;
using ScalingLaws.Core;
using ScalingLaws.Data;

namespace ScalingLaws.Simulation
{
    /// <summary>
    /// The best model anybody could train on a given date, under this game's own scaling law.
    ///
    /// **It exists because the rivals were not bound by the physics the player is.** A rival past
    /// the end of the reference table gained two to five points a release, forever, until the index
    /// stopped it at 100 in 2031. The player's reach stopped at 90.9 at the end of 2028: ten thousand
    /// billion parameters is the top of the slider, the corpora stop growing in 2025 and the recipes
    /// stopped improving at sixty four times. Measured by `CeilingProbe` with every node researched
    /// the day the calendar allowed it and unlimited money, the player was still nine points behind
    /// the field from 2031 to the end of the campaign, which is a quarter of the pull, before brand.
    /// Seven years of the fourteen could not be won by anybody, however well they played, and
    /// `ScalingLaw` has always said that nothing in the campaign timeline reaches 100.
    ///
    /// **Derived through the planner, never beside it.** This asks <see cref="TrainingPlanner.Project"/>
    /// the same question the creator asks, with the largest run the slider allows, every corpus
    /// published by the date and every published family. A second formula here would be a second
    /// scaling law, and it would disagree with the first the day either one changed.
    ///
    /// A rival is a lab, not a player, so it is held to what could be built, not to what anyone
    /// has researched: the published date of a corpus or a family is the date a lab could use it.
    /// </summary>
    public static class FrontierPhysics
    {
        /// <summary>The top of the parameter slider, in billions: ten to the fourth.</summary>
        public const double LargestRunBillions = 10_000.0;

        private static int cachedDay = int.MinValue;
        private static double cachedValue;

        /// <summary>
        /// The highest capability a run could reach on the date. Cached by day, because every lab
        /// asks every day and the answer only changes when the calendar does.
        /// </summary>
        public static double ReachableOn(GameDate date)
        {
            if (date.DayIndex == cachedDay)
            {
                return cachedValue;
            }

            var value = Compute(date);
            cachedDay = date.DayIndex;
            cachedValue = value;
            return value;
        }

        private static double Compute(GameDate date)
        {
            var everything = DatasetSource.None;

            foreach (var entry in DatasetCatalog.All)
            {
                everything |= entry.Flag;
            }

            // A lab good enough to be at the frontier is good enough to generate data, so the
            // capability gate on the synthetic corpus is passed as met.
            var supply = DatasetCatalog.Blend(everything, 1e9, date, 100.0).AvailableTokensBillions;

            if (supply <= 0.0)
            {
                return 0.0;
            }

            var market = MarketModel.Evaluate(date, 0.0);
            var best = 0.0;

            foreach (var architecture in ArchitectureCatalog.AvailableOn(date))
            {
                var blueprint = new ModelBlueprint("frontier", architecture.Id, LargestRunBillions,
                    supply * 0.999, everything);

                var projection = TrainingPlanner.Project(blueprint, ComputeProfile.Empty, market, 100.0);

                best = Math.Max(best, projection.ProjectedCapability);
            }

            return Math.Clamp(SimUnits.Finite(best), 0.0, 100.0);
        }
    }
}
