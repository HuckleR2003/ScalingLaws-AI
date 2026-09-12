using System.Linq;
using NUnit.Framework;
using ScalingLaws.Core;
using ScalingLaws.Data;
using ScalingLaws.Persistence;
using ScalingLaws.Simulation;

namespace ScalingLaws.Tests.EditMode
{
    /// <summary>
    /// Walking away from something already running, and what it costs.
    ///
    /// **Asked for by a tester, with his own numbers.** *"I missclicked and I started training a new
    /// model but it had the same stats as the one I had before. I think it would be cool to be able
    /// to cancel any research, training and upgrade. However, to balance things out, I think you
    /// should add some penalties."*
    ///
    /// Before this, all three could be abandoned for nothing at all, which is the opposite fault: a
    /// two hundred day run thrown away free is a reroll on every decision in the game.
    /// </summary>
    public sealed class CancellationTests
    {
        /// <param name="petaflops">
        /// The cluster. Small for the training fixtures on purpose: four hundred petaflops finishes
        /// a twenty billion parameter run inside a month, and what those tests need is a run that is
        /// still going two months in so there is something to walk away from.
        /// </param>
        private static CompanySimulation Running(double petaflops = 400.0)
        {
            var simulation = new CompanySimulation(new CompanyState("Prometheus AI"));
            simulation.State.CashUsd = 400_000_000L;
            simulation.SetRentedPetaflops(petaflops);

            // Points, or nothing on the tree can be started at all and every research fixture here
            // would skip itself rather than measure anything.
            simulation.State.ResearchPoints = 200_000.0;
            return simulation;
        }

        /// <summary>
        /// A run that is long enough to walk away from part way through.
        ///
        /// **Written out rather than planned.** The planner sizes a run to a compute budget, and
        /// the two things this fixture needs are in tension there: small enough to sit under the
        /// scale ceiling a company starts beneath, and long enough on the calendar that it is still
        /// going sixty days in. Twenty billion parameters against a large corpus is both, and it is
        /// the size the rest of the economy is measured against.
        /// </summary>
        private static ModelBlueprint Plan() => new(
            "Subject", ArchitectureId.DenseTransformer, 20.0, 1_800.0, DatasetSource.WebCrawl);

