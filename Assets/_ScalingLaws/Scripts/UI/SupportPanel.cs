using System;
using ScalingLaws.Data;
using ScalingLaws.Simulation;
using UnityEngine.UIElements;

namespace ScalingLaws.UI
{
    /// <summary>
    /// The support desk, on the screen the product is managed from.
    ///
    /// **The desk is the one system in this game with no picture of itself.** A cluster going under
    /// turns cabinets red and puts milliseconds in the corner; a queue nobody answers is invisible
    /// until the market has already moved. So this panel leads with the two figures that decide
    /// everything, how long an answer takes and what that is doing to the product, and only then
    /// offers the three things money and research can buy.
    ///
    /// Nothing here computes. Every figure is read from `SupportDesk` and `CompanySimulation`, so
    /// the hours printed here are the hours the queue is actually worked through.
    /// </summary>
    public sealed class SupportPanel
    {
        private readonly CompanySimulation simulation;
        private readonly Action openTeam;
        private readonly Action changed;
        private readonly Action<string> failed;

        public SupportPanel(CompanySimulation simulation, Action openTeam, Action changed,
            Action<string> failed)
        {
            this.simulation = simulation;
            this.openTeam = openTeam;
            this.changed = changed;
            this.failed = failed;
        }

        public VisualElement Build()
        {
            var desk = simulation.State.Support;
            var people = simulation.SupportPeople();

            var panel = new VisualElement();
            panel.AddToClassList("panel");
            panel.AddToClassList("support");

            var kicker = new Label(Loc.T("support.desk.kicker"));
            kicker.AddToClassList("support__kicker");
            panel.Add(kicker);

            panel.Add(BuildVerdict(desk, people));
            panel.Add(BuildQueue(desk));
            panel.Add(BuildStaffing(desk, people));

            if (desk.AgentSlots > 0)
            {
                panel.Add(BuildAgents(desk));
            }

            panel.Add(BuildLadders(desk));

            return panel;
        }

        /// <summary>
        /// The headline: how long an answer takes, and what that is worth to the product.
        ///
        /// **The second line is the one that matters** and it is why this panel exists. A player can
        /// read "62 hours" and not know whether that is good; "this is costing the product eleven
        /// per cent of how it is experienced" cannot be misread.
        /// </summary>
        private VisualElement BuildVerdict(SupportDesk desk, double people)
        {
            var block = new VisualElement();
            block.AddToClassList("support__verdict");

            var hours = desk.JudgedHours;
            var quality = desk.Quality();
            var effect = desk.ServiceMultiplier() - 1.0;

            var figure = new Label(UiFormat.Hours(hours));
            figure.AddToClassList("support__hours");
            figure.EnableInClassList("support__hours--bad", quality < 0.5);
            figure.EnableInClassList("support__hours--good", quality >= 0.999);
            block.Add(figure);

            var caption = new Label(Loc.T("support.desk.to_an_answer"));
            caption.AddToClassList("support__caption");
            block.Add(caption);

            var verdict = new Label(effect >= 0.0
                ? Loc.T("support.desk.helping", UiFormat.Percent(effect))
                : Loc.T("support.desk.costing", UiFormat.Percent(-effect)));

            verdict.AddToClassList("support__effect");
            verdict.EnableInClassList("support__effect--bad", effect < 0.0);
            block.Add(verdict);

            var note = new Label(Loc.T("support.desk.judged_over_a_month"));
            note.AddToClassList("support__note");
            block.Add(note);

            return block;
        }

        /// <summary>
        /// What is waiting, by how bad it is. Hours rather than a count of tickets, because hours
        /// are what a person on the desk spends and what the queue is actually made of.
        /// </summary>
        private VisualElement BuildQueue(SupportDesk desk)
        {
            var row = new VisualElement();
            row.AddToClassList("support__queue");

            row.Add(QueueCard(TicketClass.High, desk.BacklogHoursOf(TicketClass.High)));
            row.Add(QueueCard(TicketClass.Medium, desk.BacklogHoursOf(TicketClass.Medium)));
            row.Add(QueueCard(TicketClass.Low, desk.BacklogHoursOf(TicketClass.Low)));

            return row;
        }

