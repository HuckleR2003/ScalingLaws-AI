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
        public MarketingChannelDefinition(MarketingChannel id, string art,
            long dailyCostUsd, double reach, double speed, double persistence,
            double volatility, double credibility, AudienceSegment favours)
        {
            Id = id;
            Art = art;
            DailyCostUsd = Math.Max(0L, dailyCostUsd);
            Reach = Math.Clamp(reach, 0.0, 2.0);
            Speed = Math.Clamp(speed, 0.05, 1.0);
            Persistence = Math.Clamp(persistence, 0.0, 1.0);
            Volatility = Math.Clamp(volatility, 0.0, 1.0);
            Credibility = Math.Clamp(credibility, -1.0, 1.0);
            Favours = favours;
        }

        public MarketingChannel Id { get; }

        private static string KeyFor(MarketingChannel id) => id switch
        {
            MarketingChannel.Press => "channel.press",
            MarketingChannel.Radio => "channel.radio",
            MarketingChannel.Television => "channel.tv",
            MarketingChannel.Billboards => "channel.billboards",
            MarketingChannel.Creators => "channel.creators",
            _ => "channel.social"
        };

        /// <summary>Read from the book at access time. See `PrecisionDefinition`.</summary>
        public string DisplayName => Loc.T(KeyFor(Id));

        /// <summary>Resource name for the tile picture. Missing art draws an empty plate.</summary>
        public string Art { get; }

        /// <summary>
        /// The tooltip, and on this screen it is the whole explanation.
        ///
        /// The tiles are photographs with a name on them; everything that says what a channel
        /// actually trades sits here, so leaving it in English left six pictures and no argument.
        /// </summary>
        public string Pitch => Loc.T(KeyFor(Id) + ".pitch");
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
            // The words are `channel.*` in the phrase book.
            new(MarketingChannel.Social, "marketing_social",
                dailyCostUsd: 4_000, reach: 0.85, speed: 0.85, persistence: 0.15,
                volatility: 0.80, credibility: -0.15, favours: AudienceSegment.Consumer),

            new(MarketingChannel.Press, "marketing_press",
                dailyCostUsd: 9_000, reach: 0.30, speed: 0.35, persistence: 0.75,
                volatility: 0.20, credibility: 0.60, favours: AudienceSegment.Enterprise),

            new(MarketingChannel.Radio, "marketing_radio",
                dailyCostUsd: 6_500, reach: 0.45, speed: 0.45, persistence: 0.55,
                volatility: 0.25, credibility: 0.10, favours: AudienceSegment.Creative),

            new(MarketingChannel.Television, "marketing_tv",
                dailyCostUsd: 38_000, reach: 1.60, speed: 0.18, persistence: 0.60,
                volatility: 0.30, credibility: 0.25, favours: AudienceSegment.Consumer),

            new(MarketingChannel.Billboards, "marketing_billboards",
                dailyCostUsd: 14_000, reach: 0.70, speed: 0.30, persistence: 0.80,
                volatility: 0.15, credibility: 0.15, favours: AudienceSegment.Consumer),

            new(MarketingChannel.Creators, "marketing_creators",
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
