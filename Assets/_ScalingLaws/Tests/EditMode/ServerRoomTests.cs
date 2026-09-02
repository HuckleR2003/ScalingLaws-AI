using System.Linq;
using NUnit.Framework;
using ScalingLaws.Data;
using ScalingLaws.Simulation;

namespace ScalingLaws.Tests.EditMode
{
    /// <summary>
    /// The basement, end to end: gift, purchase, cabinets, fans, heat and the fleet.
    ///
    /// **`ServerHall` was written months before this and connected to nothing**, which made it the
    /// seventh complete mechanism in this project with no way in. A green `ServerHallTests` proved
    /// the grid worked and proved nothing about whether a player could ever stand on it. This
    /// fixture is the other half: it starts from a company with no room and ends at petaflops in the
    /// fleet, through the same calls the interface makes.
    /// </summary>
    public sealed class ServerRoomTests
    {
        private static CompanySimulation Company(long cash = 5_000_000)
        {
            var simulation = new CompanySimulation(new CompanyState("Prometheus AI", 1234));
            simulation.State.CashUsd = cash;
            return simulation;
        }

        [Test]
        public void ACompanyStartsWithNowhereToPutARack()
        {
            var simulation = Company();

            Assert.IsFalse(simulation.State.HasServerRoom);
            Assert.That(simulation.State.Hall.RackCount, Is.Zero,
                "A floor nobody owns cannot have cabinets standing on it.");
        }

        [Test]
        public void TheGiftOpensTheRoomAndCostsNothing()
        {
            var simulation = Company();
            var before = simulation.State.CashUsd;

            Assert.IsTrue(simulation.TryOpenServerRoom(true, out var why), why);

            Assert.IsTrue(simulation.State.HasServerRoom);
            Assert.IsTrue(simulation.State.ServerRoomWasAGift);
            Assert.That(simulation.State.CashUsd, Is.EqualTo(before), "A gift is a gift.");

            Assert.That(simulation.State.Hall.RackCount,
                Is.EqualTo(CompanySimulation.BasementRacks),
                "Four cabinets, which is what he says are down there.");
        }

        /// <summary>
        /// A company that could actually use a room: one product shipped and the money for the
        /// hardware that would stand in it.
        ///
        /// Added when buying the room was gated on being able to own accelerators. The gate is the
        /// point of the rule, so the fixture meets it rather than the rule being loosened to suit
        /// a fixture that was written before it existed.
        /// </summary>
        private static CompanySimulation CompanyReadyToOwnHardware()
        {
            var simulation = Company(cash: 30_000_000);
            var state = simulation.State;

            state.AddDeployedModel(new DeployedModel(
                "Shipped", ArchitectureId.DenseTransformer, 12.0, state.Date, 20.0, 1.0));

            return simulation;
        }

        [Test]
        public void BuyingItCostsTheAskingPriceAndGivesTheSameRoom()
        {
            var simulation = CompanyReadyToOwnHardware();
            var before = simulation.State.CashUsd;

            Assert.IsTrue(simulation.TryOpenServerRoom(false, out var why), why);

            Assert.That(simulation.State.CashUsd,
                Is.EqualTo(before - CompanySimulation.BasementPriceUsd));

            // **The same room either way.** A gift and a purchase differ in exactly one thing,
            // whether money moves, and two openings would be two places to get the rest wrong.
            Assert.That(simulation.State.Hall.RackCount,
                Is.EqualTo(CompanySimulation.BasementRacks));
            Assert.IsFalse(simulation.State.ServerRoomWasAGift);
        }

        /// <summary>
        /// The room cannot be bought before there is anything to put in it.
        ///
        /// **Measured, not imagined.** The campaign probe's frugal style opened a basement, stood
        /// twelve cabinets in it and bought not one accelerator in fourteen years, because owning
        /// hardware needs a released model and five million dollars. Seventy thousand for a room
        /// that can never hold anything is a trap, and a new player is exactly who walks into it.
        /// </summary>
        [Test]
        public void ItCannotBeBoughtBeforeTheCompanyCanOwnHardware()
        {
            var simulation = Company(cash: 30_000_000);
            var before = simulation.State.CashUsd;

            Assert.IsFalse(simulation.TryOpenServerRoom(false, out var why),
                "A company that has shipped nothing was sold a room it can never fill.");

            Assert.That(why, Is.Not.Empty, "Refused without saying why.");
            Assert.That(simulation.State.CashUsd, Is.EqualTo(before), "Refused and charged anyway.");
            Assert.IsFalse(simulation.State.HasServerRoom);
        }

