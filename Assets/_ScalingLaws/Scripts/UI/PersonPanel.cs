using System;
using System.Collections.Generic;
using ScalingLaws.Core;
using ScalingLaws.Data;
using ScalingLaws.Simulation;
using UnityEngine.UIElements;

namespace ScalingLaws.UI
{
    /// <summary>
    /// One person, opened.
    ///
    /// **The company has had a payroll for months and no way to look at anybody on it.** The team
    /// page lists people the way a spreadsheet does: a name, a job, a wage and a button that ends
    /// their employment. Everything that makes somebody a person rather than a row — how long they
    /// have been here, whether they are settled, what they asked for when they took the job and
    /// whether they are getting it — existed in the simulation and had nowhere to appear.
    ///
    /// Three tabs, because there are three different questions and stacking them made a card four
    /// screens tall. Who they are, when they work, and what the job is.
    ///
    /// **Nothing here decides anything.** Every figure is read from the simulation and every action
    /// goes back through `CompanySimulation`, which is where money moves and where an event gets
    /// raised. The panel draws what comes back.
    /// </summary>
    public sealed class PersonPanel
    {
        /// <summary>The three questions, in the order somebody thinks of them.</summary>
        private enum Tab
        {
            Person = 0,
            Schedule = 1,
            Role = 2
        }

        private readonly Func<CompanySimulation> company;
        private readonly Action changed;

        private Tab tab = Tab.Person;

        /// <summary>Which person is open, as an index into the roster, or -1 for none.</summary>
        private int open = -1;

        /// <summary>Set once DISMISS has been pressed, because nothing brings them back.</summary>
        private bool dismissArmed;

        private string problem = string.Empty;

        public PersonPanel(Func<CompanySimulation> company, Action changed)
        {
            this.company = company;
            this.changed = changed;
        }

        /// <summary>Whether anybody is open. The shell asks before drawing the scrim.</summary>
        public bool IsOpen => open >= 0;

        /// <summary>
        /// Opens one person, or closes the panel when given the person already open.
        ///
        /// Public and index-based so a test can drive it: an EditMode test dispatches no clicks,
        /// and a panel reachable only through a button would be untestable end to end.
        /// </summary>
        public void Show(int index)
        {
            open = open == index ? -1 : index;
            tab = Tab.Person;
            dismissArmed = false;
            problem = string.Empty;
            changed?.Invoke();
        }

        public void Close() => Show(-1);

        /// <summary>
        /// Opens a tab directly.
        ///
        /// For the proof renders, which dispatch no pointer events into a detached panel and so
        /// could otherwise only ever photograph the first tab. The same reason `OfficeChooser.Open`
        /// and `MailScreen.SendOffer` are public.
        /// </summary>
        public void ShowSchedule() => tab = Tab.Schedule;

        public void ShowRole() => tab = Tab.Role;

        /// <summary>The card, or an empty element when nobody is open.</summary>
        public VisualElement Build()
        {
            var simulation = company();
            var host = new VisualElement();

            if (simulation == null || open < 0 || open >= simulation.State.Staff.Headcount)
            {
                host.style.display = DisplayStyle.None;
                return host;
            }

            var hire = simulation.State.Staff.Hires[open];

            host.AddToClassList("pp-scrim");
            host.RegisterCallback<ClickEvent>(_ => Close());

            var card = new VisualElement();
            card.AddToClassList("pp");

            // Or a click on the card would also reach the scrim behind it and shut the panel
            // between the press and the release.
            card.RegisterCallback<ClickEvent>(evt => evt.StopPropagation());

            card.Add(BuildHead(simulation, hire));
            card.Add(BuildTabs());

            card.Add(tab switch
            {
                Tab.Schedule => BuildSchedule(simulation, hire),
                Tab.Role => BuildRole(simulation, hire),
                _ => BuildPerson(simulation, hire)
            });

            if (problem.Length > 0)
            {
                var trouble = new Label(problem);
                trouble.AddToClassList("pp__problem");
                card.Add(trouble);
            }

            host.Add(card);
            return host;
        }

