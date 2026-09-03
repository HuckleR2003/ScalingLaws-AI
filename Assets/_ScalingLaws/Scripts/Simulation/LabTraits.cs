using System.Collections.Generic;
using ScalingLaws.Core;
using ScalingLaws.Data;

namespace ScalingLaws.Simulation
{
    /// <summary>
    /// What kind of company a rival is, in one word.
    ///
    /// The board says who is ahead. It has never said what any of them is *like*, which is most of
    /// what makes a field of fourteen interesting and none of which was reachable: the dossier
    /// carries it in prose, and prose is what a player skims.
    /// </summary>
    public enum LabTrait
    {
        None = 0,

        /// <summary>Ships the biggest thing it can afford the day it can afford it.</summary>
        Fearless = 1,

        /// <summary>Waits for the next generation of silicon, then steps over everybody.</summary>
        Patient = 2,

        /// <summary>Wins on price. Whatever you charge, they charge less.</summary>
        Undercutting = 3,

        /// <summary>Gives the weights away and takes the reach instead of the margin.</summary>
        OpenHanded = 4,

        /// <summary>Sells to institutions. Slower, steadier, and hard to dislodge.</summary>
        Institutional = 5,

        // `Imitator` sat here, for `CompetitorStrategy.FastFollower`. **No lab in
        // `CompetitorField.Strategies` is assigned that strategy**, so the trait could never occur
        // and `EveryTraitCanActuallyHappenToSomebody` said so on its first run. The value 6 is left
        // unused rather than reassigned: the day a lab takes that brief, the trait comes back here.

        /// <summary>Money behind it that its own revenue does not explain.</summary>
        DeepPockets = 7,

        /// <summary>Opening everywhere at once.</summary>
        Expanding = 8,

        /// <summary>Publicly in trouble. Still trading, no longer setting the pace.</summary>
        Wobbling = 9,

        /// <summary>The people and the models went somewhere larger.</summary>
        Absorbed = 10,

        /// <summary>Competing against your interests on purpose.</summary>
        Hostile = 11,

        /// <summary>Old enough to have been here before any of this was fashionable.</summary>
        Veteran = 12,

        /// <summary>Started after the race did. Everything it has, it built in public.</summary>
        Newcomer = 13,

        /// <summary>Has been through something public and is still here.</summary>
        Scarred = 14,

        /// <summary>In front on capability today, whoever was in front last year.</summary>
        Leading = 15,

        /// <summary>Has not shipped anything in a long time.</summary>
        Quiet = 16
    }

    /// <summary>
    /// The traits a lab has today, worked out from what it has actually done.
    ///
    /// **Derived, never stored, and that is the whole design.** A field on the dossier would be a
    /// second statement about a company that its behaviour could quietly contradict, it would need
    /// a save version, and somebody would forget to update it the day a strategy changed. This
    /// reads the strategy it is running, the model it has live, the chapters that have landed, how
    /// far it has expanded and how it feels about the player. All of those are already true; none
    /// of them can go stale. Same rule <see cref="RivalExpansion"/> follows for the same reason.
    ///
    /// **Nothing here may look into the future.** `ChaptersBy` exists because a dossier opened in
    /// 2023 must not mention a 2024 collapse, and a badge reading "wobbling" over a lab that is
    /// still winning would give the whole field away on the first card the player opens. Every
    /// history question here goes through the date.
    /// </summary>
    public static class LabTraits
    {
        /// <summary>
        /// Most badges on one card.
        ///
        /// Seven of these is not a character, it is a table. Three is what a player reads without
        /// deciding to, and the ordering below spends them on the unusual ones.
        /// </summary>
        public const int MostShown = 3;

        /// <summary>Expansion level at which a lab counts as backed rather than merely funded.</summary>
        public const int BackedAtLevel = 2;

        /// <summary>And the level at which the expansion is the story rather than a detail.</summary>
        public const int ExpandingAtLevel = 3;

        /// <summary>A live model at or under this share of the going rate is undercutting.</summary>
        public const double UndercuttingAtOrBelow = 0.5;

        /// <summary>
        /// Founded this many years before the campaign opens to count as a veteran.
        ///
        /// Three years. The game starts in 2022, so this is everybody who was working on it before
        /// there was a market to work on it for.
        /// </summary>
        public const int VeteranFoundedBefore = 2019;

        /// <summary>And founded in the opening year or later to count as a newcomer.</summary>
        public const int NewcomerFoundedFrom = 2021;

        /// <summary>Days without a release past which a lab has gone quiet.</summary>
        public const int QuietAfterDays = 540;

