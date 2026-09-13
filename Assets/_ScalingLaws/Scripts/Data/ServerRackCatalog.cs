using System;
using System.Collections.Generic;
using ScalingLaws.Core;

namespace ScalingLaws.Data
{
    /// <summary>The four racks a company can stand in a hall of its own.</summary>
    public enum ServerRack
    {
        None = 0,

        /// <summary>Open frame. Cheap, holds little, and the room does the cooling.</summary>
        OpenFrame = 1,

        /// <summary>The ordinary enclosed rack everybody starts with.</summary>
        Enclosed = 2,

        /// <summary>Rear door heat exchangers. Dense, and it stays dense under load.</summary>
        HighDensity = 3,

        /// <summary>Immersion. The most slots per square metre in the game, at a price.</summary>
        Immersion = 4
    }

    /// <summary>
    /// One rack: what it holds, what it costs, and how well it sheds heat.
    ///
    /// **Cooling is the constraint that stops this being a ladder.** Without it four racks collapse
    /// into "buy the dearest one you can afford", which is the failure mode the hosting packages
    /// were explicitly designed around. A rack that cannot shed its heat does not stop working, it
    /// throttles: the slots are still there and the petaflops are not, and the power bill is paid
    /// either way.
    ///
    /// So the trade is real in both directions. Open frames are nearly free and waste most of a
    /// square metre; immersion fits four times as much in the same floor and costs more than the
    /// accelerators standing in it.
    /// </summary>
    public readonly struct ServerRackDefinition
    {
        public ServerRackDefinition(ServerRack id, int slots,
            long priceUsd, double coolingCapacityKilowatts, double idleDrawKilowatts,
            long monthlyUpkeepUsd, string art)
        {
            Id = id;
            Slots = Math.Clamp(slots, 1, 128);
            PriceUsd = Math.Clamp(priceUsd, 0L, 100_000_000L);
            CoolingCapacityKilowatts = Math.Clamp(SimUnits.Finite(coolingCapacityKilowatts), 0.5, 400.0);
            IdleDrawKilowatts = Math.Clamp(SimUnits.Finite(idleDrawKilowatts), 0.0, 40.0);
            MonthlyUpkeepUsd = Math.Clamp(monthlyUpkeepUsd, 0L, 10_000_000L);
            Art = art ?? string.Empty;
        }

        public ServerRack Id { get; }

        private static string KeyFor(ServerRack id) => id switch
        {
            ServerRack.Enclosed => "rack.enclosed",
            ServerRack.HighDensity => "rack.highdensity",
            ServerRack.Immersion => "rack.immersion",
            _ => "rack.openframe"
        };

        /// <summary>Read from the book at access time. See `PrecisionDefinition`.</summary>
        public string DisplayName => Loc.T(KeyFor(Id));
        public string Pitch => Loc.T(KeyFor(Id) + ".pitch");

        /// <summary>Accelerators it can physically hold.</summary>
        public int Slots { get; }

        public long PriceUsd { get; }

        /// <summary>
        /// Heat it can move before what is inside it starts throttling.
        ///
        /// Measured against what the accelerators in it actually draw, so filling a cheap rack with
        /// a hot generation is a real mistake with a real symptom rather than a refused purchase.
        /// </summary>
        public double CoolingCapacityKilowatts { get; }

        /// <summary>What the rack itself draws before anything is in it. Fans and pumps.</summary>
        public double IdleDrawKilowatts { get; }

        /// <summary>Servicing. Immersion needs somebody who knows what they are doing.</summary>
        public long MonthlyUpkeepUsd { get; }

        public string Art { get; }

        /// <summary>When this cabinet is the right answer, and when it is not.</summary>
        public string Note => Loc.T(KeyFor(Id) + ".note");

        /// <summary>What one slot of capacity cost to buy. The figure the cards compare on.</summary>
        public long PricePerSlotUsd => Slots <= 0 ? PriceUsd : PriceUsd / Slots;

        public override string ToString() =>
            $"{DisplayName}: {Slots} slots, {CoolingCapacityKilowatts:0.0} kW cooling";
    }

    /// <summary>
    /// The rack catalog, and the rule that keeps four of them worth having.
    ///
    /// A test walks every pair and fails if any rack is ever cheaper per slot **and** better cooled
    /// **and** cheaper to keep, because at that moment the other three are decoration. Same guard the
    /// marketing channels carry, for the same reason.
    /// </summary>
    public static class ServerRackCatalog
    {
        public const string CatalogVersion = "racks-1";

        /// <summary>
        /// How far past its cooling a rack can run before the heat costs it anything.
        ///
        /// A little headroom, because a rack sitting at exactly its rating on a warm afternoon is
        /// normal engineering rather than a fault.
        /// </summary>
        public const double ThrottleFreeHeadroom = 1.05;

        /// <summary>The worst a badly cooled rack is ever reduced to. It throttles, it does not die.</summary>
        public const double WorstThrottle = 0.45;

