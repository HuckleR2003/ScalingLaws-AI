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
                        ? Loc.T("research.ran_calendar", ResearchTree.Get(active.Node).DisplayName)
                        : Loc.T("research.in_progress_line",
                            ResearchTree.Get(active.Node).DisplayName,
                            UiFormat.Percent(active.Progress, 0),
                            Math.Min(active.DaysCompleted, active.DurationDays),
                            active.DurationDays));
            UiParts.ExplainPage(page, TechNotes.Eras);

            var board = simulation.ResearchBoard();
            var funding = BuildResearchFunding();
            var placedFunding = false;

            // The pips from the previous draw are gone from the tree; keeping their buttons would
            // mean lighting elements nobody can see and, on a tree that changed, lighting the wrong
            // ones. Rebuilt with the board it belongs to.
            treePips.Clear();

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
                var operations = new List<ResearchStanding>();

                foreach (var standing in board)
                {
                    if (standing.Node.Era != era)
                    {
                        continue;
                    }

                    switch (standing.Node.Track)
                    {
                        case ResearchTrack.ModelImprovement:
                            deepening.Add(standing);
                            break;

                        case ResearchTrack.Operations:
                            operations.Add(standing);
                            break;

                        default:
                            nodes.Add(standing);
                            break;
                    }
                }

                if (nodes.Count == 0 && deepening.Count == 0 && operations.Count == 0)
                {
                    continue;
                }

                var section = new VisualElement();
                section.AddToClassList("era");
                section.EnableInClassList("era--statecraft", era == ResearchEra.Statecraft);

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
                    // **At full size, and scrolled along.** Fitting a whole era into the band
                    // shrinks the node titles below the size this project decided was readable,
                    // and a board nobody can read is a board that may as well be a list.
                    var map = new ResearchMap { FitsOnOpen = false };

                    // The zoom controls come out of the frame and sit beside the era title. Inside
                    // it they were absolutely positioned at the top right, which is directly over
                    // the last node of the row: era one's final node was half covered and clickable
                    // in about half its area. Adding the element here re-parents it.
                    map.Controls.AddToClassList("rmap__bar--inline");
                    head.Add(map.Controls);

                    var capability = BuildBoard(era, ResearchTrack.Capability, nodes);
                    map.Surface.Add(capability);
                    map.style.height = capability.BoardHeight + 24f;
                    section.Add(map);

                    // **Where the player left off, not the beginning of an era they finished two
                    // years ago.** Deferred a frame because nothing has been laid out when this
                    // returns, so the card it is asked to centre on still has no position; the
                    // page scroller learned the same lesson the same way.
                    var lookFor = active?.Node ?? selectedResearch;

                    if (lookFor != ResearchNodeId.None && nodes.Any(s => s.Node.Id == lookFor))
                    {
                        var target = lookFor;
                        map.schedule.Execute(() =>
                        {
                            if (treePips.TryGetValue(target, out var card))
                            {
                                map.LookAt(card);
                            }
                        }).ExecuteLater(1);
                    }
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

                    band.Add(BuildBoard(era, ResearchTrack.ModelImprovement, deepening));
                    section.Add(band);
                }

                // The third line, and the only one that is not about the model. These are the room,
                // the payroll and the power bill: research a player buys because of what they own
                // rather than because of what they are training. Its own band for the same reason
                // the deepening band exists, and under it because a company with no basement has no
                // use for any of it.
                if (operations.Count > 0)
                {
                    var band = new VisualElement();
                    band.AddToClassList("deepening");
                    band.AddToClassList("deepening--ops");

                    var bandHeading = new Label(Loc.T("research.operations"));
                    bandHeading.AddToClassList("deepening__heading");
                    bandHeading.AddToClassList("deepening__heading--ops");
                    band.Add(bandHeading);

                    band.Add(BuildBoard(era, ResearchTrack.Operations, operations));
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
                    // **The state board belongs to era five and to nothing else.** It is not a research
                // track: the nodes above it are what the company learns, and this is what it agrees
                // to be responsible for afterwards. Drawing it as a fourth row of cards would have
                // said those were the same kind of decision.
                if (era == ResearchEra.Statecraft)
                {
                    stateBoard ??= new StateBoard(() => simulation, () => Show(Screen.Research));
                    section.Add(stateBoard.Build());
                }

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
                ? Loc.T("research.bar_waiting", node.DisplayName,
                    UiFormat.Number(active.PetaflopDaysRemaining, 0))
                : Loc.T("research.bar_running", node.DisplayName, left,
                    UiFormat.Percent(active.Progress, 0)));

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
            { text = cancelArmed ? Loc.T("research.cancel_confirm") : Loc.T("common.cancel") };

            stop.AddToClassList("researching__stop");
            stop.EnableInClassList("researching__stop--armed", cancelArmed);

            stop.tooltip = Loc.T("research.abandon.note");

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

        /// <summary>Shuts the card and forgets it, so a rebuild does not bring it back.</summary>
        private void CloseResearchCard()
        {
            researchCard?.RemoveFromHierarchy();
            researchCard = null;
            openResearchCard = ResearchNodeId.None;
        }

        /// <summary>
        /// Draws the open card again after the page underneath it was rebuilt.
        ///
        /// **Rebuilt rather than kept.** The figures on it move while it is open: points accrue
        /// daily, so a card that said "needs 50 points, you have 12" has to reach "you have 50"
        /// without the player closing and reopening it, and BEGIN has to light up when it becomes
        /// affordable. Keeping the element would freeze the one thing the player is waiting for.
        /// </summary>
        private void ReopenResearchCard()
        {
            if (openResearchCard == ResearchNodeId.None)
            {
                return;
            }

            foreach (var standing in simulation.ResearchBoard())
            {
                if (standing.Node.Id == openResearchCard)
                {
                    ShowResearchCard(standing, openResearchCardAt);
                    return;
                }
            }

            // The tree no longer carries that node. Nothing removes one today, and a card
            // pointing at something that has stopped existing is worse than no card.
            CloseResearchCard();
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

            // Remembered so the rebuild can put it back. Which node and where, because a card
            // that reopens in the corner of the screen has moved away from what it is about.
            openResearchCard = node.Id;
            openResearchCardAt = at;
            researchCard = new VisualElement();
            researchCard.AddToClassList("rcard");

            // A finished node reads as finished from the ground up, not from a badge. This is the
            // state a player scans a fifty node tree for.
            researchCard.EnableInClassList("rnode--done", standing.IsUnlocked);
            researchCard.style.left = Mathf.Clamp(at.x, 8f, 1400f);
            researchCard.style.top = Mathf.Clamp(at.y, 8f, 700f);

            var head = new VisualElement();
            head.AddToClassList("rcard__head");

            var icon = new VisualElement();
            icon.AddToClassList("rnode__icon");

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

            // **A node can be locked for two different reasons and only one of them is the player's
            // to fix today.** Short of points is a matter of waiting; short of a prerequisite is a
            // different node to go and start. The board already paints that road red on the click,
            // and the card said "LOCKED" for both, in English, in a game that ships in two.
            var blockedBy = standing.IsUnlocked || standing.IsInProgress
                ? new List<ResearchNodeId>()
                : ResearchTree.MissingPrerequisites(node.Id, simulation.State.HasResearch);

            var state = standing.IsUnlocked ? Loc.T("research.badge.done")
                : standing.IsInProgress ? Loc.T("common.in_progress")
                : standing.CanStart ? Loc.T("research.badge.ready")
                : blockedBy.Count > 0 ? Loc.T("research.needs_first")
                : Loc.T("common.locked");

            var badge = new Label(state);
            badge.AddToClassList("rcard__badge");
            badge.EnableInClassList("rcard__badge--ready", standing.CanStart);
            badge.EnableInClassList("rcard__badge--done", standing.IsUnlocked);
            badge.EnableInClassList("rcard__badge--needed", blockedBy.Count > 0);
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

            cost.Add(RCardFigure(Loc.T("research.points"), $"{points:N0}",
                simulation.State.ResearchPoints >= points));

            cost.Add(RCardFigure(Loc.T("research.cash"), UiFormat.Money(cash),
                simulation.State.CashUsd >= cash));

            cost.Add(RCardFigure(Loc.T("research.takes"), UiFormat.Days(standing.DurationDays), true));

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
                    CloseResearchCard();

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

            var close = new Button(CloseResearchCard) { text = Loc.T("common.close") };
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

        /// <summary>
        /// One track of one era, as a board with the lines drawn on it.
        ///
        /// The standings are handed in rather than looked up again, because the caller has
        /// already filtered and sorted them and two readings of "which nodes are in this era"
        /// is two chances to disagree. The placement comes from `ResearchLayout`, which is pure
        /// and tested; nothing here decides where a node goes.
        /// </summary>
        private ResearchBoard BuildBoard(ResearchEra era, ResearchTrack track,
            IReadOnlyList<ResearchStanding> standings)
        {
            var byId = standings.ToDictionary(standing => standing.Node.Id);
            var slots = ResearchLayout.Place(era, track);

            var board = new ResearchBoard();

            board.Fill(slots,
                id => byId.TryGetValue(id, out var standing) ? BuildBoardCard(standing) : null,
                id => byId.ContainsKey(id));

            return board;
        }

        /// <summary>
        /// One node, as a card that says what it is and what it gives.
        ///
        /// **The reward icons are the answer to the report.** A tester asked us to simplify what
        /// each node does because sometimes you do not know what something does, and the honest
        /// reading of that is not shorter prose: it is that the board said nothing at all and
        /// every word about a node lived behind a click. The icons are read off the node by
        /// `ResearchRewards`, so a node that starts unlocking something new says so without
        /// anybody remembering to edit a description.
        /// </summary>
        private VisualElement BuildBoardCard(ResearchStanding standing)
        {
            var node = standing.Node;

            var card = new Button();
            card.AddToClassList("rnode");

            card.RegisterCallback<ClickEvent>(click =>
            {
                selectedResearch = node.Id;
                MarkTheRoadTo(node.Id);
                ShowResearchCard(standing, click.position);
            });

            treePips[node.Id] = card;

            card.EnableInClassList("rnode--done", standing.IsUnlocked);
            card.EnableInClassList("rnode--running", standing.IsInProgress);
            card.EnableInClassList("rnode--ready", !standing.IsUnlocked && standing.CanStart);

            card.EnableInClassList("rnode--locked",
                !standing.IsUnlocked && !standing.IsInProgress && !standing.CanStart);

            card.EnableInClassList("rnode--picked", selectedResearch == node.Id);

            var body = new VisualElement();
            body.AddToClassList("rnode__body");

            var name = new Label(node.DisplayName.ToUpperInvariant());
            name.AddToClassList("rnode__name");
            body.Add(name);

            var rewards = new VisualElement();
            rewards.AddToClassList("rnode__rewards");

            foreach (var reward in ResearchRewards.Of(node))
            {
                var chip = new VisualElement();
                chip.AddToClassList("rnode__reward");

                var art = UnlockIcons.Get(reward.Kind);

                if (art != null)
                {
                    chip.style.backgroundImage = new StyleBackground(art);
                }

                // The name of the thing, on hover. The icon says what kind it is and the board
                // has no room for six words; the card behind the click still spells it out.
                InsightTip.AttachKeyed(chip, Loc.T(UnlockIcons.KeyFor(reward.Kind)), reward.Name);

                rewards.Add(chip);
            }

            body.Add(rewards);
            card.Add(body);

            var icon = new VisualElement();
            icon.AddToClassList("rnode__icon");

            var portrait = ResearchIcons.Get(node.Id);

            if (portrait != null)
            {
                icon.style.backgroundImage = new StyleBackground(portrait);
            }
            else
            {
                icon.AddToClassList("rnode__icon--none");
            }

            card.Add(icon);

            return card;
        }

        /// <summary>Every pip currently on the board, so a selection can light one without a redraw.</summary>
        private readonly Dictionary<ResearchNodeId, Button> treePips = new();

        /// <summary>
        /// Paints the selected node and, in red, everything it is waiting on.
        ///
        /// The chain comes from <see cref="ResearchTree.MissingPrerequisites"/> rather than being
        /// walked here, so the board and the card cannot disagree about what is blocking a node, and
        /// so the rule is testable without a panel.
        ///
        /// Every pip is cleared first. Without that the red accumulates across clicks and the board
        /// ends up showing the union of every road the player has ever looked at.
        /// </summary>
        private void MarkTheRoadTo(ResearchNodeId id)
        {
            foreach (var pair in treePips)
            {
                pair.Value.EnableInClassList("rnode--needed", false);
                pair.Value.EnableInClassList("rnode--picked", pair.Key == id);
            }

            foreach (var missing in ResearchTree.MissingPrerequisites(id, simulation.State.HasResearch))
            {
                if (treePips.TryGetValue(missing, out var pip))
                {
                    pip.EnableInClassList("rnode--needed", true);
                }
            }
        }

        private static IEnumerable<VisualElement> UnlockLines(ResearchNode node)
        {
            if (node.UnlocksArchitecture != ArchitectureId.None)
            {
                yield return UnlockLine(Loc.T("unlock.architecture"),
                    ArchitectureCatalog.Get(node.UnlocksArchitecture).DisplayName);
            }

            if (node.UnlocksData != DatasetSource.None)
            {
                foreach (var corpus in DatasetCatalog.All)
                {
                    if ((node.UnlocksData & corpus.Flag) == corpus.Flag)
                    {
                        yield return UnlockLine(Loc.T("unlock.corpus"), corpus.DisplayName);
                    }
                }
            }

            if (node.UnlocksTier != ComputeTier.None)
            {
                yield return UnlockLine(Loc.T("unlock.tier"), node.UnlocksTier.ToString());
            }

            // ModelTrait has no None member, so the gate flag is the only honest signal that a node
            // actually opens an upgrade line rather than defaulting to the zero trait.
            if (node.GatesTrait)
            {
                yield return UnlockLine(Loc.T("unlock.upgrade_line"), node.UnlocksTrait.ToString());
            }

            foreach (var definition in ModelTypeCatalog.All)
            {
                if (definition.Requires == node.Id)
                {
                    yield return UnlockLine(Loc.T("unlock.model_type"), definition.DisplayName);
                }
            }

            foreach (var required in node.Prerequisites)
            {
                yield return UnlockLine(Loc.T("unlock.needs_first"), ResearchTree.Get(required).DisplayName);
            }
        }

        private static VisualElement UnlockLine(string kind, string what)
        {
            var row = new VisualElement();
            row.AddToClassList("unlock-row");

            var tag = new Label(kind);
            tag.AddToClassList("unlock-row__tag");
            tag.EnableInClassList("unlock-row__tag--needs", kind == Loc.T("unlock.needs_first"));
            row.Add(tag);

            var name = new Label(what);
            name.AddToClassList("unlock-row__name");
            row.Add(name);

            return row;
        }

        /// <summary>The state programme's board. Built once; era five is not always on screen.</summary>
        private StateBoard stateBoard;

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
