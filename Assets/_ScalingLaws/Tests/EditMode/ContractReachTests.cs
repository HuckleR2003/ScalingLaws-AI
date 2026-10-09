using NUnit.Framework;
using ScalingLaws.Data;
using ScalingLaws.Simulation;

namespace ScalingLaws.Tests.EditMode
{
    /// <summary>
    /// A contract waiting for the player's signature is never put there in silence.
    ///
    /// **Reported on 2026-10-01: a distribution licence agreed with a lab and no terms anywhere.**
    /// The two-step licence was built as asked, the card that reads out the commission and the
    /// audience was written, the estimate behind it was written and tested, and the player saw
    /// none of it. The slot that holds "they have agreed and are waiting for you" has two fillers,
    /// a first signing and a renewal, and only the renewal raised the event the shell listens for.
    /// So the first signing filled it without knocking.
    ///
    /// **This is the invariant rather than the symptom.** A test that checked the licence alone
    /// would pass the day a third filler is written, and the whole point is that there are two
    /// already and one of them forgot.
    /// </summary>
    public sealed class ContractReachTests
    {
        private static CompanySimulation Company()
        {
            var simulation = new CompanySimulation(new CompanyState("Prometheus AI"));
            simulation.State.CashUsd = 2_000_000_000L;
            simulation.State.ResearchPoints = 50_000.0;
            simulation.SetRentedPetaflops(400.0);

            // A licence needs something to licence. Without a live model the send is refused and
            // the loop below measures nothing, which is how the first run of this failed.
            var flagship = new DeployedModel("Ardent 1", ArchitectureId.DenseTransformer, 44.0,
                simulation.State.Date, 2e10, 1.0, ModelType.General, "Ardent");

            simulation.State.AddDeployedModel(flagship);
            flagship.SeedLine(MonetizationPolicy.OpeningSubscriptionUsdPerMonth, 0.0);

            return simulation;
        }

        /// <summary>
        /// Sends licences until one is agreed to, then asks whether the player was told.
        ///
        /// Driven through the real send and the real clock rather than by writing the slot by
        /// hand: writing it by hand would test the card and not the path, and the path is where
        /// the fault was.
        /// </summary>
        [Test]
        public void AContractWaitingForASignatureAlwaysAnnouncesItself()
        {
            var simulation = Company();
            var state = simulation.State;

            // Warm enough that a licence is agreed to inside a reasonable number of attempts.
            for (var warm = 0; warm < 40; warm++)
            {
                state.Relations.Record(CompetitorId.Cohere, state.Date, 6.0, "test", "Gohere");
            }

            var told = false;
            var waiting = false;

            for (var attempt = 0; attempt < 60 && !waiting; attempt++)
            {
                if (!simulation.TrySendOffer(
                        CompetitorId.Cohere, RelationOffer.DistributionLicence, out _))
                {
                    break;
                }

                var days = RelationOfferCatalog
                    .Get(RelationOffer.DistributionLicence).DaysToAnswer + 1;

                for (var day = 0; day < days; day++)
                {
                    simulation.AdvanceDay();

                    while (state.TryDequeueEvent(out var raised))
                    {
                        if (raised.Type == CompanyEventType.RenewalOffered)
                        {
                            told = true;
                        }
                    }

                    if (state.Renewals.Count > 0)
                    {
                        waiting = true;

                        break;
                    }
                }
            }

            Assert.IsTrue(waiting,
                "No licence was ever agreed to in sixty attempts, so this measured nothing. The "
                + "acceptance odds or the warming above have moved.");

            Assert.IsTrue(told,
                "A contract is sitting in the save waiting to be signed and nothing was raised to "
                + "say so. The card that reads out the commission, the audience and what is left "
                + "for the company is opened by that event and by nothing else, so the player has "
                + "bought something they cannot read.");
        }

        /// <summary>
        /// The other filler, held to the same rule, so neither can be the one that forgets.
        /// </summary>
        [Test]
        public void ARenewalIsAnnouncedTheSameWay()
        {
            var source = System.IO.File.ReadAllText(System.IO.Path.Combine(
                UnityEngine.Application.dataPath, "_ScalingLaws", "Scripts", "Simulation",
                "CompanySimulation.Alliances.cs"));

            var fills = System.Text.RegularExpressions.Regex.Matches(
                source, @"State\.Renewals\.Add\(new PendingRenewal").Count;

            var knocks = System.Text.RegularExpressions.Regex.Matches(
                source, @"CompanyEventType\.RenewalOffered").Count;

            Assert.That(fills, Is.GreaterThan(0), "Nothing fills the pending contract slot at all.");

            Assert.That(knocks, Is.GreaterThanOrEqualTo(fills),
                $"{fills} places put a contract in front of the player and {knocks} of them say "
                + "so. The slot is the only thing the card reads, so a filler that does not raise "
                + "the event is a contract nobody can open.");
        }
    }
}