        /// <summary>The gift is exempt: it costs nothing and it arrives with the tour.</summary>
        [Test]
        public void TheGiftStillArrivesBeforeTheCompanyCanOwnHardware()
        {
            var simulation = Company();

            Assert.IsTrue(simulation.TryOpenServerRoom(true, out var why), why);
            Assert.IsTrue(simulation.State.HasServerRoom);
        }

        [Test]
        public void ACompanyThatCannotAffordItIsToldSoRatherThanCharged()
        {
            var simulation = Company(cash: 100);
            var before = simulation.State.CashUsd;

            Assert.IsFalse(simulation.TryOpenServerRoom(false, out var why));
            Assert.That(why, Is.Not.Empty);
            Assert.That(simulation.State.CashUsd, Is.EqualTo(before));
            Assert.IsFalse(simulation.State.HasServerRoom);
        }

        [Test]
        public void TheRoomOnlyOpensOnce()
        {
            var simulation = Company();

            Assert.IsTrue(simulation.TryOpenServerRoom(true, out _));
            Assert.IsFalse(simulation.TryOpenServerRoom(false, out var why),
                "Opening it twice would double the starting cabinets.");
            Assert.That(why, Is.Not.Empty);
        }

        // ---- cabinets and money ------------------------------------------------------------------

        [Test]
        public void ARackIsPaidForBeforeItStandsOnTheFloor()
        {
            var simulation = Company();
            simulation.TryOpenServerRoom(true, out _);

            var before = simulation.State.CashUsd;
            var price = ServerRackCatalog.Get(ServerRack.HighDensity).PriceUsd;

            Assert.IsTrue(simulation.TryBuyRack(ServerRack.HighDensity, out var why), why);
            Assert.IsTrue(simulation.TryStandRack(0, 3, ServerRack.HighDensity, out why), why);

            Assert.That(simulation.State.CashUsd, Is.EqualTo(before - price));
            Assert.That(simulation.State.Hall.At(0, 3).Rack, Is.EqualTo(ServerRack.HighDensity));
        }

        [Test]
        public void ARackNobodyCanAffordDoesNotAppear()
        {
            var simulation = Company();
            simulation.TryOpenServerRoom(true, out _);
            simulation.State.CashUsd = 10;

            Assert.IsFalse(simulation.TryBuyRack(ServerRack.Immersion, out var why));
            Assert.That(why, Is.Not.Empty);
            Assert.IsTrue(simulation.State.Hall.At(0, 3).IsEmpty,
                "The hall must never gain a cabinet nobody paid for.");
        }

        // ---- the trade this whole room exists for -----------------------------------------------

        [Test]
        public void AFanTakesASlotThatCouldHaveBeenSilicon()
        {
            var simulation = Company();
            simulation.TryOpenServerRoom(true, out _);

            var hall = simulation.State.Hall;
            var slots = ServerRackCatalog.Get(hall.At(0, 0).Rack).Slots;

            var before = hall.FreeSlots(0, 0);
            Assert.That(before, Is.EqualTo(slots), "Nothing installed yet.");

            Assert.IsTrue(simulation.TryFitFan(0, 0, out var why), why);

            Assert.That(hall.FreeSlots(0, 0), Is.EqualTo(slots - ServerRackCatalog.FanSlots),
                "A slot given to air is a slot not given to a card, which is the only decision this "
                + "room asks the player to make twice.");
            Assert.That(hall.At(0, 0).Fans, Is.EqualTo(1));
        }

        [Test]
        public void AFanIsPaidForAndCanBeTakenBackOut()
        {
            var simulation = Company();
            simulation.TryOpenServerRoom(true, out _);

            var before = simulation.State.CashUsd;
            Assert.IsTrue(simulation.TryFitFan(0, 0, out _));

            Assert.That(simulation.State.CashUsd,
                Is.EqualTo(before - ServerRackCatalog.FanPriceUsd));

            Assert.IsTrue(simulation.State.Hall.TryPullFan(0, 0));
            Assert.That(simulation.State.Hall.At(0, 0).Fans, Is.Zero);
        }

        [Test]
        public void AFanWillNotFitInAFullRack()
        {
            var simulation = Company();
            simulation.TryOpenServerRoom(true, out _);

            var hall = simulation.State.Hall;
            var slots = ServerRackCatalog.Get(hall.At(0, 0).Rack).Slots;

            // Fill it with silicon, which is what a player does before wondering about the heat.
            hall.Stock(slots * 4);

            Assert.That(hall.FreeSlots(0, 0), Is.Zero);
            Assert.IsFalse(simulation.TryFitFan(0, 0, out var why));
            Assert.That(why, Is.Not.Empty);
        }