        private static bool StartAnyResearch(CompanySimulation simulation)
        {
            foreach (var node in ResearchTree.All)
            {
                if (simulation.TryStartResearch(node.Id, out _))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// **The tester's own example, as arithmetic.** Two hundred days in, abandoned, and it comes
        /// back at one hundred and sixty.
        /// </summary>
        [Test]
        public void AbandoningANodeKeepsFourFifthsOfIt()
        {
            Assert.That(CancellationPolicy.DaysKept(200), Is.EqualTo(160),
                "Two hundred days abandoned should come back at one hundred and sixty, which is "
                + "the figure the report asked for.");

            Assert.That(CancellationPolicy.DaysKept(0), Is.Zero);
        }

        [Test]
        public void ANodeAbandonedPartWayStartsAgainWhereItWasLeft()
        {
            var simulation = Running();
            Assume.That(StartAnyResearch(simulation), Is.True, "nothing could be started");

            var node = simulation.State.ActiveResearch.Node;

            for (var day = 0; day < 40; day++)
            {
                simulation.AdvanceDay();
            }

            var reached = simulation.State.ActiveResearch.DaysCompleted;
            Assume.That(reached, Is.GreaterThan(4), "the node barely moved, so this measures little");

            Assert.That(simulation.TryCancelResearch(out var why), Is.True, why);

            Assert.That(simulation.State.BankedResearch.ContainsKey(node), Is.True,
                "The node was abandoned and nothing was kept, so the next attempt starts from "
                + "scratch and the penalty is the whole thing rather than a fifth of it.");

            Assert.That(simulation.State.BankedResearch[node].Days,
                Is.EqualTo(CancellationPolicy.DaysKept(reached)));

            Assert.That(simulation.TryStartResearch(node, out var restartWhy), Is.True, restartWhy);

            Assert.That(simulation.State.ActiveResearch.DaysCompleted,
                Is.EqualTo(CancellationPolicy.DaysKept(reached)),
                "It started again from zero, so the bank is written and never read.");

            Assert.That(simulation.State.BankedResearch.ContainsKey(node), Is.False,
                "The bank still holds the node after it was spent, so abandoning it again would "
                + "bank a figure that already included this one and the player could ratchet it up.");
        }

        /// <summary>
        /// **Stopping and starting is never free**, or the penalty is decoration. Each cycle costs a
        /// fifth of the progress and the node's cash again.
        /// </summary>
        [Test]
        public void FlippingANodeOnAndOffLosesGroundEveryTime()
        {
            var simulation = Running();
            Assume.That(StartAnyResearch(simulation), Is.True);

            var node = simulation.State.ActiveResearch.Node;

            for (var day = 0; day < 40; day++)
            {
                simulation.AdvanceDay();
            }

            var first = simulation.State.ActiveResearch.DaysCompleted;
            Assume.That(first, Is.GreaterThan(8));

            simulation.TryCancelResearch(out _);
            simulation.TryStartResearch(node, out _);

            var afterOne = simulation.State.ActiveResearch.DaysCompleted;

            simulation.TryCancelResearch(out _);
            simulation.TryStartResearch(node, out _);

            var afterTwo = simulation.State.ActiveResearch.DaysCompleted;

            Assert.That(afterOne, Is.LessThan(first), "The first cancel cost nothing.");
            Assert.That(afterTwo, Is.LessThan(afterOne), "The second cancel cost nothing.");
        }

        /// <summary>
        /// **A misclick on the first morning is nearly free and six months in is not.** The fee is
        /// charged on what the run has spent rather than on what it was going to.
        /// </summary>
        [Test]
        public void AbandoningARunCostsMoreTheLongerItHasBeenGoing()
        {
            var simulation = Running(25.0);
            Assume.That(simulation.TryStartTraining(Plan(), out var why), Is.True, why);

            var early = simulation.TrainingAbandonFeeUsd();

            for (var day = 0; day < 60; day++)
            {
                simulation.AdvanceDay();
            }

            var late = simulation.TrainingAbandonFeeUsd();

            Assert.That(late, Is.GreaterThan(early),
                "Walking away sixty days in costs the same as walking away on the first morning, "
                + "so the fee is not reading what the run has actually spent.");
        }

        [Test]
        public void AbandoningARunChargesTheFeeAndStopsTheRun()
        {
            var simulation = Running(25.0);

            Assume.That(simulation.TryStartTraining(Plan(), out var why), Is.True, why);

            for (var day = 0; day < 30; day++)
            {
                simulation.AdvanceDay();
            }

            var cashBefore = simulation.State.CashUsd;
            var quoted = simulation.TrainingAbandonFeeUsd();

            Assert.That(simulation.TryCancelTraining(out var charged, out var reason), Is.True, reason);

            Assert.That(simulation.State.ActiveRun, Is.Null, "The run is still going.");

            Assert.That(charged, Is.EqualTo(quoted),
                "The button said one figure and the company was charged another.");

            Assert.That(simulation.State.CashUsd, Is.EqualTo(cashBefore - charged),
                "Abandoning the run cost nothing, so a two hundred day mistake is free.");
        }

        /// <summary>
        /// **The calendar is not rewound and must not be.** What a cancel buys is not spending the
        /// remaining days on a plan the player no longer wants.
        /// </summary>
        [Test]
        public void AbandoningARunHandsBackNoTime()
        {
            var simulation = Running(25.0);
            Assume.That(simulation.TryStartTraining(Plan(), out _), Is.True);

            var startedOn = simulation.State.Date.DayIndex;

            for (var day = 0; day < 25; day++)
            {
                simulation.AdvanceDay();
            }

            simulation.TryCancelTraining(out _, out _);

            Assert.That(simulation.State.Date.DayIndex, Is.EqualTo(startedOn + 25),
                "Cancelling moved the calendar, which would let a player buy their way out of the "
                + "one thing this game charges for.");
        }

        [Test]
        public void NothingRunningIsRefusedWithAReasonRatherThanCharged()
        {
            var simulation = Running();
            var cashBefore = simulation.State.CashUsd;

            Assert.That(simulation.TryCancelTraining(out var fee, out var why), Is.False);
            Assert.That(why, Is.Not.Empty, "It refused and said nothing.");
            Assert.That(fee, Is.Zero, "It charged for stopping something that was not running.");

            Assert.That(simulation.TryCancelArchitecture(out _, out var archWhy), Is.False);
            Assert.That(archWhy, Is.Not.Empty);

            Assert.That(simulation.TryCancelUpgrade(0, out _, out var upWhy), Is.False);
            Assert.That(upWhy, Is.Not.Empty);

            Assert.That(simulation.State.CashUsd, Is.EqualTo(cashBefore));
        }

        /// <summary>The fee is a tenth, and never a fraction of a cent.</summary>
        [Test]
        public void TheFeeIsATenthOfWhatWasSpent()
        {
            Assert.That(CancellationPolicy.FeeOn(10_000_000L), Is.EqualTo(1_000_000L));
            Assert.That(CancellationPolicy.FeeOn(0L), Is.Zero);
            Assert.That(CancellationPolicy.FeeOn(-5L), Is.Zero, "A negative bill earned a refund.");
        }

        /// <summary>
        /// **A v53 campaign banked nothing and cannot be given any**, because the old rule threw the
        /// whole node away and left no record it had been started.
        /// </summary>
        [Test]
        public void AnOlderSaveBanksNothingBecauseNothingWasEverKept()
        {
            var upgraded = SaveMigration.UpgradeV53ToV54(new SaveData { version = 53 });

            Assert.That(upgraded.version, Is.EqualTo(54));
            Assert.That(upgraded.bankedResearchNodes, Is.Empty);
            Assert.That(upgraded.bankedResearchDays, Is.Empty);
            Assert.That(upgraded.bankedResearchPetaflopDays, Is.Empty);
        }
    }
}
