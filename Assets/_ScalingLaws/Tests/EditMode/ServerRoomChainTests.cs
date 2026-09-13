using NUnit.Framework;
using ScalingLaws.Core;
using ScalingLaws.Data;
using ScalingLaws.Simulation;
using ScalingLaws.UI;

namespace ScalingLaws.Tests.EditMode
{
    /// <summary>
    /// The shop, the cabinets, the power and the people, as one chain.
    ///
    /// **Asked plainly, and it deserved a plain answer**: does the shop work, do the parts actually
    /// go into the cabinets, and does what comes out of them hold the users up. Every fixture around
    /// this one tests a link. `ServerHallTests` proves the grid, `ServerRoomTests` proves the room
    /// can be opened and filled, `ComputeTierGateTests` proves the till refuses a locked tier. None
    /// of them walks from a button in the shop to a number a customer reacts to, and a chain of
    /// green links is not a green chain: that is the difference this project has been caught by
    /// twelve times.
    ///
    /// So every step here goes through the call the interface makes. The price comes from
    /// <see cref="PartsShop"/>, the purchase from <see cref="CompanySimulation.TryBuyHardware"/>,
    /// the housing from the daily tick, and the last assertion is on
    /// <see cref="ServiceQuality.Capacity"/>, which is what the market reads.
    /// </summary>
    public sealed class ServerRoomChainTests
    {
        /// <summary>
        /// A company that can legally buy silicon: cash in the bank and one model shipped.
        ///
        /// **The shipped model is not decoration.** `ComputeTier.ColocatedServers` asks for one, so
        /// a company without it is refused at the till however much money it has, and a fixture
        /// that forgot would be testing the gate rather than the room.
        /// </summary>
        private static CompanySimulation Company(long cash = 400_000_000)
        {
            var state = new CompanyState("Prometheus AI", 4242)
            {
                Date = GameDate.FromCalendar(2023, 6, 1),
                CashUsd = cash
            };

            state.AddDeployedModel(new DeployedModel(
                "Aurora 1", ArchitectureId.DenseTransformer, 20, GameDate.Start, 1e10, 1.0));

            return new CompanySimulation(state);
        }

        /// <summary>The silicon a 2023 room would actually be filled with.</summary>
        private const HardwareGenerationId Card = HardwareGenerationId.AcceleratorH100;

        // ---- the shop ------------------------------------------------------------------------

        [Test]
        public void TheShopHasSomethingToSellAndPricesItTheWayTheTillCharges()
        {
            var simulation = Company();
            var shop = new PartsShop(simulation, null);

            var stock = shop.OnSale();
            Assert.That(stock.Count, Is.GreaterThan(0), "A shop with an empty shelf is a dead end.");

            var generation = stock[0];
            var quoted = shop.UnitPriceUsd(generation);
            Assert.That(quoted, Is.GreaterThan(0L));

            var before = simulation.State.CashUsd;
            Assert.That(simulation.TryBuyHardware(generation.Id, 4, ComputeTier.ColocatedServers,
                out var why), Is.True, why);

            var charged = before - simulation.State.CashUsd;

            // **The row and the till must agree to the dollar.** A shop that priced things itself
            // would be a second opinion, and the first time scarcity moved the player would be
            // quoted one figure and charged another.
            Assert.That(charged, Is.EqualTo(quoted * 4),
                $"The row quoted ${quoted:N0} each and the account lost ${charged:N0} for four.");
        }

        [Test]
        public void TheShopOpensOnTheNewestSiliconAndEverySortChangesTheOrder()
        {
            var simulation = Company();
            simulation.State.Date = GameDate.FromCalendar(2026, 1, 1);

            var shop = new PartsShop(simulation, null);
            var newest = shop.OnSale();

            Assert.That(newest.Count, Is.GreaterThan(3),
                "By 2026 there are several generations on the shelf, or the sort is untestable.");

            for (var index = 1; index < newest.Count; index++)
            {
                Assert.That(newest[index].ReleaseDate.DayIndex,
                    Is.LessThanOrEqualTo(newest[index - 1].ReleaseDate.DayIndex),
                    "A shop opens on what shipped last.");
            }

            shop.Order = PartsOrder.Price;
            var cheapest = shop.OnSale();

            Assert.That(shop.UnitPriceUsd(cheapest[0]),
                Is.LessThanOrEqualTo(shop.UnitPriceUsd(cheapest[cheapest.Count - 1])));

            shop.Order = PartsOrder.Power;
            var strongest = shop.OnSale();

            Assert.That(strongest[0].PetaflopsPerUnit,
                Is.GreaterThanOrEqualTo(strongest[strongest.Count - 1].PetaflopsPerUnit));
        }

