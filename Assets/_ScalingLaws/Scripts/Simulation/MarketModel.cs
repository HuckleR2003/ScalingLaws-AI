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

        /// <summary>
        /// Years into the campaign after which the curve above is no longer the whole of demand:
        /// the start of 2029, when it is past ninety per cent of its ceiling.
        /// </summary>
        public const double HeavierUseFromYears = 7.0;

        /// <summary>
        /// How much more demand there is each year after that, because the people already using a
        /// model use it more.
        ///
        /// **The number of people using AI used to fall by half from 2028 to 2035.** Users are
        /// derived, tokens divided by what one person gets through, and each audience's use grows
        /// eighteen to thirty two per cent a year for the whole campaign. The Gompertz curve above
        /// saturates, so every year after it did the same tokens were divided by a heavier user and
        /// the world appeared to lose a fifth of its people. Nobody had left; the curve had simply
        /// stopped counting the heavier use as demand. The whole market's takings fell from $52bn a
        /// year to $22bn with it, so the late game was a race for a shrinking prize.
        ///
        /// A quarter a year sits inside the range of the audiences' own intensity growth, so the
        /// population holds roughly level rather than growing or shrinking. It starts only once the
        /// curve has saturated, so nothing about the years every balance test was tuned on moves.
        /// </summary>
        public const double HeavierUseGrowthPerYear = 1.25;

        // Price: exponential decay from the 2022 list price of a large completion model.
        public const double InitialPricePerMillionTokensUsd = 20.0;
        public const double PriceDecayPerYear = 0.80;
        public const double PriceFloorPerMillionTokensUsd = 0.04;

        // Algorithmic progress: the same capability gets cheaper to reach every year.
        public const double AlgorithmicEfficiencyDoublingYears = 1.0;

        /// <summary>
        /// Where the yearly doubling stops being yearly: sixty four times, which the calendar
        /// reaches at the start of 2028.
        /// </summary>
        public const double EarlyAlgorithmicEfficiency = 64.0;

        /// <summary>
        /// Past <see cref="EarlyAlgorithmicEfficiency"/> recipes keep improving, three times slower.
        ///
        /// **They used to stop dead.** Sixty four was a ceiling, the corpora had stopped growing
        /// three years earlier and the slider was already at its top, so from 2028 the best run any
        /// player could train never improved again while the field went on climbing. That is the
        /// late game `CeilingProbe` found unwinnable. Slower rather than stopped is also the honest
        /// reading of the trend: nobody expects the doubling to hold forever, and nobody expects it
        /// to end on a particular Tuesday either.
        /// </summary>
        public const double LateAlgorithmicEfficiencyDoublingYears = 3.0;

        public const double MaximumAlgorithmicEfficiency = 256.0;

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

        /// <summary>
        /// Total tokens served across the market that day, in billions.
        ///
        /// The smooth curve is the industry growing; the multiplier is the world happening to it.
        /// Applied here rather than at each reader, because there has to be exactly one place a
        /// world event can reach the economy.
        /// </summary>
        public static double DemandOn(GameDate date)
        {
            var days = date.DayIndex;
            var exponent = -DemandShape * Math.Exp(-DemandRatePerDay * days);

            var heavierUse = Math.Pow(HeavierUseGrowthPerYear,
                Math.Max(0.0, GameDate.Start.YearsUntil(date) - HeavierUseFromYears));

            return DemandCeilingBillionTokensPerDay * Math.Exp(exponent) * heavierUse
                * WorldEventCatalog.MultiplierOn(WorldLever.Demand, date);
        }

        /// <summary>
        /// Average price per million tokens that day.
        ///
        /// The floor is applied last, after the world has had its say, so a price war can never
        /// take the rate below what anybody could serve at.
        /// </summary>
        public static double PriceOn(GameDate date)
        {
            var years = GameDate.Start.YearsUntil(date);

            var price = InitialPricePerMillionTokensUsd * Math.Exp(-PriceDecayPerYear * years)
                * WorldEventCatalog.MultiplierOn(WorldLever.TokenPrice, date);

            return Math.Max(PriceFloorPerMillionTokensUsd, price);
        }

        /// <summary>
        /// Compute multiplier from better training recipes, relative to 2022. Applied to a run as
        /// the square root on both parameters and tokens, which multiplies the FLOP budget by this
        /// figure while leaving the run's shape untouched.
        /// </summary>
        public static double AlgorithmicEfficiencyOn(GameDate date)
        {
            var efficiency = BaseAlgorithmicEfficiencyOn(date)
                * WorldEventCatalog.MultiplierOn(WorldLever.Efficiency, date);

            // Never below 1.0, whatever the world does. This is measured against 2022, and a
            // regime that costs a lab a month of paperwork has not made 2022's recipes better than
            // today's.
            return Math.Clamp(efficiency, 1.0, MaximumAlgorithmicEfficiency);
        }

        /// <summary>
        /// The doubling law on its own, before the world touches it.
        ///
        /// **Two different questions and both are worth asking.** "What does the published trend
        /// say" is a fact about the scaling literature this game is built on; "what is efficiency
        /// today" is that plus a regime costing a lab a month of paperwork, or reasoning models
        /// arriving. A test that pins the law has to be able to read the law, and one that reads
        /// the sum would go red every time the calendar changed for reasons that have nothing to
        /// do with the trend it names.
        /// </summary>
        public static double BaseAlgorithmicEfficiencyOn(GameDate date)
        {
            var years = Math.Max(0.0, GameDate.Start.YearsUntil(date));
            var earlyYears = Math.Log(EarlyAlgorithmicEfficiency, 2.0) * AlgorithmicEfficiencyDoublingYears;

            var efficiency = years <= earlyYears
                ? Math.Pow(2.0, years / AlgorithmicEfficiencyDoublingYears)
                : EarlyAlgorithmicEfficiency
                  * Math.Pow(2.0, (years - earlyYears) / LateAlgorithmicEfficiencyDoublingYears);

            return Math.Clamp(efficiency, 1.0, MaximumAlgorithmicEfficiency);
        }

        /// <summary>
        /// Accelerator supply pressure today, keyframes plus whatever the world is doing to them.
        ///
        /// **The one number that pays for itself twice.** It drives the purchase price at a 0.35
        /// markup and the rental price at 1.4, so a shortage reaches a cloud tenant four times
        /// harder than it reaches somebody who already bought. A hardware shock therefore needs one
        /// entry in the calendar, not two, and the two prices can never disagree about it.
        /// </summary>
        public static double ScarcityOn(GameDate date) =>
            Math.Clamp(
                BaseScarcityOn(date) * WorldEventCatalog.MultiplierOn(WorldLever.Scarcity, date),
                0.0,
                1.0);

        /// <summary>The keyframed curve on its own, before the world touches it.</summary>
        private static double BaseScarcityOn(GameDate date)
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
