using System;
using System.Collections.Generic;
using ScalingLaws.Core;
using ScalingLaws.Data;

namespace ScalingLaws.Simulation
{
    /// <summary>One square of floor, and whatever is standing on it.</summary>
    public readonly struct HallSquare
    {
        public HallSquare(int column, int row, ServerRack rack, int accelerators, int fans = 0)
        {
            Column = column;
            Row = row;
            Rack = rack;
            Accelerators = Math.Max(0, accelerators);
            Fans = Math.Max(0, fans);
        }

        public int Column { get; }
        public int Row { get; }

        /// <summary>What is standing here, or <see cref="ServerRack.None"/> for bare floor.</summary>
        public ServerRack Rack { get; }

        /// <summary>How many accelerators are actually in it. Never more than the rack holds.</summary>
        public int Accelerators { get; }

        /// <summary>
        /// Fans fitted, each taking a slot the silicon cannot have.
        ///
        /// The rack's own rating is what it sheds unaided; these are what a player adds when the
        /// generation they wanted turned out to run hotter than the cabinet can take.
        /// </summary>
        public int Fans { get; }

        public bool IsEmpty => Rack == ServerRack.None;

        public override string ToString() =>
            IsEmpty ? $"({Column},{Row}) empty" : $"({Column},{Row}) {Rack} x{Accelerators}";
    }

    /// <summary>
    /// The floor of a server hall, as data.
    ///
    /// **Deliberately written before any scene exists.** A placement system built inside a scene is
    /// a system that can only be tested by looking at it, and every layout fault in this project was
    /// found by looking rather than by a test precisely because the scene was the only copy of the
    /// truth. The hall is a grid of squares here, in `Simulation/`, with no `UnityEngine` anywhere
    /// near it; the scene will draw this and send clicks back to it.
    ///
    /// The rules it owns are the ones that have to hold whatever the interface does: one rack to a
    /// square, no rack outside the floor, and heat measured per rack rather than for the room, so
    /// putting a hot generation in a cheap frame is a mistake with a symptom rather than a purchase
    /// the game refuses.
    /// </summary>
    public sealed class ServerHall
    {
        /// <summary>The starting hall. Six by six is thirty six squares, which is the size asked for.</summary>
        public const int DefaultColumns = 6;
        public const int DefaultRows = 6;

        private readonly ServerRack[] racks;
        private readonly int[] accelerators;
        private readonly int[] fans;

        public ServerHall(int columns = DefaultColumns, int rows = DefaultRows)
        {
            Columns = Math.Clamp(columns, 1, 32);
            Rows = Math.Clamp(rows, 1, 32);
            fans = new int[Columns * Rows];

            racks = new ServerRack[Columns * Rows];
            accelerators = new int[Columns * Rows];
        }

        public int Columns { get; }
        public int Rows { get; }

        public int SquareCount => Columns * Rows;

        public bool Contains(int column, int row) =>
            column >= 0 && row >= 0 && column < Columns && row < Rows;

        private int IndexOf(int column, int row) => row * Columns + column;

        public HallSquare At(int column, int row) =>
            Contains(column, row)
                ? new HallSquare(column, row, racks[IndexOf(column, row)],
                    accelerators[IndexOf(column, row)], fans[IndexOf(column, row)])
                : new HallSquare(column, row, ServerRack.None, 0);

        public bool IsEmpty(int column, int row) =>
            Contains(column, row) && racks[IndexOf(column, row)] == ServerRack.None;

        /// <summary>Every square that has something on it.</summary>
        public List<HallSquare> Occupied()
        {
            var found = new List<HallSquare>();
            for (var row = 0; row < Rows; row++)
            {
                for (var column = 0; column < Columns; column++)
                {
                    if (racks[IndexOf(column, row)] != ServerRack.None)
                    {
                        found.Add(At(column, row));
                    }
                }
            }

            return found;
        }

        public int RackCount
        {
            get
            {
                var count = 0;
                foreach (var rack in racks)
                {
                    if (rack != ServerRack.None)
                    {
                        count++;
                    }
                }

                return count;
            }
        }