        [Test]
        public void TheShopSaysWhyItCannotSellRatherThanRefusingAtTheTill()
        {
            // No shipped model, so the colocated tier is shut. The player must be able to read that
            // from the shop rather than by pressing BUY and watching nothing happen.
            var state = new CompanyState("Nobody yet") { CashUsd = 400_000_000 };
            var shop = new PartsShop(new CompanySimulation(state), null);

            Assert.That(shop.CanBuy, Is.False);
            Assert.That(shop.LockReason, Does.Contain("released model"));
        }

        // ---- the cabinets --------------------------------------------------------------------

        /// <summary>
        /// Standing cabinets, buying cards, and finding the cards in the cabinets a lead time later.
        /// </summary>
        [Test]
        public void CardsBoughtInTheShopEndUpInTheCabinetsOnTheFloor()
        {
            var simulation = Company();
            Assert.That(simulation.TryOpenServerRoom(true, out var why), Is.True, why);

            var hall = simulation.State.Hall;
            var slotsBefore = hall.TotalSlots;

            Assert.That(simulation.TryBuyRack(ServerRack.HighDensity, out why), Is.True, why);
            Assert.That(simulation.TryStandRack(1, 1, ServerRack.HighDensity, out why), Is.True, why);

            Assert.That(hall.TotalSlots, Is.GreaterThan(slotsBefore),
                "A cabinet on the floor is slots the company did not have.");

            Assert.That(hall.HousedAccelerators, Is.Zero,
                "Cabinets arrive empty. There is nothing to put in them yet.");

            Assert.That(simulation.TryBuyHardware(Card, 8, ComputeTier.ColocatedServers, out why),
                Is.True, why);

            // The lead time is real and it is the tier's own. Nothing is housed until it arrives.
            simulation.Advance(1);
            Assert.That(hall.HousedAccelerators, Is.Zero,
                "Silicon ordered today is not silicon on the floor today.");

            simulation.Advance(ComputeTierCatalog.Get(ComputeTier.ColocatedServers).LeadTimeDays + 1);

            Assert.That(hall.HousedAccelerators, Is.EqualTo(8),
                "Eight cards were bought and the floor has room for all of them.");
        }

        [Test]
        public void MoreSiliconThanTheFloorHoldsStaysOutsideTheRoom()
        {
            var simulation = Company();
            simulation.TryOpenServerRoom(true, out _);

            var hall = simulation.State.Hall;
            var slots = hall.TotalSlots;

            Assert.That(simulation.TryBuyHardware(Card, slots + 40, ComputeTier.ColocatedServers,
                out var why), Is.True, why);

            simulation.Advance(ComputeTierCatalog.Get(ComputeTier.ColocatedServers).LeadTimeDays + 1);

            Assert.That(hall.HousedAccelerators, Is.EqualTo(slots),
                "The room holds what it holds. The rest is still owned and still housed elsewhere.");
        }

        /// <summary>
        /// A fan costs a slot, and the slot it costs is one a card would have been standing in.
        ///
        /// **This was a hole rather than a detail.** `FreeSlots` charged the fan and `Stock` did
        /// not, so the cabinet panel said a fan takes a slot while the daily refill put a full set
        /// of cards back in anyway. The trade the whole cooling mechanic is built on, one card for
        /// one fan, was never actually charged: a fan was more cooling for nothing.
        /// </summary>
        [Test]
        public void AFanTakesTheSlotThePanelSaysItTakes()
        {
            var simulation = Company();
            simulation.TryOpenServerRoom(true, out _);

            var hall = simulation.State.Hall;

            Assert.That(simulation.TryBuyRack(ServerRack.OpenFrame, out var why), Is.True, why);
            Assert.That(simulation.TryStandRack(3, 3, ServerRack.OpenFrame, out why), Is.True, why);

            var slots = hall.TotalSlots;

            // **Before stocking, because a full cabinet has nowhere to put a fan.** That refusal is
            // correct and it is also the mechanic: the slot a fan needs is a slot a card is in.
            Assert.That(simulation.TryFitFan(3, 3, out why), Is.True, why);

            Assert.That(hall.TotalSlots, Is.EqualTo(slots - ServerRackCatalog.FanSlots),
                "A fan occupies a slot, so the floor has one fewer to let.");

            Assert.That(hall.Stock(slots), Is.EqualTo(slots - ServerRackCatalog.FanSlots),
                "And the refill must respect it, or the fan is cooling that costs nothing.");

            Assert.That(hall.FreeSlots(3, 3), Is.Zero,
                "The cabinet the fan went into is full: cards in every slot but its own.");
        }

