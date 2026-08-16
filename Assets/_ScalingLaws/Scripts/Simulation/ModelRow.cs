using ScalingLaws.Data;

namespace ScalingLaws.Simulation
{
    /// <summary>
    /// One live model, as the model dashboard reads it.
    ///
    /// A view rather than a thing the simulation owns: every figure on it is derived from the
    /// deployed model and the market split at the moment it was asked for. Nothing here is stored,
    /// so nothing here can drift out of step with the ledger.
    /// </summary>
    public readonly struct ModelRow
    {
        public ModelRow(string name, ModelType type, double capability, double users,
            double subscribers, long monthEarningsUsd, int daysOnSale)
        {
            Name = name ?? string.Empty;
            Type = type;
            Capability = capability;
            Users = users;
            Subscribers = subscribers;
            MonthEarningsUsd = monthEarningsUsd;
            DaysOnSale = daysOnSale;
        }

        public string Name { get; }
        public ModelType Type { get; }
        public double Capability { get; }

        /// <summary>People using this one, this company's share of them.</summary>
        public double Users { get; }

        /// <summary>Of those, the ones who pay.</summary>
        public double Subscribers { get; }

        /// <summary>What it has earned this calendar month.</summary>
        public long MonthEarningsUsd { get; }

        public int DaysOnSale { get; }
    }
}
