using System.Collections.Generic;

namespace ScalingLaws.Data
{
    /// <summary>The languages the game ships in.</summary>
    public enum Language
    {
        English = 0,
        Polish = 1
    }

    /// <summary>
    /// Every word the player reads, in one place.
    ///
    /// **Built plural-first, because Polish is why this exists.** English needs two forms — "1 desk"
    /// and "2 desks" — so a translation layer designed against English gets away with a boolean.
    /// Polish needs three: *1 biurko*, *2 biurka*, *5 biurek*, and the rule is not "one or many" but
    /// a genuine arithmetic on the last two digits. Adding that later means revisiting every string
    /// that has ever had a number in it, which is most of them.
    ///
    /// Dictionaries in C# rather than JSON on disk: no load order to get wrong, no parse to fail at
    /// runtime, and a missing key is a compile-time-adjacent problem rather than a blank label in a
    /// shipped build.
    ///
    /// **A missing translation never shows as empty.** It falls back to English and is recorded, so
    /// the gaps can be listed rather than discovered by a player looking at a blank button.
    /// </summary>
    public static class Loc
    {
        /// <summary>What the game is currently reading. English until something changes it.</summary>
        public static Language Current { get; set; } = Language.English;

        /// <summary>
        /// Keys asked for that the current language does not have.
        ///
        /// Kept rather than logged, so a screen can be walked and the whole gap listed at once. A
        /// translator wants the list; a console line per miss is noise.
        /// </summary>
        public static readonly HashSet<string> Missing = new();

        /// <summary>
        /// The text for a key.
        ///
        /// Falls back to English, then to the key itself. The key is deliberately the last resort
        /// rather than an empty string: "team.hire.now" on a button is ugly and unmistakable, and a
        /// blank button is neither.
        /// </summary>
        public static string T(string key)
        {
            if (string.IsNullOrEmpty(key))
            {
                return string.Empty;
            }

            var table = TableFor(Current);

            if (table.TryGetValue(key, out var text) && !string.IsNullOrEmpty(text))
            {
                return text;
            }

            Missing.Add(key);

            return English.TryGetValue(key, out var fallback) ? fallback : key;
        }

        /// <summary>Text with {0}, {1} … filled in. Same fallback rules as <see cref="T"/>.</summary>
        public static string T(string key, params object[] values) =>
            string.Format(T(key), values);

        /// <summary>
        /// The right form of a counted noun.
        ///
        /// Three keys per noun rather than one: <c>desk.one</c>, <c>desk.few</c>, <c>desk.many</c>.
        /// English fills few and many with the same plural and loses nothing; Polish uses all three
        /// and is correct without any screen knowing that Polish exists.
        ///
        /// The rule below is the real Polish one, not an approximation. Twelve to fourteen take the
        /// many form even though they end in 2, 3 and 4 — which is exactly the case a hand-rolled
        /// "last digit" check gets wrong, and it turns up the first time somebody hires twelve
        /// people.
        /// </summary>
        public static string Plural(int count, string key)
        {
            var form = Current switch
            {
                Language.Polish => PolishForm(count),
                _ => count == 1 ? "one" : "many"
            };

            return T($"{key}.{form}");
        }

        /// <summary>The count and its noun together, which is what almost every caller wants.</summary>
        public static string Counted(int count, string key) =>
            $"{count:N0} {Plural(count, key)}";

        private static string PolishForm(int count)
        {
            var absolute = count < 0 ? -count : count;

            if (absolute == 1)
            {
                return "one";
            }

            var last = absolute % 10;
            var lastTwo = absolute % 100;

            // 2, 3, 4 take the few form — but 12, 13, 14 do not, and neither do 112, 113, 114.
            return last is >= 2 and <= 4 && lastTwo is < 12 or > 14 ? "few" : "many";
        }

        private static Dictionary<string, string> TableFor(Language language) => language switch
        {
            Language.Polish => Polish,
            _ => English
        };

        // ==========================================================================================
        // ENGLISH — the source of truth. Every key lives here first.
        //
        // Keys are dotted and grouped by where they appear: screen, then thing. A key nobody can
        // place from its name is a key nobody will translate correctly.
        // ==========================================================================================