        public int FreeSquares => SquareCount - RackCount;

        // ---- placing and removing -----------------------------------------------------------

        /// <summary>
        /// Stands a rack on an empty square.
        ///
        /// Refuses rather than replaces. Standing a new rack on an occupied square would silently
        /// destroy whatever was there along with the accelerators in it, and a build mode where a
        /// mis-click costs a quarter of a million is a build mode nobody relaxes in.
        /// </summary>
        public bool TryPlace(int column, int row, ServerRack rack, out string failureReason)
        {
            failureReason = string.Empty;

            if (rack == ServerRack.None)
            {
                failureReason = "Nothing to place.";
                return false;
            }

            if (!Contains(column, row))
            {
                failureReason = "That square is not on the floor.";
                return false;
            }

            if (racks[IndexOf(column, row)] != ServerRack.None)
            {
                failureReason = "Something is already standing there.";
                return false;
            }

            racks[IndexOf(column, row)] = rack;
            accelerators[IndexOf(column, row)] = 0;
            return true;
        }

        /// <summary>
        /// Takes a rack off the floor and says what was in it.
        ///
        /// The accelerators come back as a count rather than being destroyed, because they are the
        /// expensive half and selling a rack is not a decision to scrap what it held.
        /// </summary>
        public bool TryRemove(int column, int row, out ServerRack removed, out int freedAccelerators,
            out string failureReason)
        {
            removed = ServerRack.None;
            freedAccelerators = 0;
            failureReason = string.Empty;

            if (!Contains(column, row))
            {
                failureReason = "That square is not on the floor.";
                return false;
            }

            var index = IndexOf(column, row);
            if (racks[index] == ServerRack.None)
            {
                failureReason = "Nothing is standing there.";
                return false;
            }

            removed = racks[index];
            freedAccelerators = accelerators[index];

            racks[index] = ServerRack.None;
            accelerators[index] = 0;
            fans[index] = 0;
            return true;
        }

        /// <summary>
        /// Takes a rack off the floor and hands back everything that was fitted to it.
        ///
        /// **The fans were being destroyed.** `TryRemove` reported the accelerators and silently
        /// zeroed the fan count, which cost nothing while nothing could remove a rack and costs
        /// real money the moment a player can pick one up and put it down two squares along. A
        /// build mode where moving a cabinet quietly burns $2,600 of cooling is a build mode
        /// nobody experiments in.
        /// </summary>
        public bool TryLift(int column, int row, out ServerRack removed, out int freedFans,
            out string failureReason)
        {
            removed = ServerRack.None;
            freedFans = 0;

            if (!Contains(column, row))
            {
                failureReason = "That square is not on the floor.";
                return false;
            }

            freedFans = fans[IndexOf(column, row)];

            return TryRemove(column, row, out removed, out _, out failureReason);
        }

        /// <summary>
        /// Slides a cabinet to another square with everything in it.
        ///
        /// One call rather than lift-then-place, because a lift that succeeds followed by a place
        /// that fails leaves the rack nowhere, and the interface would have to know how to undo
        /// half a move. Here the target is checked before anything leaves the first square.
        ///
        /// The accelerators are not carried: `Stock` redistributes the whole fleet across the floor
        /// on every tick, so what is in a cabinet is an arrangement rather than a possession. The
        /// fans are carried, because those were bought for that cabinet.
        /// </summary>
        public bool TryMove(int fromColumn, int fromRow, int toColumn, int toRow,
            out string failureReason)
        {
            failureReason = string.Empty;

            if (!Contains(fromColumn, fromRow) || !Contains(toColumn, toRow))
            {
                failureReason = "That square is not on the floor.";
                return false;
            }

            if (fromColumn == toColumn && fromRow == toRow)
            {
                return true;
            }

            var from = IndexOf(fromColumn, fromRow);
            var to = IndexOf(toColumn, toRow);

            if (racks[from] == ServerRack.None)
            {
                failureReason = "Nothing is standing there.";
                return false;
            }

            if (racks[to] != ServerRack.None)
            {
                failureReason = "Something is already standing there.";
                return false;
            }

            racks[to] = racks[from];
            fans[to] = fans[from];
            accelerators[to] = accelerators[from];

            racks[from] = ServerRack.None;
            fans[from] = 0;
            accelerators[from] = 0;

            return true;
        }

