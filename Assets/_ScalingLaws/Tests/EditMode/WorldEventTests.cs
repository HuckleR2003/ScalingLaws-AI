using System;
using System.Collections.Generic;
using NUnit.Framework;
using ScalingLaws.Core;
using ScalingLaws.Data;
using ScalingLaws.Simulation;

namespace ScalingLaws.Tests.EditMode
{
    /// <summary>
    /// The world happening around the company.
    ///
    /// **Everything in the calendar is a function of the date and nothing else**, which is the same
    /// promise `MarketConditions` already makes: nothing the player does moves these numbers, and
    /// that is what makes timing a skill rather than a stat. Nothing is stored, nothing is rolled,
    /// no save version is needed, and a campaign replays identically.
    ///
    /// The tests are about the three ways a dated calendar of real history goes wrong: a date that
    /// is not real and does not say so, two shocks compounding into a market nobody balanced for,
    /// and an event that moves the economy without ever reaching the player's screen.
    /// </summary>
    public sealed class WorldEventTests
    {
        /// <summary>Every day of the campaign, which is what a calendar has to be checked over.</summary>
        private static IEnumerable<GameDate> EveryWeek()
        {
            for (var day = 0; day < 5200; day += 7)
            {
                yield return new GameDate(day);
            }
        }

        /// <summary>
        /// Every entry has words, in both languages, and a headline is not its own key.
        /// </summary>
        [Test]
        public void EveryEventHasAHeadlineAndABodyInBothLanguages()
        {
            var was = Loc.Current;
            var missing = new List<string>();

            try
            {
                foreach (var language in new[] { Language.English, Language.Polish })
                {
                    Loc.Current = language;

                    foreach (var world in WorldEventCatalog.All)
                    {
                        var head = Loc.T(world.Key + ".head");
                        var body = Loc.T(world.Key + ".body");

                        if (head.EndsWith(".head", StringComparison.Ordinal))
                        {
                            missing.Add($"{language}/{world.Key}: no headline");
                        }

                        if (body.EndsWith(".body", StringComparison.Ordinal))
                        {
                            missing.Add($"{language}/{world.Key}: no body");
                        }
                    }
                }
            }
            finally
            {
                Loc.Current = was;
            }

            Assert.IsEmpty(missing,
                "A missing key renders as itself, so this would be a headline on the wire reading "
                + "`world.embargo.head`:\n  " + string.Join("\n  ", missing));
        }

        /// <summary>
        /// Anything dated past the record says it is a projection.
        ///
        /// **The honesty flag, applied to history rather than to hardware.** This roster is real
        /// events on real dates, so the line between what is on the record and what the game is
        /// guessing has to survive a player who was there for it. The cut-off is deliberately
        /// conservative: everything from 2026 on is the game's guess.
        /// </summary>
        [Test]
        public void EverythingPastTheRecordIsMarkedAsAGuess()
        {
            var record = GameDate.FromCalendar(2026, 1, 1);
            var unmarked = new List<string>();

            foreach (var world in WorldEventCatalog.All)
            {
                if (world.On.IsOnOrAfter(record) && !world.IsProjection)
                {
                    unmarked.Add($"{world.On} {world.Key} claims to be on the record");
                }

                if (!world.On.IsOnOrAfter(record) && world.IsProjection)
                {
                    unmarked.Add($"{world.On} {world.Key} is marked a guess and is inside the record");
                }
            }

            Assert.IsEmpty(unmarked, string.Join("\n  ", unmarked));
        }

        /// <summary>
        /// No lever is ever moved further than the balance was measured over.
        ///
        /// Two shocks on one curve compound, which is correct: two shortages really are worse than
        /// one. What is not correct is demand at four times or a quarter, because none of the
        /// economy was tuned anywhere near there. Checked across the whole campaign rather than at
        /// the individual entries, because the compounding is the thing that can surprise.
        /// </summary>
        [Test]
        public void NoLeverIsEverMovedFurtherThanTheBalanceWasMeasuredOver()
        {
            var worst = new List<string>();

            foreach (WorldLever lever in Enum.GetValues(typeof(WorldLever)))
            {
                if (lever == WorldLever.None)
                {
                    continue;
                }

                foreach (var date in EveryWeek())
                {
                    var multiplier = WorldEventCatalog.MultiplierOn(lever, date);

                    if (multiplier > 1.0 + WorldEventCatalog.MostAnyLeverMoves + 0.0001
                        || multiplier < 1.0 - WorldEventCatalog.MostAnyLeverMoves - 0.0001)
                    {
                        worst.Add($"{lever} on {date} reaches {multiplier:0.00}x");
                    }
                }
            }

            Assert.IsEmpty(worst,
                "Two shocks compounding is right; compounding past the band is a market nobody "
                + "tuned:\n  " + string.Join("\n  ", worst));
        }

