using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using ScalingLaws.Core;
using ScalingLaws.Data;
using ScalingLaws.Persistence;
using ScalingLaws.Simulation;
using UnityEngine;
using UnityEngine.UIElements;

namespace ScalingLaws.UI
{
    /// <summary>
    /// The research tree, its funding, and the card that opens on a node.
    ///
    /// Part of <see cref="GameShell"/>, split out on 2026-08-29. `partial` is a file boundary and
    /// nothing else: the compiler builds the same type either way, so no field changed lifetime and
    /// no call site moved. The shell had reached 5,800 lines because every screen it ever grew was
    /// written into it rather than beside it, and that is the only thing being corrected here.
    /// </summary>
    public sealed partial class GameShell
    {
        /// <summary>
        /// The technology tree, grouped by era. Every node is visible from day one including the one
        /// at the end, because the whole point of the last era is that the player can see it coming
        /// for years before they can touch it.
        /// </summary>
        /// <summary>
        /// The tree, laid out as a run of nodes that alternates above and below a spine.
        ///
        /// A grid of cards is a list with borders on it, and it hides the one thing the tree is for:
        /// that these are a sequence with prerequisites, not a menu. The zigzag makes the order
        /// readable at a glance and fits three times as many nodes on a screen, because a circle
        /// with an icon in it is smaller than a card with a paragraph in it.
        ///
        /// Selecting a node opens its card underneath. That is where the paragraph goes, and it is
        /// where a node gets room to say what it actually unlocks rather than a one line summary.
        /// </summary>
        private VisualElement BuildResearchScreen()
        {
            var active = state.ActiveResearch;
            // No standing blurb. The tree is the explanation, and a paragraph above it pushed the
            // first era half a screen down for something nobody reads twice.
            var page = NewPage(Loc.T("research.title"),
                active == null
                    ? string.Empty
                    : active.IsWaitingForCompute
                        ? $"{ResearchTree.Get(active.Node).DisplayName} has run its calendar and is "
                          + "waiting on the cluster."
                        : $"{ResearchTree.Get(active.Node).DisplayName} in progress: "
                          + $"{UiFormat.Percent(active.Progress, 0)}, "
                          + $"{Math.Min(active.DaysCompleted, active.DurationDays)} of "
                          + $"{active.DurationDays} days.");
            UiParts.ExplainPage(page, TechNotes.Eras);

            var board = simulation.ResearchBoard();
            var funding = BuildResearchFunding();
            var placedFunding = false;

            if (researchProblem.Length > 0)
            {
                var trouble = new Label(researchProblem);
                trouble.AddToClassList("mcb-problem");
                page.Add(trouble);
                researchProblem = string.Empty;
            }

            // The corner banner carries this too, and the corner banner is hidden on every screen
            // but the office. So the one screen that is about research had no way of telling the
            // player that research was running.
            if (active != null)
            {
                page.Add(BuildResearchingStrip(active));
            }

            foreach (ResearchEra era in Enum.GetValues(typeof(ResearchEra)))
            {
                var nodes = new List<ResearchStanding>();
                var deepening = new List<ResearchStanding>();

                foreach (var standing in board)
                {
                    if (standing.Node.Era != era)
                    {
                        continue;
                    }

                    if (standing.Node.Track == ResearchTrack.ModelImprovement)
                    {
                        deepening.Add(standing);
                    }
                    else
                    {
                        nodes.Add(standing);
                    }
                }

                if (nodes.Count == 0 && deepening.Count == 0)
                {
                    continue;
                }

                var section = new VisualElement();
                section.AddToClassList("era");

                var head = new VisualElement();
                head.AddToClassList("era__head");

                var heading = new Label(EraTitle(era));
                heading.AddToClassList("era__heading");
                head.Add(heading);
                section.Add(head);

                if (nodes.Count > 0)
                {
                    // The capability line, on a board you can lean into. It opens showing the whole
                    // era, because a map that starts zoomed in hides the thing the player came for;
                    // the wheel and the drag are for leaning closer, not for finding your way back.
                    var map = new ResearchMap();

                    // The zoom controls come out of the frame and sit beside the era title. Inside
                    // it they were absolutely positioned at the top right, which is directly over
                    // the last node of the row: era one's final node was half covered and clickable
                    // in about half its area. Adding the element here re-parents it.
                    map.Controls.AddToClassList("rmap__bar--inline");
                    head.Add(map.Controls);

                    var track = new VisualElement();
                    track.AddToClassList("tree-track");

                    var spine = new VisualElement();
                    spine.AddToClassList("tree-spine");
                    track.Add(spine);

                    for (var index = 0; index < nodes.Count; index++)
                    {
                        track.Add(BuildTreeNode(nodes[index], index % 2 == 0));
                    }

                    map.Surface.Add(track);
                    section.Add(map);
                }

                // The second line. A capability node opens a direction the company could not go at
                // all; these deepen something it already does, and reading them as the same kind of
                // decision is what made the tree feel like a shopping list.
                if (deepening.Count > 0)
                {
                    var band = new VisualElement();
                    band.AddToClassList("deepening");

                    var bandHeading = new Label(Loc.T("research.model_improvement"));
                    bandHeading.AddToClassList("deepening__heading");
                    band.Add(bandHeading);

                    var row = new VisualElement();
                    row.AddToClassList("deepening__row");

                    foreach (var standing in deepening)
                    {
                        var node = BuildTreeNode(standing, false);
                        node.AddToClassList("tree-node--small");
                        row.Add(node);
                    }

                    band.Add(row);
                    section.Add(band);
                }

                // Funding rides alongside the first era rather than sitting above everything. It is
                // a setting the player touches twice a campaign and the tree is what they came for,
                // so the tree starts at the top of the screen and the setting fills the gap beside
                // it that the first era's short track leaves empty anyway.
                if (!placedFunding)
                {
                    placedFunding = true;

                    var row = new VisualElement();
                    row.AddToClassList("era-row");

                    section.AddToClassList("era--beside");
                    row.Add(section);
                    row.Add(funding);
                    page.Add(row);
                }
                else
                {
                    page.Add(section);
                }
            }

            if (!placedFunding)
            {
                page.Add(funding);
            }

            return page;
        }

