using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;

namespace ScalingLaws.Tests.EditMode
{
    /// <summary>
    /// Code that exists and is never called.
    ///
    /// **This has now happened five times in this project and every one of them shipped.** The model
    /// type was chosen everywhere and passed nowhere. `Retire` could only be reached by a safety
    /// incident. Debt had a full simulation and no button. Thirty four kinds of event were raised
    /// and drained into a list nothing read. And `ShowResearchCard`, the popup with the icon, the
    /// description, the costs and the start button, sat complete and uncalled while clicking a
    /// research node only moved a ring.
    ///
    /// Every one of those passed the whole suite, because a test that drives the simulation cannot
    /// tell the difference between a control that is missing and one that is merely unreachable.
    /// This fixture reads the source instead. It is crude on purpose: a private method nobody calls
    /// is either dead or unwired, and both are worth a failure.
    /// </summary>
    public sealed class UiWiringTests
    {
        private static string UiFolder =>
            Path.Combine(Application.dataPath, "_ScalingLaws", "Scripts", "UI");

        private static Dictionary<string, string> UiSources()
        {
            var sources = new Dictionary<string, string>();
            foreach (var file in Directory.GetFiles(UiFolder, "*.cs", SearchOption.AllDirectories))
            {
                sources[Path.GetFileName(file)] = File.ReadAllText(file);
            }

            return sources;
        }

        /// <summary>
        /// Every private method in the interface layer is called by something.
        ///
        /// Private is the right scope to check. A public method with no caller may be an entry point
        /// for a test or for a screen not written yet; a private one with no caller cannot be reached
        /// by anything, ever, and the compiler does not warn about it when it is invoked nowhere but
        /// looks used because a method of the same shape exists.
        /// </summary>
        [Test]
        public void NoPrivateMethodInTheInterfaceIsUnreachable()
        {
            var sources = UiSources();
            Assert.IsNotEmpty(sources, "No UI sources found, so this test is checking nothing.");

            var declaration = new Regex(
                @"private\s+(?:static\s+)?(?:readonly\s+)?[\w<>\[\],\.\?]+\s+(\w+)\s*\(",
                RegexOptions.Compiled);

            var orphans = new List<string>();

            foreach (var (fileName, text) in sources)
            {
                foreach (Match match in declaration.Matches(text))
                {
                    var name = match.Groups[1].Value;

                    // Unity calls these itself, and a constructor shares its name with its type.
                    if (name is "Update" or "OnEnable" or "OnDisable" or "OnDestroy" or "Awake"
                        or "Start" or "OnGUI" or "if" or "for" or "foreach" or "while" or "switch"
                        or "return" or "catch" or "using" or "lock" or "readonly")
                    {
                        continue;
                    }

                    if (name == Path.GetFileNameWithoutExtension(fileName))
                    {
                        continue;
                    }

                    // The bare identifier, not "name(". Half the interface is wired with method
                    // groups: generateVisualContent += Draw, new Button(SkipDay), a callback passed
                    // by name. Counting only call syntax reported every one of those as dead, which
                    // is the kind of false alarm that gets a test deleted rather than fixed.
                    var uses = Regex.Matches(text, @"\b" + Regex.Escape(name) + @"\b").Count;
                    var declarations = Regex.Matches(
                        text, @"private\s+(?:static\s+)?[\w<>\[\],\.\?]+\s+"
                              + Regex.Escape(name) + @"\s*\(").Count;

                    if (uses <= declarations)
                    {
                        orphans.Add($"{fileName}: {name}");
                    }
                }
            }

            Assert.IsEmpty(orphans,
                "These are written and never called, so they are either dead or a control the player "
                + "cannot reach:\n  " + string.Join("\n  ", orphans)
                + "\n\nThis has shipped five times. Delete it or wire it.");
        }

        /// <summary>
        /// The specific one that just cost a turn: the research popup has to be opened by something.
        /// </summary>
        [Test]
        public void ClickingAResearchNodeOpensTheCard()
        {
            var shell = UiSources()["GameShell.cs"];

            var calls = Regex.Matches(shell, @"ShowResearchCard\s*\(").Count;

            Assert.Greater(calls, 1,
                "ShowResearchCard is declared and never called. Clicking a node then only moves a "
                + "ring, and the player is left to guess what the node does.");

            Assert.IsTrue(shell.Contains("RegisterCallback<ClickEvent>"),
                "The card has to open where the cursor is, which needs the click event rather than "
                + "the button action.");
        }

        /// <summary>
        /// Both corner banners have to be hidden the moment the screen changes rather than on the
        /// next tick, because a paused game has no next tick and that is when a player reads.
        /// </summary>
        [Test]
        public void TheCornerBannersAreHiddenWhenTheScreenChangesRatherThanOnTheNextDay()
        {
            var shell = UiSources()["GameShell.cs"];

            var show = shell.IndexOf("private void Show(Screen screen)", System.StringComparison.Ordinal);
            Assert.Greater(show, 0, "Show(Screen) is gone, so this test is checking nothing.");

            var body = shell.Substring(show, System.Math.Min(2200, shell.Length - show));

            Assert.IsTrue(body.Contains("newsBanner?.SetHidden"),
                "The news banner is not hidden inside Show, so it stays over the new screen until a "
                + "day rolls over, and while paused no day rolls over.");

            Assert.IsTrue(body.Contains("modelBanner?.SetHidden"),
                "Same for the product banner.");
        }
    }
}
