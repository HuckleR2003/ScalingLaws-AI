using System.Linq;
using NUnit.Framework;
using ScalingLaws.Core;
using ScalingLaws.Data;
using ScalingLaws.Simulation;

namespace ScalingLaws.Tests.EditMode
{
    /// <summary>
    /// Who an office has to have room for.
    ///
    /// **Reported on 2026-10-18: "I cant buy an office if I have more remote people than the
    /// office supports."** The gate on both moving and buying read `Headcount`, which is
    /// everybody on the payroll, so a company with a handful of people on site and a crowd
    /// working remotely was refused a place it had plenty of desks for.
    ///
    /// `StaffRoster` has had the right reading since remote hiring was built: `SeatedHeadcount`
    /// is what `HasFreeSeat` compares against, and `Add` already refuses a seated hire with no
    /// free desk on exactly that basis. So this was two readings of one fact with one of them
    /// disagreeing, which is a shape this project has hit before with heat thresholds and with
    /// the audience split.
    /// </summary>
    public sealed class OfficeSeatingTests
    {
        private static CompanySimulation Company(long cash = 200_000_000)
        {
            var simulation = new CompanySimulation(new CompanyState("Seating", 12));
            simulation.State.CashUsd = cash;

            // The premises ladder is gated on its own research and this fixture is not about
            // that gate, the same four lines `LearnedToRent` exists for on the PlayMode side.
            foreach (var node in ResearchTree.All)
            {
                if (node.Track == ResearchTrack.Premises)
                {
                    simulation.State.UnlockedResearch.Add(node.Id);
                }
            }

            return simulation;
        }

        private static void AddRemote(CompanySimulation simulation, int many)
        {
            for (var index = 0; index < many; index++)
            {
                simulation.State.Staff.Add(new Hire(
                    StaffRole.InfrastructureEngineer, 1, GameDate.Start,
                    $"Remote {index}", PlayerSkill.Software, HireSource.Remote, 40.0));
            }
        }

        private static void AddSeated(CompanySimulation simulation, int many)
        {
            for (var index = 0; index < many; index++)
            {
                simulation.State.Staff.Add(new Hire(StaffRole.ResearchScientist, 2, GameDate.Start));
            }
        }

        /// <summary>
        /// The first place on the ladder the company does not already sit in.
        ///
        /// `OfficeDefinition` is a struct, so there is no null to fall back on and the query
        /// throws instead if the ladder is ever empty, which is the louder of the two failures.
        /// </summary>
        private static OfficeDefinition SomewhereToMoveTo(CompanySimulation simulation) =>
            OfficeCatalog.Places()
                .First(place => place.Tier != simulation.State.Staff.Office && place.Desks > 0);

        /// <summary>The first place on the ladder that can be bought outright.</summary>
        private static OfficeDefinition SomewhereToBuy(CompanySimulation simulation) =>
            OfficeCatalog.Places()
                .First(place => place.CanBeBought
                                && place.Tier != simulation.State.Staff.Office
                                && place.Desks > 0);

        [Test]
        public void ACrowdOfRemoteHiresDoesNotBlockAMove()
        {
            var simulation = Company();
            var target = SomewhereToMoveTo(simulation);

            // Comfortably inside the lease on seats, and well past it on heads.
            AddSeated(simulation, 1);
            AddRemote(simulation, target.Desks + 8);

            Assert.That(simulation.State.Staff.Headcount, Is.GreaterThan(target.Desks),
                "This fixture is only meaningful when the head count exceeds the desks.");

            Assert.That(
                simulation.TryMoveOffice(target.Tier, out var why), Is.True,
                "A remote hire is not owed a desk, so a crowd of them cannot make an office too "
                + "small. The gate reads SeatedHeadcount, which is what HasFreeSeat and Add have "
                + "always used. Refusal was: " + why);
        }

        [Test]
        public void ACrowdOfRemoteHiresDoesNotBlockAPurchase()
        {
            var simulation = Company();
            var target = SomewhereToBuy(simulation);

            AddSeated(simulation, 1);
            AddRemote(simulation, target.Desks + 8);

            Assert.That(
                simulation.TryBuyOffice(target.Tier, null, out var why), Is.True,
                "Buying reads the same count as moving, and a remote hire is not owed a desk in "
                + "either. Refusal was: " + why);
        }

        /// <summary>
        /// The other half, and the one that keeps the gate a gate.
        ///
        /// Deleting the check would pass the two tests above and quietly let a company of forty
        /// people on site move into ten desks, so this holds the refusal from the other side.
        /// </summary>
        [Test]
        public void PeopleWhoActuallySitSomewhereStillHaveToFit()
        {
            var simulation = Company();

            // **The team has to be hired somewhere it fits before it can fail to fit anywhere
            // else.** `StaffRoster.Add` refuses a seated hire with no free desk, so asking for
            // thirteen people while the company is still in the garage quietly produces two,
            // and the move that follows then succeeds for the wrong reason. The first version
            // of this test did exactly that and passed against the broken gate.
            var roomy = Roomiest();
            Assert.That(simulation.TryMoveOffice(roomy.Tier, out var intoRoomy), Is.True, intoRoomy);

            var small = Smallest();
            Assert.That(roomy.Desks, Is.GreaterThan(small.Desks),
                "This fixture needs two places of different sizes to be meaningful.");

            AddSeated(simulation, small.Desks + 2);

            Assert.That(simulation.State.Staff.SeatedHeadcount, Is.GreaterThan(small.Desks),
                "Everybody asked for has to have actually been hired.");

            Assert.That(
                simulation.TryMoveOffice(small.Tier, out var why), Is.False,
                "An office with fewer desks than the people who need one is still too small.");

            Assert.That(why, Is.Not.Empty, "A refusal that says nothing reads as a bug.");
        }

        /// <summary>The place on the ladder with the most desks.</summary>
        private static OfficeDefinition Roomiest() =>
            OfficeCatalog.Places().OrderByDescending(place => place.Desks).First();

        /// <summary>The smallest place that still has desks in it.</summary>
        private static OfficeDefinition Smallest() =>
            OfficeCatalog.Places().Where(place => place.Desks > 0)
                .OrderBy(place => place.Desks).First();
    }
}
