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

            // **The list and the jobs share a line now.** Reported: the position tiles were too
            // big, over half of each one was empty, and there was no way to see the people at
            // all without opening a discipline one at a time. The tiles are the narrow column
            // on the right because there are eight of them and they are a menu; who works here
            // is the wide column on the left because it is the thing being read.
            var crew = new VisualElement();
            crew.AddToClassList("team__row");
            crew.Add(BuildCrewPanel());
            crew.Add(BuildPositionGrid());
            page.Add(crew);

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

            effects.Add(Row(Loc.T("team.stat.spread"),
                Loc.T("team.of_baseline", UiFormat.Percent(roster.OutcomeVarianceMultiplier()))));
            effects.Add(Row(Loc.T("team.stat.utilisation"), $"+{UiFormat.Percent(roster.UtilizationBonus())}"));
            effects.Add(Row(Loc.T("skill.data.short"), $"x{UiFormat.Number(roster.DataQualityMultiplier(), 3)}"));
            effects.Add(Row(Loc.T("team.stat.incident_risk"), $"x{UiFormat.Number(roster.IncidentRiskMultiplier(), 2)}"));
            effects.Add(Row(Loc.T("team.stat.brand"), $"+{UiFormat.Number(roster.BrandBonus(), 3)}"));
            effects.Add(Row(Loc.T("team.stat.research_pace"), $"x{UiFormat.Number(roster.ResearchSpeedMultiplier(), 3)}"));
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
        /// Eight jobs, two rows of four, in a column beside the people.
        ///
        /// **Reported: the tiles were too big and half of each one was empty.** They carried the
        /// icon, the title and the whole blurb stacked vertically, which is three lines of
        /// reading for something that is a menu. The blurb moved into the card that opens when
        /// one is clicked, which is where somebody deciding whether to hire is actually looking,
        /// and the tile is an icon with its title and count beside it.
        ///
        /// The count still sits in the position's own colour, so the shape of the company is
        /// readable without reading a word: four blue and nothing else is a lab that has never
        /// hired anybody to sell anything.
        /// </summary>
        private VisualElement BuildPositionGrid()
        {
            var panel = new VisualElement();
            panel.AddToClassList("panel");
            panel.AddToClassList("team__jobs");

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

        /// <summary>
        /// One job: the icon, its name and how many of them there are, side by side.
        ///
        /// **Always openable, which is the change that matters.** It used to disable itself when
        /// nobody held the job, so the one state where the player wants to know what the job is
        /// and what it costs was the one state that refused every click. A tile that reads as an
        /// option and does nothing reads as a bug, which this project has now shipped twice.
        /// </summary>
        private VisualElement BuildPositionTile(PositionDefinition position)
        {
            var count = state.Staff.CountOfPosition(position.Skill);

            var tile = new Button(() => ShowPositionCard(position.Skill));
            tile.AddToClassList("postile");
            tile.EnableInClassList("postile--staffed", count > 0);

            if (ColorUtility.TryParseHtmlString(position.AccentHex, out var accent))
            {
                tile.style.borderLeftColor = accent;
            }

            var icon = SkillIcons.Badge(position.Skill, 28);
            icon.AddToClassList("postile__icon");
            tile.Add(icon);

            var words = new VisualElement();
            words.AddToClassList("postile__words");

            var title = new Label(position.Title.ToUpperInvariant());
            title.AddToClassList("postile__title");
            words.Add(title);

            var rate = new Label(Loc.T("mail.per_hour",
                UiFormat.Number(position.BaseHourlyWageUsd, 0)));

            rate.AddToClassList("postile__rate");
            words.Add(rate);
            tile.Add(words);

            var number = new Label(count.ToString());
            number.AddToClassList("postile__count");

            if (ColorUtility.TryParseHtmlString(position.AccentHex, out var ringColour))
            {
                number.style.color = ringColour;
            }

            tile.Add(number);

            InsightTip.Attach(tile, position.Title.ToUpperInvariant(),
                Loc.T("team.position.note", position.Blurb,
                    UiFormat.Number(position.BaseHourlyWageUsd, 0), count));

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
        public void ShowPositionCard(PlayerSkill position)
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

            // Loc.Counted rather than a one-or-many ternary: Polish needs three forms here,
            // and "1 osoba" / "2 osoby" / "5 osob" is not a question of singular against plural.
            var under = new Label(
                Loc.T("team.roster_under", Loc.Counted(people.Count, "noun.person")));

            under.AddToClassList("roster__under");
            words.Add(under);
            head.Add(words);

            if (ColorUtility.TryParseHtmlString(definition.AccentHex, out var accent))
            {
                card.style.borderLeftColor = accent;
                title.style.color = accent;
            }

            card.Add(head);

            // **The two ways to fill the job, at the top, on the card about the job.** Hiring
            // used to be one bar under the grid that said nothing about which discipline it was
            // going to fill, so picking a job and hiring somebody were two unrelated actions on
            // one screen. One door per subject.
            card.Add(BuildPositionHiring());

            // What the job is, which is the line that used to be squeezed onto the tile at a
            // size nobody read it at.
            var blurb = new Label(definition.Blurb);
            blurb.AddToClassList("roster__blurb");
            card.Add(blurb);

            var list = ScrollMemory.Keep(new ScrollView(), "team.roster." + definition.Role);
            list.AddToClassList("roster__list");

            foreach (var slot in people)
            {
                list.Add(BuildRosterRow(slot));
            }

            // **Said rather than left blank.** An empty list under a heading that says how many
            // people are in the role reads as a panel that failed to load.
            if (people.Count == 0)
            {
                var none = new Label(Loc.T("team.nobody_in_role"));
                none.AddToClassList("roster__empty");
                list.Add(none);
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
                ? Loc.T("team.paid_hourly_since",
                    UiFormat.Number(hire.HourlyWageUsd, 2), hire.StartedOn)
                : Loc.T("team.paid_yearly_since",
                    UiFormat.Money(hire.SalaryPerYearUsd), hire.StartedOn));

            since.AddToClassList("rperson__since");
            words.Add(since);
            row.Add(words);

            // The way into their own page, which does not exist yet. It is here rather than absent
            // because the row is the only place it will ever belong, and a disabled control that
            // says what it is for is a promise; a missing one is a redesign later.
            var open = new Button { text = Loc.T("common.details") };
            open.AddToClassList("rperson__open");
            open.SetEnabled(false);
            open.tooltip = Loc.T("team.page_not_built");
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
        /// <summary>
        /// Full time or remote, on the card for the job being filled.
        ///
        /// **Full time is the one that can be refused, and it says why.** A company in the
        /// garage has no desks, so the button is off and names the reason on hover rather than
        /// sitting there grey: a disabled control with no explanation is the shape this project
        /// has already shipped twice as something a player reads as broken.
        /// </summary>
        private VisualElement BuildPositionHiring()
        {
            var row = new VisualElement();
            row.AddToClassList("hirebar");

            var free = Math.Max(0, state.Staff.Desks - state.Staff.SeatedHeadcount);

            var onSite = new Button(ShowHiringChoice)
            {
                // The English "workplace" plus an S is exactly the shape Polish cannot copy.
                text = Loc.T("team.hire_now_free", Loc.Counted(free, "noun.workplace"))
            };

            onSite.AddToClassList("hirebar__button");
            onSite.AddToClassList("hirebar__button--main");
            onSite.SetEnabled(free > 0);

            InsightTip.Attach(onSite, Loc.T("team.hire_office.title"),
                free > 0
                    ? Loc.T("team.hire_office.note")
                    : Loc.T("team.no_desks_here"));

            row.Add(onSite);

            var seats = state.Hiring.RemoteSeats;
            var usedRemote = state.Staff.CountFrom(HireSource.Remote);

            var remote = new Button(() =>
            {
                portals.Open = HiringPortal.Remote;
                Show(Screen.Hiring);
            })
            { text = Loc.T("team.hire_remote_free", seats - usedRemote) };

            remote.AddToClassList("hirebar__button");
            remote.AddToClassList("hirebar__button--remote");
            remote.SetEnabled(usedRemote < seats);

            InsightTip.Attach(remote, Loc.T("team.hire_remote.title"),
                Loc.T("team.hire_remote.note",
                    UiFormat.Percent(HiringChannels.Get(HireSource.Remote).WageMultiplier, 0)));

            row.Add(remote);
            return row;
        }

        /// <summary>
        /// Who is on the payroll, and where each of them came from.
        ///
        /// The source is a coloured tag rather than a word in a sentence, because the one thing a
        /// player wants from this list at a glance is how much of their company is the cheap kind.
        /// </summary>
        /// <summary>
        /// What a column is ordered by. Public because the header buttons are the only callers
        /// and an EditMode element has no panel to dispatch a click through.
        /// </summary>
        public enum CrewSort
        {
            /// <summary>Longest serving first, which is the order the list opens in.</summary>
            Tenure = 0,
            Loyalty = 1,
            Level = 2,
            Wage = 3
        }

        private CrewSort crewSort = CrewSort.Tenure;

        /// <summary>
        /// Orders the list by one column, largest first.
        ///
        /// **Always descending, and that is deliberate rather than unfinished.** Every column
        /// here answers "who is most" - longest here, most loyal, most skilled, dearest - and a
        /// second click that flips to "who is least" would mean the same header means two things
        /// depending on a state nothing on screen shows. Picking a different column is the
        /// question; there is no second question.
        /// </summary>
        public void SortCrewBy(CrewSort column)
        {
            crewSort = column;
            Show(Screen.Team);
        }

        /// <summary>
        /// Who works here, in an order the player chooses.
        ///
        /// **Reported: there was no way to see the team at all.** The only route to a person was
        /// opening one discipline at a time, so a company of twelve was twelve clicks and no way
        /// to compare anybody with anybody. What a player wants from a staff list is who has been
        /// here longest, who is about to leave, who is carrying the work and what each of them
        /// costs an hour, so those are the four columns and each one orders the list.
        /// </summary>
        private VisualElement BuildCrewPanel()
        {
            var roster = state.Staff;

            var panel = new VisualElement();
            panel.AddToClassList("panel");
            panel.AddToClassList("team__crew");

            var heading = new Label(Loc.T("panel.payroll"));
            heading.AddToClassList("panel__heading");
            panel.Add(heading);

            if (roster.Headcount == 0)
            {
                var alone = new Label(Loc.T("team.nobody_yet"));
                alone.AddToClassList("field__hint");
                panel.Add(alone);
                return panel;
            }

            panel.Add(BuildCrewHeader());

            var list = new VisualElement();
            list.AddToClassList("crew");

            foreach (var index in CrewOrder())
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

                // **Three readings, in the order the headers name them.** The hourly rate was
                // asked for by name and it is the one the player negotiated, so it is the one
                // shown rather than the salary it works out to.
                var years = new Label(UiFormat.Days(
                    Math.Max(0, state.Date.DayIndex - hire.StartedOn.DayIndex)));

                years.AddToClassList("crew__since");
                row.Add(years);

                var loyal = new Label(Loyalty.NameOf(Loyalty.BandFor(LoyaltyOf(hire))));
                loyal.AddToClassList("crew__loyalty");
                row.Add(loyal);

                var level = new Label(hire.Skill.ToString());
                level.AddToClassList("crew__level");
                row.Add(level);

                var pay = new Label(hire.HourlyWageUsd > 0.0
                    ? Loc.T("mail.per_hour", UiFormat.Number(hire.HourlyWageUsd, 2))
                    : Loc.T("team.per_year", UiFormat.Money(hire.SalaryPerYearUsd)));

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

            // **Both captions have been in the phrase book since it was written and neither was
            // ever read.** This card passed the English straight in, so two tiles and two paragraphs
            // sat untranslated on a Polish screen with their translations sitting unused a few
            // hundred lines apart. Found by a duplicate-key failure on an unrelated change.
            choices.Add(BuildChoiceTile(HireSource.Agency, Loc.T("hire.agency"),
                Loc.T("hire.agency.body"),
                () =>
                {
                    hiringChoice?.RemoveFromHierarchy();
                    portals.Open = HiringPortal.Agency;
                    Show(Screen.Hiring);
                }));

            choices.Add(BuildChoiceTile(HireSource.Specialist, Loc.T("hire.specialist"),
                Loc.T("hire.specialist.body"),
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

        /// <summary>
        /// The four headers, and pressing one orders the list by it.
        ///
        /// The lit one is the column in force, because a list that has silently reordered itself
        /// and does not say why is worse than one that never reorders at all.
        /// </summary>
        private VisualElement BuildCrewHeader()
        {
            var row = new VisualElement();
            row.AddToClassList("crew__header");

            // The two leading cells match the row: the icon and the source tag carry no
            // heading, because neither is something to order by.
            var lead = new Label(Loc.T("team.column.person"));
            lead.AddToClassList("crew__headlead");
            row.Add(lead);

            var job = new Label(Loc.T("team.column.role"));
            job.AddToClassList("crew__headjob");
            row.Add(job);

            row.Add(CrewHeaderButton(Loc.T("team.column.since"), CrewSort.Tenure, "crew__since"));
            row.Add(CrewHeaderButton(Loc.T("team.column.loyalty"), CrewSort.Loyalty,
                "crew__loyalty"));

            row.Add(CrewHeaderButton(Loc.T("team.column.level"), CrewSort.Level, "crew__level"));
            row.Add(CrewHeaderButton(Loc.T("team.column.wage"), CrewSort.Wage, "crew__pay"));

            var spacer = new VisualElement();
            spacer.AddToClassList("crew__headspacer");
            row.Add(spacer);

            return row;
        }

        private Button CrewHeaderButton(string caption, CrewSort column, string widthClass)
        {
            var button = new Button(() => SortCrewBy(column)) { text = caption };
            button.AddToClassList("crew__head");
            button.AddToClassList(widthClass);
            button.EnableInClassList("crew__head--on", crewSort == column);
            return button;
        }

        /// <summary>
        /// The slots of everybody on the payroll, in the order the player asked for.
        ///
        /// **Slots, not hires.** Every row on this screen addresses a person by their index in
        /// the roster, and so does letting one go, so an ordering that handed back copies would
        /// open the wrong card the moment the list was not in roster order.
        /// </summary>
        private List<int> CrewOrder()
        {
            var roster = state.Staff;
            var slots = new List<int>(roster.Headcount);

            for (var index = 0; index < roster.Headcount; index++)
            {
                slots.Add(index);
            }

            slots.Sort((left, right) => Key(right).CompareTo(Key(left)));
            return slots;

            double Key(int slot)
            {
                var hire = roster.Hires[slot];

                return crewSort switch
                {
                    CrewSort.Loyalty => LoyaltyOf(hire),
                    CrewSort.Level => hire.Skill,
                    CrewSort.Wage => hire.HourlyWageUsd > 0.0
                        ? hire.HourlyWageUsd
                        : hire.SalaryPerYearUsd / PositionCatalog.PaidHoursPerYear,

                    // Longest here is the largest number of days ago, so the day index is
                    // negated rather than the comparison being reversed for one arm.
                    _ => -hire.StartedOn.DayIndex
                };
            }
        }

        /// <summary>
        /// One person's loyalty, read the way the person card reads it.
        ///
        /// The benefits and the market salary are what make the figure mean anything, and both
        /// are company-wide, so this is the one place that assembles them for a list.
        /// </summary>
        private double LoyaltyOf(Hire hire)
        {
            var offered = state.Benefits;

            return Loyalty.For(hire, state.Date, BenefitCatalog.PointsFor(offered),
                StaffCatalog.Get(hire.Role).SalaryPerYearUsd(hire.Skill), offered);
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

            var numbers = new Label(Loc.T("team.channel_numbers",
                UiFormat.Number(channel.WageMultiplier, 2),
                UiFormat.Number(channel.QualityMultiplier, 2)));

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