        // ---- the power, and the people ---------------------------------------------------------

        /// <summary>
        /// The whole chain in one measurement: cards in cabinets turn into capacity a customer feels.
        /// </summary>
        [Test]
        public void WhatIsInTheCabinetsIsCapacityTheMarketCanUse()
        {
            var empty = Company();
            var stocked = Company();

            stocked.TryOpenServerRoom(true, out var why);
            Assert.That(stocked.State.HasServerRoom, Is.True, why);

            var slots = stocked.State.Hall.TotalSlots;
            Assert.That(stocked.TryBuyHardware(Card, slots, ComputeTier.ColocatedServers, out why),
                Is.True, why);

            var wait = ComputeTierCatalog.Get(ComputeTier.ColocatedServers).LeadTimeDays + 2;
            empty.Advance(wait);
            stocked.Advance(wait);

            Assert.That(stocked.State.Hall.HousedAccelerators, Is.EqualTo(slots));

            Assert.That(stocked.Profile.RawPetaflops, Is.GreaterThan(empty.Profile.RawPetaflops),
                "Cards standing in cabinets are compute, or the room is an ornament.");

            Assert.That(stocked.State.LastQuality.Capacity,
                Is.GreaterThan(empty.State.LastQuality.Capacity),
                "And compute is capacity the customers meet. This is the link that matters: a "
                + "room that raises petaflops and not capacity is not serving anybody.");

            Assert.That(stocked.State.LastQuality.Reliability,
                Is.GreaterThanOrEqualTo(empty.State.LastQuality.Reliability),
                "More capacity against the same demand is never a worse service.");
        }

        // ---- the five colours -------------------------------------------------------------------

        /// <summary>
        /// An empty cabinet is not green, and that was the whole of the report.
        /// </summary>
        [Test]
        public void ACabinetProducingNothingSaysSoRatherThanReadingAsHealthy()
        {
            var simulation = Company();
            simulation.TryOpenServerRoom(true, out _);

            var hall = simulation.State.Hall;
            var square = hall.Occupied().GetEnumerator();
            Assert.That(square.MoveNext(), Is.True, "The gift stands four cabinets.");

            var empty = hall.HeatAt(square.Current.Column, square.Current.Row, 2.5);

            Assert.That(empty, Is.EqualTo(ServerRackCatalog.RackHeat.Idle),
                "A cabinet with no silicon in it is not running cool. It is not running, and it "
                + "used to come back Comfortable, which is the same green a working cabinet gets.");
        }

        [Test]
        public void TheFiveStatesAreOrderedAndEachCoversItsOwnBand()
        {
            Assert.That(ServerRackCatalog.HeatOf(0.0), Is.EqualTo(ServerRackCatalog.RackHeat.Idle));
            Assert.That(ServerRackCatalog.HeatOf(0.30), Is.EqualTo(ServerRackCatalog.RackHeat.Cool));
            Assert.That(ServerRackCatalog.HeatOf(0.75),
                Is.EqualTo(ServerRackCatalog.RackHeat.Comfortable));
            Assert.That(ServerRackCatalog.HeatOf(0.95), Is.EqualTo(ServerRackCatalog.RackHeat.Warm));
            Assert.That(ServerRackCatalog.HeatOf(1.40),
                Is.EqualTo(ServerRackCatalog.RackHeat.Cooking));

            // The one that has to be exact: red begins where the cabinet actually starts losing
            // throughput, so the colour and the arithmetic cannot disagree about the same cabinet.
            Assert.That(ServerRackCatalog.HeatOf(ServerRackCatalog.ThrottleFreeHeadroom),
                Is.Not.EqualTo(ServerRackCatalog.RackHeat.Cooking));
            Assert.That(ServerRackCatalog.HeatOf(ServerRackCatalog.ThrottleFreeHeadroom + 0.01),
                Is.EqualTo(ServerRackCatalog.RackHeat.Cooking));

            Assert.That(ServerRackCatalog.ThrottleFactor(
                    ServerRackCatalog.ThrottleFreeHeadroom + 0.01, 1.0),
                Is.LessThan(1.0),
                "Red has to mean a loss the player can measure, not a shade of opinion.");
        }