        // ---- the head ------------------------------------------------------------------------

        /// <summary>
        /// Name, job and the three things a manager can do, in the corner where a window puts its
        /// controls.
        /// </summary>
        private VisualElement BuildHead(CompanySimulation simulation, in Hire hire)
        {
            var head = new VisualElement();
            head.AddToClassList("pp__head");

            var left = new VisualElement();
            left.AddToClassList("pp__who");

            var name = new Label(hire.Label);
            name.AddToClassList("pp__name");
            left.Add(name);

            var job = new Label(hire.Position != PlayerSkill.None
                ? PositionCatalog.Get(hire.Position).Title
                : StaffCatalog.Get(hire.Role).DisplayName);

            job.AddToClassList("pp__job");
            left.Add(job);

            head.Add(left);

            var actions = new VisualElement();
            actions.AddToClassList("pp__actions");

            // **Talk is drawn and disabled on purpose.** Showing where something is going to live
            // is the difference between a player who knows it is coming and one who never finds
            // out, and this project already made that call for the two mark arrows in the browser
            // mock. A test holds that it exists and that it says why.
            var talk = new Button { text = Loc.T("person.talk") };
            talk.AddToClassList("pp__action");
            talk.AddToClassList("pp__action--soon");
            talk.SetEnabled(false);
            talk.tooltip = Loc.T("person.talk_soon");
            actions.Add(talk);

            var bonus = new Button(() => tab = Tab.Person) { text = Loc.T("person.bonus") };
            bonus.AddToClassList("pp__action");
            actions.Add(bonus);

            var dismiss = new Button(() => Dismiss(simulation))
            {
                text = dismissArmed ? Loc.T("person.dismiss_sure") : Loc.T("person.dismiss")
            };

            dismiss.AddToClassList("pp__action");
            dismiss.AddToClassList("pp__action--go");
            dismiss.EnableInClassList("pp__action--armed", dismissArmed);
            actions.Add(dismiss);

            var close = new Button(Close) { text = "X" };
            close.AddToClassList("pp__action");
            close.AddToClassList("pp__action--close");
            actions.Add(close);

            head.Add(actions);
            return head;
        }

        /// <summary>Two clicks, because there is no severance and nothing brings them back.</summary>
        private void Dismiss(CompanySimulation simulation)
        {
            if (!dismissArmed)
            {
                dismissArmed = true;
                problem = Loc.T("person.dismiss_note");
                changed?.Invoke();
                return;
            }

            simulation.TryLetGo(open, out var why);
            problem = why ?? string.Empty;
            open = -1;
            dismissArmed = false;
            changed?.Invoke();
        }

        private VisualElement BuildTabs()
        {
            var row = new VisualElement();
            row.AddToClassList("pp__tabs");

            row.Add(TabButton(Tab.Person, "person.tab_person"));
            row.Add(TabButton(Tab.Schedule, "person.tab_schedule"));
            row.Add(TabButton(Tab.Role, "person.tab_role"));

            return row;
        }

        private Button TabButton(Tab which, string key)
        {
            var button = new Button(() =>
            {
                tab = which;
                problem = string.Empty;
                changed?.Invoke();
            })
            { text = Loc.T(key) };

            button.AddToClassList("pp__tab");
            button.EnableInClassList("pp__tab--on", tab == which);
            return button;
        }

        // ---- the person ----------------------------------------------------------------------

