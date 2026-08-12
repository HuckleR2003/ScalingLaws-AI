using System;
using ScalingLaws.Core;

namespace ScalingLaws.Simulation
{
    /// <summary>How a service is holding up, in the three words an operations page would use.</summary>
    public enum ServiceStatus
    {
        Stable = 0,
        Unstable = 1,
        Critical = 2
    }

    /// <summary>
    /// What it is like to actually use the product today.
    ///
    /// This is the missing half of the market. Until now share was awarded on how attractive a model
    /// was and the capacity to serve it was checked separately and then ignored: a company with no
    /// compute at all still held five million users. Demand that cannot be met has to cost something,
    /// and what it costs is the experience.
    ///
    /// Latency follows the standard queueing shape, rising slowly until the cluster is nearly full and
    /// then very fast. That curve is the reason overprovisioning is a real decision rather than waste:
    /// the difference between eighty and ninety five percent utilisation is not fifteen percent of
    /// anything, it is the difference between a fast service and an unusable one.
    /// </summary>
    public readonly struct ServiceQuality
    {
        /// <summary>Milliseconds a request takes on a cluster with room to spare.</summary>
        public const double FloorMilliseconds = 180.0;

        /// <summary>Above this the service is no longer comfortably fast.</summary>
        public const double UnstableAbove = 0.80;

        /// <summary>Above this it is failing, whatever the dashboard says.</summary>
        public const double CriticalAbove = 0.95;

        /// <summary>The worst the reliability term is allowed to make a product look.</summary>
        public const double WorstReliability = 0.35;

        public ServiceQuality(double demandedBillions, double capacityBillions, double packagedShare)
        {
            Demanded = Math.Max(0.0, SimUnits.Finite(demandedBillions));
            Capacity = Math.Max(0.0, SimUnits.Finite(capacityBillions));
            PackagedShare = Math.Clamp(SimUnits.Finite(packagedShare), 0.0, 1.0);

            Utilisation = Capacity <= 0.0
                ? Demanded > 0.0 ? 1.0 : 0.0
                : Math.Clamp(Demanded / Capacity, 0.0, 1.0);
        }

        /// <summary>Tokens people wanted today.</summary>
        public double Demanded { get; }

        /// <summary>Tokens the fleet could produce today.</summary>
        public double Capacity { get; }

        /// <summary>
        /// How much of the capacity comes from dedicated packages rather than the shared pool.
        ///
        /// Reserved capacity behaves better under load, which is what it is sold for: a package holds
        /// its latency where the shared pool starts queueing.
        /// </summary>
        public double PackagedShare { get; }

        /// <summary>Nothing to everything. One means the cluster is full.</summary>
        public double Utilisation { get; }

        public ServiceStatus Status => Utilisation switch
        {
            > CriticalAbove => ServiceStatus.Critical,
            > UnstableAbove => ServiceStatus.Unstable,
            _ => ServiceStatus.Stable
        };

        /// <summary>
        /// What a request feels like, in milliseconds.
        ///
        /// The classic queue: response time is the floor divided by the headroom left. At half load a
        /// request takes twice the floor; at ninety percent it takes ten times it. Dedicated capacity
        /// pushes the knee later, so a packaged fleet stays usable at loads that would ruin a shared
        /// one.
        /// </summary>
        public double ResponseMilliseconds
        {
            get
            {
                // Packages buy headroom, so the effective load is lower than the raw load.
                var effective = Utilisation * (1.0 - 0.28 * PackagedShare);
                var headroom = Math.Max(0.04, 1.0 - effective);

                return Math.Min(9_000.0, FloorMilliseconds / headroom);
            }
        }

        /// <summary>
        /// What the experience does to how attractive the product is.
        ///
        /// One at a comfortable load, falling away as the service degrades, and never quite zero
        /// because even a bad service keeps somebody. This multiplies into the market's own utility,
        /// so an overloaded company loses users to rivals rather than merely failing to grow, which
        /// is the behaviour that was missing.
        /// </summary>
        public double Reliability
        {
            get
            {
                if (Utilisation <= UnstableAbove)
                {
                    return 1.0;
                }

                // From the point it stops being comfortable to the point it is failing, and beyond.
                var over = (Utilisation - UnstableAbove) / (1.0 - UnstableAbove);
                var damage = Math.Clamp(over, 0.0, 1.0);

                return Math.Clamp(1.0 - (1.0 - WorstReliability) * damage * damage,
                    WorstReliability, 1.0);
            }
        }

        /// <summary>A sentence for the operations panel. Says what and why, never only a colour.</summary>
        public string Headline => Status switch
        {
            ServiceStatus.Critical =>
                "Requests are queueing badly. People are leaving faster than the product can win them.",
            ServiceStatus.Unstable =>
                "The cluster is close to full. Response times are climbing and users can feel it.",
            _ => Capacity <= 0.0
                ? "No capacity at all. Nothing can be served."
                : "Comfortable. There is headroom for a busy day."
        };
    }
}
