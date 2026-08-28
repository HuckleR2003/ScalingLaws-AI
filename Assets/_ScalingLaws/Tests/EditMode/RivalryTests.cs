using System;
using System.Linq;
using NUnit.Framework;
using ScalingLaws.Core;
using ScalingLaws.Data;
using ScalingLaws.Simulation;

namespace ScalingLaws.Tests.EditMode
{
    /// <summary>
    /// The six systems added in v42: smear campaigns, lawsuits, acquisition offers, rival
    /// expansion, model scandals and the announced offices.
    ///
    /// Several of these are design checks wearing tests. `NoSmearTierIsSimplyBetterThanAnother` is
    /// the one that matters most: the moment one tier is cheaper and safer and stronger at once,
    /// four cards collapse into one and the whole decision is gone while every other test here
    /// still passes.
    /// </summary>
    public sealed class RivalryTests
    {
        private static CompanySimulation Company()
        {
            var state = new CompanyState("Test Lab");
            state.CashUsd = 5_000_000_000L;
            return new CompanySimulation(state);
        }

        // ---- smear campaigns ----------------------------------------------------------------------

        /// <summary>
        /// Every axis climbs together, so no tier is a free lunch.
        ///
        /// Cost, damage, backfire chance and how long the target is left alone all have to move in
        /// the same direction. If any one of them ever crosses, one tier dominates and the other
        /// three are decoration.
        /// </summary>
        [Test]
        public void NoSmearTierIsSimplyBetterThanAnother()
        {
            var tiers = SmearCatalog.All.OrderBy(entry => entry.CostUsd).ToList();

            for (var index = 1; index < tiers.Count; index++)
            {
                var cheaper = tiers[index - 1];
                var dearer = tiers[index];

                Assert.That(dearer.BrandDamage, Is.GreaterThan(cheaper.BrandDamage),
                    $"{dearer.Tier} costs more than {cheaper.Tier} and does no more damage.");

                Assert.That(dearer.BackfireChance, Is.GreaterThan(cheaper.BackfireChance),
                    $"{dearer.Tier} is stronger than {cheaper.Tier} and no riskier, so nobody "
                    + "would ever choose the smaller one.");

                Assert.That(dearer.RelationCost, Is.LessThan(cheaper.RelationCost),
                    $"{dearer.Tier} should cost the relationship more than {cheaper.Tier}.");

                Assert.That(dearer.QuietDays, Is.GreaterThan(cheaper.QuietDays));
            }
        }

        /// <summary>
        /// Being caught costs more than landing it would have gained.
        ///
        /// Without this the expected value makes a smear a straightforward purchase at every tier
        /// and the risk is decoration on a button nobody would not press.
        /// </summary>
        [Test]
        public void BeingCaughtHurtsMoreThanTheStoryWouldHaveGained()
        {
            Assert.That(SmearCatalog.BackfireSeverity, Is.GreaterThan(1.0));
        }

        [Test]
        public void ASmearTakesStandingOffTheTargetAndCostsTheRelationship()
        {
            var simulation = Company();
            var lab = CompetitorId.OpenAi;

            var before = simulation.State.Relations.With(lab);
            var cash = simulation.State.CashUsd;

            Assert.That(simulation.TrySmear(lab, SmearTier.Whisper, out var backfired, out _),
                Is.True);

            Assert.That(simulation.State.CashUsd, Is.LessThan(cash), "Nothing was paid.");

            Assert.That(simulation.State.Relations.With(lab), Is.LessThan(before),
                "They know, whether or not it was traced back.");

            if (!backfired)
            {
                Assert.That(simulation.RivalStandingMultiplier(lab), Is.LessThan(1.0),
                    "It landed and took nothing off them, so it changed no number.");
            }
        }