        private VisualElement BuildPerson(CompanySimulation simulation, in Hire hire)
        {
            var body = new VisualElement();
            body.AddToClassList("pp__body");

            var left = new VisualElement();
            left.AddToClassList("pp__left");

            if (hire.Position != PlayerSkill.None)
            {
                var badge = SkillIcons.Badge(hire.Position, 64);
                badge.AddToClassList("pp__portrait");
                left.Add(badge);
            }

            var offered = simulation.State.Benefits;
            var points = BenefitCatalog.PointsFor(offered);

            var loyalty = Loyalty.For(hire, simulation.State.Date, points,
                StaffCatalog.Get(hire.Role).SalaryPerYearUsd(hire.Skill), offered);

            var band = Loyalty.BandFor(loyalty);

            left.Add(Stat(Loc.T("person.at_company"), Tenure(hire, simulation.State.Date)));
            left.Add(Stat(Loc.T("person.wage"), hire.HourlyWageUsd > 0.0
                ? Loc.T("mail.per_hour", UiFormat.Number(hire.HourlyWageUsd, 2))
                : Loc.T("offices.deal_none")));

            left.Add(Stat(Loc.T("person.a_year"), UiFormat.Money(hire.SalaryPerYearUsd)));
            left.Add(Stat(Loc.T("person.skill"), hire.Skill.ToString()));
            left.Add(Stat(Loc.T("person.found_via"),
                HiringChannels.Get(hire.Source).DisplayName));

            if (hire.BonusDays > 0)
            {
                left.Add(Stat(Loc.T("person.bonus_credited"),
                    Loc.Counted(hire.BonusDays, "noun.day")));
            }

            // Loyalty last and largest, because it is the one figure that decides whether somebody
            // takes a rival's call.
            var loyaltyRow = new VisualElement();
            loyaltyRow.AddToClassList("pp__loyalty");

            var loyaltyLabel = new Label(Loc.T("person.loyalty"));
            loyaltyLabel.AddToClassList("pp__stat-label");
            loyaltyRow.Add(loyaltyLabel);

            var loyaltyValue = new Label(Loyalty.NameOf(band));
            loyaltyValue.AddToClassList("pp__loyalty-band");
            loyaltyValue.AddToClassList(BandClass(band));
            loyaltyRow.Add(loyaltyValue);

            left.Add(loyaltyRow);

            var track = new VisualElement();
            track.AddToClassList("pp__track");

            var fill = new VisualElement();
            fill.AddToClassList("pp__fill");
            fill.style.width = Length.Percent((float)Math.Clamp(loyalty, 0.0, 100.0));
            track.Add(fill);
            left.Add(track);

            left.Add(BuildBonus(simulation, hire));

            body.Add(left);
            body.Add(BuildWants(simulation, hire));
            return body;
        }

        /// <summary>
        /// What a bonus would buy, before it is paid.
        ///
        /// Two fixed sizes rather than a field. The interesting decision is whether to spend a
        /// month of somebody's salary on them at all, not whether to spend 1.3 months, and a text
        /// field here would be a place to mistype four zeroes into a payroll.
        /// </summary>
        private VisualElement BuildBonus(CompanySimulation simulation, in Hire hire)
        {
            var block = new VisualElement();
            block.AddToClassList("pp__bonus");

            var monthly = Math.Max(1L, hire.SalaryPerYearUsd / 12);

            block.Add(BonusButton(simulation, monthly, "person.bonus_month"));
            block.Add(BonusButton(simulation, monthly * 3, "person.bonus_quarter"));

            return block;
        }

        private Button BonusButton(CompanySimulation simulation, long usd, string key)
        {
            var days = simulation.BonusDaysFor(open, usd);

            var button = new Button(() =>
            {
                simulation.TryPayBonus(open, usd, out var why);
                problem = why ?? string.Empty;
                changed?.Invoke();
            })
            {
                text = Loc.T(key) + "   " + UiFormat.Money(usd)
            };

            button.AddToClassList("pp__bonusbutton");
            button.SetEnabled(days > 0 && simulation.State.CashUsd >= usd);
            button.tooltip = Loc.T("person.bonus_buys", Loc.Counted(days, "noun.day"));

            return button;
        }

