using System;
using ScalingLaws.Core;

namespace ScalingLaws.Simulation
{
    /// <summary>
    /// What the site would be drawing if one more order went through, against what it supplies.
    ///
    /// **One reading, three readers.** The purchase rule refuses on it, the shop prints it under
    /// the price and the fleet card prints it before the click. Until this existed the only place
    /// the ceiling was ever mentioned at the moment of buying was the refusal itself, and the probe
    /// found an owning company stopping dead at 2,500 kW for eleven years with nothing on any
    /// screen that said a ceiling was coming.
    ///
    /// Counts what is already on its way. Cards still in the crates draw nothing today and all of
    /// their power the day they land, so a check that read only the running fleet let a company
    /// order past the contract for as long as the lead time lasted and find out on arrival.
    /// </summary>
    public readonly struct SitePowerOutlook
    {
        /// <summary>
        /// Where the fleet screen already turns the power line amber, and where an order now says
        /// so. Four fifths is the point at which the next batch is the one that gets turned down.
        /// </summary>
        public const double NearlyFullShare = 0.80;

        public SitePowerOutlook(double drawBeforeKilowatts, double drawAfterKilowatts, double capacityKilowatts)
        {
            DrawBeforeKilowatts = Math.Max(0.0, SimUnits.Finite(drawBeforeKilowatts));
            DrawAfterKilowatts = Math.Max(0.0, SimUnits.Finite(drawAfterKilowatts));
            CapacityKilowatts = Math.Max(0.0, SimUnits.Finite(capacityKilowatts));
        }

        /// <summary>Running now plus already ordered, before this order.</summary>
        public double DrawBeforeKilowatts { get; }

        /// <summary>The same with this order added.</summary>
        public double DrawAfterKilowatts { get; }

        /// <summary>What the site supplies once this order's tier is on it.</summary>
        public double CapacityKilowatts { get; }

        public double ShareBefore => Share(DrawBeforeKilowatts);

        public double ShareAfter => Share(DrawAfterKilowatts);

        /// <summary>Whether the order fits at all. The purchase is refused when it does not.</summary>
        public bool Fits => DrawAfterKilowatts <= CapacityKilowatts;

        /// <summary>Fits, and leaves the site at four fifths or more.</summary>
        public bool IsNearlyFull => Fits && ShareAfter >= NearlyFullShare;

        /// <summary>This order is the one that takes the site past four fifths.</summary>
        public bool CrossesIntoNearlyFull => IsNearlyFull && ShareBefore < NearlyFullShare;

        private double Share(double draw) => CapacityKilowatts <= 0.0
            ? draw > 0.0 ? double.PositiveInfinity : 0.0
            : draw / CapacityKilowatts;
    }
}