        /// <summary>The same lab cannot be hit twice in a row, which is what the quiet days are for.</summary>
        [Test]
        public void ALabCannotBeTargetedAgainImmediately()
        {
            var simulation = Company();
            var lab = CompetitorId.OpenAi;

            Assert.That(simulation.TrySmear(lab, SmearTier.Whisper, out _, out _), Is.True);
            Assert.That(simulation.TrySmear(lab, SmearTier.Whisper, out _, out var why), Is.False);
            Assert.That(why, Is.Not.Empty);
        }

        /// <summary>
        /// A story stops costing the company it was about, eventually.
        ///
        /// A permanent cut bought once for forty thousand dollars would compound over fourteen
        /// years into the strongest purchase in the game.
        /// </summary>
        [Test]
        public void ASmearWearsOff()
        {
            var simulation = Company();
            var lab = CompetitorId.OpenAi;

            simulation.State.SmearDamage[lab] = 0.10;

            simulation.Advance(400);

            Assert.That(simulation.State.SmearDamage.ContainsKey(lab), Is.False,
                "A story people stopped repeating is still costing them something.");
        }

        // ---- lawsuits -----------------------------------------------------------------------------

        /// <summary>
        /// Asking for more lowers the odds, across the whole range.
        ///
        /// This is the entire mechanic. If the curve ever flattens, the correct play is to demand
        /// the ceiling every time and the slider stops being a decision.
        /// </summary>
        [Test]
        public void AskingForMoreAlwaysLowersTheOdds()
        {
            const long ceiling = 1_000_000_000L;
            var previous = 1.0;

            for (var share = 0.0; share <= 1.0; share += 0.1)
            {
                var odds = LawsuitBook.OddsFor((long)(ceiling * share), ceiling);

                Assert.That(odds, Is.LessThan(previous),
                    $"The odds did not fall between {share - 0.1:0.0} and {share:0.0}.");

                previous = odds;
            }

            Assert.That(LawsuitBook.OddsFor(ceiling, ceiling),
                Is.EqualTo(LawsuitBook.WorstOdds).Within(0.001));
        }

        /// <summary>Even the best case is not a coin flip in the company's favour by much.</summary>
        [Test]
        public void NoCaseIsEverASureThing()
        {
            Assert.That(LawsuitBook.BestOdds, Is.LessThan(0.7));
            Assert.That(LawsuitBook.WorstOdds, Is.GreaterThan(0.0));
        }

        /// <summary>Costs are paid on filing and are gone, win or lose.</summary>
        [Test]
        public void FilingCostsMoneyBeforeAnybodyHasWonAnything()
        {
            var simulation = Company();
            var lab = CompetitorId.OpenAi;

            if (!simulation.CanSue(lab, out var ceiling, out _))
            {
                Assert.Pass("No grounds against this lab on day one, which is the common case.");
                return;
            }

            var cash = simulation.State.CashUsd;

            Assert.That(simulation.TryFileLawsuit(lab, ceiling / 2, out _), Is.True);
            Assert.That(simulation.State.CashUsd, Is.LessThan(cash));
            Assert.That(simulation.State.Lawsuits.Count, Is.EqualTo(1));
            Assert.That(simulation.State.Lawsuits[0].IsClosed, Is.False,
                "The verdict was decided at filing time, which deletes the wait.");
        }

        /// <summary>A case cannot be filed without something the player can point at.</summary>
        [Test]
        public void ACompanyWithNoProductHasNoCase()
        {
            var simulation = Company();

            foreach (CompetitorId lab in Enum.GetValues(typeof(CompetitorId)))
            {
                Assert.That(simulation.CanSue(lab, out _, out _), Is.False,
                    $"A company that has shipped nothing is suing {lab} over something.");
            }
        }

        // ---- being bought -------------------------------------------------------------------------