        /// <summary>
        /// What this person asked for, and whether they are getting it.
        ///
        /// **This is the half of the panel worth opening for.** Everybody values a gym card a
        /// little; the person who asked for one values it a great deal, and the person who asked
        /// and did not get one notices every month. It means the same payroll buys more loyalty at
        /// one company than at another, and it is the reason to read a person rather than a row.
        /// </summary>
        private VisualElement BuildWants(CompanySimulation simulation, in Hire hire)
        {
            var right = new VisualElement();
            right.AddToClassList("pp__right");

            var heading = new Label(Loc.T("person.wants"));
            heading.AddToClassList("pp__section");
            right.Add(heading);

            var wanted = StaffExpectations.For(hire);

            if (wanted.Count == 0)
            {
                var none = new Label(Loc.T("person.wants_nothing"));
                none.AddToClassList("pp__note");
                right.Add(none);
                return right;
            }

            var offered = simulation.State.Benefits;

            foreach (var benefit in wanted)
            {
                var definition = BenefitCatalog.Get(benefit);
                var met = Offers(offered, benefit);

                var row = new VisualElement();
                row.AddToClassList("pp__want");
                row.EnableInClassList("pp__want--met", met);

                var name = new Label(definition.DisplayName);
                name.AddToClassList("pp__want-name");
                row.Add(name);

                var state = new Label(Loc.T(met ? "person.want_met" : "person.want_unmet"));
                state.AddToClassList("pp__want-state");
                row.Add(state);

                // The cost is per head and the BUSINESS page never said so. It says so here.
                var cost = new Label(Loc.T("benefits.per_head",
                    UiFormat.Money(definition.MonthlyCostPerHeadUsd)));

                cost.AddToClassList("pp__want-cost");
                row.Add(cost);

                right.Add(row);
            }

            var verdict = new Label(Loc.T(
                StaffExpectations.IsLookedAfter(hire, offered)
                    ? "person.wants_all_met"
                    : "person.wants_some_unmet"));

            verdict.AddToClassList("pp__note");
            right.Add(verdict);

            var where = new Label(Loc.T("person.wants_where"));
            where.AddToClassList("pp__note");
            where.AddToClassList("pp__note--quiet");
            right.Add(where);

            return right;
        }

        private static bool Offers(IReadOnlyCollection<StaffBenefit> offered, StaffBenefit wanted)
        {
            if (offered == null)
            {
                return false;
            }

            foreach (var benefit in offered)
            {
                if (benefit == wanted)
                {
                    return true;
                }
            }

            return false;
        }

        // ---- the schedule --------------------------------------------------------------------

        private VisualElement BuildSchedule(CompanySimulation simulation, in Hire hire)
        {
            var body = new VisualElement();
            body.AddToClassList("pp__body");
            body.AddToClassList("pp__body--single");

            var heading = new Label(Loc.T("person.hours"));
            heading.AddToClassList("pp__section");
            body.Add(heading);

            var span = new Label(Loc.T("person.hours_span",
                hire.StartHour.ToString("00"), hire.EndHour.ToString("00")));

            span.AddToClassList("pp__hours");
            body.Add(span);

            var length = new Label(Loc.T("person.hours_a_day", hire.HoursPerDay.ToString()));
            length.AddToClassList("pp__note");
            body.Add(length);

            body.Add(BuildDayBar(hire));

            var row = new VisualElement();
            row.AddToClassList("pp__hourbuttons");

            row.Add(HourButton(simulation, "person.earlier", -1, -1));
            row.Add(HourButton(simulation, "person.later", 1, 1));
            row.Add(HourButton(simulation, "person.shorter", 0, -1));
            row.Add(HourButton(simulation, "person.longer", 0, 1));

            body.Add(row);

            var note = new Label(Loc.T("person.hours_note"));
            note.AddToClassList("pp__note");
            note.AddToClassList("pp__note--quiet");
            body.Add(note);

            return body;
        }

        /// <summary>
        /// The day drawn as twenty four cells, because a span of two numbers is a fact and a bar is
        /// a shape. Somebody comparing two people's hours reads the bars, not the digits.
        /// </summary>
        private static VisualElement BuildDayBar(in Hire hire)
        {
            var bar = new VisualElement();
            bar.AddToClassList("pp__day");

            for (var hour = 0; hour < 24; hour++)
            {
                var cell = new VisualElement();
                cell.AddToClassList("pp__hour");
                cell.EnableInClassList("pp__hour--on",
                    hour >= hire.StartHour && hour < hire.EndHour);

                bar.Add(cell);
            }

            return bar;
        }

