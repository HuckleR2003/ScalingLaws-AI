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
            // ---- counted nouns. Three forms each, always. ------------------------------------
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

            // ---- the bottom bar ---------------------------------------------------------------
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

            // ---- words that appear on a dozen screens ------------------------------------------
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

            // ---- MODEL --------------------------------------------------------------------------
            ["model.title"] = "MODEL",
            ["model.new"] = "NEW MODEL",
            ["model.new.note"] =
                "Design a training run from nothing. Months, and the biggest bill the company pays.",
            ["model.upgrade"] = "UPGRADE",
            ["model.upgrade.note"] =
                "Improve something already on sale, without training it again.",
            ["model.upgrade.none"] =
                "Nothing on sale yet. Build one first and this is how you keep it current.",
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

            // ---- TEAM ---------------------------------------------------------------------------
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

            // ---- hiring ---------------------------------------------------------------------------
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
            ["hire.mood.delighted"] = "They would take this happily.",
            ["hire.mood.pleased"] = "This is enough. They would sign.",
            ["hire.mood.neutral"] = "Borderline. It could go either way.",
            ["hire.mood.cool"] = "Short. They will push back rather than leave.",
            ["hire.mood.insulted"] = "Insulting. Send this and they are gone.",

            // ---- COMPUTE ----------------------------------------------------------------------------
            ["compute.title"] = "COMPUTE",
            ["compute.service"] = "SERVICE",
            ["compute.server_usage"] = "Server Usage",
            ["compute.response_time"] = "Response Time: {0}ms",
            ["compute.right_now"] = "Right now",
            ["compute.online_users"] = "Online users",

            // ---- RESEARCH -----------------------------------------------------------------------------
            ["research.title"] = "RESEARCH",
            ["research.funding"] = "FUNDING",
            ["research.fixed_budget"] = "A FIXED BUDGET",
            ["research.revenue_share"] = "A SHARE OF REVENUE",
            ["research.begin"] = "BEGIN",
            ["research.what_it_opens"] = "WHAT IT OPENS",
            ["research.needs_first"] = "NEEDS FIRST",
            ["research.fit"] = "FIT",

            // ---- UPGRADE ---------------------------------------------------------------------------------
            ["upgrade.title"] = "UPGRADE MODEL",
            ["upgrade.what_it_does"] = "WHAT IT WOULD DO",
            ["upgrade.pick_hint"] =
                "Pick one or more upgrades on the left. They can be commissioned together, and the "
                + "cluster splits its time between them.",
            ["upgrade.capability"] = "CAPABILITY",
            ["upgrade.brand"] = "BRAND",
            ["upgrade.efficiency"] = "SERVING EFFICIENCY",
            ["upgrade.cost"] = "COST",
            ["upgrade.calendar"] = "CALENDAR",
            ["upgrade.compute"] = "COMPUTE",
            ["upgrade.start_one"] = "START UPGRADE",
            ["upgrade.start_many"] = "START {0} UPGRADES",
            ["upgrade.at_ceiling"] = "AT THE CEILING",
            ["upgrade.behind"] = "{0} BEHIND",
            ["upgrade.level_market"] = "LEVEL {0}   ·   MARKET {1}",

            // ---- the corner banners --------------------------------------------------------------------------
            ["banner.researching"] = "RESEARCHING",
            ["banner.research_waiting"] = "RESEARCH WAITING",
            ["banner.arranging"] = "ARRANGING",
            ["banner.working_on_upgrade"] = "WORKING ON UPGRADE",
            ["banner.days_left.one"] = "1 day left",
            ["banner.days_left.many"] = "{0} days left",
            ["banner.finishing_today"] = "finishing today",

            // ---- the guide ------------------------------------------------------------------------------------
            ["guide.task"] = "TASK",
            ["guide.show_me"] = "Show me",
            ["guide.task.first_model"] = "Create your first model",
            ["guide.task.first_release"] = "Release your first model",
            ["guide.task.double_cash"] = "Double the company budget",

            // ---- money and time, as the interface says them -------------------------------------------------------
            ["money.needs"] = "NEEDS {0}",
            ["money.a_day"] = "{0} a day",
            ["money.a_month"] = "{0} a month",
            ["time.months"] = "{0} months",
            ["time.days"] = "{0} days"
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
            // ---- counted nouns -------------------------------------------------------------------
            // The first three are filled in as a worked example of all three forms:
            //   1 biurko  ·  2 biurka  ·  5 biurek
            ["noun.desk.one"] = "biurko",
            ["noun.desk.few"] = "biurka",
            ["noun.desk.many"] = "biurek",

            ["noun.day.one"] = "dzień",
            ["noun.day.few"] = "dni",
            ["noun.day.many"] = "dni",

            ["noun.month.one"] = "miesiąc",
            ["noun.month.few"] = "miesiące",
            ["noun.month.many"] = "miesięcy",

            ["noun.model.one"] = "",
            ["noun.model.few"] = "",
            ["noun.model.many"] = "",

            ["noun.person.one"] = "",
            ["noun.person.few"] = "",
            ["noun.person.many"] = "",

            ["noun.offer.one"] = "",
            ["noun.offer.few"] = "",
            ["noun.offer.many"] = "",

            ["noun.upgrade.one"] = "",
            ["noun.upgrade.few"] = "",
            ["noun.upgrade.many"] = "",

            // ---- the bottom bar -------------------------------------------------------------------
            ["hud.site"] = "",
            ["hud.model"] = "",
            ["hud.research"] = "",
            ["hud.architecture"] = "",
            ["hud.upgrade"] = "",
            ["hud.team"] = "",
            ["hud.compute"] = "",
            ["hud.business"] = "",
            ["hud.release"] = "",
            ["hud.capital"] = "",
            ["hud.ranking"] = "",
            ["hud.intel"] = "",
            ["hud.marketing"] = "",
            ["hud.news"] = "",
            ["hud.mail"] = "",

            // ---- common ----------------------------------------------------------------------------
            ["common.close"] = "",
            ["common.cancel"] = "",
            ["common.confirm"] = "",
            ["common.back"] = "",
            ["common.next"] = "",
            ["common.skip"] = "",
            ["common.done"] = "",
            ["common.buy"] = "",
            ["common.sell"] = "",
            ["common.start"] = "",
            ["common.none"] = "",
            ["common.locked"] = "",
            ["common.in_progress"] = "",
            ["common.a_day"] = "",
            ["common.a_month"] = "",
            ["common.a_year"] = "",
            ["common.per_hour"] = "",

            // ---- MODEL -------------------------------------------------------------------------------
            ["model.title"] = "",
            ["model.new"] = "",
            ["model.new.note"] = "",
            ["model.upgrade"] = "",
            ["model.upgrade.note"] = "",
            ["model.upgrade.none"] = "",
            ["model.service"] = "",
            ["model.service.none"] = "",
            ["model.month"] = "",
            ["model.income"] = "",
            ["model.costs"] = "",
            ["model.from_subscriptions"] = "",
            ["model.subscribers"] = "",
            ["model.on_sale"] = "",
            ["model.on_sale.none"] = "",
            ["model.manage"] = "",
            ["model.column.name"] = "",
            ["model.column.users"] = "",
            ["model.column.subs"] = "",
            ["model.column.income"] = "",

            // ---- TEAM ----------------------------------------------------------------------------------
            ["team.title"] = "",
            ["team.positions"] = "",
            ["team.payroll"] = "",
            ["team.worth"] = "",
            ["team.where"] = "",
            ["team.hire_now"] = "",
            ["team.hire_remote"] = "",
            ["team.let_go"] = "",
            ["team.details"] = "",
            ["team.workplaces_available"] = "",

            // ---- hiring ----------------------------------------------------------------------------------
            ["hire.where_looking"] = "",
            ["hire.agency"] = "",
            ["hire.specialist"] = "",
            ["hire.get_in_touch"] = "",
            ["hire.your_offer"] = "",
            ["hire.hourly_wage"] = "",
            ["hire.signing_bonus"] = "",
            ["hire.send_offer"] = "",
            ["hire.decline"] = "",
            ["hire.negotiate"] = "",
            ["hire.mood.delighted"] = "",
            ["hire.mood.pleased"] = "",
            ["hire.mood.neutral"] = "",
            ["hire.mood.cool"] = "",
            ["hire.mood.insulted"] = "",

            // ---- COMPUTE -----------------------------------------------------------------------------------
            ["compute.title"] = "",
            ["compute.service"] = "",
            ["compute.server_usage"] = "",
            ["compute.response_time"] = "",
            ["compute.right_now"] = "",
            ["compute.online_users"] = "",

            // ---- RESEARCH ------------------------------------------------------------------------------------
            ["research.title"] = "",
            ["research.funding"] = "",
            ["research.fixed_budget"] = "",
            ["research.revenue_share"] = "",
            ["research.begin"] = "",
            ["research.what_it_opens"] = "",
            ["research.needs_first"] = "",
            ["research.fit"] = "",

            // ---- UPGRADE ---------------------------------------------------------------------------------------
            ["upgrade.title"] = "",
            ["upgrade.what_it_does"] = "",
            ["upgrade.pick_hint"] = "",
            ["upgrade.capability"] = "",
            ["upgrade.brand"] = "",
            ["upgrade.efficiency"] = "",
            ["upgrade.cost"] = "",
            ["upgrade.calendar"] = "",
            ["upgrade.compute"] = "",
            ["upgrade.start_one"] = "",
            ["upgrade.start_many"] = "",
            ["upgrade.at_ceiling"] = "",
            ["upgrade.behind"] = "",
            ["upgrade.level_market"] = "",

            // ---- banners -----------------------------------------------------------------------------------------
            ["banner.researching"] = "",
            ["banner.research_waiting"] = "",
            ["banner.arranging"] = "",
            ["banner.working_on_upgrade"] = "",
            ["banner.days_left.one"] = "",
            ["banner.days_left.many"] = "",
            ["banner.finishing_today"] = "",

            // ---- the guide -----------------------------------------------------------------------------------------
            ["guide.task"] = "ZADANIE",
            ["guide.show_me"] = "Pokaż mi",
            ["guide.task.first_model"] = "Stwórz pierwszy model",
            ["guide.task.first_release"] = "Wydaj pierwszy model",
            ["guide.task.double_cash"] = "Podwój budżet firmy",

            // ---- money and time ------------------------------------------------------------------------------------
            ["money.needs"] = "",
            ["money.a_day"] = "",
            ["money.a_month"] = "",
            ["time.months"] = "",
            ["time.days"] = ""
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
