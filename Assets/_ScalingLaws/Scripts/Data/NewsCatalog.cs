using System;
using System.Collections.Generic;

namespace ScalingLaws.Data
{
    /// <summary>
    /// Where a story sits on the page.
    ///
    /// The layout is the taxonomy: the wire is everything in order, the two middle columns are the
    /// cuts a player checks daily, and the three on the right are the ones somebody is charging for.
    /// </summary>
    public enum NewsSection
    {
        /// <summary>Everything, newest first. Free, and the only section that is never empty.</summary>
        Wire = 0,

        /// <summary>Trouble. Other people's, and yours, which is the half that stings.</summary>
        Scandals = 1,

        /// <summary>What shipped. Yours and theirs, on the same page, on purpose.</summary>
        Premieres = 2,

        /// <summary>Advice worth acting on, when it is right. TrendSearch.</summary>
        TotalTrueNews = 3,

        /// <summary>What the other labs are actually doing. KnownWords.</summary>
        ItSpy = 4,

        /// <summary>What is coming and when. National Press, gated behind TrendSearch.</summary>
        EventHunter = 5
    }

    /// <summary>What one paid section costs, what it is allowed to print, and who publishes it.</summary>
    public readonly struct NewsDeskDefinition
    {
        public NewsDeskDefinition(NewsSection section, string key, string outlet, IntelTier requires,
            IntelTier alsoRequires)
        {
            Section = section;
            this.key = key;
            Outlet = outlet ?? string.Empty;
            Requires = requires;
            AlsoRequires = alsoRequires;
        }

        /// <summary>
        /// The stem the three readable parts hang off.
        ///
        /// **This row used to store its own English.** A catalog built once at type load keeps
        /// whatever language it was built in, which is the fault eighteen other catalogs in this
        /// game were converted for; this one was missed, and the whole right hand side of the news
        /// screen stayed English in a Polish campaign. A tester reported exactly that.
        ///
        /// The three keys are written out below rather than concatenated at the call site, so
        /// `LocalisationTests.EveryKeyTheInterfaceAsksForExists` can still see them as literals.
        /// </summary>
        private readonly string key;

        public NewsSection Section { get; }

        /// <summary>Resolved per read, so a language change mid-campaign reaches it.</summary>
        public string Title => Loc.T(key + ".title");

        /// <summary>Who publishes it, which is who the invoice comes from. A name, not a phrase.</summary>
        public string Outlet { get; }

        public IntelTier Requires { get; }

        /// <summary>
        /// A second membership on top of the first, or <see cref="IntelTier.PublicNews"/> for none.
        ///
        /// Event Hunter is the one that has this, and it is deliberately unpleasant: National Press
        /// sells you the section and then it turns out the section only opens for TrendSearch
        /// members. Paying for access that needs another payment is a real thing that happens to
        /// people who buy research, and the note the player reads says exactly which one is missing.
        /// </summary>
        public IntelTier AlsoRequires { get; }

        public string Pitch => Loc.T(key + ".pitch");

        /// <summary>What the panel says while it is shut.</summary>
        public string LockedNote => Loc.T(key + ".locked");

        public bool NeedsTwo => AlsoRequires != IntelTier.PublicNews;
    }

    /// <summary>
    /// The three research outfits and the three sections they feed.
    ///
    /// **They are not a ladder.** Each is bought on its own, each answers a different question, and
    /// the dearest one does not contain the other two. TrendSearch tells you whether a thing is worth
    /// doing, KnownWords tells you what the other labs are doing, National Press tells you when
    /// things happen. A company can want any one of those without wanting the others.
    ///
    /// Prices are per month and they are the author's numbers, not derived: 20k, 50k, 400k.
    /// </summary>
    public static class NewsCatalog
    {
        public const string CatalogVersion = "news-1";

        private static readonly NewsDeskDefinition[] Desks =
        {
            new(NewsSection.TotalTrueNews, "news.desk.tt", "TrendSearch Team",
                IntelTier.TrendSearch, IntelTier.PublicNews),

            new(NewsSection.ItSpy, "news.desk.spy", "KnownWords",
                IntelTier.KnownWords, IntelTier.PublicNews),

            // The one that needs two. See NewsDeskDefinition.AlsoRequires for why.
            new(NewsSection.EventHunter, "news.desk.hunter", "National Press",
                IntelTier.NationalPress, IntelTier.TrendSearch)
        };

        public static IReadOnlyList<NewsDeskDefinition> PaidDesks => Desks;

        public static bool TryGetDesk(NewsSection section, out NewsDeskDefinition desk)
        {
            foreach (var candidate in Desks)
            {
                if (candidate.Section == section)
                {
                    desk = candidate;
                    return true;
                }
            }

            desk = default;
            return false;
        }

        /// <summary>The three buyable memberships, cheapest first.</summary>
        public static IReadOnlyList<IntelTier> Memberships { get; } = new[]
        {
            IntelTier.NationalPress,
            IntelTier.KnownWords,
            IntelTier.TrendSearch
        };

        public static string OutletName(IntelTier tier) => tier switch
        {
            IntelTier.NationalPress => "National Press",
            IntelTier.KnownWords => "KnownWords",
            IntelTier.TrendSearch => "TrendSearch Team",
            _ => Loc.T("news.outlet.public")
        };

        /// <summary>
        /// What each outfit tells the player it is for, on the membership card.
        ///
        /// **A literal per arm, never a key built from the enum name.** `LocalisationTests` reads
        /// literals and a concatenated key is invisible to it, which has already cost this project
        /// one shipped screen of raw keys.
        /// </summary>
        public static string OutletPitch(IntelTier tier) => tier switch
        {
            IntelTier.NationalPress => Loc.T("news.pitch.press"),
            IntelTier.KnownWords => Loc.T("news.pitch.known"),
            IntelTier.TrendSearch => Loc.T("news.pitch.trend"),
            _ => Loc.T("news.pitch.public")
        };

        /// <summary>Sections a company can read for nothing.</summary>
        public static bool IsFree(NewsSection section) =>
            section is NewsSection.Wire or NewsSection.Scandals or NewsSection.Premieres;
    }
}
