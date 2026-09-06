using System;
using System.Collections.Generic;
using ScalingLaws.Data;
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
        /// <summary>
        /// The eight lines, resolved fresh every time they are asked for.
        ///
        /// **A property rather than a `static readonly` array, and that is the whole point.** A
        /// static array of resolved strings is built once, the first time anything touches the type,
        /// and keeps whatever language was current at that moment for the rest of the process. The
        /// player can switch language from the settings sheet at any time, so the menu would go on
        /// typing English after the rest of the screen had turned Polish.
        ///
        /// The menu rebuilds itself on a language change, and the array is read once per rebuild, so
        /// resolving eight phrases here costs nothing anybody can measure.
        /// </summary>
        public static string[] All => new[]
        {
            Loc.T("menu.tip.1"),
            Loc.T("menu.tip.2"),
            Loc.T("menu.tip.3"),
            Loc.T("menu.tip.4"),
            Loc.T("menu.tip.5"),
            Loc.T("menu.tip.6"),
            Loc.T("menu.tip.7"),
            Loc.T("menu.tip.8")
        };
    }
}
