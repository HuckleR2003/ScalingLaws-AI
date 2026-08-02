using System;
using System.Collections.Generic;
using ScalingLaws.Core;

namespace ScalingLaws.Data
{
    /// <summary>
    /// The offline hardware library. Pure data plus lookups, no economics: pricing lives in
    /// ComputeTierCatalog and depreciation lives in HardwareValuation.
    ///
    /// This is the ONE hardware data source for the game. Extend the tables below, never start a
    /// second library (the rule that keeps PC Workman maintainable, and the reason it has one
    /// compatibility engine instead of six).
    ///
    /// Numbers are public vendor specifications, rounded:
    ///   - accelerator throughput is dense BF16 petaflop/s per unit
    ///   - price is the launch list price of one unit in USD
    ///   - power is sustained board power under a training load, in kilowatts
    ///   - memory is on-package memory per unit in GB
    /// Entries dated after 2026 are marked IsProjection: they are roadmap extrapolation, not
    /// shipped product, and the UI must say so.
    /// </summary>
    public static class HardwareCatalog
    {
        /// <summary>Bump when the tables change. Saves record it so a load can tell what it was built against.</summary>
        public const string CatalogVersion = "2026.08.02";

        private static readonly HardwareGeneration[] Entries = BuildEntries();
        private static readonly Dictionary<HardwareGenerationId, HardwareGeneration> ById = BuildIndex();

        public static IReadOnlyList<HardwareGeneration> All => Entries;

        public static bool TryGet(HardwareGenerationId id, out HardwareGeneration generation)
        {
            return ById.TryGetValue(id, out generation);
        }

        public static HardwareGeneration Get(HardwareGenerationId id)
        {
            if (!ById.TryGetValue(id, out var generation))
            {
                throw new ArgumentOutOfRangeException(nameof(id), id, "Unknown hardware generation.");
            }

            return generation;
        }

        public static IEnumerable<HardwareGeneration> OfClass(HardwareClass hardwareClass)
        {
            foreach (var entry in Entries)
            {
                if (entry.Class == hardwareClass)
                {
                    yield return entry;
                }
            }
        }

        /// <summary>Everything of a class that can actually be ordered on a given day.</summary>
        public static IEnumerable<HardwareGeneration> AvailableOn(GameDate date, HardwareClass hardwareClass)
        {
            foreach (var entry in Entries)
            {
                if (entry.Class == hardwareClass && entry.IsAvailableOn(date))
                {
                    yield return entry;
                }
            }
        }

        /// <summary>
        /// The best part of a class on sale that day, ranked by raw throughput for accelerators and
        /// by how many accelerators it can feed for everything else.
        /// </summary>
        public static bool TryGetFrontier(GameDate date, HardwareClass hardwareClass, out HardwareGeneration frontier)
        {
            frontier = default;
            var found = false;
            foreach (var entry in AvailableOn(date, hardwareClass))
            {
                if (!found)
                {
                    frontier = entry;
                    found = true;
                    continue;
                }

                var better = hardwareClass == HardwareClass.Accelerator
                    ? entry.PetaflopsPerUnit > frontier.PetaflopsPerUnit
                    : entry.AcceleratorsServed > frontier.AcceleratorsServed;
                if (better)
                {
                    frontier = entry;
                }
            }

            return found;
        }

        /// <summary>
        /// How many strictly newer parts of the same class have shipped since a generation launched.
        /// This is what actually kills resale value, not calendar age on its own.
        /// </summary>
        public static int CountSuccessorsReleased(HardwareGenerationId id, GameDate asOf)
        {
            if (!ById.TryGetValue(id, out var generation))
            {
                return 0;
            }

            var count = 0;
            foreach (var entry in Entries)
            {
                if (entry.Class != generation.Class || entry.Id == generation.Id)
                {
                    continue;
                }

                if (entry.ReleaseDate > generation.ReleaseDate && entry.IsAvailableOn(asOf))
                {
                    count++;
                }
            }

            return count;
        }

        /// <summary>
        /// The next accelerator generation to ship strictly after a date. This is what a patient
        /// competitor, and a player paying for intelligence, is actually looking at: the launch that
        /// makes waiting worth more than shipping now.
        /// </summary>
        public static bool TryGetNextAcceleratorLaunch(GameDate after, out HardwareGeneration next)
        {
            next = default;
            var found = false;
            foreach (var entry in Entries)
            {
                if (entry.Class != HardwareClass.Accelerator || entry.ReleaseDate <= after)
                {
                    continue;
                }

                if (!found || entry.ReleaseDate < next.ReleaseDate)
                {
                    next = entry;
                    found = true;
                }
            }

            return found;
        }

        private static Dictionary<HardwareGenerationId, HardwareGeneration> BuildIndex()
        {
            var index = new Dictionary<HardwareGenerationId, HardwareGeneration>(Entries.Length);
            foreach (var entry in Entries)
            {
                index[entry.Id] = entry;
            }

            return index;
        }

