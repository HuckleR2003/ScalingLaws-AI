using System.Collections.Generic;
using ScalingLaws.Core;

namespace ScalingLaws.Data
{
    /// <summary>What happened to a lab, as of the last chapter written for it.</summary>
    public enum LabFate
    {
        /// <summary>Still its own company, still deciding its own direction.</summary>
        Independent = 0,

        /// <summary>Alive, but no longer setting the pace and visibly running out of room.</summary>
        Struggling = 1,

        /// <summary>The people and the models went somewhere larger. The name may still exist.</summary>
        Absorbed = 2
    }

    /// <summary>
    /// What kind of thing a chapter is. The news desk sorts by this and the dossier colours by it.
    /// </summary>
    public enum LabChapterKind
    {
        Founding = 0,
        Milestone = 1,
        Funding = 2,
        Setback = 3,

        /// <summary>Something that cost them trust rather than money. These go to SCANDALS.</summary>
        Scandal = 4,

        /// <summary>The end of the company as an independent thing.</summary>
        Exit = 5
    }

    /// <summary>One dated entry in a lab's history.</summary>
    public readonly struct LabChapter
    {
        public LabChapter(GameDate on, LabChapterKind kind, string headline, string body,
            bool isProjection = false)
        {
            On = on;
            Kind = kind;
            Headline = headline ?? string.Empty;
            Body = body ?? string.Empty;
            IsProjection = isProjection;
        }

        public GameDate On { get; }
        public LabChapterKind Kind { get; }
        public string Headline { get; }
        public string Body { get; }

        /// <summary>
        /// True when the entry is not documented history.
        ///
        /// The same honesty flag the hardware and competitor tables carry. A chapter dated past what
        /// is actually known is the game inventing a future, and the dossier says so rather than
        /// printing a guess in the same type as a fact.
        /// </summary>
        public bool IsProjection { get; }

        public bool HasHappenedBy(GameDate date) => date.IsOnOrAfter(On);
    }

    /// <summary>Everything the player can read about one lab.</summary>
    public readonly struct LabDossier
    {
        public LabDossier(CompetitorId competitor, string name, GameDate founded, string home,
            string positioning, string story, LabFate fate, LabChapter[] chapters)
        {
            Competitor = competitor;
            Name = name;
            Founded = founded;
            Home = home;
            Positioning = positioning;
            Story = story;
            Fate = fate;
            Chapters = chapters ?? new LabChapter[0];
        }

        public CompetitorId Competitor { get; }
        public string Name { get; }

        /// <summary>When the company started, which is often years before it built anything famous.</summary>
        public GameDate Founded { get; }

        public string Home { get; }

        /// <summary>One line: what this lab is actually for. The thing that separates it from the rest.</summary>
        public string Positioning { get; }

        /// <summary>Two or three sentences of who they are, written to be read once.</summary>
        public string Story { get; }

        public LabFate Fate { get; }
        public LabChapter[] Chapters { get; }

        /// <summary>
        /// The chapters that have already happened, oldest first.
        ///
        /// **The future is never shown.** A dossier that lists a collapse two years before it happens
        /// turns the whole mechanic into a spoiler, and it would break the same rule the projection
        /// flag exists for: the player is told what is known, on the day it becomes known.
        /// </summary>
        public List<LabChapter> ChaptersBy(GameDate date)
        {
            var so_far = new List<LabChapter>();

            foreach (var chapter in Chapters)
            {
                if (chapter.HasHappenedBy(date))
                {
                    so_far.Add(chapter);
                }
            }

            return so_far;
        }
    }

