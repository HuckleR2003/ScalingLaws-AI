using System;
using ScalingLaws.Core;
using ScalingLaws.Data;

namespace ScalingLaws.Simulation
{
    /// <summary>
    /// The post, and how long people wait for an answer.
    ///
    /// **The queue is carried as hours of work owed, not as a list of tickets.** Three numbers
    /// instead of thousands of objects, and they say the same thing: how long the desk is behind.
    /// The average wait falls straight out of that by the oldest rule in queueing, backlog divided
    /// by throughput, which is also the honest reading. A desk two days behind answers in about two
    /// days, whatever it writes on its status page.
    ///
    /// That also decides the save: a list of tickets would be a list to migrate forever, and this
    /// is three doubles and three levels.
    ///
    /// **Nothing here reads UnityEngine and nothing here rolls dice.** Arrivals are demand divided
    /// by a constant, so a campaign replays identically and a balance question can be answered in
    /// milliseconds rather than by clicking.
    /// </summary>
    public sealed class SupportDesk
    {
        private readonly double[] backlogHours = new double[3];

        /// <summary>Levels bought on each of the three ladders.</summary>
        public int DeflectionLevel { get; private set; }

        /// <summary>See <see cref="DeflectionLevel"/>.</summary>
        public int TrainingLevel { get; private set; }

        /// <summary>See <see cref="DeflectionLevel"/>.</summary>
        public int AgentLevel { get; private set; }

        /// <summary>How many of the unlocked seats the player has switched on.</summary>
        public int AgentsWorking { get; private set; }

        /// <summary>
        /// The average wait the market is judging, in hours: today's queue smoothed over a month.
        ///
        /// **Causal, so it is saved.** Tomorrow's market reads it, and rebuilding it from a backlog
        /// would forget every day that came before, which is the whole point of an average.
        /// </summary>
        public double JudgedHours { get; private set; }

        /// <summary>Hours of work owed on one class of ticket.</summary>
        public double BacklogHoursOf(TicketClass kind) => backlogHours[(int)kind];

        /// <summary>Everything owed, in hours.</summary>
        public double BacklogHours => backlogHours[0] + backlogHours[1] + backlogHours[2];

        /// <summary>Seats unlocked so far.</summary>
        public int AgentSlots => SupportCatalog.AgentSlotsAt(AgentLevel);

        /// <summary>
        /// What one working agent takes out of the fleet, in people it stops serving. Zero when
        /// nothing is switched on, so a company that never opens a desk pays nothing for it.
        /// </summary>
        public double UsersOwedToAgents => AgentsWorking * SupportCatalog.AgentUsersEquivalent;

        /// <summary>
        /// Hours of work the desk can get through in a day.
        ///
        /// People take anything; agents take the ordinary post only, and slowly. The split matters
        /// because a desk of ten agents and nobody at all still cannot answer a single outage.
        /// </summary>
        public double CapacityHoursPerDay(double supportPeople)
        {
            var people = Math.Max(0.0, supportPeople) * SupportCatalog.HoursPerPersonPerDay;
            var agents = AgentsWorking * SupportCatalog.HoursPerPersonPerDay * SupportCatalog.AgentSpeed;
            return people + agents;
        }

        /// <summary>How much of a day's capacity agents may spend, being only good for one class.</summary>
        public double AgentHoursPerDay =>
            AgentsWorking * SupportCatalog.HoursPerPersonPerDay * SupportCatalog.AgentSpeed;

        /// <summary>
        /// Tickets arriving today, before deflection, from the people actually being served.
        ///
        /// **Served, never held, and sublinear in the audience.** A company whose market share says
        /// five million and whose cluster
        /// serves nobody has no post, because nobody got far enough to have a problem.
        /// </summary>
        public double TicketsPerDay(double servedUsers)
        {
            var people = Math.Max(0.0, SimUnits.Finite(servedUsers));
            var raw = Math.Sqrt(people) * SupportCatalog.TicketsPerRootUserPerDay;
            return raw * (1.0 - SupportCatalog.DeflectionAt(DeflectionLevel));
        }

        /// <summary>
        /// One day: the post arrives, the desk works through what it can, and what is left waits.
        ///
        /// High tickets are served first and low last, which is what a desk does and what makes the
        /// class split matter: an overwhelmed desk answers the outage and leaves the password.
        /// </summary>
        public void Advance(double servedUsers, double supportPeople)
        {
            var tickets = TicketsPerDay(servedUsers);
            var quicker = 1.0 - SupportCatalog.TrainingAt(TrainingLevel);

            for (var kind = 0; kind < 3; kind++)
            {
                var arriving = tickets * SupportCatalog.ShareOf((TicketClass)kind);
                backlogHours[kind] += arriving * SupportCatalog.HoursOf((TicketClass)kind) * quicker;
            }

            var agentHours = AgentHoursPerDay;
            var peopleHours = Math.Max(0.0, supportPeople) * SupportCatalog.HoursPerPersonPerDay;

            // People first, worst trouble first.
            for (var kind = 2; kind >= 0 && peopleHours > 0.0; kind--)
            {
                var spent = Math.Min(peopleHours, backlogHours[kind]);
                backlogHours[kind] -= spent;
                peopleHours -= spent;
            }

            // Then the agents, on the ordinary post and nothing else.
            var lowSpent = Math.Min(agentHours, backlogHours[(int)TicketClass.Low]);
            backlogHours[(int)TicketClass.Low] -= lowSpent;

            // **What nobody answers inside the judgement window is written off, not carried.**
            // A person who waited a month for a password does not wait a second month; they have
            // already left, and the desk's penalty is how that shows up. Without this the queue is
            // a debt that compounds forever: a year of neglect took half a year of full staffing to
            // work off, so a player who fixed the problem saw nothing change for months, which is
            // not a decision, it is a punishment with a delay on it.
            for (var kind = 0; kind < 3; kind++)
            {
                var monthOfPost = tickets * SupportCatalog.ShareOf((TicketClass)kind)
                    * SupportCatalog.HoursOf((TicketClass)kind) * quicker * SupportCatalog.JudgementDays;

                backlogHours[kind] = Math.Min(backlogHours[kind], monthOfPost);
            }

            for (var kind = 0; kind < 3; kind++)
            {
                backlogHours[kind] = Math.Max(0.0, SimUnits.Finite(backlogHours[kind]));
            }

            // **What the market is told is the month, not the day.** The instantaneous queue is the
            // honest reading of today; a customer forms an opinion out of weeks of it.
            var today = AverageHours(supportPeople);
            JudgedHours += (today - JudgedHours) / SupportCatalog.JudgementDays;
            JudgedHours = Math.Clamp(SimUnits.Finite(JudgedHours), 0.0, SupportCatalog.AbandonedHours);
        }

