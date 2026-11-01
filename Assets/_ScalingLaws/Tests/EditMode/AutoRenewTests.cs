using System.Linq;
using NUnit.Framework;
using ScalingLaws.Data;
using ScalingLaws.Persistence;
using ScalingLaws.Simulation;

namespace ScalingLaws.Tests.EditMode
{
    /// <summary>
    /// Terms that roll over on their own, and the two things that must stay true about them.
    ///
    /// **Asked for by the tester, and he chose the shape.** Offered two versions, one that renews
    /// on the same terms and can be switched off and one that renews and charges whatever the
    /// balance is, he picked the first and gave the better argument for it: the cash cost of one of
    /// these arrangements is the fee for setting it up, an advance, and what the other lab is in it
    /// for comes afterwards. A company that has paid that once is continuing something rather than
    /// buying it again.
    ///
    /// So the two rules are: **it can never charge what the company does not have**, and **it is
    /// never silent**, in either direction.
    /// </summary>
    public sealed class AutoRenewTests
    {
        private const CompetitorId Lab = CompetitorId.Cohere;

        private static CompanySimulation Company(uint seed)
        {
            var simulation = new CompanySimulation(new CompanyState("Adco", seed));
            simulation.State.CashUsd = 200_000_000;
            simulation.State.ResearchPoints = 5_000;
            simulation.SetRentedPetaflops(200.0);

            simulation.State.Relations.Record(Lab, simulation.State.Date,
                RivalRelations.Best - simulation.State.Relations.With(Lab),
                "relation.reason.published");

            return simulation;
        }

        /// <summary>
        /// Ends a term two days out and runs past it, which is the only way a renewal is raised.
        /// </summary>
        private static void RunPastTheEndOfATerm(CompanySimulation simulation)
        {
            simulation.State.Deals.Add(new StandingDeal(Lab, RelationOffer.CapacityPurchase,
                simulation.State.Date, simulation.State.Date.AddDays(2)));

            for (var day = 0; day < 4; day++)
            {
                simulation.AdvanceDay();
            }
        }

        private static bool HasALiveDealWith(CompanySimulation simulation) =>
            simulation.State.Deals.Any(deal =>
                deal.Lab == Lab && deal.IsLiveOn(simulation.State.Date));

        /// <summary>
        /// A seed on which they actually ring, because they only do about half the time.
        ///
        /// **Found rather than assumed.** Picking a number and hoping is how a fixture ends up
        /// passing because nothing happened at all.
        /// </summary>
        private static uint SeedWhereTheyRing()
        {
            for (var seed = 1u; seed < 200u; seed++)
            {
                var simulation = Company(seed);
                RunPastTheEndOfATerm(simulation);

                if (simulation.RenewalIsOnTheTable)
                {
                    return seed;
                }
            }

            Assert.Fail("No seed in two hundred produced a renewal, so these tests measure nothing.");
            return 0u;
        }

        [Test]
        public void ATermRollsOverOnItsOwnWhenTheCompanyHasAskedForThat()
        {
            var seed = SeedWhereTheyRing();

            // The same seed twice, so the only difference between the two is the switch.
            var off = Company(seed);
            RunPastTheEndOfATerm(off);

            var on = Company(seed);
            on.SetAutoRenew(Lab, true);

            var before = on.State.CashUsd;
            RunPastTheEndOfATerm(on);

            Assert.IsTrue(off.RenewalIsOnTheTable,
                "With the switch off they should be waiting for an answer.");

            Assert.IsFalse(off.AutoRenewsWith(Lab), "The switch defaults to off.");

            Assert.IsFalse(on.RenewalIsOnTheTable,
                "With the switch on there is nothing left to answer: it was answered.");

            Assert.IsTrue(HasALiveDealWith(on),
                "The term rolled over and no term came out of it, so the switch charged the "
                + "company for nothing. That is the failure this is really guarding against.");

            Assert.That(on.State.CashUsd, Is.LessThan(before),
                "Rolling over on the same terms costs the same money. It is not a free renewal.");
        }

