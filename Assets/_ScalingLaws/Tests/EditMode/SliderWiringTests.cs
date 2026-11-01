using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;

namespace ScalingLaws.Tests.EditMode
{
    /// <summary>
    /// No slider may rebuild its own page while the player is holding it.
    ///
    /// **This is the ratchet for the fault that made every slider in the game undraggable.** A
    /// value-changed callback that calls Show destroys the handle under the cursor, the pointer
    /// capture goes with it, and the drag ends on its first pixel: clicking the groove was the only
    /// thing that ever worked, and the author reported exactly that on 2026-10-31 about all of
    /// them at once. `SliderDragTests` measured both halves of it.
    ///
    /// It is the tutorial NEXT button a third time, so it gets a guard rather than a note.
    ///
    /// Two ways past it, both correct: update the page in place from the callback, which is what
    /// the creator cash slider does, or hand the rebuild to `UiParts.WhenSettled`, which waits for
    /// the handle to be let go.
    /// </summary>
    public sealed class SliderWiringTests
    {
        private static readonly string UiFolder =
            Path.Combine(Application.dataPath, "_ScalingLaws", "Scripts", "UI");

        /// <summary>What a rebuild of a whole page looks like from inside a callback.</summary>
        private static readonly Regex Rebuilds =
            new(@"\b(Show|Refresh|ShowStage|RepriceAndRebuild)\s*\(", RegexOptions.Compiled);

        /// <summary>
        /// A registration on something whose name says it is a slider, with its lambda body.
        /// Deliberately crude: it reads the ten lines after the call, which covers every callback
        /// of this shape in the project and cannot be fooled into passing by a long one.
        /// </summary>
        private static readonly Regex Registration =
            new(@"(?<name>[A-Za-z_][A-Za-z0-9_]*)\s*\.RegisterValueChangedCallback",
                RegexOptions.Compiled);

        [Test]
        public void NoSliderRebuildsThePageItIsStandingOn()
        {
            var offences = new List<string>();

            foreach (var file in Directory.GetFiles(UiFolder, "*.cs", SearchOption.AllDirectories))
            {
                var lines = File.ReadAllLines(file);

                for (var index = 0; index < lines.Length; index++)
                {
                    var match = Registration.Match(lines[index]);
                    if (!match.Success)
                    {
                        continue;
                    }

                    var name = match.Groups["name"].Value;
                    if (!name.ToLowerInvariant().Contains("slider"))
                    {
                        continue;
                    }

                    var body = string.Join("\n", lines,
                        index, System.Math.Min(10, lines.Length - index));

                    if (!Rebuilds.IsMatch(body))
                    {
                        continue;
                    }

                    offences.Add($"{Path.GetFileName(file)}:{index + 1} ({name})");
                }
            }

            Assert.That(offences, Is.Empty,
                "These sliders rebuild their own page on every change, which takes the handle out "
                + "of the tree and makes them click-only:\n  "
                + string.Join("\n  ", offences)
                + "\nUpdate the page in place, or pass the rebuild to UiParts.WhenSettled so it "
                + "runs when the handle is let go.");
        }
    }
}