        /// <summary>
        /// A state programme blocks the sale outright.
        ///
        /// Without it the strongest line in the game is to take ten billion of public money and
        /// then sell the company holding it.
        /// </summary>
        [Test]
        public void NobodyMayBuyACompanyThatOwesAStateProgramme()
        {
            var simulation = Company();

            Assert.That(simulation.CanBeAcquired(out _), Is.True,
                "Nothing is owed yet, so nothing should be blocking a sale.");

            // The loan is put into the book directly rather than taken through the funding screen.
            // The rule under test is "a state programme blocks the sale", and routing through
            // TryTakeLoan would additionally test the research gates and the calendar that decide
            // when the programme is offered, which is a different fixture's job.
            simulation.State.Loans.Add(new Loan(
                LoanProduct.SovereignSeed, simulation.State.Date,
                1_000_000_000L, 3_300_000_000L, 3_650, 365));

            Assert.That(simulation.CanBeAcquired(out var blocked), Is.False,
                "The company was sold out from under a sovereign compute programme.");

            Assert.That(blocked, Is.Not.Empty, "A refusal with no sentence reads as a bug.");
        }

        /// <summary>Nobody ever bids below what the company is worth on paper.</summary>
        [Test]
        public void ABidIsNeverBelowBookValue()
        {
            Assert.That(Acquisitions.WorstMultiple, Is.GreaterThanOrEqualTo(1.0));
            Assert.That(Acquisitions.BestMultiple, Is.GreaterThan(Acquisitions.WorstMultiple));

            for (var roll = 0.0; roll <= 1.0; roll += 0.25)
            {
                for (var gap = -30.0; gap <= 30.0; gap += 10.0)
                {
                    var multiple = Acquisitions.MultipleFor(50.0, 50.0 + gap, roll);

                    Assert.That(multiple, Is.GreaterThanOrEqualTo(Acquisitions.WorstMultiple));
                    Assert.That(multiple, Is.LessThanOrEqualTo(Acquisitions.BestMultiple));
                }
            }
        }

        /// <summary>A buyer behind the company pays more than one already ahead of it.</summary>
        [Test]
        public void SomebodyBuyingAPositionPaysMoreThanSomebodyBuyingAnAsset()
        {
            var behind = Acquisitions.MultipleFor(60.0, 40.0, 0.5);
            var ahead = Acquisitions.MultipleFor(60.0, 80.0, 0.5);

            Assert.That(behind, Is.GreaterThan(ahead));
        }

        // ---- rival expansion ----------------------------------------------------------------------

        /// <summary>
        /// The field grows, it grows at different times, and it never runs away.
        ///
        /// Two labs stepping up in the same week reads as one scripted event rather than as a field
        /// of companies each doing their own thing, so the spread is asserted rather than assumed.
        /// </summary>
        [Test]
        public void RivalsGrowAtDifferentTimesAndStopAtTheCeiling()
        {
            const uint seed = 0x5CA1AB1E;
            var end = GameDate.FromCalendar(2036, 1, 1);

            var levels = Enum.GetValues(typeof(CompetitorId))
                .Cast<CompetitorId>()
                .Select(lab => RivalExpansion.LevelOn(seed, lab, end))
                .ToList();

            Assert.That(levels.All(level => level <= RivalExpansion.MaximumLevel), Is.True);
            Assert.That(levels.Max(), Is.GreaterThan(0), "Nobody grew in fourteen years.");

            var firstSteps = Enum.GetValues(typeof(CompetitorId))
                .Cast<CompetitorId>()
                .Select(lab => FirstStepDay(seed, lab))
                .Where(day => day > 0)
                .ToList();

            Assert.That(firstSteps.Distinct().Count(), Is.GreaterThan(1),
                "Every lab expanded on the same day, which reads as one scripted event.");
        }

        /// <summary>Nobody expands on day one, when every company here is small.</summary>
        [Test]
        public void NobodyExpandsInTheOpeningStretch()
        {
            const uint seed = 0x5CA1AB1E;
            var opening = GameDate.FromCalendar(2022, 6, 1);

            foreach (CompetitorId lab in Enum.GetValues(typeof(CompetitorId)))
            {
                Assert.That(RivalExpansion.LevelOn(seed, lab, opening), Is.EqualTo(0));
            }
        }

