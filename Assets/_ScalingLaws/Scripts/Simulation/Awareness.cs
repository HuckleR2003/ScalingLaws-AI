using System;
using System.Collections.Generic;
using ScalingLaws.Core;
using ScalingLaws.Data;

namespace ScalingLaws.Simulation
{
    /// <summary>
    /// A booked campaign: what is being run, to whom, for how long.
    ///
    /// Channels are held as a set rather than one at a time because the interesting decision is the
    /// combination. Television plus social is broad and fast; press plus radio is slow and sticks.
    /// </summary>
    public sealed class MarketingCampaign
    {
        public MarketingCampaign(IReadOnlyList<MarketingChannel> channels, AudienceSegment target,
            int termMonths, GameDate startedOn)
        {
            var picked = new List<MarketingChannel>(MarketingCatalog.MostChannelsAtOnce);

            foreach (var channel in channels ?? Array.Empty<MarketingChannel>())
            {
                if (!picked.Contains(channel) && picked.Count < MarketingCatalog.MostChannelsAtOnce)
                {
                    picked.Add(channel);
                }
            }

            Channels = picked;
            Target = target;
            TermMonths = Math.Max(0, termMonths);
            StartedOn = startedOn;
        }

        public IReadOnlyList<MarketingChannel> Channels { get; }

        /// <summary>Who it is aimed at. Other audiences still hear it, less.</summary>
        public AudienceSegment Target { get; }

        /// <summary>Booked length in months. Zero is open ended and costs more per day.</summary>
        public int TermMonths { get; }

        public GameDate StartedOn { get; }

        public bool IsOpenEnded => TermMonths <= 0;

        public int DaysBooked => TermMonths <= 0 ? int.MaxValue : TermMonths * 30;

        public int DaysRun(GameDate today) => Math.Max(0, today.DayIndex - StartedOn.DayIndex);

        public bool HasFinished(GameDate today) =>
            !IsOpenEnded && DaysRun(today) >= DaysBooked;

        public int DaysLeft(GameDate today) =>
            IsOpenEnded ? int.MaxValue : Math.Max(0, DaysBooked - DaysRun(today));

        /// <summary>What a day of this costs, with the term discount or the open ended surcharge.</summary>
        public long DailyCostUsd
        {
            get
            {
                var total = 0L;
                foreach (var channel in Channels)
                {
                    total += MarketingCatalog.Get(channel).DailyCostUsd;
                }

                return (long)Math.Round(total * MarketingCatalog.TermMultiplier(TermMonths));
            }
        }
    }

    /// <summary>
    /// How many people have heard of the company, audience by audience.
    ///
    /// **This is what marketing buys, and it is the only thing it buys.** Awareness gates whether a
    /// product is considered at all; it does nothing to how good the product is, what it costs to
    /// run, or how much an audience will tolerate paying. A bad product advertised hard gets tried
    /// and abandoned, which costs money twice: once on the campaign and again on the serving.
    ///
    /// It is a stock rather than a rate. Campaigns push it up at a speed that belongs to the channel,
    /// it falls back toward a floor set by the company's standing when they stop, and how much
    /// survives is the channel's persistence. That is the difference between renting attention and
    /// building a name.
    /// </summary>
    public sealed class Awareness
    {
        /// <summary>What a completely unknown company still gets from word of mouth.</summary>
        public const double Floor = 0.15;

        /// <summary>Awareness lost each day with nothing running, before the standing floor.</summary>
        public const double DecayPerDay = 0.010;

        /// <summary>Spend a day, per audience, at which a channel is saturated.</summary>
        public const double SaturationUsdPerDay = 60_000.0;

        private readonly Dictionary<AudienceSegment, double> heard = new();

        /// <summary>How well known the company is to one audience, nothing to everything.</summary>
        public double In(AudienceSegment segment) =>
            heard.TryGetValue(segment, out var value) ? value : 0.0;

        public void Set(AudienceSegment segment, double value) =>
            heard[segment] = Math.Clamp(SimUnits.Finite(value), 0.0, 1.0);

