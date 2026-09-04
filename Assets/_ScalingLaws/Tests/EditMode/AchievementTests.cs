using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using ScalingLaws.Data;
using ScalingLaws.Simulation;
using UnityEngine;
using UnityEngine.UIElements;

namespace ScalingLaws.Tests.EditMode
{
    /// <summary>
    /// The achievement table and the function that reads a campaign against it.
    ///
    /// **These are the tests the fix arrived without, and the author said so rather than guessing.**
    /// The note that came with it listed four checks worth writing and declined to write them
    /// because there was no way to run Unity and hand over a test nobody had executed. That is the
    /// right call and this is the other half of it.
    ///
    /// Nothing here touches `AchievementStore`: that one writes to `PlayerPrefs`, which is machine
    /// state shared with the editor, and a test that clears it would take a real player's record
    /// with it. The evaluator is a pure function and is where the interesting claims live anyway.
    /// </summary>
    public sealed class AchievementTests
    {
        private static CompanyState Fresh() => new("Testco", 7);

        // ---- the table -------------------------------------------------------------------------

        /// <summary>
        /// No two entries share an id or a Steam name.
        ///
        /// The API name is the one identifier that has to be right forever: it is what the store
        /// writes and what Steam would key on, and two entries sharing one means earning either
        /// silently earns both.
        /// </summary>
        [Test]
        public void NoTwoAchievementsShareAnIdentity()
        {
            var ids = new HashSet<AchievementId>();
            var names = new HashSet<string>();

            foreach (var definition in AchievementCatalog.All)
            {
                Assert.IsTrue(ids.Add(definition.Id), $"{definition.Id} appears twice.");

                Assert.IsNotEmpty(definition.ApiName, $"{definition.Id} has no Steam name.");
                Assert.IsTrue(names.Add(definition.ApiName),
                    $"{definition.ApiName} is used by two achievements, so earning one earns both.");
            }

            Assert.AreEqual(AchievementCatalog.All.Count, ids.Count);
        }

        /// <summary>
        /// Every entry is reachable by the lookup the store uses.
        ///
        /// `Get` is what `Unlock` calls, and an id with no entry behind it returns null there, which
        /// the store reads as "already earned" and drops on the floor.
        /// </summary>
        [Test]
        public void EveryAchievementCanBeLookedUpById()
        {
            foreach (var definition in AchievementCatalog.All)
            {
                Assert.AreSame(definition, AchievementCatalog.Get(definition.Id),
                    $"{definition.Id} is in the table and cannot be found in it.");
            }
        }

        /// <summary>
        /// A brand new company has earned exactly one thing: starting.
        ///
        /// **This is the test that catches a threshold left at zero.** A metric that reads zero on
        /// a fresh campaign and a threshold of zero satisfy each other, so the achievement would be
        /// handed out on day one to everybody, and nothing else in the suite would notice.
        /// </summary>
        [Test]
        public void AFreshCompanyHasEarnedNothingButStarting()
        {
            var earned = AchievementEvaluator.Satisfied(Fresh(), 0);

            CollectionAssert.AreEquivalent(
                new[] { AchievementId.TimeStart },
                earned.Select(definition => definition.Id).ToList(),
                "A new campaign earned something other than starting one, which means a threshold "
                + "is at or below what an empty company already reads.");
        }

        /// <summary>
        /// Every metric the table names has an arm in the evaluator, and every arm has a user.
        ///
        /// **Read off the source, because a `switch` with a default arm cannot fail loudly.** A
        /// member added to `AchievementMetric` and forgotten in `Read` falls through to `_ => 0.0`
        /// and its achievement is unearnable forever, silently. That is the same shape as era five
        /// drawing under era one's name, which shipped in this project for exactly that reason.
        ///
        /// The other direction is checked too: a metric nothing watches is a number being read for
        /// nobody.
        /// </summary>
        [Test]
        public void EveryMetricHasAnArmInTheEvaluatorAndAnAchievementThatWatchesIt()
        {
            var source = File.ReadAllText(Path.Combine(
                Application.dataPath, "_ScalingLaws", "Scripts", "Simulation",
                "AchievementEvaluator.cs"));

            var watched = new HashSet<AchievementMetric>(
                AchievementCatalog.All.Select(entry => entry.Metric));

            foreach (AchievementMetric metric in Enum.GetValues(typeof(AchievementMetric)))
            {
                if (metric == AchievementMetric.NotWiredYet)
                {
                    continue;
                }

                Assert.That(source, Does.Contain("AchievementMetric." + metric),
                    $"{metric} has no arm in the evaluator, so it reads zero forever and whatever "
                    + "watches it can never be earned.");

                Assert.IsTrue(watched.Contains(metric),
                    $"{metric} is read by the evaluator and no achievement watches it.");
            }
        }

        /// <summary>
        /// The three that nothing counts are never given out, and they say what they are.
        ///
        /// Both halves matter. Handing them out would be an achievement that unlocks on a number
        /// nobody measures; dropping them from the table would lose the copy and the Steam name
        /// that are already written.
        /// </summary>
        [Test]
        public void NothingIsAwardedForAMetricThatIsNotCountedYet()
        {
            var waiting = AchievementCatalog.All.Where(entry => entry.NeedsCounter).ToList();

            Assert.IsNotEmpty(waiting, "The table has no unwired entries, so this guard is stale.");

            foreach (var definition in waiting)
            {
                Assert.AreEqual(AchievementMetric.NotWiredYet, definition.Metric);
            }

            var earned = AchievementEvaluator.Satisfied(Fresh(), 99);

            foreach (var definition in waiting)
            {
                Assert.IsFalse(earned.Contains(definition),
                    $"{definition.ApiName} was awarded on a metric nothing measures.");
            }
        }

