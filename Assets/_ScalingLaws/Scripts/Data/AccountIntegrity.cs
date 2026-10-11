using System;

namespace ScalingLaws.Data
{
    /// <summary>
    /// How many accounts the company is serving per real human being.
    ///
    /// **Every other number in this game counts people.** `AudienceCatalog` sizes an audience in
    /// human beings, `TokensPerUserPerDay` is what one of them gets through in a day, and the user
    /// figure on every screen is tokens divided by that. So the game has never had a way to say the
    /// thing every real service says: a lot of those accounts are the same person twice.
    ///
    /// The two halves cost the company completely different things and that is the whole mechanic:
    ///
    /// - **A duplicate free account is a bill.** It takes a free allowance, it takes capacity off
    ///   the fleet, it makes the queue longer for everybody, and nobody is ever invoiced for it.
    /// - **A duplicate paid account is revenue.** It is also a lie in the dashboard: the company
    ///   believes it has more customers than it has, and the forecast it plans against is wrong by
    ///   however many duplicates there are.
    ///
    /// Which is why the two levers here are not a weak one and a strong one. Detection finds the
    /// duplicates that are costing money and leaves the ones that are paying. A hard ban catches
    /// both, because linking payment cards does not know which duplicates you were hoping to keep.
    /// </summary>
    public static class AccountIntegrity
    {
        /// <summary>
        /// Free accounts per real person with nothing done about it.
        ///
        /// The excess is what matters rather than the figure: 0.74 of a second account per head.
        /// Plausible against what consumer services report and coarse on purpose, in the same
        /// spirit as the audience curves. It is not a number anybody publishes precisely, because
        /// a company that could measure it exactly would not have the problem.
        /// </summary>
        public const double FreeAccountsPerPerson = 1.74;

        /// <summary>
        /// Paid subscriptions per real person with nothing done about it.
        ///
        /// Far lower than the free figure for the obvious reason: the second one costs money. What
        /// is left is mostly a person with a personal and a work subscription, and card fraud.
        /// </summary>
        public const double PaidAccountsPerPerson = 1.11;

        /// <summary>How far the recognition ladder climbs.</summary>
        public const int MostDetectionLevels = 6;

        /// <summary>
        /// What the finished ladder takes off the duplicate free accounts.
        ///
        /// **Sixty per cent and never more, because the rest of them are not detectable.** A
        /// household behind one address, two people on one campus, a phone handed to a child: a
        /// service that insisted on catching those would be turning away real customers, which is
        /// what the ban below is for and is why it costs what it costs.
        /// </summary>
        public const double MostDetectionCut = 0.60;

        /// <summary>
        /// What the hard ban takes off duplicates, free and paid alike.
        ///
        /// Stronger than the whole ladder, on the author's call, and it has to be: it is bought with
        /// money rather than understanding, so if it were also weaker nobody would ever buy it.
        /// What it costs instead is customers and standing, every single day it is switched on.
        /// </summary>
        public const double BanCut = 0.80;

        /// <summary>
        /// Paid once to the firm that implements it. Switching it off does not refund it and
        /// switching it back on does not charge again: the integration is built either way.
        /// </summary>
        public const long BanCostUsd = 1_400_000L;

        /// <summary>
        /// What the ban costs the company's standing every day it is on.
        ///
        /// **Ongoing rather than once, because it is a standing policy rather than a purchase.**
        /// A quarter of `Standing.DailyDriftRate`, so a company running it has to be earning its
        /// reputation back somewhere else. People do not like being asked to prove who they are,
        /// and a lab that links payment cards to catch them is doing identity work on its own
        /// users, which is a different kind of company from one that does not.
        /// </summary>
        public const double BanReputationDragPerDay = 0.0004;

        /// <summary>
        /// What the ladder has taken off the duplicate free accounts at this level.
        ///
        /// Diminishing, and it lands exactly on <see cref="MostDetectionCut"/> rather than near it,
        /// so the ceiling is a number somebody chose rather than wherever the steps happened to
        /// stop. The first level is the biggest because the easy duplicates are genuinely easy:
        /// one device, one address, six accounts made in an afternoon.
        /// </summary>
        public static double DetectionAt(int level) => level switch
        {
            <= 0 => 0.0,
            1 => 0.14,
            2 => 0.26,
            3 => 0.37,
            4 => 0.46,
            5 => 0.54,
            _ => MostDetectionCut
        };

        /// <summary>
        /// Research points for the next level. Small, and the points are the real gate.
        ///
        /// The whole ladder is 1,050 points and $140,000, against the ban's $1,400,000 and no
        /// research at all. That ratio is the decision: ten times the money for something you did
        /// not have to understand, and a bill in customers afterwards.
        /// </summary>
        public static int PointsFor(int nextLevel) => 50 * Math.Max(1, nextLevel);

        /// <summary>The small bill that comes with the points, in dollars.</summary>
        public static long CashFor(int nextLevel) => 5_000L + 5_000L * Math.Max(1, nextLevel);

        /// <summary>
        /// What the company is actually serving per head, given what it has done about it.
        ///
        /// **The two cuts stack on what is left, never by adding.** That is the rule
        /// `SafetyModules` is held to and it is here for the same reason: added together, a full
        /// ladder and a ban would reach 140% and the arithmetic would start handing back fewer
        /// accounts than there are people. Multiplied, the full ladder and the ban together still
        /// leave 8% of the duplicates in place, and nothing in here ever reaches certainty.
        ///
        /// Detection does not appear in the paid figure at all. It is not an oversight: a
        /// duplicate subscription is paying, so there is nothing for a fraud system to recover and
        /// no reason for one to look.
        /// </summary>
        public static AccountLoad Serving(int detectionLevel, bool banOn)
        {
            var detected = 1.0 - DetectionAt(detectionLevel);
            var banned = banOn ? 1.0 - BanCut : 1.0;

            var freeExcess = (FreeAccountsPerPerson - 1.0) * detected * banned;
            var paidExcess = (PaidAccountsPerPerson - 1.0) * banned;

            return new AccountLoad(1.0 + freeExcess, 1.0 + paidExcess);
        }
    }

    /// <summary>
    /// Accounts per real person, split by whether anybody is invoiced for them.
    ///
    /// A value type with both halves in it rather than two calls, because every reader needs both
    /// and the two are computed from the same two facts. Returning them separately is how the free
    /// figure ends up suppressed in one screen and not in another.
    /// </summary>
    public readonly struct AccountLoad
    {
        public AccountLoad(double free, double paid)
        {
            // Never below one. One real person is at least one account, and a cut that pushed this
            // under one would start inventing people who are not there.
            Free = Math.Clamp(double.IsNaN(free) ? 1.0 : free, 1.0, 8.0);
            Paid = Math.Clamp(double.IsNaN(paid) ? 1.0 : paid, 1.0, 8.0);
        }

        /// <summary>Free accounts per real person. Everything above one is a bill.</summary>
        public double Free { get; }

        /// <summary>Paid subscriptions per real person. Everything above one is paying.</summary>
        public double Paid { get; }

        /// <summary>Duplicate free accounts per head, which is the part that costs money.</summary>
        public double FreeExcess => Free - 1.0;

        /// <summary>Duplicate paid subscriptions per head.</summary>
        public double PaidExcess => Paid - 1.0;

        /// <summary>Nothing is being served twice. True only if both halves are clean.</summary>
        public bool IsClean => FreeExcess <= 0.0 && PaidExcess <= 0.0;
    }
}
