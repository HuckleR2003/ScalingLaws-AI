using System;
using ScalingLaws.Core;
using ScalingLaws.Data;

namespace ScalingLaws.Simulation
{
    /// <summary>
    /// The ONE place the outside world is generated. Every curve here is a pure function of the
    /// date, so a test can ask what 2027 looks like without simulating five years first.
    ///
    /// Shape of the campaign, and why the game is not a guaranteed profit machine:
    ///   demand grows fast and then saturates
    ///   price per token falls by roughly half every year, permanently
    ///   the frontier capability climbs the whole time
    /// Revenue only holds up if capability keeps pace with the frontier, because a model that stood
    /// still is competing on price in a market whose price is collapsing.
    /// </summary>
    public static class MarketModel
    {
        // Demand: Gompertz curve in billions of tokens served per day across the whole market.
        // Anchored on roughly 14B tokens/day in early 2022 and 9300B in early 2024.
        public const double DemandCeilingBillionTokensPerDay = 2_000_000.0;
        private const double DemandShape = 11.891;
        private const double DemandRatePerDay = 0.00109;

        // Price: exponential decay from the 2022 list price of a large completion model.
        public const double InitialPricePerMillionTokensUsd = 20.0;
        public const double PriceDecayPerYear = 0.80;
        public const double PriceFloorPerMillionTokensUsd = 0.04;

        // Algorithmic progress: the same capability gets cheaper to reach every year.
        public const double AlgorithmicEfficiencyDoublingYears = 1.0;
        public const double MaximumAlgorithmicEfficiency = 64.0;

        // Cloud pricing, derived from what the hardware actually costs rather than a magic curve.
        public const int CloudAmortizationDays = 1095;
        public const double CloudMarkup = 2.6;
        public const double ScarcityElasticity = 1.4;
        public const double CloudPowerCostPerKilowattHourUsd = 0.14;
        public const double CloudHousingCostPerKilowattHourUsd = 0.2466;

        /// <summary>Days between a part launching and the clouds renting it out.</summary>
        public const int CloudAvailabilityLagDays = 180;

        /// <summary>
        /// Accelerator supply pressure over the campaign, as keyframes. Piecewise linear between
        /// them, flat outside. The 2023 peak is the allocation crunch; it never fully goes away.
        /// </summary>
        private static readonly (GameDate Date, double Value)[] ScarcityKeyframes =
        {
            (GameDate.FromCalendar(2022, 1, 1), 0.25),
            (GameDate.FromCalendar(2022, 10, 1), 0.55),
            (GameDate.FromCalendar(2023, 6, 1), 1.00),
            (GameDate.FromCalendar(2024, 6, 1), 0.80),
            (GameDate.FromCalendar(2025, 6, 1), 0.40),
            (GameDate.FromCalendar(2026, 6, 1), 0.25),
            (GameDate.FromCalendar(2028, 1, 1), 0.20)
        };

        public static MarketConditions Evaluate(GameDate date)
        {
            return Evaluate(date, CompetitorCatalog.FrontierCapabilityOn(date));
        }

        /// <summary>
        /// Same world, but with the frontier supplied from the live agent field rather than the
        /// reference table. Everything else here is a pure function of the date and does not care
        /// who is winning.
        /// </summary>
        public static MarketConditions Evaluate(GameDate date, double frontierCapability)
        {
            var scarcity = ScarcityOn(date);
            var rentable = RentableGenerationOn(date);

            return new MarketConditions(
                date,
                DemandOn(date),
                PriceOn(date),
                frontierCapability,
                scarcity,
                RentPricePerPetaflopHourUsd(rentable, scarcity),
                rentable,
                AlgorithmicEfficiencyOn(date));
        }

        /// <summary>Total tokens served across the market that day, in billions.</summary>
        public static double DemandOn(GameDate date)
        {
            var days = date.DayIndex;
            var exponent = -DemandShape * Math.Exp(-DemandRatePerDay * days);
            return DemandCeilingBillionTokensPerDay * Math.Exp(exponent);
        }

