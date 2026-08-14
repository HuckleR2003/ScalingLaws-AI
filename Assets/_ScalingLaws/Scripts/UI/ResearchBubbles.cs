using System;
using UnityEngine.UIElements;

namespace ScalingLaws.UI
{
    /// <summary>
    /// The blue bubbles that lift off the desk while the lab is learning something.
    ///
    /// It is feedback rather than decoration, and the difference is that it only appears when points
    /// are genuinely being earned. A bubble is a promise that the counter is moving; one that rises
    /// on an idle company would be a lie told three times a minute.
    ///
    /// Purely presentational. It reads how many points a day the simulation says are coming and
    /// nothing here feeds back into the rules, which is why it can safely run off the frame clock
    /// while the simulation moves in whole days.
    /// </summary>
    public sealed class ResearchBubbles
    {
        /// <summary>How often one lifts off, in milliseconds. Slow enough to read, as agreed.</summary>
        public const long IntervalMilliseconds = 3000;

        /// <summary>How long one takes to travel. Long enough to follow with the eye.</summary>
        private const long FlightMilliseconds = 1400;

        /// <summary>Never more than this many in the air, whatever the rate.</summary>
        private const int MostAtOnce = 6;

        private readonly VisualElement host;
        private readonly Func<double> pointsPerDay;
        private int inFlight;
        private int spawned;

        public ResearchBubbles(VisualElement host, Func<double> pointsPerDay)
        {
            this.host = host;
            this.pointsPerDay = pointsPerDay;

            host.AddToClassList("bubbles");
            host.pickingMode = PickingMode.Ignore;

            host.schedule.Execute(Emit).Every(IntervalMilliseconds);
        }

        private void Emit()
        {
            if (host.panel == null || inFlight >= MostAtOnce)
            {
                return;
            }

            var rate = pointsPerDay();
            if (rate <= 0.0)
            {
                // Nothing is being learned, so nothing rises. This is the whole reason the element
                // reads the simulation instead of running on a timer alone.
                return;
            }

            var bubble = new VisualElement();
            bubble.AddToClassList("bubble");
            bubble.pickingMode = PickingMode.Ignore;

            // A little scatter so a stream of them does not read as one blinking dot. Deterministic
            // from a counter rather than random, because a repeating pattern is invisible at this
            // speed and an unseeded random in the interface is one more thing that cannot be replayed.
            var lane = spawned % 3;
            spawned++;

            bubble.style.left = Length.Percent(38f + lane * 9f);
            bubble.style.bottom = Length.Percent(24f);

            // A bigger bubble when more is being learned, which turns the rate into something the
            // player can see without reading the counter.
            var size = rate >= 12.0 ? 16f : rate >= 6.0 ? 13f : 10f;
            bubble.style.width = size;
            bubble.style.height = size;

            host.Add(bubble);
            inFlight++;

            // One frame later, so the transition has a start state to move away from. Setting both
            // ends in the same frame skips the animation entirely.
            bubble.schedule.Execute(() =>
            {
                bubble.AddToClassList("bubble--away");
                bubble.style.bottom = Length.Percent(96f);
                bubble.style.left = Length.Percent(76f + lane * 3f);
                bubble.style.opacity = 0f;
            }).ExecuteLater(20);

            bubble.schedule.Execute(() =>
            {
                bubble.RemoveFromHierarchy();
                inFlight--;
            }).ExecuteLater(FlightMilliseconds + 60);
        }
    }
}