    /// <summary>
    /// Who the rivals are, beyond a capability number.
    ///
    /// **The ranking board was twelve rows of arithmetic.** A player could see that a lab was ahead
    /// and had no way of knowing why, what it was for, or whether it was the sort of company that
    /// was about to fall over. That is most of what makes a field of competitors interesting, and
    /// none of it was anywhere in the game.
    ///
    /// **Every name is a parody and every history is real.** That is the whole trick, and it is the
    /// same one Game Dev Tycoon plays: a player who was there recognises every beat, and no actual
    /// company has its name attached to the word scandal.
    ///
    /// The nine older labs were renamed on 2026-08-15 and the names were not invented that day. They
    /// had been sitting in the logo file names since the marks were drawn in August, because whoever
    /// drew them had already made this decision. `NameOf` was the only thing that never caught up.
    ///
    /// Every chapter is a documented public event unless it carries `IsProjection`. That boundary is
    /// not decoration: these arcs are drawn from things that actually happened, and the difference
    /// between what was announced and where the game thinks it goes has to survive contact with a
    /// player who followed it at the time.
    ///
    /// **No individual is named anywhere in this file.** A resignation the company itself announced
    /// is a fact about the company. Putting a real person's name next to the word scandal in a game
    /// is a different thing entirely, and the stories lose nothing by saying "the chief executive".
    ///
    /// **This is the one place a lab's name is written.** `CompetitorCatalog.NameOf` reads it, so
    /// renaming the rest of the roster is editing one column in this file and nothing else.
    /// </summary>
    public static class LabDossiers
    {
        public const string CatalogVersion = "2026.08.15";

        private static readonly Dictionary<CompetitorId, LabDossier> ById = Build();

        public static IEnumerable<LabDossier> All => ById.Values;

        public static bool TryGet(CompetitorId competitor, out LabDossier dossier) =>
            ById.TryGetValue(competitor, out dossier);

        /// <summary>The name, or the enum as a last resort so a new lab is never blank on screen.</summary>
        public static string NameOf(CompetitorId competitor) =>
            ById.TryGetValue(competitor, out var dossier) ? dossier.Name : competitor.ToString();

        /// <summary>
        /// Every chapter across every lab that lands on a given day.
        ///
        /// This is what the news desk reads. It is a lookup rather than a per-lab scan because the
        /// desk runs once a day and there is no reason for it to know how many labs exist.
        /// </summary>
        public static List<(LabDossier Lab, LabChapter Chapter)> ChaptersOn(GameDate date)
        {
            var today = new List<(LabDossier, LabChapter)>();

            foreach (var dossier in ById.Values)
            {
                foreach (var chapter in dossier.Chapters)
                {
                    if (chapter.On == date)
                    {
                        today.Add((dossier, chapter));
                    }
                }
            }

            return today;
        }

        private static GameDate On(int year, int month, int day) =>
            GameDate.FromCalendar(year, month, day);