        /// <summary>
        /// The calendar reaches the economy, and it reaches it in exactly one place per curve.
        ///
        /// **The failure this catches is the expensive one**: a hook applied at each reader rather
        /// than at the curve, so a shortage doubles the rent and forgets the purchase price. Both
        /// prices come off `ScarcityOn`, so measuring the one number is enough.
        /// </summary>
        [Test]
        public void AShortageReachesBothTheRentAndThePurchasePrice()
        {
            // The export controls window, well inside it, against a quiet week two years earlier.
            var during = GameDate.FromCalendar(2022, 11, 1);
            var scarcityDuring = MarketModel.ScarcityOn(during);

            Assert.Greater(WorldEventCatalog.MultiplierOn(WorldLever.Scarcity, during), 1.0,
                "The calendar says nothing is happening to supply in late 2022, which is wrong.");

            var rentable = MarketModel.RentableGenerationOn(during);

            var rentAtCalm = MarketModel.RentPricePerPetaflopHourUsd(rentable, 0.2);
            var rentAtShock = MarketModel.RentPricePerPetaflopHourUsd(rentable, scarcityDuring);

            Assert.Greater(rentAtShock, rentAtCalm,
                "Scarcity does not reach the rental price, so a shortage costs a cloud tenant "
                + "nothing.");

            var generation = HardwareCatalog.Get(rentable);
            var tier = ComputeTierCatalog.Get(ComputeTier.ColocatedServers);

            Assert.Greater(
                MarketModel.PurchasePricePerUnitUsd(generation, tier, scarcityDuring),
                MarketModel.PurchasePricePerUnitUsd(generation, tier, 0.2),
                "Scarcity does not reach the purchase price, so buying is free of the shortage "
                + "that is making renting expensive.");
        }

        /// <summary>
        /// The day the world changed is the day the demand curve says so.
        ///
        /// Worth its own test rather than folding into the sweep above, because this is the single
        /// most consequential entry in the file and the one most likely to be quietly retuned to
        /// nothing while trying to fix something else.
        /// </summary>
        [Test]
        public void TheLaunchThatChangedEverythingActuallyMovesTheMarket()
        {
            var before = GameDate.FromCalendar(2022, 11, 29);
            var after = GameDate.FromCalendar(2022, 12, 2);

            var jump = MarketModel.DemandOn(after) / MarketModel.DemandOn(before);

            Assert.Greater(jump, 1.4,
                "The launch is in the calendar and the demand curve barely notices it.");
        }

        /// <summary>
        /// Every event reaches the wire on the day it starts.
        ///
        /// An event that moves the economy and never appears on a screen is the shape of fault this
        /// project has shipped eleven times: complete, correct, and impossible to know about.
        /// </summary>
        [Test]
        public void EveryEventReachesTheWireOnTheDayItStarts()
        {
            var silent = new List<string>();

            foreach (var world in WorldEventCatalog.All)
            {
                var starting = WorldEventCatalog.StartingOn(world.On);
                var found = false;

                foreach (var entry in starting)
                {
                    found |= entry.Key == world.Key;
                }

                if (!found)
                {
                    silent.Add($"{world.On} {world.Key} does not report itself as starting");
                    continue;
                }

                var item = NewsDesk.FromWorldEvent(world);

                if (string.IsNullOrWhiteSpace(item.Headline))
                {
                    silent.Add($"{world.Key} files an empty headline");
                }

                if (world.IsProjection && !item.Body.Contains(Loc.T("world.projection")))
                {
                    silent.Add($"{world.Key} is a guess and its news item does not say so");
                }
            }

            Assert.IsEmpty(silent, string.Join("\n  ", silent));
        }

        /// <summary>
        /// A shock decays rather than switching off.
        ///
        /// A supply crunch that ends on a Tuesday at full strength is a cliff no real market has
        /// had, and it would teach the player the exact day rather than the shape.
        /// </summary>
        [Test]
        public void EveryShockFadesRatherThanStopping()
        {
            foreach (var world in WorldEventCatalog.All)
            {
                Assert.AreEqual(1.0, world.StrengthOn(world.On), 0.0001,
                    $"{world.Key} does not start at full strength.");

                var middle = world.On.AddDays(world.Days / 2);
                var late = world.On.AddDays((int)(world.Days * 0.9));

                Assert.Less(world.StrengthOn(late), world.StrengthOn(middle),
                    $"{world.Key} is as strong at the end as it is in the middle.");

                Assert.AreEqual(0.0, world.StrengthOn(world.On.AddDays(world.Days)), 0.0001,
                    $"{world.Key} is still running after its window has closed.");
            }
        }
    }
}