        public static readonly Dictionary<string, string> English = new()
        {
            // ---- counted nouns. Polish takes three forms and the rule is real: 2-4 take 'few', 12-14 take 'many' ----
            ["noun.desk.one"] = "desk",
            ["noun.desk.few"] = "desks",
            ["noun.desk.many"] = "desks",
            ["noun.day.one"] = "day",
            ["noun.day.few"] = "days",
            ["noun.day.many"] = "days",
            ["noun.month.one"] = "month",
            ["noun.month.few"] = "months",
            ["noun.month.many"] = "months",
            ["noun.model.one"] = "model",
            ["noun.model.few"] = "models",
            ["noun.model.many"] = "models",
            ["noun.person.one"] = "person",
            ["noun.person.few"] = "people",
            ["noun.person.many"] = "people",
            ["noun.offer.one"] = "offer",
            ["noun.offer.few"] = "offers",
            ["noun.offer.many"] = "offers",
            ["noun.upgrade.one"] = "upgrade",
            ["noun.upgrade.few"] = "upgrades",
            ["noun.upgrade.many"] = "upgrades",

            // ---- the bottom bar ----------------------------------------------------------------------
            ["hud.site"] = "SITE",
            ["hud.model"] = "MODEL",
            ["hud.research"] = "RESEARCH",
            ["hud.architecture"] = "ARCHITECTURE",
            ["hud.upgrade"] = "UPGRADE",
            ["hud.team"] = "TEAM",
            ["hud.compute"] = "COMPUTE",
            ["hud.business"] = "BUSINESS",
            ["hud.release"] = "RELEASE",
            ["hud.capital"] = "CAPITAL",
            ["hud.ranking"] = "RANKING",
            ["hud.intel"] = "INTEL",
            ["hud.marketing"] = "MARKETING",
            ["hud.news"] = "NEWS",
            ["hud.mail"] = "@ MAIL",

            // ---- words the whole interface uses ------------------------------------------------------
            ["common.close"] = "CLOSE",
            ["common.cancel"] = "CANCEL",
            ["common.confirm"] = "CONFIRM",
            ["common.back"] = "BACK",
            ["common.next"] = "Next",
            ["common.skip"] = "Skip",
            ["common.done"] = "DONE",
            ["common.buy"] = "BUY",
            ["common.sell"] = "SELL",
            ["common.start"] = "START",
            ["common.none"] = "none",
            ["common.locked"] = "LOCKED",
            ["common.in_progress"] = "IN PROGRESS",
            ["common.a_day"] = "a day",
            ["common.a_month"] = "a month",
            ["common.a_year"] = "a year",
            ["common.per_hour"] = "an hour",

            // ---- settings ----------------------------------------------------------------------------
            ["settings.title"] = "SETTINGS",
            ["settings.note"] = "These are kept separately from a campaign and survive deleting one.",
            ["settings.language"] = "LANGUAGE",
            ["settings.language.note"] = "The interface, the phone and every explanation card.",
            ["settings.volume"] = "MASTER VOLUME",
            ["settings.volume.note"] = "All music and interface sound.",
            ["settings.fullscreen"] = "FULLSCREEN",
            ["settings.fullscreen.note"] = "Use a borderless full screen window.",
            ["settings.motion"] = "REDUCE MOTION",
            ["settings.motion.note"] = "Shortens the opening sequence and holds the office camera still.",

            // ---- the model hub -----------------------------------------------------------------------
            ["model.title"] = "MODEL",
            ["model.new"] = "NEW MODEL",
            ["model.new.note"] = "Design a training run from nothing. Months, and the biggest bill the company pays.",
            ["model.upgrade"] = "UPGRADE",
            ["model.upgrade.note"] = "Improve something already on sale, without training it again.",
            ["model.upgrade.none"] = "Nothing on sale yet. Build one first and this is how you keep it current.",
            ["model.service"] = "SERVICE",
            ["model.service.none"] = "Nothing is being served. The dial starts when a model does.",
            ["model.month"] = "THIS MONTH",
            ["model.income"] = "INCOME",
            ["model.costs"] = "COSTS",
            ["model.from_subscriptions"] = "FROM SUBSCRIPTIONS",
            ["model.subscribers"] = "SUBSCRIBERS",
            ["model.on_sale"] = "ON SALE",
            ["model.on_sale.none"] = "Nothing on sale. Build a model, then release it.",
            ["model.manage"] = "MANAGE WHAT IS ON SALE",
            ["model.column.name"] = "MODEL",
            ["model.column.users"] = "USERS",
            ["model.column.subs"] = "SUBS",
            ["model.column.income"] = "NET INCOME",

            // ---- the team ----------------------------------------------------------------------------
            ["team.title"] = "TEAM",
            ["team.positions"] = "POSITIONS",
            ["team.payroll"] = "ON THE PAYROLL",
            ["team.worth"] = "WHAT THE TEAM IS WORTH",
            ["team.where"] = "WHERE YOU WORK",
            ["team.hire_now"] = "HIRE NOW",
            ["team.hire_remote"] = "HIRE NOW - REMOTE",
            ["team.let_go"] = "LET GO",
            ["team.details"] = "DETAILS",
            ["team.workplaces_available"] = "{0} workplaces available",

            // ---- hiring ------------------------------------------------------------------------------
            ["hire.where_looking"] = "WHERE ARE YOU LOOKING?",
            ["hire.agency"] = "EMPLOYMENT AGENCY",
            ["hire.specialist"] = "FIND A SPECIALIST",
            ["hire.get_in_touch"] = "GET IN TOUCH",
            ["hire.your_offer"] = "YOUR OFFER",
            ["hire.hourly_wage"] = "Hourly wage",
            ["hire.signing_bonus"] = "Signing bonus",
            ["hire.send_offer"] = "SEND OFFER",
            ["hire.decline"] = "DECLINE",
            ["hire.negotiate"] = "NEGOTIATE",
            ["hire.wage"] = "WAGE",
            ["hire.worth"] = "WORTH",
            ["hire.mood.delighted"] = "They would take this happily.",
            ["hire.mood.pleased"] = "This is enough. They would sign.",
            ["hire.mood.neutral"] = "Borderline. It could go either way.",
            ["hire.mood.cool"] = "Short. They will push back rather than leave.",
            ["hire.mood.insulted"] = "Insulting. Send this and they are gone.",

            // ---- compute -----------------------------------------------------------------------------
            ["compute.title"] = "COMPUTE",
            ["compute.service"] = "SERVICE",
            ["compute.server_usage"] = "Server Usage",
            ["compute.response_time"] = "Response Time: {0}ms",
            ["compute.right_now"] = "Right now",
            ["compute.online_users"] = "Online users",

            // ---- research ----------------------------------------------------------------------------
            ["research.title"] = "RESEARCH",
            ["research.funding"] = "FUNDING",
            ["research.fixed_budget"] = "A FIXED BUDGET",
            ["research.revenue_share"] = "A SHARE OF REVENUE",
            ["research.begin"] = "BEGIN",
            ["research.what_it_opens"] = "WHAT IT OPENS",
            ["research.needs_first"] = "NEEDS FIRST",
            ["research.fit"] = "FIT",
            ["research.points"] = "POINTS",
            ["research.cash"] = "CASH",
            ["research.duration"] = "DURATION",
            ["research.waiting_compute"] = "Waiting for cluster time. Free some capacity and this finishes.",

            // ---- the upgrade screen ------------------------------------------------------------------
            ["upgrade.title"] = "UPGRADE MODEL",
            ["upgrade.upgrading"] = "UPGRADING",
            ["upgrade.what_it_does"] = "WHAT IT WOULD DO",
            ["upgrade.pick_hint"] = "Pick one or more upgrades on the left. They can be commissioned together and the cluster splits its time between them.",
            ["upgrade.capability"] = "CAPABILITY",
            ["upgrade.brand"] = "BRAND",
            ["upgrade.efficiency"] = "SERVING EFFICIENCY",
            ["upgrade.cost"] = "COST",
            ["upgrade.calendar"] = "CALENDAR",
            ["upgrade.time"] = "TIME",
            ["upgrade.compute"] = "COMPUTE",
            ["upgrade.start_one"] = "START UPGRADE",
            ["upgrade.start_many"] = "START {0} UPGRADES",
            ["upgrade.plan_release"] = "PLAN THE RELEASE",
            ["upgrade.at_ceiling"] = "AT THE CEILING",
            ["upgrade.max"] = "MAX",
            ["upgrade.behind"] = "{0} BEHIND",
            ["upgrade.level"] = "LEVEL {0}",
            ["upgrade.from"] = "FROM {0}",
            ["upgrade.level_market"] = "LEVEL {0}   ·   MARKET {1}",
            ["upgrade.none_live"] = "Nothing is live yet. Release a model and this fills up.",
            ["upgrade.version"] = "Version: {0}",
            ["upgrade.base"] = "Base",

            // ---- the release planner -----------------------------------------------------------------
            ["release.version_name"] = "Version name:",
            ["release.change_price"] = "CHANGE PRICE",
            ["release.change"] = "CHANGE",
            ["release.apply"] = "APPLY",
            ["release.free_tokens"] = "FREE TOKENS PER USER",
            ["release.per_month"] = "{0} / month",
            ["release.per_day"] = "{0}/day",
            ["release.what_ships"] = "WHAT SHIPS IN THIS VERSION",
            ["release.nothing_ships"] = "Price and allowance only. No post-training work in this one.",
            ["release.ship"] = "SHIP THIS VERSION",
            ["release.in_use"] = "RELEASES IN USE",
            ["release.current"] = "current",
            ["release.market_sees"] = "WHAT THE MARKET SEES",
            ["release.effective_capability"] = "EFFECTIVE CAPABILITY",
            ["release.best_version"] = "BEST VERSION SHIPPED",
            ["release.average_price"] = "AVERAGE PRICE PAID",
            ["release.count_one"] = "1 release in use",
            ["release.count_many"] = "{0} releases in use",
            ["release.older_holds"] = "More people are still on {0} than on the current release. An update nobody liked does not take the audience.",

            // ---- the architecture screen -------------------------------------------------------------
            ["arch.title"] = "ARCHITECTURE",
            ["arch.strap"] = "The house family every later model inherits from. Budget and calendar decide how far the programme reaches. Focus decides whether it reaches anywhere at all.",
            ["arch.programme"] = "PROGRAMME",
            ["arch.family_name"] = "FAMILY NAME",
            ["arch.slot"] = "SLOT",
            ["arch.build_on"] = "BUILD ON",
            ["arch.clean_sheet"] = "Clean sheet",
            ["arch.slot_empty"] = "{0} - empty",
            ["arch.slot_overwrite"] = "{0} - overwrite {1}",
            ["arch.iterate_hint"] = "Iterating a family you already own costs 40% less and takes 40% less time, and each generation reaches about half as far as the one before it. Families plateau. A clean sheet costs full price and has no such ceiling.",
            ["arch.directions"] = "RESEARCH DIRECTIONS",
            ["arch.directions_hint"] = "Effort is relative. Spreading it evenly across all five buys a fifth of the depth in each, which is a real choice and usually a bad one.",
            ["arch.investment"] = "INVESTMENT",
            ["arch.budget"] = "RESEARCH BUDGET",
            ["arch.length"] = "PROGRAMME LENGTH",
            ["arch.investment_hint"] = "Money and time are a geometric mean, not a sum. A billion dollars in three months is not a breakthrough, and neither is three years of two people.",
            ["arch.outcome"] = "LIKELY OUTCOME",
            ["arch.research_power"] = "RESEARCH POWER",
            ["arch.focus"] = "FOCUS",
            ["arch.certainty"] = "CERTAINTY",
            ["arch.would_produce"] = "WHAT IT WOULD PRODUCE",
            ["arch.active_parameters"] = "ACTIVE PARAMETERS",
            ["arch.quality_per_parameter"] = "QUALITY PER PARAMETER",
            ["arch.training_efficiency"] = "TRAINING EFFICIENCY",
            ["arch.serving_multiplier"] = "SERVING MULTIPLIER",
            ["arch.capability_bonus"] = "CAPABILITY BONUS",
            ["arch.cash"] = "CASH",
            ["arch.compute"] = "COMPUTE",
            ["arch.saves"] = "SAVES",
            ["arch.commit"] = "COMMIT THE PROGRAMME",
            ["arch.families"] = "HOUSE FAMILIES",
            ["arch.no_families"] = "No house families yet. Everything is running on published techniques, which is exactly what every rival is also running on.",
            ["arch.abandon"] = "ABANDON THIS PROGRAMME",
            ["arch.abandon_confirm"] = "CONFIRM, NOTHING COMES BACK",
            ["arch.day_of"] = "day {0} of {1}",
            ["arch.verdict.busy"] = "A family programme is already running. One at a time.",
            ["arch.verdict.coin_toss"] = "Runnable, and underfunded enough that the result is close to a coin toss. More money or more calendar narrows the band.",
            ["arch.verdict.spread"] = "Runnable, and the effort is spread so evenly that no direction goes deep enough to matter.",
            ["arch.verdict.good"] = "Runnable, and focused enough to land somewhere useful.",
            ["arch.beyond_ceiling"] = "{0} is pushed further than the company knows how to go. Research opens the rest of that slider.",

            // ---- the paid desks ----------------------------------------------------------------------
            ["intel.title"] = "INTELLIGENCE",
            ["intel.strap"] = "What the research desk believes is coming. Confidence is what the desk claims about itself, and it is always higher than how often the desk turns out to be right.",
            ["intel.join"] = "JOIN",
            ["intel.on_retainer"] = "ON RETAINER  ·  CLICK TO CANCEL",
            ["intel.no_notes"] = "No notes yet. A desk on retainer files its first one within a few weeks.",

            // ---- banners -----------------------------------------------------------------------------
            ["banner.researching"] = "RESEARCHING",
            ["banner.research_waiting"] = "RESEARCH WAITING",
            ["banner.arranging"] = "ARRANGING",
            ["banner.working_on_upgrade"] = "WORKING ON UPGRADE",
            ["banner.days_left.one"] = "1 day left",
            ["banner.days_left.many"] = "{0} days left",
            ["banner.finishing_today"] = "finishing today",

            // ---- money and time ----------------------------------------------------------------------
            ["money.needs"] = "NEEDS {0}",
            ["money.a_day"] = "{0} a day",
            ["money.a_month"] = "{0} a month",
            ["time.months"] = "{0} months",
            ["time.days"] = "{0} days",

            // ---- the (i) cards -----------------------------------------------------------------------
            ["tech.sparsity.title"] = "SPARSITY",
            ["tech.sparsity.what"] = "A dense model runs every parameter for every token. A sparse one routes each token to a few experts and leaves the rest idle, so a model can be enormous to hold and small to run.",
            ["tech.sparsity.affects"] = "Moves the family's active parameter fraction, which the market reads as serving burden and the planner reads as compute per token. It does not make the model better at anything.",
            ["tech.sparsity.high"] = "Cheap to serve at a size nobody else can afford to run, and the headroom to keep growing. Routing is fragile: quality per parameter falls, and a badly routed model is worse than a dense one half its size.",
            ["tech.sparsity.low"] = "Every parameter earns its keep and the model behaves predictably. You pay for all of it on every token, forever, and the price war is won by whoever pays less.",
            ["tech.throughput.title"] = "THROUGHPUT",
            ["tech.throughput.what"] = "How much of the cluster is doing arithmetic rather than waiting. Overlapping communication with computation, fusing kernels, keeping the pipeline full.",
            ["tech.throughput.affects"] = "Divides the calendar on every training run in the family. Nothing else. It does not raise the ceiling and it does not lower the bill.",
            ["tech.throughput.high"] = "Runs finish sooner, so the same fleet ships more models a year and a launch window can still be caught. Buys nothing at all if the company is not compute bound.",
            ["tech.throughput.low"] = "Runs take as long as they take. The effort goes into directions that change what the model is rather than when it arrives, which is the right trade while the frontier is moving slowly.",
            ["tech.quality.title"] = "QUALITY PER PARAMETER",
            ["tech.quality.what"] = "Getting more capability out of the same weights: better initialisation, better objectives, better data ordering, better use of the tokens you already paid for.",
            ["tech.quality.affects"] = "Raises the capability ceiling of every model in the family. It is the only direction that makes the models themselves better, and it is the slowest to pay off.",
            ["tech.quality.high"] = "Every model this family ever produces scores higher for the same compute, which compounds across a decade. It is the most expensive direction and the last to show up in the books.",
            ["tech.quality.low"] = "Nothing improves on its own and you stay where the published techniques are. Correct when the company is losing on cost rather than on capability, because a better model nobody can afford to run is not a product.",
            ["tech.serving.title"] = "SERVING COST",
            ["tech.serving.what"] = "What a token costs once the model is live: quantisation, caching, batching, speculative decoding. The engineering nobody writes papers about.",
            ["tech.serving.affects"] = "Multiplies inference cost per token for every model in the family. Invisible until the day a rival cuts their price and you have to decide whether you can follow.",
            ["tech.serving.high"] = "Margin on every token forever, and the ability to survive a price war you did not start. It changes nothing a customer can see, so it never wins you a launch.",
            ["tech.serving.low"] = "The effort goes somewhere visible instead. Fine while demand is small; a company serving billions of tokens a day at a bad multiplier is losing money on every one of them and cannot advertise its way out.",
            ["tech.reasoning.title"] = "REASONING",
            ["tech.reasoning.what"] = "Structure the model is trained to use rather than scale it is given: chains of thought, process rewards, search at inference time.",
            ["tech.reasoning.affects"] = "A direct capability bonus that does not come from parameters or tokens. It is the one direction that is not on the scaling curve, which is why it is the most expensive per point.",
            ["tech.reasoning.high"] = "Capability the competition cannot buy with a bigger cluster, and the only lever left once the frontier is compute limited. Costs the most per point of anything here.",
            ["tech.reasoning.low"] = "Scale and data do the work. Cheaper per point of capability today, and it stays cheaper right up until everybody's models are the same size and nobody can tell the products apart.",
            ["tech.budget.title"] = "RESEARCH BUDGET",
            ["tech.budget.what"] = "What the company puts behind the programme. Paid whether or not it works.",
            ["tech.budget.affects"] = "Combines with the calendar as a geometric mean, then sets how wide the outcome band is. Money alone cannot buy a breakthrough and it cannot buy time.",
            ["tech.budget.high"] = "A narrow band around a good result: an expensive programme mostly lands where the screen said it would. It is capital that never comes back and it is gone whether the family is any good or not.",
            ["tech.budget.low"] = "Cheap, and a lottery ticket. The band is wide enough that the result could be better than the expected number or nearly worthless, and a young company sometimes has to take that bet.",
            ["tech.length.title"] = "PROGRAMME LENGTH",
            ["tech.length.what"] = "How long the research runs before it delivers a family.",
            ["tech.length.affects"] = "The other half of the geometric mean, and it also narrows the outcome band. The calendar is the part that cannot be bought out of.",
            ["tech.length.high"] = "A deeper, more certain result, and a slot blocked for a year or more while the frontier keeps moving. One family programme at a time.",
            ["tech.length.low"] = "A family in hand months earlier, at a result that could be anything within the band. Shipping something ordinary this year often beats shipping something good in two.",

            // ---- model traits, which are what the upgrade screen upgrades ----------------------------
            ["trait.reasoning.name"] = "Reasoning",
            ["trait.reasoning.desc"] = "Multi-step problems. The most expensive points on the board and the ones buyers notice first.",
            ["trait.knowledge.name"] = "Knowledge",
            ["trait.knowledge.desc"] = "Breadth of recall. Cheap to buy, and the first thing a rival matches.",
            ["trait.coding.name"] = "Coding",
            ["trait.coding.desc"] = "Code generation and repair. The single highest paying segment in the whole market.",
            ["trait.multilingual.name"] = "Multilanguage",
            ["trait.multilingual.desc"] = "Languages beyond English. Opens regions rather than raising the ceiling.",
            ["trait.multimodal.name"] = "Multimodal",
            ["trait.multimodal.desc"] = "Images, audio and video in and out. Expensive to train, and buyers assume it by 2025.",
            ["trait.context.name"] = "Context length",
            ["trait.context.desc"] = "How much the model can hold at once. Sells to enterprises, costs memory to serve.",
            ["trait.safety.name"] = "Safety",
            ["trait.safety.desc"] = "Refusals that land in the right place. Invisible when it works. The only defence against an incident.",
            ["trait.latency.name"] = "Speed",
            ["trait.latency.desc"] = "Time to first token. Buyers feel this before they read any benchmark.",
            ["trait.efficiency.name"] = "Optimisation",
            ["trait.efficiency.desc"] = "Cost per served token. Buys no headlines and decides whether the company survives a price war.",
            ["trait.tooluse.name"] = "Tool use",
            ["trait.tooluse.desc"] = "Calling things that are not the model. The whole agent market runs on this.",
            ["trait.ecosystem.name"] = "Ecosystem",
            ["trait.ecosystem.desc"] = "SDKs, integrations, everyone else building on top of you. Slow to grow and slow to lose.",

            // ---- Emil, the phone and the tour --------------------------------------------------------
            ["guide.cousin.name"] = "Emil",
            ["guide.cousin.handle"] = "Emil bro",
            ["guide.cousin.relation"] = "Cousin :3",
            ["guide.welcome"] = "Welcome back!",
            ["guide.backlog.1"] = "Heey mate, is it done? Papers signed?",
            ["guide.backlog.2"] = "I'll spare you what I went through on my own... I'll show you how this business works",
            ["guide.live.1"] = "._.",
            ["guide.live.2"] = "Mate, party's over. Unless you're inviting me!",
            ["guide.reply.accept"] = "Hey, all done. Walk me through it, quick?",
            ["guide.reply.decline"] = "Can't waste time ;) See you in the rankings.",
            ["guide.reply.declined"] = "Ohoho mate, hope you know what you're doing. PS. One shall rule.",
            ["guide.next"] = "Next",
            ["guide.show_me"] = "Show me",
            ["guide.skip"] = "I'll take it from here",
            ["guide.task"] = "TASK",
            ["guide.favour.spent"] = "{0} begun, on the house. Emil's mate covered it, about {1} days.",
            ["guide.step.hud"] = "Right. That bar at the bottom is your command centre. Literally everything this company does gets clicked from there. Don't learn it by heart, just know that if you're looking for something, it's in that bar.",
            ["guide.step.burn"] = "Okay... I'm not going to do the marketing pitch. I'll tell you straight away about the one thing that'll bankrupt you fastest.",
            ["guide.step.compute_open"] = "Click COMPUTE. That's where you rent the machines.",
            ["guide.step.compute_rent"] = "You don't own servers, so you're stuck renting. And watch out, these people are hyenas. It is very easy to miss that you're suddenly burning millions a week on capacity you don't use. What for? So a user gets 10 ping instead of 12? The one on 90 won't feel the difference.",
            ["guide.step.compute_dial"] = "That dial is your load. Sitting low, you're paying for air. Pinned to the ceiling, people wait and then they leave. Keep it in the middle and check it after every launch.",
            ["guide.step.research_pitch"] = "Now the part nobody explains properly. You can have all the money in the world and still not be able to build anything, because you don't know how yet. That's what research is.",
            ["guide.step.research_open"] = "Click RESEARCH. I want you to see this before you spend anything.",
            ["guide.step.research_tree"] = "The whole tree is visible from day one, including the stuff at the end you can't touch for years. That's on purpose. You're meant to see what's coming and plan for it.",
            ["guide.step.research_cost"] = "Two currencies. Points, which you only earn by actually building things, and a bit of cash. Money alone buys you nothing here. That's the whole idea.",
            ["guide.step.research_gift"] = "And here's the thing. I've got a mate at one of the labs who owes me a favour. Pick anything from era one and it's on him. Seriously, anything. Just pick one.",
            ["guide.step.research_gift_note"] = "Points and cash, both covered. Don't overthink it. Precision is the cheapest thing on the board and it speeds up every training run you'll ever do, if you want my honest tip.",
            ["guide.step.model_open"] = "Now the good part. Click MODEL.",
            ["guide.step.model_hub"] = "Here's everything you sell and what it brings in. Two doors: build a new model, or improve the one you already have. Improving is cheaper and faster, but it won't raise the ceiling.",
            ["guide.step.create_open"] = "Go into NEW MODEL. I'll show you what this actually stands on.",
            ["guide.step.create_scale"] = "SCALE is the heart of it. Parameters and tokens. Remember one thing: there is an optimal ratio of tokens to parameters and the game shows it to you. Overshoot either way and you burn compute for a worse model at a higher price.",
            ["guide.step.create_locked"] = "See those darkened ends on the sliders? Not a bug. That's how much you can supervise. Research opens them, and that is exactly why you do it.",
            ["guide.step.create_precision"] = "Precision. You start on FP64 because you don't know anything else yet. It is the slowest option that exists. The first node in the tree opens FP32, then BF16, and that is the cheapest training speedup you will ever buy.",
            ["guide.step.create_safety"] = "SAFETY. I know you want to click through it. Don't. The regulator turns up after an incident and looks at what you had switched on the day you launched. Cheap now or very expensive later.",
            ["guide.step.create_review"] = "REVIEW at the end shows you what comes out, how long it takes and what you pay. That time is the truth. Training takes exactly as long as it says here.",
            ["guide.step.arch_pitch"] = "One more thing before you go and build. There is a level above the model, and most people find it far too late.",
            ["guide.step.arch_open"] = "ARCHITECTURE. Go on.",
            ["guide.step.arch_what"] = "A house family is what every model you build afterwards inherits from. Get this right once and every model for the next five years is cheaper, better or both. Get it wrong and you paid for a year of nothing.",
            ["guide.step.arch_locked"] = "Same story with the sliders here. Most of every one is shut, and research opens it. Don't spread the effort across all five either. A fifth of the depth in each direction is a real choice and it is almost always the wrong one.",
            ["guide.step.arch_info"] = "See those little (i) circles? Click one. It tells you what the thing actually does, and what you get at each end of the slider. I wish somebody had written those for me.",
            ["guide.step.after_release"] = "Right, say you've shipped. Here's where most people sit down and relax, and it is the single most expensive mistake in this whole business.",
            ["guide.step.upgrade_open"] = "Click UPGRADE. This is the screen that keeps you alive between models.",
            ["guide.step.upgrade_tiles"] = "Everything you could improve on something already selling. No new training run, no six months. Look at the red badges: that's how far behind the market you already are on each one. That number grows on its own while you do nothing.",
            ["guide.step.upgrade_release"] = "And when you've picked, you don't just commission it. You plan a version: give it a name, set the price, decide the free allowance. That's the green button.",
            ["guide.step.versions"] = "And one thing nobody warns you about. Shipping an update does not move everybody onto it. If people don't like it they stay on the old one, and you'll see it on that list. A bad release can sit at nine per cent while the thing from six months ago holds seventy.",
            ["guide.step.dream"] = "Hah. And if it works out and you don't burn out on the way, you'll have lovely offices, apartments, server rooms, cars to pick from ;)",
            ["guide.step.offices_open"] = "I'm not even joking. Look, this is where you move house.",
            ["guide.step.offices"] = "Rent or buy. Renting is a bill you can walk away from when things go badly. Buying is capital that never comes back and it ends the rent forever. About ten years of rent, so if you'll still be here in a decade, buy.",
            ["guide.step.offices_desks"] = "And the desks are what caps hiring. Not the money, the desks. A company that can afford twenty people and has ten chairs employs ten people.",
            ["guide.step.wrap"] = "And that's me done. Build the first model, ship it, watch the money. Anything comes up, write. Phone's in the corner.",
            ["guide.task.first_research"] = "Start your first research",
            ["guide.task.first_model"] = "Create your first model",
            ["guide.task.first_release"] = "Release your first model",
            ["guide.task.first_upgrade"] = "Upgrade a model you sell",
            ["guide.task.double_cash"] = "Double the company budget",

        };

