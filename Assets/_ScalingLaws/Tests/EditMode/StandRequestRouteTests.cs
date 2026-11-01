using System;
using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace ScalingLaws.Tests.EditMode
{
    /// <summary>
    /// Pressing BOOK A STAND on the map has to land the player on the stand window.
    ///
    /// **Francisco's "it doesnt work", with three seconds of video attached.** The map holds no
    /// reference to the company on purpose, so the press records which venue was asked about and
    /// leaves. The desk opens the window on the way back in.
    ///
    /// That handover was being run from the **middle of `BuildTree`**, where it switched to the
    /// news desk, and then the startup path ran `Show(Screen.Site)` four lines later and put the
    /// office back over it. A frame after that the stand window mounted itself into a page nobody
    /// was looking at. The map closed, the office appeared, nothing happened.
    ///
    /// The rule is one line long: **the handover is allowed to overrule the opening screen, so it
    /// has to run after it.** Nothing else in the build is allowed to do that, which is why this is
    /// a rule rather than a tidy-up.
    ///
    /// Read from the source because the whole fault is an ordering between two statements, and an
    /// EditMode test has no scene to start.
    /// </summary>
    public sealed class StandRequestRouteTests
    {
        private static readonly string ShellSource = Path.Combine(
            Application.dataPath, "_ScalingLaws", "Scripts", "UI", "GameShell.cs");

        [Test]
        public void TheStandRequestIsAnsweredAfterTheOpeningScreenIsChosen()
        {
            var source = File.ReadAllText(ShellSource);

            var build = source.IndexOf("BuildTree();", StringComparison.Ordinal);
            Assert.That(build, Is.GreaterThan(-1), "BuildTree is no longer called by that name.");

            var opens = source.IndexOf("Show(Screen.Site);", build, StringComparison.Ordinal);
            var answers = source.IndexOf("AnswerAnyStandRequest();", build, StringComparison.Ordinal);

            Assert.That(opens, Is.GreaterThan(-1), "The startup path no longer opens the office.");
            Assert.That(answers, Is.GreaterThan(-1),
                "Nothing answers the stand request any more, so BOOK A STAND on the map leaves the "
                + "city and opens nothing.");

            Assert.That(answers, Is.GreaterThan(opens),
                "The stand request is answered before the opening screen is chosen, so the office "
                + "is drawn over the news desk the handover just opened and the stand window mounts "
                + "into a page the player cannot see. That is the shape of the report, not a "
                + "stylistic preference: move the call below Show(Screen.Site).");
        }

        /// <summary>
        /// One caller. Two would mean the office can win the race depending on which ran last.
        /// </summary>
        [Test]
        public void OnlyOneThingAnswersTheStandRequest()
        {
            var source = File.ReadAllText(ShellSource);
            var count = 0;

            for (var at = 0; ; count++)
            {
                at = source.IndexOf("AnswerAnyStandRequest();", at, StringComparison.Ordinal);

                if (at < 0)
                {
                    break;
                }

                at += 1;
            }

            Assert.That(count, Is.EqualTo(1),
                "There is more than one call to AnswerAnyStandRequest. The request clears itself "
                + "when it is read, so a second caller reads an empty string and the first one to "
                + "run decides whether the player ever reaches the stand window.");
        }
    }
}
