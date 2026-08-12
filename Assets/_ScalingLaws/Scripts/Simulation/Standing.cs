using System;
using ScalingLaws.Core;

namespace ScalingLaws.Simulation
{
    /// <summary>
    /// What moved the company's standing today, and by how much.
    ///
    /// Reputation already existed as a single number nudged from five scattered places. It stays one
    /// number, because a second reputation would immediately disagree with the first, but the daily
    /// change is now assembled here from named parts so the player can be told **why** it moved and
    /// so each driver can be tested on its own.
    ///
    /// Every field is a signed daily delta on the 0..1 reputation scale.
    /// </summary>
    public readonly struct StandingChange
    {
        public StandingChange(double service, double freeTier, double modelAge, double price,
            double marketing, double drift)
        {
            Service = SimUnits.Finite(service);
            FreeTier = SimUnits.Finite(freeTier);
            ModelAge = SimUnits.Finite(modelAge);
            Price = SimUnits.Finite(price);
            Marketing = SimUnits.Finite(marketing);
            Drift = SimUnits.Finite(drift);
        }

        /// <summary>Serving people well. The only driver that needs users to exist.</summary>
        public double Service { get; }

        /// <summary>A generous free tier buys goodwill. Small, and it is not free: the tokens cost.</summary>
        public double FreeTier { get; }

        /// <summary>Nothing new for a long time. Negative and it grows.</summary>
        public double ModelAge { get; }

        /// <summary>Cheap is liked, expensive is resented. Signed.</summary>
        public double Price { get; }

        /// <summary>Being seen. Deliberately the weakest positive driver in the list.</summary>
        public double Marketing { get; }

        /// <summary>The pull back toward obscurity that applies to everyone every day.</summary>
        public double Drift { get; }

        public double Total => Service + FreeTier + ModelAge + Price + Marketing + Drift;

        /// <summary>The single largest mover, for a one line explanation in the interface.</summary>
        public string Headline
        {
            get
            {
                var biggest = Math.Abs(Drift);
                var name = "fading quietly";

                Consider(Service, "serving people", ref biggest, ref name);
                Consider(FreeTier, "the free tier", ref biggest, ref name);
                Consider(ModelAge, "an ageing line", ref biggest, ref name);
                Consider(Price, "your prices", ref biggest, ref name);
                Consider(Marketing, "marketing", ref biggest, ref name);

                return name;
            }
        }

        private static void Consider(double value, string label, ref double biggest, ref string name)
        {
            if (Math.Abs(value) > biggest)
            {
                biggest = Math.Abs(value);
                name = label;
            }
        }
    }

    /// <summary>
    /// How the public feels about the company, and how many of them care enough to follow it.
    ///
    /// Two numbers with different jobs. **Reputation** is an opinion: it moves quickly, it is bounded,
    /// and it is what a stranger thinks. **Fans** are a stock: people who stay attached to the brand
    /// between products, they accumulate slowly and they leave slowly. A scandal can halve an opinion
    /// overnight and still leave most of the fans, which is exactly how it works in life and is the
    /// reason these cannot be one number.
    /// </summary>
    public static class Standing
    {
        /// <summary>
        /// The share of its standing a company loses each day to being forgotten.
        ///
        /// A fraction, not a fixed amount, and the difference decides whether the opening of the game
        /// is playable. A flat 0.0006 a day took a new company from its starting five percent to zero
        /// in eighty two days, which is less time than the first model takes to train: the player
        /// arrived at their first release with no standing and no possible way to have kept any.
        /// Nobody can forget a company they never heard of.
        ///
        /// At forty percent standing this costs the same 0.0006 a day the flat rate did, so the
        /// middle of the curve is unchanged and only the ends move.
        /// </summary>
        public const double DailyDriftRate = 0.0015;

        /// <summary>Full marks for serving a large share of the market well.</summary>
        public const double ServiceGain = 0.0012;

        /// <summary>A maximally generous free tier is worth this much a day. Small on purpose.</summary>
        public const double FreeTierGain = 0.00035;

