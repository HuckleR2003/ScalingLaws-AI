using System;
using System.Collections.Generic;
using ScalingLaws.Core;
using ScalingLaws.Data;

namespace ScalingLaws.Simulation
{
    public enum IntelSubject
    {
        HardwareLaunch = 0,
        RivalRelease = 1,
        RivalHoldingBack = 2,
        PriceCollapse = 3,
        ScarcitySpike = 4,
        DemandSurge = 5
    }

    /// <summary>
    /// One thing the research desk believes is about to happen.
    ///
    /// <see cref="IsCorrect"/> is the truth and the player never sees it. What the player sees is
    /// <see cref="Confidence"/>, which is what the desk believes about itself, and those two are not
    /// the same number. A team on retainer is right most of the time and confident all of the time.
    /// Acting on a wrong signal is supposed to hurt, otherwise paying for information is just a tax
    /// with a guaranteed refund.
    /// </summary>
    public readonly struct IntelSignal
    {
        public IntelSignal(
            IntelSubject subject,
            IntelTier tier,
            GameDate issuedOn,
            GameDate predictedDate,
            string headline,
            string detail,
            double confidence,
            bool isCorrect)
        {
            Subject = subject;
            Tier = tier;
            IssuedOn = issuedOn;
            PredictedDate = predictedDate;
            Headline = headline ?? string.Empty;
            Detail = detail ?? string.Empty;
            Confidence = Math.Clamp(SimUnits.Finite(confidence), 0.0, 1.0);
            IsCorrect = isCorrect;
        }

        public IntelSubject Subject { get; }
        public IntelTier Tier { get; }
        public GameDate IssuedOn { get; }

        /// <summary>When the desk thinks it lands. Wrong signals have wrong dates.</summary>
        public GameDate PredictedDate { get; }

        public string Headline { get; }
        public string Detail { get; }

        /// <summary>What the desk claims. Always higher than the tier's real hit rate.</summary>
        public double Confidence { get; }

        /// <summary>The truth. Never shown, never exported to a save the player can read.</summary>
        public bool IsCorrect { get; }

        public int LeadTimeDays => Math.Max(0, PredictedDate.DayIndex - IssuedOn.DayIndex);

        public override string ToString() => $"[{Tier}] {Headline} ({Confidence:P0})";
    }

    /// <summary>
    /// The ONE place advance warning is produced and priced.
    ///
    /// Three paid tiers on top of the free news. Each buys lead time and a hit rate, and the two do
    /// not move together: the cheap tier hears things early and is wrong a lot, which is worse than
    /// useless if the player treats it as fact.
    /// </summary>
    public static class IntelligenceService
    {
        /// <summary>
        /// Monthly retainer per outfit. The author's figures.
        ///
        /// These replaced 120k / 650k / 2.8M on 2026-08-13, which makes information roughly six times
        /// cheaper across the board. That is a deliberate change of what the system is for: at the old
        /// prices a desk was a late-game luxury, and at these a young company can afford National
        /// Press in its first year, which is when advance warning about hardware is worth the most.
        /// </summary>
        public static long MonthlyRetainerUsd(IntelTier tier) => tier switch
        {
            IntelTier.NationalPress => 20_000,
            IntelTier.KnownWords => 50_000,
            IntelTier.TrendSearch => 400_000,
            _ => 0
        };

        /// <summary>Share of signals from this tier that turn out to be right.</summary>
        public static double Accuracy(IntelTier tier) => tier switch
        {
            IntelTier.NationalPress => 0.58,
            IntelTier.KnownWords => 0.76,
            IntelTier.TrendSearch => 0.90,
            _ => 1.0
        };

        /// <summary>How far ahead this tier sees, in days.</summary>
        public static int LeadTimeDays(IntelTier tier) => tier switch
        {
            IntelTier.NationalPress => 120,
            IntelTier.KnownWords => 220,
            IntelTier.TrendSearch => 380,
            _ => 0
        };

        /// <summary>Expected days between signals. A better desk also reports more often.</summary>
        public static int ReportIntervalDays(IntelTier tier) => tier switch
        {
            IntelTier.NationalPress => 45,
            IntelTier.KnownWords => 32,
            IntelTier.TrendSearch => 24,
            _ => 0
        };

