using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using ScalingLaws.Core;
using ScalingLaws.Data;
using ScalingLaws.Simulation;

namespace ScalingLaws.Tests.EditMode
{
    /// <summary>
    /// Does the basement actually take work off the rented fleet, with several kinds of card in it.
    ///
    /// **Asked on 2026-10-24: "czy serwerownia faktycznie działa naprawdę, 3-5 rodzajów części".**
    /// `ServerRoomChainTests` already walks shop to cabinet to capacity, and it is a good test, but
    /// it fills every slot with one generation and it stops at capacity. Two things it therefore
    /// cannot answer: whether a room holding a mixture behaves, and whether any of it shows up as
    /// the company needing less of the cluster it rents.
    ///
    /// Utilisation is the figure that answers the second one, because it is demand over capacity
    /// and it is what `ServiceQuality` turns into response time and reliability. A room that raises
    /// petaflops and leaves utilisation where it was has added an ornament.
    /// </summary>
    public sealed class RoomOffloadTests
    {
        /// <summary>
        /// Late enough that several generations of accelerator are on sale at once.
        ///
        /// The question is about a mixture, and a mixture needs a date with a market behind it:
        /// in 2023 there are too few to be meaningful.
        /// </summary>
        private static GameDate Day => GameDate.FromCalendar(2027, 1, 1);

        /// <summary>
        /// The cluster both companies rent, sized from the measurement rather than guessed.
        ///
        /// Capacity comes out at 0.177 of petaflops here, so 400 leaves the company at about 0.84
        /// of its fleet against a demand of 59.5, and the basement should take it to about 0.75.
        ///
        /// **60.0 was the first value in this slot and it measured nothing.** It put the company
        /// five times oversubscribed, where utilisation is clamped at 1.000 and response time sits
        /// on its ceiling, so the room added 78% more compute and every figure a player reads
        /// stayed where it was. A fleet of hundreds would drown the room instead. Both ends of that
        /// range are green bars that say nothing.
        /// </summary>
        private const double RentedPetaflops = 400.0;

        private static CompanySimulation Company(long cash = 4_000_000_000)
        {
            var state = new CompanyState("Prometheus AI", 4242)
            {
                Date = Day,
                CashUsd = cash
            };

            // **Capability near the frontier of its own year, not 20.** A 2022-grade model in
            // 2027 is bought by almost nobody, so a fixture built on one measures a company with
            // no customers, and every capacity figure in it is noise against a demand of nearly
            // zero. The first version of this file did exactly that.
            state.AddDeployedModel(new DeployedModel(
                "Aurora 1", ArchitectureId.DenseTransformer, 72, Day.AddDays(-120), 1e12, 1.0));

            // AddDeployedModel only adds to the list; LastReleaseDate is set by the release path and nothing else. Without this a fixture runs a company the game reads as having shipped nothing since day zero, which is -0.0016 a day of staleness for ever.
            state.LastReleaseDate = Day.AddDays(-120);

            return new CompanySimulation(state);
        }

        /// <summary>The newest few accelerators a company could buy on that day, newest first.</summary>
        private static List<HardwareGeneration> Mixture(int many)
            => HardwareCatalog.AvailableOn(Day, HardwareClass.Accelerator)
                .OrderByDescending(generation => generation.ReleaseDate.DayIndex)
                .Take(many)
                .ToList();

        [Test]
        public void SeveralGenerationsStandInOneRoomAndEveryOneOfThemIsHoused()
        {
            var simulation = Company();

            Assert.That(simulation.TryOpenServerRoom(true, out var why), Is.True, why);

            var kinds = Mixture(5);

            Assert.That(kinds.Count, Is.GreaterThanOrEqualTo(3),
                "This fixture is about a mixture and needs at least three kinds on sale to be "
                + "about anything. If the catalogue release dates move, move the date above.");

            var slots = simulation.State.Hall.TotalSlots;
            var each = slots / kinds.Count;

            Assert.That(each, Is.GreaterThan(0), "The gift room has fewer slots than kinds.");

            foreach (var generation in kinds)
            {
                Assert.That(
                    simulation.TryBuyHardware(generation.Id, each,
                        ComputeTier.ColocatedServers, out var refused),
                    Is.True, generation.Id + ": " + refused);
            }

            // Every order waits its own lead time, so the longest one governs.
            simulation.Advance(ComputeTierCatalog.Get(ComputeTier.ColocatedServers).LeadTimeDays + 2);

            RoomHands.FitEverything(simulation);
            simulation.Advance(1);

            Assert.That(simulation.State.Hall.HousedAccelerators,
                Is.EqualTo(each * kinds.Count),
                "Cards were bought for the room and some of them are not standing in it.");

            var standing = new HashSet<HardwareGenerationId>();

            for (var column = 0; column < CompanyState.BasementColumns; column++)
            {
                for (var row = 0; row < CompanyState.BasementColumns; row++)
                {
                    foreach (var (generation, count) in
                             simulation.State.Hall.CardsIn(column, row, out _))
                    {
                        if (count > 0)
                        {
                            standing.Add(generation);
                        }
                    }
                }
            }

            Assert.That(standing.Count, Is.EqualTo(kinds.Count),
                "The room reports fewer kinds than were put in it, so the cabinets are losing "
                + "what generation a card is. Found: "
                + string.Join(", ", standing.Select(id => id.ToString())));
        }