        /// <summary>
        /// What a product gets from being known, as a multiplier on how attractive it looks.
        ///
        /// Never zero, because somebody always stumbles across a thing, and never above one, because
        /// being famous is not the same as being good. This is the term that makes an unknown company
        /// with an excellent model lose to a known company with an average one, which is the whole
        /// argument for having a marketing system at all.
        /// </summary>
        public static double Consideration(double awareness) =>
            Floor + (1.0 - Floor) * Math.Clamp(SimUnits.Finite(awareness), 0.0, 1.0);

        /// <summary>
        /// One day of campaigns and forgetting.
        ///
        /// Returns what the day cost, so the caller can bill it in the same place it bills everything
        /// else rather than working the number out a second time.
        /// </summary>
        public long Advance(IReadOnlyList<MarketingCampaign> campaigns, GameDate today,
            double reputation, DeterministicRandom random,
            Func<AudienceSegment, double> heldShare = null)
        {
            var spend = 0L;

            // Standing keeps you known even with nothing running. A famous lab does not become
            // anonymous the month it stops advertising.
            var floorFromStanding = Math.Clamp(SimUnits.Finite(reputation), 0.0, 1.0) * 0.55;

            foreach (var definition in AudienceCatalog.All)
            {
                var segment = definition.Segment;
                var pressure = 0.0;
                var speed = 0.0;

                foreach (var campaign in campaigns)
                {
                    if (campaign.HasFinished(today))
                    {
                        continue;
                    }

                    foreach (var channel in campaign.Channels)
                    {
                        var channelDefinition = MarketingCatalog.Get(channel);
                        var affinity = MarketingCatalog.AffinityFor(channel, segment)
                            * (campaign.Target == segment ? 1.0 : 0.6);

                        // Saturating, so doubling the money on one channel does not double the reach.
                        var money = channelDefinition.DailyCostUsd
                            / (1.0 + channelDefinition.DailyCostUsd / SaturationUsdPerDay);

                        var swing = 1.0;
                        if (channelDefinition.Volatility > 0.0)
                        {
                            // Volatile channels land anywhere between much less and much more than
                            // they promise. This is the only randomness in the system and it is
                            // deterministic, so a campaign replays identically from a save.
                            swing = 1.0 + (random.NextDouble() * 2.0 - 1.0) * channelDefinition.Volatility;
                        }

                        pressure += channelDefinition.Reach * affinity * swing
                            * money / SaturationUsdPerDay;

                        speed = Math.Max(speed, channelDefinition.Speed);
                    }
                }

                // Being used is itself being known. If a fifth of an audience is on the service then
                // at least a fifth of them have heard of it, whatever the advertising says. Without
                // this, awareness was a permanent tax on any company that did not advertise rather
                // than a lever for one that wanted to grow, and a five year balance test caught it.
                var fromUse = Math.Clamp(heldShare?.Invoke(segment) ?? 0.0, 0.0, 1.0);
                var floor = Math.Max(floorFromStanding, fromUse);

                var current = In(segment);
                var target = Math.Clamp(pressure, 0.0, 1.0);

                if (target > current)
                {
                    current += (target - current) * Math.Clamp(speed, 0.05, 1.0);
                }
                else
                {
                    current -= DecayPerDay;
                }

                Set(segment, Math.Max(floor, current));
            }

            foreach (var campaign in campaigns)
            {
                if (!campaign.HasFinished(today))
                {
                    spend += campaign.DailyCostUsd;
                }
            }

            return spend;
        }

        /// <summary>The average across audiences, for a headline figure.</summary>
        public double Overall
        {
            get
            {
                var total = 0.0;
                foreach (var definition in AudienceCatalog.All)
                {
                    total += In(definition.Segment);
                }

                return AudienceCatalog.All.Count == 0 ? 0.0 : total / AudienceCatalog.All.Count;
            }
        }

        public void Capture(List<double> into)
        {
            into.Clear();
            foreach (var definition in AudienceCatalog.All)
            {
                into.Add(In(definition.Segment));
            }
        }

        public void Restore(IReadOnlyList<double> values)
        {
            heard.Clear();
            if (values == null)
            {
                return;
            }

            for (var index = 0; index < AudienceCatalog.All.Count && index < values.Count; index++)
            {
                Set(AudienceCatalog.All[index].Segment, values[index]);
            }
        }
    }
}
