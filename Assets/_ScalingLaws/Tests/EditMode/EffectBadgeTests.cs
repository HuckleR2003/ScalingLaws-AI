using System;
using System.Collections.Generic;
using NUnit.Framework;
using ScalingLaws.Core;
using ScalingLaws.Data;
using ScalingLaws.Simulation;
using ScalingLaws.UI;

namespace ScalingLaws.Tests.EditMode
{
    /// <summary>
    /// The badges that say what is temporarily true about the company.
    ///
    /// **`EffectBook` was complete and invisible.** Five kinds, each with a duration, a signed
    /// magnitude, a taper, expiry, save and restore, all four real ones raised correctly by the
    /// simulation, and `DemandMultiplier` read by the market every single day between 0.15x and
    /// 4.0x. Nothing in `Scripts/UI/` referenced `Effects` at all, so a player watched their user
    /// count quadruple and slide back with no word anywhere about why. The eleventh mechanism in
    /// this project finished in the simulation and unreachable from the interface.
    ///
    /// The author has a list of further effects to add. These tests exist so that adding one is a
    /// member, a raise site and two phrase-book keys, and so that forgetting the words fails here
    /// rather than shipping a square with a key printed in it.
    /// </summary>
    public sealed class EffectBadgeTests
    {
        private static IEnumerable<ModelEffectKind> RealKinds()
        {
            foreach (ModelEffectKind kind in Enum.GetValues(typeof(ModelEffectKind)))
            {
                if (kind != ModelEffectKind.None)
                {
                    yield return kind;
                }
            }
        }

        /// <summary>
        /// Every kind has a name, a note and a glyph, in both languages.
        ///
        /// A missing key does not throw in this project, it renders the key, so the failure this
        /// catches is a top bar with `effect.glyph.whatever` printed in a small square.
        /// </summary>
        [Test]
        public void EveryEffectHasWordsInBothLanguages()
        {
            var was = Loc.Current;
            var missing = new List<string>();

            try
            {
                foreach (var language in new[] { Language.English, Language.Polish })
                {
                    Loc.Current = language;

                    foreach (var kind in RealKinds())
                    {
                        if (string.IsNullOrWhiteSpace(EffectBook.NameOf(kind)))
                        {
                            missing.Add($"{language}/{kind}: no name");
                        }

                        if (string.IsNullOrWhiteSpace(EffectBook.NoteFor(kind)))
                        {
                            missing.Add($"{language}/{kind}: no note");
                        }

                        // The glyph is what is actually drawn in the square, and it is the one that
                        // would be silently wrong: a badge printing its own key still looks like a
                        // badge from across the room.
                        var badges = new EffectBadges();
                        var state = new CompanyState("Adco", 7);

                        state.Effects.Add(new ModelEffect(kind, state.Date, 90, 0.2), state.Date);
                        badges.Refresh(state);

                        Assert.AreEqual(1, badges.Count, $"{language}/{kind} drew no badge.");

                        // Asked the way the badge asks it, rather than by rebuilding the mapping
                        // beside it. A missing key renders as itself, so a badge with a raw key in
                        // it still looks like a badge from across the room.
                        var key = EffectBadges.GlyphKeyOf(kind);
                        var glyph = Loc.T(key);

                        if (glyph == key || string.IsNullOrWhiteSpace(glyph))
                        {
                            missing.Add($"{language}/{kind}: no glyph ({key})");
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

        /// <summary>
        /// The strip shows what is running and nothing else, and it goes away when nothing is.
        /// </summary>
        [Test]
        public void TheStripFollowsWhatIsActuallyRunning()
        {
            var state = new CompanyState("Adco", 11);
            var badges = new EffectBadges();

            badges.Refresh(state);
            Assert.AreEqual(0, badges.Count, "Nothing is running, so there is nothing to say.");

            state.Effects.Add(
                new ModelEffect(ModelEffectKind.Viral, state.Date, 30, 0.3), state.Date);
            state.Effects.Add(
                new ModelEffect(ModelEffectKind.Backlash, state.Date, 10, -0.2), state.Date);

            badges.Refresh(state);
            Assert.AreEqual(2, badges.Count);

            // Past the shorter one's window. The book expires it, so the strip has to lose it
            // without being told: this is the half that would rot if the badge kept its own list.
            state.Date = state.Date.AddDays(15);
            badges.Refresh(state);

            Assert.AreEqual(1, badges.Count,
                "The backlash ran out five days ago and the strip is still drawing it.");
        }

        /// <summary>
        /// The strip is capped, and it drops the oldest rather than the newest.
        ///
        /// `Active` returns newest first, which is the right order to lose from: an effect in its
        /// last week is the one the player has already read about.
        /// </summary>
        [Test]
        public void TheStripIsCappedSoItCannotEatTheHeader()
        {
            var state = new CompanyState("Adco", 12);
            var badges = new EffectBadges();

            foreach (var kind in RealKinds())
            {
                state.Effects.Add(new ModelEffect(kind, state.Date, 120, 0.1), state.Date);
            }

            badges.Refresh(state);

            Assert.LessOrEqual(badges.Count, EffectBadges.MostShown);
            Assert.AreEqual(Math.Min(5, EffectBadges.MostShown), badges.Count,
                "Five kinds are running and the cap is five, so all five should be on screen.");
        }

        /// <summary>
        /// The interface actually reads the book.
        ///
        /// The whole reason this fixture exists is that for a long time nothing did. A source sweep,
        /// because an EditMode test builds no shell and cannot see the top bar.
        /// </summary>
        [Test]
        public void SomethingInTheInterfaceDrawsTheEffects()
        {
            var ui = System.IO.Path.Combine(
                UnityEngine.Application.dataPath, "_ScalingLaws", "Scripts", "UI");

            var found = false;

            foreach (var file in System.IO.Directory.GetFiles(ui, "*.cs"))
            {
                if (System.IO.File.ReadAllText(file).Contains("EffectBadges"))
                {
                    found = true;
                    break;
                }
            }

            Assert.IsTrue(found,
                "Nothing in the interface draws the effect badges, so demand is being multiplied "
                + "by up to four and the player is never told why.");
        }
    }
}
