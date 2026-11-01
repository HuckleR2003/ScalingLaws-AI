using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;

namespace ScalingLaws.Tests.EditMode
{
    /// <summary>
    /// Every route into the hiring screen states the job it is filling.
    ///
    /// **The ratchet for the one report this project lost twice.** `HiringPortals` keeps its
    /// shortlists per channel **and** per position, and the position was a field that only the
    /// chips inside that screen ever wrote. So opening hiring from a job card showed whichever
    /// job had last been chosen in there, with that job's shortlist under it.
    ///
    /// The tester reported it, it was not written down, it was not fixed, and the second report
    /// said "you still havent fixed this". A note would have been lost the same way; this is a
    /// test.
    ///
    /// It reads the source rather than driving the screen because the fault is in the handover
    /// between two files, and an EditMode test has no panel to click in.
    /// </summary>
    public sealed class HiringRouteTests
    {
        private static readonly string UiFolder =
            Path.Combine(Application.dataPath, "_ScalingLaws", "Scripts", "UI");

        /// <summary>Opening a channel. The assignment that starts a route into the screen.</summary>
        private static readonly Regex OpensAChannel =
            new(@"\.Open\s*=\s*HiringPortal\.", RegexOptions.Compiled);

        /// <summary>Saying which job. It may come before or after, so both sides are read.</summary>
        private static readonly Regex StatesThePosition =
            new(@"\.Position\s*=", RegexOptions.Compiled);

        [Test]
        public void EveryRouteIntoHiringSaysWhichJobItIsFilling()
        {
            var offences = new List<string>();

            foreach (var file in Directory.GetFiles(UiFolder, "*.cs", SearchOption.AllDirectories))
            {
                // The screen's own chooser sets the channel from the card the player just
                // pressed and is already standing on a position, so it is not a route in.
                if (Path.GetFileName(file) == "HiringPortals.cs")
                {
                    continue;
                }

                var lines = File.ReadAllLines(file);

                for (var index = 0; index < lines.Length; index++)
                {
                    if (!OpensAChannel.IsMatch(lines[index]))
                    {
                        continue;
                    }

                    var from = System.Math.Max(0, index - 4);
                    var count = System.Math.Min(lines.Length - from, index - from + 5);
                    var window = string.Join("\n", lines, from, count);

                    if (StatesThePosition.IsMatch(window))
                    {
                        continue;
                    }

                    offences.Add($"{Path.GetFileName(file)}:{index + 1}");
                }
            }

            Assert.That(offences, Is.Empty,
                "These routes open the hiring screen without saying which job they are filling, "
                + "so it opens on whichever position was last chosen in there and draws that "
                + "job's shortlist:\n  "
                + string.Join("\n  ", offences)
                + "\nSet portals.Position beside portals.Open.");
        }
    }
}