        /// <summary>
        /// Cooling actually recovers throughput a hot cabinet was losing.
        ///
        /// **This is the mechanic, and without this assertion the fans are decoration.** A fan that
        /// costs money and a slot and changes no number is exactly the class of thing this project
        /// has shipped six times.
        /// </summary>
        [Test]
        public void FansRecoverThroughputThatHeatWasTakingAway()
        {
            var simulation = Company(cash: 50_000_000);
            simulation.TryOpenServerRoom(true, out _);

            var hall = simulation.State.Hall;

            // A cheap frame stuffed with a late generation, which is the mistake the room is built
            // to punish and then to let you fix.
            //
            // **The generation matters and this is the design working.** An open frame sheds 1.5 kW
            // a slot; an H100 draws 0.7 and never troubles it. Cabinets do not age, chips get
            // hotter, so the cheap frame that was fine in 2022 is throttling by 2027 without the
            // player having changed anything. That is the spine applied to the one thing they own.
            Assert.IsTrue(simulation.TryBuyRack(ServerRack.OpenFrame, out _));
            Assert.IsTrue(simulation.TryStandRack(3, 3, ServerRack.OpenFrame, out _));

            // Full, which is what a player does first and what makes the cabinet hot.
            hall.Stock(hall.TotalSlots);

            var part = HardwareCatalog.Get(HardwareGenerationId.AcceleratorVr200);

            var before = hall.Output(part.PetaflopsPerUnit, part.PowerKilowatts);
            Assert.That(before.ThrottledRacks, Is.GreaterThan(0),
                "The fixture has to actually create a hot cabinet or it is measuring nothing.");

            // **Take a card out, put a fan in.** That is the whole decision and there is no other
            // way to make it: air and silicon share the cabinet, so cooling is always paid for in
            // compute rather than only in money.
            hall.Stock(hall.TotalSlots - 1);

            var fitted = 0;

            for (var index = 0; index < 3; index++)
            {
                if (simulation.TryFitFan(3, 3, out _))
                {
                    fitted++;
                }
            }

            Assert.That(fitted, Is.GreaterThan(0),
                "No fan went in, so the rest of this test would pass by measuring nothing.");

            var after = hall.Output(part.PetaflopsPerUnit, part.PowerKilowatts);

            Assert.That(after.ThrottledRacks, Is.LessThan(before.ThrottledRacks),
                "Fitting fans did not stop any cabinet throttling, so cooling is decoration.");

            Assert.That(after.Petaflops, Is.GreaterThan(before.Petaflops),
                "**One fewer card and more throughput**, which is the whole claim: past a point the "
                + "next card in a cabinet is worth less than the air it displaces. Without this the "
                + "fan is a purchase that changes no number anybody would act on.");
        }

        // ---- and it has to reach the fleet ---------------------------------------------------------

        /// <summary>
        /// The room's capacity arrives in the same profile the market and the books read.
        ///
        /// **The guard against the failure this project keeps hitting.** A room that computes
        /// petaflops nothing consumes is a complete mechanism with no way out, which is exactly what
        /// `ServerHall` was for months.
        /// </summary>
        [Test]
        public void WhatTheRoomProducesReachesTheFleet()
        {
            var simulation = Company(cash: 50_000_000);

            // Cards the company actually owns. The old version of this test stocked the hall
            // directly with as many accelerators as it had slots, which is how the room used to
            // conjure capacity out of nothing: it housed hardware nobody had bought. Fixing that
            // was the point, so the setup has to buy hardware the way a player does.
            simulation.State.Pool.AddAsset(new HardwareAsset(
                HardwareGenerationId.AcceleratorA100, ComputeTier.ColocatedServers, 24,
                simulation.State.Date, 10_000, 0));

            var rented = simulation.Profile;

            simulation.TryOpenServerRoom(true, out _);
            simulation.Advance(1);

            Assert.That(simulation.State.Hall.HousedAccelerators, Is.GreaterThan(0),
                "The company owns hardware and the racks are still empty, so nothing ever puts a "
                + "card in a cabinet and the room is a screen that computes numbers nothing "
                + "consumes.");

            var owned = simulation.Profile;

            // Effective, not raw. These cabinets do not throttle an A100, so the card produces
            // exactly what it did in a datacenter; what changes is how well it is used, and the
            // market consumes the effective figure rather than the nameplate one.
            Assert.That(owned.EffectivePetaflops, Is.Not.EqualTo(rented.EffectivePetaflops),
                "Housing the fleet changed nothing the market can read.");
        }

