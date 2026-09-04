using System;
using System.Collections.Generic;
using NUnit.Framework;
using ScalingLaws.Data;
using ScalingLaws.Simulation;

namespace ScalingLaws.Tests.EditMode
{
    /// <summary>
    /// The nineteen per cent of the phrase book the existing guard cannot see.
    ///
    /// **`LocalisationTests.EveryKeyTheInterfaceAsksForExists` reads source text**, so it can only
    /// check keys that appear in it as literals. A key built by concatenation never does. An audit
    /// counted the gap: of 1,923 keys, 1,332 appear as literals, 223 are dead copy, and **368 are
    /// asked for by a stem plus a suffix and are invisible to every check in the project**.
    ///
    /// The blind spots are not obscure corners. `TechNotes` is 195 of them - the "(i)" cards on
    /// every control in the game. `SkillNotes` is 35, the seven skills a new player distributes two
    /// hundred points across. `GrantCatalog` is 36 and `WorldEventCatalog` 66.
    ///
    /// **This checks behaviour rather than source text**, which is why it can cover them all at
    /// once and why it keeps working as they grow: it resolves what the catalogs actually return and
    /// fails if any of it came back as its own key. That is what a missing phrase looks like on
    /// screen, and `Loc.T` deliberately does not throw - it renders the key, so `arch.tilte` once
    /// shipped as the visible word.
    ///
    /// Both languages, because a key present in English and absent in Polish is the shape this book
    /// has drifted into before: the two dictionaries were 129 entries apart when they were merged.
    /// </summary>
    public sealed class LocalisationCoverageTests
    {
        /// <summary>
        /// Something that came back as its own key never made it into the book.
        ///
        /// Also catches empty, which is the other way a phrase goes missing without anybody
        /// noticing: a blank label is a layout that looks slightly wrong rather than an error.
        /// </summary>
        private static void Resolved(string what, string value, List<string> missing)
        {
            if (string.IsNullOrWhiteSpace(value) || value.Contains(" ") == false && value.Contains(".")
                && value == value.ToLowerInvariant())
            {
                missing.Add($"{what} = \"{value}\"");
            }
        }

        private static void CheckNote(string what, TechNotes.Note note, List<string> missing)
        {
            Resolved(what + ".title", note.Title, missing);
            Resolved(what + ".what", note.What, missing);
            Resolved(what + ".affects", note.Affects, missing);
            Resolved(what + ".high", note.High, missing);
            Resolved(what + ".low", note.Low, missing);
        }

        private static void InBothLanguages(Action<List<string>> check)
        {
            var before = Loc.Current;

            try
            {
                foreach (var language in new[] { Language.English, Language.Polish })
                {
                    Loc.Current = language;

                    var missing = new List<string>();
                    check(missing);

                    CollectionAssert.IsEmpty(missing,
                        $"These resolved to their own key in {language}, which is what a missing "
                        + "phrase looks like on screen: " + string.Join("; ", missing));
                }
            }
            finally
            {
                Loc.Current = before;
            }
        }

        /// <summary>
        /// Every "(i)" card in the game, all forty of them, five phrases each.
        ///
        /// The largest blind spot by far, and the one that matters most: these cards are the only
        /// place several controls explain what they do at all.
        /// </summary>
        [Test]
        public void EveryTechNoteHasAllFiveOfItsPhrases()
        {
            InBothLanguages(missing =>
            {
                foreach (var property in typeof(TechNotes).GetProperties())
                {
                    if (property.PropertyType != typeof(TechNotes.Note))
                    {
                        continue;
                    }

                    CheckNote("TechNotes." + property.Name,
                        (TechNotes.Note)property.GetValue(null), missing);
                }
            });
        }

        /// <summary>The seven skills the creator asks a new player to understand.</summary>
        [Test]
        public void EverySkillNoteHasAllFiveOfItsPhrases()
        {
            InBothLanguages(missing =>
            {
                foreach (PlayerSkill skill in Enum.GetValues(typeof(PlayerSkill)))
                {
                    CheckNote("SkillNotes." + skill, SkillNotes.For(skill), missing);
                }
            });
        }

        /// <summary>
        /// Every grant's name, body and terms.
        ///
        /// These keys are built as `"grant.access." + part`, which is the pattern CLAUDE.md forbids
        /// by name. Writing them out whole would be thirty six lines; checking that they resolve is
        /// stronger, because it tests what the player sees rather than what the source says.
        /// </summary>
        [Test]
        public void EveryGrantHasItsNameBodyAndTerms()
        {
            InBothLanguages(missing =>
            {
                foreach (var grant in GrantCatalog.All)
                {
                    Resolved($"{grant.Id}.name", Loc.T(grant.NameKey), missing);
                    Resolved($"{grant.Id}.body", Loc.T(grant.BodyKey), missing);
                    Resolved($"{grant.Id}.terms", Loc.T(grant.TermsKey), missing);
                }
            });
        }

        /// <summary>Every rival trait's badge and the sentence under it.</summary>
        [Test]
        public void EveryLabTraitHasABadgeAndANote()
        {
            InBothLanguages(missing =>
            {
                foreach (LabTrait trait in Enum.GetValues(typeof(LabTrait)))
                {
                    if (trait == LabTrait.None)
                    {
                        continue;
                    }

                    Resolved($"{trait}.name", LabTraits.NameOf(trait), missing);
                    Resolved($"{trait}.note", LabTraits.NoteFor(trait), missing);
                }
            });
        }

        /// <summary>
        /// Every world event's headline and body, on the day it fires.
        ///
        /// Sixty six keys, and the only thing that ever reads them is the wire on one particular
        /// date, which means a missing one would have surfaced years into a campaign as a headline
        /// reading `world.embargo.head`.
        /// </summary>
        [Test]
        public void EveryWorldEventHasAHeadlineAndABody()
        {
            InBothLanguages(missing =>
            {
                foreach (var entry in WorldEventCatalog.All)
                {
                    Resolved($"{entry.Key}.head", Loc.T(entry.Key + ".head"), missing);
                    Resolved($"{entry.Key}.body", Loc.T(entry.Key + ".body"), missing);
                }
            });
        }
    }
}
