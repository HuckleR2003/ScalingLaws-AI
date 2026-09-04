using System;
using System.Collections.Generic;
using System.Linq;
using ScalingLaws.Data;

namespace ScalingLaws.Simulation
{
    /// <summary>
    /// One piece the company owns, and where it stands.
    ///
    /// Bought and placed are separate states on purpose. Buying something is spending money; putting
    /// it on the floor is deciding the office has room for it, and a floor that is full is a real
    /// constraint that a single "owned" flag could not express.
    /// </summary>
    public sealed class DecorItem
    {
        public DecorItem(FurnitureKind kind, float x, float z, bool placed)
        {
            Kind = kind;
            X = x;
            Z = z;
            IsPlaced = placed;
        }

        public FurnitureKind Kind { get; }

        /// <summary>Where it stands on the floor, in room metres. Meaningless while stored.</summary>
        public float X { get; private set; }
        public float Z { get; private set; }

        public bool IsPlaced { get; private set; }

        public FurniturePiece Definition => FurnitureCatalog.Get(Kind);

        internal void PlaceAt(float x, float z)
        {
            X = x;
            Z = z;
            IsPlaced = true;
        }

        internal void Store()
        {
            IsPlaced = false;
        }
    }

    /// <summary>
    /// Everything the company has bought for the office, and what standing it up is worth.
    ///
    /// **Only placed pieces count.** A sofa in a box raises nobody's morale, and letting stored
    /// stock pay out would make the floor plan decorative in the literal sense — the player would
    /// buy and never place. Placing is the point of the feature, so placing is what pays.
    ///
    /// Pure: no UnityEngine, so the arithmetic is testable without a scene.
    /// </summary>
    public sealed class DecorPlan
    {
        /// <summary>
        /// How the floor is divided when the game picks a spot.
        ///
        /// Placement is automatic — the player clicks PLACE and the piece goes to the first free
        /// slot. Dragging a box around a render texture is a different feature and a much larger
        /// one; this gets furniture into the room today and can be replaced by dragging later
        /// without any of the economics changing.
        /// </summary>
        public const float SlotSpacing = 2.0f;

        /// <summary>Metres kept clear of the zone's edges, so nothing straddles a boundary.</summary>
        public const float WallMargin = 0.6f;

        private readonly List<DecorItem> items = new();

        public IReadOnlyList<DecorItem> Items => items;

        public IEnumerable<DecorItem> Placed => items.Where(item => item.IsPlaced);

        public IEnumerable<DecorItem> Stored => items.Where(item => !item.IsPlaced);

        /// <summary>
        /// The piece added most recently, or null.
        ///
        /// The shop needs to pick up what it just bought, and `TryBuyFurniture` answers with a
        /// refusal rather than with the item. Reading the end of the list is honest here: `Buy` is
        /// the only thing that appends, and it appends exactly one.
        /// </summary>
        public DecorItem Newest => items.Count == 0 ? null : items[items.Count - 1];

        /// <summary>What the whole collection cost, at list price. Shown so the player can see it.</summary>
        public double InvestedUsd => items.Sum(item => item.Definition.PriceUsd);

        /// <summary>Extra seats from placed desks. Stored desks seat nobody.</summary>
        public int ExtraDesks => Placed.Sum(item => item.Definition.DeskSeats);

        /// <summary>Morale added by the floor, capped.</summary>
        public double MoraleBonus => Math.Min(FurnitureCatalog.MoraleCeiling,
            Placed.Sum(item => item.Definition.MoraleBonus));

        /// <summary>Research rate added by the floor, capped.</summary>
        public double ResearchBonus => Math.Min(FurnitureCatalog.ResearchCeiling,
            Placed.Sum(item => item.Definition.ResearchBonus));

        /// <summary>How many pieces of one kind are owned. The shop shows it so repeats are obvious.</summary>
        public int CountOf(FurnitureKind kind) => items.Count(item => item.Kind == kind);

        /// <summary>
        /// Buys a piece and stands it up straight away.
        ///
        /// One click rather than two, because the player asked for it: buying something and then
        /// having to find it in a list to put it in the room is two decisions where there was one.
        /// It can still be stored afterwards.
        /// </summary>
        public DecorItem Buy(FurnitureKind kind, DecorZone zone)
        {
            var item = new DecorItem(kind, 0f, 0f, false);
            items.Add(item);
            Place(item, zone);
            return item;
        }

