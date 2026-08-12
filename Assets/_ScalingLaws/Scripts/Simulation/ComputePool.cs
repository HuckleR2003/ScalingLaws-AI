using System;
using System.Collections.Generic;
using ScalingLaws.Core;
using ScalingLaws.Data;

namespace ScalingLaws.Simulation
{
    /// <summary>
    /// Everything the company can compute on: owned batches with their purchase dates, plus however
    /// many accelerators it is renting today.
    ///
    /// Rented capacity always tracks whatever the clouds are offering, so it never ages. Owned
    /// capacity is frozen at the generation it was bought as, and pays for that every single day.
    /// </summary>
    public sealed class ComputePool
    {
        /// <summary>Accelerator count a single run spreads across before the fabric starts costing throughput.</summary>
        public const int ScalingFreeAcceleratorCount = 256;

        /// <summary>Throughput lost per doubling past the free count.</summary>
        public const double ScalingPenaltyPerDoubling = 0.035;

        public const double MinimumScalingEfficiency = 0.55;

        /// <summary>A cluster with no hosts, memory or fabric still limps along at this fraction.</summary>
        public const double MinimumBalanceFactor = 0.15;

        private const double DaysPerMonth = 30.4375;
        private const double DaysPerYear = 365.2425;

        private readonly List<HardwareAsset> assets = new();

        public IReadOnlyList<HardwareAsset> Assets => assets;

        /// <summary>
        /// Rented capacity, in petaflop/s. Change it any day; it bills from the next tick.
        ///
        /// Denominated in capacity rather than in units on purpose. A cloud tenant contracts for
        /// throughput, and if it were a unit count then the day the clouds moved from one generation
        /// to the next the bill would triple on its own, with no decision made and no extra work
        /// getting done. Renting is the option that does not surprise you; that is its whole point.
        /// Hardware ageing belongs to what you own.
        /// </summary>
        public double RentedPetaflops { get; private set; }

        /// <summary>
        /// Hosting packages held, by kind. Whole units, and they stack.
        ///
        /// Kept beside the slider rather than inside it because they are a different contract: the
        /// slider buys shared petaflops that queue, a package buys reserved capacity that does not.
        /// Folding them into one number would throw away the only thing that distinguishes them.
        /// </summary>
        public Dictionary<HostingPackage, int> Packages { get; } = new();

        public int PackageCount(HostingPackage id) =>
            Packages.TryGetValue(id, out var held) ? held : 0;

        public void SetPackageCount(HostingPackage id, int units)
        {
            var definition = HostingCatalog.Get(id);
            Packages[id] = Math.Clamp(units, 0, definition.UnitCap);
        }

        /// <summary>Petaflops the packages add on top of the slider.</summary>
        public double PackagedPetaflops
        {
            get
            {
                var total = 0.0;
                foreach (var definition in HostingCatalog.All)
                {
                    total += definition.Petaflops * PackageCount(definition.Id);
                }

                return total;
            }
        }

        /// <summary>
        /// How much of the packaged capacity is genuinely reserved, weighted by size. Bulk pulls this
        /// down, which is exactly what it is sold as.
        /// </summary>
        public double PackagedQuality
        {
            get
            {
                var weighted = 0.0;
                var total = 0.0;

                foreach (var definition in HostingCatalog.All)
                {
                    var petaflops = definition.Petaflops * PackageCount(definition.Id);
                    weighted += petaflops * definition.ReservedQuality;
                    total += petaflops;
                }

                return total <= 0.0 ? 0.0 : weighted / total;
            }
        }

        public long PackagesDailyCostUsd
        {
            get
            {
                var total = 0L;
                foreach (var definition in HostingCatalog.All)
                {
                    total += definition.DailyCostUsd * PackageCount(definition.Id);
                }

                return total;
            }
        }

        public void SetRentedPetaflops(double petaflops)
        {
            RentedPetaflops = Math.Clamp(SimUnits.Finite(petaflops), 0.0, 5_000_000.0);
        }