        [Test]
        public void EveryStateHasAWordAndASentenceInBothLanguages()
        {
            var was = Loc.Current;

            try
            {
                foreach (Language language in System.Enum.GetValues(typeof(Language)))
                {
                    Loc.Current = language;

                    foreach (ServerRackCatalog.RackHeat state
                        in System.Enum.GetValues(typeof(ServerRackCatalog.RackHeat)))
                    {
                        var key = ServerRackCatalog.KeyFor(state);

                        // Built by concatenation, so the literal-reading guard cannot see it. This
                        // is the fixture that shape of key exists to be covered by.
                        Assert.That(Loc.T(key), Is.Not.EqualTo(key), $"{language}: {key}");
                        Assert.That(Loc.T(key + ".note"), Is.Not.EqualTo(key + ".note"),
                            $"{language}: {key}.note");
                    }
                }
            }
            finally
            {
                Loc.Current = was;
            }
        }

        /// <summary>
        /// The stylesheet and the 3D room paint the same five colours.
        ///
        /// **SF-07, guarded rather than promised.** There were four copies of this mapping and two
        /// of them had already drifted. A comment asking two files to agree is not a guarantee; a
        /// test that reads one of them back is.
        /// </summary>
        [Test]
        public void TheRoomPaintsOneColourPerState()
        {
            var sheet = System.IO.File.ReadAllText(System.IO.Path.Combine(
                UnityEngine.Application.dataPath, "_ScalingLaws", "Resources", "ScalingLaws.uss"));

            foreach (ServerRackCatalog.RackHeat state
                in System.Enum.GetValues(typeof(ServerRackCatalog.RackHeat)))
            {
                var selector = "." + ServerRackCatalog.ClassFor(state);
                var at = sheet.IndexOf(selector + " {", System.StringComparison.Ordinal);

                Assert.That(at, Is.GreaterThan(-1), $"{selector} is named from C# and not styled.");

                var open = sheet.IndexOf("rgb(", at, System.StringComparison.Ordinal);
                var close = sheet.IndexOf(')', open);
                var parts = sheet.Substring(open + 4, close - open - 4).Split(',');

                var painted = ScalingLaws.UI.RackHeatPalette.Of(state);

                Assert.That(int.Parse(parts[0].Trim()) / 255f,
                    Is.EqualTo(painted.r).Within(1f / 255f), $"{selector} red");
                Assert.That(int.Parse(parts[1].Trim()) / 255f,
                    Is.EqualTo(painted.g).Within(1f / 255f), $"{selector} green");
                Assert.That(int.Parse(parts[2].Trim()) / 255f,
                    Is.EqualTo(painted.b).Within(1f / 255f), $"{selector} blue");
            }
        }

        /// <summary>
        /// And it is paid for. A room that produced capacity for free would break the spine.
        /// </summary>
        [Test]
        public void EveryCardInTheRoomIsOnTheElectricityBill()
        {
            var simulation = Company();
            simulation.TryOpenServerRoom(true, out _);

            var slots = simulation.State.Hall.TotalSlots;
            simulation.Advance(1);

            var emptyRoom = simulation.Profile.DailyOperatingCostUsd;
            var emptyDraw = simulation.Profile.PowerDrawKilowatts;

            Assert.That(simulation.TryBuyHardware(Card, slots, ComputeTier.ColocatedServers,
                out var why), Is.True, why);

            simulation.Advance(ComputeTierCatalog.Get(ComputeTier.ColocatedServers).LeadTimeDays + 1);

            Assert.That(simulation.Profile.PowerDrawKilowatts, Is.GreaterThan(emptyDraw),
                "Cards draw power. An empty cabinet and a full one cannot cost the same.");

            Assert.That(simulation.Profile.DailyOperatingCostUsd, Is.GreaterThan(emptyRoom),
                "And the draw is invoiced, at the room's own tariff.");
        }
    }
}