        /// <summary>
        /// How many slots this square has left once its silicon and its fans are counted.
        ///
        /// **Both compete for the same space**, which is the entire decision this screen exists for:
        /// a slot given to air is a slot not given to a card, and the newest generation needs
        /// several of them to run at all.
        /// </summary>
        public int FreeSlots(int column, int row)
        {
            if (!Contains(column, row))
            {
                return 0;
            }

            var index = IndexOf(column, row);

            if (racks[index] == ServerRack.None)
            {
                return 0;
            }

            var total = ServerRackCatalog.Get(racks[index]).Slots;
            var used = accelerators[index] + fans[index] * ServerRackCatalog.FanSlots;

            return Math.Max(0, total - used);
        }

        /// <summary>Fits one fan, if the cabinet has room for it.</summary>
        public bool TryFitFan(int column, int row, out string failureReason)
        {
            failureReason = string.Empty;

            if (!Contains(column, row) || racks[IndexOf(column, row)] == ServerRack.None)
            {
                failureReason = "there is no rack on that square";
                return false;
            }

            if (FreeSlots(column, row) < ServerRackCatalog.FanSlots)
            {
                failureReason = "the rack is full";
                return false;
            }

            fans[IndexOf(column, row)]++;
            return true;
        }

        /// <summary>Takes one out again. False when there was none to take.</summary>
        public bool TryPullFan(int column, int row)
        {
            if (!Contains(column, row) || fans[IndexOf(column, row)] <= 0)
            {
                return false;
            }

            fans[IndexOf(column, row)]--;
            return true;
        }

        /// <summary>
        /// How hard one cabinet is being asked to work, as a fraction of what it can shed.
        ///
        /// **One computation, read by everything that draws this room.** The floor tile, the 3D
        /// room, the cabinet panel and the corner banner all colour the same square, and the tile
        /// used to work its own ratio out inline. Two formulas for one quantity is the disagreement
        /// with a date on it that this project keeps rediscovering, and here it would show as a
        /// green cabinet next to a panel saying it is throttling.
        ///
        /// Zero for an empty square and for one with nothing in it: a cabinet holding no silicon is
        /// not running cool, it is not running.
        /// </summary>
        /// <param name="upgrades">
        /// What the company has researched about running a room. **Nullable, defaulting to null
        /// rather than to `default`**: `default(RoomUpgrades)` never runs the constructor, so it
        /// would arrive with no cooling and a zero throttle penalty and silently make heat free.
        /// </param>
        public double HeatRatio(int column, int row, double kilowattsPerAccelerator,
            RoomUpgrades? upgrades = null)
        {
            if (!Contains(column, row))
            {
                return 0.0;
            }

            var index = IndexOf(column, row);

            if (racks[index] == ServerRack.None || accelerators[index] <= 0)
            {
                return 0.0;
            }

            var definition = ServerRackCatalog.Get(racks[index]);
            var heat = accelerators[index] * Math.Max(0.0, SimUnits.Finite(kilowattsPerAccelerator));

            var cooling = (upgrades ?? RoomUpgrades.None).CoolingFor(definition, fans[index]);

            return heat / Math.Max(0.1, cooling);
        }

        /// <summary>The same reading as a colour. See <see cref="ServerRackCatalog.HeatOf"/>.</summary>
        public ServerRackCatalog.RackHeat HeatAt(int column, int row,
            double kilowattsPerAccelerator, RoomUpgrades? upgrades = null) =>
            ServerRackCatalog.HeatOf(HeatRatio(column, row, kilowattsPerAccelerator, upgrades));

        /// <summary>Every fan on the floor.</summary>
        public int FanCount
        {
            get
            {
                var total = 0;

                foreach (var count in fans)
                {
                    total += count;
                }

                return total;
            }
        }

