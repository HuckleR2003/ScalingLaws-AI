using System;
using ScalingLaws.Core;
using ScalingLaws.Data;

namespace ScalingLaws.Simulation
{
    /// <summary>
    /// How the product that is actually on sale is doing.
    ///
    /// Everything here is read from state that already exists. The banner draws this and computes
    /// nothing, which is the same rule the Foundation panel and the finance report follow: a readout
    /// that does its own arithmetic is a second copy of the rules waiting to disagree with them.
    ///
    /// It describes **one** product, the strongest live model, because that is the one the market is
    /// actually choosing between. A company with three lines has three products and this describes
    /// the flagship; the rest are on the management page.
    /// </summary>
    public readonly struct ProductStanding
    {
        public ProductStanding(string name, bool exists, double happiness, double topicality,
            double subscribers, long monthEarnings, long monthNet, int daysOld, double capability,
            double frontier, long ownLifetime = 0L, long ownRecent = 0L)
        {
            GivenName = name;
            Exists = exists;
            Happiness = Math.Clamp(SimUnits.Finite(happiness), 0.0, 1.0);
            Topicality = Math.Clamp(SimUnits.Finite(topicality), 0.0, 1.0);
            Subscribers = Math.Max(0.0, SimUnits.Finite(subscribers));
            MonthEarningsUsd = monthEarnings;
            MonthNetUsd = monthNet;
            DaysOld = Math.Max(0, daysOld);
            Capability = Math.Max(0.0, SimUnits.Finite(capability));
            Frontier = Math.Max(0.0, SimUnits.Finite(frontier));
            OwnLifetimeUsd = Math.Max(0L, ownLifetime);
            OwnRecentUsd = Math.Max(0L, ownRecent);
        }

        private string GivenName { get; }

        /// <summary>
        /// What the banner calls the product.
        ///
        /// **Resolved when it is read, not when the struct is built.** The fallback is a phrase the
        /// player sees, so it follows the language like everything else. A struct built during an
        /// English session and read after a switch would otherwise still say NO PRODUCT.
        /// </summary>
        public string Name =>
            string.IsNullOrWhiteSpace(GivenName) ? Loc.T("banner.no_product") : GivenName;

        /// <summary>False when nothing is on sale, which is a state the banner has to draw.</summary>
        public bool Exists { get; }

        /// <summary>
        /// How much the people using it prefer it to their next best option. The same figure the
        /// corner banner shows for the company, because with one product they are the same thing.
        /// </summary>
        public double Happiness { get; }

        /// <summary>
        /// Whether the product still reads as current. Two things make it stale and they are
        /// different: the calendar moving on, and the frontier moving past it. A model can be six
        /// months old and still current if nobody has beaten it, and three months old and already
        /// behind if somebody has.
        /// </summary>
        public double Topicality { get; }

        public double Subscribers { get; }

        /// <summary>Subscription and API money this calendar month, from the books.</summary>
        public long MonthEarningsUsd { get; }

        /// <summary>Everything in minus everything out this month. Negative is normal early.</summary>
        public long MonthNetUsd { get; }

        // The two above are the company. The two below are this product, and keeping them apart
        // is the whole point of the pair existing.
        //
        // **They were one pair doing both jobs and it drew a headcount as money.** A follower
        // banner passed its own user count into `monthNet` and swapped the caption to say so, so
        // 1.96M people were printed as `+$1.96M` in the green a profit gets, because
        // `IsProfitable` reads that same slot. Two fields carrying four meanings is the shape of
        // the fault rather than an accident in one line, so they are separated rather than
        // formatted differently.

        /// <summary>What this one product has taken since the day it went on sale.</summary>
        public long OwnLifetimeUsd { get; }

        /// <summary>What it has taken over its last <see cref="DeployedModel.RecentDays"/> on sale.</summary>
        public long OwnRecentUsd { get; }

        public int DaysOld { get; }
        public double Capability { get; }
        public double Frontier { get; }

        public bool IsProfitable => MonthNetUsd >= 0L;

        /// <summary>One word for the topicality bar, so the colour is not the only information.</summary>
        public string Freshness => Topicality switch
        {
            >= 0.8 => Loc.T("banner.fresh.current"),
            >= 0.55 => Loc.T("banner.fresh.holding"),
            >= 0.3 => Loc.T("banner.fresh.slipping"),
            _ => Loc.T("banner.fresh.outdated")
        };

        /// <summary>
        /// How current a product is, from the two things that age it.
        ///
        /// Calendar age is measured against the same band the reputation system uses, so a product
        /// does not read as fresh on one screen and stale on another. The frontier gap is measured in
        /// capability points, where ten points is a full generation of compute.
        /// </summary>
        public static double TopicalityOf(int daysOld, double capability, double frontier)
        {
            var byAge = 1.0 - Math.Clamp(
                (daysOld - (double)Standing.FreshDays) / (Standing.StaleDays - Standing.FreshDays),
                0.0, 1.0);

            var gap = Math.Max(0.0, SimUnits.Finite(frontier) - SimUnits.Finite(capability));
            var byFrontier = 1.0 - Math.Clamp(gap / 10.0, 0.0, 1.0);

            // The worse of the two, not the average. Being current means being current in both
            // senses, and averaging would let a brand new model that is already ten points behind
            // read as half fresh.
            return Math.Min(byAge, byFrontier);
        }
    }
}