        /// <summary>
        /// One node on the spine. High or low, and its state is carried by the ring rather than by
        /// a word, so a whole era reads without any of it being spelled out.
        /// </summary>
        /// <summary>
        /// How the company pays for discovery, at the top of the research screen.
        ///
        /// Two ways, and they are a real choice rather than a preference. A fixed budget is paid
        /// whatever happens, which is a promise a struggling company cannot keep. A share of revenue
        /// costs nothing in a bad month and nothing is what it discovers, so a company that stops
        /// earning also stops learning exactly when it most needs to catch up.
        /// </summary>
        /// <summary>
        /// Research in progress, on the screen research lives on.
        ///
        /// Days left is the headline because that is the number a player plans around, and the bar
        /// is behind the words rather than under them so the whole strip is the progress, the same
        /// shape the training strip uses in the corner.
        /// </summary>
        private VisualElement BuildResearchingStrip(ResearchProject active)
        {
            var node = ResearchTree.Get(active.Node);
            var left = Math.Max(0, active.DurationDays - active.DaysCompleted);

            var strip = new VisualElement();
            strip.AddToClassList("researching");

            var fill = new VisualElement();
            fill.AddToClassList("researching__fill");
            fill.style.width = Length.Percent(
                (float)(Math.Clamp(active.Progress, 0.0, 1.0) * 100.0));

            strip.Add(fill);

            var text = new VisualElement();
            text.AddToClassList("researching__text");

            var title = new Label(Loc.T("panel.researching"));
            title.AddToClassList("researching__title");
            text.Add(title);

            // A node needs days *and* compute, and only the days pass on their own. A company with
            // its whole fleet on a training run reaches the end of the calendar and stops, and this
            // used to read "0 days left, 30% done" for the rest of the campaign.
            var what = new Label(active.IsWaitingForCompute
                ? $"{node.DisplayName}  ·  the calendar is done, the cluster is not  ·  "
                  + $"{UiFormat.Number(active.PetaflopDaysRemaining, 0)} PF-days still owed"
                : $"{node.DisplayName}  ·  {left} days left  ·  "
                  + $"{UiFormat.Percent(active.Progress, 0)} done");

            what.AddToClassList("researching__what");
            what.EnableInClassList("researching__what--waiting", active.IsWaitingForCompute);
            text.Add(what);

            if (active.IsWaitingForCompute)
            {
                var why = new Label(
                    Loc.T("research.shares_fleet"));

                why.AddToClassList("researching__why");
                text.Add(why);
            }

            strip.Add(text);

            var stop = new Button(() =>
            {
                if (cancelArmed)
                {
                    cancelArmed = false;
                    simulation.TryCancelResearch(out _);
                }
                else
                {
                    cancelArmed = true;
                }

                Show(Screen.Research);
            })
            { text = cancelArmed ? "CONFIRM, NOTHING COMES BACK" : "CANCEL" };

            stop.AddToClassList("researching__stop");
            stop.EnableInClassList("researching__stop--armed", cancelArmed);

            stop.tooltip = "Abandons the programme. The cash and the points were spent on the day it "
                + "started and none of it comes back; what you get is the right to start something "
                + "else today rather than in four months.";

            strip.Add(stop);
            return strip;
        }

