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

        private readonly List<FounderTrait> chosenTraits = new();
        private CompanyArchetype chosenArchetype = CompanyArchetype.Custom;
        private string companyName = "Prometheus AI";

        private int introLine;
        private float introTimer;
        private VisualElement introHost;

        private void OnEnable()
        {
            root = GetComponent<UIDocument>().rootVisualElement;
            if (theme != null && !root.styleSheets.Contains(theme))
            {
                root.styleSheets.Add(theme);
            }

            Show(Stage.Menu);
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
            stage = next;
            root.Clear();
            root.AddToClassList("root");
            root.style.justifyContent = Justify.Center;
            root.style.alignItems = Align.Center;

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
            var panel = Panel(470);

            var brand = new Label("SCALING LAWS");
            brand.AddToClassList("rail__brand");
            brand.style.paddingLeft = 0;
            panel.Add(brand);

            var tagline = new Label("An AI company, from January 2022.");
            tagline.AddToClassList("rail__subtitle");
            tagline.style.paddingLeft = 0;
            panel.Add(tagline);

            var newGame = new Button(OnNewGame) { text = "NEW CAMPAIGN" };
            newGame.AddToClassList("button");
            newGame.AddToClassList("button--primary");
            newGame.style.width = Length.Percent(100);
            newGame.style.marginBottom = 10;
            panel.Add(newGame);

            var resume = new Button(SceneFlow.ResumeCampaign) { text = "CONTINUE" };
            resume.AddToClassList("button");
            resume.style.width = Length.Percent(100);
            resume.style.marginBottom = 10;
            resume.SetEnabled(SaveStore.HasSave);
            panel.Add(resume);

            var quit = new Button(Quit) { text = "QUIT" };
            quit.AddToClassList("button");
            quit.style.width = Length.Percent(100);
            panel.Add(quit);

            panel.Add(Hint(awaitingOverwriteConfirmation
                ? "This overwrites the saved campaign. Press NEW CAMPAIGN again to confirm."
                : SaveStore.HasSave
                    ? "A saved campaign is on this machine."
                    : "No saved campaign yet."));

            return panel;
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
