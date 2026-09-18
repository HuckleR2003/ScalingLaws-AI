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

        /// <summary>
        /// True on the left-hand square of a room cooler. The cooler also covers the square to its
        /// right, and that square is found from the anchor rather than stored, so a cooler can
        /// never be half on the floor.
        /// </summary>
        private readonly bool[] coolers;

        /// <summary>Overclock level per square, zero for stock. Meaningful only under a cabinet.</summary>
        private readonly int[] overclock;

        /// <summary>
        /// Which cards stand in each cabinet, by generation. Null where nothing is recorded.
        ///
        /// **`accelerators` stays the count and this is the identity.** A card in a cabinet
        /// whose generation is not recorded is *unknown*: a v61 room, or a room a test filled with
        /// <see cref="Fill"/>. Unknown cards are priced at the fleet's average and are given a
        /// generation by the next <see cref="Stock(IReadOnlyDictionary{HardwareGenerationId,int})"/>,
        /// newest first, from what the company owns and has not put anywhere.
        /// </summary>
        private readonly Dictionary<HardwareGenerationId, int>[] kinds;

        public ServerHall(int columns = DefaultColumns, int rows = DefaultRows)
        {
            Columns = Math.Clamp(columns, 1, 32);
            Rows = Math.Clamp(rows, 1, 32);
            fans = new int[Columns * Rows];

            racks = new ServerRack[Columns * Rows];
            accelerators = new int[Columns * Rows];
            coolers = new bool[Columns * Rows];
            overclock = new int[Columns * Rows];
            kinds = new Dictionary<HardwareGenerationId, int>[Columns * Rows];
        }

        // ---- which cards are where --------------------------------------------------------------

        private int KnownIn(int index)
        {
            var total = 0;

            if (kinds[index] != null)
            {
                foreach (var count in kinds[index].Values)
                {
                    total += count;
                }
            }

            return total;
        }

        private int UnknownIn(int index) => Math.Max(0, accelerators[index] - KnownIn(index));

        private void AddKind(int index, HardwareGenerationId generation, int count = 1)
        {
            kinds[index] ??= new Dictionary<HardwareGenerationId, int>();
            kinds[index].TryGetValue(generation, out var held);
            kinds[index][generation] = held + count;
        }

        private bool RemoveKind(int index, HardwareGenerationId generation)
        {
            if (kinds[index] == null || !kinds[index].TryGetValue(generation, out var held) || held <= 0)
            {
                return false;
            }

            if (held == 1)
            {
                kinds[index].Remove(generation);
            }
            else
            {
                kinds[index][generation] = held - 1;
            }

            return true;
        }

        /// <summary>
        /// Takes one card out of a cabinet when nobody said which: an unknown one first, then the
        /// weakest. Used where a rule has to give a slot back, never where a player chose a card.
        /// </summary>
        private void RemoveAny(int index)
        {
            if (accelerators[index] <= 0)
            {
                return;
            }

            if (UnknownIn(index) <= 0 && kinds[index] != null)
            {
                var weakest = default(HardwareGenerationId);
                var weakestPf = double.MaxValue;

                foreach (var pair in kinds[index])
                {
                    var pf = HardwareCatalog.TryGet(pair.Key, out var part) ? part.PetaflopsPerUnit : 0.0;

                    if (pair.Value > 0 && pf < weakestPf)
                    {
                        weakest = pair.Key;
                        weakestPf = pf;
                    }
                }

                RemoveKind(index, weakest);
            }

            accelerators[index]--;
        }

        /// <summary>
        /// What a cabinet's cards make and draw at stock clocks, before heat. Recorded cards at
        /// their own figures, unknown ones at the averages the caller passes.
        /// </summary>
        private (double Petaflops, double Kilowatts) Load(int index, double averagePetaflops,
            double averageKilowatts)
        {
            var unknown = UnknownIn(index);
            var petaflops = unknown * Math.Max(0.0, SimUnits.Finite(averagePetaflops));
            var kilowatts = unknown * Math.Max(0.0, SimUnits.Finite(averageKilowatts));

            if (kinds[index] != null)
            {
                foreach (var pair in kinds[index])
                {
                    if (!HardwareCatalog.TryGet(pair.Key, out var part))
                    {
                        continue;
                    }

                    petaflops += pair.Value * part.PetaflopsPerUnit;
                    kilowatts += pair.Value * part.PowerKilowatts;
                }
            }

            return (petaflops, kilowatts);
        }

        /// <summary>
        /// The cards in one cabinet, strongest first. Unknown cards are reported with
        /// <paramref name="unknown"/> so the screen can say so rather than inventing a model.
        /// </summary>
        public List<(HardwareGenerationId Generation, int Count)> CardsIn(int column, int row,
            out int unknown)
        {
            unknown = 0;
            var found = new List<(HardwareGenerationId, int)>();

            if (!Contains(column, row))
            {
                return found;
            }

            var index = IndexOf(column, row);
            unknown = UnknownIn(index);

            if (kinds[index] != null)
            {
                foreach (var pair in kinds[index])
                {
                    if (pair.Value > 0)
                    {
                        found.Add((pair.Key, pair.Value));
                    }
                }
            }

            found.Sort((left, right) => Strength(right.Item1).CompareTo(Strength(left.Item1)));
            return found;
        }

        private static double Strength(HardwareGenerationId generation) =>
            HardwareCatalog.TryGet(generation, out var part) ? part.PetaflopsPerUnit : 0.0;

        /// <summary>How many cards of one generation stand anywhere on this floor.</summary>
        public int HousedOf(HardwareGenerationId generation)
        {
            var total = 0;

            foreach (var map in kinds)
            {
                if (map != null && map.TryGetValue(generation, out var count))
                {
                    total += count;
                }
            }

            return total;
        }

        /// <summary>
        /// What every card on the floor makes and draws at stock clocks, before heat. The fleet
        /// takes this off the colocation figures, so the cards downstairs are not paid for twice.
        /// </summary>
        public (double Petaflops, double Kilowatts) HousedRaw(double averagePetaflops,
            double averageKilowatts)
        {
            var petaflops = 0.0;
            var kilowatts = 0.0;

            for (var index = 0; index < racks.Length; index++)
            {
                if (racks[index] == ServerRack.None || accelerators[index] <= 0)
                {
                    continue;
                }

                var (pf, kw) = Load(index, averagePetaflops, averageKilowatts);
                petaflops += pf;
                kilowatts += kw;
            }

            return (petaflops, kilowatts);
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

        /// <summary>Bare floor: no cabinet and no cooler standing on it.</summary>
        public bool IsEmpty(int column, int row) =>
            Contains(column, row) && racks[IndexOf(column, row)] == ServerRack.None
            && !IsCooler(column, row);

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

        public int FreeSquares => SquareCount - RackCount - 2 * CoolerCount;

        // ---- room coolers ----------------------------------------------------------------------

        /// <summary>Every room cooler on the floor. Each covers two squares.</summary>
        public int CoolerCount
        {
            get
            {
                var count = 0;

                foreach (var anchor in coolers)
                {
                    if (anchor)
                    {
                        count++;
                    }
                }

                return count;
            }
        }

        /// <summary>
        /// Whether a cooler covers this square, and if so which square it is anchored on.
        /// A cooler stands on its anchor and the square to its right.
        /// </summary>
        public bool TryCoolerAt(int column, int row, out int anchorColumn)
        {
            anchorColumn = -1;

            if (!Contains(column, row))
            {
                return false;
            }

            if (coolers[IndexOf(column, row)])
            {
                anchorColumn = column;
                return true;
            }

            if (column > 0 && coolers[IndexOf(column - 1, row)])
            {
                anchorColumn = column - 1;
                return true;
            }

            return false;
        }

        public bool IsCooler(int column, int row) => TryCoolerAt(column, row, out _);

        /// <summary>
        /// Stands a room cooler on this square and the one to its right.
        ///
        /// Two squares, as asked, because the cost of air has to be counted in the same currency
        /// as a cabinet: floor. A cooler on one square would be a purchase; a cooler on two is a
        /// decision about what the room is for.
        /// </summary>
        public bool TryPlaceCooler(int column, int row, out string failureReason)
        {
            failureReason = string.Empty;

            if (!Contains(column, row) || !Contains(column + 1, row))
            {
                failureReason = Loc.T("room.cooler_no_room");
                return false;
            }

            if (!IsEmpty(column, row) || !IsEmpty(column + 1, row))
            {
                failureReason = Loc.T("room.square_taken");
                return false;
            }

            coolers[IndexOf(column, row)] = true;
            return true;
        }

        /// <summary>Takes a cooler off the floor, from either of its squares.</summary>
        public bool TryRemoveCooler(int column, int row, out string failureReason)
        {
            failureReason = string.Empty;

            if (!TryCoolerAt(column, row, out var anchor))
            {
                failureReason = Loc.T("room.no_cooler_here");
                return false;
            }

            coolers[IndexOf(anchor, row)] = false;
            return true;
        }

        /// <summary>
        /// Slides a cooler to another pair of squares. Checked before anything moves, and a cooler
        /// is allowed to overlap the squares it is leaving, so shuffling one along by a square works.
        /// </summary>
        public bool TryMoveCooler(int fromColumn, int fromRow, int toColumn, int toRow,
            out string failureReason)
        {
            failureReason = string.Empty;

            if (!TryCoolerAt(fromColumn, fromRow, out var anchor))
            {
                failureReason = Loc.T("room.no_cooler_here");
                return false;
            }

            coolers[IndexOf(anchor, fromRow)] = false;

            if (TryPlaceCooler(toColumn, toRow, out failureReason))
            {
                return true;
            }

            coolers[IndexOf(anchor, fromRow)] = true;
            return false;
        }

        // ---- overclock -------------------------------------------------------------------------

        /// <summary>The level a cabinet has been set to. It only runs while the room allows it.</summary>
        public int OverclockAt(int column, int row) =>
            Contains(column, row) && racks[IndexOf(column, row)] != ServerRack.None
                ? overclock[IndexOf(column, row)]
                : 0;

        /// <summary>
        /// Sets a cabinet's overclock. Raising it needs a room with air to spare, today; lowering it
        /// is always allowed, because backing off is never the dangerous direction.
        /// </summary>
        public bool TrySetOverclock(int column, int row, int level, double kilowattsPerAccelerator,
            RoomUpgrades? upgrades, out string failureReason)
        {
            failureReason = string.Empty;

            if (!Contains(column, row) || racks[IndexOf(column, row)] == ServerRack.None)
            {
                failureReason = Loc.T("room.no_rack_here");
                return false;
            }

            var index = IndexOf(column, row);
            var wanted = Math.Clamp(level, 0, ServerRackCatalog.OverclockLevels);

            if (wanted > overclock[index])
            {
                var climate = Climate(kilowattsPerAccelerator, upgrades);

                if (climate.Ratio >= ServerRackCatalog.OverclockAllowedBelow)
                {
                    failureReason = Loc.T("rack.oc_room_warm");
                    return false;
                }
            }

            overclock[index] = wanted;
            return true;
        }

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
                failureReason = Loc.T("room.nothing_to_place");
                return false;
            }

            if (!Contains(column, row))
            {
                failureReason = Loc.T("room.off_the_floor");
                return false;
            }

            if (racks[IndexOf(column, row)] != ServerRack.None || IsCooler(column, row))
            {
                failureReason = Loc.T("room.square_taken");
                return false;
            }

            racks[IndexOf(column, row)] = rack;
            accelerators[IndexOf(column, row)] = 0;
            kinds[IndexOf(column, row)] = null;
            overclock[IndexOf(column, row)] = 0;
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
                failureReason = Loc.T("room.off_the_floor");
                return false;
            }

            var index = IndexOf(column, row);
            if (racks[index] == ServerRack.None)
            {
                failureReason = Loc.T("room.square_empty");
                return false;
            }

            removed = racks[index];
            freedAccelerators = accelerators[index];

            racks[index] = ServerRack.None;
            accelerators[index] = 0;
            kinds[index] = null;
            fans[index] = 0;
            overclock[index] = 0;
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
                failureReason = Loc.T("room.off_the_floor");
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
        /// Everything in it travels with it: the cards, which the player put there by hand, and the
        /// fans, which were bought for that cabinet.
        /// </summary>
        public bool TryMove(int fromColumn, int fromRow, int toColumn, int toRow,
            out string failureReason)
        {
            failureReason = string.Empty;

            if (!Contains(fromColumn, fromRow) || !Contains(toColumn, toRow))
            {
                failureReason = Loc.T("room.off_the_floor");
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
                failureReason = Loc.T("room.square_empty");
                return false;
            }

            if (racks[to] != ServerRack.None || IsCooler(toColumn, toRow))
            {
                failureReason = Loc.T("room.square_taken");
                return false;
            }

            racks[to] = racks[from];
            fans[to] = fans[from];
            accelerators[to] = accelerators[from];
            kinds[to] = kinds[from];
            overclock[to] = overclock[from];

            racks[from] = ServerRack.None;
            fans[from] = 0;
            accelerators[from] = 0;
            kinds[from] = null;
            overclock[from] = 0;

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

            return Math.Max(0, CardCapacity(index) - accelerators[index]);
        }

        /// <summary>Fits one fan, if the cabinet has room for it.</summary>
        public bool TryFitFan(int column, int row, out string failureReason)
        {
            failureReason = string.Empty;

            if (!Contains(column, row) || racks[IndexOf(column, row)] == ServerRack.None)
            {
                failureReason = Loc.T("room.no_rack_here");
                return false;
            }

            // **A full cabinet still refuses.** Making it evict a card looked like a kindness and
            // `AFanWillNotFitInAFullRack` said no within a minute: the player takes a card out and
            // then puts the fan in, which is two deliberate clicks rather than one that quietly
            // spends compute. `TryPullCard` is the other half and it exists for this.
            if (FreeSlots(column, row) < ServerRackCatalog.FanSlots)
            {
                failureReason = Loc.T("room.rack_full");
                return false;
            }

            fans[IndexOf(column, row)]++;
            return true;
        }

        /// <summary>
        /// Takes one card out of one cabinet.
        ///
        /// **The other half of <see cref="TryFitCard"/>, and the reason a fan can go into a full
        /// cabinet at all.** Air and silicon share the slots, so the player takes a card out and
        /// then puts the fan in. Two clicks, both deliberate.
        ///
        /// The card is not destroyed. It goes back to the store, not in a cabinet, and **it stays
        /// there**: nothing refills a slot the player emptied. The pass that used to do exactly
        /// that every tick is why a card could never be taken out.
        /// </summary>
        public bool TryPullCard(int column, int row, out string failureReason) =>
            TryPullCard(column, row, null, out failureReason);

        /// <summary>Takes one card of this generation out, or any card when none is named.</summary>
        public bool TryPullCard(int column, int row, HardwareGenerationId? generation,
            out string failureReason)
        {
            failureReason = string.Empty;

            if (!Contains(column, row) || racks[IndexOf(column, row)] == ServerRack.None)
            {
                failureReason = Loc.T("room.no_rack_here");
                return false;
            }

            var index = IndexOf(column, row);

            if (accelerators[index] <= 0)
            {
                failureReason = Loc.T("rack.none_here");
                return false;
            }

            if (generation.HasValue)
            {
                if (!RemoveKind(index, generation.Value))
                {
                    failureReason = Loc.T("rack.none_here");
                    return false;
                }

                accelerators[index]--;
                return true;
            }

            RemoveAny(index);
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
            var climate = Climate(kilowattsPerAccelerator, upgrades);
            var level = climate.OverclocksRunning ? overclock[index] : 0;

            var heat = Load(index, 0.0, kilowattsPerAccelerator).Kilowatts
                       * (1.0 + ServerRackCatalog.OverclockHeatPerLevel * level);

            // The room's own heat reaches every cabinet in it: the air going in is already warm.
            var cooling = (upgrades ?? RoomUpgrades.None).CoolingFor(definition, fans[index])
                          * climate.CabinetFactor;

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
        /// Brings the floor into line with a fleet: trims what cannot be there, **and adds nothing**.
        ///
        /// **It used to top up every cabinet from whatever was loose, every tick.** That made every
        /// arrangement a player made by hand last a fraction of a second, a card could never be
        /// taken out (the next pass put it straight back), and new silicon mounted itself wherever
        /// the arithmetic fell. Reported three times as "the parts mount themselves and I cannot
        /// take anything out". Cards go into cabinets by hand now, and this only keeps the floor
        /// honest:
        ///
        /// 1. **Can it be there.** A cabinet that lost slots to a fan gives cards back.
        /// 2. **Is it owned.** Selling a generation takes its cards off the floor, fullest cabinet
        ///    first.
        /// 3. **What is it.** A card with no recorded generation is named from what is owned and
        ///    not yet in a cabinet, newest first.
        ///
        /// Returns how many are housed.
        /// </summary>
        public int Stock(IReadOnlyDictionary<HardwareGenerationId, int> owned)
        {
            // ---- 1. nothing may stand where it cannot ----------------------------------------
            for (var index = 0; index < accelerators.Length; index++)
            {
                while (accelerators[index] > CardCapacity(index))
                {
                    RemoveAny(index);
                }
            }

            // ---- 2. nothing may stand that is not owned, generation by generation ------------
            var generations = new HashSet<HardwareGenerationId>();

            foreach (var map in kinds)
            {
                if (map != null)
                {
                    generations.UnionWith(map.Keys);
                }
            }

            foreach (var generation in generations)
            {
                var have = 0;
                owned?.TryGetValue(generation, out have);

                var housed = HousedOf(generation);

                while (housed > Math.Max(0, have))
                {
                    var fullest = -1;
                    var most = 0;

                    for (var index = 0; index < kinds.Length; index++)
                    {
                        if (kinds[index] != null && kinds[index].TryGetValue(generation, out var count)
                            && count > most)
                        {
                            fullest = index;
                            most = count;
                        }
                    }

                    if (fullest < 0)
                    {
                        break;
                    }

                    RemoveKind(fullest, generation);
                    accelerators[fullest]--;
                    housed--;
                }
            }

            // ---- 3. a card nobody recorded gets a name, newest first ---------------------------
            //
            // **Never a new card.** A v61 room and a room filled by a tool hold cards with no
            // generation; they are matched against what is owned and not yet in a cabinet, and a
            // card with nothing left to match leaves the floor, because the company does not own it.
            var spare = new List<(HardwareGenerationId Generation, int Count)>();

            if (owned != null)
            {
                foreach (var pair in owned)
                {
                    var left = pair.Value - HousedOf(pair.Key);

                    if (left > 0)
                    {
                        spare.Add((pair.Key, left));
                    }
                }
            }

            spare.Sort((left, right) => Strength(right.Generation).CompareTo(Strength(left.Generation)));

            for (var index = 0; index < accelerators.Length; index++)
            {
                var unknown = UnknownIn(index);

                while (unknown > 0)
                {
                    var at = spare.FindIndex(line => line.Count > 0);

                    if (at < 0)
                    {
                        accelerators[index]--;
                    }
                    else
                    {
                        AddKind(index, spare[at].Generation);
                        spare[at] = (spare[at].Generation, spare[at].Count - 1);
                    }

                    unknown--;
                }
            }

            return HousedAccelerators;
        }

        /// <summary>
        /// Stocks a room with anonymous cards the old way: trims, then spreads whatever is left over
        /// across the free slots in proportion. **For tests and tools only.** The game never calls
        /// it, because a room that fills itself is a room the player cannot arrange, and that was
        /// reported three times before the pass that did it was taken out of
        /// <see cref="Stock(IReadOnlyDictionary{HardwareGenerationId,int})"/>.
        /// </summary>
        public int Fill(int available)
        {
            var wanted = Math.Max(0, available);

            // ---- 1. nothing may stand where it cannot ----------------------------------------
            for (var index = 0; index < accelerators.Length; index++)
            {
                while (accelerators[index] > CardCapacity(index))
                {
                    RemoveAny(index);
                }
            }

            // ---- 2. nothing may stand that is not owned --------------------------------------
            var housed = HousedAccelerators;

            while (housed > wanted)
            {
                var fullest = -1;

                for (var index = 0; index < accelerators.Length; index++)
                {
                    if (accelerators[index] > 0
                        && (fullest < 0 || accelerators[index] > accelerators[fullest]))
                    {
                        fullest = index;
                    }
                }

                if (fullest < 0)
                {
                    break;
                }

                RemoveAny(fullest);
                housed--;
            }

            // ---- 3. and whatever is owned and homeless goes in ---------------------------------
            var free = 0;

            for (var index = 0; index < racks.Length; index++)
            {
                free += CardCapacity(index) - accelerators[index];
            }

            var spare = Math.Min(wanted - housed, free);

            if (spare <= 0)
            {
                return housed;
            }

            // Spread in proportion to the room each cabinet has left rather than filling the first
            // one until it cooks. A room that crams everything into rack one and leaves rack two
            // empty is not a room anybody runs, and it made the rack choice invisible: the heat
            // landed wherever the array happened to start.
            var placed = 0;

            for (var index = 0; index < racks.Length && free > 0; index++)
            {
                var room = CardCapacity(index) - accelerators[index];

                if (room <= 0)
                {
                    continue;
                }

                var share = (int)((long)room * spare / free);

                accelerators[index] += share;
                placed += share;
            }

            // Whatever the division left over, one at a time into whatever still has room.
            for (var index = 0; index < racks.Length && placed < spare; index++)
            {
                if (accelerators[index] >= CardCapacity(index))
                {
                    continue;
                }

                accelerators[index]++;
                placed++;
            }

            return housed + placed;
        }

        /// <summary>
        /// Puts one card of this generation into one cabinet, from the store.
        ///
        /// **It never creates a card and it never moves one.** The caller says how many of this
        /// generation are owned and not yet in a cabinet; with none, the answer is no. Taking a card
        /// out of another cabinet behind the player's back was the old behaviour, and a click that
        /// rearranges a different cabinet is a click nobody can predict.
        /// </summary>
        public bool TryFitCard(int column, int row, HardwareGenerationId generation, int inStore,
            out string failureReason)
        {
            failureReason = string.Empty;

            if (!Contains(column, row) || racks[IndexOf(column, row)] == ServerRack.None)
            {
                failureReason = Loc.T("room.no_rack_here");
                return false;
            }

            if (FreeSlots(column, row) <= 0)
            {
                failureReason = Loc.T("rack.full");
                return false;
            }

            if (inStore <= 0)
            {
                failureReason = Loc.T("rack.nothing_owned");
                return false;
            }

            var index = IndexOf(column, row);
            AddKind(index, generation);
            accelerators[index]++;
            return true;
        }

        /// <summary>
        /// How many accelerators one cabinet can hold today, fans included in the arithmetic.
        ///
        /// **This is the slot a fan costs, and for a while nothing charged it.**
        /// <see cref="FreeSlots"/> subtracted the fans, so the cabinet panel told the player a
        /// fan takes a slot; <see cref="Stock"/> and <see cref="TotalSlots"/> did not, so the
        /// daily refill put a full set of cards back in around it. The trade the whole cooling
        /// mechanic stands on, one card for one fan, was therefore never actually made: a fan
        /// was more cooling for no cards, and the catalog table promising "3 cards + 1 fan" was
        /// describing a game the code was not playing.
        /// </summary>
        private int CardCapacity(int index)
        {
            if (racks[index] == ServerRack.None)
            {
                return 0;
            }

            return Math.Max(0,
                ServerRackCatalog.Get(racks[index]).Slots
                - fans[index] * ServerRackCatalog.FanSlots);
        }

        /// <summary>Every slot on the floor a card could stand in. See <see cref="CardCapacity"/>.</summary>
        public int TotalSlots
        {
            get
            {
                var slots = 0;
                for (var index = 0; index < racks.Length; index++)
                {
                    slots += CardCapacity(index);
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
                return total + FanCount * ServerRackCatalog.FanMonthlyUpkeepUsd
                       + CoolerCount * ServerRackCatalog.RoomCoolerMonthlyUpkeepUsd;
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

            // **The room first, then each cabinet in it.** Every cabinet sheds into the same air,
            // so how warm that air is decides how well each of them can shed at all.
            var climate = Climate(perUnitHeat, room);

            var petaflops = 0.0;

            // Every rack on the floor draws its idle, stocked or not. Fans and pumps do not care
            // whether anything is plugged in, and a hall full of empty immersion tanks is a bill.
            // The room coolers are on the same bill.
            var draw = IdleDrawKilowatts + CoolerCount * ServerRackCatalog.RoomCoolerDrawKilowatts;
            var throttled = 0;

            for (var index = 0; index < racks.Length; index++)
            {
                if (racks[index] == ServerRack.None || accelerators[index] <= 0)
                {
                    continue;
                }

                var definition = ServerRackCatalog.Get(racks[index]);
                var level = climate.OverclocksRunning ? overclock[index] : 0;
                var (rawPetaflops, rawKilowatts) = Load(index, perUnit, perUnitHeat);

                // An overclock buys work with heat, and the heat is the bill as well as the risk.
                var heat = rawKilowatts * (1.0 + ServerRackCatalog.OverclockHeatPerLevel * level);

                // **Fans raise the cabinet's rating rather than lowering the heat**, which is the
                // honest shape: air moves warmth out of the box, it does not make the silicon draw
                // less. The bill goes up either way, and that is the cost of the fix.
                //
                // Airflow modelling adds to this for every cabinet at once, and liquid loops
                // flatten the curve past it for immersion tanks alone. Both come in through
                // `RoomUpgrades` so the hall goes on knowing nothing about the research tree. The
                // room's own heat comes in through the climate: a hot room is warm air going in.
                var cooling = room.CoolingFor(definition, fans[index]) * climate.CabinetFactor;

                var factor = ServerRackCatalog.ThrottleFactor(
                    heat, cooling, room.PenaltyFor(racks[index]));
                if (factor < 1.0)
                {
                    throttled++;
                }

                petaflops += rawPetaflops * factor
                             * (1.0 + ServerRackCatalog.OverclockThroughputPerLevel * level);

                // The power is drawn whether or not the work gets done, which is the whole cost of
                // getting this wrong: the bill is for the heat, and the output is not. The fans are
                // on the same bill.
                draw += heat + fans[index] * ServerRackCatalog.FanDrawKilowatts;
            }

            return new HallOutput(petaflops, draw, throttled, climate);
        }

        /// <summary>
        /// What one cabinet is delivering, with the room and its overclock counted. The cabinet
        /// panel quotes this as users, so it has to be the same arithmetic as <see cref="Output"/>.
        /// </summary>
        public double CabinetPetaflops(int column, int row, double petaflopsPerAccelerator,
            double kilowattsPerAccelerator, RoomUpgrades? upgrades = null)
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

            var room = upgrades ?? RoomUpgrades.None;
            var climate = Climate(kilowattsPerAccelerator, room);
            var level = climate.OverclocksRunning ? overclock[index] : 0;
            var (rawPetaflops, rawKilowatts) = Load(index, petaflopsPerAccelerator, kilowattsPerAccelerator);

            var heat = rawKilowatts * (1.0 + ServerRackCatalog.OverclockHeatPerLevel * level);
            var cooling = room.CoolingFor(ServerRackCatalog.Get(racks[index]), fans[index])
                          * climate.CabinetFactor;
            var factor = ServerRackCatalog.ThrottleFactor(heat, cooling, room.PenaltyFor(racks[index]));

            return rawPetaflops * factor * (1.0 + ServerRackCatalog.OverclockThroughputPerLevel * level);
        }

        /// <summary>
        /// The room's heat against what it can shed.
        ///
        /// **Overclocks are counted and then, if the room cannot take them, switched off.** Two
        /// passes rather than a loop: an overclock that tips the room over its budget is the one
        /// thing the room refuses, and a room without overclocks is the only honest fallback.
        /// </summary>
        public RoomClimate Climate(double kilowattsPerAccelerator, RoomUpgrades? upgrades = null)
        {
            var perUnitHeat = Math.Max(0.0, SimUnits.Finite(kilowattsPerAccelerator));
            var cooling = ServerRackCatalog.BasementPassiveCoolingKilowatts
                          + CoolerCount * ServerRackCatalog.RoomCoolerCoolingKilowatts;

            var withOverclock = RoomHeat(perUnitHeat, true);

            if (withOverclock <= cooling)
            {
                return new RoomClimate(withOverclock, cooling, true);
            }

            return new RoomClimate(RoomHeat(perUnitHeat, false), cooling, false);
        }

        /// <summary>Everything the cabinets put into the room's air.</summary>
        private double RoomHeat(double perUnitHeat, bool overclocks)
        {
            var heat = IdleDrawKilowatts;

            for (var index = 0; index < racks.Length; index++)
            {
                if (racks[index] == ServerRack.None)
                {
                    continue;
                }

                var level = overclocks ? overclock[index] : 0;

                heat += Load(index, 0.0, perUnitHeat).Kilowatts
                        * (1.0 + ServerRackCatalog.OverclockHeatPerLevel * level)
                        + fans[index] * ServerRackCatalog.FanDrawKilowatts;
            }

            return heat;
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
                kinds[index] = null;
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

        /// <summary>
        /// Which cards stand where: flattened triples of square, generation and count. v62.
        /// </summary>
        public void CaptureCards(List<int> into)
        {
            into.Clear();

            for (var index = 0; index < kinds.Length; index++)
            {
                if (kinds[index] == null)
                {
                    continue;
                }

                foreach (var pair in kinds[index])
                {
                    if (pair.Value <= 0)
                    {
                        continue;
                    }

                    into.Add(index);
                    into.Add((int)pair.Key);
                    into.Add(pair.Value);
                }
            }
        }

        /// <summary>
        /// Restores which cards stand where, after the counts. A generation that does not exist, a
        /// square off the floor, or more named cards than the cabinet holds are dropped rather than
        /// trusted: whatever is left unnamed is named again by the next stock pass.
        /// </summary>
        public void RestoreCards(IReadOnlyList<int> saved)
        {
            for (var index = 0; index < kinds.Length; index++)
            {
                kinds[index] = null;
            }

            if (saved == null)
            {
                return;
            }

            for (var at = 0; at + 2 < saved.Count; at += 3)
            {
                var index = saved[at];
                var generation = saved[at + 1];
                var count = saved[at + 2];

                if (index < 0 || index >= kinds.Length || count <= 0
                    || racks[index] == ServerRack.None
                    || !Enum.IsDefined(typeof(HardwareGenerationId), generation))
                {
                    continue;
                }

                var room = accelerators[index] - KnownIn(index);

                if (room > 0)
                {
                    AddKind(index, (HardwareGenerationId)generation, Math.Min(count, room));
                }
            }
        }

        /// <summary>Coolers and overclocks, the two things v61 added to the floor.</summary>
        public void CaptureRoom(List<int> intoCoolers, List<int> intoOverclock)
        {
            intoCoolers.Clear();
            intoOverclock.Clear();

            for (var index = 0; index < racks.Length; index++)
            {
                intoCoolers.Add(coolers[index] ? 1 : 0);
                intoOverclock.Add(overclock[index]);
            }
        }

        /// <summary>
        /// Restores coolers and overclocks after the racks. A cooler whose squares are not both free
        /// is dropped rather than overlapping a cabinet, and an overclock is clamped to the levels
        /// that exist: a corrupt or edited file must never crash the room.
        /// </summary>
        public void RestoreRoom(IReadOnlyList<int> savedCoolers, IReadOnlyList<int> savedOverclock)
        {
            for (var index = 0; index < racks.Length; index++)
            {
                coolers[index] = false;
                overclock[index] = 0;
            }

            for (var index = 0; index < racks.Length; index++)
            {
                if (savedOverclock != null && index < savedOverclock.Count
                    && racks[index] != ServerRack.None)
                {
                    overclock[index] = Math.Clamp(savedOverclock[index], 0,
                        ServerRackCatalog.OverclockLevels);
                }

                if (savedCoolers == null || index >= savedCoolers.Count || savedCoolers[index] != 1)
                {
                    continue;
                }

                TryPlaceCooler(index % Columns, index / Columns, out _);
            }
        }

        public void Clear()
        {
            for (var index = 0; index < racks.Length; index++)
            {
                racks[index] = ServerRack.None;
                accelerators[index] = 0;
                kinds[index] = null;
                fans[index] = 0;
                coolers[index] = false;
                overclock[index] = 0;
            }
        }
    }

    /// <summary>
    /// The room's heat against what it can shed. A snapshot, for the rules and for the screen.
    /// </summary>
    public readonly struct RoomClimate
    {
        public RoomClimate(double heatKilowatts, double coolingKilowatts, bool overclocksRunning)
        {
            HeatKilowatts = Math.Max(0.0, SimUnits.Finite(heatKilowatts));
            CoolingKilowatts = Math.Max(0.1, SimUnits.Finite(coolingKilowatts, 0.1));
            OverclocksRunning = overclocksRunning;
        }

        public double HeatKilowatts { get; }
        public double CoolingKilowatts { get; }

        /// <summary>False when the room could not take the overclocks and they were switched off.</summary>
        public bool OverclocksRunning { get; }

        public double Ratio => HeatKilowatts / Math.Max(0.1, CoolingKilowatts);

        public ServerRackCatalog.RoomClimateState State => ServerRackCatalog.ClimateOf(Ratio);

        /// <summary>How much of its own rating each cabinet can shed in air this warm.</summary>
        public double CabinetFactor => ServerRackCatalog.RoomCoolingFactor(Ratio);
    }

    /// <summary>What a hall is delivering right now.</summary>
    public readonly struct HallOutput
    {
        public HallOutput(double petaflops, double drawKilowatts, int throttledRacks,
            RoomClimate climate = default)
        {
            Petaflops = Math.Max(0.0, SimUnits.Finite(petaflops));
            DrawKilowatts = Math.Max(0.0, SimUnits.Finite(drawKilowatts));
            ThrottledRacks = Math.Max(0, throttledRacks);
            Climate = climate;
        }

        /// <summary>The room these figures were worked out in.</summary>
        public RoomClimate Climate { get; }

        public double Petaflops { get; }
        public double DrawKilowatts { get; }

        /// <summary>How many racks are running hot. The number the hall screen warns on.</summary>
        public int ThrottledRacks { get; }

        public bool IsHealthy => ThrottledRacks == 0;
    }
}
