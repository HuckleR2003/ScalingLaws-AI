using System;

namespace ScalingLaws.Simulation
{
    /// <summary>
    /// Research points put into a family programme to shorten its calendar.
    ///
    /// **Points, and deliberately not money.** Money buying time is the one thing the design of this
    /// game refuses: capital that skips a calendar gate removes the pressure the whole economy is
    /// built on, and the architecture screen already sells quality for money through the budget and
    /// length pair. Points are different in kind. They are only ever paid for **finished work**,
    /// never for elapsed time, so spending them here says the lab already understands part of what
    /// it is about to build, which is the one honest reason a programme could run shorter.
    ///
    /// It also gives points a second sink. Until now the only thing they bought was research nodes,
    /// so a company that had cleared the tree it cared about was accruing a currency with nothing to
    /// spend it on.
    ///
    /// **Three rules, each with a test:**
    ///
    /// 1. Nothing bought is exactly nothing off, so a player who never touches the control runs the
    ///    programme they would have run before this existed. The neutral-option rule every other
    ///    control in this game is held to.
    /// 2. It compresses and never skips. <see cref="MostOfTheCalendar"/> is the hard ceiling and no
    ///    amount of points reaches past it.
    /// 3. **It buys calendar and nothing else.** The petaflop-days are untouched, so a programme
    ///    shortened to its floor can still sit waiting on the cluster, which is the honest outcome:
    ///    understanding the problem does not make the machines faster.
    /// </summary>
    public static class PriorWork
    {
        /// <summary>
        /// The most of a programme's calendar that prior understanding can ever take off.
        ///
        /// Thirty per cent is enough to be worth saving for and small enough that the year a family
        /// takes is still a year. A programme is a bet on where the frontier will be when it lands,
        /// and a lever that could halve the wait would turn that bet into a purchase.
        /// </summary>
        public const double MostOfTheCalendar = 0.30;

        /// <summary>
        /// Points for one day off, and it is linear on purpose.
        ///
        /// A rate rather than a price list: a four hundred day programme and a ninety day one are
        /// the same trade per day, so the control means the same thing wherever the length slider
        /// happens to be. A starting value, and the one number to move if this turns out cheap.
        ///
        /// **Whole, and a test is why.** At 1.5 the quote rounded up and the purchase rounded down,
        /// so paying 1.99 points bought a day that cost 1.5 and the card would have printed a
        /// figure half a point away from what left the company. An integer rate makes the quote,
        /// the charge and the slider the same number everywhere.
        /// </summary>
        public const double PointsPerDay = 2.0;

        /// <summary>
        /// The most days that can come off a calendar of this length.
        ///
        /// **Zero for anything already at or under the floor**, which is the case a naive cap gets
        /// wrong: a company whose founder and country have already pulled a programme under sixty
        /// days would otherwise have it *lengthened* by a clamp meant to protect it.
        /// </summary>
        public static int MostDaysOff(int calendarDays)
        {
            if (calendarDays <= ArchitectureBlueprint.MinimumDurationDays)
            {
                return 0;
            }

            var byShare = (int)Math.Floor(calendarDays * MostOfTheCalendar);
            var byFloor = calendarDays - ArchitectureBlueprint.MinimumDurationDays;

            return Math.Max(0, Math.Min(byShare, byFloor));
        }

        /// <summary>Points it would take to buy every day this calendar is allowed to lose.</summary>
        public static double MostPointsWorthSpending(int calendarDays) =>
            PointsFor(MostDaysOff(calendarDays));

        /// <summary>What a whole number of days costs. Rounded up, so a day is never half paid for.</summary>
        public static double PointsFor(int days) =>
            days <= 0 ? 0.0 : Math.Ceiling(days * PointsPerDay);

        /// <summary>
        /// Days off for points spent, held inside what this calendar is allowed to lose.
        ///
        /// Rounded down: the player gets whole days they have paid for in full and never a day they
        /// have not.
        /// </summary>
        public static int DaysOff(int calendarDays, double points)
        {
            if (points <= 0.0 || double.IsNaN(points))
            {
                return 0;
            }

            var bought = (int)Math.Floor(points / PointsPerDay);

            return Math.Clamp(bought, 0, MostDaysOff(calendarDays));
        }

        /// <summary>
        /// What the programme will actually run for once the points are counted.
        ///
        /// One expression, so the screen that quotes it and the rule that commits it cannot answer
        /// differently. This project has had four copies of one heat threshold disagreeing about the
        /// same cabinet, and the architecture calendar was already being computed in two places.
        /// </summary>
        public static int CalendarAfter(int calendarDays, double points) =>
            calendarDays - DaysOff(calendarDays, points);
    }
}
