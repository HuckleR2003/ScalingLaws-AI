using System;
using System.Collections.Generic;
using ScalingLaws.Data;

namespace ScalingLaws.Simulation
{
    /// <summary>
    /// What one person expected when they took the job, and whether they got it.
    ///
    /// **Derived from the hire, never stored.** A person's expectations are decided the moment they
    /// exist and never change, so they are a pure function of who they are: their name and the day
    /// they started. That means no save version, no migration, identical replay, and no field
    /// anybody has to remember to write when a hire is created through a route somebody adds later.
    /// Same rule <see cref="RivalExpansion"/> and <see cref="LabTraits"/> follow.
    ///
    /// **They are drawn from the benefits the company can already offer**, not from a new list.
    /// `BenefitCatalog` has had six of them since it was written, they already cost money per head
    /// and already buy loyalty. Inventing a parallel set of things people want would mean two
    /// tables that could disagree about what a gym card is.
    ///
    /// The asymmetry is the design. An unmet expectation is a small, permanent drag; a met one is
    /// worth more than the benefit alone, because it is the difference between a perk somebody
    /// receives and one they asked for. That is what makes reading this panel worth the click:
    /// the same money spent on the same benefit is worth more on some people than on others.
    /// </summary>
    public static class StaffExpectations
    {
        /// <summary>
        /// Share of people who want nothing in particular.
        ///
        /// **Most of them.** If everybody arrived with a list, the benefits screen would stop being
        /// a decision and become a checklist to complete, and the panel would say the same thing
        /// about every person in the company.
        /// </summary>
        public const double ShareWithNoExpectations = 0.45;

        /// <summary>And of the rest, how many want a second thing as well.</summary>
        public const double ShareWantingTwo = 0.30;

        /// <summary>What one unmet expectation costs, in the same points benefits are worth.</summary>
        public const double UnmetPenaltyPoints = 2.2;

        /// <summary>
        /// How much faster loyalty climbs for somebody whose expectations are met.
        ///
        /// A quarter, as asked. Applied to the tenure term rather than to the total, because that
        /// is the part that grows with time: a person who is getting what they asked for settles
        /// in faster, they do not start out more attached.
        /// </summary>
        public const double MetTenureBonus = 0.25;

        /// <summary>
        /// What this person asked for. Empty for most of them.
        ///
        /// Ordered, so the panel lists them the same way every repaint.
        /// </summary>
        public static List<StaffBenefit> For(in Hire hire)
        {
            var wanted = new List<StaffBenefit>(2);
            var seed = SeedFor(hire);

            if (Unit(seed, 1) < ShareWithNoExpectations)
            {
                return wanted;
            }

            var options = BenefitCatalog.All;
            var first = (int)(Unit(seed, 2) * options.Count) % options.Count;
            wanted.Add(options[first].Benefit);

            if (Unit(seed, 3) < ShareWantingTwo)
            {
                var second = (int)(Unit(seed, 4) * options.Count) % options.Count;

                if (options[second].Benefit != wanted[0])
                {
                    wanted.Add(options[second].Benefit);
                }
            }

            return wanted;
        }

        /// <summary>How many of this person's expectations the company is currently meeting.</summary>
        public static int MetCount(in Hire hire, IReadOnlyCollection<StaffBenefit> offered)
        {
            if (offered == null)
            {
                return 0;
            }

            var met = 0;

            foreach (var wanted in For(hire))
            {
                if (Offers(offered, wanted))
                {
                    met++;
                }
            }

            return met;
        }

        /// <summary>
        /// Whether a set contains a benefit, written out rather than using `Contains`.
        ///
        /// `IReadOnlyCollection` has no `Contains`, and the one the compiler reaches for instead is
        /// the span extension for strings, which produces an error about a missing
        /// `comparisonType`. Six items, iterated.
        /// </summary>
        private static bool Offers(IReadOnlyCollection<StaffBenefit> offered, StaffBenefit wanted)
        {
            foreach (var benefit in offered)
            {
                if (benefit == wanted)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>And how many it is not.</summary>
        public static int UnmetCount(in Hire hire, IReadOnlyCollection<StaffBenefit> offered) =>
            For(hire).Count - MetCount(hire, offered);

        /// <summary>
        /// True when this person asked for something and is getting all of it.
        ///
        /// Somebody who asked for nothing is not "satisfied" by this rule and does not get the
        /// bonus: the bonus is for meeting a request, and there was none to meet.
        /// </summary>
        public static bool IsLookedAfter(in Hire hire, IReadOnlyCollection<StaffBenefit> offered)
        {
            var wanted = For(hire);
            return wanted.Count > 0 && MetCount(hire, offered) == wanted.Count;
        }

        /// <summary>
        /// The loyalty points this person's expectations add or take away, on top of what the
        /// benefits themselves are worth to everybody.
        /// </summary>
        public static double PointsFor(in Hire hire, IReadOnlyCollection<StaffBenefit> offered) =>
            -UnmetPenaltyPoints * UnmetCount(hire, offered);

        /// <summary>
        /// A stable number for one person.
        ///
        /// **Not `string.GetHashCode`.** It is randomised per process on modern runtimes, so the
        /// same person would want different things on every launch and a save reloaded twice would
        /// disagree with itself. This project has been caught by that once already, on the company
        /// mark's colour.
        /// </summary>
        private static uint SeedFor(in Hire hire)
        {
            unchecked
            {
                var seed = 2166136261u;

                foreach (var character in hire.Name ?? string.Empty)
                {
                    seed = (seed ^ character) * 16777619u;
                }

                seed ^= (uint)hire.StartedOn.DayIndex * 2654435761u;
                seed ^= (uint)hire.Position * 40503u;

                return seed;
            }
        }

        /// <summary>One well-mixed value in [0,1) from a seed and a channel.</summary>
        private static double Unit(uint seed, uint channel)
        {
            unchecked
            {
                var value = seed ^ (channel * 2246822519u);

                value ^= value >> 13;
                value *= 2654435761u;
                value ^= value >> 16;

                return (value % 100000u) / 100000.0;
            }
        }
    }
}