        /// <summary>Marketing at full tilt is worth this much a day. The weakest lever here.</summary>
        public const double MarketingGain = 0.00025;

        /// <summary>Worst case daily loss from a line nobody has refreshed.</summary>
        public const double StaleLoss = 0.0016;

        /// <summary>A line is fresh below this many days and fully stale above the next one.</summary>
        public const int FreshDays = 240;

        public const int StaleDays = 900;

        /// <summary>Strongest daily swing price can produce in either direction.</summary>
        public const double PriceSwing = 0.0006;

        /// <summary>How fast the fan base moves toward the following the company has earned.</summary>
        public const double FanAdoptionPerDay = 0.004;

        /// <summary>Fans lost each day regardless. Slower than reputation, because they are attached.</summary>
        public const double FanDecayPerDay = 0.0012;

        /// <summary>
        /// What today does to the company's standing.
        ///
        /// Nothing here is random. Each driver is something the player chose or failed to choose, and
        /// the sum is small enough that no single day decides anything: standing is the shape of a
        /// year of decisions rather than the result of one.
        /// </summary>
        public static StandingChange Today(double marketShare, double servedBillions,
            double freeTierGenerosity, int daysSinceLastRelease, double priceMultiplier,
            double marketingIntensity, double reputationGainMultiplier,

            // Defaulted to the middle of the range so a caller that only wants to look at one driver
            // does not have to state a standing it is not testing. The simulation always passes the
            // real one.
            double currentReputation = 0.4)
        {
            var service = servedBillions > 0.0
                ? ServiceGain * Math.Clamp(marketShare * 10.0, 0.0, 1.0)
                : 0.0;

            var free = FreeTierGain * Math.Clamp(SimUnits.Finite(freeTierGenerosity), 0.0, 1.0);

            // Nothing new for eight months starts to show, and by two and a half years the company
            // reads as one that stopped trying.
            var staleness = Math.Clamp(
                (daysSinceLastRelease - (double)FreshDays) / (StaleDays - FreshDays), 0.0, 1.0);

            var age = -StaleLoss * staleness;

            // A price at par moves nothing. Half price is liked as much as double is resented.
            var felt = Math.Clamp(SimUnits.Finite(priceMultiplier, 1.0), 0.25, 4.0);
            var price = -PriceSwing * Math.Clamp(Math.Log(felt) / Math.Log(2.0), -1.0, 1.0);

            var marketing = MarketingGain * Math.Clamp(SimUnits.Finite(marketingIntensity), 0.0, 1.0);

            // The founder multiplier lifts what the company earns, never what it loses. A founder who
            // is good with people does not make an ageing product line age more slowly.
            var lift = Math.Max(0.0, SimUnits.Finite(reputationGainMultiplier, 1.0));

            return new StandingChange(service * lift, free * lift, age, price,
                marketing * lift, -DailyDriftRate * Math.Clamp(currentReputation, 0.0, 1.0));
        }

        /// <summary>
        /// The following the company has earned: how many people would pay attention to its next
        /// announcement whether or not they use anything it sells today.
        ///
        /// Reputation squared, because being twice as well regarded is worth far more than twice the
        /// following: obscure companies have no fans at any price, and admired ones have far more
        /// than their user count explains.
        /// </summary>
        public static double FanTarget(double users, double reputation)
        {
            var regard = Math.Clamp(SimUnits.Finite(reputation), 0.0, 1.0);
            return Math.Max(0.0, SimUnits.Finite(users)) * regard * regard;
        }

        /// <summary>Moves the fan base one day toward what the company has earned.</summary>
        public static double AdvanceFans(double fans, double target)
        {
            var held = Math.Max(0.0, SimUnits.Finite(fans));
            var wanted = Math.Max(0.0, SimUnits.Finite(target));

            // Gaining is a courtship and losing is a slow drift away. Fans arrive faster than they
            // leave, which is what makes them a stock worth building rather than a second user count.
            var moved = wanted > held
                ? held + (wanted - held) * FanAdoptionPerDay
                : held + (wanted - held) * FanDecayPerDay;

            return Math.Max(0.0, moved);
        }
    }
}