        private VisualElement BuildResearchFunding()
        {
            var state = simulation.State;

            var panel = new VisualElement();
            panel.AddToClassList("panel");
            panel.AddToClassList("rfund-half");

            var head = new VisualElement();
            head.AddToClassList("rfund__head");

            var heading = new Label(Loc.T("research.funding"));
            heading.AddToClassList("panel__heading");
            UiParts.ExplainHeading(heading, TechNotes.ResearchPoints);
            heading.style.marginBottom = 0;
            head.Add(heading);

            var banked = new Label(Loc.T("research.points_banked",
                UiFormat.Number(state.ResearchPoints, 0),
                UiFormat.Number(state.ResearchPointsToday, 1)));

            banked.AddToClassList("rfund__banked");
            head.Add(banked);
            panel.Add(head);

            var modes = new VisualElement();
            modes.AddToClassList("rfund__modes");

            modes.Add(FundingChip(Loc.T("research.fixed_budget"), ResearchFundingMode.Fixed,
                state.ResearchFunding == ResearchFundingMode.Fixed));

            modes.Add(FundingChip(Loc.T("research.revenue_share"), ResearchFundingMode.RevenueShare,
                state.ResearchFunding == ResearchFundingMode.RevenueShare));

            panel.Add(modes);

            if (state.ResearchFunding == ResearchFundingMode.Fixed)
            {
                var label = new Label(Loc.T("research.a_month", UiFormat.Money(state.ResearchMonthlyUsd)));
                label.AddToClassList("field__label");
                panel.Add(label);

                // Logarithmic, because the range runs from a thousand to five million and a linear
                // slider would spend nine tenths of its travel on amounts that change nothing.
                var slider = new Slider(
                    Mathf.Log10(ResearchBudget.MinimumMonthlyUsd),
                    Mathf.Log10(ResearchBudget.MaximumMonthlyUsd))
                {
                    value = Mathf.Log10(Math.Max(ResearchBudget.MinimumMonthlyUsd,
                        state.ResearchMonthlyUsd))
                };

                slider.AddToClassList("field");
                slider.RegisterValueChangedCallback(evt =>
                {
                    state.ResearchMonthlyUsd = (long)Math.Round(Math.Pow(10.0, evt.newValue));
                    Show(Screen.Research);
                });

                panel.Add(slider);
            }
            else
            {
                var revenue = simulation.MonthlyRevenueUsd();

                var label = new Label(
                    Loc.T("research.share_of", UiFormat.Percent(state.ResearchRevenueShare, 0),
                    UiFormat.Money(revenue),
                    UiFormat.Money((long)Math.Round(revenue * state.ResearchRevenueShare))));

                label.AddToClassList("field__label");
                panel.Add(label);

                var slider = new Slider(0f, 0.5f) { value = (float)state.ResearchRevenueShare };
                slider.AddToClassList("field");
                slider.RegisterValueChangedCallback(evt =>
                {
                    state.ResearchRevenueShare = evt.newValue;
                    Show(Screen.Research);
                });

                panel.Add(slider);
            }

            var budget = ResearchBudget.MonthlyBudgetUsd(state.ResearchFunding,
                state.ResearchMonthlyUsd, state.ResearchRevenueShare,
                simulation.MonthlyRevenueUsd());

            var hint = new Label(Loc.T("research.funding_note",
                UiFormat.Number(ResearchBudget.PointsFromFunding(budget), 0)));

            hint.AddToClassList("field__hint");
            panel.Add(hint);

            return panel;
        }

        private Button FundingChip(string text, ResearchFundingMode mode, bool on)
        {
            var chip = new Button(() =>
            {
                simulation.State.ResearchFunding = mode;
                Show(Screen.Research);
            })
            { text = text };

            chip.AddToClassList("chip");
            chip.EnableInClassList("chip--on", on);
            return chip;
        }

