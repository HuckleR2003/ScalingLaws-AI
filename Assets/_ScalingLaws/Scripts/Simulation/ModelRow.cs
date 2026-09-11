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
            double subscribers, long recentEarningsUsd, int daysOnSale)
        {
            Name = name ?? string.Empty;
            Type = type;
            Capability = capability;
            Users = users;
            Subscribers = subscribers;
            RecentEarningsUsd = recentEarningsUsd;
            DaysOnSale = daysOnSale;
        }

        public string Name { get; }
        public ModelType Type { get; }
        public double Capability { get; }

        /// <summary>People using this one, this company's share of them.</summary>
        public double Users { get; }

        /// <summary>Of those, the ones who pay.</summary>
        public double Subscribers { get; }

        /// <summary>
        /// What it has taken over its last <see cref="DeployedModel.RecentDays"/> on sale.
        ///
        /// **Read out of the record rather than split again here.** It used to be this month's
        /// subscriptions divided by a utility weight this table computed for itself, which is a
        /// third answer to a question the daily attribution already answers and writes down. The
        /// column and the corner banner now quote the same recorded figure.
        /// </summary>
        public long RecentEarningsUsd { get; }

        public int DaysOnSale { get; }
    }
}