        private static VisualElement QueueCard(TicketClass kind, double hours)
        {
            var card = new VisualElement();
            card.AddToClassList("support__class");
            card.AddToClassList(ClassOf(kind));

            var name = new Label(Loc.T(NameKeyOf(kind)));
            name.AddToClassList("support__classname");
            card.Add(name);

            var waiting = new Label(UiFormat.Hours(hours));
            waiting.AddToClassList("support__classhours");
            card.Add(waiting);

            var note = new Label(Loc.T("support.class.waiting"));
            note.AddToClassList("support__classnote");
            card.Add(note);

            return card;
        }

        /// <summary>
        /// Written out rather than built from the enum name, because a key made by concatenation is
        /// invisible to the guard that checks every key the interface asks for exists.
        /// </summary>
        /// <summary>
        /// **Written out, never assembled.** `LocalisationTests` and `StylesheetTests` can only read
        /// literals, so a key or a class built by concatenation is invisible to both of them, and
        /// this project has shipped a screen of raw keys that way once already.
        /// </summary>
        private static string NameKeyOf(TicketClass kind) => kind switch
        {
            TicketClass.Low => "support.class.low",
            TicketClass.Medium => "support.class.medium",
            _ => "support.class.high"
        };

        private static string ClassOf(TicketClass kind) => kind switch
        {
            TicketClass.Low => "support__class--low",
            TicketClass.Medium => "support__class--medium",
            _ => "support__class--high"
        };

        private static string TitleKeyOf(SupportCatalog.Upgrade upgrade) => upgrade switch
        {
            SupportCatalog.Upgrade.Deflection => "support.upgrade.deflection",
            SupportCatalog.Upgrade.ContinuousTraining => "support.upgrade.training",
            _ => "support.upgrade.agents"
        };

        private static string NoteKeyOf(SupportCatalog.Upgrade upgrade) => upgrade switch
        {
            SupportCatalog.Upgrade.Deflection => "support.upgrade.deflection.note",
            SupportCatalog.Upgrade.ContinuousTraining => "support.upgrade.training.note",
            _ => "support.upgrade.agents.note"
        };

        /// <summary>
        /// Who is on the desk, what they can get through, and the two ways to add somebody.
        ///
        /// **Both buttons lead into the hiring the game already has.** One opens it with the job
        /// chosen, the other pays an agency premium to skip the conversation entirely, which is the
        /// same trade the furnished office pack makes: convenience costs money rather than replacing
        /// the system it shortcuts.
        /// </summary>
        private VisualElement BuildStaffing(SupportDesk desk, double people)
        {
            var block = new VisualElement();
            block.AddToClassList("support__staffing");

            var line = new Label(Loc.T("support.desk.on_the_desk",
                UiFormat.Number(people, 1),
                UiFormat.Hours(desk.CapacityHoursPerDay(people)),
                UiFormat.Hours(simulation.SupportArrivingHoursPerDay())));

            line.AddToClassList("support__staffline");
            block.Add(line);

            var buttons = new VisualElement();
            buttons.AddToClassList("support__buttons");

            var hire = new Button(() => openTeam?.Invoke()) { text = Loc.T("support.hire") };
            hire.AddToClassList("support__button");
            buttons.Add(hire);

            var remote = new Button(() =>
            {
                if (!simulation.TryHireSupportRemotely(out var why))
                {
                    failed?.Invoke(why);
                    return;
                }

                changed?.Invoke();
            })
            {
                text = Loc.T("support.hire_remote",
                    UiFormat.Money((long)Math.Round(simulation.RemoteSupportDailyUsd())))
            };

            remote.AddToClassList("support__button");
            remote.AddToClassList("support__button--remote");
            buttons.Add(remote);

            block.Add(buttons);

            var note = new Label(Loc.T("support.hire_remote_note"));
            note.AddToClassList("support__note");
            block.Add(note);

            return block;
        }

