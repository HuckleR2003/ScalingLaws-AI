namespace ScalingLaws.Data
{
    /// <summary>Which kind of trouble a person is writing in about.</summary>
    public enum TicketClass
    {
        /// <summary>A password, a bill, a question the guide answers. Most of the post.</summary>
        Low = 0,

        /// <summary>Something is behaving oddly for one customer and somebody has to look.</summary>
        Medium = 1,

        /// <summary>Data, money or an outage. It goes to a person the day it arrives.</summary>
        High = 2
    }

    /// <summary>
    /// What a support desk is made of: how much post arrives, how long each kind takes, and the
    /// three things research can buy to make it smaller, faster or partly automatic.
    ///
    /// **Pure numbers and lookups. No rules.** `SupportDesk` owns the arithmetic; this file owns the
    /// table, so a balance change is one edit here rather than a hunt through the simulation.
    ///
    /// The shape of the whole system, in one line: **being used is what generates the post.** More
    /// people served means more tickets, which means more staff, which is what makes growth cost
    /// something. That is the spine of this game applied to customer service rather than hardware.
    /// </summary>
    public static class SupportCatalog
    {
        /// <summary>
        /// Tickets a day per square root of the people being served.
        ///
        /// **Sublinear, and this is the load-bearing decision of the whole system.** A linear rate
        /// is what everybody writes first and it is wrong: at one ticket per fifty thousand people
        /// a company serving thirty million would need forty full-time staff, which is twice the
        /// desks in the largest office in the game, so the mechanic would stop being a decision and
        /// become a tax nobody can pay. It is also untrue. Most of what a bigger audience writes in
        /// about is the same question the last million asked, so the tenth million costs far less to
        /// answer than the first.
        ///
        /// Calibrated against the office ladder, because that is what caps a desk: three million
        /// people served is about twenty one tickets a day and three people; thirty million is
        /// sixty six and about ten, which is the small floor full; a hundred million is a hundred
        /// and twenty and about nineteen, which is the big floor with agents helping.
        /// </summary>
        public const double TicketsPerRootUserPerDay = 0.012;

        /// <summary>How the post splits. Ordinary questions dominate; disasters are rare.</summary>
        public static double ShareOf(TicketClass kind) => kind switch
        {
            TicketClass.Low => 0.70,
            TicketClass.Medium => 0.25,
            _ => 0.05
        };

        /// <summary>
        /// What one ticket costs a person, in hours of their attention.
        ///
        /// A high ticket is eight times a low one, which is why a desk sized on headcount alone is
        /// sized wrong: the same hundred tickets can be a slow afternoon or a lost week.
        /// </summary>
        public static double HoursOf(TicketClass kind) => kind switch
        {
            TicketClass.Low => 0.5,
            TicketClass.Medium => 1.5,
            _ => 4.0
        };

        /// <summary>Hours one person spends on the queue in a working day.</summary>
        public const double HoursPerPersonPerDay = 6.0;

        /// <summary>
        /// An agent is slower than a person and only takes the ordinary post.
        ///
        /// The author's figure, and it has to stay under one: an agent that matched a person would
        /// make the desk a purchase rather than a decision, and hiring would stop being a choice.
        /// </summary>
        public const double AgentSpeed = 0.65;

        /// <summary>
        /// What one working agent takes out of the fleet, expressed as people it stops serving.
        ///
        /// **Derived rather than a constant of its own.** An agent reads and answers all day, so it
        /// costs what serving a crowd costs, and this number travels through the same serving
        /// arithmetic as everybody else. That is what keeps it honest across fourteen years in which
        /// the cost of serving one person falls by orders of magnitude.
        /// </summary>
        public const double AgentUsersEquivalent = 20_000.0;

        /// <summary>Past this the queue reads as abandoned and the product is judged on it.</summary>
        public const double AbandonedHours = 96.0;

        /// <summary>Under this the desk is answering while the problem is still fresh.</summary>
        public const double AnsweredHours = 48.0;

        /// <summary>What a perfect desk adds to how the product is experienced.</summary>
        public const double BestBonus = 0.08;

        /// <summary>
        /// What an abandoned desk costs, and it is two and a half times the bonus on purpose.
        ///
        /// The author's numbers, and they match how this game already treats service: neglect
        /// outweighs excellence, the same way a stale product line outweighs perfect uptime.
        /// </summary>
        public const double WorstPenalty = 0.20;

        /// <summary>The three things research can buy for the desk.</summary>
        public enum Upgrade
        {
            /// <summary>Fewer people need to write in at all.</summary>
            Deflection = 0,

            /// <summary>Every answered ticket makes the next one quicker.</summary>
            ContinuousTraining = 1,

            /// <summary>One more seat an agent can sit in.</summary>
            Agents = 2
        }

        /// <summary>How far each ladder climbs.</summary>
        public static int MostLevelsOf(Upgrade upgrade) => upgrade switch
        {
            Upgrade.Deflection => 3,
            Upgrade.ContinuousTraining => 3,
            _ => 10
        };

        /// <summary>
        /// The share of the post that never arrives, by level: none, 0.6%, 1.4%, 2%.
        ///
        /// Small on purpose. Deflection is the least interesting of the three to buy and the one a
        /// player notices last, so it is priced as a rounding error that adds up, not as a fix.
        /// </summary>
        public static double DeflectionAt(int level) => level switch
        {
            <= 0 => 0.0,
            1 => 0.006,
            2 => 0.014,
            _ => 0.020
        };

        /// <summary>How much quicker the desk works, by level: none, 0.5%, 1%, 1.5%.</summary>
        public static double TrainingAt(int level) => level switch
        {
            <= 0 => 0.0,
            1 => 0.005,
            2 => 0.010,
            _ => 0.015
        };

        /// <summary>Seats for agents: one per level, ten at the top.</summary>
        public static int AgentSlotsAt(int level) => level <= 0 ? 0 : level >= 10 ? 10 : level;

        /// <summary>
        /// Research points for the next level of a ladder.
        ///
        /// The agents climb steeply because each level is a seat that draws real compute forever,
        /// and a ladder that is cheap at the top would make ten agents the obvious opening move.
        /// </summary>
        public static int PointsFor(Upgrade upgrade, int nextLevel)
        {
            var level = nextLevel < 1 ? 1 : nextLevel;
            return upgrade switch
            {
                Upgrade.Deflection => 40 * level,
                Upgrade.ContinuousTraining => 55 * level,
                _ => 90 * level
            };
        }

        /// <summary>The small bill that comes with the points, in dollars.</summary>
        public static long CashFor(Upgrade upgrade, int nextLevel)
        {
            var level = nextLevel < 1 ? 1 : nextLevel;
            return upgrade switch
            {
                Upgrade.Deflection => 1_000L + 1_000L * level,
                Upgrade.ContinuousTraining => 2_000L + 1_000L * level,
                _ => 20_000L + 15_000L * level
            };
        }
    }
}