        /// <summary>
        /// How steeply heat past the headroom costs throughput.
        ///
        /// **Measured rather than chosen, and the mechanic did not exist before it.** With a gentle
        /// slope a full cabinet at twenty per cent over its rating lost eight per cent, so trading a
        /// card for a fan was never worth it in any rack, with any generation, at any point in the
        /// game. Fans cost money and a slot and changed no number anybody would act on, which is
        /// exactly the class of thing this project has shipped six times.
        ///
        /// At 2.2 the sums come out like this, and the pattern is the spine of the whole game:
        ///
        /// | Generation | Open frame | Enclosed | High density | Immersion |
        /// |---|---|---|---|---|
        /// | H100 to GB300 | full | full | full | full |
        /// | VR200 (2027) | **3 cards + 1 fan** | full | full | full |
        /// | Next (2029) | **3 + 1** | **7 + 1** | full | full |
        ///
        /// Cabinets do not age. Chips get hotter. A room filled in 2023 and never revisited is
        /// quietly throttling by 2027 without the player having changed anything, and the fix costs
        /// a slot. That is "upgrades are timed, not purchased" applied to the one thing they own.
        /// </summary>
        public const double ThrottlePenalty = 2.2;

        // ---- cooling you add afterwards ---------------------------------------------------------
        //
        // A rack's rating is what it sheds on its own. Everything past that is a fan, and a fan
        // takes a slot: the trade is silicon against air in the same cabinet, which is the decision
        // this whole screen exists for.

        /// <summary>Heat one fan moves, on top of whatever the rack sheds by itself.</summary>
        public const double FanCoolingKilowatts = 2.4;

        /// <summary>What a fan draws. Moving heat out of a box costs a little more heat.</summary>
        public const double FanDrawKilowatts = 0.18;

        /// <summary>Slots one fan occupies. One, or nobody would ever weigh it against a card.</summary>
        public const int FanSlots = 1;

        public const long FanPriceUsd = 2_600;

        /// <summary>Servicing, per fan, per month. Bearings.</summary>
        public const long FanMonthlyUpkeepUsd = 40;

        private static readonly ServerRackDefinition[] Entries =
        {
            // The words are `rack.*` in the phrase book.
            new(ServerRack.OpenFrame,
                slots: 4,
                priceUsd: 9_000,
                coolingCapacityKilowatts: 6.0,
                idleDrawKilowatts: 0.1,
                monthlyUpkeepUsd: 200,
                art: "rack_lv1"),

            new(ServerRack.Enclosed,
                slots: 8,
                priceUsd: 34_000,
                coolingCapacityKilowatts: 14.0,
                idleDrawKilowatts: 0.4,
                monthlyUpkeepUsd: 700,
                art: "rack_lv2"),

            new(ServerRack.HighDensity,
                slots: 16,
                priceUsd: 96_000,
                coolingCapacityKilowatts: 38.0,
                idleDrawKilowatts: 1.4,
                monthlyUpkeepUsd: 2_600,
                art: "rack_lv3"),

            new(ServerRack.Immersion,
                slots: 28,
                priceUsd: 240_000,
                coolingCapacityKilowatts: 90.0,
                idleDrawKilowatts: 2.2,
                monthlyUpkeepUsd: 9_000,
                art: "rack_lv4")
        };

        public static IReadOnlyList<ServerRackDefinition> All => Entries;

        public static ServerRackDefinition Get(ServerRack id)
        {
            foreach (var entry in Entries)
            {
                if (entry.Id == id)
                {
                    return entry;
                }
            }

            return Entries[1];
        }

        public static bool TryGet(ServerRack id, out ServerRackDefinition definition)
        {
            foreach (var entry in Entries)
            {
                if (entry.Id == id)
                {
                    definition = entry;
                    return true;
                }
            }

            definition = default;
            return false;
        }

        /// <summary>
        /// How a cabinet is doing, in the four words the room is coloured with.
        ///
        /// Green, amber, red. <see cref="Throttling"/> and <see cref="Cooking"/> are both red on the
        /// floor and are kept apart because the panel that opens on one cabinet has room to say
        /// which, and "over its rating" and "half speed" are different problems with different
        /// fixes.
        /// </summary>
        /// <summary>
        /// What one cabinet is doing, in five states, and they are five because they are five
        /// colours.
        ///
        /// **<see cref="Idle"/> is the one that was missing and it was the one worth having.**
        /// A cabinet with no silicon in it read as <c>Comfortable</c>, which is green, which is
        /// the same answer a cabinet doing full work under its rating gets. So a room where
        /// half the cabinets were empty looked like a room that was fine, and the player had
        /// nothing to click. An empty cabinet is not running cool. It is not running.
        ///
        /// The order is the scale, coldest first, so a worst-cabinet comparison is a
        /// comparison rather than a table.
        /// </summary>
        public enum RackHeat
        {
            /// <summary>Nothing in it, or nothing standing on the square. Produces no power.</summary>
            Idle = 0,