        /// <summary>
        /// The agents, and what they are eating. **The fleet cost is printed beside the control that
        /// changes it**, because an automation whose bill is on another screen reads as free.
        /// </summary>
        private VisualElement BuildAgents(SupportDesk desk)
        {
            var block = new VisualElement();
            block.AddToClassList("support__agents");

            var head = new Label(Loc.T("support.agents.working",
                desk.AgentsWorking.ToString(), desk.AgentSlots.ToString()));

            head.AddToClassList("support__agentline");
            block.Add(head);

            var row = new VisualElement();
            row.AddToClassList("support__agentrow");

            var fewer = new Button(() =>
            {
                simulation.SetSupportAgents(desk.AgentsWorking - 1);
                changed?.Invoke();
            })
            {
                text = "-"
            };

            fewer.AddToClassList("support__step");
            fewer.SetEnabled(desk.AgentsWorking > 0);
            row.Add(fewer);

            var more = new Button(() =>
            {
                simulation.SetSupportAgents(desk.AgentsWorking + 1);
                changed?.Invoke();
            })
            {
                text = "+"
            };

            more.AddToClassList("support__step");
            more.SetEnabled(desk.AgentsWorking < desk.AgentSlots);
            row.Add(more);

            block.Add(row);

            var cost = new Label(Loc.T("support.agents.fleet",
                UiFormat.Count(desk.UsersOwedToAgents)));

            cost.AddToClassList("support__note");
            block.Add(cost);

            return block;
        }

        private VisualElement BuildLadders(SupportDesk desk)
        {
            var grid = new VisualElement();
            grid.AddToClassList("support__ladders");

            grid.Add(Ladder(desk, SupportCatalog.Upgrade.Deflection));
            grid.Add(Ladder(desk, SupportCatalog.Upgrade.ContinuousTraining));
            grid.Add(Ladder(desk, SupportCatalog.Upgrade.Agents));

            return grid;
        }

        /// <summary>
        /// One ladder: what it is, what it is doing now, and what the next rung costs.
        ///
        /// The current effect is printed as a per cent because that is how the author asked for it
        /// to read, and the top of a ladder says so plainly rather than offering a button that
        /// refuses every click, which this project has shipped twice and caught twice.
        /// </summary>
        private VisualElement Ladder(SupportDesk desk, SupportCatalog.Upgrade upgrade)
        {
            var level = desk.LevelOf(upgrade);
            var most = SupportCatalog.MostLevelsOf(upgrade);
            var maxed = level >= most;

            var card = new VisualElement();
            card.AddToClassList("support__ladder");

            var name = new Label(Loc.T(TitleKeyOf(upgrade)));
            name.AddToClassList("support__laddername");
            card.Add(name);

            var body = new Label(Loc.T(NoteKeyOf(upgrade)));
            body.AddToClassList("support__ladderbody");
            card.Add(body);

            var now = new Label(EffectText(upgrade, level));
            now.AddToClassList("support__laddernow");
            card.Add(now);

            var steps = new Label(Loc.T("support.upgrade.level", level.ToString(), most.ToString()));
            steps.AddToClassList("support__ladderlevel");
            card.Add(steps);

            if (maxed)
            {
                var done = new Label(Loc.T("support.at_the_top"));
                done.AddToClassList("support__ladderdone");
                card.Add(done);
                return card;
            }

            var points = SupportCatalog.PointsFor(upgrade, level + 1);
            var cash = SupportCatalog.CashFor(upgrade, level + 1);

            var buy = new Button(() =>
            {
                if (!simulation.TryBuySupportUpgrade(upgrade, out var why))
                {
                    failed?.Invoke(why);
                    return;
                }

                changed?.Invoke();
            })
            {
                text = Loc.T("support.upgrade.buy",
                    UiFormat.Points(points), UiFormat.Money(cash))
            };

            buy.AddToClassList("support__button");
            buy.AddToClassList("support__ladderbuy");
            card.Add(buy);

            var next = new Label(Loc.T("support.upgrade.next", EffectText(upgrade, level + 1)));
            next.AddToClassList("support__laddernext");
            card.Add(next);

            return card;
        }

        /// <summary>
        /// One seat or several, written as a person would say it.
        ///
        /// **Two keys rather than a count beside a plural**, because the first rung of the agent
        /// ladder opens exactly one seat and "1 seats" is the line the player reads on the day they
        /// are deciding whether to buy it. Polish needs the pair anyway: the plural form there is not
        /// the singular with a letter on the end.
        /// </summary>
        private static string Seats(int seats) =>
            Loc.T(seats == 1 ? "support.agents.seat" : "support.agents.seats", seats.ToString());

        private static string EffectText(SupportCatalog.Upgrade upgrade, int level) => upgrade switch
        {
            SupportCatalog.Upgrade.Deflection =>
                UiFormat.Percent(SupportCatalog.DeflectionAt(level)),
            SupportCatalog.Upgrade.ContinuousTraining =>
                UiFormat.Percent(SupportCatalog.TrainingAt(level)),
            _ => Seats(SupportCatalog.AgentSlotsAt(level))
        };
    }
}
