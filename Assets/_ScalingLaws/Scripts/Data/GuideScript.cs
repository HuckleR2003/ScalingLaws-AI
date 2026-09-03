using System.Collections.Generic;

namespace ScalingLaws.Data
{
    /// <summary>Which screen a guide step wants the player looking at.</summary>
    public enum GuideTarget
    {
        /// <summary>Nowhere in particular. The step is about the interface itself.</summary>
        None = 0,

        Site = 1,
        Compute = 2,
        Model = 3,
        Create = 4,
        Research = 5,
        Team = 6,
        Release = 7,

        /// <summary>The upgrade wall. Where the tour goes after a release.</summary>
        Upgrade = 8,

        /// <summary>The premises chooser, for the last act.</summary>
        Offices = 9,

        /// <summary>The house family screen.</summary>
        Architecture = 10,

        /// <summary>The bank. Where the money comes from when the company has not earned it yet.</summary>
        Funding = 11,

        /// <summary>The board, for the one line where he points at his own company on it.</summary>
        Ranking = 12,

        /// <summary>The basement, for the step where he hands over the keys to it.</summary>
        Room = 13
    }

    /// <summary>
    /// One thing Emil says, and what he wants the player to do about it.
    ///
    /// **A step is data, not a method.** The whole tutorial is a list of these, which means the
    /// order can be changed without touching the presentation, a step can be skipped without a
    /// branch, and — the reason that matters most here — every line of it is in one file that a
    /// translator can be handed.
    /// </summary>
    public sealed class GuideStep
    {
        public GuideStep(string id, string line, GuideTarget target = GuideTarget.None,
            string highlight = null, string prompt = null, bool waitForClick = false,
            int creatorStage = -1)
        {
            Id = id;
            lineKey = line;
            Target = target;
            Highlight = highlight;
            promptKey = prompt;
            WaitForClick = waitForClick;
            CreatorStage = creatorStage;
        }

        public string Id { get; }

        /// <summary>What he says. Kept conversational: he is a cousin, not a manual.</summary>
        /// <summary>
        /// What he says, resolved when it is read.
        ///
        /// The constructor takes a phrase-book key. Resolving here rather than at construction is
        /// what lets the language change mid-conversation without the rest of the tour staying in
        /// the language it started in.
        /// </summary>
        public string Line => Loc.T(lineKey);

        private readonly string lineKey;

        /// <summary>The screen this step is about, or None.</summary>
        public GuideTarget Target { get; }

        /// <summary>
        /// A USS class the step lights up while it is showing.
        ///
        /// Naming the class rather than the element, because the guide must not know what the
        /// screen is built out of. If a screen is rebuilt with different elements under the same
        /// class the tutorial keeps working.
        /// </summary>
        public string Highlight { get; }

        /// <summary>The button caption, or null for "Next".</summary>
        /// <summary>What the button says. A key, same as the line.</summary>
        public string Prompt => string.IsNullOrEmpty(promptKey) ? null : Loc.T(promptKey);

        private readonly string promptKey;

        /// <summary>
        /// True when the step waits for the player to actually open the target screen.
        ///
        /// **This is the difference between a tutorial and a slideshow.** A step that advances on
        /// its own teaches nothing; one that waits until the player has pressed the thing being
        /// described means they have done it once before it matters.
        /// </summary>
        public bool WaitForClick { get; }

        /// <summary>
        /// Which page of the model creator this step is talking about, or -1 for none.
        ///
        /// **The tour got lost here in the first playtest and this is why.** Opening the creator put
        /// the player on whatever page they had left it on, while Emil went on describing the scale
        /// belt, then precision, then safety. Nothing was highlighted because none of it was on
        /// screen, and there was no way for the player to know which of eight pages he meant.
        ///
        /// So the step names the page and the shell puts the creator on it. That is the difference
        /// between a guide and a voice-over.
        /// </summary>
        public int CreatorStage { get; }
    }

    /// <summary>
    /// Emil, and everything he says.
    ///
    /// **All the words in one place, deliberately.** The author asked what it would take to add
    /// Polish, and this is most of the answer: a screen full of string literals is a screen nobody
    /// can translate, and a list like this is one a translator can be handed as a file. Every new
    /// piece of story the phone carries should land here rather than in a panel.
    /// </summary>
    public static class GuideScript
    {
        /// <summary>The contact, as the phone shows him.</summary>
        public static string CousinName => Loc.T("guide.cousin.name");