            /// <summary>
            /// Working, with room for half as many cards again inside its own rating.
            /// </summary>
            Cool = 1,

            /// <summary>Working normally. Inside its rating, no headroom worth planning around.</summary>
            Comfortable = 2,

            /// <summary>Near the top of what it can shed. Still full output, nothing left over.</summary>
            Warm = 3,

            /// <summary>
            /// Past the headroom and losing throughput. The power is still being paid for and
            /// the work is not being done, which is the whole cost of overfilling a cabinet.
            /// </summary>
            Cooking = 4
        }

        /// <summary>
        /// Where a cabinet stops having room worth planning around.
        ///
        /// **Two thirds, derived rather than chosen**: at this load half as many cards again
        /// would still sit inside the cabinet's own rating, which is what "there is room in
        /// here" has to mean to somebody deciding where the next purchase goes.
        /// </summary>
        public const double CoolBelow = 2.0 / 3.0;

        /// <summary>Where the room turns amber. Past <see cref="ThrottleFreeHeadroom"/> it is red.</summary>
        public const double WarmAbove = 0.85;

        /// <summary>
        /// The heat ratio turned into a colour, and it is the only place that mapping is made.
        ///
        /// **Two copies of this is the failure the author's own guide calls SF-07.** The floor tile,
        /// the cabinet panel, the corner banner and the room in 3D all show the same cabinet, and a
        /// tile that is green beside a panel saying the rack is throttling is a disagreement with no
        /// owner and no assertion.
        /// </summary>
        public static RackHeat HeatOf(double ratio)
        {
            var heat = SimUnits.Finite(ratio);

            // **Zero is not cold, it is off.** `HeatRatio` answers zero for an empty square and
            // for a cabinet holding no silicon, and both of those used to come back green.
            if (heat <= 0.0)
            {
                return RackHeat.Idle;
            }

            if (heat > ThrottleFreeHeadroom)
            {
                return RackHeat.Cooking;
            }

            if (heat > WarmAbove)
            {
                return RackHeat.Warm;
            }

            return heat > CoolBelow ? RackHeat.Comfortable : RackHeat.Cool;
        }

        /// <summary>
        /// The phrase-book stem for a state, so the word and the colour cannot drift.
        ///
        /// Written out rather than built from the enum name, for the reason every catalog here
        /// already follows: a key assembled by concatenation is invisible to
        /// <c>LocalisationTests.EveryKeyTheInterfaceAsksForExists</c>, and this project has
        /// shipped a screen of raw keys once already.
        /// </summary>
        public static string KeyFor(RackHeat heat) => heat switch
        {
            RackHeat.Cool => "rack.state.cool",
            RackHeat.Comfortable => "rack.state.ok",
            RackHeat.Warm => "rack.state.warm",
            RackHeat.Cooking => "rack.state.hot",
            _ => "rack.state.idle"
        };

        /// <summary>The class the interface paints a state with. One mapping, same as the word.</summary>
        public static string ClassFor(RackHeat heat) => heat switch
        {
            RackHeat.Cool => "rheat--cool",
            RackHeat.Comfortable => "rheat--ok",
            RackHeat.Warm => "rheat--warm",
            RackHeat.Cooking => "rheat--hot",
            _ => "rheat--idle"
        };

        /// <summary>
        /// What a rack actually delivers when it is asked to shed more heat than it can.
        ///
        /// Linear from full output at its rating down to <see cref="WorstThrottle"/> at twice it.
        /// The slots stay occupied and the power is still drawn, which is the point: overfilling a
        /// cheap rack is money spent on accelerators that spend their afternoons at half speed.
        /// </summary>
        /// <param name="penalty">
        /// How steeply this kind of cabinet falls off past its rating. Defaults to
        /// <see cref="ThrottlePenalty"/>, which is every cabinet in a company that has researched
        /// nothing; <see cref="RoomUpgrades.PenaltyFor"/> softens it for immersion tanks once
        /// liquid loops are in. An argument rather than a second method, because two throttle
        /// curves is the disagreement-with-a-date-on-it this project keeps rediscovering.
        /// </param>
        public static double ThrottleFactor(double heatKilowatts, double coolingKilowatts,
            double penalty = ThrottlePenalty)
        {
            var heat = Math.Max(0.0, SimUnits.Finite(heatKilowatts));
            var cooling = Math.Max(0.1, SimUnits.Finite(coolingKilowatts, 0.1));
            var steepness = Math.Clamp(SimUnits.Finite(penalty, ThrottlePenalty), 0.2, 4.0);

            var ratio = heat / cooling;
            if (ratio <= ThrottleFreeHeadroom)
            {
                return 1.0;
            }

            var over = (ratio - ThrottleFreeHeadroom) / ThrottleFreeHeadroom;
            return Math.Clamp(1.0 - over * steepness, WorstThrottle, 1.0);
        }
    }
}