        /// <summary>
        /// The rule the tester chose this version for: it cannot hurt you while you are not looking.
        /// </summary>
        [Test]
        public void ARollOverNeverChargesWhatTheCompanyDoesNotHave()
        {
            var seed = SeedWhereTheyRing();

            // **Against the same days with the switch off, not against the opening balance.** The
            // company is billed for its fleet and its staff every one of these four days whatever
            // happens with the renewal, so comparing to what it started with measures the daily
            // burn and calls it a roll-over. The first version of this test did exactly that.
            var quiet = Company(seed);
            quiet.State.CashUsd = 1_000;
            RunPastTheEndOfATerm(quiet);

            var simulation = Company(seed);
            simulation.SetAutoRenew(Lab, true);
            simulation.State.CashUsd = 1_000;

            RunPastTheEndOfATerm(simulation);

            Assert.IsFalse(HasALiveDealWith(simulation),
                "A term was signed that the company could not pay for.");

            Assert.That(simulation.State.CashUsd, Is.EqualTo(quiet.State.CashUsd),
                "The four days cost a different amount with the switch on, so the roll-over took "
                + "money for a term it did not sign.");

            Assert.IsTrue(simulation.RenewalIsOnTheTable,
                "The offer has to fall back to the table rather than vanish. Otherwise a short "
                + "week silently costs the player an arrangement they would have paid for by hand.");
        }

        [Test]
        public void TurningTheSwitchOffStopsIt()
        {
            var seed = SeedWhereTheyRing();

            var simulation = Company(seed);
            simulation.SetAutoRenew(Lab, true);
            simulation.SetAutoRenew(Lab, false);

            Assert.IsFalse(simulation.AutoRenewsWith(Lab));

            RunPastTheEndOfATerm(simulation);

            Assert.IsTrue(simulation.RenewalIsOnTheTable,
                "Switched off and it still answered for the player.");
        }

        /// <summary>
        /// It belongs to one lab, not to the company.
        /// </summary>
        [Test]
        public void TheSwitchIsPerLab()
        {
            var simulation = Company(31);
            simulation.SetAutoRenew(Lab, true);

            Assert.IsTrue(simulation.AutoRenewsWith(Lab));
            Assert.IsFalse(simulation.AutoRenewsWith(CompetitorId.OpenAi),
                "Saying yes to one lab said yes to all of them.");
        }

        [Test]
        public void TheSwitchSurvivesASaveAndALoad()
        {
            var simulation = Company(31);
            simulation.SetAutoRenew(Lab, true);
            simulation.SetAutoRenew(CompetitorId.OpenAi, true);

            var back = SaveStore.Restore(SaveStore.Capture(simulation.State));

            Assert.IsTrue(back.AutoRenewals.Contains(Lab));
            Assert.IsTrue(back.AutoRenewals.Contains(CompetitorId.OpenAi));
            Assert.That(back.AutoRenewals.Count, Is.EqualTo(2),
                "Something else came back switched on that nobody switched on.");
        }

        /// <summary>
        /// v76 to v77. A campaign played before the switch existed was never asked the question.
        /// </summary>
        [Test]
        public void AnOlderFileRollsNothingOverByItself()
        {
            var data = SaveStore.Capture(Company(31).State);
            data.version = 76;
            data.autoRenewLabs = null;

            var upgraded = SaveMigration.UpgradeV76ToV77(data);

            Assert.That(upgraded.version, Is.EqualTo(77),
                "Each step stamps its own number, never whatever happens to be newest today.");

            Assert.That(upgraded.autoRenewLabs, Is.Empty,
                "A v76 campaign never chose this, and turning it on for a lab the player never "
                + "agreed to would answer on their behalf and then charge them for the answer.");
        }
    }
}