        /// <summary>
        /// Fills the halls's racks with a number of accelerators, front to back.
        ///
        /// The player does not place accelerators one at a time; they own a fleet and the hall holds
        /// as much of it as it has slots for. Returns how many actually fit, so the caller can say
        /// what is standing in the yard with nowhere to go.
        /// </summary>
        public int Stock(int available)
        {
            var wanted = Math.Max(0, available);
            var slots = TotalSlots;

            if (slots <= 0)
            {
                for (var index = 0; index < accelerators.Length; index++)
                {
                    accelerators[index] = 0;
                }

                return 0;
            }

            var housed = 0;
            var remainder = Math.Min(wanted, slots);

            // Spread in proportion to each rack's own capacity rather than filling the first one
            // until it cooks. A room that crams everything into rack one and leaves rack two empty
            // is not a room anybody runs, and it made the rack choice invisible: the heat landed
            // wherever the array happened to start.
            for (var index = 0; index < racks.Length; index++)
            {
                if (racks[index] == ServerRack.None)
                {
                    accelerators[index] = 0;
                    continue;
                }

                var capacity = ServerRackCatalog.Get(racks[index]).Slots;
                var share = (int)((long)capacity * Math.Min(wanted, slots) / slots);

                accelerators[index] = share;
                housed += share;
                remainder -= share;
            }

            // Whatever the division left over, one at a time into whatever still has room.
            for (var index = 0; index < racks.Length && remainder > 0; index++)
            {
                if (racks[index] == ServerRack.None)
                {
                    continue;
                }

                var capacity = ServerRackCatalog.Get(racks[index]).Slots;
                if (accelerators[index] >= capacity)
                {
                    continue;
                }

                accelerators[index]++;
                housed++;
                remainder--;
            }

            return housed;
        }

        public int TotalSlots
        {
            get
            {
                var slots = 0;
                foreach (var rack in racks)
                {
                    if (rack != ServerRack.None)
                    {
                        slots += ServerRackCatalog.Get(rack).Slots;
                    }
                }

                return slots;
            }
        }

        public int HousedAccelerators
        {
            get
            {
                var total = 0;
                foreach (var count in accelerators)
                {
                    total += count;
                }

                return total;
            }
        }

        // ---- what the hall costs and delivers ---------------------------------------------------

        /// <summary>Idle draw plus upkeep, before anything is switched on.</summary>
        public double IdleDrawKilowatts
        {
            get
            {
                var draw = 0.0;
                foreach (var rack in racks)
                {
                    if (rack != ServerRack.None)
                    {
                        draw += ServerRackCatalog.Get(rack).IdleDrawKilowatts;
                    }
                }

                return draw;
            }
        }

        public long MonthlyUpkeepUsd
        {
            get
            {
                var total = 0L;
                foreach (var rack in racks)
                {
                    if (rack != ServerRack.None)
                    {
                        total += ServerRackCatalog.Get(rack).MonthlyUpkeepUsd;
                    }
                }

                // Bearings. A fan is a moving part in a room with no moving parts.
                return total + FanCount * ServerRackCatalog.FanMonthlyUpkeepUsd;
            }
        }

