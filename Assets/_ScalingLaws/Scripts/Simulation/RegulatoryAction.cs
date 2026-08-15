using System;
using ScalingLaws.Core;

namespace ScalingLaws.Simulation
{
    /// <summary>
    /// A regulator has opened a file on the company, and has not decided anything yet.
    ///
    /// **The penalty used to arrive the same day the incident did.** One tick the company was fine,
    /// the next there was a nine figure demand in the mail, and the player had no moment in between
    /// to feel it coming. That is the difference between a hard game and an arbitrary one: the
    /// outcome was already decided either way, and the five days are what turn a number changing
    /// into something happening to you.
    ///
    /// **Nothing about the result is decided here.** The verdict is rolled when the inspection
    /// closes, against the safety modules the model was built with, which is the same roll that
    /// happened instantly before. This only holds it open long enough to be dreaded.
    /// </summary>
    public sealed class RegulatoryAction
    {
        /// <summary>How long a regulator takes. Long enough to notice, short enough not to nag.</summary>
        public const int InspectionDays = 5;

        public RegulatoryAction(SafetyIncident incident, GameDate openedOn, string modelName)
        {
            Incident = incident;
            OpenedOn = openedOn;
            ModelName = string.IsNullOrWhiteSpace(modelName) ? "the model" : modelName;
        }

        /// <summary>What was found. Already resolved; only whether it lands is still open.</summary>
        public SafetyIncident Incident { get; }

        public GameDate OpenedOn { get; }

        /// <summary>Which model is under inspection. The banner names it.</summary>
        public string ModelName { get; }

        public int DaysElapsed { get; private set; }

        public bool IsClosed => DaysElapsed >= InspectionDays;

        public int DaysLeft => Math.Max(0, InspectionDays - DaysElapsed);

        /// <summary>Zero to one, for the bar under the headline.</summary>
        public double Progress => Math.Clamp(DaysElapsed / (double)InspectionDays, 0.0, 1.0);

        public void Advance() => DaysElapsed = Math.Min(InspectionDays, DaysElapsed + 1);

        /// <summary>Only the save uses this.</summary>
        public void Restore(int daysElapsed) =>
            DaysElapsed = Math.Clamp(daysElapsed, 0, InspectionDays);

        /// <summary>What the banner says under the headline.</summary>
        public string Subtitle => $"{ModelName} violated regional safety requirements.";

        public override string ToString() =>
            $"{ModelName}: inspection day {DaysElapsed} of {InspectionDays}";
    }
}