        /// <summary>
        /// Stands a piece in the first free slot, or leaves it stored when the floor is full.
        ///
        /// Returns false when there was nowhere to put it, which is what the shop reports rather
        /// than taking the money and losing the object.
        /// </summary>
        public bool Place(DecorItem item, DecorZone zone)
        {
            if (item == null || !items.Contains(item))
            {
                return false;
            }

            foreach (var slot in FreeSlots(zone))
            {
                item.PlaceAt(slot.x, slot.z);
                return true;
            }

            item.Store();
            return false;
        }

        /// <summary>
        /// Every slot in a zone, taken or not, in the order the floor fills.
        ///
        /// The build mode draws these as the places a piece can go, so it needs the whole grid and
        /// not the gaps in it. Same walk as `FreeSlots` and deliberately so: two orders would mean
        /// the square the player clicks and the square the plan fills are different squares.
        /// </summary>
        public IEnumerable<(float x, float z)> AllSlots(DecorZone zone)
        {
            for (var z = zone.Z + WallMargin; z <= zone.Z + zone.Depth - WallMargin; z += SlotSpacing)
            {
                for (var x = zone.X + WallMargin; x <= zone.X + zone.Width - WallMargin; x += SlotSpacing)
                {
                    yield return (x, z);
                }
            }
        }

        /// <summary>
        /// What is standing on this slot, or null.
        ///
        /// Matched on position within a hundredth of a metre, which is the same tolerance
        /// `FreeSlots` uses to decide a slot is taken. One tolerance, or a slot can be occupied for
        /// one of them and free for the other.
        /// </summary>
        public DecorItem At(float x, float z)
        {
            foreach (var item in items)
            {
                if (item.IsPlaced && Math.Abs(item.X - x) < 0.01f && Math.Abs(item.Z - z) < 0.01f)
                {
                    return item;
                }
            }

            return null;
        }

        /// <summary>
        /// Stands a piece on one particular slot.
        ///
        /// **The build mode's placement, beside the furnished move's.** `Place` takes the first free
        /// slot because six pieces arriving at once are not being positioned by anybody; this takes
        /// the slot the player pointed at. Refuses an occupied slot rather than stacking, which is
        /// the failure the plan has no way to represent: two items at one position are two items the
        /// scene draws inside each other.
        /// </summary>
        public bool PlaceOn(DecorItem item, float x, float z)
        {
            if (item == null || !items.Contains(item) || At(x, z) != null)
            {
                return false;
            }

            item.PlaceAt(x, z);
            return true;
        }

        /// <summary>Takes a piece off the floor without selling it. The money stays spent.</summary>
        public void Store(DecorItem item)
        {
            if (item != null && items.Contains(item))
            {
                item.Store();
            }
        }

        /// <summary>
        /// Sells a piece back and returns what it fetched.
        ///
        /// Zero when the item is not owned, so a double click on SELL cannot mint money out of an
        /// object that has already gone.
        /// </summary>
        public double Sell(DecorItem item)
        {
            if (item == null || !items.Remove(item))
            {
                return 0.0;
            }

            return item.Definition.ResaleValueUsd;
        }

        /// <summary>
        /// The grid positions nothing is standing on yet.
        ///
        /// Walked in room order — along the front of the floor first, then back — so the office
        /// fills up from the open space near the camera rather than from a corner the player cannot
        /// see. A piece placed where it is invisible reads as a piece that was never bought.
        /// </summary>
        private IEnumerable<(float x, float z)> FreeSlots(DecorZone zone)
        {
            var taken = Placed.Select(item => (item.X, item.Z)).ToList();

            for (var z = zone.Z + WallMargin; z <= zone.Z + zone.Depth - WallMargin; z += SlotSpacing)
            {
                for (var x = zone.X + WallMargin; x <= zone.X + zone.Width - WallMargin; x += SlotSpacing)
                {
                    var here = (x, z);
                    if (!taken.Any(spot => Math.Abs(spot.X - x) < 0.01f
                        && Math.Abs(spot.Z - z) < 0.01f))
                    {
                        yield return here;
                    }
                }
            }
        }

        /// <summary>Rebuilds a plan from a save. Positions are trusted as written.</summary>
        public static DecorPlan Restore(IEnumerable<(FurnitureKind kind, float x, float z, bool placed)> saved)
        {
            var plan = new DecorPlan();
            if (saved == null)
            {
                return plan;
            }

            foreach (var (kind, x, z, placed) in saved)
            {
                plan.items.Add(new DecorItem(kind, x, z, placed));
            }

            return plan;
        }
    }
}