        private Button HourButton(CompanySimulation simulation, string key, int startBy, int endBy)
        {
            var hire = simulation.State.Staff.Hires[open];
            var start = hire.StartHour + startBy;
            var end = hire.EndHour + endBy;

            var button = new Button(() =>
            {
                simulation.TrySetHours(open, start, end, out var why);
                problem = why ?? string.Empty;
                changed?.Invoke();
            })
            { text = Loc.T(key) };

            button.AddToClassList("pp__hourbutton");
            button.SetEnabled(start >= 0 && end <= 24 && end > start);
            return button;
        }

        // ---- the role ------------------------------------------------------------------------

        private VisualElement BuildRole(CompanySimulation simulation, in Hire hire)
        {
            var body = new VisualElement();
            body.AddToClassList("pp__body");
            body.AddToClassList("pp__body--single");

            var heading = new Label(Loc.T("person.role_does"));
            heading.AddToClassList("pp__section");
            body.Add(heading);

            var title = new Label(hire.Position != PlayerSkill.None
                ? PositionCatalog.Get(hire.Position).Title
                : StaffCatalog.Get(hire.Role).DisplayName);

            title.AddToClassList("pp__rolename");
            body.Add(title);

            if (hire.Position != PlayerSkill.None)
            {
                var blurb = new Label(PositionCatalog.Get(hire.Position).Blurb);
                blurb.AddToClassList("pp__note");
                body.Add(blurb);
            }

            // The one mechanic every role already has, quoted from the constant that governs it
            // rather than restated, so this cannot drift from the simulation.
            body.Add(Stat(Loc.T("person.role_share"),
                UiFormat.Percent(ResearchBudget.StaffShare, 0)));

            var how = new Label(Loc.T("person.role_research"));
            how.AddToClassList("pp__note");
            body.Add(how);

            var more = new Label(Loc.T("person.role_more"));
            more.AddToClassList("pp__note");
            more.AddToClassList("pp__note--quiet");
            body.Add(more);

            return body;
        }

        // ---- small parts ---------------------------------------------------------------------

        /// <summary>
        /// The class for a loyalty band, written out whole.
        ///
        /// **Not `"pp__loyalty-band--" + band`.** `StylesheetTests` reads literals, so a class
        /// assembled at runtime is invisible to the guard that checks every class the interface
        /// uses actually exists, and an unstyled class in UI Toolkit takes default flex and
        /// silently collapses what it is on. It caught this on the first run.
        /// </summary>
        private static string BandClass(LoyaltyBand band) => band switch
        {
            LoyaltyBand.Committed => "pp__loyalty-band--committed",
            LoyaltyBand.Settled => "pp__loyalty-band--settled",
            LoyaltyBand.Open => "pp__loyalty-band--open",
            _ => "pp__loyalty-band--loose"
        };

        private static VisualElement Stat(string label, string value)
        {
            var row = new VisualElement();
            row.AddToClassList("pp__stat");

            var caption = new Label(label);
            caption.AddToClassList("pp__stat-label");
            row.Add(caption);

            var amount = new Label(value);
            amount.AddToClassList("pp__stat-value");
            row.Add(amount);

            return row;
        }

        /// <summary>
        /// How long they have been here, in the unit a person would use.
        ///
        /// Months up to two years and then years with one decimal, because "fourteen months" is how
        /// somebody says it and "1.2 years" is how a spreadsheet does.
        /// </summary>
        public static string Tenure(in Hire hire, GameDate today)
        {
            var days = Math.Max(0, today.DayIndex - hire.StartedOn.DayIndex);
            var months = days / 30.44;

            return months < 24.0
                ? Loc.T("person.months", UiFormat.Number(Math.Floor(months), 0))
                : Loc.T("person.years", UiFormat.Number(days / 365.25, 1));
        }
    }
}