        /// <summary>Convenience for callers that think in units of whatever the clouds offer today.</summary>
        public void SetRentedAcceleratorEquivalent(int units, HardwareGenerationId rentable)
        {
            if (!HardwareCatalog.TryGet(rentable, out var generation) || generation.PetaflopsPerUnit <= 0.0)
            {
                SetRentedPetaflops(0.0);
                return;
            }

            SetRentedPetaflops(Math.Max(0, units) * generation.PetaflopsPerUnit);
        }

        public void AddAsset(HardwareAsset asset)
        {
            if (asset.Units > 0)
            {
                assets.Add(asset);
            }
        }

        /// <summary>Removes a batch. Returns false when the index does not exist.</summary>
        public bool RemoveAssetAt(int index)
        {
            if (index < 0 || index >= assets.Count)
            {
                return false;
            }

            assets.RemoveAt(index);
            return true;
        }

        public bool ReplaceAssetAt(int index, HardwareAsset asset)
        {
            if (index < 0 || index >= assets.Count)
            {
                return false;
            }

            if (asset.Units <= 0)
            {
                assets.RemoveAt(index);
                return true;
            }

            assets[index] = asset;
            return true;
        }

        public void Clear()
        {
            assets.Clear();
            RentedPetaflops = 0.0;
        }

        /// <summary>Installed power capacity, summed over every tier the company has hardware in.</summary>
        public double PowerCapacityKilowatts()
        {
            var seen = new HashSet<ComputeTier>();
            var capacity = 0.0;
            foreach (var asset in assets)
            {
                if (asset.Units <= 0 || !seen.Add(asset.Tier))
                {
                    continue;
                }

                if (ComputeTierCatalog.TryGet(asset.Tier, out var tier) && !tier.IsRented)
                {
                    capacity += tier.PowerCapacityKilowatts;
                }
            }

            return capacity;
        }

