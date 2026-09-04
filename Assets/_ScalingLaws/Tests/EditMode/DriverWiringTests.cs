using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using NUnit.Framework;
using ScalingLaws.UI;

namespace ScalingLaws.Tests.EditMode
{
    /// <summary>
    /// Public members of the interface's driver classes that nothing ever calls.
    ///
    /// **`UiWiringTests` reads private methods, and this project keeps shipping the public version of
    /// the same fault.** A private method with no caller is dead code. A public one with no caller is
    /// a mechanism nobody drives, which looks exactly like a finished feature from every angle except
    /// the player's.
    ///
    /// Two in one week:
    ///
    /// * `StaffPresence.Refresh` — the shell built the class and never asked it anything, so a
    ///   company could hire a floor of people and the office stayed empty.
    /// * `GuideOverlay.PlayerOpened` — written months ago, described in the project notes as working,
    ///   called by nothing. Six steps of the tutorial told the player to click a tab and then went on
    ///   waiting for a button after they had clicked it.
    ///
    /// The list is deliberately short and named rather than every class in `UI/`. These are the
    /// classes the shell owns and drives, where "nothing calls this" is a bug rather than an
    /// observation: a panel's `Build` is called by whoever shows it, and a driver's methods are called
    /// by one place or by nowhere.
    /// </summary>
    public sealed class DriverWiringTests
    {
        /// <summary>
        /// The classes the shell holds and drives.
        ///
        /// Add one here when the shell grows a field it has to remember to talk to. That is the whole
        /// population of this failure so far.
        /// </summary>
        private static readonly Type[] Drivers =
        {
            typeof(GuideOverlay),
            typeof(StaffPresence),
            typeof(FounderPresence),
            typeof(PromptChips),
            typeof(TaskBanner),
            typeof(StateBoard)
        };

        /// <summary>
        /// Methods whose only job is to be called from a test or a proof render.
        ///
        /// Named rather than pattern-matched, so adding one is a deliberate line here rather than a
        /// suffix somebody can reach for to silence this guard.
        /// </summary>
        private static readonly HashSet<string> ForTooling = new()
        {
            "OpenMessengerForProof"
        };

        private static string AllCode()
        {
            var scripts = Path.Combine(Directory.GetCurrentDirectory(),
                "Assets", "_ScalingLaws", "Scripts");

            var text = new StringBuilder();

            foreach (var file in Directory.GetFiles(scripts, "*.cs", SearchOption.AllDirectories))
            {
                text.Append(File.ReadAllText(file));
            }

            // Comments are stripped, or a method's own doc comment naming it would vouch
            // for it and this guard would pass on exactly the methods that need it most.
            var code = Regex.Replace(text.ToString(), @"/\*.*?\*/", " ",
                RegexOptions.Singleline);

            return Regex.Replace(code, @"//[^\n]*", " ");
        }

        [Test]
        public void EveryPublicMethodOnADriverIsCalledBySomething()
        {
            var code = AllCode();
            var orphans = new List<string>();

            foreach (var driver in Drivers)
            {
                var declared = driver.GetMethods(
                    BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);

                foreach (var method in declared)
                {
                    // Properties and events arrive here as get_/set_/add_ pairs, and an operator is
                    // not something anybody calls by name.
                    if (method.IsSpecialName || ForTooling.Contains(method.Name))
                    {
                        continue;
                    }

                    // **The bare name, not the name followed by a bracket.** A method handed
                    // over as a delegate - `Reached = WalkthroughDid;` - is driven and has
                    // no call parentheses anywhere, and the first run of this guard
                    // reported exactly that as dead.
                    //
                    // The declaration itself is one occurrence, so anything live has at
                    // least two. Counted rather than searched-and-excluded, because the
                    // declaration and a use read the same to a regex and excluding by file
                    // would miss a class that drives itself.
                    var uses = Regex.Matches(
                        code, @"\b" + Regex.Escape(method.Name) + @"\b").Count;

                    if (uses <= 1)
                    {
                        orphans.Add($"{driver.Name}.{method.Name}");
                    }
                }
            }

            CollectionAssert.IsEmpty(orphans,
                "These are public, complete, and nothing ever calls them, which is what a finished "
                + "feature nobody can reach looks like from the inside: "
                + string.Join(", ", orphans));
        }

        /// <summary>
        /// The tutorial is finished by doing the thing, not by pressing a button afterwards.
        ///
        /// The specific wire that was missing, asserted specifically, because the guard above would
        /// go green again the moment somebody called `PlayerOpened` from anywhere at all.
        /// </summary>
        [Test]
        public void OpeningATabTellsTheTutorialTheTabWasOpened()
        {
            var shell = File.ReadAllText(Path.Combine(Directory.GetCurrentDirectory(),
                "Assets", "_ScalingLaws", "Scripts", "UI", "GameShell.cs"));

            Assert.IsTrue(Regex.IsMatch(shell, @"guide\??\.PlayerOpened\s*\("),
                "Nothing tells the tour that the player reached a screen on their own, so every "
                + "step that says \"click COMPUTE\" waits for a button after they have.");

            StringAssert.Contains("GuideTargetForScreen", shell,
                "The tour is told about a screen without a way to turn it into a target.");
        }
    }
}
