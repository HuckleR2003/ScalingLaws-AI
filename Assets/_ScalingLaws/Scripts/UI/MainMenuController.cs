using System;
using System.Collections.Generic;
using ScalingLaws.Core;
using ScalingLaws.Data;
using ScalingLaws.Persistence;
using UnityEngine;
using UnityEngine.UIElements;

namespace ScalingLaws.UI
{
    /// <summary>
    /// The front door and the opening flow: menu, then a cold open on January 2022, then who you
    /// are, then which lab you are taking over.
    ///
    /// The opening exists to make the first ten seconds mean something. A tycoon that drops you
    /// straight into a spreadsheet has already lost the argument about whether the numbers matter.
    /// By the time the first screen of the game appears the player has been told what year it is,
    /// what is about to happen in it, and has made two decisions that will follow them for a decade
    /// of game time.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public sealed class MainMenuController : MonoBehaviour
    {
        /// <summary>Seconds each line of the cold open holds before the next appears.</summary>
        public const float IntroLineSeconds = 2.4f;

        private static readonly string[] IntroLines =
        {
            "JANUARY 2022",
            "Language models are a research curiosity with a following of maybe ten thousand people.",
            "Nobody outside the field can name one. Nothing has been productised. Nothing has been priced.",
            "In eleven months a chat box will reach a hundred million users and every assumption in this "
            + "industry will be rewritten twice before 2026.",
            "You have twelve million dollars and no product.",
            "None of the decisions ahead of you can be undone. Most of them are about timing."
        };

        [SerializeField] private StyleSheet theme;

        private enum Stage
        {
            Menu,
            Intro,
            Founder,
            Company
        }

        private VisualElement root;
        private Stage stage = Stage.Menu;
        private bool awaitingOverwriteConfirmation;
        private bool settingsOpen;

        private readonly List<FounderTrait> chosenTraits = new();
        private CompanyArchetype chosenArchetype = CompanyArchetype.Custom;
        private string companyName = "Prometheus AI";

        private int introLine;
        private float introTimer;
        private VisualElement introHost;

        private void OnEnable()
        {
            root = GetComponent<UIDocument>().rootVisualElement;
            UiBootstrap.Prepare(root, theme);

            try
            {
                Show(Stage.Menu);
            }
            catch (Exception exception)
            {
                UiBootstrap.ShowFailure(root, "The menu", exception);
            }
        }

        private void Update()
        {
            if (stage != Stage.Intro)
            {
                return;
            }

            introTimer += Time.unscaledDeltaTime;
            if (introTimer < IntroLineSeconds || introLine >= IntroLines.Length)
            {
                return;
            }

            introTimer = 0f;
            AddIntroLine(IntroLines[introLine]);
            introLine++;
        }

        // ------------------------------------------------------------------ shell

        private void Show(Stage next)
        {
            try
            {
                Render(next);
            }
            catch (Exception exception)
            {
                UiBootstrap.ShowFailure(root, $"The {next} screen", exception);
            }
        }

        private void Render(Stage next)
        {
            stage = next;
            root.Clear();
            root.AddToClassList("root");

            // The menu is a full bleed layout of its own; the later stages are centred cards.
            var centred = next != Stage.Menu;
            root.style.justifyContent = centred ? Justify.Center : Justify.FlexStart;
            root.style.alignItems = centred ? Align.Center : Align.Stretch;

            switch (next)
            {
                case Stage.Menu:
                    root.Add(BuildMenu());
                    break;
                case Stage.Intro:
                    root.Add(BuildIntro());
                    break;
                case Stage.Founder:
                    root.Add(BuildFounder());
                    break;
                default:
                    root.Add(BuildCompany());
                    break;
            }
        }

        private static VisualElement Panel(int width)
        {
            var panel = new VisualElement();
            panel.AddToClassList("panel");
            panel.style.width = width;
            return panel;
        }

        private static Label Heading(string text)
        {
            var label = new Label(text);
            label.AddToClassList("page-title");
            return label;
        }

        private static Label Hint(string text)
        {
            var label = new Label(text);
            label.AddToClassList("field__hint");
            return label;
        }

        // ------------------------------------------------------------------ menu

        private VisualElement BuildMenu()
        {
            var page = new VisualElement();
            page.AddToClassList("menu-root");
            page.style.width = Length.Percent(100);
            page.style.height = Length.Percent(100);

            var layout = new VisualElement();
            layout.AddToClassList("menu-layout");
            page.Add(layout);

            layout.Add(BuildMenuCopy());
            layout.Add(BuildConsoleCard());

            var signature = new VisualElement();
            signature.AddToClassList("studio-signature");
            var studio = new Label("HCK LABS");
            studio.AddToClassList("signature-studio");
            var author = new Label("Marcin 'HCK' Firmuga");
            author.AddToClassList("signature-author");
            signature.Add(studio);
            signature.Add(author);
            page.Add(signature);

            if (settingsOpen)
            {
                page.Add(BuildSettingsPanel());
            }

            return page;
        }

        private VisualElement BuildMenuCopy()
        {
            var copy = new VisualElement();
            copy.AddToClassList("menu-copy");

            var eyebrow = new Label("HCK LABS PRESENTS");
            eyebrow.AddToClassList("menu-eyebrow");
            copy.Add(eyebrow);

            var lockup = new VisualElement();
            lockup.AddToClassList("title-lockup");

            var small = new Label("AN AI COMPANY TYCOON");
            small.AddToClassList("title-line");
            small.AddToClassList("title-line--small");
            lockup.Add(small);

            var first = new Label("SCALING");
            first.AddToClassList("title-line");
            lockup.Add(first);

            var second = new Label("LAWS");
            second.AddToClassList("title-line");
            second.AddToClassList("title-line--accent");
            lockup.Add(second);

            copy.Add(lockup);

            var subtitle = new Label(
                "January 2022. Twelve million dollars, no product, and eleven months before the "
                + "world finds out what any of this is for.");
            subtitle.AddToClassList("menu-subtitle");
            copy.Add(subtitle);

            var actions = new VisualElement();
            actions.AddToClassList("menu-actions");
            copy.Add(actions);

            var resume = new Button(SceneFlow.ResumeCampaign) { text = "CONTINUE" };
            resume.AddToClassList("menu-button");
            resume.AddToClassList("menu-button--primary");
            resume.SetEnabled(SaveStore.HasSave);
            actions.Add(resume);

            var newGame = new Button(OnNewGame)
            {
                text = awaitingOverwriteConfirmation ? "CONFIRM: OVERWRITE" : "NEW COMPANY"
            };
            newGame.AddToClassList("menu-button");
            if (awaitingOverwriteConfirmation)
            {
                newGame.AddToClassList("menu-button--danger");
            }

            actions.Add(newGame);

            var note = new Label(awaitingOverwriteConfirmation
                ? "This deletes the saved campaign. Press again to confirm."
                : SaveStore.HasSave
                    ? "A saved campaign is on this machine."
                    : "No saved campaign yet.");
            note.AddToClassList("menu-note");
            actions.Add(note);

            var settings = new Button(() =>
            {
                settingsOpen = true;
                Show(Stage.Menu);
            })
            { text = "SETTINGS" };
            settings.AddToClassList("menu-button");
            actions.Add(settings);

            var quit = new Button(Quit) { text = "QUIT" };
            quit.AddToClassList("menu-button");
            quit.AddToClassList("menu-button--quiet");
            actions.Add(quit);

            return copy;
        }

        /// <summary>
        /// The decorative panel, and the counterpart to the bakery window in BakaBakeBakery. Built
        /// entirely from styled elements: a loss curve as bars, a rack of status lights, and a few
        /// readouts. No texture is loaded, so the menu looked finished before any art existed and
        /// keeps working if none ever arrives.
        /// </summary>
        private VisualElement BuildConsoleCard()
        {
            var card = new VisualElement();
            card.AddToClassList("console-card");

            var header = new VisualElement();
            header.AddToClassList("console-header");

            var kicker = new Label("TRAINING RUN 001");
            kicker.AddToClassList("console-kicker");
            header.Add(kicker);

            var number = new Label("MUSE-1");
            number.AddToClassList("console-number");
            header.Add(number);

            card.Add(header);

            // A loss curve: tall on the left, flattening to the right, with the last few bars live.
            var chart = new VisualElement();
            chart.AddToClassList("loss-chart");
            var heights = new[] { 100, 82, 70, 61, 54, 49, 45, 42, 39, 37, 35, 34, 33, 32 };
            for (var index = 0; index < heights.Length; index++)
            {
                var bar = new VisualElement();
                bar.AddToClassList("loss-bar");
                if (index >= heights.Length - 3)
                {
                    bar.AddToClassList("loss-bar--live");
                }

                bar.style.height = Length.Percent(heights[index]);
                chart.Add(bar);
            }

            card.Add(chart);

            card.Add(ConsoleRow("PARAMETERS", "20.0B"));
            card.Add(ConsoleRow("TRAINING TOKENS", "400B"));
            card.Add(ConsoleRow("TOKENS PER PARAMETER", "20.0"));
            card.Add(ConsoleRow("PROJECTED CAPABILITY", "21.5"));

            var rack = new VisualElement();
            rack.AddToClassList("rack-row");
            for (var index = 0; index < 18; index++)
            {
                var led = new VisualElement();
                led.AddToClassList("rack-led");
                if (index % 3 != 2)
                {
                    led.AddToClassList("rack-led--on");
                }

                rack.Add(led);
            }

            card.Add(rack);

            var caption = new Label("RENTED CAPACITY  ONLINE");
            caption.AddToClassList("console-caption");
            card.Add(caption);

            return card;
        }

        private static VisualElement ConsoleRow(string label, string value)
        {
            var row = new VisualElement();
            row.AddToClassList("console-row");

            var name = new Label(label);
            name.AddToClassList("console-label");
            row.Add(name);

            var reading = new Label(value);
            reading.AddToClassList("console-value");
            row.Add(reading);

            return row;
        }

        private VisualElement BuildSettingsPanel()
        {
            var panel = new VisualElement();
            panel.AddToClassList("settings-panel");

            var sheet = new VisualElement();
            sheet.AddToClassList("settings-sheet");
            panel.Add(sheet);

            var heading = new Label("SETTINGS");
            heading.AddToClassList("page-title");
            sheet.Add(heading);

            var sub = new Label("These are kept separately from a campaign and survive deleting one.");
            sub.AddToClassList("page-subtitle");
            sheet.Add(sub);

            var volumeRow = new VisualElement();
            volumeRow.AddToClassList("setting-row");
            volumeRow.Add(SettingCopy("MASTER VOLUME", "All music and interface sound."));

            var volumeValue = new Label($"{Mathf.RoundToInt(GameSettings.MasterVolume * 100f)}%");
            volumeValue.AddToClassList("setting-value");

            var volumeSlider = new Slider(0f, 100f) { value = GameSettings.MasterVolume * 100f };
            volumeSlider.style.width = 190;
            volumeSlider.RegisterValueChangedCallback(evt =>
            {
                GameSettings.SetMasterVolume(evt.newValue / 100f);
                volumeValue.text = $"{Mathf.RoundToInt(evt.newValue)}%";
            });

            volumeRow.Add(volumeSlider);
            volumeRow.Add(volumeValue);
            sheet.Add(volumeRow);

            var fullscreenRow = new VisualElement();
            fullscreenRow.AddToClassList("setting-row");
            fullscreenRow.Add(SettingCopy("FULLSCREEN", "Use a borderless full screen window."));
            var fullscreenToggle = new Toggle { value = GameSettings.Fullscreen };
            fullscreenToggle.RegisterValueChangedCallback(evt => GameSettings.SetFullscreen(evt.newValue));
            fullscreenRow.Add(fullscreenToggle);
            sheet.Add(fullscreenRow);

            var motionRow = new VisualElement();
            motionRow.AddToClassList("setting-row");
            motionRow.Add(SettingCopy("REDUCE MOTION",
                "Shortens the opening sequence and holds the office camera still."));
            var motionToggle = new Toggle { value = GameSettings.ReduceMotion };
            motionToggle.RegisterValueChangedCallback(evt => GameSettings.SetReduceMotion(evt.newValue));
            motionRow.Add(motionToggle);
            sheet.Add(motionRow);

            var about = new VisualElement();
            about.AddToClassList("panel");
            about.style.marginTop = 18;
            var studio = new Label("HCK LABS");
            studio.AddToClassList("signature-studio");
            var author = new Label("Marcin 'HCK' Firmuga");
            author.AddToClassList("signature-author");
            var discipline = new Label("SOFTWARE AND GAMES");
            discipline.AddToClassList("console-caption");
            about.Add(studio);
            about.Add(author);
            about.Add(discipline);
            sheet.Add(about);

            var back = new Button(() =>
            {
                settingsOpen = false;
                Show(Stage.Menu);
            })
            { text = "BACK" };
            back.AddToClassList("menu-button");
            back.style.marginTop = 18;
            sheet.Add(back);

            return panel;
        }

        private static VisualElement SettingCopy(string name, string description)
        {
            var copy = new VisualElement();
            copy.AddToClassList("setting-copy");

            var label = new Label(name);
            label.AddToClassList("setting-name");
            copy.Add(label);

            var detail = new Label(description);
            detail.AddToClassList("setting-description");
            copy.Add(detail);

            return copy;
        }

        private void OnNewGame()
        {
            if (SaveStore.HasSave && !awaitingOverwriteConfirmation)
            {
                awaitingOverwriteConfirmation = true;
                Show(Stage.Menu);
                return;
            }

            SaveStore.Clear();
            introLine = 0;
            introTimer = 0f;
            chosenTraits.Clear();
            Show(Stage.Intro);
        }

        // ------------------------------------------------------------------ cold open

        private VisualElement BuildIntro()
        {
            // Deliberately black rather than the game's blue. The campaign has not started yet.
            root.style.backgroundColor = new Color(0f, 0f, 0f, 1f);

            var column = new VisualElement();
            column.style.width = 760;

            introHost = new VisualElement();
            column.Add(introHost);

            var skip = new Button(() => Show(Stage.Founder)) { text = "CONTINUE" };
            skip.AddToClassList("button");
            skip.style.marginTop = 34;
            skip.style.alignSelf = Align.FlexStart;
            column.Add(skip);

            return column;
        }

        private void AddIntroLine(string text)
        {
            var label = new Label(text);
            label.style.whiteSpace = WhiteSpace.Normal;
            label.style.marginBottom = 16;

            if (introLine == 0)
            {
                label.style.fontSize = 46;
                label.style.unityFontStyleAndWeight = FontStyle.Bold;
                label.style.letterSpacing = 8;
            }
            else
            {
                label.style.fontSize = 17;
                label.style.color = new Color(0.78f, 0.83f, 0.9f, 1f);
            }

            introHost.Add(label);
        }

        // ------------------------------------------------------------------ founder

        private VisualElement BuildFounder()
        {
            root.style.backgroundColor = StyleKeyword.Null;

            var column = new VisualElement();
            column.style.width = 900;
            column.Add(Heading("WHO ARE YOU"));
            column.Add(Hint($"Pick {FounderTraitCatalog.TraitsPerFounder}. They apply for the whole campaign "
                + "and every one of them is a trade, not a bonus."));

            var grid = new VisualElement();
            grid.AddToClassList("grid");
            column.Add(grid);

            foreach (var definition in FounderTraitCatalog.All)
            {
                grid.Add(BuildTraitCard(definition));
            }

            var next = new Button(() => Show(Stage.Company)) { text = "CONTINUE" };
            next.AddToClassList("button");
            next.AddToClassList("button--primary");
            next.style.marginTop = 12;
            next.style.alignSelf = Align.FlexStart;
            next.SetEnabled(chosenTraits.Count == FounderTraitCatalog.TraitsPerFounder);
            column.Add(next);

            return column;
        }

        private VisualElement BuildTraitCard(FounderTraitDefinition definition)
        {
            var picked = chosenTraits.Contains(definition.Trait);
            var card = new Button(() => ToggleTrait(definition.Trait));
            card.AddToClassList("card");
            card.EnableInClassList("card--ahead", picked);
            card.EnableInClassList("card--locked",
                !picked && chosenTraits.Count >= FounderTraitCatalog.TraitsPerFounder);

            var title = new Label(definition.DisplayName.ToUpperInvariant());
            title.AddToClassList("card__title");
            card.Add(title);

            var effects = new Label(definition.EffectSummary);
            effects.AddToClassList("card__line");
            effects.style.whiteSpace = WhiteSpace.Normal;
            card.Add(effects);

            if (picked)
            {
                var badge = new Label("PICKED");
                badge.AddToClassList("card__badge");
                card.Add(badge);
            }

            card.tooltip = definition.Flavour;
            return card;
        }

        private void ToggleTrait(FounderTrait trait)
        {
            if (chosenTraits.Contains(trait))
            {
                chosenTraits.Remove(trait);
            }
            else if (chosenTraits.Count < FounderTraitCatalog.TraitsPerFounder)
            {
                chosenTraits.Add(trait);
            }

            Show(Stage.Founder);
        }

        // ------------------------------------------------------------------ company

        private VisualElement BuildCompany()
        {
            var column = new VisualElement();
            column.style.width = 900;
            column.Add(Heading("WHICH LAB"));
            column.Add(Hint("Four companies that could have existed in 2022. Each starts you somewhere "
                + "different on the same map."));

            var grid = new VisualElement();
            grid.AddToClassList("grid");
            column.Add(grid);

            foreach (var identity in CompanyIdentityCatalog.Tiles())
            {
                grid.Add(BuildCompanyCard(identity));
            }

            var custom = new Button(() => Choose(CompanyArchetype.Custom))
            {
                text = "CREATE YOUR OWN COMPANY"
            };
            custom.AddToClassList("button");
            custom.style.width = Length.Percent(100);
            custom.style.marginTop = 6;
            column.Add(custom);

            if (chosenArchetype != CompanyArchetype.Custom || CompanyIsChosen)
            {
                var identity = CompanyIdentityCatalog.Get(chosenArchetype);

                var opening = new VisualElement();
                opening.AddToClassList("panel");
                opening.style.marginTop = 14;
                opening.Add(Hint(identity.Opening));
                column.Add(opening);

                var nameField = new TextField("Company name") { value = companyName };
                nameField.AddToClassList("field");
                nameField.RegisterValueChangedCallback(evt => companyName = evt.newValue);
                column.Add(nameField);

                var begin = new Button(Begin) { text = "BEGIN, JANUARY 2022" };
                begin.AddToClassList("button");
                begin.AddToClassList("button--primary");
                begin.style.width = Length.Percent(100);
                column.Add(begin);
            }

            return column;
        }

        private bool CompanyIsChosen { get; set; }

        private VisualElement BuildCompanyCard(CompanyIdentityDefinition identity)
        {
            var card = new Button(() => Choose(identity.Archetype));
            card.AddToClassList("card");
            card.EnableInClassList("card--ahead", CompanyIsChosen && chosenArchetype == identity.Archetype);

            // The logo is the mark drawn in the house colour. No texture to import, and it still
            // reads as four different companies at a glance.
            var mark = new Label(identity.Mark);
            mark.style.fontSize = 34;
            mark.style.unityFontStyleAndWeight = FontStyle.Bold;
            mark.style.color = HexColor(identity.AccentHex);
            card.Add(mark);

            var title = new Label(identity.DisplayName.ToUpperInvariant());
            title.AddToClassList("card__title");
            card.Add(title);

            var tagline = new Label(identity.Tagline);
            tagline.AddToClassList("card__line");
            card.Add(tagline);

            var stats = new Label($"{UiFormat.Money(identity.StartingCashUsd)}   "
                + $"{FounderTraitCatalog.Get(identity.HouseTrait).DisplayName}");
            stats.AddToClassList("card__line");
            card.Add(stats);

            return card;
        }

        private void Choose(CompanyArchetype archetype)
        {
            chosenArchetype = archetype;
            CompanyIsChosen = true;
            companyName = CompanyIdentityCatalog.Get(archetype).DisplayName;
            Show(Stage.Company);
        }

        private void Begin()
        {
            SceneFlow.StartNewCampaign(
                companyName,
                (int)chosenArchetype,
                chosenTraits.Count > 0 ? (int)chosenTraits[0] : (int)FounderTrait.None,
                chosenTraits.Count > 1 ? (int)chosenTraits[1] : (int)FounderTrait.None);
        }

        private static Color HexColor(string hex)
        {
            return ColorUtility.TryParseHtmlString(hex, out var colour) ? colour : Color.white;
        }

        private static void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
