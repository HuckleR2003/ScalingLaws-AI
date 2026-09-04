using System;
using ScalingLaws.Data;

namespace ScalingLaws.Simulation
{
    /// <summary>
    /// What the company owns, as opposed to what it is worth.
    ///
    /// **Two different numbers, and the bank page has to show both without pretending they are one.**
    /// The valuation is what an investor would price the company at: a function of capability, of
    /// how far the frontier is ahead, and of the revenue run rate. The book value is the sum of the
    /// things in the building. A young lab with one good model is worth far more than its parts; a
    /// company with a warehouse of ageing accelerators and no product is worth far less.
    ///
    /// Adding the rows up and calling the answer the valuation would be the "two readings of one
    /// number" fault this project has now fixed six times. They are both on the page, side by side,
    /// and the gap between them is the interesting part.
    ///
    /// Derived on read, never stored. Every figure already exists somewhere else and this only
    /// gathers them, so it cannot drift from the ledger that produced them.
    /// </summary>
    public readonly struct AssetSheet
    {
        public AssetSheet(long cashUsd, long hardwareUsd, long propertyUsd, long furnitureUsd)
        {
            CashUsd = Math.Max(0L, cashUsd);
            HardwareUsd = Math.Max(0L, hardwareUsd);
            PropertyUsd = Math.Max(0L, propertyUsd);
            FurnitureUsd = Math.Max(0L, furnitureUsd);
        }

        /// <summary>Money in the bank. The one asset that is worth exactly what it says.</summary>
        public long CashUsd { get; }

        /// <summary>
        /// The fleet at what it would fetch today, not at what it cost.
        ///
        /// Accelerators lose roughly a quarter of their value with every successor launch, which is
        /// the spine of this game, so a book value at purchase price would be the one place in it
        /// where hardware does not age.
        /// </summary>
        public long HardwareUsd { get; }

        /// <summary>
        /// Buildings the company owns outright, at what was paid for them.
        ///
        /// Only the ones it owns: a rented office is somebody else's asset, and counting a lease as
        /// property is how a balance sheet stops meaning anything. The basement counts, because it
        /// was bought and it is a place.
        /// </summary>
        public long PropertyUsd { get; }

        /// <summary>Furniture, at resale rather than at purchase, for the reason hardware is.</summary>
        public long FurnitureUsd { get; }

        /// <summary>Everything the company owns. Not the valuation; see the note on the type.</summary>
        public long BookValueUsd => CashUsd + HardwareUsd + PropertyUsd + FurnitureUsd;

        /// <summary>
        /// How much of the valuation is things rather than promise.
        ///
        /// Below one for almost every healthy company in this game, and the number climbing toward
        /// one is a company whose story has stopped being worth more than its warehouse.
        /// </summary>
        public double BackedShare(long valuationUsd) =>
            valuationUsd <= 0L ? 0.0 : Math.Clamp(BookValueUsd / (double)valuationUsd, 0.0, 4.0);
    }
}