        // ==========================================================================================
        // POLISH — the skeleton.
        //
        // **Every key from English is here with an empty string.** That is deliberate: an empty
        // value falls back to English and lands in Missing, so the gap is visible and listable
        // rather than silent. Fill a line, save, run the game — that is the whole loop.
        //
        // Three rules for whoever fills this in:
        //
        //   1. Plurals need all three forms. one / few / many. Never leave few empty and hope.
        //   2. Placeholders keep their order. {0} is the first value the code passes, and moving
        //      it changes what the sentence says rather than how it reads.
        //   3. Polish runs roughly 15% longer than English. A button caption that only just fits
        //      in English will not fit here — say it shorter rather than letting it clip.
        // ==========================================================================================

        public static readonly Dictionary<string, string> Polish = new()
        {
            // ---- counted nouns. Polish takes three forms and the rule is real: 2-4 take 'few', 12-14 take 'many' ----
            ["noun.desk.one"] = "biurko",
            ["noun.desk.few"] = "biurka",
            ["noun.desk.many"] = "biurek",
            ["noun.day.one"] = "dzień",
            ["noun.day.few"] = "dni",
            ["noun.day.many"] = "dni",
            ["noun.month.one"] = "miesiąc",
            ["noun.month.few"] = "miesiące",
            ["noun.month.many"] = "miesięcy",
            ["noun.model.one"] = "model",
            ["noun.model.few"] = "modele",
            ["noun.model.many"] = "modeli",
            ["noun.person.one"] = "osoba",
            ["noun.person.few"] = "osoby",
            ["noun.person.many"] = "osób",
            ["noun.offer.one"] = "oferta",
            ["noun.offer.few"] = "oferty",
            ["noun.offer.many"] = "ofert",
            ["noun.upgrade.one"] = "ulepszenie",
            ["noun.upgrade.few"] = "ulepszenia",
            ["noun.upgrade.many"] = "ulepszeń",

            // ---- the bottom bar ----------------------------------------------------------------------
            ["hud.site"] = "SIEDZIBA",
            ["hud.model"] = "MODEL",
            ["hud.research"] = "BADANIA",
            ["hud.architecture"] = "ARCHITEKTURA",
            ["hud.upgrade"] = "ULEPSZENIA",
            ["hud.team"] = "ZESPÓŁ",
            ["hud.compute"] = "MOC",
            ["hud.business"] = "BIZNES",
            ["hud.release"] = "WYDANIE",
            ["hud.capital"] = "KAPITAŁ",
            ["hud.ranking"] = "RANKING",
            ["hud.intel"] = "WYWIAD",
            ["hud.marketing"] = "MARKETING",
            ["hud.news"] = "WIADOMOŚCI",
            ["hud.mail"] = "@ POCZTA",

            // ---- words the whole interface uses ------------------------------------------------------
            ["common.close"] = "ZAMKNIJ",
            ["common.cancel"] = "ANULUJ",
            ["common.confirm"] = "POTWIERDŹ",
            ["common.back"] = "WSTECZ",
            ["common.next"] = "Dalej",
            ["common.skip"] = "Pomiń",
            ["common.done"] = "GOTOWE",
            ["common.buy"] = "KUP",
            ["common.sell"] = "SPRZEDAJ",
            ["common.start"] = "ZACZNIJ",
            ["common.none"] = "brak",
            ["common.locked"] = "ZABLOKOWANE",
            ["common.in_progress"] = "W TOKU",
            ["common.a_day"] = "dziennie",
            ["common.a_month"] = "miesięcznie",
            ["common.a_year"] = "rocznie",
            ["common.per_hour"] = "za godzinę",

            // ---- settings ----------------------------------------------------------------------------
            ["settings.title"] = "USTAWIENIA",
            ["settings.note"] = "Zapisywane osobno od kampanii. Skasowanie zapisu ich nie ruszy.",
            ["settings.language"] = "JĘZYK",
            ["settings.language.note"] = "Interfejs, telefon i wszystkie karty z objaśnieniami.",
            ["settings.volume"] = "GŁOŚNOŚĆ",
            ["settings.volume.note"] = "Muzyka i dźwięki interfejsu.",
            ["settings.fullscreen"] = "PEŁNY EKRAN",
            ["settings.fullscreen.note"] = "Okno pełnoekranowe bez ramki.",
            ["settings.motion"] = "MNIEJ RUCHU",
            ["settings.motion.note"] = "Skraca sekwencję otwierającą i zatrzymuje kamerę w biurze.",

            // ---- the model hub -----------------------------------------------------------------------
            ["model.title"] = "MODEL",
            ["model.new"] = "NOWY MODEL",
            ["model.new.note"] = "Zaprojektuj trening od zera. Miesiące i największy rachunek, jaki firma płaci.",
            ["model.upgrade"] = "ULEPSZ",
            ["model.upgrade.note"] = "Popraw coś, co już jest w sprzedaży, bez ponownego treningu.",
            ["model.upgrade.none"] = "Nic nie jest w sprzedaży. Zbuduj model, a tędy będziesz go trzymać na czasie.",
            ["model.service"] = "OBSŁUGA",
            ["model.service.none"] = "Nic nie jest serwowane. Wskaźnik ruszy razem z modelem.",
            ["model.month"] = "TEN MIESIĄC",
            ["model.income"] = "PRZYCHÓD",
            ["model.costs"] = "KOSZTY",
            ["model.from_subscriptions"] = "Z SUBSKRYPCJI",
            ["model.subscribers"] = "SUBSKRYBENCI",
            ["model.on_sale"] = "W SPRZEDAŻY",
            ["model.on_sale.none"] = "Nic w sprzedaży. Zbuduj model, potem go wydaj.",
            ["model.manage"] = "ZARZĄDZAJ SPRZEDAŻĄ",
            ["model.column.name"] = "MODEL",
            ["model.column.users"] = "UŻYTKOWNICY",
            ["model.column.subs"] = "SUBSKRYPCJE",
            ["model.column.income"] = "PRZYCHÓD NETTO",

            // ---- the team ----------------------------------------------------------------------------
            ["team.title"] = "ZESPÓŁ",
            ["team.positions"] = "STANOWISKA",
            ["team.payroll"] = "NA LIŚCIE PŁAC",
            ["team.worth"] = "ILE WART JEST ZESPÓŁ",
            ["team.where"] = "GDZIE PRACUJESZ",
            ["team.hire_now"] = "ZATRUDNIJ",
            ["team.hire_remote"] = "ZATRUDNIJ - ZDALNIE",
            ["team.let_go"] = "ZWOLNIJ",
            ["team.details"] = "SZCZEGÓŁY",
            ["team.workplaces_available"] = "wolne stanowiska: {0}",

            // ---- hiring ------------------------------------------------------------------------------
            ["hire.where_looking"] = "GDZIE SZUKASZ?",
            ["hire.agency"] = "AGENCJA PRACY",
            ["hire.specialist"] = "ZNAJDŹ SPECJALISTĘ",
            ["hire.get_in_touch"] = "SKONTAKTUJ SIĘ",
            ["hire.your_offer"] = "TWOJA OFERTA",
            ["hire.hourly_wage"] = "Stawka godzinowa",
            ["hire.signing_bonus"] = "Premia za podpis",
            ["hire.send_offer"] = "WYŚLIJ OFERTĘ",
            ["hire.decline"] = "ODRZUĆ",
            ["hire.negotiate"] = "NEGOCJUJ",
            ["hire.wage"] = "STAWKA",
            ["hire.worth"] = "WARTOŚĆ",
            ["hire.mood.delighted"] = "Wzięliby to z radością.",
            ["hire.mood.pleased"] = "To wystarczy. Podpiszą.",
            ["hire.mood.neutral"] = "Na styk. Może pójść w obie strony.",
            ["hire.mood.cool"] = "Za mało. Będą negocjować, ale nie odejdą.",
            ["hire.mood.insulted"] = "Obraźliwe. Wyślij to, a ich nie ma.",

            // ---- compute -----------------------------------------------------------------------------
            ["compute.title"] = "MOC OBLICZENIOWA",
            ["compute.service"] = "OBSŁUGA",
            ["compute.server_usage"] = "Obciążenie serwerów",
            ["compute.response_time"] = "Czas odpowiedzi: {0} ms",
            ["compute.right_now"] = "Teraz",
            ["compute.online_users"] = "Użytkownicy online",

            // ---- research ----------------------------------------------------------------------------
            ["research.title"] = "BADANIA",
            ["research.funding"] = "FINANSOWANIE",
            ["research.fixed_budget"] = "STAŁY BUDŻET",
            ["research.revenue_share"] = "UDZIAŁ W PRZYCHODZIE",
            ["research.begin"] = "ROZPOCZNIJ",
            ["research.what_it_opens"] = "CO OTWIERA",
            ["research.needs_first"] = "WYMAGA WCZEŚNIEJ",
            ["research.fit"] = "DOPASOWANIE",
            ["research.points"] = "PUNKTY",
            ["research.cash"] = "GOTÓWKA",
            ["research.duration"] = "CZAS TRWANIA",
            ["research.waiting_compute"] = "Czeka na czas klastra. Zwolnij moc, a się dokończy.",

            // ---- the upgrade screen ------------------------------------------------------------------
            ["upgrade.title"] = "ULEPSZ MODEL",
            ["upgrade.upgrading"] = "ULEPSZANY",
            ["upgrade.what_it_does"] = "CO TO ZMIENI",
            ["upgrade.pick_hint"] = "Wybierz jedno lub więcej ulepszeń po lewej. Można je zlecić razem, a klaster podzieli czas między nie.",
            ["upgrade.capability"] = "ZDOLNOŚCI",
            ["upgrade.brand"] = "MARKA",
            ["upgrade.efficiency"] = "WYDAJNOŚĆ SERWOWANIA",
            ["upgrade.cost"] = "KOSZT",
            ["upgrade.calendar"] = "KALENDARZ",
            ["upgrade.time"] = "CZAS",
            ["upgrade.compute"] = "MOC",
            ["upgrade.start_one"] = "ZACZNIJ ULEPSZANIE",
            ["upgrade.start_many"] = "ZACZNIJ ULEPSZENIA: {0}",
            ["upgrade.plan_release"] = "ZAPLANUJ WYDANIE",
            ["upgrade.at_ceiling"] = "MAKSIMUM",
            ["upgrade.max"] = "MAX",
            ["upgrade.behind"] = "{0} W TYLE",
            ["upgrade.level"] = "POZIOM {0}",
            ["upgrade.from"] = "OD {0}",
            ["upgrade.level_market"] = "POZIOM {0}   ·   RYNEK {1}",
            ["upgrade.none_live"] = "Nic jeszcze nie działa. Wydaj model, a to się zapełni.",
            ["upgrade.version"] = "Wersja: {0}",
            ["upgrade.base"] = "Bazowa",

            // ---- the release planner -----------------------------------------------------------------
            ["release.version_name"] = "Nazwa wersji:",
            ["release.change_price"] = "ZMIEŃ CENĘ",
            ["release.change"] = "ZMIEŃ",
            ["release.apply"] = "ZATWIERDŹ",
            ["release.free_tokens"] = "DARMOWE TOKENY NA UŻYTKOWNIKA",
            ["release.per_month"] = "{0} / mies.",
            ["release.per_day"] = "{0}/dzień",
            ["release.what_ships"] = "CO WCHODZI W TĘ WERSJĘ",
            ["release.nothing_ships"] = "Tylko cena i limit. Bez prac po treningu.",
            ["release.ship"] = "WYDAJ TĘ WERSJĘ",
            ["release.in_use"] = "WERSJE W UŻYCIU",
            ["release.current"] = "aktualna",
            ["release.market_sees"] = "CO WIDZI RYNEK",
            ["release.effective_capability"] = "ZDOLNOŚCI EFEKTYWNE",
            ["release.best_version"] = "NAJLEPSZA WYDANA WERSJA",
            ["release.average_price"] = "ŚREDNIA PŁACONA CENA",
            ["release.count_one"] = "1 wersja w użyciu",
            ["release.count_many"] = "wersje w użyciu: {0}",
            ["release.older_holds"] = "Więcej osób siedzi na {0} niż na aktualnym wydaniu. Aktualizacja, której nikt nie polubił, nie zabiera publiczności.",

            // ---- the architecture screen -------------------------------------------------------------
            ["arch.title"] = "ARCHITEKTURA",
            ["arch.strap"] = "Rodzina, po której dziedziczy każdy późniejszy model. Budżet i kalendarz decydują, jak daleko sięgnie program. Skupienie decyduje, czy sięgnie gdziekolwiek.",
            ["arch.programme"] = "PROGRAM",
            ["arch.family_name"] = "NAZWA RODZINY",
            ["arch.slot"] = "SLOT",
            ["arch.build_on"] = "BUDUJ NA",
            ["arch.clean_sheet"] = "Od zera",
            ["arch.slot_empty"] = "{0} - pusty",
            ["arch.slot_overwrite"] = "{0} - nadpisz {1}",
            ["arch.iterate_hint"] = "Rozwijanie rodziny, którą już masz, kosztuje 40% mniej i trwa 40% krócej, a każda generacja sięga o połowę mniej niż poprzednia. Rodziny się wypłaszczają. Projekt od zera kosztuje pełną cenę i nie ma takiego sufitu.",
            ["arch.directions"] = "KIERUNKI BADAŃ",
            ["arch.directions_hint"] = "Wysiłek jest względny. Rozłożony równo na pięć kierunków daje piątą część głębi w każdym. To realny wybór i zwykle zły.",
            ["arch.investment"] = "INWESTYCJA",
            ["arch.budget"] = "BUDŻET BADAŃ",
            ["arch.length"] = "DŁUGOŚĆ PROGRAMU",
            ["arch.investment_hint"] = "Pieniądze i czas to średnia geometryczna, nie suma. Miliard w trzy miesiące to nie przełom, a trzy lata pracy dwóch osób też nie.",
            ["arch.outcome"] = "PRAWDOPODOBNY WYNIK",
            ["arch.research_power"] = "SIŁA BADAWCZA",
            ["arch.focus"] = "SKUPIENIE",
            ["arch.certainty"] = "PEWNOŚĆ",
            ["arch.would_produce"] = "CO BY DAŁO",
            ["arch.active_parameters"] = "AKTYWNE PARAMETRY",
            ["arch.quality_per_parameter"] = "JAKOŚĆ NA PARAMETR",
            ["arch.training_efficiency"] = "WYDAJNOŚĆ TRENINGU",
            ["arch.serving_multiplier"] = "MNOŻNIK SERWOWANIA",
            ["arch.capability_bonus"] = "BONUS ZDOLNOŚCI",
            ["arch.cash"] = "GOTÓWKA",
            ["arch.compute"] = "MOC",
            ["arch.saves"] = "OSZCZĘDZA",
            ["arch.commit"] = "ZATWIERDŹ PROGRAM",
            ["arch.families"] = "RODZINY WŁASNE",
            ["arch.no_families"] = "Brak własnych rodzin. Wszystko chodzi na opublikowanych technikach, czyli dokładnie na tym, na czym chodzi każdy rywal.",
            ["arch.abandon"] = "PORZUĆ TEN PROGRAM",
            ["arch.abandon_confirm"] = "POTWIERDŹ, NIC NIE WRACA",
            ["arch.day_of"] = "dzień {0} z {1}",
            ["arch.verdict.busy"] = "Program rodziny już trwa. Jeden na raz.",
            ["arch.verdict.coin_toss"] = "Wykonalne, ale niedofinansowane na tyle, że wynik to prawie rzut monetą. Więcej pieniędzy albo czasu zwęzi pasmo.",
            ["arch.verdict.spread"] = "Wykonalne, ale wysiłek jest rozłożony tak równo, że żaden kierunek nie sięga wystarczająco głęboko.",
            ["arch.verdict.good"] = "Wykonalne i wystarczająco skupione, żeby wylądować gdzieś użytecznie.",
            ["arch.beyond_ceiling"] = "{0} jest pchnięte dalej, niż firma potrafi. Badania otwierają resztę tego suwaka.",

            // ---- the paid desks ----------------------------------------------------------------------
            ["intel.title"] = "WYWIAD",
            ["intel.strap"] = "Co według działu analiz nadchodzi. Pewność to ich własna deklaracja i zawsze jest wyższa niż to, jak często mają rację.",
            ["intel.join"] = "DOŁĄCZ",
            ["intel.on_retainer"] = "NA ETACIE  ·  KLIKNIJ, BY ANULOWAĆ",
            ["intel.no_notes"] = "Jeszcze nic. Opłacony dział składa pierwszą notatkę w kilka tygodni.",

            // ---- banners -----------------------------------------------------------------------------
            ["banner.researching"] = "BADANIA",
            ["banner.research_waiting"] = "BADANIA CZEKAJĄ",
            ["banner.arranging"] = "ORGANIZACJA",
            ["banner.working_on_upgrade"] = "TRWA ULEPSZANIE",
            ["banner.days_left.one"] = "został 1 dzień",
            ["banner.days_left.many"] = "zostało dni: {0}",
            ["banner.finishing_today"] = "kończy się dziś",

            // ---- money and time ----------------------------------------------------------------------
            ["money.needs"] = "POTRZEBA {0}",
            ["money.a_day"] = "{0} dziennie",
            ["money.a_month"] = "{0} miesięcznie",
            ["time.months"] = "miesięcy: {0}",
            ["time.days"] = "dni: {0}",

            // ---- the (i) cards -----------------------------------------------------------------------
            ["tech.sparsity.title"] = "RZADKOŚĆ",
            ["tech.sparsity.what"] = "Gęsty model uruchamia każdy parametr dla każdego tokena. Rzadki kieruje token do kilku ekspertów, a resztę zostawia bezczynną, więc model może być ogromny w pamięci i mały w działaniu.",
            ["tech.sparsity.affects"] = "Zmienia udział aktywnych parametrów, który rynek czyta jako koszt serwowania, a planer jako moc na token. Nie sprawia, że model jest w czymkolwiek lepszy.",
            ["tech.sparsity.high"] = "Tani w serwowaniu przy rozmiarze, na który nikogo innego nie stać, i zapas na dalszy wzrost. Routing jest kruchy: jakość na parametr spada, a źle poprowadzony model jest gorszy niż gęsty o połowę mniejszy.",
            ["tech.sparsity.low"] = "Każdy parametr zarabia na siebie, a model zachowuje się przewidywalnie. Płacisz za całość przy każdym tokenie, zawsze, a wojnę cenową wygrywa ten, kto płaci mniej.",
            ["tech.throughput.title"] = "PRZEPUSTOWOŚĆ",
            ["tech.throughput.what"] = "Jaka część klastra liczy, a nie czeka. Nakładanie komunikacji na obliczenia, łączenie kerneli, utrzymywanie pełnego potoku.",
            ["tech.throughput.affects"] = "Dzieli kalendarz każdego treningu w rodzinie. Nic więcej. Nie podnosi sufitu i nie obniża rachunku.",
            ["tech.throughput.high"] = "Treningi kończą się szybciej, więc ta sama flota wypuszcza więcej modeli rocznie i da się jeszcze złapać okno premiery. Nie daje nic, jeśli firma nie jest ograniczona mocą.",
            ["tech.throughput.low"] = "Treningi trwają tyle, ile trwają. Wysiłek idzie w kierunki, które zmieniają czym model jest, a nie kiedy przyjdzie. To dobry wybór, dopóki front porusza się wolno.",
            ["tech.quality.title"] = "JAKOŚĆ NA PARAMETR",
            ["tech.quality.what"] = "Więcej zdolności z tych samych wag: lepsza inicjalizacja, lepsze funkcje celu, lepsza kolejność danych, lepsze wykorzystanie tokenów, za które już zapłaciłeś.",
            ["tech.quality.affects"] = "Podnosi sufit zdolności każdego modelu w rodzinie. To jedyny kierunek, który faktycznie poprawia same modele, i najwolniej się zwraca.",
            ["tech.quality.high"] = "Każdy model tej rodziny punktuje wyżej przy tej samej mocy, a to kumuluje się przez dekadę. Najdroższy kierunek i ostatni, który widać w księgach.",
            ["tech.quality.low"] = "Nic nie poprawia się samo i zostajesz tam, gdzie są opublikowane techniki. Słuszne, gdy firma przegrywa kosztem, a nie zdolnościami: lepszy model, na którego serwowanie nikogo nie stać, nie jest produktem.",
            ["tech.serving.title"] = "KOSZT SERWOWANIA",
            ["tech.serving.what"] = "Ile kosztuje token, gdy model już działa: kwantyzacja, cache, batchowanie, dekodowanie spekulacyjne. Inżynieria, o której nikt nie pisze publikacji.",
            ["tech.serving.affects"] = "Mnoży koszt inferencji na token dla każdego modelu w rodzinie. Niewidoczne do dnia, w którym rywal tnie cenę i musisz zdecydować, czy dasz radę pójść za nim.",
            ["tech.serving.high"] = "Marża na każdym tokenie na zawsze i możliwość przetrwania wojny cenowej, której nie zacząłeś. Nie zmienia niczego, co widzi klient, więc nigdy nie wygra ci premiery.",
            ["tech.serving.low"] = "Wysiłek idzie w coś widocznego. W porządku, dopóki popyt jest mały. Firma serwująca miliardy tokenów dziennie przy złym mnożniku traci na każdym z nich i nie wyreklamuje się z tego.",
            ["tech.reasoning.title"] = "ROZUMOWANIE",
            ["tech.reasoning.what"] = "Struktura, której model uczy się używać, zamiast skali, którą dostaje: łańcuchy myśli, nagrody za proces, przeszukiwanie w czasie inferencji.",
            ["tech.reasoning.affects"] = "Bezpośredni bonus do zdolności, który nie bierze się z parametrów ani tokenów. Jedyny kierunek poza krzywą skalowania, dlatego najdroższy za punkt.",
            ["tech.reasoning.high"] = "Zdolności, których konkurencja nie kupi większym klastrem, i jedyna dźwignia, gdy front jest ograniczony mocą. Kosztuje najwięcej za punkt ze wszystkiego tutaj.",
            ["tech.reasoning.low"] = "Skala i dane robią robotę. Taniej za punkt zdolności dziś i tak zostanie, dokładnie do momentu, gdy wszyscy mają modele tej samej wielkości i nikt nie odróżnia produktów.",
            ["tech.budget.title"] = "BUDŻET BADAŃ",
            ["tech.budget.what"] = "Ile firma daje na program. Płacone niezależnie od tego, czy się uda.",
            ["tech.budget.affects"] = "Łączy się z kalendarzem jako średnia geometryczna, a potem ustala szerokość pasma wyniku. Same pieniądze nie kupią przełomu ani czasu.",
            ["tech.budget.high"] = "Wąskie pasmo wokół dobrego wyniku: drogi program zwykle ląduje tam, gdzie ekran obiecał. To kapitał, który nie wraca, i znika niezależnie od tego, czy rodzina jest dobra.",
            ["tech.budget.low"] = "Tanio i jak los na loterii. Pasmo jest tak szerokie, że wynik może być lepszy od oczekiwanego albo prawie bezwartościowy. Młoda firma czasem musi tak zagrać.",
            ["tech.length.title"] = "DŁUGOŚĆ PROGRAMU",
            ["tech.length.what"] = "Jak długo trwają badania, zanim dostarczą rodzinę.",
            ["tech.length.affects"] = "Druga połowa średniej geometrycznej, też zwęża pasmo wyniku. Kalendarza nie da się wykupić.",
            ["tech.length.high"] = "Głębszy i pewniejszy wynik oraz zablokowany slot na rok albo dłużej, podczas gdy front idzie dalej. Jeden program rodziny na raz.",
            ["tech.length.low"] = "Rodzina w ręku miesiące wcześniej, przy wyniku, który może być czymkolwiek w paśmie. Wydanie czegoś przeciętnego w tym roku często bije wydanie czegoś dobrego za dwa lata.",

            // ---- model traits, which are what the upgrade screen upgrades ----------------------------
            ["trait.reasoning.name"] = "Rozumowanie",
            ["trait.reasoning.desc"] = "Problemy wieloetapowe. Najdroższe punkty na planszy i te, które kupujący zauważają najpierw.",
            ["trait.knowledge.name"] = "Wiedza",
            ["trait.knowledge.desc"] = "Szerokość pamięci. Tanie w zakupie i pierwsza rzecz, którą rywal wyrówna.",
            ["trait.coding.name"] = "Programowanie",
            ["trait.coding.desc"] = "Generowanie i naprawa kodu. Najlepiej płacący segment na całym rynku.",
            ["trait.multilingual.name"] = "Wielojęzyczność",
            ["trait.multilingual.desc"] = "Języki poza angielskim. Otwiera regiony, zamiast podnosić sufit.",
            ["trait.multimodal.name"] = "Multimodalność",
            ["trait.multimodal.desc"] = "Obraz, dźwięk i wideo na wejściu i wyjściu. Drogie w treningu, a od 2025 kupujący zakładają, że jest.",
            ["trait.context.name"] = "Długość kontekstu",
            ["trait.context.desc"] = "Ile model utrzyma naraz. Sprzedaje się korporacjom, kosztuje pamięć przy serwowaniu.",
            ["trait.safety.name"] = "Bezpieczeństwo",
            ["trait.safety.desc"] = "Odmowy w odpowiednich miejscach. Niewidoczne, gdy działa. Jedyna obrona przed incydentem.",
            ["trait.latency.name"] = "Szybkość",
            ["trait.latency.desc"] = "Czas do pierwszego tokena. Kupujący czują to, zanim przeczytają jakikolwiek benchmark.",
            ["trait.efficiency.name"] = "Optymalizacja",
            ["trait.efficiency.desc"] = "Koszt serwowanego tokena. Nie kupi nagłówków, a decyduje, czy firma przetrwa wojnę cenową.",
            ["trait.tooluse.name"] = "Użycie narzędzi",
            ["trait.tooluse.desc"] = "Wywoływanie rzeczy spoza modelu. Na tym stoi cały rynek agentów.",
            ["trait.ecosystem.name"] = "Ekosystem",
            ["trait.ecosystem.desc"] = "SDK, integracje, wszyscy budujący na tobie. Wolno rośnie i wolno się traci.",

            // ---- Emil, the phone and the tour --------------------------------------------------------
            ["guide.cousin.name"] = "Emil",
            ["guide.cousin.handle"] = "Emil ziomek",
            ["guide.cousin.relation"] = "Kuzyn :3",
            ["guide.welcome"] = "Witaj z powrotem!",
            ["guide.backlog.1"] = "Heej stary, masz już to za sobą? Papiery odebrane?",
            ["guide.backlog.2"] = "Oszczędzę ci tego, co przechodziłem sam... Pokażę ci, co i jak w tym biznesie",
            ["guide.live.1"] = "._.",
            ["guide.live.2"] = "Stary, koniec melanżu. Chyba że mnie zapraszasz!",
            ["guide.reply.accept"] = "Siema stary, dokładnie po wszystkim. Wprowadzisz mnie byle szybko?",
            ["guide.reply.decline"] = "Nie mogę tracić czasu ;) Widzimy się w rankingu.",
            ["guide.reply.declined"] = "Ohoho stary, mam nadzieję, że wiesz co robisz. PS. Jeden będzie rządzić.",
            ["guide.next"] = "Dalej",
            ["guide.show_me"] = "Pokaż mi",
            ["guide.skip"] = "Dalej sam",
            ["guide.task"] = "ZADANIE",
            ["guide.favour.spent"] = "{0} ruszyło, na koszt firmy kolegi. Ziomek Emila to załatwił, jakieś {1} dni.",
            ["guide.step.hud"] = "Dobra. To na dole to twoje centrum dowodzenia. Dosłownie wszystko, co ta firma robi, klika się stamtąd. Nie ucz się tego na pamięć, po prostu wiedz, że jak czegoś szukasz, to jest w tym pasku.",
            ["guide.step.burn"] = "Dobra... nie będę pieprzyć marketingowo. Powiem ci od razu o jednej rzeczy, na którą musisz uważać, żeby szybko nie zbankrutować.",
            ["guide.step.compute_open"] = "Klikaj w MOC. To tam się wynajmuje moc obliczeniową.",
            ["guide.step.compute_rent"] = "Nie masz własnych serwerów, więc jesteś skazany na wynajem. I uważaj, to hieny. Możesz łatwo przeoczyć, że nagle wydajesz miliony tygodniowo na nadwyżkę mocy. Po co? Żeby user miał 10 pingu zamiast 12? Ten, co ma 90, różnicy nie odczuje.",
            ["guide.step.compute_dial"] = "Ten zegar to twoje obłożenie. Jak siedzi nisko, płacisz za powietrze. Jak wbija się pod sufit, ludzie czekają i odchodzą. Trzymaj go w środku i sprawdzaj po każdej premierze.",
            ["guide.step.research_pitch"] = "Teraz to, czego nikt normalnie nie tłumaczy. Możesz mieć wszystkie pieniądze świata i dalej nic nie zbudujesz, bo jeszcze nie umiesz. Od tego są badania.",
            ["guide.step.research_open"] = "Klikaj w BADANIA. Chcę, żebyś to zobaczył, zanim cokolwiek wydasz.",
            ["guide.step.research_tree"] = "Całe drzewo widać od pierwszego dnia, łącznie z tym na końcu, czego nie ruszysz przez lata. Tak ma być. Masz widzieć, co nadchodzi, i to planować.",
            ["guide.step.research_cost"] = "Dwie waluty. Punkty, które zdobywasz tylko realnie coś budując, i trochę gotówki. Same pieniądze nic ci tu nie kupią. O to właśnie chodzi.",
            ["guide.step.research_gift"] = "I teraz najlepsze. Mam ziomka w jednym z labów, który jest mi winien przysługę. Wybierz cokolwiek z pierwszej ery i to na jego koszt. Serio, cokolwiek. Po prostu wybierz.",
            ["guide.step.research_gift_note"] = "Punkty i kasa, jedno i drugie opłacone. Nie kombinuj za długo. Precyzja to najtańsza rzecz na planszy i przyspiesza każdy trening, jaki zrobisz, jak chcesz szczerą podpowiedź.",
            ["guide.step.model_open"] = "Teraz najlepsze. Klikaj w MODEL.",
            ["guide.step.model_hub"] = "Tu masz zestawienie wszystkiego, co sprzedajesz, i ile ci to niesie. Dwoje drzwi: budujesz nowy model albo ulepszasz ten, co już jest. Ulepszenie jest tańsze i szybsze, ale sufitu nie podniesie.",
            ["guide.step.create_open"] = "Wchodzimy w NOWY MODEL. Pokażę ci, na czym to naprawdę stoi.",
            ["guide.step.create_scale"] = "SKALA to serce. Parametry i tokeny. Zapamiętaj jedno: jest optymalny stosunek tokenów do parametrów i gra ci go pokazuje. Jak przesadzisz w którąkolwiek stronę, palisz moc i dostajesz gorszy model za większe pieniądze.",
            ["guide.step.create_locked"] = "Widzisz te zaciemnione końcówki suwaków? To nie bug. Tyle, ile potrafisz nadzorować. Badania je otwierają i to jest właśnie powód, żeby je robić.",
            ["guide.step.create_precision"] = "Precyzja. Zaczynasz na FP64, bo nic innego jeszcze nie umiesz. To najwolniejsza opcja, jaka istnieje. Pierwszy węzeł w drzewie otwiera FP32, potem BF16, i to jest najtańsze przyspieszenie treningu, jakie kupisz.",
            ["guide.step.create_safety"] = "BEZPIECZEŃSTWO. Wiem, że kusi przeklikać. Nie rób tego. Regulator przychodzi po incydencie i patrzy, co miałeś włączone w dniu premiery. Tanio teraz albo bardzo drogo później.",
            ["guide.step.create_review"] = "PRZEGLĄD na końcu pokazuje ci, co z tego wyjdzie, ile to potrwa i ile zapłacisz. Ten czas to prawda. Trening trwa dokładnie tyle, ile tu pisze.",
            ["guide.step.arch_pitch"] = "Jeszcze jedno, zanim pójdziesz budować. Jest poziom wyżej niż model i większość ludzi trafia na niego stanowczo za późno.",
            ["guide.step.arch_open"] = "ARCHITEKTURA. Dawaj.",
            ["guide.step.arch_what"] = "Własna rodzina to coś, po czym dziedziczy każdy model, jaki potem zbudujesz. Zrób to raz dobrze, a każdy model przez najbliższe pięć lat jest tańszy, lepszy albo jedno i drugie. Zrób źle i zapłaciłeś za rok niczego.",
            ["guide.step.arch_locked"] = "Z suwakami tu ta sama historia. Większość każdego jest zamknięta, a badania to otwierają. I nie rozkładaj wysiłku na wszystkie pięć. Piąta część głębi w każdym kierunku to realny wybór i prawie zawsze zły.",
            ["guide.step.arch_info"] = "Widzisz te małe kółka (i)? Kliknij któreś. Mówi ci, co ta rzecz naprawdę robi i co dostajesz na obu końcach suwaka. Chciałbym, żeby ktoś mi to kiedyś spisał.",
            ["guide.step.after_release"] = "Dobra, powiedzmy, że wydałeś. I tu większość ludzi siada i się rozluźnia, a to najdroższy błąd w całym tym biznesie.",
            ["guide.step.upgrade_open"] = "Klikaj w ULEPSZENIA. To ekran, który trzyma cię przy życiu między modelami.",
            ["guide.step.upgrade_tiles"] = "Wszystko, co możesz poprawić w czymś, co już się sprzedaje. Bez nowego treningu, bez pół roku. Popatrz na czerwone plakietki: tyle jesteś już w tyle za rynkiem w każdej z nich. Ta liczba rośnie sama, kiedy nic nie robisz.",
            ["guide.step.upgrade_release"] = "A jak już wybierzesz, to nie zlecasz tego ot tak. Planujesz wersję: nadajesz nazwę, ustawiasz cenę, decydujesz o darmowym limicie. To ten zielony przycisk.",
            ["guide.step.versions"] = "I jeszcze jedno, o czym nikt nie uprzedza. Wydanie aktualizacji nie przenosi na nią wszystkich. Jak się ludziom nie spodoba, zostają na starej, i zobaczysz to na tej liście. Słabe wydanie potrafi siedzieć na dziewięciu procentach, a to sprzed pół roku trzymać siedemdziesiąt.",
            ["guide.step.dream"] = "Hah. A jak ci wyjdzie i się po drodze nie wypalisz, będziesz mieć śliczne biura, apartamenty, serwerownie, samochody do wyboru ;)",
            ["guide.step.offices_open"] = "Nawet nie żartuję. Patrz, tu się przeprowadzasz.",
            ["guide.step.offices"] = "Wynajem albo zakup. Wynajem to rachunek, od którego możesz odejść, jak się sypie. Zakup to kapitał, który nie wraca, i kończy czynsz na zawsze. Jakieś dziesięć lat czynszu, więc jak będziesz tu za dekadę, kupuj.",
            ["guide.step.offices_desks"] = "A biurka to sufit zatrudnienia. Nie pieniądze, biurka. Firma, którą stać na dwadzieścia osób, a ma dziesięć krzeseł, zatrudnia dziesięć osób.",
            ["guide.step.wrap"] = "I tyle ode mnie. Zrób pierwszy model, wypuść go i patrz na kasę. Jak coś, pisz. Telefon masz w rogu.",
            ["guide.task.first_research"] = "Zacznij pierwsze badanie",
            ["guide.task.first_model"] = "Stwórz pierwszy model",
            ["guide.task.first_release"] = "Wydaj pierwszy model",
            ["guide.task.first_upgrade"] = "Ulepsz model, który sprzedajesz",
            ["guide.task.double_cash"] = "Podwój budżet firmy",

        };

        /// <summary>
        /// Keys English has and a language does not, or has left empty.
        ///
        /// The translator's worklist. A test asserts this is empty for a language before it can be
        /// offered in the menu — a half-translated language shipping quietly is how a player ends up
        /// reading two of them at once.
        /// </summary>
        public static List<string> Untranslated(Language language)
        {
            var table = TableFor(language);
            var gaps = new List<string>();

            foreach (var pair in English)
            {
                if (!table.TryGetValue(pair.Key, out var text) || string.IsNullOrEmpty(text))
                {
                    gaps.Add(pair.Key);
                }
            }

            gaps.Sort();
            return gaps;
        }
    }
}