        public static string CousinHandle => Loc.T("guide.cousin.handle");

        public static string CousinRelation => Loc.T("guide.cousin.relation");

        /// <summary>The chat app on his phone. A name, not a phrase, so it is not translated.</summary>
        public const string AppName = "dIn";

        public static string WelcomeLine => Loc.T("guide.welcome");

        public static IReadOnlyList<string> AppMenu { get; } = new[]
        {
            "Contact List",
            "Messages",
            "ToDo list",
            "Achievements"
        };

        public const int AutoSelectedMenuItem = 1;

        /// <summary>
        /// What is already on the screen when the phone is opened.
        ///
        /// Two messages from earlier, so the conversation is one the player is joining rather than
        /// one that starts because they looked at it.
        /// </summary>
        public static IReadOnlyList<string> Backlog =>
            new[] { Loc.T("guide.backlog.1"), Loc.T("guide.backlog.2") };

        /// <summary>
        /// What is on screen when he calls **back**, after the player stepped out.
        ///
        /// A different opening, because the first one introduces him and this one does not need to:
        /// repeating the welcome would say the tour is starting over, which is exactly the thing
        /// that is not happening.
        /// </summary>
        public static IReadOnlyList<string> ReturnBacklog =>
            new[] { Loc.T("guide.return.backlog.1") };

        /// <summary>
        /// What he types while the player is watching, with the pause before each.
        ///
        /// **The first pause is long on purpose.** The phone lands, the screen wakes, the app opens
        /// — and if he starts typing under all that, the player is reading a message while the
        /// animation that introduces the messenger is still running.
        /// </summary>
        public static IReadOnlyList<(float DelaySeconds, float TypingSeconds, string Text)> Live =>
            new[]
            {
                (5f, 2f, Loc.T("guide.live.1")),
                (0f, 2f, Loc.T("guide.live.2"))
            };

        /// <summary>The call back, which is shorter because he is picking something up.</summary>
        public static IReadOnlyList<(float DelaySeconds, float TypingSeconds, string Text)> ReturnLive =>
            new[]
            {
                (3f, 1.6f, Loc.T("guide.return.live.1")),
                (0f, 1.6f, Loc.T("guide.return.live.2"))
            };

        /// <summary>Pick the tour back up.</summary>
        public static string ReturnAccept => Loc.T("guide.return.accept");

        /// <summary>Take the tutorial.</summary>
        public static string ReplyAccept => Loc.T("guide.reply.accept");

        /// <summary>Skip it.</summary>
        public static string ReplyDecline => Loc.T("guide.reply.decline");

        /// <summary>What he says when the player walks off. He is not offended, exactly.</summary>
        public static string DeclineReply => Loc.T("guide.reply.declined");

        /// <summary>
        /// The step at which the favour is handed over.
        ///
        /// Named rather than counted, so inserting a step above it cannot quietly move the gift to
        /// a different part of the conversation.
        /// </summary>
        /// <summary>
        /// The step where he hands over the basement.
        ///
        /// Named rather than counted, same as the research favour: inserting a line above it must
        /// not quietly move a gift to a different part of the conversation.
        /// </summary>
        public const string BasementStepId = "gift_racks";

        public const string GiftStepId = "research_gift";

        /// <summary>
        /// The one step that carries an offer: he sets the five direction sliders.
        ///
        /// Named here for the same reason the two gifts are, and because the shell has to recognise
        /// it without a second copy of the id written into `GameShell`.
        /// </summary>
        public const string ArchitectureOfferStepId = "arch_what";

        /// <summary>Where a named step sits in the tour, or -1 when there is no such step.</summary>
        public static int IndexOf(string stepId)
        {
            for (var index = 0; index < Steps.Count; index++)
            {
                if (Steps[index].Id == stepId)
                {
                    return index;
                }
            }

            return -1;
        }

