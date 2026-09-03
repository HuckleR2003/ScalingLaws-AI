namespace ScalingLaws.Data
{
    /// <summary>
    /// What every explained control is, what it honestly moves, and what each end of it buys.
    ///
    /// **One file, because the alternative is the same sentence written four times.** Sparsity is
    /// explained on the architecture screen, on the model creator and in the research tree, and
    /// before this the three said different things: one called it the biggest lever on cost, one
    /// called it a way to build bigger, and the third did not mention it. A player who reads all
    /// three learns that the game is not sure.
    ///
    /// **The honest line is the point of the format.** `Affects` names what the number actually
    /// touches in the simulation, in the simulation's own terms, including when the answer is
    /// unflattering. A description that only lists benefits is an advertisement, and this game is
    /// about timing decisions that have a wrong side.
    ///
    /// The two ends are both real. A control where one end is simply better is not a control, and
    /// writing `Low` forces that to be checked: anywhere the low end had nothing to say, the
    /// mechanic behind it needed fixing rather than describing.
    ///
    /// Plain strings and no UnityEngine, so this stays in `Data/` and the tests can read it.
    /// </summary>
    public static class TechNotes
    {
        /// <summary>Bump when the copy changes enough that a screenshot would be stale.</summary>
        public const string CatalogVersion = "2026.08.23";

        /// <summary>One explanation. Mirrors `UI.InsightTip.Reading` without depending on it.</summary>
        public readonly struct Note
        {
            public Note(string title, string what, string affects, string high, string low)
            {
                Title = title;
                What = what;
                Affects = affects;
                High = high;
                Low = low;
            }

            public string Title { get; }
            public string What { get; }
            public string Affects { get; }
            public string High { get; }
            public string Low { get; }
        }

        /// <summary>
        /// One note, assembled from the phrase book.
        ///
        /// **The prose used to live here and now it does not.** Thirteen notes of four paragraphs
        /// each is the largest block of player-facing writing in the game, and leaving it in a C#
        /// file meant it could not be translated without a programmer. Five keys per note: title,
        /// what it is, what it honestly moves, and what each end of the control buys.
        /// </summary>
        private static Note From(string stem) => new(
            Loc.T(stem + ".title"),
            Loc.T(stem + ".what"),
            Loc.T(stem + ".affects"),
            Loc.T(stem + ".high"),
            Loc.T(stem + ".low"));

        public static Note Sparsity => From("tech.sparsity");

        public static Note Throughput => From("tech.throughput");

        public static Note Quality => From("tech.quality");

        public static Note Serving => From("tech.serving");

        public static Note Reasoning => From("tech.reasoning");

        public static Note ResearchBudget => From("tech.budget");

        public static Note ProgrammeLength => From("tech.length");

        public static Note WebCrawl => From("tech.webcrawl");

        public static Note CuratedWeb => From("tech.curated");

        public static Note LicensedArchives => From("tech.licensed");

        public static Note Parameters => From("tech.parameters");

        public static Note TokensPerParameter => From("tech.tokens");

        public static Note SafetyEffort => From("tech.safety");

        // ---- the twenty six the rest of the game was missing ------------------------------------
        //
        // Written in a different register from the thirteen above, on purpose. Those explain a
        // control the player is dragging; these explain a word on a screen to somebody who has never
        // read anything about this industry. Every one of them opens with an everyday thing, because
        // a correct definition is exactly what the screens already were.

        public static Note Revenue => From("tech.revenue");

        public static Note Margin => From("tech.margin");

        public static Note TokenPrice => From("tech.tokenprice");

        public static Note DailyBurn => From("tech.burn");

        public static Note Valuation => From("tech.valuation");

        public static Note FounderStake => From("tech.equity");

        public static Note Instalment => From("tech.instalment");

        public static Note PetaflopDay => From("tech.petaflop");

        public static Note RentOrOwn => From("tech.rentbuy");

        public static Note ReservedCapacity => From("tech.reserved");

        public static Note ClusterSplit => From("tech.split");

        public static Note Positions => From("tech.headcount");

        public static Note Wage => From("tech.wage");

        public static Note TeamWorth => From("tech.talent");

        public static Note Awareness => From("tech.awareness");

        public static Note Channels => From("tech.channel");

        public static Note CampaignLength => From("tech.campaign");

        public static Note MarketPar => From("tech.par");

        public static Note WaitingToRelease => From("tech.shelf");

        public static Note Capability => From("tech.capability");

        public static Note MarketShare => From("tech.share");

        public static Note ResearchPoints => From("tech.points");

        public static Note Eras => From("tech.era");

        public static Note Traits => From("tech.trait");

        public static Note TraitLevels => From("tech.traitlevel");

        public static Note Pricing => From("tech.pricing");

        public static Note FreeTier => From("tech.freetier");
    }
}