        /// <summary>How far ahead of the second-best a lab has to be to be called leading.</summary>
        public const double LeadingBy = 1.5;

        /// <summary>
        /// Everything true about a lab today, most distinctive first, capped at
        /// <see cref="MostShown"/>.
        /// </summary>
        public static List<LabTrait> For(CompetitorId lab, CompanyState state)
        {
            var found = new List<LabTrait>(MostShown);

            if (state == null)
            {
                return found;
            }

            var date = state.Date;

            // **The rare and the personal first.** A lab coming apart, or one that has decided it
            // is against you, is the thing a player opened this card to find out. Its strategy is
            // the last line, because every lab has one of those.
            var known = LabDossiers.TryGet(lab, out var dossier);

            if (known)
            {
                if (dossier.Fate == LabFate.Absorbed && HasLanded(dossier, LabChapterKind.Exit, date))
                {
                    found.Add(LabTrait.Absorbed);
                }
                else if (dossier.Fate == LabFate.Struggling
                    && (HasLanded(dossier, LabChapterKind.Setback, date)
                        || HasLanded(dossier, LabChapterKind.Scandal, date)))
                {
                    found.Add(LabTrait.Wobbling);
                }
            }

            // **Hostile or worse**, not exactly hostile. The bands run Rivalry, Hostile, Tense,
            // Neutral, Friendly, so `== Hostile` skipped the one band that means a lab is actively
            // looking for a way to cost the player something. The bar directly above says which of
            // the two it is; the badge only has to say that it is one of them.
            if (RivalRelations.BandFor(state.Relations.With(lab)) <= RelationBand.Hostile)
            {
                Take(found, LabTrait.Hostile);
            }

            var level = RivalExpansion.LevelOn(state.RosterSeed, lab, date);

            if (level >= ExpandingAtLevel)
            {
                Take(found, LabTrait.Expanding);
            }
            else if (level >= BackedAtLevel && known
                && HasLanded(dossier, LabChapterKind.Funding, date))
            {
                Take(found, LabTrait.DeepPockets);
            }

            // Who is actually in front today. Ahead of everything else about a lab except that it
            // is falling apart or has decided it is against you.
            if (IsLeading(lab, state, date))
            {
                Take(found, LabTrait.Leading);
            }

            // Been through something public and still trading. Not the same as wobbling: this is a
            // company that took the hit and is still on the board.
            if (known && dossier.Fate != LabFate.Struggling && dossier.Fate != LabFate.Absorbed
                && (HasLanded(dossier, LabChapterKind.Scandal, date)
                    || HasLanded(dossier, LabChapterKind.Setback, date)))
            {
                Take(found, LabTrait.Scarred);
            }

            // Price before strategy, because a lab selling at a quarter of the rate is a fact about
            // this month rather than about its founding brief.
            if (IsUndercutting(lab, state, date))
            {
                Take(found, LabTrait.Undercutting);
            }

            if (HasGoneQuiet(lab, state, date))
            {
                Take(found, LabTrait.Quiet);
            }

            Take(found, FromStrategy(lab, state));

            // How long they have been at it, last, because it is the least surprising thing about
            // anybody and it should never crowd out what they are doing now.
            if (known)
            {
                if (dossier.Founded.Year < VeteranFoundedBefore)
                {
                    Take(found, LabTrait.Veteran);
                }
                else if (dossier.Founded.Year >= NewcomerFoundedFrom)
                {
                    Take(found, LabTrait.Newcomer);
                }
            }

            return found;
        }

        /// <summary>The one every lab has, from the brief it is actually running.</summary>
        private static LabTrait FromStrategy(CompetitorId lab, CompanyState state)
        {
            var agent = state.Rivals?.Find(lab);

            if (agent == null)
            {
                return LabTrait.None;
            }

            return agent.Strategy switch
            {
                CompetitorStrategy.FrontierRace => LabTrait.Fearless,
                CompetitorStrategy.PatientScaler => LabTrait.Patient,
                CompetitorStrategy.CostLeader => LabTrait.Undercutting,
                CompetitorStrategy.OpenWeights => LabTrait.OpenHanded,
                CompetitorStrategy.EnterpriseFocus => LabTrait.Institutional,
                // FastFollower has no lab, so it has no badge. See the gap at 6 above.
                _ => LabTrait.None
            };
        }

