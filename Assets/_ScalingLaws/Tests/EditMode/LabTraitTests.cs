using System.Collections.Generic;
using NUnit.Framework;
using ScalingLaws.Core;
using ScalingLaws.Data;
using ScalingLaws.Simulation;

namespace ScalingLaws.Tests.EditMode
{
    /// <summary>
    /// What kind of company each rival is, worked out from what it has actually done.
    ///
    /// **Derived and never stored**, so a badge cannot contradict the behaviour it describes, no
    /// save version is needed, and nobody has to remember to update a field the day a strategy
    /// changes. The tests here are mostly about the two ways that could still go wrong: a trait
    /// that can never occur, and a trait that reads the future.
    /// </summary>
    public sealed class LabTraitTests
    {
        /// <summary>Every lab on the board, None excluded.</summary>
        private static IEnumerable<CompetitorId> AllLabs()
        {
            foreach (CompetitorId lab in System.Enum.GetValues(typeof(CompetitorId)))
            {
                if (lab != CompetitorId.None)
                {
                    yield return lab;
                }
            }
        }

        private static CompanySimulation Campaign(uint seed = 7)
        {
            var state = new CompanyState("Adco", seed);
            return new CompanySimulation(state);
        }

        /// <summary>
        /// Every trait in the enum happens to somebody, at some point, in a real campaign.
        ///
        /// **This is the guard that matters.** A trait nothing can produce is the same fault as a
        /// mechanism nothing can reach, and this project has shipped eleven of those. It walks the
        /// fourteen labs across the whole campaign rather than asserting against a table, so a
        /// trait whose condition drifts out of reach fails here rather than quietly never showing.
        /// </summary>
        [Test]
        public void EveryTraitCanActuallyHappenToSomebody()
        {
            var seen = new HashSet<LabTrait>();

            foreach (var seed in new uint[] { 3, 11, 29, 71 })
            {
                var simulation = Campaign(seed);
                var state = simulation.State;

                foreach (CompetitorId lab in AllLabs())
                {
                    // A relationship bad enough to be called hostile, on one lab, so that branch is
                    // reachable without playing fourteen years of spite.
                    if (lab == CompetitorId.OpenAi)
                    {
                        state.Relations.Record(lab, state.Date, RivalRelations.Worst,
                            "relation.reason.smear", string.Empty);
                    }

                    for (var year = 2022; year <= 2030; year++)
                    {
                        state.Date = GameDate.FromCalendar(year, 6, 1);

                        foreach (var trait in LabTraits.For(lab, state))
                        {
                            seen.Add(trait);
                        }
                    }
                }
            }

            var missing = new List<LabTrait>();

            foreach (LabTrait trait in System.Enum.GetValues(typeof(LabTrait)))
            {
                if (trait != LabTrait.None && !seen.Contains(trait))
                {
                    missing.Add(trait);
                }
            }

            Assert.IsEmpty(missing,
                "These traits exist in the enum and nothing in a real campaign produces them, so "
                + "they are words nobody will ever read:\n  "
                + string.Join("\n  ", missing));
        }

        /// <summary>
        /// A card opened early cannot say how the company ends.
        ///
        /// `ChaptersBy` exists because a dossier opened in 2023 must not mention a 2024 collapse.
        /// A badge reading "wobbling" over a lab that is still winning would give the whole field
        /// away on the first card the player ever opens.
        /// </summary>
        [Test]
        public void NoTraitGivesAwayHowALabEnds()
        {
            var simulation = Campaign(5);
            var state = simulation.State;

            state.Date = GameDate.Start;

            var spoilers = new List<string>();

            foreach (CompetitorId lab in AllLabs())
            {
                foreach (var trait in LabTraits.For(lab, state))
                {
                    if (trait is LabTrait.Wobbling or LabTrait.Absorbed)
                    {
                        spoilers.Add($"{lab} is already {trait} on day one");
                    }
                }
            }

            Assert.IsEmpty(spoilers,
                "On 1 January 2022 nothing has happened to anybody yet:\n  "
                + string.Join("\n  ", spoilers));
        }

        /// <summary>Three at most, or the card is a table rather than a character.</summary>
        [Test]
        public void NoLabEverShowsMoreThanThreeBadges()
        {
            var simulation = Campaign(13);
            var state = simulation.State;

            foreach (CompetitorId lab in AllLabs())
            {
                state.Relations.Record(lab, state.Date, RivalRelations.Worst,
                    "relation.reason.smear", string.Empty);

                for (var year = 2022; year <= 2032; year++)
                {
                    state.Date = GameDate.FromCalendar(year, 3, 9);

                    Assert.LessOrEqual(LabTraits.For(lab, state).Count, LabTraits.MostShown,
                        $"{lab} in {year} wants more badges than the card is allowed to show.");
                }
            }
        }

        /// <summary>
        /// Every lab says something about itself, always. A blank card is worse than a plain one.
        /// </summary>
        [Test]
        public void EveryLabHasSomethingToSayAboutItself()
        {
            var simulation = Campaign(17);
            var state = simulation.State;
            state.Date = GameDate.FromCalendar(2024, 1, 1);

            foreach (CompetitorId lab in AllLabs())
            {
                Assert.IsNotEmpty(LabTraits.For(lab, state),
                    $"{lab} has no traits at all, so its card has an empty section on it.");
            }
        }

        /// <summary>
        /// Every trait has a word and a sentence, in both languages.
        ///
        /// A missing key renders as itself here, so the failure is a chip on a rival's card reading
        /// `labtrait.fearless`.
        /// </summary>
        [Test]
        public void EveryTraitHasWordsInBothLanguages()
        {
            var was = Loc.Current;
            var missing = new List<string>();

            try
            {
                foreach (var language in new[] { Language.English, Language.Polish })
                {
                    Loc.Current = language;

                    foreach (LabTrait trait in System.Enum.GetValues(typeof(LabTrait)))
                    {
                        if (trait == LabTrait.None)
                        {
                            continue;
                        }

                        var name = LabTraits.NameOf(trait);
                        var note = LabTraits.NoteFor(trait);

                        if (string.IsNullOrWhiteSpace(name) || name.StartsWith("labtrait."))
                        {
                            missing.Add($"{language}/{trait}: no word");
                        }

                        if (string.IsNullOrWhiteSpace(note) || note.StartsWith("labtrait."))
                        {
                            missing.Add($"{language}/{trait}: no sentence");
                        }
                    }
                }
            }
            finally
            {
                Loc.Current = was;
            }

            Assert.IsEmpty(missing, string.Join("\n  ", missing));
        }
    }
}
