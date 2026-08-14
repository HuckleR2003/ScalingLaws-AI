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

    /// <summary>One paid section, what it costs, and what it is allowed to print.</summary>
    public readonly struct NewsDeskDefinition
    {
        public NewsDeskDefinition(NewsSection section, string title, string outlet, IntelTier requires,
            IntelTier alsoRequires, string pitch, string lockedNote)
        {
            Section = section;
            Title = title ?? string.Empty;
            Outlet = outlet ?? string.Empty;
            Requires = requires;
            AlsoRequires = alsoRequires;
            Pitch = pitch ?? string.Empty;
            LockedNote = lockedNote ?? string.Empty;
        }

        public NewsSection Section { get; }
        public string Title { get; }

        /// <summary>Who publishes it, which is who the invoice comes from.</summary>
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

        public string Pitch { get; }

        /// <summary>What the panel says while it is shut.</summary>
        public string LockedNote { get; }

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
            new(NewsSection.TotalTrueNews, "TOTAL TRUE NEWS", "TrendSearch Team",
                IntelTier.TrendSearch, IntelTier.PublicNews,
                "Whether a thing is worth doing. The desk that will tell you a launch is not worth "
                + "buying and that waiting two months gets you a cheaper part that works.",
                "Requires TrendSearch Team membership."),

            new(NewsSection.ItSpy, "IT SPY", "KnownWords",
                IntelTier.KnownWords, IntelTier.PublicNews,
                "What the other labs are actually doing. Revenue, technology, how many models they "
                + "have built and how many are still on sale.",
                "Requires KnownWords membership."),

            // The one that needs two. See NewsDeskDefinition.AlsoRequires for why.
            new(NewsSection.EventHunter, "EVENT HUNTER", "National Press",
                IntelTier.NationalPress, IntelTier.TrendSearch,
                "What is coming and roughly when. Hardware, launches, the shape of the next quarter.",
                "Requires TrendSearch Team membership.")
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
            _ => "Public news"
        };

        /// <summary>What each outfit tells the player it is for, on the membership card.</summary>
        public static string OutletPitch(IntelTier tier) => tier switch
        {
            IntelTier.NationalPress =>
                "Wide coverage, first to print, wrong often enough that acting on it unread is its own "
                + "kind of decision.",
            IntelTier.KnownWords =>
                "A desk that only watches the other labs. Nothing about the market, everything about "
                + "who is building what.",
            IntelTier.TrendSearch =>
                "The expensive one. Long lead time, the highest hit rate in the game, and still not "
                + "certain, because nobody sells certainty.",
            _ => "Whatever is already public. Arrives with the news rather than before it."
        };

        /// <summary>Sections a company can read for nothing.</summary>
        public static bool IsFree(NewsSection section) =>
            section is NewsSection.Wire or NewsSection.Scandals or NewsSection.Premieres;
    }
}
