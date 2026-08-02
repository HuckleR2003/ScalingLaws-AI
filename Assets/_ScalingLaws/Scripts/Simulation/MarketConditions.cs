using System;
using ScalingLaws.Core;
using ScalingLaws.Data;

namespace ScalingLaws.Simulation
{
    /// <summary>
    /// The world on one day, as it looks from outside the company. Everything here is a function of
    /// the date alone: nothing the player does moves these numbers, which is what makes timing a
    /// skill rather than a stat.
    /// </summary>
    public readonly struct MarketConditions
    {
        public MarketConditions(
            GameDate date,
            double totalDemandBillionTokensPerDay,
            double pricePerMillionTokensUsd,
            double frontierCapability,
            double scarcityIndex,
            double rentPricePerPetaflopHourUsd,
            HardwareGenerationId rentableGeneration,
            double algorithmicEfficiency)
        {
            Date = date;
            TotalDemandBillionTokensPerDay = Math.Max(0.0, SimUnits.Finite(totalDemandBillionTokensPerDay));
            PricePerMillionTokensUsd = Math.Max(0.001, SimUnits.Finite(pricePerMillionTokensUsd, 1.0));
            FrontierCapability = Math.Clamp(SimUnits.Finite(frontierCapability), 0.0, 100.0);
            ScarcityIndex = Math.Clamp(SimUnits.Finite(scarcityIndex), 0.0, 1.0);
            RentPricePerPetaflopHourUsd = Math.Max(0.01, SimUnits.Finite(rentPricePerPetaflopHourUsd, 1.0));
            RentableGeneration = rentableGeneration;
            AlgorithmicEfficiency = Math.Clamp(SimUnits.Finite(algorithmicEfficiency, 1.0), 1.0, 1024.0);
        }

        public GameDate Date { get; }

        /// <summary>Everything every provider serves that day, in billions of tokens.</summary>
        public double TotalDemandBillionTokensPerDay { get; }

        /// <summary>Average market price per million tokens. It only ever goes down.</summary>
        public double PricePerMillionTokensUsd { get; }

        /// <summary>Best capability any rival has live that day.</summary>
        public double FrontierCapability { get; }

        /// <summary>How tight accelerator supply is, 0 to 1. Drives rental prices, not availability.</summary>
        public double ScarcityIndex { get; }

        public double RentPricePerPetaflopHourUsd { get; }

        /// <summary>What the clouds are actually offering. Always a generation or two behind launch day.</summary>
        public HardwareGenerationId RentableGeneration { get; }

        /// <summary>
        /// How much more capability the same FLOPs buy compared with 2022, from better training
        /// recipes alone. This is the tailwind that keeps late-game runs affordable while the
        /// frontier keeps climbing.
        /// </summary>
        public double AlgorithmicEfficiency { get; }

        public double RentPricePerPetaflopDayUsd => RentPricePerPetaflopHourUsd * SimUnits.HoursPerDay;

        /// <summary>Whole market revenue for the day, in dollars.</summary>
        public double DailyMarketRevenueUsd => TotalDemandBillionTokensPerDay * 1000.0 * PricePerMillionTokensUsd;

        public override string ToString() =>
            $"{Date}: demand {TotalDemandBillionTokensPerDay:N0}B tok/d, ${PricePerMillionTokensUsd:0.###}/M, frontier {FrontierCapability:0.0}";
    }
}