        /// <summary>
        /// The whole fleet reduced to the dozen numbers the rest of the simulation needs.
        /// </summary>
        public ComputeProfile BuildProfile(GameDate date, MarketConditions market)
        {
            var ownedAccelerators = 0;
            var acceleratorsInTransit = 0;
            var ownedPetaflops = 0.0;
            var weightedCeiling = 0.0;
            var memoryGigabytes = 0.0;
            var powerDraw = 0.0;
            var operatingCost = 0.0;
            var cloudRent = 0.0;
            var electricity = 0.0;
            var housing = 0.0;
            var maintenance = 0.0;
            var depreciation = 0.0;
            var residualValue = 0L;

            var supportCapacity = new Dictionary<HardwareClass, double>
            {
                { HardwareClass.Cpu, 0.0 },
                { HardwareClass.Memory, 0.0 },
                { HardwareClass.Network, 0.0 }
            };

            foreach (var asset in assets)
            {
                if (asset.Units <= 0 || !HardwareCatalog.TryGet(asset.GenerationId, out var generation))
                {
                    continue;
                }

                // Value falls from the day the money left, not from the day the crates arrived.
                depreciation += HardwareValuation.DailyDepreciationUsd(asset, date);
                residualValue += HardwareValuation.ResidualValueUsd(asset, date);

                if (!asset.IsOnline(date))
                {
                    if (generation.Class == HardwareClass.Accelerator)
                    {
                        acceleratorsInTransit += asset.Units;
                    }

                    continue;
                }

                var tier = ComputeTierCatalog.Get(asset.Tier);
                var assetPower = generation.PowerKilowatts * asset.Units;
                powerDraw += assetPower;

                // Kept as four running totals rather than one. The sum is identical; what changes is
                // that the books can now say which bill it was.
                electricity += assetPower * SimUnits.HoursPerDay * tier.PowerCostPerKilowattHourUsd;
                housing += assetPower * tier.HousingCostPerKilowattMonthUsd / DaysPerMonth;
                maintenance += asset.TotalPurchasePriceUsd * tier.MaintenanceRatePerYear / DaysPerYear;

                if (generation.Class == HardwareClass.Accelerator)
                {
                    ownedAccelerators += asset.Units;
                    var assetPetaflops = generation.PetaflopsPerUnit * asset.Units;
                    ownedPetaflops += assetPetaflops;
                    weightedCeiling += assetPetaflops * generation.UtilizationCeiling;
                    memoryGigabytes += (double)generation.MemoryGigabytes * asset.Units;
                }
                else
                {
                    supportCapacity[generation.Class] += (double)generation.AcceleratorsServed * asset.Units;
                }
            }

            var rentedPetaflops = 0.0;
            var rentedUnits = 0;
            if (RentedPetaflops > 0.0
                && HardwareCatalog.TryGet(market.RentableGeneration, out var rented)
                && rented.PetaflopsPerUnit > 0.0)
            {
                rentedPetaflops = RentedPetaflops;
                rentedUnits = (int)Math.Ceiling(RentedPetaflops / rented.PetaflopsPerUnit);
                weightedCeiling += rentedPetaflops * rented.UtilizationCeiling;
                memoryGigabytes += (double)rented.MemoryGigabytes * rentedUnits;
                cloudRent += rentedPetaflops * market.RentPricePerPetaflopHourUsd * SimUnits.HoursPerDay;
            }

            // Packaged capacity is rented capacity with a better contract behind it. It joins here so
            // memory, utilisation and every downstream reader see one fleet rather than two.
            var packaged = PackagedPetaflops;
            if (packaged > 0.0)
            {
                weightedCeiling += packaged * 0.92;
                rentedPetaflops += packaged;
                cloudRent += PackagesDailyCostUsd;
            }

            var rawPetaflops = ownedPetaflops + rentedPetaflops;
            var ceiling = rawPetaflops > 0.0 ? weightedCeiling / rawPetaflops : 0.35;
            var totalAccelerators = ownedAccelerators + rentedUnits;
            var balance = BalanceFactor(ownedAccelerators, supportCapacity);
            var scaling = ScalingEfficiency(totalAccelerators);

            // Starving only costs the owned half. Rented capacity arrives with its hosts, memory
            // and fabric already attached, which is part of what the hourly rate pays for.
            var effectivePetaflops = (ownedPetaflops * balance + rentedPetaflops) * ceiling * scaling;

            var bill = new FleetBill(cloudRent, electricity, housing, maintenance);
            operatingCost += bill.TotalUsd;

            return new ComputeProfile(
                totalAccelerators,
                rentedUnits,
                acceleratorsInTransit,
                rawPetaflops,
                effectivePetaflops,
                ceiling,
                balance,
                scaling,
                memoryGigabytes,
                powerDraw,
                PowerCapacityKilowatts(),
                operatingCost,
                depreciation,
                residualValue,
                bill);
        }

        /// <summary>
        /// Throughput kept when a run is spread across a lot of accelerators. Doubling the cluster
        /// never quite doubles the useful FLOPs, and past a point it barely helps at all.
        /// </summary>
        public static double ScalingEfficiency(int acceleratorCount)
        {
            if (acceleratorCount <= ScalingFreeAcceleratorCount)
            {
                return 1.0;
            }

            var doublings = Math.Log(acceleratorCount / (double)ScalingFreeAcceleratorCount, 2.0);
            return Math.Clamp(1.0 - ScalingPenaltyPerDoubling * doublings, MinimumScalingEfficiency, 1.0);
        }

        /// <summary>
        /// How well fed the owned accelerators are. Rented capacity arrives fully provisioned, which
        /// is part of what the hourly rate buys; owned accelerators need hosts, memory and fabric
        /// bought alongside them or they idle.
        /// </summary>
        private static double BalanceFactor(int ownedAccelerators, IReadOnlyDictionary<HardwareClass, double> supportCapacity)
        {
            if (ownedAccelerators <= 0)
            {
                return 1.0;
            }

            var factor = 1.0;
            foreach (var pair in supportCapacity)
            {
                factor = Math.Min(factor, pair.Value / ownedAccelerators);
            }

            return Math.Clamp(factor, MinimumBalanceFactor, 1.0);
        }
    }
}