        /// <summary>
        /// How long a ticket arriving today waits, in hours.
        ///
        /// Backlog over throughput, in days, turned into hours. A desk with no capacity at all and
        /// any post is <see cref="SupportCatalog.AbandonedHours"/> by definition rather than
        /// infinity: the player needs a number on a screen, not a division by zero.
        /// </summary>
        public double AverageHours(double supportPeople)
        {
            var capacity = CapacityHoursPerDay(supportPeople);
            if (BacklogHours <= 0.0)
            {
                return 0.0;
            }

            if (capacity <= 0.0)
            {
                return SupportCatalog.AbandonedHours;
            }

            return Math.Min(SupportCatalog.AbandonedHours, BacklogHours / capacity * 24.0);
        }

        /// <summary>
        /// Nothing to answer is not a failing desk. One while the month's average sits inside
        /// <see cref="SupportCatalog.AnsweredHours"/>, zero past
        /// <see cref="SupportCatalog.AbandonedHours"/>, straight line between.
        ///
        /// **Reads the month rather than today**, which is why it needs no staff count: who is on
        /// the desk was already accounted for on every one of those days.
        /// </summary>
        public double Quality()
        {
            if (JudgedHours <= SupportCatalog.AnsweredHours)
            {
                return 1.0;
            }

            var span = SupportCatalog.AbandonedHours - SupportCatalog.AnsweredHours;
            return Math.Clamp((SupportCatalog.AbandonedHours - JudgedHours) / span, 0.0, 1.0);
        }

        /// <summary>
        /// What the desk does to the experience of using the product: 0.80 at its worst, 1.08 at its
        /// best. **It multiplies the same reliability the market already reads**, rather than being
        /// a second opinion about how people feel, because there is one such number in this game and
        /// two would disagree by the end of the first campaign.
        /// </summary>
        public double ServiceMultiplier()
        {
            var quality = Quality();
            return 1.0 - SupportCatalog.WorstPenalty
                + quality * (SupportCatalog.WorstPenalty + SupportCatalog.BestBonus);
        }

        /// <summary>Buys one level. The caller charges; this only records what was bought.</summary>
        public void RecordUpgrade(SupportCatalog.Upgrade upgrade)
        {
            switch (upgrade)
            {
                case SupportCatalog.Upgrade.Deflection:
                    DeflectionLevel = Math.Min(DeflectionLevel + 1,
                        SupportCatalog.MostLevelsOf(upgrade));
                    break;
                case SupportCatalog.Upgrade.ContinuousTraining:
                    TrainingLevel = Math.Min(TrainingLevel + 1,
                        SupportCatalog.MostLevelsOf(upgrade));
                    break;
                default:
                    AgentLevel = Math.Min(AgentLevel + 1, SupportCatalog.MostLevelsOf(upgrade));
                    break;
            }
        }

        /// <summary>The level a ladder is on, for the screen and for pricing the next one.</summary>
        public int LevelOf(SupportCatalog.Upgrade upgrade) => upgrade switch
        {
            SupportCatalog.Upgrade.Deflection => DeflectionLevel,
            SupportCatalog.Upgrade.ContinuousTraining => TrainingLevel,
            _ => AgentLevel
        };

        /// <summary>
        /// Switches agents on or off, clamped to the seats that exist. Turning one off frees its
        /// compute the same day, because that is what a player turning it off is asking for.
        /// </summary>
        public void SetAgentsWorking(int agents) =>
            AgentsWorking = Math.Clamp(agents, 0, AgentSlots);

        /// <summary>Restores a saved desk. Levels first: the seats decide the working count.</summary>
        public void Restore(double low, double medium, double high, double judgedHours,
            int deflection, int training, int agentLevel, int working)
        {
            backlogHours[0] = Math.Max(0.0, SimUnits.Finite(low));
            backlogHours[1] = Math.Max(0.0, SimUnits.Finite(medium));
            backlogHours[2] = Math.Max(0.0, SimUnits.Finite(high));
            JudgedHours = Math.Clamp(SimUnits.Finite(judgedHours), 0.0, SupportCatalog.AbandonedHours);

            DeflectionLevel = Math.Clamp(deflection, 0,
                SupportCatalog.MostLevelsOf(SupportCatalog.Upgrade.Deflection));
            TrainingLevel = Math.Clamp(training, 0,
                SupportCatalog.MostLevelsOf(SupportCatalog.Upgrade.ContinuousTraining));
            AgentLevel = Math.Clamp(agentLevel, 0,
                SupportCatalog.MostLevelsOf(SupportCatalog.Upgrade.Agents));

            SetAgentsWorking(working);
        }
    }
}
