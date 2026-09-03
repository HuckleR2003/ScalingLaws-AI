using System.Collections.Generic;
using ScalingLaws.Core;
using ScalingLaws.Data;
using ScalingLaws.Simulation;
using UnityEngine.UIElements;

namespace ScalingLaws.UI
{
    /// <summary>
    /// The small square badges in the top bar: what is temporarily true about the company.
    ///
    /// **`EffectBook` was the eleventh mechanism in this project finished in the simulation and
    /// impossible for a player to see.** It is complete and it has been for a long time: five kinds,
    /// each with a duration, a signed magnitude, a taper over the last quarter of its window, a
    /// one-of-each-kind rule, expiry, save and restore. All four of the real ones are raised
    /// correctly. `DemandMultiplier` is read by the market every single day, and it clamps between
    /// 0.15x and 4.0x, so the number of people arriving at the company can be a sixth or four times
    /// what the product alone explains.
    ///
    /// It even carried `NameOf`, `NoteFor` and `IsBad`, which exist for no purpose other than
    /// drawing a badge. Nothing in `Scripts/UI/` referenced `Effects` at all. The player watched
    /// their user count quadruple and then slide back, with no word anywhere about why.
    ///
    /// **Generic over the enum on purpose.** The author has a list of new effects to add, and the
    /// point of this file is that adding one is a member, a raise site and two phrase-book keys.
    /// Nothing here has to be touched, and `EffectBadgeTests` fails if a new kind arrives without
    /// its words.
    /// </summary>
    public sealed class EffectBadges
    {
        /// <summary>
        /// Beyond this many at once the strip starts eating the top bar, so the oldest are dropped.
        ///
        /// The list comes back newest first, which is the right order to lose from: an effect in its
        /// last week is the one the player has already read.
        /// </summary>
        public const int MostShown = 5;

        private readonly List<VisualElement> pool = new();

        public EffectBadges()
        {
            Root = new VisualElement();
            Root.AddToClassList("fx");
        }

        public VisualElement Root { get; }

        /// <summary>
        /// Repoints the strip at whatever is running today.
        ///
        /// **Rebuilt rather than pooled, and that is a deliberate exception.** The tutorial strip
        /// taught this project that rebuilding an element the cursor is resting on destroys the
        /// click between the press and the release. These badges are not buttons: they carry a
        /// hover card and nothing else, and the worst a rebuild costs is a card that closes. The
        /// alternative is holding an element per kind forever so a strip that is empty most of the
        /// campaign can keep five hidden children.
        /// </summary>
        public void Refresh(CompanyState state)
        {
            if (state == null)
            {
                return;
            }

            var live = state.Effects.Active(state.Date);

            Root.Clear();
            pool.Clear();

            Root.style.display = live.Count == 0 ? DisplayStyle.None : DisplayStyle.Flex;

            for (var index = 0; index < live.Count && index < MostShown; index++)
            {
                var badge = Build(live[index], state.Date);
                pool.Add(badge);
                Root.Add(badge);
            }
        }

        /// <summary>How many badges are on screen. For the tests, which have no panel to look at.</summary>
        public int Count => pool.Count;

        private static VisualElement Build(ModelEffect effect, GameDate today)
        {
            var badge = new VisualElement();
            badge.AddToClassList("fx__badge");
            badge.EnableInClassList("fx__badge--bad", EffectBook.IsBad(effect.Kind));

            // Two letters rather than an icon, because there is no art for these yet and a blank
            // square in the top bar reads as a broken image rather than as an unfinished one. The
            // glyph comes from the phrase book, so it can be initials in either language.
            var glyph = new Label(Loc.T(GlyphKeyOf(effect.Kind)));
            glyph.AddToClassList("fx__glyph");
            badge.Add(glyph);

            // The days left, under the glyph, because "how long is this going to last" is the only
            // follow-up question a temporary effect ever produces.
            var left = new Label(effect.DaysLeft(today).ToString());
            left.AddToClassList("fx__days");
            badge.Add(left);

            // The card says what it is, what it is doing, and how long is left. The multiplier is
            // read from the effect rather than restated, so the badge cannot disagree with the
            // market about the size of the window it is describing.
            //
            // The default placement is above, and `InsightTip` already flips a card below when
            // there is no room over it. These sit in the top bar, so every one of them flips.
            InsightTip.Attach(badge, EffectBook.NameOf(effect.Kind),
                EffectBook.NoteFor(effect.Kind) + "\n\n"
                + Loc.T("effect.pulling",
                    UiFormat.Percent(effect.Multiplier(today) - 1.0, 0))
                + "   ·   "
                + Loc.T("effect.days_left", effect.DaysLeft(today).ToString()));

            return badge;
        }

        /// <summary>
        /// The phrase-book key for a kind's glyph, written out whole.
        ///
        /// **Not `"effect.glyph." + something`.** `LocalisationTests` can only read literals, so a
        /// key assembled at runtime is invisible to the guard that checks every key exists, and
        /// this project has already shipped one screen of raw keys that way. The first version of
        /// this file concatenated and the guard caught it on the next run, which is the whole
        /// reason that test is worth its own fixture.
        ///
        /// Public so a test can ask the same question the badge does rather than rebuilding the
        /// mapping beside it.
        /// </summary>
        public static string GlyphKeyOf(ModelEffectKind kind) => kind switch
        {
            ModelEffectKind.Viral => "effect.glyph.viral",
            ModelEffectKind.FirstRelease => "effect.glyph.first",
            ModelEffectKind.SafeHarbour => "effect.glyph.harbour",
            ModelEffectKind.Backlash => "effect.glyph.backlash",
            ModelEffectKind.Campaign => "effect.glyph.campaign",
            _ => "effect.glyph.none"
        };
    }
}
