using System;
using ScalingLaws.Core;
using ScalingLaws.Data;

namespace ScalingLaws.Simulation
{
    /// <summary>What standing at an event did for the company.</summary>
    public readonly struct EventOutcome
    {
        public EventOutcome(double fans, double reputation, bool viral, double viralShare)
        {
            Fans = Math.Max(0.0, SimUnits.Finite(fans));
            Reputation = Math.Max(0.0, SimUnits.Finite(reputation));
            WentViral = viral;
            ViralShare = Math.Clamp(SimUnits.Finite(viralShare), 0.0, 1.0);
        }

        /// <summary>People who left attached to the brand.</summary>
        public double Fans { get; }

        /// <summary>What it did to the regard, which is slower and smaller.</summary>
        public double Reputation { get; }

        /// <summary>Whether the stand was the thing everybody talked about afterwards.</summary>
        public bool WentViral { get; }

        /// <summary>
        /// The chance it had of going viral, kept so the summary can say what the odds were.
        ///
        /// **A result with no odds beside it teaches nothing.** A player who went viral at 6% and
        /// one who went viral at 38% made very different decisions and the screen would otherwise
        /// show them the same word.
        /// </summary>
        public double ViralShare { get; }
    }

    /// <summary>
    /// What a stand is worth, and the one roll in this whole feature.
    ///
    /// **Two figures doing two jobs, which is what keeps them from being one number with two
    /// labels.** The crowd decides how many people leave attached to the company, because being
    /// seen by a lot of people is how a following is built. The address decides how much the
    /// company is thought better of, because being at a serious venue is not the same as being in
    /// front of a crowd. A festival ground builds a following and barely moves the regard; a civic
    /// square does the opposite.
    ///
    /// `Docs/EVENTS_PLAN.md` is the design.
    /// </summary>
    public static class EventStand
    {
        private const uint SeedSalt = 0x53544E44;

        /// <summary>
        /// Fans per visitor at a small stand before the address is weighed in.
        ///
        /// Sized against what a company actually has: a campaign measured over eight releases sat
        /// between three and fourteen thousand fans, so Bayview Park at a large stand is worth a
        /// few thousand. That is meant to be a real decision rather than a rounding error, and it
        /// is also why the ladder costs what it costs.
        /// </summary>
        public const double FansPerVisitor = 0.012;

        /// <summary>
        /// Regard at a small stand at a perfect address.
        ///
        /// `Standing.DailyDrift` is -0.0006 a day, so a large stand at Valley Plaza is worth about
        /// two months of standing simply not decaying. Deliberately on that scale: an event should
        /// be a season's worth of goodwill rather than a replacement for serving people well,
        /// which is the strongest positive driver in the game and has to stay that way.
        /// </summary>
        public const double ReputationAtBestAddress = 0.012;

        /// <summary>The most a stand can ever be talked about, however big the crowd.</summary>
        public const double MostViralChance = 0.40;

        /// <summary>What going viral multiplies the following by.</summary>
        public const double ViralFansMultiple = 2.5;

        /// <summary>And what it adds to the regard, as a share of what the stand already earned.</summary>
        public const double ViralReputationMultiple = 1.8;

        /// <summary>
        /// How likely this stand is to be the one people talk about.
        ///
        /// **Driven by the crowd rather than by the address**, which is the author's own split: a
        /// thing goes around because a lot of people saw it, not because the venue was respectable.
        /// The size of the stand multiplies it, so the large stand buys a better chance as well as
        /// a bigger result, and the cap stops the biggest venue from making it close to certain.
        /// </summary>
        public static double ViralChance(EventVenueId venueId, StandTier tier)
        {
            var venue = EventVenueCatalog.Get(venueId);

            // Measured against the largest crowd in the city, so the park is the reference.
            var crowd = Math.Clamp(venue.AverageVisitors / 85_000.0, 0.0, 1.0);

            return Math.Min(MostViralChance,
                crowd * 0.18 * EventVenueCatalog.EffectMultiple(tier));
        }

        /// <summary>
        /// What a small stand at this venue is worth before the tier multiplies it.
        ///
        /// Exposed so the venue window can say what standing here would do before anything is
        /// paid for, rather than the screen working it out a second time.
        /// </summary>
        public static double FansAt(EventVenueId venueId, StandTier tier)
        {
            var venue = EventVenueCatalog.Get(venueId);

            return venue.AverageVisitors
                * FansPerVisitor
                * (0.5 + 0.5 * venue.Prestige)
                * EventVenueCatalog.EffectMultiple(tier);
        }

        /// <summary>What it is worth to the regard, which is the address's half.</summary>
        public static double ReputationAt(EventVenueId venueId, StandTier tier)
        {
            var venue = EventVenueCatalog.Get(venueId);

            return ReputationAtBestAddress
                * venue.Prestige
                * EventVenueCatalog.EffectMultiple(tier);
        }

        /// <summary>
        /// Run the stand and find out how it went.
        ///
        /// The only roll in this feature, and it has its own stream so adding it does not shift
        /// every other draw in the game. Everything else about events is derived or decided by the
        /// player.
        /// </summary>
        public static EventOutcome Run(CityEventId id, int year, StandTier tier, uint seed)
        {
            var definition = CityEventCatalog.Get(id);
            var venue = EventVenueCatalog.Get(definition.Venue);

            var chance = ViralChance(venue.Id, tier);
            var random = StreamFor(seed, id, year, tier);
            var viral = random.NextChance(chance);

            var fans = FansAt(venue.Id, tier);
            var regard = ReputationAt(venue.Id, tier);

            if (viral)
            {
                fans *= ViralFansMultiple;
                regard *= ViralReputationMultiple;
            }

            return new EventOutcome(fans, regard, viral, chance);
        }

        private static DeterministicRandom StreamFor(uint seed, CityEventId id, int year,
            StandTier tier)
        {
            unchecked
            {
                var mixed = seed ^ SeedSalt;
                mixed = mixed * 16777619u + (uint)((int)id * 97 + 13);
                mixed = mixed * 16777619u + (uint)year;
                mixed = mixed * 16777619u + (uint)((int)tier * 7 + 3);

                return new DeterministicRandom(mixed == 0u ? SeedSalt : mixed);
            }
        }
    }
}
