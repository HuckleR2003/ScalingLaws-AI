using System;
using System.Collections.Generic;
using NUnit.Framework;
using ScalingLaws.Data;
using ScalingLaws.Simulation;

namespace ScalingLaws.Tests.EditMode
{
    /// <summary>
    /// No two names in a saved enum may share a value.
    ///
    /// **Written after it happened twice in one afternoon.** C# is perfectly happy with two names
    /// for one value - that is what an alias is - so neither collision failed to compile and neither
    /// produced an error at runtime. What they produced was worse:
    ///
    /// * `GeneralIntelligence = 501` was already `ShardedOptimizerStates`. The research tree's
    ///   dictionary kept whichever was built last, three nodes silently became unreachable, and a
    ///   fourth reported a prerequisite dated seven years after itself.
    /// * `StateProgramme = 27` was already `GrantRepaid`. The ledger looks a line up by walking its
    ///   catalog and returning the first match, so two lines wrote into one slot and every figure in
    ///   that row was the sum of two unrelated things.
    ///
    /// Every enum here is **written into saves**. A collision that shipped would not be a bug that
    /// could be fixed later; it would be a save format in which one number means two things, and
    /// separating them afterwards means guessing which of the two any given file meant.
    /// </summary>
    public sealed class EnumIdentityTests
    {
        private static void NothingSharesAValue<T>() where T : struct, Enum
        {
            var seen = new Dictionary<int, string>();
            var clashes = new List<string>();

            foreach (var name in Enum.GetNames(typeof(T)))
            {
                var value = (int)(object)Enum.Parse<T>(name);

                if (seen.TryGetValue(value, out var first))
                {
                    clashes.Add($"{first} and {name} are both {value}");
                }
                else
                {
                    seen[value] = name;
                }
            }

            CollectionAssert.IsEmpty(clashes,
                $"{typeof(T).Name} has names sharing a value, and this enum is written into saves: "
                + string.Join("; ", clashes));
        }

        [Test]
        public void ResearchNodeIdsAreUnique() => NothingSharesAValue<ResearchNodeId>();

        [Test]
        public void LedgerLinesAreUnique() => NothingSharesAValue<LedgerLine>();

        [Test]
        public void CompanyEventTypesAreUnique() => NothingSharesAValue<CompanyEventType>();

        [Test]
        public void StateSectorsAreUnique() => NothingSharesAValue<StateSector>();

        [Test]
        public void CountriesAndRegionsAreUnique()
        {
            NothingSharesAValue<Country>();
            NothingSharesAValue<WorldRegion>();
        }

        [Test]
        public void TheThingsTheSaveWritesAsIntsAreUnique()
        {
            NothingSharesAValue<GuideStage>();
            NothingSharesAValue<IncidentSeverity>();
            NothingSharesAValue<ResearchEra>();
            NothingSharesAValue<ResearchTrack>();
            NothingSharesAValue<StaffRole>();
            NothingSharesAValue<OfficeTier>();
            NothingSharesAValue<ComputeTier>();
            NothingSharesAValue<ArchitectureId>();
            NothingSharesAValue<DatasetSource>();
            NothingSharesAValue<ModelTrait>();
            NothingSharesAValue<CompetitorId>();
        }
    }
}