        private static HardwareGeneration[] BuildEntries()
        {
            return new[]
            {
                // Accelerators. Throughput is dense BF16; the FP8 and FP4 numbers vendors quote are
                // marketing for this simulation's purposes and are not used.
                Accelerator(HardwareGenerationId.AcceleratorV100, "NVIDIA", "V100 SXM2 32GB",
                    2017, 6, 21, 0.125, 10_000, 0.30, 32, 0.28, 730, false),
                Accelerator(HardwareGenerationId.AcceleratorA100, "NVIDIA", "A100 SXM4 80GB",
                    2020, 11, 16, 0.312, 15_000, 0.40, 80, 0.36, 730, false),
                Accelerator(HardwareGenerationId.AcceleratorH100, "NVIDIA", "H100 SXM5",
                    2022, 10, 1, 0.989, 30_000, 0.70, 80, 0.42, 730, false),
                Accelerator(HardwareGenerationId.AcceleratorH200, "NVIDIA", "H200 SXM",
                    2024, 2, 1, 0.989, 32_000, 0.70, 141, 0.44, 730, false),
                Accelerator(HardwareGenerationId.AcceleratorB200, "NVIDIA", "B200",
                    2025, 1, 15, 2.250, 40_000, 1.00, 192, 0.48, 700, false),
                Accelerator(HardwareGenerationId.AcceleratorGb300, "NVIDIA", "GB300",
                    2026, 3, 1, 3.300, 48_000, 1.40, 288, 0.50, 700, false),
                Accelerator(HardwareGenerationId.AcceleratorVr200, "NVIDIA", "VR200",
                    2027, 6, 1, 6.500, 55_000, 1.80, 384, 0.52, 660, true),
                Accelerator(HardwareGenerationId.AcceleratorNext, "NVIDIA", "Next generation",
                    2029, 1, 1, 13.000, 62_000, 2.20, 576, 0.54, 660, true),

                // Host CPUs. They do not add FLOPs, they keep accelerators fed. Starve them and the
                // cluster runs at a fraction of its rated throughput.
                Support(HardwareGenerationId.CpuIceLake, HardwareClass.Cpu, "Intel", "Xeon Ice Lake-SP",
                    2021, 4, 6, 8, 9_000, 0.27, 1095, false),
                Support(HardwareGenerationId.CpuMilan, HardwareClass.Cpu, "AMD", "EPYC Milan",
                    2021, 3, 15, 8, 8_000, 0.28, 1095, false),
                Support(HardwareGenerationId.CpuGenoa, HardwareClass.Cpu, "AMD", "EPYC Genoa",
                    2022, 11, 10, 12, 11_000, 0.36, 1095, false),
                Support(HardwareGenerationId.CpuEmeraldRapids, HardwareClass.Cpu, "Intel", "Xeon Emerald Rapids",
                    2023, 12, 14, 12, 10_500, 0.35, 1095, false),
                Support(HardwareGenerationId.CpuTurin, HardwareClass.Cpu, "AMD", "EPYC Turin",
                    2024, 10, 10, 16, 13_000, 0.40, 1095, false),
                Support(HardwareGenerationId.CpuNext, HardwareClass.Cpu, "AMD", "Next generation host",
                    2027, 1, 1, 24, 15_000, 0.45, 1095, true),

                // Node memory. Sets how much of a dataset stays hot next to the accelerators.
                Support(HardwareGenerationId.MemoryDdr4, HardwareClass.Memory, "Generic", "DDR4 node kit",
                    2016, 1, 1, 4, 1_800, 0.05, 1095, false),
                Support(HardwareGenerationId.MemoryDdr5, HardwareClass.Memory, "Generic", "DDR5 node kit",
                    2021, 11, 4, 8, 2_600, 0.06, 1095, false),
                Support(HardwareGenerationId.MemoryDdr5Dense, HardwareClass.Memory, "Generic", "DDR5 high density kit",
                    2024, 3, 1, 12, 3_400, 0.07, 1095, false),
                Support(HardwareGenerationId.MemoryCxl, HardwareClass.Memory, "Generic", "CXL pooled memory",
                    2026, 1, 1, 20, 4_200, 0.08, 1095, false),

                // Fabric. The reason a 20000 accelerator run is not simply twenty 1000 accelerator runs.
                Support(HardwareGenerationId.NetworkIb200, HardwareClass.Network, "NVIDIA", "InfiniBand HDR 200G",
                    2020, 11, 16, 32, 32_000, 0.40, 1460, false),
                Support(HardwareGenerationId.NetworkIb400, HardwareClass.Network, "NVIDIA", "InfiniBand NDR 400G",
                    2022, 11, 1, 64, 48_000, 0.55, 1460, false),
                Support(HardwareGenerationId.NetworkIb800, HardwareClass.Network, "NVIDIA", "InfiniBand XDR 800G",
                    2024, 6, 1, 128, 72_000, 0.80, 1460, false),
                Support(HardwareGenerationId.NetworkOptical1600, HardwareClass.Network, "Generic", "Co-packaged optics 1.6T",
                    2026, 9, 1, 256, 95_000, 1.00, 1460, true)
            };
        }

        private static HardwareGeneration Accelerator(
            HardwareGenerationId id,
            string vendor,
            string name,
            int year,
            int month,
            int day,
            double petaflops,
            long priceUsd,
            double kilowatts,
            int memoryGigabytes,
            double utilizationCeiling,
            int valueHalfLifeDays,
            bool isProjection)
        {
            return new HardwareGeneration(
                id,
                HardwareClass.Accelerator,
                vendor,
                name,
                GameDate.FromCalendar(year, month, day),
                petaflops,
                0,
                priceUsd,
                kilowatts,
                memoryGigabytes,
                utilizationCeiling,
                valueHalfLifeDays,
                isProjection);
        }

        private static HardwareGeneration Support(
            HardwareGenerationId id,
            HardwareClass hardwareClass,
            string vendor,
            string name,
            int year,
            int month,
            int day,
            int acceleratorsServed,
            long priceUsd,
            double kilowatts,
            int valueHalfLifeDays,
            bool isProjection)
        {
            return new HardwareGeneration(
                id,
                hardwareClass,
                vendor,
                name,
                GameDate.FromCalendar(year, month, day),
                0.0,
                acceleratorsServed,
                priceUsd,
                kilowatts,
                0,
                0.5,
                valueHalfLifeDays,
                isProjection);
        }
    }
}
