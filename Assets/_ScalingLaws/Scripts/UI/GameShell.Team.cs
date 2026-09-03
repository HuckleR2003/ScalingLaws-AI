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
    /// The people: the roster, the positions, the payroll and the three hiring channels.
    ///
    /// Part of <see cref="GameShell"/>, split out on 2026-08-29. `partial` is a file boundary and
    /// nothing else: the compiler builds the same type either way, so no field changed lifetime and
    /// no call site moved. The shell had reached 5,800 lines because every screen it ever grew was
    /// written into it rather than beside it, and that is the only thing being corrected here.
    /// </summary>
    public sealed partial class GameShell
    {
        /// <summary>
        /// Team and office on one screen, because they are one decision: desks cap headcount, so a
        /// lease signed months ago is what decides whether the person you need today can start.
        /// </summary>
        /// <summary>
        /// Team and office on one screen, because they are one decision: desks cap headcount, so a
        /// lease signed months ago is what decides whether the person you need today can start.
        ///
        /// **The hiring grid is one tile per founder skill.** The player already learned those seven
        /// words when they spent two hundred points at character creation; making them learn a
        /// second vocabulary to hire would be asking twice for the same thing.
        /// </summary>
        private VisualElement BuildTeamScreen()
        {
            var roster = state.Staff;

            var page = NewPage(Loc.T("page.team"),
                Loc.T("page.team.strap",
                    roster.SeatedHeadcount,
                    roster.Desks,
                    roster.OfficeDefinition.DisplayName,
                    UiFormat.Money(roster.DailyPayrollUsd))
                + (roster.CountFrom(HireSource.Remote) > 0
                    ? " " + Loc.T("page.team.remote", roster.CountFrom(HireSource.Remote))
                    : string.Empty));

            UiParts.ExplainPage(page, TechNotes.Wage);

            page.Add(BuildPositionGrid());
            page.Add(BuildHireButtons());

            if (roster.Headcount > 0)
            {
                page.Add(BuildPayrollList());
            }

            // The two bottom panels share a line. What the team is worth is a table of six
            // readings and wants the width; where you work is one line and a picture and does not.
            var bottom = new VisualElement();
            bottom.AddToClassList("team__bottom");

            var effects = new VisualElement();
            effects.AddToClassList("panel");
            effects.AddToClassList("team__worth");

            var effectsHeading = new Label(Loc.T("panel.team_worth"));
            effectsHeading.AddToClassList("panel__heading");
            UiParts.ExplainHeading(effectsHeading, TechNotes.TeamWorth);
            effects.Add(effectsHeading);

            effects.Add(Row("Training outcome spread",
                $"{UiFormat.Percent(roster.OutcomeVarianceMultiplier())} of baseline"));
            effects.Add(Row("Cluster utilization", $"+{UiFormat.Percent(roster.UtilizationBonus())}"));
            effects.Add(Row("Data quality", $"x{UiFormat.Number(roster.DataQualityMultiplier(), 3)}"));
            effects.Add(Row("Incident risk", $"x{UiFormat.Number(roster.IncidentRiskMultiplier(), 2)}"));
            effects.Add(Row("Brand from the team", $"+{UiFormat.Number(roster.BrandBonus(), 3)}"));
            effects.Add(Row("Research pace", $"x{UiFormat.Number(roster.ResearchSpeedMultiplier(), 3)}"));
            bottom.Add(effects);

            var offices = new VisualElement();
            offices.AddToClassList("panel");
            offices.AddToClassList("team__where");

            var officeHeading = new Label(Loc.T("panel.where_you_work"));
            officeHeading.AddToClassList("panel__heading");
            offices.Add(officeHeading);

            var current = state.Staff.OfficeDefinition;

            var where = new Label(
                Loc.T("team.office_line", current.Level, current.DisplayName,
                state.Staff.SeatedHeadcount, current.Desks,
                UiFormat.Money(current.MonthlyRentUsd)));

            where.AddToClassList("office-now");
            offices.Add(where);

            offices.Add(BuildUpgradeButton());
            bottom.Add(offices);

            page.Add(bottom);
            return page;
        }

        /// <summary>
        /// Seven tiles, one per discipline, in two rows.
        ///
        /// The count sits in a ring in the corner in the position's own colour, so the shape of the
        /// company is readable without reading a single word: four blue and nothing else is a lab
        /// that has never hired anybody to sell anything.
        /// </summary>
        private VisualElement BuildPositionGrid()
        {
            var panel = new VisualElement();
            panel.AddToClassList("panel");

            var heading = new Label(Loc.T("panel.positions"));
            heading.AddToClassList("panel__heading");
            UiParts.ExplainHeading(heading, TechNotes.Positions);
            panel.Add(heading);

            var grid = new VisualElement();
            grid.AddToClassList("posgrid");

            foreach (var position in PositionCatalog.All)
            {
                grid.Add(BuildPositionTile(position));
            }

            panel.Add(grid);
            return panel;
        }

        private VisualElement BuildPositionTile(PositionDefinition position)
        {
            var count = state.Staff.CountOfPosition(position.Skill);

            // A button rather than a plate. The count was already the most useful thing on the
            // tile and it was the one thing you could not act on: seeing "3" and having no way to
            // find out who the three are is a dead end on the screen that is about people.
            var tile = new Button(() => ShowRoster(position.Skill));
            tile.AddToClassList("postile");
            tile.EnableInClassList("postile--staffed", count > 0);
            tile.SetEnabled(count > 0);

            if (ColorUtility.TryParseHtmlString(position.AccentHex, out var accent))
            {
                tile.style.borderLeftColor = accent;
            }

            // 52 to 62: the tiles got narrower so all seven fit on one line, and the icon was
            // the one thing that must not shrink with them.
            var icon = SkillIcons.Badge(position.Skill, 62);
            icon.AddToClassList("postile__icon");
            tile.Add(icon);

            var title = new Label(position.Title.ToUpperInvariant());
            title.AddToClassList("postile__title");
            tile.Add(title);

            var blurb = new Label(position.Blurb);
            blurb.AddToClassList("postile__blurb");
            tile.Add(blurb);

            // The ring is out of flow so it sits in the corner rather than pushing the title down.
            var ring = new VisualElement();
            ring.AddToClassList("postile__ring");

            if (ColorUtility.TryParseHtmlString(position.AccentHex, out var ringColour))
            {
                ring.style.borderTopColor = ringColour;
                ring.style.borderBottomColor = ringColour;
                ring.style.borderLeftColor = ringColour;
                ring.style.borderRightColor = ringColour;
                ring.style.color = ringColour;
            }

            var number = new Label(count.ToString());
            number.AddToClassList("postile__count");
            ring.Add(number);

            tile.Add(ring);

            InsightTip.Attach(tile, position.Title.ToUpperInvariant(),
                $"{position.Blurb} An ordinary one asks about ${position.BaseHourlyWageUsd:N0} an "
                + $"hour. {count} on the team.");

            return tile;
        }

        /// <summary>
        /// Everybody in one discipline, over the screen.
        ///
        /// Built on the same card the finished-run notice uses, because it is the same kind of
        /// moment: something the player asked to look at, over the top of what they were doing,
        /// dismissed by clicking away from it. Reusing that shape means one veil, one card, one set
        /// of manners, rather than a second modal that behaves almost the same.
        /// </summary>
        private void ShowRoster(PlayerSkill position)
        {
            rosterCard?.RemoveFromHierarchy();

            var definition = PositionCatalog.Get(position);
            var people = new List<int>();

            for (var index = 0; index < state.Staff.Headcount; index++)
            {
                if (state.Staff.Hires[index].Position == position)
                {
                    people.Add(index);
                }
            }

            var veil = new VisualElement();
            veil.AddToClassList("notice-veil");
            veil.RegisterCallback<ClickEvent>(_ => rosterCard?.RemoveFromHierarchy());

            var card = new VisualElement();
            card.AddToClassList("notice");
            card.AddToClassList("roster");
            card.RegisterCallback<ClickEvent>(click => click.StopPropagation());

            var head = new VisualElement();
            head.AddToClassList("roster__head");

            var icon = SkillIcons.Badge(position, 54);
            icon.AddToClassList("roster__icon");
            head.Add(icon);

            var words = new VisualElement();
            words.AddToClassList("roster__words");

            var title = new Label(definition.Title.ToUpperInvariant());
            title.AddToClassList("roster__title");
            words.Add(title);

            var under = new Label(people.Count == 1
                ? "One person, and what they cost."
                : $"{people.Count} people, and what they cost.");

            under.AddToClassList("roster__under");
            words.Add(under);
            head.Add(words);

            if (ColorUtility.TryParseHtmlString(definition.AccentHex, out var accent))
            {
                card.style.borderLeftColor = accent;
                title.style.color = accent;
            }

            card.Add(head);

            var list = new ScrollView();
            list.AddToClassList("roster__list");

            foreach (var slot in people)
            {
                list.Add(BuildRosterRow(slot));
            }

            card.Add(list);

            var close = new Button(() => rosterCard?.RemoveFromHierarchy()) { text = Loc.T("common.close") };
            close.AddToClassList("notice__button");
            card.Add(close);

            veil.Add(card);
            rosterCard = veil;
            shellRoot.Add(veil);
        }

        private VisualElement BuildRosterRow(int slot)
        {
            var hire = state.Staff.Hires[slot];
            var channel = HiringChannels.Get(hire.Source);

            var row = new VisualElement();
            row.AddToClassList("rperson");

            var tag = new Label(channel.DisplayName.ToUpperInvariant());
            tag.AddToClassList("rperson__tag");

            if (ColorUtility.TryParseHtmlString(channel.AccentHex, out var tint))
            {
                tag.style.color = tint;
                tag.style.borderTopColor = tint;
                tag.style.borderBottomColor = tint;
                tag.style.borderLeftColor = tint;
                tag.style.borderRightColor = tint;
            }

            row.Add(tag);

            var words = new VisualElement();
            words.AddToClassList("rperson__words");

            var name = new Label(hire.Label);
            name.AddToClassList("rperson__name");
            words.Add(name);

            var since = new Label(hire.HourlyWageUsd > 0.0
                ? $"${hire.HourlyWageUsd:N2} an hour  ·  since {hire.StartedOn}"
                : $"{UiFormat.Money(hire.SalaryPerYearUsd)} a year  ·  since {hire.StartedOn}");

            since.AddToClassList("rperson__since");
            words.Add(since);
            row.Add(words);

            // The way into their own page, which does not exist yet. It is here rather than absent
            // because the row is the only place it will ever belong, and a disabled control that
            // says what it is for is a promise; a missing one is a redesign later.
            var open = new Button { text = Loc.T("common.details") };
            open.AddToClassList("rperson__open");
            open.SetEnabled(false);
            open.tooltip = "Their own page is not built yet.";
            row.Add(open);

            var release = new Button(() =>
            {
                simulation.TryLetGo(slot, out _);
                rosterCard?.RemoveFromHierarchy();
                Show(Screen.Team);
            })
            { text = Loc.T("team.let_go") };

            release.AddToClassList("rperson__release");
            row.Add(release);

            return row;
        }

        /// <summary>
        /// The two ways to start hiring, under the grid.
        ///
        /// Both say what they cost the player before they are pressed: how many desks are free, and
        /// how many remote contracts are left. A hire button that opens a screen only to say no
        /// wastes the click that was the whole point of the screen.
        /// </summary>
        private VisualElement BuildHireButtons()
        {
            var row = new VisualElement();
            row.AddToClassList("hirebar");

            var free = Math.Max(0, state.Staff.Desks - state.Staff.SeatedHeadcount);

            var onSite = new Button(ShowHiringChoice)
            {
                text = $"HIRE NOW     -     ({free} workplace{(free == 1 ? string.Empty : "s")} available)"
            };

            onSite.AddToClassList("hirebar__button");
            onSite.AddToClassList("hirebar__button--main");
            onSite.SetEnabled(free > 0);

            InsightTip.Attach(onSite, "HIRE INTO THE OFFICE",
                "Two routes: the employment register, which is free and sends ordinary people, or "
                + "a specialist search, which costs a fee and finds exactly what you asked for.");

            row.Add(onSite);

            var seats = state.Hiring.RemoteSeats;
            var usedRemote = state.Staff.CountFrom(HireSource.Remote);

            var remote = new Button(() =>
            {
                portals.Open = HiringPortal.Remote;
                Show(Screen.Hiring);
            })
            { text = $"HIRE NOW - REMOTE ({seats - usedRemote})" };

            remote.AddToClassList("hirebar__button");
            remote.AddToClassList("hirebar__button--remote");
            remote.SetEnabled(usedRemote < seats);

            InsightTip.Attach(remote, "HIRE REMOTELY",
                $"IThand.hck. No desk needed, {HiringChannels.Get(HireSource.Remote).WageMultiplier:P0} "
                + "of the usual wage, and the people are much weaker than their profiles claim. "
                + "This is how a company with no office starts.");

            row.Add(remote);
            return row;
        }

        /// <summary>
        /// Who is on the payroll, and where each of them came from.
        ///
        /// The source is a coloured tag rather than a word in a sentence, because the one thing a
        /// player wants from this list at a glance is how much of their company is the cheap kind.
        /// </summary>
        private VisualElement BuildPayrollList()
        {
            var roster = state.Staff;

            var panel = new VisualElement();
            panel.AddToClassList("panel");

            var heading = new Label(Loc.T("panel.payroll"));
            heading.AddToClassList("panel__heading");
            panel.Add(heading);

            var list = new VisualElement();
            list.AddToClassList("crew");

            for (var index = 0; index < roster.Headcount; index++)
            {
                var slot = index;
                var hire = roster.Hires[index];

                // **The row is the way in.** A list of people with no way to look at any of them
                // is a spreadsheet, and everything that makes somebody a person rather than a row
                // was already in the simulation with nowhere to appear.
                var row = new Button(() => personPanel.Show(slot));
                row.AddToClassList("crew__row");

                if (hire.Position != PlayerSkill.None)
                {
                    var icon = SkillIcons.Badge(hire.Position, 24);
                    icon.AddToClassList("crew__icon");
                    row.Add(icon);
                }

                var channel = HiringChannels.Get(hire.Source);

                var tag = new Label(channel.DisplayName.ToUpperInvariant());
                tag.AddToClassList("crew__tag");

                if (ColorUtility.TryParseHtmlString(channel.AccentHex, out var accent))
                {
                    tag.style.color = accent;
                    tag.style.borderTopColor = accent;
                    tag.style.borderBottomColor = accent;
                    tag.style.borderLeftColor = accent;
                    tag.style.borderRightColor = accent;
                }

                row.Add(tag);

                var name = new Label(hire.Label);
                name.AddToClassList("crew__name");
                row.Add(name);

                var job = new Label(hire.Position != PlayerSkill.None
                    ? PositionCatalog.Get(hire.Position).Title
                    : StaffCatalog.Get(hire.Role).DisplayName);

                job.AddToClassList("crew__job");
                row.Add(job);

                var pay = new Label(hire.HourlyWageUsd > 0.0
                    ? $"${hire.HourlyWageUsd:N2}/h"
                    : UiFormat.Money(hire.SalaryPerYearUsd) + "/yr");

                pay.AddToClassList("crew__pay");
                row.Add(pay);

                var release = new Button(() =>
                {
                    simulation.TryLetGo(slot, out _);
                    Show(Screen.Team);
                })
                { text = Loc.T("team.let_go") };

                // Or letting somebody go would also open their card, on somebody who no longer
                // works here, and the panel would draw whoever slid into that index.
                release.RegisterCallback<ClickEvent>(evt => evt.StopPropagation());

                release.AddToClassList("crew__release");
                row.Add(release);

                list.Add(row);
            }

            panel.Add(list);
            return panel;
        }

        /// <summary>
        /// Agency or specialist, on a card over the screen.
        ///
        /// The same shape as the card that appears when a training run finishes, because it is the
        /// same moment: the player has committed to something and the game is asking which of two
        /// roads they want. Two very different prices, stated on the buttons.
        /// </summary>
        private void ShowHiringChoice()
        {
            hiringChoice?.RemoveFromHierarchy();

            var veil = new VisualElement();
            veil.AddToClassList("notice-veil");
            veil.RegisterCallback<ClickEvent>(_ => hiringChoice?.RemoveFromHierarchy());

            var card = new VisualElement();
            card.AddToClassList("notice");
            card.AddToClassList("notice--hiring");
            card.RegisterCallback<ClickEvent>(click => click.StopPropagation());

            var title = new Label(Loc.T("hire.where_looking2"));
            title.AddToClassList("notice__title");
            card.Add(title);

            var body = new Label(
                Loc.T("team.both_routes"));

            body.AddToClassList("notice__body");
            card.Add(body);

            var choices = new VisualElement();
            choices.AddToClassList("hirechoice");

            choices.Add(BuildChoiceTile(HireSource.Agency, "EMPLOYMENT AGENCY",
                "Free to use. Sends whoever is on the register, and they are worse than their "
                + "paperwork says. Standard wages.",
                () =>
                {
                    hiringChoice?.RemoveFromHierarchy();
                    portals.Open = HiringPortal.Agency;
                    Show(Screen.Hiring);
                }));

            choices.Add(BuildChoiceTile(HireSource.Specialist, "FIND A SPECIALIST",
                "Costs a search fee whether or not they sign. You set the discipline and the "
                + "minimum level, and what arrives beats the advert. Wages a fifth higher.",
                () =>
                {
                    hiringChoice?.RemoveFromHierarchy();
                    portals.Open = HiringPortal.Specialist;
                    Show(Screen.Hiring);
                }));

            card.Add(choices);

            var cancel = new Button(() => hiringChoice?.RemoveFromHierarchy()) { text = Loc.T("common.not_now") };
            cancel.AddToClassList("notice__button");
            card.Add(cancel);

            veil.Add(card);
            hiringChoice = veil;
            shellRoot.Add(veil);
        }

        private VisualElement BuildChoiceTile(HireSource source, string title, string blurb,
            Action go)
        {
            var channel = HiringChannels.Get(source);

            var tile = new Button(go);
            tile.AddToClassList("hirechoice__tile");

            if (ColorUtility.TryParseHtmlString(channel.AccentHex, out var accent))
            {
                tile.style.borderLeftColor = accent;
            }

            var address = new Label(channel.SiteName);
            address.AddToClassList("hirechoice__url");
            tile.Add(address);

            var name = new Label(title);
            name.AddToClassList("hirechoice__title");
            tile.Add(name);

            var text = new Label(blurb);
            text.AddToClassList("hirechoice__blurb");
            tile.Add(text);

            var numbers = new Label(
                $"wage x{channel.WageMultiplier:0.00}   ·   quality x{channel.QualityMultiplier:0.00}");

            numbers.AddToClassList("hirechoice__numbers");

            if (ColorUtility.TryParseHtmlString(channel.AccentHex, out var tint))
            {
                numbers.style.color = tint;
            }

            tile.Add(numbers);
            return tile;
        }

        /// <summary>Whichever site the player walked into.</summary>
        private VisualElement BuildHiringScreen()
        {
            var page = new VisualElement();
            page.AddToClassList("content");

            // Built by hand rather than through NewPage, so it has to ask for its own strip.
            var strip = PageArt.BannerFor("background_hiring");

            if (strip != null)
            {
                page.Add(strip);
            }

            page.Add(portals.Build());

            if (state.Hiring.OpenCount > 0)
            {
                page.Add(portals.InboxLink());
            }

            var back = new Button(() => Show(Screen.Team)) { text = Loc.T("hire.back_to_team") };
            back.AddToClassList("portal__back");
            page.Add(back);

            return page;
        }

    }
}