        /// <summary>
        /// Are they ahead of everybody else on capability today, and clearly enough to say so.
        ///
        /// The margin matters. Two labs a tenth of a point apart are level, and a badge that
        /// changed hands every fortnight would be noise on a card a player opens twice a year.
        /// </summary>
        private static bool IsLeading(CompetitorId lab, CompanyState state, GameDate date)
        {
            if (state.Rivals == null)
            {
                return false;
            }

            var best = 0.0;
            var second = 0.0;
            var leader = CompetitorId.None;

            foreach (var model in state.Rivals.LiveModels(date))
            {
                if (model.Capability > best)
                {
                    second = best;
                    best = model.Capability;
                    leader = model.Competitor;
                }
                else if (model.Capability > second)
                {
                    second = model.Capability;
                }
            }

            return leader == lab && best - second >= LeadingBy;
        }

        /// <summary>
        /// Have they shipped nothing for a long time.
        ///
        /// Read from the live model's release date rather than from a counter, so a lab that goes
        /// quiet and then ships loses this the same day, without anything having to be reset.
        /// </summary>
        private static bool HasGoneQuiet(CompetitorId lab, CompanyState state, GameDate date)
        {
            var agent = state.Rivals?.Find(lab);

            if (agent == null || !agent.HasShipped)
            {
                return false;
            }

            return date.DayIndex - agent.LiveReleaseDate.DayIndex > QuietAfterDays;
        }

        /// <summary>Is what they have on sale today priced well under the going rate.</summary>
        private static bool IsUndercutting(CompetitorId lab, CompanyState state, GameDate date)
        {
            if (state.Rivals == null)
            {
                return false;
            }

            foreach (var model in state.Rivals.LiveModels(date))
            {
                if (model.Competitor == lab && model.PriceMultiplier <= UndercuttingAtOrBelow)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Has a chapter of this kind already happened, as of today.
        ///
        /// The date test is the point. Reading the whole array would let the first card a player
        /// opens in 2022 tell them which three labs are going to fall over.
        /// </summary>
        private static bool HasLanded(in LabDossier dossier, LabChapterKind kind, GameDate date)
        {
            foreach (var chapter in dossier.Chapters)
            {
                if (chapter.Kind == kind && date.IsOnOrAfter(chapter.On))
                {
                    return true;
                }
            }

            return false;
        }

        private static void Take(List<LabTrait> found, LabTrait trait)
        {
            if (trait != LabTrait.None && found.Count < MostShown && !found.Contains(trait))
            {
                found.Add(trait);
            }
        }

        /// <summary>
        /// The word on the badge.
        ///
        /// `None` is answered without asking the book. A blank entry in the phrase book renders as
        /// an empty label, which reads as a missing control rather than as a missing translation,
        /// and `LocalisationTests.NoPhraseIsBlank` refuses one.
        /// </summary>
        public static string NameOf(LabTrait trait) =>
            trait == LabTrait.None ? string.Empty : Loc.T(KeyOf(trait));

        /// <summary>And why it is there, which is the half a single word cannot carry.</summary>
        public static string NoteFor(LabTrait trait) =>
            trait == LabTrait.None ? string.Empty : Loc.T(KeyOf(trait) + "_note");

        /// <summary>True when this one is a warning rather than a description.</summary>
        public static bool IsWarning(LabTrait trait) =>
            trait is LabTrait.Wobbling or LabTrait.Absorbed or LabTrait.Hostile or LabTrait.Leading;

        /// <summary>
        /// The phrase-book key, written out whole.
        ///
        /// Not built by concatenation: `LocalisationTests` reads literals, and a key assembled at
        /// runtime is invisible to the guard that checks every key exists. That guard caught the
        /// effect badges doing exactly this the day they were written.
        /// </summary>
        private static string KeyOf(LabTrait trait) => trait switch
        {
            LabTrait.Fearless => "labtrait.fearless",
            LabTrait.Patient => "labtrait.patient",
            LabTrait.Undercutting => "labtrait.undercutting",
            LabTrait.OpenHanded => "labtrait.open",
            LabTrait.Institutional => "labtrait.institutional",
            LabTrait.DeepPockets => "labtrait.deep",
            LabTrait.Expanding => "labtrait.expanding",
            LabTrait.Wobbling => "labtrait.wobbling",
            LabTrait.Absorbed => "labtrait.absorbed",
            LabTrait.Hostile => "labtrait.hostile",
            LabTrait.Veteran => "labtrait.veteran",
            LabTrait.Newcomer => "labtrait.newcomer",
            LabTrait.Scarred => "labtrait.scarred",
            LabTrait.Leading => "labtrait.leading",
            LabTrait.Quiet => "labtrait.quiet",
            _ => string.Empty
        };
    }
}