        /// <summary>
        /// The card that opens when a node is clicked: what it is, what it costs, what it gives.
        ///
        /// It exists because the tree was twenty one circles with a word under each and no way to
        /// find out what any of them did before committing. A player should be able to read a branch
        /// before spending three months on it.
        /// </summary>
        private void ShowResearchCard(ResearchStanding standing, Vector2 at)
        {
            researchCard?.RemoveFromHierarchy();

            var node = standing.Node;
            researchCard = new VisualElement();
            researchCard.AddToClassList("rcard");
            researchCard.style.left = Mathf.Clamp(at.x, 8f, 1400f);
            researchCard.style.top = Mathf.Clamp(at.y, 8f, 700f);

            var head = new VisualElement();
            head.AddToClassList("rcard__head");

            var icon = new VisualElement();
            icon.AddToClassList("rcard__icon");

            var art = ResearchIcons.Get(node.Id);
            if (art != null)
            {
                icon.style.backgroundImage = new StyleBackground(art);
            }

            head.Add(icon);

            var titles = new VisualElement();
            titles.AddToClassList("rcard__titles");

            var title = new Label(node.DisplayName.ToUpperInvariant());
            title.AddToClassList("rcard__title");
            titles.Add(title);

            var state = standing.IsUnlocked ? "RESEARCHED"
                : standing.IsInProgress ? "IN PROGRESS"
                : standing.CanStart ? "READY" : "LOCKED";

            var badge = new Label(state);
            badge.AddToClassList("rcard__badge");
            badge.EnableInClassList("rcard__badge--ready", standing.CanStart);
            badge.EnableInClassList("rcard__badge--done", standing.IsUnlocked);
            titles.Add(badge);

            head.Add(titles);
            researchCard.Add(head);

            var body = new Label(node.Description);
            body.AddToClassList("rcard__body");
            researchCard.Add(body);

            // What it opens. This is the half of a node that decides whether it is worth doing, and
            // it was only ever drawn on the card at the bottom of the page that nobody scrolled to.
            // Built into a list first, because the heading above them should not print when the
            // node opens nothing, and the source is a lazy sequence that cannot be counted twice.
            var effects = new List<VisualElement>(UnlockLines(node));
            if (effects.Count > 0)
            {
                var opens = new Label(Loc.T("panel.what_it_opens"));
                opens.AddToClassList("rcard__opens");
                researchCard.Add(opens);

                foreach (var line in effects)
                {
                    line.AddToClassList("rcard__unlock");
                    researchCard.Add(line);
                }
            }

            var points = ResearchBudget.PointCostOf(node.CostUsd);
            var cash = ResearchBudget.CashCostOf(node.CostUsd);

            // Three figures rather than a sentence, because these are the three the player is
            // comparing against what they have, and a sentence makes them read it to find them.
            var cost = new VisualElement();
            cost.AddToClassList("rcard__costs");

            cost.Add(RCardFigure("POINTS", $"{points:N0}",
                simulation.State.ResearchPoints >= points));

            cost.Add(RCardFigure("CASH", UiFormat.Money(cash),
                simulation.State.CashUsd >= cash));

            cost.Add(RCardFigure("TAKES", UiFormat.Days(standing.DurationDays), true));

            researchCard.Add(cost);

            var have = new Label(
                Loc.T("research.you_have", UiFormat.Count(simulation.State.ResearchPoints),
                UiFormat.Money(simulation.State.CashUsd)));

            have.AddToClassList("rcard__have");
            researchCard.Add(have);

            if (!standing.CanStart && !standing.IsUnlocked && !standing.IsInProgress)
            {
                var why = new Label(standing.BlockedReason);
                why.AddToClassList("rcard__blocked");
                researchCard.Add(why);
            }

            var buttons = new VisualElement();
            buttons.AddToClassList("rcard__buttons");

            if (standing.CanStart)
            {
                var start = new Button(() =>
                {
                    if (!simulation.TryStartResearch(node.Id, out var why))
                    {
                        // It should not be reachable, since the card only offers this when the
                        // standing says it can start. If it ever is, say why rather than doing
                        // nothing, which is what made this button feel broken before.
                        researchProblem = why;
                        AudioDirector.Deny();
                        Show(Screen.Research);
                        return;
                    }

                    AudioDirector.Confirm();
                    researchCard?.RemoveFromHierarchy();

                    // Same as starting a run. The work is months long and there is nothing further
                    // to do on this screen, so the room is where the player belongs.
                    Show(Screen.Site);
                })
                {
                    text = Loc.T("research.begin_cost", UiFormat.Count(points), UiFormat.Money(cash))
                };

                start.AddToClassList("button");
                start.AddToClassList("button--primary");
                start.style.marginLeft = 0;
                buttons.Add(start);
            }

            var close = new Button(() => researchCard?.RemoveFromHierarchy()) { text = Loc.T("common.close") };
            close.AddToClassList("button");
            close.style.marginLeft = 6;
            buttons.Add(close);

            researchCard.Add(buttons);
            shellRoot.Add(researchCard);
        }