        /// <summary>
        /// The tour, in six acts.
        ///
        /// **Written the way somebody who has done it would explain it.** The compute warning comes
        /// second because it is the thing that kills new companies, not because it is the second
        /// most interesting screen. Everything he says about the economy is true of this game's
        /// economy: a tutorial that teaches a rule the simulation does not have is worse than no
        /// tutorial at all.
        ///
        /// **A step is data, not a method.** Every line is a phrase-book key, so the whole tour can
        /// be handed to a translator without anybody opening a C# file, and the order can be changed
        /// without touching the presentation.
        ///
        /// The player can leave at any step. That is not a failure state and he does not sulk about
        /// it: the three opening tasks stay in the corner either way, because they are the opening
        /// of the game rather than the end of the lesson.
        /// </summary>
        public static IReadOnlyList<GuideStep> Steps { get; } = new List<GuideStep>
        {
            // ---- the floor, and the thing that bankrupts people ----------------------------
            new("hud", "guide.step.hud", GuideTarget.None, "hud-slot"),
            new("burn", "guide.step.burn"),
            new("compute_open", "guide.step.compute_open",
                GuideTarget.Compute, null, "guide.show_me", true),
            new("compute_rent", "guide.step.compute_rent", GuideTarget.Compute, "fleet-panel"),
            new("compute_dial", "guide.step.compute_dial", GuideTarget.Compute, "service__dial"),

            // **A number, and a reason to remember it.** The rent slider is the one control in the
            // game that bills every day whether or not anything is training, and it has cost real
            // campaigns real money. A figure is easier to hold onto than a warning, so he gives one.
            //
            // The tease is the same promise "I'll come back later" already makes, moved to the front
            // of the tour where it can do some work: it is the reason to sit through the rest of it,
            // and it names what the cap is temporary *for*.
            new("compute_cap", "guide.step.compute_cap", GuideTarget.Compute, "fleet-panel"),

            // ---- where the money comes from before the company earns any --------------------
            //
            // Directly after the burn, because that is the question the burn raises and leaving it
            // unanswered for six acts is what makes a new player think the opening is unwinnable.
            new("bank_pitch", "guide.step.bank_pitch"),
            new("bank_open", "guide.step.bank_open",
                GuideTarget.Funding, null, "guide.show_me", true),
            new("bank_tiles", "guide.step.bank_tiles", GuideTarget.Funding, "ltile"),
            new("bank_cost", "guide.step.bank_cost", GuideTarget.Funding, "loanbill"),
            new("bank_state", "guide.step.bank_state", GuideTarget.Funding, "ltile--state"),

            // ---- research, and the first one is on him ------------------------------------
            new("research_pitch", "guide.step.research_pitch"),
            new("research_open", "guide.step.research_open",
                GuideTarget.Research, null, "guide.show_me", true),
            new("research_tree", "guide.step.research_tree", GuideTarget.Research, "tree-node"),
            new("research_cost", "guide.step.research_cost", GuideTarget.Research, "tree-node"),
            new("research_groups", "guide.step.research_groups", GuideTarget.Research, "era"),
            new("launch_trap", "guide.step.launch_trap", GuideTarget.Research),
            new(GiftStepId, "guide.step.research_gift", GuideTarget.Research, "tree-node"),
            new("research_gift_note", "guide.step.research_gift_note", GuideTarget.Research),

            // ---- the level above the model ------------------------------------------------
            new("arch_pitch", "guide.step.arch_pitch"),
            new("arch_open", "guide.step.arch_open",
                GuideTarget.Architecture, null, "guide.show_me", true),
            new("arch_what", "guide.step.arch_what", GuideTarget.Architecture, "arx__card"),
            new("arch_locked", "guide.step.arch_locked", GuideTarget.Architecture, "dlock"),
            new("arch_info", "guide.step.arch_info", GuideTarget.Architecture, "infodot"),

            // ---- the model ---------------------------------------------------------------
            new("model_open", "guide.step.model_open",
                GuideTarget.Model, null, "guide.show_me", true),
            new("model_hub", "guide.step.model_hub", GuideTarget.Model, "door"),
            // Page 0, so opening the creator lands on branding rather than on wherever the player
            // last left it. A tour that opens a door onto the middle of a form is not a tour.
            new("create_open", "guide.step.create_open",
                GuideTarget.Create, null, "guide.show_me", true, 0),
            new("create_brand", "guide.step.create_brand", GuideTarget.Create, "wb", null, false, 0),
            new("create_foundation", "guide.step.create_foundation", GuideTarget.Create,
                "type-tile", null, false, 1),
            new("create_scale", "guide.step.create_scale", GuideTarget.Create, "scale-half",
                null, false, 2),
            new("create_locked", "guide.step.create_locked", GuideTarget.Create, "scale-lock",
                null, false, 2),

            // **On page 2, because that is where the control is.** It was on page 4 with a
            // highlight class that matches the free-tier pricing pill, so the tour walked forward a
            // page, described something two pages behind it, and rang the wrong thing.
            new("create_precision", "guide.step.create_precision", GuideTarget.Create, "scale-half",
                null, false, 2),

            // Nothing to say about the data page and the compute page beyond what is on them, so he
            // says he is waiting rather than filling the silence. Except for the one control on the
            // compute page that has cost real players real money.
            new("create_data", "guide.step.create_data", GuideTarget.Create, null, null, false, 3),
            new("create_spend", "guide.step.create_spend", GuideTarget.Create, "stage-slider",
                null, false, 4),
            new("create_waiting", "guide.step.create_waiting", GuideTarget.Create, null,
                null, false, 4),

            new("create_safety", "guide.step.create_safety", GuideTarget.Create, "effort-chip",
                null, false, 5),
            new("create_fine", "guide.step.create_fine", GuideTarget.Create, "effort-chip",
                null, false, 5),
            new("create_review", "guide.step.create_review", GuideTarget.Create, "verdict",
                null, false, 6),
            new("create_start", "guide.step.create_start", GuideTarget.Create, "verdict",
                null, false, 6),

            // **The last page, walked to rather than mentioned.** The tour explained seven stages
            // and stopped, so a player who followed it exactly never opened AFTER TRAINING and did
            // not know a finished run has to be put on sale from there. Asked for by name after a
            // playtest. Forward only: the tour has been caught once walking back two pages without
            // saying why, and it read as the tutorial losing its place.
            new("create_after", "guide.step.create_after", GuideTarget.Create, null,
                null, false, 7),

            // **The step the playtest asked for by name.** The tour finished the creator and never
            // said where a finished model is put on sale, so a player who followed it exactly ended
            // up with a trained model on a shelf and no idea which tab shipped it.
            new("create_publish", "guide.step.create_publish", GuideTarget.Release,
                null, "guide.show_me", true),
            new("publish_how", "guide.step.publish_how", GuideTarget.Release, "relrow"),

            // ---- what you do the day after a release ---------------------------------------
            new("after_release", "guide.step.after_release"),
            new("upgrade_open", "guide.step.upgrade_open",
                GuideTarget.Upgrade, null, "guide.show_me", true),
            new("upgrade_tiles", "guide.step.upgrade_tiles", GuideTarget.Upgrade, "utile__badge"),
            new("upgrade_release", "guide.step.upgrade_release", GuideTarget.Upgrade, "udet__go"),
            new("versions", "guide.step.versions", GuideTarget.Upgrade, "udet__go"),

            // ---- and the reason anybody does any of it --------------------------------------
            new("dream", "guide.step.dream"),
            new("offices_open", "guide.step.offices_open",
                GuideTarget.Offices, null, "guide.show_me", true),
            new("offices", "guide.step.offices", GuideTarget.Offices, "office-row__move"),
            new("offices_desks", "guide.step.offices_desks", GuideTarget.Offices, "office-figure"),

            // He names his own company on the way out, now that it is on the board. A permanent
            // character with no place in the world is a voice from nowhere.
            new("emil_company", "guide.step.emil_company", GuideTarget.Ranking, "rank-row"),

            // ---- and the thing at the end -----------------------------------------------------
            //
            // The reward for finishing, and the reason "I'll come back later" says there is one. It
            // is a real asset rather than a line of dialogue: four cabinets in a basement, which is
            // the first compute the company physically owns.
            new("gift_tease", "guide.step.gift_tease"),
            new("gift_basement", "guide.step.gift_basement"),
            new(BasementStepId, "guide.step.gift_racks", GuideTarget.Room, null,
                "guide.gift.accept", true),

            new("wrap", "guide.step.wrap")
        };

        /// <summary>
        /// The tasks in the corner, in order.
        ///
        /// The same list whether the tutorial was taken or skipped, because they are the opening of
        /// the game rather than the end of the lesson: learn something, build something, sell it,
        /// keep it current, and prove the company can grow the money it started with.
        /// </summary>
        public static IReadOnlyList<(string Id, string Key)> Tasks { get; } = new[]
        {
            ("first_research", "guide.task.first_research"),
            ("first_model", "guide.task.first_model"),
            ("first_release", "guide.task.first_release"),
            ("first_upgrade", "guide.task.first_upgrade"),
            ("double_cash", "guide.task.double_cash")
        };

    }
}
