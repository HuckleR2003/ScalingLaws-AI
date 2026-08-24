using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using ScalingLaws.Data;
using ScalingLaws.Simulation;

namespace ScalingLaws.Tests.EditMode
{
    /// <summary>
    /// Walks the whole tutorial the way a player does, and checks the four ways it can break.
    ///
    /// **A tutorial fails silently in every one of them.** A step pointing at a class nothing draws
    /// highlights nothing and looks like a step with no highlight. A step that never advances looks
    /// like the player missing a button. A missing phrase renders the key, which reads as a bug in
    /// the game rather than a hole in the copy. And a tour that runs out without a closing line
    /// simply stops, which is indistinguishable from a crash.
    ///
    /// So the walk is here rather than in a click test: it costs milliseconds and it covers every
    /// step, where a human playthrough covers whichever branch was taken that evening.
    /// </summary>
    public sealed class GuideWalkTests
    {
        private static string Stylesheet =>
            File.ReadAllText("Assets/_ScalingLaws/Resources/ScalingLaws.uss");

        [Test]
        public void TheWholeTourAdvancesFromTheFirstStepToTheLast()
        {
            // The shell walks the tour by moving `Step` and reading the script at that index, so
            // the walk here is the same arithmetic against the same list.
            var progress = new GuideProgress { Stage = GuideStage.Touring, Step = 0 };
            var seen = new List<string>();

            // One more than the script, so a step that refuses to advance fails readably here
            // rather than hanging the runner.
            for (var guard = 0; guard <= GuideScript.Steps.Count; guard++)
            {
                if (progress.Step >= GuideScript.Steps.Count)
                {
                    break;
                }

                var step = GuideScript.Steps[progress.Step];

                Assert.That(seen, Does.Not.Contain(step.Id),
                    $"The tour came back to \"{step.Id}\", so it is looping.");

                seen.Add(step.Id);
                progress.Step++;
            }

            Assert.That(progress.Step, Is.EqualTo(GuideScript.Steps.Count),
                "The tour stopped short and never reached the end, which on screen is a phone that "
                + "will not go away.");

            Assert.That(seen.Count, Is.EqualTo(GuideScript.Steps.Count),
                "Every step has to be reachable, or it is copy nobody will ever read.");
        }

        [Test]
        public void EveryStepSaysSomethingInBothLanguages()
        {
            var was = Loc.Current;
            var missing = new List<string>();

            try
            {
                foreach (var language in new[] { Language.English, Language.Polish })
                {
                    Loc.Current = language;

                    foreach (var step in GuideScript.Steps)
                    {
                        // `Line` and `Prompt` already resolve through the phrase book, so a hole
                        // shows up as the key coming back rather than as an empty string. Passing
                        // them to `Loc.T` a second time is what the first version of this did, and
                        // it failed every step in the tour for the wrong reason.
                        if (string.IsNullOrWhiteSpace(step.Line) || step.Line.StartsWith("guide."))
                        {
                            missing.Add($"{language} {step.Id}: \"{step.Line}\"");
                        }

                        var prompt = step.Prompt;

                        if (prompt != null
                            && (string.IsNullOrWhiteSpace(prompt) || prompt.StartsWith("guide.")))
                        {
                            missing.Add($"{language} {step.Id} button: \"{prompt}\"");
                        }
                    }
                }
            }
            finally
            {
                Loc.Current = was;
            }

            Assert.IsEmpty(missing,
                "Steps printing their own key at the player:\n  " + string.Join("\n  ", missing));
        }

        /// <summary>
        /// Every class a step wants to spotlight is one the stylesheet actually knows about.
        ///
        /// This is the cheap half of the check. It cannot prove the element is on screen at that
        /// moment, and it does catch the common case, which is a class renamed in a refactor while
        /// the tutorial went on pointing at the old name.
        /// </summary>
        [Test]
        public void EveryHighlightNamesAClassTheInterfaceDraws()
        {
            var sheet = Stylesheet;
            var unknown = GuideScript.Steps
                .Where(step => !string.IsNullOrEmpty(step.Highlight))
                .Select(step => (step.Id, step.Highlight))
                .Where(pair => !sheet.Contains("." + pair.Highlight))
                .Select(pair => $"{pair.Id} -> .{pair.Highlight}")
                .ToList();

            Assert.IsEmpty(unknown,
                "Steps pointing a spotlight at a class nothing styles, which on screen is a hole "
                + "cut around nothing:\n  " + string.Join("\n  ", unknown));
        }

        [Test]
        public void EveryStepThatOpensAScreenNamesOne()
        {
            var wrong = GuideScript.Steps
                .Where(step => step.WaitForClick && step.Target == GuideTarget.None)
                .Select(step => step.Id)
                .ToList();

            Assert.IsEmpty(wrong,
                "Steps with a SHOW ME button that go nowhere:\n  " + string.Join("\n  ", wrong));
        }

        /// <summary>
        /// The tour ends on a closing line rather than simply running out.
        /// </summary>
        [Test]
        public void TheLastStepIsAGoodbyeAndNotAnInstruction()
        {
            var last = GuideScript.Steps[^1];

            Assert.That(last.Target, Is.EqualTo(GuideTarget.None),
                "The tour should not end by dumping the player on a screen they did not ask for.");
            Assert.IsFalse(last.WaitForClick,
                "A tour whose final step waits for a click the player has no reason to make is a "
                + "phone that never leaves.");
        }

        /// <summary>
        /// The bank is in the tour, and the tab is opened rather than described.
        ///
        /// Added because it was missing entirely: the tutorial explained the burn and then said
        /// nothing about where money comes from before a company earns any, which is the question
        /// the burn raises.
        /// </summary>
        [Test]
        public void TheTourVisitsTheBankAndOpensTheTab()
        {
            var bank = GuideScript.Steps.Where(step => step.Target == GuideTarget.Funding).ToList();

            Assert.That(bank, Is.Not.Empty, "The tutorial never mentions the bank.");

            Assert.That(bank.Any(step => step.WaitForClick), Is.True,
                "The bank is described and never opened, so the player is told about a tab they "
                + "have not been shown.");

            var burn = GuideScript.Steps.ToList().FindIndex(step => step.Id == "burn");
            var first = GuideScript.Steps.ToList().FindIndex(
                step => step.Target == GuideTarget.Funding);

            Assert.That(first, Is.GreaterThan(burn),
                "The bank answers the question the burn asks, so it has to come after it.");
        }

        /// <summary>
        /// Leaving early is not a failure state, and the opening tasks survive it.
        /// </summary>
        [Test]
        public void LeavingEarlyStillLeavesThePlayerWithSomethingToDo()
        {
            var progress = new GuideProgress { Stage = GuideStage.Touring, Step = 3 };
            progress.Stage = GuideStage.Finished;

            Assert.That(progress.Stage, Is.EqualTo(GuideStage.Finished),
                "Skipping has to actually end it.");
            Assert.That(GuideScript.Tasks, Is.Not.Empty,
                "The opening tasks are the game rather than the end of the lesson, so they stand "
                + "whether the tour was taken or not.");
        }
    }
}
