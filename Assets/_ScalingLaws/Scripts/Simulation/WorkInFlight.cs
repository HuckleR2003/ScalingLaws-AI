using System;
using ScalingLaws.Core;

namespace ScalingLaws.Simulation
{
    /// <summary>
    /// Whatever long job the company is in the middle of, described the same way whichever it is.
    ///
    /// Training and research are different mechanics that look identical to a player waiting for
    /// them: a name, a bar, and a number of days. Giving the banner one shape for both is what stops
    /// a second strip appearing in the corner the first time another kind of work is added.
    /// </summary>
    public readonly struct WorkInFlight
    {
        public WorkInFlight(string caption, string subject, double progress, int daysLeft)
        {
            Caption = caption ?? string.Empty;
            Subject = subject ?? string.Empty;
            Progress = Math.Clamp(SimUnits.Finite(progress), 0.0, 1.0);
            DaysLeft = Math.Max(0, daysLeft);
            Busy = true;
        }

        /// <summary>The words across the strip: what kind of work this is.</summary>
        public string Caption { get; }

        /// <summary>What it is being done to. The model's name, or the node's.</summary>
        public string Subject { get; }

        public double Progress { get; }

        /// <summary>The number a player plans around. The percentage only makes the bar readable.</summary>
        public int DaysLeft { get; }

        public bool Busy { get; }

        public static WorkInFlight Idle => default;
    }
}
