using System;
using System.Collections.Generic;
using UnityEngine.UIElements;

namespace ScalingLaws.UI
{
    /// <summary>
    /// Text that types itself in, holds, and wipes out again.
    ///
    /// **The point is that a menu with one fixed sentence on it is a screenshot.** The lines rotate,
    /// so somebody sitting on the front door for thirty seconds reads four different things about
    /// the game rather than the same one four times.
    ///
    /// The load between lines is the old television idea the author asked for: rather than fading,
    /// the line collapses to a bright bar and reopens, which is cheap to do with two elements and
    /// reads as a screen changing channel rather than as a label being edited.
    ///
    /// It is driven by the panel's own scheduler rather than by `Update`, so it costs nothing when
    /// the menu is not on screen and it stops itself when the element leaves the tree.
    /// </summary>
    public sealed class Typewriter
    {
        /// <summary>Seconds a finished line stays up before it starts wiping.</summary>
        public const int HoldMilliseconds = 3400;

        /// <summary>How long the bright bar sits between one line and the next.</summary>
        public const int FlickerMilliseconds = 180;

        private readonly Label target;
        private readonly VisualElement flicker;
        private readonly IReadOnlyList<string> lines;
        private readonly int typeMilliseconds;
        private readonly int wipeMilliseconds;
        private readonly Action finished;
        private readonly bool loop;

        private int line;
        private int character;
        private IVisualElementScheduledItem job;

        /// <summary>
        /// One line, typed once, then a callback. Used by the cold open.
        /// </summary>
        public Typewriter(Label target, string text, int totalMilliseconds, Action finished)
            : this(target, null, new[] { text }, totalMilliseconds, 0, false, finished)
        {
        }

        /// <summary>
        /// Several lines, cycling. Used by the menu.
        /// </summary>
        public Typewriter(Label target, VisualElement flicker, IReadOnlyList<string> lines,
            int typeMilliseconds, int wipeMilliseconds, bool loop, Action finished = null)
        {
            this.target = target;
            this.flicker = flicker;
            this.lines = lines ?? Array.Empty<string>();
            this.typeMilliseconds = Math.Max(120, typeMilliseconds);
            this.wipeMilliseconds = Math.Max(0, wipeMilliseconds);
            this.loop = loop;
            this.finished = finished;
        }

        public void Start()
        {
            if (target == null || lines.Count == 0)
            {
                finished?.Invoke();
                return;
            }

            line = 0;
            BeginLine();
        }

        public void Stop()
        {
            job?.Pause();
            job = null;
        }

        private void BeginLine()
        {
            character = 0;
            target.text = string.Empty;

            var text = lines[line];
            if (text.Length == 0)
            {
                Next();
                return;
            }

            // The whole line has to land inside the budget however long it is, so the tick is the
            // budget divided by the characters rather than a fixed rate. A fixed rate makes a long
            // line take twice as long as a short one, and the cold open has three seconds, full stop.
            var tick = Math.Max(8, typeMilliseconds / text.Length);

            Flash();

            job?.Pause();
            job = target.schedule.Execute(() =>
            {
                if (character >= text.Length)
                {
                    job?.Pause();
                    Hold();
                    return;
                }

                character++;
                target.text = text.Substring(0, character);
            }).Every(tick);
        }

        private void Hold()
        {
            if (!loop)
            {
                finished?.Invoke();
                return;
            }

            target.schedule.Execute(BeginWipe).ExecuteLater(HoldMilliseconds);
        }

        private void BeginWipe()
        {
            var text = lines[line];

            // Wiping runs faster than typing. Reading is the part worth the player's time; deleting
            // is the part that gets out of the way.
            var tick = Math.Max(6, Math.Max(1, wipeMilliseconds) / Math.Max(1, text.Length));

            job?.Pause();
            job = target.schedule.Execute(() =>
            {
                if (character <= 0)
                {
                    job?.Pause();
                    Next();
                    return;
                }

                character--;
                target.text = text.Substring(0, character);
            }).Every(tick);
        }

        private void Next()
        {
            line = (line + 1) % lines.Count;

            if (line == 0 && !loop)
            {
                finished?.Invoke();
                return;
            }

            target.schedule.Execute(BeginLine).ExecuteLater(FlickerMilliseconds);
        }

        /// <summary>The bright bar. Nothing if the caller did not give one.</summary>
        private void Flash()
        {
            if (flicker == null)
            {
                return;
            }

            flicker.AddToClassList("crt-flash--on");
            flicker.schedule
                .Execute(() => flicker.RemoveFromClassList("crt-flash--on"))
                .ExecuteLater(FlickerMilliseconds);
        }
    }

    /// <summary>
    /// What the front door says about the game.
    ///
    /// Written to be read by somebody who has not played it. Every one of them describes a thing
    /// that actually happens: the fine is the author's own first campaign, the frontier moving under
    /// a finished model is the spine of the whole design, and the tax bill in January is a mechanic
    /// rather than a joke.
    /// </summary>
    public static class MenuLines
    {
        public static readonly string[] All =
        {
            "January 2022. Twelve million dollars, no product, and eleven months before the world "
            + "finds out what any of this is for.",

            "Watch your safety numbers. Sixty million in the bank, everything going well, and then a "
            + "regulator pulls your only model off the market and fines you ninety.",

            "Your model finishes training in four months. The frontier does not wait four months.",

            "Rent the compute or buy it. Buy it too early and you own a depreciating asset; buy it "
            + "too late and somebody else already has the customers.",

            "Corporation tax accrues all year and arrives in January. A company that cannot pay in "
            + "January decided that in September.",

            "You can pay for advance warning. The cheap desk is wrong about one thing in three and "
            + "sounds exactly as confident as the expensive one.",

            "Advertising buys attention, never quality. A bad model advertised hard gets tried and "
            + "abandoned, which costs you twice.",

            "There is no winning move that works twice. Token prices halve every year."
        };
    }
}
