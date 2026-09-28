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

        /// <summary>
        /// The whole tab: what the desk is achieving on the left, what is still owed on the right.
        ///
        /// **It was a strip buried under the standing panel and it is a tab of its own now**,
        /// because the author asked for it and because a desk three panels down the management page
        /// is a desk nobody opens until the audience has already gone. The satisfaction figure is in
        /// the name of the tab for the same reason: it is the one number here that can be read
        /// without opening anything.
        ///
        /// The shape is borrowed on purpose from a support page in a game the author liked: a narrow
        /// column of what is true, a wide table of what is outstanding, a grey bar titling each, and
        /// no chrome in between.
        ///
        /// **What is deliberately not borrowed is the list of individual letters.** This game carries
        /// the queue as hours of work owed rather than as thousands of objects, which is a decision
        /// recorded in <see cref="SupportDesk"/> and the reason the desk costs nothing to simulate.
        /// A table of ticket numbers with names beside them would therefore be invented, and the
        /// honesty flag covers a support screen exactly as it covers a hardware specification. Every
        /// row here is a class of post the simulation genuinely tracks and every figure on it is
        /// read from the desk.
        /// </summary>
        public VisualElement Build()
        {
            var desk = simulation.State.Support;
            var people = simulation.SupportPeople();

            var panel = new VisualElement();
            panel.AddToClassList("sup");

            var top = new VisualElement();
            top.AddToClassList("sup__top");

            top.Add(BuildStats(desk, people));
            top.Add(BuildTickets(desk, people));

            panel.Add(top);

            if (desk.AgentSlots > 0)
            {
                panel.Add(BuildAgents(desk));
            }

            // **The shop is last and that is a design decision, not a layout one.** Hiring is the
            // cheap answer to a backlog and research is the expensive one, and a panel that opens
            // on the ladders teaches the wrong lesson about a queue.
            panel.Add(BuildLadders(desk));

            return panel;
        }

        /// <summary>
        /// The left column: how the desk is doing, what that is worth, and the figures behind it.
        ///
        /// **The face is beside the percentage and the sentence is under both**, in that order.
        /// The question a player arrives with is "is this all right", and a bare 72% cannot answer
        /// it: there is no scale printed anywhere near it. "This is costing the product eleven per
        /// cent of how it is experienced" cannot be misread.
        /// </summary>
        private VisualElement BuildStats(SupportDesk desk, double people)
        {
            var column = new VisualElement();
            column.AddToClassList("sup__stats");

            column.Add(Heading(Loc.T("sup.stats")));

            var body = new VisualElement();
            body.AddToClassList("sup__statsbody");

            var quality = desk.Quality();

            var satisfaction = new VisualElement();
            satisfaction.AddToClassList("sup__satis");

            var face = new SupportFace(quality);
            satisfaction.Add(face);

            var figure = new Label(UiFormat.Percent(quality));
            figure.AddToClassList("sup__big");
            figure.style.color = face.Tone;
            satisfaction.Add(figure);

            body.Add(satisfaction);

            var caption = new VisualElement();
            caption.AddToClassList("sup__satisrow");

            var word = new Label(Loc.T("sup.satisfaction"));
            word.AddToClassList("sup__capbold");
            caption.Add(word);
            var note = TechNotes.SupportDesk;

            caption.Add(InsightTip.InfoBadge(note.Title,
                new InsightTip.Reading(note.What, note.Affects, note.High, note.Low)));
            body.Add(caption);

            var effect = desk.ServiceMultiplier() - 1.0;

            var verdict = new Label(effect >= 0.0
                ? Loc.T("support.desk.helping", UiFormat.Percent(effect))
                : Loc.T("support.desk.costing", UiFormat.Percent(-effect)));

            verdict.AddToClassList("sup__verdict");
            verdict.EnableInClassList("sup__verdict--bad", effect < 0.0);
            body.Add(verdict);

            body.Add(Cell(UiFormat.Hours(desk.JudgedHours), Loc.T("sup.average_wait")));
            body.Add(Cell(UiFormat.Count(desk.TicketsResolved), Loc.T("sup.resolved")));
            body.Add(Cell(
                UiFormat.Count(desk.TicketsPerDay(simulation.UsersServedToday())),
                Loc.T("sup.arriving")));

            var actions = new VisualElement();
            actions.AddToClassList("sup__acts");

            var hire = new Button(() => openTeam?.Invoke()) { text = Loc.T("support.hire") };
            hire.AddToClassList("sup__button");
            actions.Add(hire);

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

            // **Quieter than the plain hire, never louder.** It is the same person with a third
            // added to the wage, and an expensive convenience drawn as the bright button on a row
            // gets pressed by reflex.
            remote.AddToClassList("sup__button");
            remote.AddToClassList("sup__button--remote");
            actions.Add(remote);

            body.Add(actions);

            var who = new Label(Loc.T("support.desk.on_the_desk",
                UiFormat.Number(people, 1),
                UiFormat.Hours(desk.CapacityHoursPerDay(people)),
                UiFormat.Hours(simulation.SupportArrivingHoursPerDay())));

            who.AddToClassList("sup__note");
            body.Add(who);

            column.Add(body);

            return column;
        }

        /// <summary>
        /// The right column: what is still owed, worst first.
        ///
        /// Three rows rather than a thousand, and the reason is written in <see cref="Build"/>.
        /// What each row carries is what somebody looking at a queue wants to know: how many are in
        /// it, how long the one at the front has been there, and whether anybody is reaching it at
        /// all.
        /// </summary>
        private VisualElement BuildTickets(SupportDesk desk, double people)
        {
            var column = new VisualElement();
            column.AddToClassList("sup__queue");

            column.Add(Heading(Loc.T("sup.unresolved")));

            var body = new VisualElement();
            body.AddToClassList("sup__queuebody");

            if (desk.BacklogHours <= 0.0)
            {
                // **An empty desk says so rather than drawing an empty table.** A header row over
                // nothing reads as a screen that failed to load the rest of itself.
                var clear = new Label(Loc.T("sup.nothing_waiting"));
                clear.AddToClassList("sup__clear");
                body.Add(clear);
                column.Add(body);
                return column;
            }

            var head = new VisualElement();
            head.AddToClassList("sup__row");
            head.AddToClassList("sup__row--head");

            head.Add(Field(Loc.T("sup.col.class"), "sup__c1"));
            head.Add(Field(Loc.T("sup.col.waiting"), "sup__c2"));
            head.Add(Field(Loc.T("sup.col.oldest"), "sup__c3"));
            head.Add(Field(Loc.T("sup.col.worked"), "sup__c4"));

            body.Add(head);

            foreach (var kind in new[] { TicketClass.High, TicketClass.Medium, TicketClass.Low })
            {
                body.Add(TicketRow(desk, kind, people));
            }

            column.Add(body);

            return column;
        }

        private VisualElement TicketRow(SupportDesk desk, TicketClass kind, double people)
        {
            var row = new VisualElement();
            row.AddToClassList("sup__row");
            row.AddToClassList(RowClassOf(kind));

            var name = Field(Loc.T(NameKeyOf(kind)), "sup__c1");
            name.AddToClassList("sup__classname");
            row.Add(name);

            row.Add(Field(
                Loc.T("sup.letters", UiFormat.Count(desk.TicketsWaitingOf(kind))), "sup__c2"));

            // **At the ceiling this is not a wait, and printing the ceiling says nothing.**
            // `AbandonedHours` is where the simulation stops counting, because somebody who has
            // waited a month for a password has already left rather than waited a second month. A
            // desk far enough behind puts every class on that number, and the first render came
            // back with the same figure three times, which reads as a column that is broken rather
            // than as a company in trouble. The word is the honest reading and it is a different
            // statement from a duration.
            var waited = desk.WaitHoursOf(kind, people);
            var lost = waited >= SupportCatalog.AbandonedHours;

            var oldest = Field(lost ? Loc.T("sup.abandoned") : UiFormat.Hours(waited), "sup__c3");
            oldest.EnableInClassList("sup__late", waited >= SupportCatalog.AnsweredHours);
            row.Add(oldest);

            row.Add(Field(WorkedBy(desk, kind, people), "sup__c4"));

            return row;
        }

        /// <summary>
        /// Who actually reaches this class, which is not the same as who is employed.
        ///
        /// **Agents answer the ordinary post and nothing else**, so a company with ten agents and
        /// nobody at all has nobody on its outages. This column is the only place in the game that
        /// says so at the moment the player is looking at the outage.
        /// </summary>
        private static string WorkedBy(SupportDesk desk, TicketClass kind, double people)
        {
            var staff = people > 0.0 ? UiFormat.Number(people, 1) : string.Empty;
            var agents = kind == TicketClass.Low ? desk.AgentsWorking : 0;

            if (staff.Length == 0 && agents == 0)
            {
                return Loc.T("sup.worked.nobody");
            }

            if (agents == 0)
            {
                return Loc.T("sup.worked.people", staff);
            }

            return staff.Length == 0
                ? Loc.T("sup.worked.agents", agents.ToString())
                : Loc.T("sup.worked.both", staff, agents.ToString());
        }

        /// <summary>The grey bar a section is titled with, which is the whole look being borrowed.</summary>
        private static Label Heading(string text)
        {
            var head = new Label(text);
            head.AddToClassList("sup__head");
            return head;
        }

        /// <summary>A figure over its caption, which is the left column's entire vocabulary.</summary>
        private static VisualElement Cell(string figure, string caption)
        {
            var cell = new VisualElement();
            cell.AddToClassList("sup__cell");

            var big = new Label(figure);
            big.AddToClassList("sup__cellfigure");
            cell.Add(big);

            var note = new Label(caption);
            note.AddToClassList("sup__cellnote");
            cell.Add(note);

            return cell;
        }

        /// <summary>
        /// One cell of the table on the right. Named apart from <see cref="Cell"/> on purpose:
        /// they are different shapes in two columns of the same screen, and one name for both is
        /// how a figure ends up drawn in the wrong vocabulary.
        /// </summary>
        private static Label Field(string text, string column)
        {
            var cell = new Label(text);
            cell.AddToClassList("sup__field");
            cell.AddToClassList(column);
            return cell;
        }

        /// <summary>
        /// **Written out, never assembled**, for the reason the other switches in this file give:
        /// `StylesheetTests` reads literals, so a class built by concatenation is invisible to it
        /// and collapses whatever it is on without anything failing.
        /// </summary>
        private static string RowClassOf(TicketClass kind) => kind switch
        {
            TicketClass.Low => "sup__row--low",
            TicketClass.Medium => "sup__row--medium",
            _ => "sup__row--high"
        };

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


        /// <summary>
        /// The three ladders on their own, stacked, for the person card.
        ///
        /// **The same builder the management desk uses, not a second copy.** The author asked for
        /// these in two places, and the trap in that is two panels quoting different levels or
        /// charging different prices for the same rung a month after somebody edits one of them.
        /// Everything above the ladders stays on the desk: a queue, a wait and a hiring button are
        /// facts about the company, and the card they would be drawn on is about one person.
        /// </summary>
        public VisualElement BuildLaddersFor(SupportDesk desk)
        {
            var grid = BuildLadders(desk);
            grid.AddToClassList("support__ladders--stacked");

            return grid;
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
