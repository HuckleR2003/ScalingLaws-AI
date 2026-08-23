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

        // ---- the five architecture directions ---------------------------------------------------

        public static Note Sparsity => new(
            "SPARSITY",
            "A dense model runs every parameter for every token. A sparse one routes each token to a "
            + "few experts and leaves the rest idle, so a model can be enormous to hold and small to "
            + "run.",
            "Moves the family's active parameter fraction, which the market reads as serving burden "
            + "and the planner reads as compute per token. It does not make the model better at "
            + "anything.",
            "Cheap to serve at a size nobody else can afford to run, and the headroom to keep "
            + "growing. Routing is fragile: quality per parameter falls, and a badly routed model is "
            + "worse than a dense one half its size.",
            "Every parameter earns its keep and the model behaves predictably. You pay for all of it "
            + "on every token, forever, and the price war is won by whoever pays less.");

        public static Note Throughput => new(
            "THROUGHPUT",
            "How much of the cluster is doing arithmetic rather than waiting. Overlapping "
            + "communication with computation, fusing kernels, keeping the pipeline full.",
            "Divides the calendar on every training run in the family. Nothing else. It does not "
            + "raise the ceiling and it does not lower the bill.",
            "Runs finish sooner, so the same fleet ships more models a year and a launch window can "
            + "still be caught. Buys nothing at all if the company is not compute bound.",
            "Runs take as long as they take. The effort goes into directions that change what the "
            + "model is rather than when it arrives, which is the right trade while the frontier is "
            + "moving slowly.");

        public static Note Quality => new(
            "QUALITY PER PARAMETER",
            "Getting more capability out of the same weights: better initialisation, better "
            + "objectives, better data ordering, better use of the tokens you already paid for.",
            "Raises the capability ceiling of every model in the family. It is the only direction "
            + "that makes the models themselves better, and it is the slowest to pay off.",
            "Every model this family ever produces scores higher for the same compute, which "
            + "compounds across a decade. It is the most expensive direction and the last to show "
            + "up in the books.",
            "Nothing improves on its own and you stay where the published techniques are. Correct "
            + "when the company is losing on cost rather than on capability, because a better model "
            + "nobody can afford to run is not a product.");

        public static Note Serving => new(
            "SERVING COST",
            "What a token costs once the model is live: quantisation, caching, batching, speculative "
            + "decoding. The engineering nobody writes papers about.",
            "Multiplies inference cost per token for every model in the family. Invisible until the "
            + "day a rival cuts their price and you have to decide whether you can follow.",
            "Margin on every token forever, and the ability to survive a price war you did not "
            + "start. It changes nothing a customer can see, so it never wins you a launch.",
            "The effort goes somewhere visible instead. Fine while demand is small; a company "
            + "serving billions of tokens a day at a bad multiplier is losing money on every one of "
            + "them and cannot advertise its way out.");

        public static Note Reasoning => new(
            "REASONING",
            "Structure the model is trained to use rather than scale it is given: chains of thought, "
            + "process rewards, search at inference time.",
            "A direct capability bonus that does not come from parameters or tokens. It is the one "
            + "direction that is not on the scaling curve, which is why it is the most expensive per "
            + "point.",
            "Capability the competition cannot buy with a bigger cluster, and the only lever left "
            + "once the frontier is compute limited. Costs the most per point of anything here.",
            "Scale and data do the work. Cheaper per point of capability today, and it stays "
            + "cheaper right up until everybody's models are the same size and nobody can tell the "
            + "products apart.");

        // ---- the two investment controls ---------------------------------------------------------

        public static Note ResearchBudget => new(
            "RESEARCH BUDGET",
            "What the company puts behind the programme. Paid whether or not it works.",
            "Combines with the calendar as a geometric mean, then sets how wide the outcome band is. "
            + "Money alone cannot buy a breakthrough and it cannot buy time.",
            "A narrow band around a good result: an expensive programme mostly lands where the "
            + "screen said it would. It is capital that never comes back and it is gone whether the "
            + "family is any good or not.",
            "Cheap, and a lottery ticket. The band is wide enough that the result could be better "
            + "than the expected number or nearly worthless, and a young company sometimes has to "
            + "take that bet.");

        public static Note ProgrammeLength => new(
            "PROGRAMME LENGTH",
            "How long the research runs before it delivers a family.",
            "The other half of the geometric mean, and it also narrows the outcome band. The "
            + "calendar is the part that cannot be bought out of.",
            "A deeper, more certain result, and a slot blocked for a year or more while the "
            + "frontier keeps moving. One family programme at a time.",
            "A family in hand months earlier, at a result that could be anything within the band. "
            + "Shipping something ordinary this year often beats shipping something good in two.");

        // ---- the corpora -------------------------------------------------------------------------

        public static Note WebCrawl => new(
            "WEB CRAWL",
            "Everything that could be scraped, filtered as little as possible. What every lab "
            + "started with.",
            "Sets effective corpus quality and how many tokens are available at all. It is free and "
            + "it is the floor.",
            "Volume nothing else matches, and no licence to negotiate.",
            "Quality low enough that adding more of it stops helping. A run trained on this alone "
            + "hits a ceiling no amount of compute lifts.");

        public static Note CuratedWeb => new(
            "CURATED CORPORA",
            "The same text, filtered hard: deduplicated, quality scored, the worst of it thrown away.",
            "Raises effective corpus quality, which multiplies into what a run gets out of the same "
            + "tokens. Costs tokens: throwing away the bad half means having half as much.",
            "Every petaflop-day converts better, so the same cluster buys a better model.",
            "More tokens to train on, which matters when the run is large enough to be token "
            + "limited rather than compute limited.");

        public static Note LicensedArchives => new(
            "LICENSED ARCHIVES",
            "Books, journals and archives bought rather than scraped. Expensive, clean, and legally "
            + "yours.",
            "High quality tokens, and they are the ones a regulator cannot make you delete. Data "
            + "provenance is what the incident model rolls against.",
            "The best quality per token in the game, and a data question that does not arrive in "
            + "court eighteen months later.",
            "The money stays in the bank. Three of the labs on the ranking screen were sued over a "
            + "training set, so this is not free.");

        // ---- the model creator ---------------------------------------------------------------------

        public static Note Parameters => new(
            "PARAMETERS",
            "How large the model is. The single biggest decision on this screen.",
            "Drives compute required, capability, and the serving burden the market charges you for "
            + "ever afterwards. Capped by what the company has researched, not by what it can pay.",
            "Capability the small models cannot reach, and a bill on every token you ever serve. A "
            + "big model that nobody wants is the most expensive mistake available here.",
            "Cheap to train, cheap to serve, quick to ship, and it will be beaten on capability by "
            + "everything the frontier releases next year.");

        public static Note TokensPerParameter => new(
            "TOKENS PER PARAMETER",
            "How much data the model sees for its size. Compute-optimal is about twenty to one, "
            + "which is a published result rather than a number this game invented.",
            "Sets how much of the run's compute turns into capability. Both ends of the belt waste "
            + "it, and the waste is real petaflop-days you paid for.",
            "An overtrained model is small for what it can do, so it is cheap to serve for its "
            + "capability. Past a point the extra tokens buy almost nothing.",
            "An undertrained model is a large model that has not learned what it could have. Fast "
            + "to finish, and it leaves capability on the table that the compute already bought.");

        public static Note SafetyEffort => new(
            "SAFETY EFFORT",
            "How long the safety stage runs before the model ships.",
            "Multiplies the safety days only. Not the training, not the bill. It lowers the daily "
            + "incident risk the model carries for its whole life.",
            "A model that is far less likely to put a nine figure fine and a forced withdrawal in "
            + "front of you on an ordinary Tuesday. Weeks of calendar for a few percent.",
            "The model ships sooner into a market that is moving. Every campaign that ended badly "
            + "in this game ended on an incident, and x1 is the setting that forgets that.");
    }
}
