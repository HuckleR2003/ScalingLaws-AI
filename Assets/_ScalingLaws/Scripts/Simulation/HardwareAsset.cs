using System;
using ScalingLaws.Core;
using ScalingLaws.Data;

namespace ScalingLaws.Simulation
{
    /// <summary>
    /// A batch of identical hardware the company bought on one day at one price.
    ///
    /// The purchase date is the field that matters. It is what depreciation is measured from, and it
    /// is the difference between a cluster that paid for itself and a cluster that ate the round.
    /// </summary>
    public readonly struct HardwareAsset
    {
        public HardwareAsset(
            HardwareGenerationId generationId,
            ComputeTier tier,
            int units,
            GameDate purchaseDate,
            long purchasePricePerUnitUsd,
            int leadTimeDays)
        {
            GenerationId = generationId;
            Tier = tier;
            Units = Math.Clamp(units, 0, 10_000_000);
            PurchaseDate = purchaseDate;
            PurchasePricePerUnitUsd = Math.Clamp(purchasePricePerUnitUsd, 0L, 10_000_000L);
            CommissionDate = purchaseDate.AddDays(Math.Clamp(leadTimeDays, 0, 1500));
        }

        public HardwareGenerationId GenerationId { get; }
        public ComputeTier Tier { get; }
        public int Units { get; }
        public GameDate PurchaseDate { get; }

        /// <summary>Price actually paid per unit, after any tier discount. Not the catalog list price.</summary>
        public long PurchasePricePerUnitUsd { get; }

        /// <summary>The day it starts producing FLOPs. Money leaves on purchase, work starts here.</summary>
        public GameDate CommissionDate { get; }

        public long TotalPurchasePriceUsd => PurchasePricePerUnitUsd * Units;

        public bool IsOnline(GameDate date) => date.IsOnOrAfter(CommissionDate) && Units > 0;

        public bool IsInTransit(GameDate date) => date.IsBefore(CommissionDate) && Units > 0;

        public int DaysUntilOnline(GameDate date) => Math.Max(0, CommissionDate.DayIndex - date.DayIndex);

        /// <summary>Same batch, fewer units. Used when part of a batch is sold off.</summary>
        public HardwareAsset WithUnits(int units)
        {
            return new HardwareAsset(
                GenerationId,
                Tier,
                units,
                PurchaseDate,
                PurchasePricePerUnitUsd,
                CommissionDate.DayIndex - PurchaseDate.DayIndex);
        }

        public override string ToString() => $"{Units}x {GenerationId} bought {PurchaseDate} in {Tier}";
    }
}