        /// <summary>
        /// What the hall delivers, given what one accelerator makes and draws.
        ///
        /// **Heat is worked out per rack, not for the room.** A hall that is comfortable on average
        /// and has one immersion tank's worth of accelerators crammed into open frames is a hall
        /// where those accelerators run at half speed, and averaging would hide exactly the mistake
        /// this system exists to let the player make.
        /// </summary>
        public HallOutput Output(double petaflopsPerAccelerator, double kilowattsPerAccelerator,
            RoomUpgrades? upgrades = null)
        {
            var perUnit = Math.Max(0.0, SimUnits.Finite(petaflopsPerAccelerator));
            var perUnitHeat = Math.Max(0.0, SimUnits.Finite(kilowattsPerAccelerator));
            var room = upgrades ?? RoomUpgrades.None;

            var petaflops = 0.0;

            // Every rack on the floor draws its idle, stocked or not. Fans and pumps do not care
            // whether anything is plugged in, and a hall full of empty immersion tanks is a bill.
            var draw = IdleDrawKilowatts;
            var throttled = 0;

            for (var index = 0; index < racks.Length; index++)
            {
                if (racks[index] == ServerRack.None || accelerators[index] <= 0)
                {
                    continue;
                }

                var definition = ServerRackCatalog.Get(racks[index]);
                var heat = accelerators[index] * perUnitHeat;

                // **Fans raise the cabinet's rating rather than lowering the heat**, which is the
                // honest shape: air moves warmth out of the box, it does not make the silicon draw
                // less. The bill goes up either way, and that is the cost of the fix.
                //
                // Airflow modelling adds to this for every cabinet at once, and liquid loops
                // flatten the curve past it for immersion tanks alone. Both come in through
                // `RoomUpgrades` so the hall goes on knowing nothing about the research tree.
                var cooling = room.CoolingFor(definition, fans[index]);

                var factor = ServerRackCatalog.ThrottleFactor(
                    heat, cooling, room.PenaltyFor(racks[index]));
                if (factor < 1.0)
                {
                    throttled++;
                }

                petaflops += accelerators[index] * perUnit * factor;

                // The power is drawn whether or not the work gets done, which is the whole cost of
                // getting this wrong: the bill is for the heat, and the output is not. The fans are
                // on the same bill.
                draw += heat + fans[index] * ServerRackCatalog.FanDrawKilowatts;
            }

            return new HallOutput(petaflops, draw, throttled);
        }

        // ---- persistence ------------------------------------------------------------------------

        public void Capture(List<int> intoRacks, List<int> intoAccelerators,
            List<int> intoFans = null)
        {
            intoRacks.Clear();
            intoAccelerators.Clear();
            intoFans?.Clear();

            foreach (var rack in racks)
            {
                intoRacks.Add((int)rack);
            }

            foreach (var count in accelerators)
            {
                intoAccelerators.Add(count);
            }

            if (intoFans == null)
            {
                return;
            }

            foreach (var count in fans)
            {
                intoFans.Add(count);
            }
        }

        public void Restore(IReadOnlyList<int> savedRacks, IReadOnlyList<int> savedAccelerators,
            IReadOnlyList<int> savedFans = null)
        {
            for (var index = 0; index < racks.Length; index++)
            {
                racks[index] = ServerRack.None;
                accelerators[index] = 0;
                fans[index] = 0;

                if (savedRacks != null && index < savedRacks.Count
                    && Enum.IsDefined(typeof(ServerRack), savedRacks[index]))
                {
                    racks[index] = (ServerRack)savedRacks[index];
                }

                if (savedAccelerators != null && index < savedAccelerators.Count)
                {
                    accelerators[index] = Math.Max(0, savedAccelerators[index]);
                }

                // A file written before fans existed has none, which is the true reading of it.
                if (savedFans != null && index < savedFans.Count)
                {
                    fans[index] = Math.Max(0, savedFans[index]);
                }
            }
        }

        public void Clear()
        {
            for (var index = 0; index < racks.Length; index++)
            {
                racks[index] = ServerRack.None;
                accelerators[index] = 0;
                fans[index] = 0;
            }
        }
    }

    /// <summary>What a hall is delivering right now.</summary>
    public readonly struct HallOutput
    {
        public HallOutput(double petaflops, double drawKilowatts, int throttledRacks)
        {
            Petaflops = Math.Max(0.0, SimUnits.Finite(petaflops));
            DrawKilowatts = Math.Max(0.0, SimUnits.Finite(drawKilowatts));
            ThrottledRacks = Math.Max(0, throttledRacks);
        }

        public double Petaflops { get; }
        public double DrawKilowatts { get; }

        /// <summary>How many racks are running hot. The number the hall screen warns on.</summary>
        public int ThrottledRacks { get; }

        public bool IsHealthy => ThrottledRacks == 0;
    }
}