        private static Dictionary<CompetitorId, LabDossier> Build()
        {
            var all = new[]
            {
                // ---------------------------------------------------------------- the frontier

                new LabDossier(CompetitorId.OpenAi, "OpenSI", On(2015, 12, 11), "San Francisco",
                    "Consumer reach first, frontier capability second, and the two feed each other.",
                    "Started as a non-profit research lab and restructured around a capped-profit arm "
                    + "in 2019 to pay for compute. The chat product in late 2022 is the moment this "
                    + "industry stopped being a research field and became a market.",
                    LabFate.Independent,
                    new[]
                    {
                        new LabChapter(On(2022, 11, 30), LabChapterKind.Milestone,
                            "A chat box reaches everyone",
                            "A research preview becomes the fastest adopted consumer product anybody "
                            + "has measured. Every assumption about who buys this changes in a week."),

                        new LabChapter(On(2023, 11, 17), LabChapterKind.Scandal,
                            "The board fires the chief executive, then unfires him",
                            "Five days of open warfare between a board and a company. He is back by "
                            + "the following week and most of the board is not. Governance is not a "
                            + "footnote at this scale; it is the thing that decides who owns the "
                            + "frontier."),
                    }),

                new LabDossier(CompetitorId.Anthropic, "Antropic", On(2021, 1, 1), "San Francisco",
                    "Safety as a product feature, sold to companies that have to answer for what "
                    + "their software does.",
                    "Founded by people who left the frontier lab they helped build, over how fast it "
                    + "was moving. Spent its first two years quiet and unremarkable, then became the "
                    + "default choice for buyers who need to explain their vendor to a regulator.",
                    LabFate.Independent,
                    new[]
                    {
                        new LabChapter(On(2023, 3, 14), LabChapterKind.Milestone,
                            "The safety lab ships",
                            "Two years of nothing, then a model that is immediately taken seriously. "
                            + "The quiet part was the strategy."),
                    }),

                new LabDossier(CompetitorId.GoogleDeepMind, "DeepThink", On(2010, 9, 23),
                    "London",
                    "A research institution attached to the largest distribution channel on earth.",
                    "Bought in 2014 and merged with its parent's own AI division in 2023. It has the "
                    + "compute, the researchers and the users, and spent two years being beaten to "
                    + "market by companies with a fraction of all three.",
                    LabFate.Independent,
                    new[]
                    {
                        new LabChapter(On(2023, 4, 20), LabChapterKind.Milestone,
                            "Two research groups become one",
                            "The parent stops running two AI labs against each other and merges them. "
                            + "A reorganisation is not a model, but this one is what makes the next "
                            + "two years possible."),
                    }),

                new LabDossier(CompetitorId.MetaAi, "Infinity", On(2013, 12, 9), "Menlo Park",
                    "Give the weights away and make everyone else's moat worthless.",
                    "The only lab at this scale whose strategy is to destroy the price of the thing "
                    + "everybody else sells. It does not need the model to earn; it needs the model "
                    + "nobody can charge for.",
                    LabFate.Independent,
                    new[]
                    {
                        new LabChapter(On(2023, 7, 18), LabChapterKind.Milestone,
                            "Open weights arrive at scale",
                            "A capable model released with weights anybody can download. Every "
                            + "company charging for API access has to explain what they are charging "
                            + "for."),
                    }),

                new LabDossier(CompetitorId.MistralAi, "Astral", On(2023, 4, 28), "Paris",
                    "Small, fast, open, European. Efficiency as the whole argument.",
                    "Founded by researchers out of the big labs, and it shipped a model people "
                    + "actually used within six months of existing. Proof that the frontier is not "
                    + "the only place to compete.",
                    LabFate.Independent,
                    new[]
                    {
                        new LabChapter(On(2023, 9, 27), LabChapterKind.Milestone,
                            "Seven billion parameters, released by magnet link",
                            "No launch event, no paper embargo. A torrent link and a model small "
                            + "enough to run on one machine."),
                    }),

                new LabDossier(CompetitorId.DeepSeek, "DeepSearch", On(2023, 7, 17), "Hangzhou",
                    "Frontier capability at a cost that makes the incumbents' pricing look absurd.",
                    "Grew out of a quantitative trading firm, which shows: the whole company is an "
                    + "argument about cost per unit of capability. Its releases repeatedly moved "
                    + "markets that had nothing to do with software.",
                    LabFate.Independent,
                    new[]
                    {
                        new LabChapter(On(2025, 1, 20), LabChapterKind.Milestone,
                            "Frontier reasoning, at a tenth of the price",
                            "A reasoning model at roughly the level of the best available, trained "
                            + "for a reported fraction of the cost, released open. Hardware shares "
                            + "fall worldwide on the news."),
                    }),

                new LabDossier(CompetitorId.XAi, "zAI", On(2023, 3, 9), "Bay Area",
                    "Attention as a distribution strategy, and a very large cluster very quickly.",
                    "Started late and bought its way to the frontier with capital and speed of "
                    + "buildout rather than a research lead. What it has that nobody else does is a "
                    + "social network to launch into.",
                    LabFate.Independent,
                    new[]
                    {
                        new LabChapter(On(2024, 9, 2), LabChapterKind.Milestone,
                            "A cluster built in months, not years",
                            "A training cluster of a size that normally takes two years, brought up "
                            + "in a converted factory in a few months. Buildout speed turns out to "
                            + "be a strategy."),
                    }),

                new LabDossier(CompetitorId.AlibabaQwen, "Swen", On(2023, 4, 7), "Hangzhou",
                    "Open weights across every size, backed by a cloud business that wants the "
                    + "workloads.",
                    "A cloud vendor's model family rather than a startup, which is why it ships in "
                    + "every size from phone to datacentre. The strategy is not to win the frontier; "
                    + "it is to be the default thing people build on.",
                    LabFate.Independent,
                    new[]
                    {
                        new LabChapter(On(2023, 8, 3), LabChapterKind.Milestone,
                            "Open weights, every size",
                            "A cloud vendor releases its own family openly. Being the default is "
                            + "worth more to them than being the best."),
                    }),

                new LabDossier(CompetitorId.Groq, "Grob", On(2022, 1, 3), "Austin",
                    "Nothing in particular, which is the problem.",
                    "A small lab that started the same month you did, with roughly the same money "
                    + "and the same idea. It ships steadily and falls further behind every year, "
                    + "because steady is not the same as fast when the frontier triples annually. "
                    + "It is here so that your own struggle does not look like a personal failing.",
                    LabFate.Struggling,
                    new[]
                    {
                        new LabChapter(On(2022, 4, 12), LabChapterKind.Founding,
                            "Somebody else had the same idea",
                            "A lab of the same size as yours ships something barely usable in the "
                            + "same season you do.", isProjection: true),
                    }),

                // ------------------------------------------------- the open source rocket, falling

                new LabDossier(CompetitorId.StabilityAi, "StableAI", On(2020, 1, 1), "London",
                    "Open image generation, given away, funded by a community that does not pay.",
                    "For about eight months this was the most exciting company in the field. It put "
                    + "an image model out in the open in August 2022, and within weeks it was "
                    + "running on home machines and inside a hundred products the lab did not own "
                    + "and could not bill. A hundred million dollars followed, at a billion dollar "
                    + "valuation. Then the bill arrived, and it was the same bill three times over: "
                    + "compute that costs the same whether or not anybody pays, no way to charge "
                    + "for something already free, and a question about the training data that "
                    + "nobody had answered before it became a court's problem. The models never got "
                    + "worse. The company did.",
                    LabFate.Struggling,
                    new[]
                    {
                        new LabChapter(On(2022, 8, 22), LabChapterKind.Milestone,
                            "Open image generation reaches everybody",
                            "Weights released to anybody who wants them. Within weeks it is running "
                            + "on home machines and inside a hundred products the lab does not own "
                            + "and cannot bill."),

                        new LabChapter(On(2022, 10, 17), LabChapterKind.Funding,
                            "A hundred million dollars, at a billion dollar valuation",
                            "Investors buy the community rather than the revenue. The company looks, "
                            + "for one autumn, like the obvious next giant."),

                        new LabChapter(On(2023, 1, 16), LabChapterKind.Scandal,
                            "Sued over what the model was trained on",
                            "A stock photography company and a group of artists both go to court "
                            + "over the training set. **Nothing in the model changed. The company's "
                            + "cost of existing did.** This is what an unresolved data question "
                            + "looks like when it arrives eighteen months late."),

                        new LabChapter(On(2024, 3, 22), LabChapterKind.Setback,
                            "The chief executive resigns",
                            "He leaves the role and the board on the same day, saying publicly that "
                            + "you cannot beat centralised AI with more centralised AI. The company "
                            + "has no revenue model and now has no founder either."),

                        new LabChapter(On(2024, 4, 3), LabChapterKind.Setback,
                            "Layoffs and restructuring",
                            "About a tenth of the staff go. The community that made the company "
                            + "famous has already forked the models and does not need the company to "
                            + "keep using them."),
                    }),

                // ------------------------------------------- the challenger that was bought in place

                new LabDossier(CompetitorId.InflectionAi, "IntroduceAI", On(2022, 3, 1),
                    "Palo Alto",
                    "One personal assistant, tuned for how it talks rather than what it scores.",
                    "Founded in 2022 by a co-founder of a famous London research lab alongside a "
                    + "well known technology investor, and funded on day one at a scale that made "
                    + "it a serious challenger. Its assistant was deliberately worse at tasks and "
                    + "deliberately better at conversation, and a million people a day preferred it "
                    + "that way. It never lost to anybody. It was hired. The company was taken "
                    + "apart by an announcement rather than by a competitor, twelve days after "
                    + "reaching the frontier, and none of that is visible in a benchmark.",
                    LabFate.Absorbed,
                    new[]
                    {
                        new LabChapter(On(2023, 5, 2), LabChapterKind.Milestone,
                            "A companion, not a tool",
                            "The assistant launches. It is deliberately worse at tasks and "
                            + "deliberately better at conversation, and a lot of people prefer it."),

                        new LabChapter(On(2023, 6, 29), LabChapterKind.Funding,
                            "One point three billion dollars",
                            "One of the largest raises in the field, from a cloud vendor, a chip "
                            + "maker and a row of famous names. On paper this is now a top three "
                            + "independent lab."),

                        new LabChapter(On(2024, 3, 7), LabChapterKind.Milestone,
                            "Level with the frontier on the benchmarks that matter to them",
                            "A million people use it daily, six million monthly, and the new model "
                            + "is competitive with the best on several published tests. The "
                            + "challenger is real."),

                        new LabChapter(On(2024, 3, 19), LabChapterKind.Exit,
                            "A cloud vendor hires the chief executive and most of the team",
                            "Twelve days after looking like a genuine third force, the company is "
                            + "hollowed out in a single announcement. The people go to the vendor, "
                            + "the models are licensed for a reported six hundred and fifty million "
                            + "dollars, and what is left pivots to selling to businesses. **Nobody "
                            + "was acquired and nobody went bankrupt.** That is the part worth "
                            + "understanding."),
                    }),

                // ------------------------------------------------ the European bid, out-scaled

                new LabDossier(CompetitorId.AlephAlpha, "Algho Alpha", On(2019, 1, 1), "Heidelberg",
                    "Sovereign AI. A model a European government or bank can run without asking "
                    + "anybody's permission.",
                    "Built its own large multilingual model years before it was fashionable, and "
                    + "argued that Europe could not depend on American labs for infrastructure this "
                    + "important. European industry agreed hard enough to put half a billion in: a "
                    + "retail group, an engineering conglomerate, an enterprise software house and "
                    + "a hardware vendor, rather than venture capital. The argument was right and "
                    + "the arithmetic was brutal. Half a billion is a rounding error against a "
                    + "frontier that triples its training compute every year.",
                    LabFate.Struggling,
                    new[]
                    {
                        new LabChapter(On(2022, 4, 14), LabChapterKind.Milestone,
                            "Europe builds its own",
                            "A large multilingual model out of Germany, at a time when almost "
                            + "nobody outside the United States is attempting one."),

                        new LabChapter(On(2023, 11, 6), LabChapterKind.Funding,
                            "Half a billion, from European industry rather than venture capital",
                            "A retail group, an engineering conglomerate, an enterprise software "
                            + "company and a hardware vendor put in more than five hundred million "
                            + "between them. This is not a bet on a product. It is a bet that "
                            + "Europe needs one of these and cannot buy it."),

                        new LabChapter(On(2024, 9, 1), LabChapterKind.Setback,
                            "The scale gap",
                            "The frontier labs are now spending more on a single training run than "
                            + "this company has raised in its life. Competing on raw capability "
                            + "stops being a plan and the company moves toward tooling, "
                            + "deployment and sovereignty instead. **Being right about the problem "
                            + "does not mean you can afford the answer.**"),

                        new LabChapter(On(2026, 3, 2), LabChapterKind.Exit,
                            "Strategic consolidation",
                            "The European bid and a North American enterprise lab combine rather "
                            + "than continue separately. Two companies that both refused to chase "
                            + "the frontier discover they were solving the same problem from "
                            + "different continents.", isProjection: true),
                    }),

                // ------------------------------------------------------------- the survivor

                // **The one invented company on this board**, and the only reason it is here is
                // that Emil is a permanent character. A cousin who walks you through your first hour
                // and then exists nowhere in the world is a voice from nothing.
                //
                // Everything about the arc is deliberately unremarkable. He is not a rival: he is
                // somebody the player knows who happens to run a small shop, always a step behind
                // the middle of the board and never falling off it. A cousin who turned out to be
                // secretly winning would make the favour he does in the tutorial read as charity.
                //
                // Every chapter carries IsProjection, because the honesty flag is about not passing
                // invention off as record and this is invention from end to end.
                new LabDossier(CompetitorId.ESolutions, "E-Solutions", On(2021, 9, 1), "Radom",
                    "Build the boring thing, make it work, invoice for it.",
                    "Six people above a hardware shop, doing the work nobody writes articles about: "
                    + "helpdesk assistants for regional firms, back office automation, a support "
                    + "line that answers. It has never been near the frontier and has never tried "
                    + "to be. Your cousin started it a year before you started yours, which is the "
                    + "entire reason he has opinions about your first year.",
                    LabFate.Independent,
                    new[]
                    {
                        new LabChapter(On(2023, 2, 20), LabChapterKind.Milestone,
                            "First product out of the door",
                            "A helpdesk assistant for three regional insurers. Nobody covered it. "
                            + "It has been paying six salaries ever since.",
                            isProjection: true),

                        new LabChapter(On(2024, 6, 3), LabChapterKind.Milestone,
                            "Turns down an acquisition",
                            "An offer arrives that would have made him comfortable and made the "
                            + "company somebody else's. He says the quiet part out loud in a trade "
                            + "interview: he likes the work and does not want a manager.",
                            isProjection: true),

                        new LabChapter(On(2026, 5, 19), LabChapterKind.Milestone,
                            "Still here",
                            "Four years, four products, no funding round and no headline. Whatever "
                            + "else has happened on this board, E-Solutions has invoiced every "
                            + "month of it.",
                            isProjection: true)
                    }),

                new LabDossier(CompetitorId.Cohere, "Gohere", On(2019, 1, 1), "Toronto",
                    "Sell to companies, run inside their walls, never chase the consumer.",
                    "Founded in 2019 by, among others, one of the authors of the 2017 attention "
                    + "paper that every model in this game descends from. It made one decision "
                    + "early and never moved off it: not the frontier lab, and not famous. Models "
                    + "deployed inside a customer's own infrastructure, sold on auditability rather "
                    + "than on intelligence, to buyers who do not change vendor every six months. "
                    + "You can ignore this company for four years. It was worth close to seven "
                    + "billion dollars by the time the loud ones started falling over.",
                    LabFate.Independent,
                    new[]
                    {
                        new LabChapter(On(2023, 6, 8), LabChapterKind.Milestone,
                            "The unglamorous option ships",
                            "A model family sold to businesses, deployable inside a customer's own "
                            + "infrastructure. No consumer app, no launch spectacle, no benchmark "
                            + "argument."),

                        new LabChapter(On(2024, 4, 4), LabChapterKind.Milestone,
                            "Retrieval, aimed squarely at companies with their own documents",
                            "A model built around answering from a customer's own material rather "
                            + "than from what it memorised. The pitch is not intelligence, it is "
                            + "auditability."),

                        new LabChapter(On(2025, 8, 14), LabChapterKind.Funding,
                            "Still here, and worth more than ever",
                            "A raise at close to seven billion dollars, three years after the "
                            + "companies that got all the attention started falling over. **The "
                            + "lesson is not that quiet wins. It is that a company which knows "
                            + "exactly who its customer is does not have to survive the same "
                            + "storms.**"),
                    }),
            };

            var index = new Dictionary<CompetitorId, LabDossier>(all.Length);
            foreach (var dossier in all)
            {
                index[dossier.Competitor] = dossier;
            }

            return index;
        }
    }
}
