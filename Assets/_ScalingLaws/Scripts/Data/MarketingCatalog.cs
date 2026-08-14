using System;
using System.Collections.Generic;

namespace ScalingLaws.Data
{
    public enum MarketingChannel
    {
        Social = 0,
        Press = 1,
        Radio = 2,
        Television = 3,
        Billboards = 4,
        Creators = 5
    }

    /// <summary>
    /// One way of being seen.
    ///
    /// Channels differ in **cost, speed, persistence, volatility and who they reach**, never in the
    /// size of a bonus. A set of channels that all do the same thing at different prices is a menu
    /// with one correct answer on it, and the whole reason to allow three at once is that they cover
    /// each other's weaknesses.
    /// </summary>
    public readonly struct MarketingChannelDefinition
    {
        public MarketingChannelDefinition(MarketingChannel id, string displayName, string art,
            string pitch, long dailyCostUsd, double reach, double speed, double persistence,
            double volatility, double credibility, AudienceSegment favours)
        {
            Id = id;
            DisplayName = displayName;
            Art = art;
            Pitch = pitch;
            DailyCostUsd = Math.Max(0L, dailyCostUsd);
            Reach = Math.Clamp(reach, 0.0, 2.0);
            Speed = Math.Clamp(speed, 0.05, 1.0);
            Persistence = Math.Clamp(persistence, 0.0, 1.0);
            Volatility = Math.Clamp(volatility, 0.0, 1.0);
            Credibility = Math.Clamp(credibility, -1.0, 1.0);
            Favours = favours;
        }

        public MarketingChannel Id { get; }
        public string DisplayName { get; }

        /// <summary>Resource name for the tile picture. Missing art draws an empty plate.</summary>
        public string Art { get; }

        public string Pitch { get; }
        public long DailyCostUsd { get; }

        /// <summary>How much awareness a full day of this buys, before the audience fit.</summary>
        public double Reach { get; }

        /// <summary>How quickly it lands. Social is days, television is weeks.</summary>
        public double Speed { get; }

        /// <summary>How much of the awareness survives after the campaign stops.</summary>
        public double Persistence { get; }

        /// <summary>How unpredictable the result is. High means it can do far more or far less.</summary>
        public double Volatility { get; }

        /// <summary>What it does to standing. Press builds it; shouting on social can cost it.</summary>
        public double Credibility { get; }

        /// <summary>The audience it reaches best. Others still hear it, less.</summary>
        public AudienceSegment Favours { get; }

        public override string ToString() => $"{DisplayName} (${DailyCostUsd:N0}/day)";
    }

    /// <summary>
    /// The channels, and how long a campaign can run for.
    ///
    /// Nothing here is a straight upgrade of anything else. Television reaches the most people and is
    /// slow and expensive; social is cheap and fast and swings hard both ways; press hardly moves the
    /// numbers and is the only thing that reliably builds standing. A player who buys the most
    /// expensive option because it is the most expensive should be wrong about half the time.
    /// </summary>
    public static class MarketingCatalog
    {
        public const string CatalogVersion = "marketing-2026-08-13";

        /// <summary>Nobody may run more than this many channels at once.</summary>
        public const int MostChannelsAtOnce = 3;

        private static readonly MarketingChannelDefinition[] Entries =
        {
            new(MarketingChannel.Social, "Social", "marketing_social",
                "Cheap, immediate and it forgets you just as fast. It can go further than anything "
                + "else here or nowhere at all, and it is the only channel that can cost you standing.",
                dailyCostUsd: 4_000, reach: 0.85, speed: 0.85, persistence: 0.15,
                volatility: 0.80, credibility: -0.15, favours: AudienceSegment.Consumer),

            new(MarketingChannel.Press, "Press", "marketing_press",
                "Barely moves the numbers and is the one thing that reliably builds standing. "
                + "Enterprise buyers read it; nobody else does.",
                dailyCostUsd: 9_000, reach: 0.30, speed: 0.35, persistence: 0.75,
                volatility: 0.20, credibility: 0.60, favours: AudienceSegment.Enterprise),

            new(MarketingChannel.Radio, "Radio", "marketing_radio",
                "Regional, steady and unfashionable. Cheap for what it covers and it keeps working "
                + "long after the money stops.",
                dailyCostUsd: 6_500, reach: 0.45, speed: 0.45, persistence: 0.55,
                volatility: 0.25, credibility: 0.10, favours: AudienceSegment.Creative),

            new(MarketingChannel.Television, "Television", "marketing_tv",
                "The widest reach there is and the slowest to arrive. Expensive enough that a short "
                + "campaign is money spent before anybody has noticed.",
                dailyCostUsd: 38_000, reach: 1.60, speed: 0.18, persistence: 0.60,
                volatility: 0.30, credibility: 0.25, favours: AudienceSegment.Consumer),

            new(MarketingChannel.Billboards, "Billboards", "marketing_billboards",
                "Physical, local and impossible to skip. Slow, and what it buys stays bought for a "
                + "while after the posters come down.",
                dailyCostUsd: 14_000, reach: 0.70, speed: 0.30, persistence: 0.80,
                volatility: 0.15, credibility: 0.15, favours: AudienceSegment.Consumer),

            new(MarketingChannel.Creators, "Creators", "marketing_creators",
                "Somebody with an audience talks about you. A sharp spike, a short memory, and a real "
                + "chance of the wrong person saying the wrong thing.",
                dailyCostUsd: 11_000, reach: 1.10, speed: 0.75, persistence: 0.25,
                volatility: 0.70, credibility: 0.05, favours: AudienceSegment.Developer)
        };

        /// <summary>The lengths a campaign can be booked for. Zero means open ended.</summary>
        public static readonly int[] TermsInMonths = { 1, 2, 3, 6, 0 };

        /// <summary>
        /// What an open ended booking costs against a fixed term.
        ///
        /// A quarter more per day, because nobody sells an unlimited contract at the price of a
        /// committed one. It is the convenience of never having to think about it again, and
        /// convenience is the thing this game charges for.
        /// </summary>
        public const double OpenEndedSurcharge = 1.25;

        /// <summary>
        /// Longer bookings are cheaper per day, down to a floor. Six months is a real commitment and
        /// the discount is what makes committing a decision rather than an obviously worse option.
        /// </summary>
        public static double TermMultiplier(int months) => months switch
        {
            <= 0 => OpenEndedSurcharge,
            1 => 1.00,
            2 => 0.94,
            3 => 0.88,
            _ => 0.78
        };

        public static IReadOnlyList<MarketingChannelDefinition> All => Entries;

        public static MarketingChannelDefinition Get(MarketingChannel id)
        {
            foreach (var entry in Entries)
            {
                if (entry.Id == id)
                {
                    return entry;
                }
            }

            throw new ArgumentOutOfRangeException(nameof(id), id, "Unknown marketing channel.");
        }

        /// <summary>
        /// How well a channel reaches an audience that is not its favourite.
        ///
        /// Everybody hears everything a little. A television campaign does reach developers, it just
        /// reaches far more consumers, and a game where a channel reaches exactly one group turns
        /// targeting into a lookup table.
        /// </summary>
        public static double AffinityFor(MarketingChannel channel, AudienceSegment segment) =>
            Get(channel).Favours == segment ? 1.0 : 0.45;
    }
}