        /// <summary>
        /// The room is a cheaper address, not free hardware.
        ///
        /// **This is the whole trade and it is easy to lose in either direction.** Housing your own
        /// silicon saves a datacenter's monthly fee and costs you the utilisation a tuned hall
        /// would have given you. Make the basement produce more petaflops than the same cards in a
        /// datacenter and it stops being a decision; make it save nothing and nobody opens one.
        /// </summary>
        [Test]
        public void HousingYourOwnFleetIsCheaperAndSlightlyWorse()
        {
            var datacentre = Company(cash: 50_000_000);
            var basement = Company(cash: 50_000_000);

            foreach (var simulation in new[] { datacentre, basement })
            {
                simulation.State.Pool.AddAsset(new HardwareAsset(
                    HardwareGenerationId.AcceleratorA100, ComputeTier.ColocatedServers, 24,
                    simulation.State.Date, 10_000, 0));
            }

            basement.TryOpenServerRoom(true, out _);
            basement.Advance(1);
            datacentre.Advance(1);

            Assert.That(basement.State.Hall.HousedAccelerators, Is.GreaterThan(0));

            Assert.That(basement.Profile.EffectivePetaflops,
                Is.LessThan(datacentre.Profile.EffectivePetaflops),
                "A cabinet in a house got as much out of the same card as a datacenter did, which "
                + "makes the room free money rather than a trade.");

            Assert.That(basement.Profile.UtilizationCeiling,
                Is.LessThan(datacentre.Profile.UtilizationCeiling));
        }

        [Test]
        public void AnEmptyRoomStillCostsElectricity()
        {
            var simulation = Company();

            var before = simulation.Profile.DailyOperatingCostUsd;

            simulation.TryOpenServerRoom(true, out _);

            Assert.That(simulation.Profile.DailyOperatingCostUsd, Is.GreaterThan(before),
                "Four cabinets draw their idle whether or not anything is plugged into them, which "
                + "is the reason a room is a commitment rather than a container.");
        }

        // ---- the save --------------------------------------------------------------------------

        [Test]
        public void TheFloorSurvivesASave()
        {
            var simulation = Company(cash: 50_000_000);
            simulation.TryOpenServerRoom(true, out _);
            simulation.TryBuyRack(ServerRack.Immersion, out _);
            simulation.TryStandRack(2, 2, ServerRack.Immersion, out _);
            simulation.TryFitFan(2, 2, out _);
            simulation.TryFitFan(2, 2, out _);
            simulation.State.Hall.Stock(20);

            var racks = new System.Collections.Generic.List<int>();
            var cards = new System.Collections.Generic.List<int>();
            var fans = new System.Collections.Generic.List<int>();

            simulation.State.Hall.Capture(racks, cards, fans);

            var reloaded = new ServerHall(CompanyState.BasementColumns, CompanyState.BasementRows);
            reloaded.Restore(racks, cards, fans);

            Assert.That(reloaded.RackCount, Is.EqualTo(simulation.State.Hall.RackCount));
            Assert.That(reloaded.FanCount, Is.EqualTo(simulation.State.Hall.FanCount),
                "Fans are a purchase. A save that forgets them refunds them.");
            Assert.That(reloaded.At(2, 2).Rack, Is.EqualTo(ServerRack.Immersion));
            Assert.That(reloaded.HousedAccelerators,
                Is.EqualTo(simulation.State.Hall.HousedAccelerators));
        }

        /// <summary>
        /// Every phrase the room uses exists in both languages.
        /// </summary>
        [Test]
        public void TheRoomReadsInBothLanguages()
        {
            var was = Loc.Current;

            try
            {
                foreach (var language in new[] { Language.English, Language.Polish })
                {
                    Loc.Current = language;

                    foreach (var key in new[]
                             {
                                 "room.title", "room.strap", "room.floor", "room.rack",
                                 "room.locked.title", "room.locked.body", "rack.slots",
                                 "rack.healthy", "rack.throttled", "part.fan"
                             })
                    {
                        Assert.That(Loc.T(key), Is.Not.EqualTo(key),
                            $"{language}: {key} is printing itself at the player.");
                    }
                }
            }
            finally
            {
                Loc.Current = was;
            }
        }
    }
}