        /// <summary>
        /// The bankruptcy count comes from the caller, not from the campaign.
        ///
        /// A company that has folded knows nothing about the ones before it, which is the whole
        /// reason the count lives in `PlayerPrefs`. If the evaluator ever started reading it off
        /// the state, the three survival achievements would reset with every new company.
        /// </summary>
        [Test]
        public void TheBankruptcyCountIsWhateverTheCallerPassesIn()
        {
            var state = Fresh();

            Assert.AreEqual(0.0,
                AchievementEvaluator.Read(state, AchievementMetric.BankruptciesLifetime, 0));

            Assert.AreEqual(7.0,
                AchievementEvaluator.Read(state, AchievementMetric.BankruptciesLifetime, 7));
        }

        /// <summary>A null campaign is an empty answer rather than an exception.</summary>
        [Test]
        public void NoCampaignEarnsNothingAndThrowsNothing()
        {
            Assert.IsEmpty(AchievementEvaluator.Satisfied(null, 4));
            Assert.AreEqual(0.0, AchievementEvaluator.Read(null, AchievementMetric.CashUsd, 0));
        }

        /// <summary>
        /// Every number in `AchievementMoments` is a real catalog id, and the shell drains them.
        ///
        /// The moments list carries plain integers so that `Simulation/` never has to name an
        /// achievement, and the cost of that indirection is exactly this: nothing but a test can
        /// tell that the number on one side is the entry on the other. A renamed or deleted
        /// achievement would leave a rule announcing a moment nobody can claim.
        /// </summary>
        [Test]
        public void EveryMomentTheRulesCanAnnounceIsARealAchievement()
        {
            foreach (var field in typeof(AchievementMoments).GetFields())
            {
                var value = (int)field.GetValue(null);

                Assert.IsNotNull(AchievementCatalog.Get((AchievementId)value),
                    $"AchievementMoments.{field.Name} is {value}, which is no achievement.");
            }

            var shell = File.ReadAllText(Path.Combine(
                Application.dataPath, "_ScalingLaws", "Scripts", "UI", "GameShell.cs"));

            Assert.That(shell, Does.Contain("AchievementMomentsToday"),
                "Nothing in the interface reads the moments the rules announce, so the list is "
                + "written every day and cleared unread the next.");
        }

        /// <summary>
        /// The rules announce the inspection that held.
        ///
        /// A source check, because reaching this in a test means rolling a saving throw that is
        /// deliberately unlikely, and asserting a coin lands the right way is not a test.
        /// </summary>
        [Test]
        public void SurvivingAnInspectionIsAnnounced()
        {
            var rules = File.ReadAllText(Path.Combine(
                Application.dataPath, "_ScalingLaws", "Scripts", "Simulation",
                "CompanySimulation.cs"));

            Assert.That(rules, Does.Contain("AchievementMoments.RegulatorHeld"),
                "The one achievement that has to be announced by the rules is not announced, so it "
                + "can never be earned.");
        }

        /// <summary>
        /// Every name and note resolves in both languages.
        ///
        /// Ninety four keys read through `NameKey` and `NoteKey`, which are fields rather than
        /// literals, so `LocalisationTests` cannot see them: it reads source text. This is the same
        /// blind spot `LocalisationCoverageTests` exists for.
        /// </summary>
        [Test]
        public void EveryAchievementSpeaksBothLanguages()
        {
            var before = Loc.Current;

            try
            {
                foreach (var language in new[] { Language.English, Language.Polish })
                {
                    Loc.Current = language;

                    foreach (var definition in AchievementCatalog.All)
                    {
                        var name = Loc.T(definition.NameKey);
                        var note = Loc.T(definition.NoteKey);

                        Assert.AreNotEqual(definition.NameKey, name,
                            $"{definition.ApiName} renders its own key as a name in {language}.");

                        Assert.AreNotEqual(definition.NoteKey, note,
                            $"{definition.ApiName} renders its own key as a note in {language}.");

                        Assert.IsNotEmpty(name);
                        Assert.IsNotEmpty(note);
                    }
                }
            }
            finally
            {
                Loc.Current = before;
            }
        }

        /// <summary>
        /// The page draws every achievement in the table.
        ///
        /// **The twelfth unreachable mechanism was one screen away.** The catalog, the evaluator and
        /// the store arrived complete, and unlocking played a sound and showed nothing, so this
        /// asserts the row count rather than the fact that a page was built: a page that renders one
        /// group and drops nine would pass a smoke test and fail a player.
        /// </summary>
        [Test]
        public void TheAchievementsPageDrawsEveryEntry()
        {
            var page = new ScalingLaws.UI.AchievementsPage().Build();

            var rows = page.Query(className: "achrow").ToList();

            Assert.AreEqual(AchievementCatalog.All.Count, rows.Count,
                "The page and the table disagree about how many achievements there are.");

            var headings = page.Query(className: "achgroup__heading").ToList();

            Assert.AreEqual(
                AchievementCatalog.All.Select(entry => entry.Group).Distinct().Count(),
                headings.Count,
                "A group in the table has no heading on the page, so its rows read as belonging "
                + "to whichever group is above them.");
        }

    }
}