        /// <summary>
        /// The half the question was really about: does any of it reach the rented cluster.
        ///
        /// Two identical companies, one with a filled basement, both tipped the same day. The room
        /// has to show up as capacity the customers meet and as a load that is lower for it.
        /// </summary>
        [Test]
        public void AFilledRoomTakesLoadOffTheFleetRatherThanOnlyRaisingPetaflops()
        {
            var renting = Company();
            var owning = Company();

            // **Both rent the same cluster, and that is the repair.** The first version gave the
            // control no compute at all, so it compared a room against nothing: capacity 0.0
            // against 690.0, load pinned at 1.000, and a green bar that proved only that
            // something beats nothing. The claim in the name of this test is about a company
            // that already has a fleet, so the control has to have one.
            renting.SetRentedPetaflops(RentedPetaflops);
            owning.SetRentedPetaflops(RentedPetaflops);

            Assert.That(owning.TryOpenServerRoom(true, out var why), Is.True, why);

            var kinds = Mixture(5);
            var each = owning.State.Hall.TotalSlots / kinds.Count;

            foreach (var generation in kinds)
            {
                Assert.That(
                    owning.TryBuyHardware(generation.Id, each,
                        ComputeTier.ColocatedServers, out var refused),
                    Is.True, refused);
            }

            var wait = ComputeTierCatalog.Get(ComputeTier.ColocatedServers).LeadTimeDays + 2;

            renting.Advance(wait);
            owning.Advance(wait);

            RoomHands.FitEverything(owning);

            renting.Advance(1);
            owning.Advance(1);

            // **Printed, not only asserted.** The question this fixture answers was asked out
            // loud, and "green" is a worse answer than four numbers somebody can read.
            UnityEngine.Debug.Log(
                "ROOM kinds=" + string.Join("/", kinds.Select(g => g.Id.ToString()))
                + " each=" + each
                + "\nROOM petaflops rented=" + renting.Profile.RawPetaflops.ToString("0.0")
                + " withRoom=" + owning.Profile.RawPetaflops.ToString("0.0")
                + "\nROOM capacity rented=" + renting.State.LastQuality.Capacity.ToString("0.0")
                + " withRoom=" + owning.State.LastQuality.Capacity.ToString("0.0")
                + "\nROOM load rented=" + renting.State.LastQuality.Utilisation.ToString("0.000")
                + " withRoom=" + owning.State.LastQuality.Utilisation.ToString("0.000")
                + "\nROOM demanded rented=" + renting.State.LastQuality.Demanded.ToString("0.0")
                + " withRoom=" + owning.State.LastQuality.Demanded.ToString("0.0")
                + "\nROOM ms rented="
                + renting.State.LastQuality.ResponseMilliseconds.ToString("0.0")
                + " withRoom=" + owning.State.LastQuality.ResponseMilliseconds.ToString("0.0"));

            Assert.That(owning.Profile.RawPetaflops,
                Is.GreaterThan(renting.Profile.RawPetaflops),
                "A room full of silicon that is not compute is an ornament.");

            Assert.That(owning.State.LastQuality.Capacity,
                Is.GreaterThan(renting.State.LastQuality.Capacity),
                "And compute has to be capacity the market reads, not a number on one screen.");

            // **This is the assertion the question was about.** Utilisation is demand over
            // capacity, so the same demand served by more capacity is a cluster working less
            // hard, which is what "it takes the load off" has to mean before it means anything.
            Assert.That(owning.State.LastQuality.Utilisation,
                Is.LessThan(renting.State.LastQuality.Utilisation),
                "Both companies owe the same customers the same work. The one with the room is "
                + "not running any cooler for it, so the basement is not relieving the fleet, "
                + "whatever the petaflop figure says. Demanded: "
                + owning.State.LastQuality.Demanded.ToString("0.##") + " against "
                + renting.State.LastQuality.Demanded.ToString("0.##"));
        }
    }
}
