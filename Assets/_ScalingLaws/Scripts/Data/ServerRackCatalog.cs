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
        public ServerRackDefinition(ServerRack id, string displayName, string pitch, int slots,
            long priceUsd, double coolingCapacityKilowatts, double idleDrawKilowatts,
            long monthlyUpkeepUsd, string art, string note)
        {
            Id = id;
            DisplayName = displayName ?? id.ToString();
            Pitch = pitch ?? string.Empty;
            Slots = Math.Clamp(slots, 1, 128);
            PriceUsd = Math.Clamp(priceUsd, 0L, 100_000_000L);
            CoolingCapacityKilowatts = Math.Clamp(SimUnits.Finite(coolingCapacityKilowatts), 0.5, 400.0);
            IdleDrawKilowatts = Math.Clamp(SimUnits.Finite(idleDrawKilowatts), 0.0, 40.0);
            MonthlyUpkeepUsd = Math.Clamp(monthlyUpkeepUsd, 0L, 10_000_000L);
            Art = art ?? string.Empty;
            Note = note ?? string.Empty;
        }

        public ServerRack Id { get; }
        public string DisplayName { get; }
        public string Pitch { get; }

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
        public string Note { get; }

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

        private static readonly ServerRackDefinition[] Entries =
        {
            new(ServerRack.OpenFrame, "Open frame",
                "Four posts and some cable ties. The room is the cooling, which works until the room "
                + "is full.",
                slots: 4,
                priceUsd: 9_000,
                coolingCapacityKilowatts: 6.0,
                idleDrawKilowatts: 0.1,
                monthlyUpkeepUsd: 200,
                art: "rack_lv1",
                note: "Cheapest per slot in the game and the first thing to throttle. Fine while the "
                    + "hall is half empty and a false economy once it is not."),

            new(ServerRack.Enclosed, "Enclosed rack",
                "Doors, a blanking kit and a proper airflow path. What a normal room is full of.",
                slots: 8,
                priceUsd: 34_000,
                coolingCapacityKilowatts: 14.0,
                idleDrawKilowatts: 0.4,
                monthlyUpkeepUsd: 700,
                art: "rack_lv2",
                note: "The one to beat. Neither the cheap answer nor the dense one."),

            new(ServerRack.HighDensity, "High density",
                "Rear door heat exchanger. Twice the accelerators in the same footprint, and it "
                + "still sheds what they make.",
                slots: 16,
                priceUsd: 96_000,
                coolingCapacityKilowatts: 38.0,
                idleDrawKilowatts: 1.4,
                monthlyUpkeepUsd: 2_600,
                art: "rack_lv3",
                note: "Dear per slot and it buys floor space, which is the thing a hall runs out of "
                    + "before it runs out of money."),

            new(ServerRack.Immersion, "Immersion tank",
                "The whole thing sits in dielectric fluid. Nothing else in the game fits this much "
                + "in one square, and nothing else costs this much to keep.",
                slots: 28,
                priceUsd: 240_000,
                coolingCapacityKilowatts: 90.0,
                idleDrawKilowatts: 2.2,
                monthlyUpkeepUsd: 9_000,
                art: "rack_lv4",
                note: "Worth it when the floor is the binding constraint and a waste of nine thousand "
                    + "a month when it is not.")
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
        /// What a rack actually delivers when it is asked to shed more heat than it can.
        ///
        /// Linear from full output at its rating down to <see cref="WorstThrottle"/> at twice it.
        /// The slots stay occupied and the power is still drawn, which is the point: overfilling a
        /// cheap rack is money spent on accelerators that spend their afternoons at half speed.
        /// </summary>
        public static double ThrottleFactor(double heatKilowatts, double coolingKilowatts)
        {
            var heat = Math.Max(0.0, SimUnits.Finite(heatKilowatts));
            var cooling = Math.Max(0.1, SimUnits.Finite(coolingKilowatts, 0.1));

            var ratio = heat / cooling;
            if (ratio <= ThrottleFreeHeadroom)
            {
                return 1.0;
            }

            var over = (ratio - ThrottleFreeHeadroom) / ThrottleFreeHeadroom;
            return Math.Clamp(1.0 - over * (1.0 - WorstThrottle), WorstThrottle, 1.0);
        }
    }
}
