using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using ScalingLaws.Data;

namespace ScalingLaws.Tests.EditMode
{
    /// <summary>
    /// The phrase-book keys that are built rather than written, and are therefore invisible to the
    /// guard that covers everything else.
    ///
    /// **`LocalisationTests.EveryKeyTheInterfaceAsksForExists` can only read literals.** It walks
    /// the source for `Loc.T("...")`, which is the right check for the nine hundred keys asked for
    /// that way and completely blind to `Loc.T(stem + ".title")`.
    ///
    /// Three places build keys: `TechNotes`, `SkillNotes` and the research tree's descriptions.
    /// Between them that is around three hundred keys and the largest block of player-facing writing
    /// in the game, with no coverage at all until this fixture. Rename a stem, or add a note and
    /// forget one of its five lines, and the screen ships the key as the sentence while every test
    /// stays green.
    ///
    /// **This resolves the real accessors rather than re-deriving the keys.** A test that rebuilt
    /// `stem + ".title"` itself would be a second copy of the thing it is checking, and would pass
    /// for the same wrong reason the code failed.
    /// </summary>
    public sealed class BuiltKeyTests
    {
        /// <summary>
        /// What a miss looks like coming back out of the phrase book.
        ///
        /// `Loc.T` returns the key when it cannot find one, so a missing entry reads as
        /// `tech.sparsity.title` sitting on the screen where a paragraph should be.
        /// </summary>
        private static bool LooksLikeAKey(string text) =>
            !string.IsNullOrWhiteSpace(text)
            && !text.Contains(' ')
            && text.Contains('.')
            && text.ToLowerInvariant() == text;

        private static void Check(string what, string value, ICollection<string> broken)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                broken.Add($"{what}: empty");
                return;
            }

            if (LooksLikeAKey(value))
            {
                broken.Add($"{what}: reads as the key, \"{value}\"");
            }
        }

        private static void CheckNote(string what, string title, string body, string affects,
            string high, string low, ICollection<string> broken)
        {
            Check(what + ".title", title, broken);
            Check(what + ".what", body, broken);
            Check(what + ".affects", affects, broken);
            Check(what + ".high", high, broken);
            Check(what + ".low", low, broken);
        }

        /// <summary>
        /// Every technology note resolves in both languages.
        ///
        /// Found by reflection rather than by a hand-written list, so a note added tomorrow is
        /// covered without anybody remembering to add it here. A list would drift the first time
        /// somebody added the fourteenth note.
        /// </summary>
        [Test]
        public void EveryTechnologyNoteResolvesInBothLanguages()
        {
            var before = Loc.Current;
            var broken = new List<string>();

            try
            {
                foreach (var language in new[] { Language.English, Language.Polish })
                {
                    Loc.Current = language;

                    var notes = typeof(TechNotes)
                        .GetProperties(BindingFlags.Public | BindingFlags.Static)
                        .Where(property => property.PropertyType == typeof(TechNotes.Note));

                    var found = 0;

                    foreach (var property in notes)
                    {
                        found++;
                        var note = (TechNotes.Note)property.GetValue(null);

                        CheckNote($"{language}/{property.Name}", note.Title, note.What,
                            note.Affects, note.High, note.Low, broken);
                    }

                    Assert.That(found, Is.GreaterThan(8),
                        "Reflection found almost no notes, so this fixture is checking nothing.");
                }
            }
            finally
            {
                Loc.Current = before;
            }

            Assert.IsEmpty(broken,
                "These are built by concatenation, so the literal guard cannot see them:\n  "
                + string.Join("\n  ", broken));
        }

        /// <summary>The seven skill notes, same shape and the same blind spot.</summary>
        [Test]
        public void EverySkillNoteResolvesInBothLanguages()
        {
            var before = Loc.Current;
            var broken = new List<string>();

            try
            {
                foreach (var language in new[] { Language.English, Language.Polish })
                {
                    Loc.Current = language;

                    foreach (PlayerSkill skill in Enum.GetValues(typeof(PlayerSkill)))
                    {
                        if (skill == PlayerSkill.None)
                        {
                            continue;
                        }

                        var note = SkillNotes.For(skill);

                        CheckNote($"{language}/{skill}", note.Title, note.What, note.Affects,
                            note.High, note.Low, broken);
                    }
                }
            }
            finally
            {
                Loc.Current = before;
            }

            Assert.IsEmpty(broken, string.Join("\n  ", broken));
        }

        /// <summary>
        /// Every research node's name and description resolve.
        ///
        /// Fifty nodes, and the description is the longest single piece of writing a player reads on
        /// that screen. It is built from the node's id, so renaming an id silently empties it.
        /// </summary>
        [Test]
        public void EveryResearchNodeReadsAsProseInBothLanguages()
        {
            var before = Loc.Current;
            var broken = new List<string>();

            try
            {
                foreach (var language in new[] { Language.English, Language.Polish })
                {
                    Loc.Current = language;

                    foreach (var node in ResearchTree.All)
                    {
                        Check($"{language}/{node.Id}.name", node.DisplayName, broken);
                        Check($"{language}/{node.Id}.desc", node.Description, broken);
                    }
                }
            }
            finally
            {
                Loc.Current = before;
            }

            Assert.IsEmpty(broken,
                "A node whose description reads as its own id is a node nobody can evaluate:\n  "
                + string.Join("\n  ", broken));
        }

        /// <summary>
        /// Every benefit and every relation band resolves.
        ///
        /// These are written as literals inside their catalogs rather than concatenated, so the
        /// existing guard does cover them. They are here because the failure is the same shape and
        /// one fixture for "does the player read prose or a key" is easier to reach for than two.
        /// </summary>
        [Test]
        public void TheNewCatalogsReadAsProseInBothLanguages()
        {
            var before = Loc.Current;
            var broken = new List<string>();

            try
            {
                foreach (var language in new[] { Language.English, Language.Polish })
                {
                    Loc.Current = language;

                    foreach (var benefit in BenefitCatalog.All)
                    {
                        Check($"{language}/{benefit.Benefit}.name", benefit.DisplayName, broken);
                        Check($"{language}/{benefit.Benefit}.note", benefit.Note, broken);
                    }

                    foreach (Simulation.RelationBand band
                        in Enum.GetValues(typeof(Simulation.RelationBand)))
                    {
                        Check($"{language}/{band}", Simulation.RivalRelations.NameOf(band), broken);
                        Check($"{language}/{band}.note",
                            Simulation.RivalRelations.NoteFor(band), broken);
                    }

                    foreach (Simulation.ModelEffectKind kind
                        in Enum.GetValues(typeof(Simulation.ModelEffectKind)))
                    {
                        if (kind == Simulation.ModelEffectKind.None)
                        {
                            continue;
                        }

                        Check($"{language}/{kind}", Simulation.EffectBook.NameOf(kind), broken);
                        Check($"{language}/{kind}.note",
                            Simulation.EffectBook.NoteFor(kind), broken);
                    }
                }
            }
            finally
            {
                Loc.Current = before;
            }

            Assert.IsEmpty(broken, string.Join("\n  ", broken));
        }
    }
}