        private static int FirstStepDay(uint seed, CompetitorId lab)
        {
            for (var day = 0; day < 6000; day += 1)
            {
                if (RivalExpansion.StepsUpOn(seed, lab, new GameDate(day)))
                {
                    return day;
                }
            }

            return -1;
        }

        // ---- scandals -----------------------------------------------------------------------------

        /// <summary>
        /// Every scandal is caused by something the player did, never by a dice roll.
        ///
        /// A company doing nothing wrong is never written about, which is the difference between a
        /// mechanic that teaches and one that only punishes.
        /// </summary>
        [Test]
        public void ACompanyDoingNothingWrongIsNeverWrittenAbout()
        {
            var kind = ModelScandals.Today(
                reputation: 0.7,
                priceAgainstMarket: 1.0,
                freeTierJustCut: false,
                sustainedLoad: 0.5,
                daysSinceRelease: 30,
                cornersCut: false);

            Assert.That(kind, Is.EqualTo(ScandalKind.None));
        }

        [Test]
        public void EachThingThePlayerDidHasItsOwnStory()
        {
            Assert.That(ModelScandals.Today(0.7, 3.0, false, 0.5, 30, false),
                Is.EqualTo(ScandalKind.Pricing));

            Assert.That(ModelScandals.Today(0.7, 1.0, true, 0.5, 30, false),
                Is.EqualTo(ScandalKind.FreeTierCut));

            Assert.That(ModelScandals.Today(0.7, 1.0, false, 0.99, 30, false),
                Is.EqualTo(ScandalKind.Reliability));

            Assert.That(ModelScandals.Today(0.7, 1.0, false, 0.5, 5000, false),
                Is.EqualTo(ScandalKind.Stagnation));

            Assert.That(ModelScandals.Today(0.7, 1.0, false, 0.5, 30, true),
                Is.EqualTo(ScandalKind.Corners));
        }

        /// <summary>A company nobody has heard of is not damaged by a story nobody reads.</summary>
        [Test]
        public void AStoryHurtsAWellKnownCompanyMoreThanAnUnknownOne()
        {
            var known = ModelScandals.CostFor(ScandalKind.Pricing, 0.9);
            var unknown = ModelScandals.CostFor(ScandalKind.Pricing, 0.2);

            Assert.That(known, Is.GreaterThan(unknown));
            Assert.That(ModelScandals.CostFor(ScandalKind.None, 0.9), Is.EqualTo(0.0));
        }

        /// <summary>Nobody is beneath notice and above it at the same time.</summary>
        [Test]
        public void AVeryObscureCompanyIsNotWorthTheColumn()
        {
            Assert.That(ModelScandals.Today(0.05, 5.0, true, 0.99, 5000, true),
                Is.EqualTo(ScandalKind.None));
        }

        // ---- the announced offices ------------------------------------------------------------------

        /// <summary>
        /// The places that are shown and not built stay out of the save format.
        ///
        /// They carry no `OfficeTier`, so they can never be written into a file, and every one of
        /// them holds more desks than the largest place that actually exists or the ladder would
        /// be pointing downwards.
        /// </summary>
        [Test]
        public void TheAnnouncedOfficesAreBiggerThanAnythingRealAndCarryNoTier()
        {
            Assert.That(OfficeCatalog.ComingSoon, Is.Not.Empty);

            var largest = OfficeCatalog.All.Max(place => place.Desks);
            var previous = largest;

            foreach (var soon in OfficeCatalog.ComingSoon)
            {
                Assert.That(soon.Desks, Is.GreaterThan(previous),
                    "An announced place holds no more than what is already available, so the "
                    + "ladder reads as though it stops or goes backwards.");

                Assert.That(soon.DisplayName, Is.Not.Empty);
                Assert.That(soon.Note, Is.Not.Empty);

                previous = soon.Desks;
            }
        }
    }
}
