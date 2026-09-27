using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using ScalingLaws.Simulation;

namespace ScalingLaws.Tests.EditMode
{
    /// <summary>
    /// Every kind of event the simulation raises reaches somewhere a player can see it.
    ///
    /// **This project has now shipped that fault twice.** The first time, the game raised thirty
    /// four kinds of event a day and showed the player none of them: `GameShell.DrainEvents` pulled
    /// them into a list nothing read, so a safety incident could fine the company, cut its
    /// reputation and retire the flagship, and all the player saw was income falling for no stated
    /// reason. The wire was built to fix that.
    ///
    /// The second time was the relations system. Nine event types went in over two days, every one
    /// of them raised correctly, and not one was read by the wire, by the notice or by any screen.
    /// A player signed an alliance, had an offer refused and lost a research programme, and nothing
    /// anywhere said so. Found by sweeping, not by playing, which is the only way this class of
    /// fault is ever found in time.
    ///
    /// **A type may be deliberately silent, and several are.** The rule is not that everything is
    /// announced, it is that nothing is silent by accident: a silent type has to be named in
    /// `Quiet` below, with the reason, so that adding one is a decision somebody wrote down.
    /// </summary>
    public sealed class EventReachTests
    {
        /// <summary>
        /// Types the game deliberately never shows, and why.
        ///
        /// Mostly the player's own click coming back a second later. A feed that reports the player
        /// to themselves buries the events they did not cause, which is the reasoning `NewsDesk`
        /// already carries for its own filtering.
        /// </summary>
        private static readonly Dictionary<CompanyEventType, string> Quiet = new()
        {
            [CompanyEventType.TrainingStarted] = "the player just pressed it",
            [CompanyEventType.ResearchStarted] = "the player just pressed it",
            [CompanyEventType.HardwareOrdered] = "announced by the order itself",
            [CompanyEventType.StaffHired] = "the player just pressed it",
            [CompanyEventType.SkillLevelled] = "the corner banner draws it",
            [CompanyEventType.OfferSent] = "the player just pressed it; the answer is the moment",
            [CompanyEventType.Notice] = "already a sentence somebody chose to show",

            // **Declared and never raised by anything, which this guard found on its first run.**
            // Not silent, unused: the grants system announces itself through the mail and never
            // took this slot. Kept rather than deleted because renumbering an event type is the
            // kind of edit that goes wrong quietly, and named here so the next reader knows it is
            // free rather than broken.
            [CompanyEventType.GrantOffered] = "declared and never raised; the grant arrives by post"
        };

        /// <summary>Everything under `Scripts/`, joined, so a name can be looked for across it.</summary>
        private static string AllCode()
        {
            var root = Path.Combine(UnityEngine.Application.dataPath, "_ScalingLaws", "Scripts");

            return string.Join("\n",
                Directory.GetFiles(root, "*.cs", SearchOption.AllDirectories)
                    .Where(path => !path.EndsWith("CompanyEvent.cs", StringComparison.Ordinal))
                    .Select(File.ReadAllText));
        }

        [Test]
        public void EveryEventTypeReachesTheWireTheNoticeOrAScreen()
        {
            var code = AllCode();
            var silent = new List<string>();

            foreach (CompanyEventType type in Enum.GetValues(typeof(CompanyEventType)))
            {
                if (Quiet.ContainsKey(type))
                {
                    continue;
                }

                // Raising it does not count: the search is for somewhere that reads it, and the one
                // file that only ever raises is excluded above.
                if (!code.Contains($"CompanyEventType.{type}", StringComparison.Ordinal))
                {
                    silent.Add(type.ToString());
                }
            }

            CollectionAssert.IsEmpty(silent,
                "These events are raised and read by nothing, so the thing they report happens and "
                + "the player is never told. Either give them a case on the wire or in the notice, "
                + "or name them in `Quiet` with the reason: "
                + string.Join(", ", silent));
        }

        /// <summary>
        /// Nothing is in the quiet list that does not exist.
        ///
        /// A list of exceptions that outlives what it excepted is how a guard quietly stops
        /// guarding, which this project has been caught by once already on a test that asserted a
        /// count instead of a fact.
        /// </summary>
        [Test]
        public void TheQuietListNamesOnlyRealEvents()
        {
            foreach (var pair in Quiet)
            {
                Assert.IsTrue(Enum.IsDefined(typeof(CompanyEventType), pair.Key),
                    $"{pair.Key} is excused from being shown and is not an event any more.");

                Assert.IsNotEmpty(pair.Value,
                    $"{pair.Key} is silent and nobody wrote down why.");
            }
        }
    }
}