        /// <summary>One figure on the card, greyed when the company cannot cover it.</summary>
        private static VisualElement RCardFigure(string label, string value, bool affordable)
        {
            var figure = new VisualElement();
            figure.AddToClassList("rcard-figure");
            figure.EnableInClassList("rcard-figure--short", !affordable);

            var caption = new Label(label);
            caption.AddToClassList("rcard-figure__label");
            figure.Add(caption);

            var amount = new Label(value);
            amount.AddToClassList("rcard-figure__value");
            figure.Add(amount);

            return figure;
        }

        private VisualElement BuildTreeNode(ResearchStanding standing, bool above)
        {
            var node = standing.Node;

            var column = new VisualElement();
            column.AddToClassList("tree-node");
            column.EnableInClassList("tree-node--above", above);

            // ShowResearchCard was written in full and never called from anywhere, so clicking a
            // node only moved a ring and the player was left to guess what the node did. The click
            // carries its own position, which is why this is a ClickEvent rather than the Button
            // action: the card opens where the finger is rather than in a fixed corner.
            var button = new Button();
            button.RegisterCallback<ClickEvent>(click =>
            {
                selectedResearch = node.Id;
                ShowResearchCard(standing, click.position);
            });

            button.AddToClassList("tree-pip");
            button.EnableInClassList("tree-pip--done", standing.IsUnlocked);
            button.EnableInClassList("tree-pip--running", standing.IsInProgress);
            button.EnableInClassList("tree-pip--ready", !standing.IsUnlocked && standing.CanStart);
            button.EnableInClassList("tree-pip--picked", selectedResearch == node.Id);

            var icon = new VisualElement();
            icon.AddToClassList("tree-pip__icon");

            // One icon lookup, and it reads the catalogued names. The lookup this replaced
            // guessed at names like research_code and research_chat that were never drawn, so every
            // node fell through to the empty badge while the real files sat in Resources/Research.
            var art = ResearchIcons.Get(node.Id);
            if (art != null)
            {
                icon.style.backgroundImage = new StyleBackground(art);
            }
            else
            {
                icon.AddToClassList("tree-pip__icon--none");
            }

            button.Add(icon);
            column.Add(button);

            var label = new Label(node.DisplayName.ToUpperInvariant());
            label.AddToClassList("tree-node__label");
            column.Add(label);

            return column;
        }

        private static IEnumerable<VisualElement> UnlockLines(ResearchNode node)
        {
            if (node.UnlocksArchitecture != ArchitectureId.None)
            {
                yield return UnlockLine("ARCHITECTURE",
                    ArchitectureCatalog.Get(node.UnlocksArchitecture).DisplayName);
            }

            if (node.UnlocksData != DatasetSource.None)
            {
                foreach (var corpus in DatasetCatalog.All)
                {
                    if ((node.UnlocksData & corpus.Flag) == corpus.Flag)
                    {
                        yield return UnlockLine("CORPUS", corpus.DisplayName);
                    }
                }
            }

            if (node.UnlocksTier != ComputeTier.None)
            {
                yield return UnlockLine("COMPUTE TIER", node.UnlocksTier.ToString());
            }

            // ModelTrait has no None member, so the gate flag is the only honest signal that a node
            // actually opens an upgrade line rather than defaulting to the zero trait.
            if (node.GatesTrait)
            {
                yield return UnlockLine("UPGRADE LINE", node.UnlocksTrait.ToString());
            }

            foreach (var definition in ModelTypeCatalog.All)
            {
                if (definition.Requires == node.Id)
                {
                    yield return UnlockLine("MODEL TYPE", definition.DisplayName);
                }
            }

            foreach (var required in node.Prerequisites)
            {
                yield return UnlockLine("NEEDS FIRST", ResearchTree.Get(required).DisplayName);
            }
        }

        private static VisualElement UnlockLine(string kind, string what)
        {
            var row = new VisualElement();
            row.AddToClassList("unlock-row");

            var tag = new Label(kind);
            tag.AddToClassList("unlock-row__tag");
            tag.EnableInClassList("unlock-row__tag--needs", kind == "NEEDS FIRST");
            row.Add(tag);

            var name = new Label(what);
            name.AddToClassList("unlock-row__name");
            row.Add(name);

            return row;
        }

        private static string EraTitle(ResearchEra era) => era switch
        {
            ResearchEra.Foundations => Loc.T("research.era.1"),
            ResearchEra.Scaling => Loc.T("research.era.2"),
            ResearchEra.Autonomy => Loc.T("research.era.3"),
            ResearchEra.Superintelligence => Loc.T("research.era.4"),
            _ => Loc.T("research.era.5")
        };

    }
}
