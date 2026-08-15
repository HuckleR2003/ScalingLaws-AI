using System.Collections.Generic;
using NUnit.Framework;
using ScalingLaws.Data;
using ScalingLaws.Simulation;

namespace ScalingLaws.Tests.EditMode
{
    /// <summary>
    /// A finished node hands over what its own card promised.
    ///
    /// **This is the sixth time this project has shipped a mechanism nothing could reach**, and the
    /// worst one so far. The research card prints "CORPUS: Curated corpora" and "ARCHITECTURE:
    /// Mixture of experts" straight off the node. The player pays the points, the cash and four
    /// months of calendar, and until 2026-08-15 received neither: the only code that granted a
    /// corpus was `TryAcquireDataSource` and the only code that adopted a family was
    /// `TryAdoptArchitecture`, and nothing in the interface ever called either one.
    ///
    /// **A campaign was locked to the web crawl and a dense transformer from the first day to the
    /// last**, and the whole suite passed, because the scripted operator in `PlayabilityTests` calls
    /// those two methods directly and therefore never noticed they were unreachable.
    ///
    /// So this fixture does not test the methods. It finishes a node the way the game does and reads
    /// what the company owns afterwards.
    /// </summary>
    public sealed class ResearchDeliversTests
    {
        private static CompanySimulation Company()
        {
            var simulation = new CompanySimulation(new CompanyState("Prometheus AI"));
            simulation.SetRentedAccelerators(4000);
            return simulation;
        }

        /// <summary>Runs a node to completion the way the day loop does, prerequisites included.</summary>
        private static void Finish(CompanySimulation simulation, ResearchNodeId id)
        {
            var node = ResearchTree.Get(id);

            foreach (var required in node.Prerequisites)
            {
                simulation.State.UnlockedResearch.Add(required);
            }

            // A node cannot be started before its own earliest date, and the campaign opens in
            // January 2022 while most of the tree opens later. Wind the calendar forward first.
            for (var day = 0; day < 3000 && !simulation.State.Date.IsOnOrAfter(node.EarliestDate); day++)
            {
                simulation.State.CashUsd = 5_000_000_000;
                simulation.Advance(1);
            }

            // Points and cash are not what this fixture is about, and a node that cannot be afforded
            // never completes, so the company is given enough of both to start.
            simulation.State.CashUsd = 5_000_000_000;
            simulation.State.ResearchPoints = 5_000_000;

            Assert.IsTrue(simulation.TryStartResearch(id, out var reason), reason);

            for (var day = 0; day < 2000 && simulation.State.ActiveResearch != null; day++)
            {
                simulation.Advance(1);
            }

            Assert.IsNull(simulation.State.ActiveResearch, $"{id} never finished.");
            Assert.IsTrue(simulation.State.HasResearch(id));
        }

        [Test]
        public void EveryNodeThatPromisesACorpusActuallyHandsItOver()
        {
            var broken = new List<string>();

            foreach (var node in ResearchTree.All)
            {
                if (node.UnlocksData == DatasetSource.None)
                {
                    continue;
                }

                var simulation = Company();
                Finish(simulation, node.Id);

                var owned = simulation.State.OwnedDataSources;
                if ((owned & node.UnlocksData) != node.UnlocksData)
                {
                    broken.Add($"{node.DisplayName} promises {node.UnlocksData} and delivered nothing");
                }
            }

            CollectionAssert.IsEmpty(broken, string.Join("\n", broken));
        }

        [Test]
        public void EveryNodeThatPromisesAnArchitectureActuallyHandsItOver()
        {
            var broken = new List<string>();

            foreach (var node in ResearchTree.All)
            {
                if (node.UnlocksArchitecture == ArchitectureId.None)
                {
                    continue;
                }

                var simulation = Company();
                Finish(simulation, node.Id);

                if (!simulation.State.HasArchitecture(node.UnlocksArchitecture))
                {
                    broken.Add($"{node.DisplayName} promises {node.UnlocksArchitecture} "
                        + "and the company still cannot build with it");
                }
            }

            CollectionAssert.IsEmpty(broken, string.Join("\n", broken));
        }

        [Test]
        public void ACompanyStartsWithOneCorpusAndOneFamily()
        {
            // The premise the two tests above rest on. If a new company already owned everything,
            // they would pass on a game that granted nothing.
            var simulation = Company();

            Assert.AreEqual(DatasetCatalog.StartingSources, simulation.State.OwnedDataSources);
            Assert.AreEqual(1, simulation.State.AdoptedArchitectures.Count);
        }

        [Test]
        public void TheGrantIsFreeBecauseTheNodeWasAlreadyPaidFor()
        {
            // Granted directly rather than through TryAcquireDataSource, which charges. The node
            // costs points, and points cannot be bought outright: charging again at the end would
            // mean a player who researched a corpus and then could not afford it.
            var simulation = Company();

            ResearchNode found = default;
            var any = false;
            foreach (var node in ResearchTree.All)
            {
                if (node.UnlocksData != DatasetSource.None)
                {
                    found = node;
                    any = true;
                    break;
                }
            }

            Assert.IsTrue(any, "No node unlocks a corpus, so this test is checking nothing.");

            Finish(simulation, found.Id);
            var before = simulation.State.CashUsd;
            simulation.Advance(1);

            Assert.Greater(simulation.State.CashUsd, before - 500_000_000,
                "Finishing the node must not send a second invoice for what it unlocked.");
        }

        [Test]
        public void TheCompletionEventSaysWhatWasOpened()
        {
            // The player is not watching the corpus list when a four month programme lands. If the
            // event does not say what arrived, the unlock is invisible even when it works.
            var simulation = Company();

            ResearchNode found = default;
            foreach (var node in ResearchTree.All)
            {
                if (node.UnlocksData != DatasetSource.None)
                {
                    found = node;
                    break;
                }
            }

            Finish(simulation, found.Id);

            var said = false;
            while (simulation.State.TryDequeueEvent(out var raised))
            {
                if (raised.Type == CompanyEventType.ResearchCompleted && raised.Message.Contains("Opens"))
                {
                    said = true;
                }
            }

            Assert.IsTrue(said, "The completion event never mentioned what the node opened.");
        }
    }
}
