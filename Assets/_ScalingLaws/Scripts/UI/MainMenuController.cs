using System;
using System.Collections.Generic;
using ScalingLaws.Core;
using ScalingLaws.Data;
using ScalingLaws.Persistence;
using ScalingLaws.Simulation;
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

        /// <summary>Traits shown before the SHOW MORE banner. One row, and the row is four wide.</summary>
        public const int TraitsPerRow = 4;

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
        private readonly SkillSet skills = new();
        private CompanyArchetype chosenArchetype = CompanyArchetype.Custom;
        private string companyName = "Prometheus AI";
        private string founderName = "Anonymous";
        private bool showAllTraits;
        private WorldRegion chosenRegion = WorldRegion.America;
        private Country chosenCountry = Country.UnitedStates;

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

            // Both creator pages are laid out to fit a normal window with no scrolling at all. The
            // scroller is there as a floor, not as the plan: without it a page that does not fit is
            // squashed rather than overflowed, which is what put the skill bars through their own
            // titles and cut the second row of traits off the bottom of the screen.
            var centred = next == Stage.Intro;
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
                    root.Add(Scroller(BuildFounder()));
                    break;
                default:
                    root.Add(Scroller(BuildCompany()));
                    break;
            }
        }

        /// <summary>Wraps a page so it scrolls when it is taller than the window instead of squashing.</summary>
        private static VisualElement Scroller(VisualElement content)
        {
            var scroll = new ScrollView(ScrollViewMode.Vertical);
            scroll.AddToClassList("page-scroll");
            scroll.style.flexGrow = 1;
            scroll.contentContainer.style.alignItems = Align.Center;
            scroll.contentContainer.style.paddingTop = 14;
            scroll.contentContainer.style.paddingBottom = 14;
            scroll.Add(content);
            return scroll;
        }

        /// <summary>The BACK and CONTINUE pair every creator page ends with, pinned to the right.</summary>
        private VisualElement Footer(string continueText, Action onContinue, Action onBack, bool ready,
            string blockedReason)
        {
            var footer = new VisualElement();
            footer.AddToClassList("creator-footer");

            if (!ready && !string.IsNullOrEmpty(blockedReason))
            {
                var reason = new Label(blockedReason);
                reason.AddToClassList("creator-footer__reason");
                footer.Add(reason);
            }

            var back = new Button(onBack) { text = "BACK" };
            back.AddToClassList("menu-button");
            back.AddToClassList("menu-button--quiet");
            back.style.width = 150;
            footer.Add(back);

            var forward = new Button(onContinue) { text = continueText };
            forward.AddToClassList("menu-button");
            forward.AddToClassList("menu-button--primary");
            forward.style.width = 230;
            forward.style.marginLeft = 10;
            forward.SetEnabled(ready);
            footer.Add(forward);

            return footer;
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

            var page = new VisualElement();
            page.AddToClassList("creator");

            var heading = new Label("WHO ARE YOU");
            heading.AddToClassList("page-title");
            page.Add(heading);

            var columns = new VisualElement();
            columns.AddToClassList("creator__columns");
            page.Add(columns);

            columns.Add(BuildIdentityColumn());
            columns.Add(BuildSkillsColumn());

            var traits = new VisualElement();
            traits.AddToClassList("traits");

            var traitsHeader = new VisualElement();
            traitsHeader.AddToClassList("traits__header");

            var traitsHeading = new Label("TRAITS");
            traitsHeading.AddToClassList("panel__heading");
            traitsHeader.Add(traitsHeading);

            traitsHeader.Add(Hint($"Pick {FounderTraitCatalog.TraitsPerFounder}. They last the whole campaign "
                + "and every one is a trade, not a bonus."));
            traits.Add(traitsHeader);

            // One row of four, with the rest behind a banner joined to the bottom of it. Eight cards
            // at once is two more rows than the page has room for, and the page not scrolling is
            // worth more than seeing every trait at the same time.
            var all = FounderTraitCatalog.All;
            var pickedIsHidden = chosenTraits.Exists(trait => IndexOfTrait(trait) >= TraitsPerRow);
            var expanded = showAllTraits || pickedIsHidden;
            var visible = expanded ? all.Count : Math.Min(TraitsPerRow, all.Count);

            var block = new VisualElement();
            block.AddToClassList("trait-block");

            var grid = new VisualElement();
            grid.AddToClassList("trait-grid");
            block.Add(grid);

            for (var index = 0; index < visible; index++)
            {
                grid.Add(BuildTraitCard(all[index]));
            }

            var toggle = new Button(() => { showAllTraits = !showAllTraits; Show(Stage.Founder); })
            {
                text = expanded ? "SHOW LESS" : $"SHOW MORE  ({all.Count - visible})"
            };
            toggle.AddToClassList("trait-banner");

            // Collapsing while a pick is in the hidden half would hide a decision the player made.
            toggle.SetEnabled(!pickedIsHidden || showAllTraits);
            if (pickedIsHidden && !showAllTraits)
            {
                toggle.tooltip = "One of your picks is in this half.";
            }

            block.Add(toggle);
            traits.Add(block);

            page.Add(traits);

            var remaining = FounderTraitCatalog.TraitsPerFounder - chosenTraits.Count;
            page.Add(Footer("CONTINUE", () => Show(Stage.Company), () => Show(Stage.Intro),
                remaining == 0,
                remaining == 1 ? "One more trait to pick." : $"{remaining} more traits to pick."));

            return page;
        }

        private VisualElement BuildIdentityColumn()
        {
            var column = new VisualElement();
            column.AddToClassList("creator__identity");

            // The name sits above the portrait so the plate can take every pixel left over. When it
            // was underneath, the column ended in dead space no matter how tall the page got.
            var line = new Label("They call me");
            line.AddToClassList("creator__line");
            column.Add(line);

            var nameField = new TextField { value = founderName };
            nameField.AddToClassList("creator__name");
            nameField.RegisterValueChangedCallback(evt => founderName = evt.newValue);
            column.Add(nameField);

            var portrait = new VisualElement();
            portrait.AddToClassList("portrait");
            var portraitHint = new Label("PORTRAIT");
            portraitHint.AddToClassList("portrait__label");
            portrait.Add(portraitHint);
            column.Add(portrait);

            return column;
        }

        private VisualElement BuildSkillsColumn()
        {
            var column = new VisualElement();
            column.AddToClassList("creator__skills");

            var spent = skills.TotalAllocated;
            var remaining = PlayerSkillLimits.StartingPoints - spent;

            var header = new VisualElement();
            header.AddToClassList("skills-header");

            var title = new Label("SKILLS");
            title.AddToClassList("panel__heading");
            header.Add(title);

            var budget = new Label($"{remaining} POINTS LEFT");
            budget.AddToClassList("skills-budget");
            budget.EnableInClassList("skills-budget--spent", remaining <= 0);
            header.Add(budget);

            column.Add(header);
            column.Add(Hint($"Everything starts at {PlayerSkillLimits.StartingLevel}, where it has no effect "
                + $"either way. Each click adds {PlayerSkillLimits.PointsPerClick}. These keep growing as you "
                + "play, and they are the only thing money cannot buy."));

            // Seven rows in one stack is taller than the page has room for. Four and three side by
            // side is the same information in a little over half the height.
            var split = new VisualElement();
            split.AddToClassList("skills-split");

            var left = new VisualElement();
            left.AddToClassList("skills-split__column");
            var right = new VisualElement();
            right.AddToClassList("skills-split__column");
            right.AddToClassList("skills-split__column--right");

            for (var index = 0; index < PlayerSkillCatalog.All.Count; index++)
            {
                var host = index < 4 ? left : right;
                host.Add(BuildSkillRow(PlayerSkillCatalog.All[index], remaining));
            }

            split.Add(left);
            split.Add(right);
            column.Add(split);

            return column;
        }

        private VisualElement BuildSkillRow(PlayerSkillDefinition definition, int remaining)
        {
            var level = skills.Level(definition.Skill);

            // Three fixed columns: icon, text, controls. The level used to share a column with the
            // plus button and ended up underneath it, so it lives in the control stack now and the
            // bar can never run under the title.
            var row = new VisualElement();
            row.AddToClassList("skill-row");

            // 42 is the tallest box that still fits inside the height the text beside it already
            // sets, so the row does not grow by a pixel. The rest of the size came from cropping
            // the art tight to its glyph: half of each source image was empty margin.
            row.Add(SkillIcons.Badge(definition.Skill, 42));

            var body = new VisualElement();
            body.AddToClassList("skill-row__body");

            var name = new Label(definition.DisplayName.ToUpperInvariant());
            name.AddToClassList("skill-row__name");
            body.Add(name);

            var track = new VisualElement();
            track.AddToClassList("skill-track");
            var fill = new VisualElement();
            fill.AddToClassList("skill-track__fill");
            fill.style.width = Length.Percent(level);
            fill.EnableInClassList("skill-track__fill--lowered", level < PlayerSkillLimits.StartingLevel);
            track.Add(fill);

            var baseline = new VisualElement();
            baseline.AddToClassList("skill-track__baseline");
            baseline.style.left = Length.Percent(PlayerSkillLimits.StartingLevel);
            track.Add(baseline);
            body.Add(track);

            var effect = new Label(definition.ShortEffect);
            effect.AddToClassList("skill-row__effect");
            body.Add(effect);

            row.Add(body);

            var controls = new VisualElement();
            controls.AddToClassList("skill-row__controls");

            var value = new Label($"{level} / {PlayerSkillLimits.MaximumLevel}");
            value.AddToClassList("skill-row__value");
            value.EnableInClassList("skill-row__value--raised", level > PlayerSkillLimits.StartingLevel);
            value.EnableInClassList("skill-row__value--lowered", level < PlayerSkillLimits.StartingLevel);
            controls.Add(value);

            var buttons = new VisualElement();
            buttons.AddToClassList("skill-row__buttons");

            var minus = new Button(() => AdjustSkill(definition.Skill, -PlayerSkillLimits.PointsPerClick))
            { text = "-" };
            minus.AddToClassList("skill-button");
            minus.SetEnabled(level > PlayerSkillLimits.StartingLevel);
            buttons.Add(minus);

            var plus = new Button(() => AdjustSkill(definition.Skill, PlayerSkillLimits.PointsPerClick))
            { text = "+" };
            plus.AddToClassList("skill-button");
            plus.SetEnabled(remaining >= PlayerSkillLimits.PointsPerClick
                && level + PlayerSkillLimits.PointsPerClick <= PlayerSkillLimits.MaximumLevel);
            buttons.Add(plus);

            controls.Add(buttons);
            row.Add(controls);

            row.tooltip = $"{definition.Description}  At 100: {definition.EffectAtFull}.";
            return row;
        }

        private void AdjustSkill(PlayerSkill skill, int delta)
        {
            var level = skills.Level(skill);
            var target = Mathf.Clamp(level + delta, PlayerSkillLimits.StartingLevel, PlayerSkillLimits.MaximumLevel);

            if (delta > 0)
            {
                var remaining = PlayerSkillLimits.StartingPoints - skills.TotalAllocated;
                if (remaining < target - level)
                {
                    return;
                }
            }

            skills.SetLevel(skill, target);
            Show(Stage.Founder);
        }

        private VisualElement BuildTraitCard(FounderTraitDefinition definition)
        {
            var picked = chosenTraits.Contains(definition.Trait);
            var card = new Button(() => ToggleTrait(definition.Trait));
            card.AddToClassList("trait-card");
            card.EnableInClassList("trait-card--picked", picked);
            card.EnableInClassList("trait-card--locked",
                !picked && chosenTraits.Count >= FounderTraitCatalog.TraitsPerFounder);

            var title = new Label(definition.DisplayName.ToUpperInvariant());
            title.AddToClassList("trait-card__title");
            card.Add(title);

            var flavour = new Label(definition.Flavour);
            flavour.AddToClassList("trait-card__flavour");
            card.Add(flavour);

            // The effects sit in their own slightly lighter block so the numbers read as a data
            // panel rather than as more prose. Every line is generated from the multiplier itself,
            // so a balance change can never leave the card describing the old value.
            var effects = new VisualElement();
            effects.AddToClassList("trait-effects");
            foreach (var line in TraitEffectLines(definition))
            {
                effects.Add(line);
            }

            card.Add(effects);

            if (picked)
            {
                var badge = new Label("PICKED");
                badge.AddToClassList("trait-card__badge");
                card.Add(badge);
            }

            card.tooltip = definition.EffectSummary;
            return card;
        }

        private static IEnumerable<VisualElement> TraitEffectLines(FounderTraitDefinition definition)
        {
            yield return EffectLine("Brand", definition.BrandBonus, 0.0, true);
            yield return EffectLine("Operating cost", definition.OperatingCostMultiplier, 1.0, false);
            yield return EffectLine("Research time", definition.ResearchDurationMultiplier, 1.0, false);
            yield return EffectLine("Training speed", definition.TrainingThroughputMultiplier, 1.0, true);
            yield return EffectLine("Hardware price", definition.HardwarePriceMultiplier, 1.0, false);
            yield return EffectLine("Data supply", definition.DataSupplyMultiplier, 1.0, true);
            yield return EffectLine("Valuation", definition.ValuationMultiplier, 1.0, true);
            yield return EffectLine("Reputation gain", definition.ReputationGainMultiplier, 1.0, true);
        }

        /// <summary>
        /// One "label  +12%" row, or nothing at all when the trait does not touch that number.
        /// Colour follows whether the change helps, not whether it is positive: a cost going down
        /// is green even though the figure is negative.
        /// </summary>
        private static VisualElement EffectLine(string label, double value, double neutral, bool higherIsBetter)
        {
            var delta = value - neutral;
            if (Math.Abs(delta) < 0.0005)
            {
                return new VisualElement { style = { display = DisplayStyle.None } };
            }

            var row = new VisualElement();
            row.AddToClassList("trait-effects__row");

            var name = new Label(label);
            name.AddToClassList("trait-effects__label");
            row.Add(name);

            var amount = new Label($"{(delta > 0 ? "+" : string.Empty)}{delta * 100.0:0.#}%");
            amount.AddToClassList("trait-effects__value");
            amount.EnableInClassList("trait-effects__value--good", delta > 0 == higherIsBetter);
            amount.EnableInClassList("trait-effects__value--bad", delta > 0 != higherIsBetter);
            row.Add(amount);

            return row;
        }

        private static int IndexOfTrait(FounderTrait trait)
        {
            var all = FounderTraitCatalog.All;
            for (var index = 0; index < all.Count; index++)
            {
                if (all[index].Trait == trait)
                {
                    return index;
                }
            }

            return 0;
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
            var page = new VisualElement();
            page.AddToClassList("creator");

            var heading = new Label("YOUR LAB");
            heading.AddToClassList("page-title");
            page.Add(heading);

            page.Add(Hint("Four companies that could have existed in 2022, and one country to run "
                + "whichever you take from."));

            // Tiles and the custom banner are one block with no gap between them, so the fifth
            // option reads as part of the same choice rather than as an afterthought below it.
            var block = new VisualElement();
            block.AddToClassList("lab-block");

            var grid = new VisualElement();
            grid.AddToClassList("lab-grid");
            block.Add(grid);

            foreach (var identity in CompanyIdentityCatalog.Tiles())
            {
                grid.Add(BuildCompanyCard(identity));
            }

            var custom = new Button(() => Choose(CompanyArchetype.Custom))
            {
                text = "CREATE YOUR OWN COMPANY"
            };
            custom.AddToClassList("lab-banner");
            custom.EnableInClassList("lab-banner--on",
                CompanyIsChosen && chosenArchetype == CompanyArchetype.Custom);
            block.Add(custom);
            page.Add(block);

            page.Add(BuildRegionSection());

            if (CompanyIsChosen)
            {
                var identity = CompanyIdentityCatalog.Get(chosenArchetype);

                var opening = new VisualElement();
                opening.AddToClassList("panel");
                opening.style.marginTop = 14;
                opening.Add(Hint(identity.Opening));

                var nameField = new TextField("Company name") { value = companyName };
                nameField.AddToClassList("field");
                nameField.RegisterValueChangedCallback(evt => companyName = evt.newValue);
                opening.Add(nameField);
                page.Add(opening);
            }

            page.Add(Footer("BEGIN, JANUARY 2022", Begin, () => Show(Stage.Founder),
                CompanyIsChosen, "Pick a lab first."));

            return page;
        }

        /// <summary>
        /// The map, the country list it opens, and the four numbers that come with the choice.
        /// The three parts are one block with no gaps, because they are one decision.
        /// </summary>
        private VisualElement BuildRegionSection()
        {
            var section = new VisualElement();
            section.AddToClassList("region");

            var header = new VisualElement();
            header.AddToClassList("region__header");

            var title = new Label("REGION");
            title.AddToClassList("panel__heading");
            header.Add(title);

            var chosen = new Label(chosenCountry == Country.None
                ? "No country chosen"
                : WorldRegionCatalog.Get(chosenCountry).DisplayName.ToUpperInvariant());
            chosen.AddToClassList("region__chosen");
            header.Add(chosen);
            section.Add(header);

            var body = new VisualElement();
            body.AddToClassList("region__body");

            var map = new WorldMapElement(chosenRegion, PickRegion);
            body.Add(map);

            var list = new VisualElement();
            list.AddToClassList("region__list");

            if (chosenRegion == WorldRegion.None)
            {
                var prompt = new Label("Click a continent.");
                prompt.AddToClassList("field__hint");
                list.Add(prompt);
            }
            else
            {
                var listTitle = new Label(WorldRegionCatalog.Get(chosenRegion).DisplayName.ToUpperInvariant());
                listTitle.AddToClassList("region__list-title");
                list.Add(listTitle);

                var blurb = new Label(WorldRegionCatalog.Get(chosenRegion).Blurb);
                blurb.AddToClassList("field__hint");
                list.Add(blurb);

                foreach (var country in WorldRegionCatalog.CountriesIn(chosenRegion))
                {
                    var entry = new Button(() => PickCountry(country.Country)) { text = country.DisplayName };
                    entry.AddToClassList("country");
                    entry.EnableInClassList("country--on", country.Country == chosenCountry);
                    list.Add(entry);
                }
            }

            body.Add(list);
            section.Add(body);
            section.Add(BuildRegionEffects());

            return section;
        }

        private VisualElement BuildRegionEffects()
        {
            var strip = new VisualElement();
            strip.AddToClassList("region-effects");

            if (chosenRegion == WorldRegion.None)
            {
                var none = new Label("Pick a region to see what it costs and what it is worth.");
                none.AddToClassList("field__hint");
                strip.Add(none);
                return strip;
            }

            // Before a country is picked the region average stands in, so the three can be compared
            // without committing. It is labelled as an average rather than passed off as exact.
            var exact = chosenCountry != Country.None;
            var profile = exact
                ? WorldRegionCatalog.Get(chosenCountry)
                : WorldRegionCatalog.Average(chosenRegion);

            var caption = new Label(exact
                ? WorldRegionCatalog.Get(chosenCountry).Note
                : $"Average across {WorldRegionCatalog.CountriesIn(chosenRegion).Count} countries. "
                  + "Pick one for the real figures.");
            caption.AddToClassList("region-effects__caption");
            strip.Add(caption);

            var row = new VisualElement();
            row.AddToClassList("region-effects__row");

            row.Add(EffectTile("HARDWARE ACCESS", profile.HardwarePriceMultiplier, 1.0, false,
                "What accelerators cost here"));
            row.Add(EffectTile("CORPORATE TAX", profile.TaxRate, 0.0, false, "Share of operating profit"));
            row.Add(EffectTile("INNOVATION", profile.InnovationMultiplier, 1.0, true,
                "Research and upgrade speed"));
            row.Add(EffectTile("LOCAL COMPETITION", profile.LocalCompetitionMultiplier, 1.0, false,
                "How hard your brand has to work"));

            strip.Add(row);
            return strip;
        }

        private static VisualElement EffectTile(string label, double value, double neutral,
            bool higherIsBetter, string note)
        {
            var tile = new VisualElement();
            tile.AddToClassList("region-tile");

            var name = new Label(label);
            name.AddToClassList("region-tile__label");
            tile.Add(name);

            var delta = value - neutral;
            var amount = new Label(neutral == 0.0
                ? $"{value * 100.0:0.#}%"
                : $"{(delta > 0 ? "+" : string.Empty)}{delta * 100.0:0.#}%");
            amount.AddToClassList("region-tile__value");

            var helpful = Math.Abs(delta) < 0.0005 || delta > 0 == higherIsBetter;
            amount.EnableInClassList("region-tile__value--good", Math.Abs(delta) >= 0.0005 && helpful);
            amount.EnableInClassList("region-tile__value--bad", Math.Abs(delta) >= 0.0005 && !helpful);
            tile.Add(amount);

            var hint = new Label(note);
            hint.AddToClassList("region-tile__note");
            tile.Add(hint);

            return tile;
        }

        private void PickRegion(WorldRegion region)
        {
            chosenRegion = region;
            chosenCountry = Country.None;
            Show(Stage.Company);
        }

        private void PickCountry(Country country)
        {
            chosenCountry = country;
            chosenRegion = WorldRegionCatalog.Get(country).Region;
            Show(Stage.Company);
        }

        private bool CompanyIsChosen { get; set; }

        private VisualElement BuildCompanyCard(CompanyIdentityDefinition identity)
        {
            var card = new Button(() => Choose(identity.Archetype));
            card.AddToClassList("lab-card");
            card.EnableInClassList("lab-card--on", CompanyIsChosen && chosenArchetype == identity.Archetype);

            // The logo is the mark drawn in the house colour. No texture to import, and it still
            // reads as four different companies at a glance.
            var mark = new Label(identity.Mark);
            mark.AddToClassList("lab-mark");
            mark.style.color = HexColor(identity.AccentHex);
            card.Add(mark);

            var title = new Label(identity.DisplayName.ToUpperInvariant());
            title.AddToClassList("lab-card__name");
            card.Add(title);

            var tagline = new Label(identity.Tagline);
            tagline.AddToClassList("lab-card__line");
            card.Add(tagline);

            var stats = new Label($"{UiFormat.Money(identity.StartingCashUsd)}   "
                + $"{FounderTraitCatalog.Get(identity.HouseTrait).DisplayName}");
            stats.AddToClassList("lab-card__line");
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
            SceneFlow.RequestedFounderName =
                string.IsNullOrWhiteSpace(founderName) ? "Anonymous" : founderName.Trim();
            SceneFlow.RequestedSkillLevels = skills.LevelsToArray();
            SceneFlow.RequestedRegion = (int)chosenRegion;
            SceneFlow.RequestedCountry = (int)chosenCountry;

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