        /// <summary>Average price per million tokens that day.</summary>
        public static double PriceOn(GameDate date)
        {
            var years = GameDate.Start.YearsUntil(date);
            var price = InitialPricePerMillionTokensUsd * Math.Exp(-PriceDecayPerYear * years);
            return Math.Max(PriceFloorPerMillionTokensUsd, price);
        }

        /// <summary>
        /// Compute multiplier from better training recipes, relative to 2022. Applied to a run as
        /// the square root on both parameters and tokens, which multiplies the FLOP budget by this
        /// figure while leaving the run's shape untouched.
        /// </summary>
        public static double AlgorithmicEfficiencyOn(GameDate date)
        {
            var years = Math.Max(0.0, GameDate.Start.YearsUntil(date));
            var efficiency = Math.Pow(2.0, years / AlgorithmicEfficiencyDoublingYears);
            return Math.Clamp(efficiency, 1.0, MaximumAlgorithmicEfficiency);
        }

        public static double ScarcityOn(GameDate date)
        {
            var first = ScarcityKeyframes[0];
            if (date <= first.Date)
            {
                return first.Value;
            }

            for (var index = 1; index < ScarcityKeyframes.Length; index++)
            {
                var previous = ScarcityKeyframes[index - 1];
                var current = ScarcityKeyframes[index];
                if (date > current.Date)
                {
                    continue;
                }

                var span = current.Date.DayIndex - previous.Date.DayIndex;
                if (span <= 0)
                {
                    return current.Value;
                }

                var t = (date.DayIndex - previous.Date.DayIndex) / (double)span;
                return previous.Value + (current.Value - previous.Value) * t;
            }

            return ScarcityKeyframes[ScarcityKeyframes.Length - 1].Value;
        }

        /// <summary>
        /// What the clouds will rent that day: the best accelerator that launched at least
        /// <see cref="CloudAvailabilityLagDays"/> ago. Renting always tracks the frontier, just late.
        /// That lag is the price of never owning a depreciating asset.
        /// </summary>
        public static HardwareGenerationId RentableGenerationOn(GameDate date)
        {
            var effectiveDate = date.AddDays(-CloudAvailabilityLagDays);
            if (HardwareCatalog.TryGetFrontier(effectiveDate, HardwareClass.Accelerator, out var frontier))
            {
                return frontier.Id;
            }

            return HardwareGenerationId.AcceleratorV100;
        }

        /// <summary>
        /// Hourly rental price of one petaflop/s, built up from the real cost of owning the part
        /// that is being rented out, plus a cloud margin, plus whatever the shortage allows.
        /// </summary>
        public static double RentPricePerPetaflopHourUsd(HardwareGenerationId generationId, double scarcity)
        {
            if (!HardwareCatalog.TryGet(generationId, out var generation) || generation.PetaflopsPerUnit <= 0.0)
            {
                return 10.0;
            }

            var hardwarePerHour = generation.LaunchPriceUsd / (double)CloudAmortizationDays / SimUnits.HoursPerDay;
            var powerPerHour = generation.PowerKilowatts * CloudPowerCostPerKilowattHourUsd;
            var housingPerHour = generation.PowerKilowatts * CloudHousingCostPerKilowattHourUsd;

            var costPerHour = hardwarePerHour + powerPerHour + housingPerHour;
            var listedPerHour = costPerHour * CloudMarkup * (1.0 + ScarcityElasticity * Math.Clamp(scarcity, 0.0, 1.0));
            return listedPerHour / generation.PetaflopsPerUnit;
        }

        /// <summary>
        /// What the company pays per unit to buy hardware outright. Shortage pricing applies here
        /// too: in 2023 nobody was buying accelerators at list.
        /// </summary>
        public static long PurchasePricePerUnitUsd(HardwareGeneration generation, ComputeTierDefinition tier, double scarcity)
        {
            var scarcityMarkup = 1.0 + 0.35 * Math.Clamp(scarcity, 0.0, 1.0);
            return SimUnits.ToDollars(generation.LaunchPriceUsd * tier.CapitalPriceMultiplier * scarcityMarkup);
        }
    }
}