        /// <summary>
        /// Confidence the desk puts on its own note. Deliberately above its real hit rate, by more
        /// at the cheap end, because that is how bought research actually reads.
        /// </summary>
        public static double StatedConfidence(IntelTier tier, DeterministicRandom random)
        {
            var overclaim = tier switch
            {
                IntelTier.NationalPress => 0.22,
                IntelTier.KnownWords => 0.12,
                IntelTier.TrendSearch => 0.05,
                _ => 0.0
            };

            return Math.Clamp(Accuracy(tier) + overclaim + random.NextRange(-0.04, 0.04), 0.0, 0.99);
        }

        /// <summary>
        /// Produces one note. Correct notes describe something real that is coming; incorrect ones
        /// describe the same kind of event with the date or the subject moved.
        /// </summary>
        public static IntelSignal Generate(
            IntelTier tier,
            GameDate date,
            CompetitorField field,
            DeterministicRandom random)
        {
            var isCorrect = random.NextChance(Accuracy(tier));
            var confidence = StatedConfidence(tier, random);
            var horizon = date.AddDays(LeadTimeDays(tier));

            var waiting = field?.LabsWaitingForHardware();
            if (waiting != null && waiting.Count > 0 && random.NextChance(0.35))
            {
                var lab = waiting[random.NextInt(0, waiting.Count)];
                var predicted = isCorrect
                    ? lab.NextReleaseDate
                    : lab.NextReleaseDate.AddDays(random.NextInt(-200, 200));

                return new IntelSignal(
                    IntelSubject.RivalHoldingBack,
                    tier,
                    date,
                    predicted,
                    $"{lab.LabName} is sitting out this hardware cycle",
                    isCorrect
                        ? $"They have pushed their launch to about {predicted} to train on newer silicon. Shipping into that window means being overtaken shortly after."
                        : $"They look ready to launch around {predicted}. Nothing suggests they are waiting.",
                    confidence,
                    isCorrect);
            }

            if (HardwareCatalog.TryGetNextAcceleratorLaunch(date, out var launch)
                && launch.ReleaseDate <= horizon
                && random.NextChance(0.5))
            {
                var predicted = isCorrect
                    ? launch.ReleaseDate
                    : launch.ReleaseDate.AddDays(random.NextInt(-180, 180));

                return new IntelSignal(
                    IntelSubject.HardwareLaunch,
                    tier,
                    date,
                    predicted,
                    $"{launch.VendorName} {launch.DisplayName} lands around {predicted}",
                    isCorrect
                        ? $"About {launch.PetaflopsPerUnit:0.00} PF per unit at roughly ${launch.LaunchPriceUsd:N0}. Buying the outgoing part now means holding it through this launch."
                        : $"Expected around {predicted}. Timing on this one is soft.",
                    confidence,
                    isCorrect);
            }

            var scarcityAhead = MarketModel.ScarcityOn(horizon) - MarketModel.ScarcityOn(date);
            if (Math.Abs(scarcityAhead) > 0.12)
            {
                var tightening = scarcityAhead > 0;
                var reported = isCorrect ? tightening : !tightening;
                return new IntelSignal(
                    IntelSubject.ScarcitySpike,
                    tier,
                    date,
                    horizon,
                    reported ? "Accelerator supply is about to tighten" : "Accelerator supply is about to loosen",
                    reported
                        ? "Rental rates and purchase premiums both move up. Contracts signed now hold their price."
                        : "Rental rates ease off. Waiting costs less than it did.",
                    confidence,
                    isCorrect);
            }

            var priceThen = MarketModel.PriceOn(horizon);
            var priceNow = MarketModel.PriceOn(date);
            var drop = priceNow <= 0.0 ? 0.0 : 1.0 - priceThen / priceNow;
            var reportedDrop = isCorrect ? drop : drop * random.NextRange(0.1, 2.5);

            return new IntelSignal(
                IntelSubject.PriceCollapse,
                tier,
                date,
                horizon,
                $"Token prices down about {reportedDrop:P0} by {horizon}",
                "Margin on every model currently live shrinks by roughly that much unless serving cost falls with it.",
                confidence,
                isCorrect);
        }
    }
}
