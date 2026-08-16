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
        Release = 7
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
            string highlight = null, string prompt = null, bool waitForClick = false)
        {
            Id = id;
            Line = line;
            Target = target;
            Highlight = highlight;
            Prompt = prompt;
            WaitForClick = waitForClick;
        }

        public string Id { get; }

        /// <summary>What he says. Kept conversational: he is a cousin, not a manual.</summary>
        public string Line { get; }

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
        public string Prompt { get; }

        /// <summary>
        /// True when the step waits for the player to actually open the target screen.
        ///
        /// **This is the difference between a tutorial and a slideshow.** A step that advances on
        /// its own teaches nothing; one that waits until the player has pressed the thing being
        /// described means they have done it once before it matters.
        /// </summary>
        public bool WaitForClick { get; }
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
        public const string CousinName = "Emil";

        public const string CousinHandle = "Emil bro";

        public const string CousinRelation = "Cousin :3";

        /// <summary>What the app is called. A blue square with three letters on it.</summary>
        public const string AppName = "dIn";

        public const string WelcomeLine = "Welcome back!";

        /// <summary>The four things the app can do. Only one of them is built.</summary>
        public static IReadOnlyList<string> AppMenu { get; } = new[]
        {
            "Contact List", "Messages", "ToDo list", "Achievements"
        };

        /// <summary>The one that lights up on its own, a second in.</summary>
        public const int AutoSelectedMenuItem = 1;

        /// <summary>
        /// What is already on the screen when the phone is opened.
        ///
        /// Two messages from earlier, so the conversation is one the player is joining rather than
        /// one that starts because they looked at it.
        /// </summary>
        public static IReadOnlyList<string> Backlog { get; } = new[]
        {
            "Heey stary, masz już to za sobą? Papiery odebrane?",
            "Oszczędzę ci to co przechodziłem sam... Pokażę ci co i jak w tym biznesie"
        };

        /// <summary>What he types while the player is watching, with the pause before each.</summary>
        public static IReadOnlyList<(float DelaySeconds, float TypingSeconds, string Text)> Live { get; }
            = new[]
            {
                (3f, 2f, "._."),
                (0f, 2f, "Stary, koniec melanżu. Chyba że mnie zapraszasz!")
            };

        /// <summary>Take the tutorial.</summary>
        public const string ReplyAccept =
            "Siema stary, dokładnie po wszystkim. Wprowadzisz mnie byle szybko?";

        /// <summary>Skip it.</summary>
        public const string ReplyDecline = "Nie mogę tracić czasu ;) Widzimy się w rankingu.";

        /// <summary>What he says when the player walks off. He is not offended, exactly.</summary>
        public const string DeclineReply =
            "Ohoho stary, mam nadzieję że wiesz co robisz. PS. Jeden będzie rządzić.";

        /// <summary>
        /// The tour.
        ///
        /// Written the way somebody who has done it would actually explain it: the compute warning
        /// comes second because it is the thing that kills new companies, not because it is the
        /// second most interesting screen. Everything he says about the economy is true of this
        /// game's economy — a tutorial that teaches a rule the simulation does not have is worse
        /// than no tutorial.
        /// </summary>
        public static IReadOnlyList<GuideStep> Steps { get; } = new List<GuideStep>
        {
            new("hud",
                "Dobra. To na dole to twoje centrum dowodzenia — dosłownie wszystko, co ta firma "
                + "robi, klika się stamtąd. Nie ucz się tego na pamięć, po prostu wiedz, że jak "
                + "czegoś szukasz, to jest w tym pasku.",
                GuideTarget.None, "hud-slot"),

            new("burn",
                "Dobra... nie będę pieprzyć marketingowo. Powiem ci od razu o jednej rzeczy, na "
                + "którą musisz uważać, żeby szybko nie zbankrutować.",
                GuideTarget.None),

            new("compute_open",
                "Klikaj w COMPUTE. To tam się wynajmuje moc obliczeniową.",
                GuideTarget.Compute, null, "Otwórz COMPUTE", true),

            new("compute_rent",
                "Nie masz własnych serwerów, więc jesteś skazany na wynajem. I uważaj — to hieny. "
                + "Możesz łatwo przeoczyć, że nagle wydajesz miliony tygodniowo na nadwyżkę mocy. "
                + "Po co? Żeby user miał 10 pingu zamiast 12? Ten, co ma 90, różnicy nie odczuje.",
                GuideTarget.Compute, "fleet-panel"),

            new("compute_dial",
                "Ten zegar to twoje obłożenie. Jak siedzi nisko, płacisz za powietrze. Jak wbija "
                + "się pod sufit, ludzie czekają i odchodzą. Trzymaj go w środku i sprawdzaj po "
                + "każdej premierze.",
                GuideTarget.Compute, "service__dial"),

            new("model_open",
                "Teraz najlepsze. Klikaj w MODEL.",
                GuideTarget.Model, null, "Otwórz MODEL", true),

            new("model_hub",
                "Tu masz zestawienie wszystkiego, co sprzedajesz, i ile ci to niesie. Dwoje drzwi: "
                + "budujesz nowy model albo ulepszasz ten, co już jest. Ulepszenie jest tańsze i "
                + "szybsze — ale sufitu nie podniesie.",
                GuideTarget.Model, "door"),

            new("create_open",
                "Wchodzimy w NEW MODEL. Pokażę ci, na czym to naprawdę stoi.",
                GuideTarget.Create, null, "Otwórz NEW MODEL", true),

            new("create_scale",
                "SCALE to serce. Parametry i tokeny. Zapamiętaj jedno: jest optymalny stosunek "
                + "tokenów do parametrów i gra ci go pokazuje. Jak przesadzisz w którąkolwiek "
                + "stronę, palisz compute i dostajesz gorszy model za większe pieniądze.",
                GuideTarget.Create, "scale-half"),

            new("create_locked",
                "Widzisz te zaciemnione końcówki suwaków? To nie bug. Tyle, ile potrafisz "
                + "nadzorować. Research je otwiera — i to jest właśnie powód, żeby go robić.",
                GuideTarget.Create, "scale-lock"),

            new("create_precision",
                "Precyzja. Zaczynasz na FP64, bo nic innego jeszcze nie umiesz. To najwolniejsza "
                + "opcja, jaka istnieje. Pierwszy research w drzewie otwiera FP32, potem BF16 — "
                + "i to jest najtańsze przyspieszenie treningu, jakie kupisz.",
                GuideTarget.Create, "chip"),

            new("create_safety",
                "SAFETY. Wiem, że kusi przeklikać. Nie rób tego. Regulator przychodzi po incydencie "
                + "i patrzy, co miałeś włączone w dniu premiery. Tanio teraz albo bardzo drogo "
                + "później.",
                GuideTarget.Create, "tier-plate"),

            new("create_review",
                "Na końcu REVIEW pokazuje ci, co z tego wyjdzie, ile to potrwa i ile zapłacisz. "
                + "Ten czas to prawda — trening trwa dokładnie tyle, ile tu pisze.",
                GuideTarget.Create, "verdict"),

            new("wrap",
                "I tyle ode mnie. Zrób pierwszy model, wypuść go i patrz na kasę. Jak coś, pisz. "
                + "Telefon masz w rogu.",
                GuideTarget.None)
        };

        /// <summary>
        /// The tasks in the corner, in order.
        ///
        /// The same three whether the tutorial was taken or skipped, because they are the opening
        /// of the game rather than the end of the lesson: build something, sell it, and prove the
        /// company can grow the money it started with.
        /// </summary>
        public static IReadOnlyList<(string Id, string Text)> Tasks { get; } = new[]
        {
            ("first_model", "Stwórz pierwszy model"),
            ("first_release", "Wydaj pierwszy model"),
            ("double_cash", "Podwój budżet firmy")
        };
    }
}
