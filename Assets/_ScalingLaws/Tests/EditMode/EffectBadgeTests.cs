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
        /// The remaining time on a badge is a guess, and it is the same guess every time.
        ///
        /// **Both halves matter and they pull against each other.** A figure re-rolled per repaint
        /// reads as a broken counter; one re-rolled per day can be averaged out over a week and the
        /// true number recovered. The skew is derived from what makes the effect that effect, so it
        /// survives a save, replays identically, and cannot be sampled.
        /// </summary>
        [Test]
        public void TheRemainingTimeIsAGuessAndItDoesNotMoveAround()
        {
            var start = GameDate.FromCalendar(2024, 3, 1);
            var effect = new ModelEffect(ModelEffectKind.Viral, start, 100, 0.3);

            var first = effect.EstimatedDaysLeft(start);

            for (var again = 0; again < 20; again++)
            {
                Assert.AreEqual(first, effect.EstimatedDaysLeft(start),
                    "Asking twice on the same day gave two answers, so this is a dice roll on the "
                    + "repaint rather than an estimate.");
            }

            // The same effect rebuilt from what a save stores has to guess the same thing, or a
            // reload would move the number the player is planning around.
            var reloaded = new ModelEffect(ModelEffectKind.Viral, start, 100, 0.3);
            Assert.AreEqual(first, reloaded.EstimatedDaysLeft(start));

            Assert.That(first, Is.InRange(
                (int)(100 * (1.0 - ModelEffect.EstimateTolerance)),
                (int)System.Math.Ceiling(100 * (1.0 + ModelEffect.EstimateTolerance))),
                "The guess is outside the tolerance it is supposed to stay inside.");
        }

        /// <summary>
        /// Across many windows the guess is wrong in both directions, and by a real amount.
        ///
        /// A skew that was always positive would teach the player to subtract a fixed fraction, and
        /// one that never reached the tolerance would make the constant a lie.
        /// </summary>
        [Test]
        public void TheGuessIsWrongBothWaysAndSometimesByALot()
        {
            var high = 0;
            var low = 0;
            var far = 0;

            for (var day = 0; day < 400; day++)
            {
                var start = GameDate.Start.AddDays(day);
                var effect = new ModelEffect(ModelEffectKind.Backlash, start, 200, -0.2);
                var guess = effect.EstimatedDaysLeft(start);

                if (guess > 200)
                {
                    high++;
                }

                if (guess < 200)
                {
                    low++;
                }

                if (System.Math.Abs(guess - 200) > 200 * 0.25)
                {
                    far++;
                }
            }

            Assert.Greater(high, 40, "The estimate is never optimistic.");
            Assert.Greater(low, 40, "The estimate is never pessimistic.");
            Assert.Greater(far, 40,
                "Nothing is ever more than a quarter out, so the forty per cent tolerance is not "
                + "the number this is actually using.");
        }

        /// <summary>
        /// A backlash presses on the fan base, and it presses on the target rather than the count.
        ///
        /// **That distinction is the feature.** Taking fans directly would be a fine the player pays
        /// and cannot answer. Lowering what the fan base is pulling toward means the ordinary things
        /// that earn a following genuinely fight it, which is the whole reason the author asked for
        /// it this way round.
        /// </summary>
        [Test]
        public void ABacklashHoldsTheFanBaseDownAndFadesWithTheRestOfIt()
        {
            var state = new CompanyState("Adco", 21);

            Assert.AreEqual(0.0, state.Effects.FanPressure(state.Date),
                "Nothing has gone wrong, so nothing is holding the fans down.");

            // The worst severity the incident model produces, so the pressure is at its ceiling.
            state.Effects.Add(
                new ModelEffect(ModelEffectKind.Backlash, state.Date, 300,
                    -EffectBook.BacklashWorstMagnitude),
                state.Date);

            var atTheStart = state.Effects.FanPressure(state.Date);

            Assert.That(atTheStart, Is.EqualTo(EffectBook.BacklashFanLoss).Within(0.001),
                "A severe backlash should reach the full quarter it is allowed to take.");

            // A milder one takes proportionally less, or severity would stop meaning anything.
            var milder = new CompanyState("Adco", 22);
            milder.Effects.Add(
                new ModelEffect(ModelEffectKind.Backlash, milder.Date, 300, -0.09), milder.Date);

            Assert.Less(milder.Effects.FanPressure(milder.Date), atTheStart);

            // And it fades on the same curve the demand penalty does. `Strength` tapers over the
            // last quarter of the window, so day 290 of 300 is well inside the taper.
            state.Date = state.Date.AddDays(290);

            Assert.Less(state.Effects.FanPressure(state.Date), atTheStart,
                "The pressure is still at full strength ten days from the end, so it stops on a "
                + "Tuesday rather than fading.");

            state.Date = state.Date.AddDays(20);

            Assert.AreEqual(0.0, state.Effects.FanPressure(state.Date),
                "The backlash is over and it is still holding the fans down.");
        }

        /// <summary>
        /// A backlash runs somewhere between four months and thirteen, and not the same length twice.
        ///
        /// It used to be `45 + severity * 200`, so a player who had seen one severe incident knew
        /// the next one would last 113 days.
        /// </summary>
        [Test]
        public void HowLongABacklashRunsIsDrawnRatherThanDerived()
        {
            Assert.Greater(EffectBook.BacklashDaysHigh, EffectBook.BacklashDaysLow * 2,
                "The band is too narrow to be a surprise.");

            Assert.That(EffectBook.BacklashDaysLow, Is.InRange(110, 130),
                "Four months, give or take.");

            Assert.That(EffectBook.BacklashDaysHigh, Is.InRange(380, 400),
                "Thirteen months, give or take.");

            // **And the constants are actually the ones an incident uses.** Two numbers in a
            // catalog that the code beside them does not read is the shape of half the faults in
            // this project, so the band is measured through a real penalty rather than asserted on
            // its own definition.
            var lengths = new HashSet<int>();

            for (uint seed = 1; seed <= 12; seed++)
            {
                var simulation = new CompanySimulation(new CompanyState("Adco", seed));
                var date = simulation.State.Date;

                simulation.State.AddDeployedModel(new DeployedModel("Muse",
                    ArchitectureId.DenseTransformer, 40.0, date, 2e10, 1.0, ModelType.General));

                // An inspection that will close on its own. The penalty is decided when the file
                // closes rather than when it opens, which is why driving days is the only way in.
                simulation.State.PendingAction = new RegulatoryAction(
                    new SafetyIncident(IncidentSeverity.Severe, date,
                        "Personal data was reachable from a public endpoint.",
                        reputationLoss: 0.10, fineUsd: 90_000_000, forcedWithdrawal: false),
                    date, "Muse");

                simulation.Advance(RegulatoryAction.InspectionDays + 1);

                var backlash = simulation.State.Effects.Find(
                    ModelEffectKind.Backlash, simulation.State.Date);

                Assert.IsNotNull(backlash,
                    $"Seed {seed}: an inspection closed with a penalty and opened no backlash.");

                Assert.That(backlash.Days,
                    Is.InRange(EffectBook.BacklashDaysLow, EffectBook.BacklashDaysHigh),
                    $"Seed {seed}: the length is outside the band the catalog states.");

                lengths.Add(backlash.Days);
            }

            Assert.Greater(lengths.Count, 3,
                "Twelve incidents produced almost the same length every time, so it is being "
                + "derived from the severity again and a player can plan around it.");
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
