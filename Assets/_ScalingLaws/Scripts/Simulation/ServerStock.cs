using System;
using System.Collections.Generic;
using ScalingLaws.Data;

namespace ScalingLaws.Simulation
{
    /// <summary>
    /// Cabinets and fans the company owns and has not stood up.
    ///
    /// **Buying and placing are two decisions and they were one.** `TryPlaceRack` charged for a
    /// cabinet and put it on a square in the same call, so there was no way to own a rack without
    /// standing it somewhere, no way to take one off the floor without destroying it, and no way to
    /// change your mind between the two. `DecorPlan` has had exactly this split since the office
    /// furniture was built, for exactly this reason: a floor that is full is a real constraint, and
    /// one flag cannot say both "paid for" and "standing there".
    ///
    /// **What the player carries on the cursor is a stock item, not a third state.** The interface
    /// singles one out of the warehouse while the player looks for a square; putting it down takes
    /// it from here, and cancelling puts nothing back because it never left. So a player who quits
    /// with a rack on the cursor finds it in the warehouse, which is the only honest reading, and
    /// the save needs no field for a hand.
    ///
    /// Pure: no UnityEngine, so the whole flow is testable without a basement to look at.
    /// </summary>
    public sealed class ServerStock
    {
        private readonly Dictionary<ServerRack, int> racks = new();

        /// <summary>
        /// Loose fans, not attached to any cabinet.
        ///
        /// A fan comes off with the rack it was fitted to. Destroying it on the way to the warehouse
        /// would make moving a cabinet cost money, and the one thing a build mode has to be is a
        /// place the player can change their mind cheaply.
        /// </summary>
        public int Fans { get; private set; }

        /// <summary>How many of one kind are in the warehouse.</summary>
        public int CountOf(ServerRack rack) =>
            rack != ServerRack.None && racks.TryGetValue(rack, out var count) ? count : 0;

        /// <summary>Every kind with at least one in stock, catalog order.</summary>
        public List<ServerRack> Kinds()
        {
            var found = new List<ServerRack>();

            foreach (var definition in ServerRackCatalog.All)
            {
                if (CountOf(definition.Id) > 0)
                {
                    found.Add(definition.Id);
                }
            }

            return found;
        }

        public int RackCount
        {
            get
            {
                var total = 0;

                foreach (var count in racks.Values)
                {
                    total += count;
                }

                return total;
            }
        }

        public bool IsEmpty => RackCount == 0 && Fans == 0;

        /// <summary>
        /// What the warehouse is holding, at list price.
        ///
        /// Shown so a player can see that a floor with four squares free and $300,000 of cabinets in
        /// boxes is a decision they made rather than a bug.
        /// </summary>
        public long ValueUsd
        {
            get
            {
                var total = 0L;

                foreach (var pair in racks)
                {
                    total += ServerRackCatalog.Get(pair.Key).PriceUsd * pair.Value;
                }

                return total + (long)Fans * ServerRackCatalog.FanPriceUsd;
            }
        }

        // ---- in and out -------------------------------------------------------------------------

        /// <summary>Puts a cabinet in. Money moves in <c>CompanySimulation</c>, never here.</summary>
        public void Add(ServerRack rack, int count = 1)
        {
            if (rack == ServerRack.None || count <= 0)
            {
                return;
            }

            racks[rack] = CountOf(rack) + count;
        }

        public void AddFans(int count)
        {
            if (count <= 0)
            {
                return;
            }

            Fans += count;
        }

        /// <summary>Takes one out, or says there was none. Never goes negative.</summary>
        public bool TryTake(ServerRack rack)
        {
            var held = CountOf(rack);

            if (held <= 0)
            {
                return false;
            }

            if (held == 1)
            {
                racks.Remove(rack);
            }
            else
            {
                racks[rack] = held - 1;
            }

            return true;
        }

        public bool TryTakeFan()
        {
            if (Fans <= 0)
            {
                return false;
            }

            Fans--;
            return true;
        }

        // ---- persistence ------------------------------------------------------------------------
        //
        // Written as a pair of parallel lists rather than a dictionary, because that is the shape
        // the save format already uses for the hall and JsonUtility cannot serialise a dictionary.

        public void Capture(List<int> intoKinds, List<int> intoCounts, out int intoFans)
        {
            intoKinds.Clear();
            intoCounts.Clear();
            intoFans = Fans;

            foreach (var definition in ServerRackCatalog.All)
            {
                var count = CountOf(definition.Id);

                if (count <= 0)
                {
                    continue;
                }

                intoKinds.Add((int)definition.Id);
                intoCounts.Add(count);
            }
        }

        public void Restore(IReadOnlyList<int> savedKinds, IReadOnlyList<int> savedCounts,
            int savedFans)
        {
            racks.Clear();
            Fans = Math.Max(0, savedFans);

            if (savedKinds == null || savedCounts == null)
            {
                return;
            }

            for (var index = 0; index < savedKinds.Count && index < savedCounts.Count; index++)
            {
                if (!Enum.IsDefined(typeof(ServerRack), savedKinds[index]))
                {
                    continue;
                }

                Add((ServerRack)savedKinds[index], Math.Max(0, savedCounts[index]));
            }
        }

        public void Clear()
        {
            racks.Clear();
            Fans = 0;
        }
    }
}
