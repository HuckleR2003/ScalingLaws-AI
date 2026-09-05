using System;
using System.Collections.Generic;

namespace ScalingLaws.Data
{
    /// <summary>The three things a run can be hardened with. Each has four tiers.</summary>
    public enum SafetyModule
    {
        /// <summary>The model attacks itself offline between maintenance windows.</summary>
        Assa = 0,

        /// <summary>Somebody, or something, trying to make it break its own rules.</summary>
        RedTeam = 1,

        /// <summary>Where the users' data sits and what a leak could actually reach.</summary>
        DataProtection = 2
    }

    /// <summary>One rung of one module.</summary>
    public readonly struct SafetyTier
    {
        public SafetyTier(SafetyModule module, int tier, string icon,
            ResearchNodeId requires, int extraDays, long extraCostUsd,
            double riskReduction, double saveChance, double perModelBonus, int perModelCap)
        {
            Module = module;
            Tier = tier;
            Icon = icon;
            Requires = requires;
            ExtraDays = Math.Max(0, extraDays);
            ExtraCostUsd = Math.Max(0L, extraCostUsd);
            RiskReduction = Math.Clamp(riskReduction, 0.0, 0.95);
            SaveChance = Math.Clamp(saveChance, 0.0, 0.95);
            PerModelBonus = Math.Max(0.0, perModelBonus);
            PerModelCap = Math.Max(0, perModelCap);
        }

        public SafetyModule Module { get; }
        public int Tier { get; }

        /// <summary>
        /// The stem for everything written about this tier.
        ///
        /// **Written out rather than assembled from the module and the number.** A key built by
        /// concatenation is invisible to `LocalisationTests.EveryKeyTheInterfaceAsksForExists`,
        /// which can only read literals, and this project has already shipped a screen of raw keys
        /// once for exactly that reason.
        /// </summary>
        private static string KeyFor(SafetyModule module, int tier) => (module, tier) switch
        {
            (SafetyModule.Assa, 0) => "safety.assa0",
            (SafetyModule.Assa, 1) => "safety.assa1",
            (SafetyModule.Assa, 2) => "safety.assa2",
            (SafetyModule.Assa, 3) => "safety.assa3",
            (SafetyModule.RedTeam, 0) => "safety.red0",
            (SafetyModule.RedTeam, 1) => "safety.red1",
            (SafetyModule.RedTeam, 2) => "safety.red2",
            (SafetyModule.RedTeam, 3) => "safety.red3",
            (SafetyModule.DataProtection, 0) => "safety.data0",
            (SafetyModule.DataProtection, 1) => "safety.data1",
            (SafetyModule.DataProtection, 2) => "safety.data2",
            _ => "safety.data3"
        };

        /// <summary>Read from the book at access time, never stored. See `PlayerSkillDefinition`.</summary>
        public string DisplayName => Loc.T(KeyFor(Module, Tier));

        /// <summary>File name under `Resources/Research`. The same art the tree node uses.</summary>
        public string Icon { get; }

        /// <summary>The node that opens it. `None` means the company starts with it.</summary>
        public ResearchNodeId Requires { get; }

        /// <summary>Days added to the run. Safety is work, and work takes the calendar.</summary>
        public int ExtraDays { get; }

        public long ExtraCostUsd { get; }

        /// <summary>How much less likely an incident is to happen at all.</summary>
        public double RiskReduction { get; }

        /// <summary>Chance to walk away from a penalty that has already been decided.</summary>
        public double SaveChance { get; }

        /// <summary>Extra effect for every model on sale, up to <see cref="PerModelCap"/>.</summary>
        public double PerModelBonus { get; }

        /// <summary>Models past this add nothing. A fleet of products is not a fleet of auditors.</summary>
        public int PerModelCap { get; }

        public string Description => Loc.T(KeyFor(Module, Tier) + ".about");

        /// <summary>Risk reduction with the fleet bonus folded in.</summary>
        public double RiskReductionWith(int liveModels) =>
            Math.Clamp(RiskReduction + PerModelBonus * Math.Min(Math.Max(0, liveModels), PerModelCap)
                * (Module == SafetyModule.RedTeam ? 0.0 : 1.0), 0.0, 0.95);

        /// <summary>Save chance with the fleet bonus folded in.</summary>
        public double SaveChanceWith(int liveModels) =>
            Math.Clamp(SaveChance + PerModelBonus * Math.Min(Math.Max(0, liveModels), PerModelCap)
                * (Module == SafetyModule.RedTeam ? 1.0 : 0.0), 0.0, 0.95);
    }

    /// <summary>
    /// How hard the team worked the safety stage. Time for a little of everything.
    /// </summary>
    public readonly struct SafetyEffort
    {
        public SafetyEffort(int multiplier, double timeMultiplier, double statBonus)
        {
            Multiplier = Math.Clamp(multiplier, 1, 4);
            TimeMultiplier = Math.Max(1.0, timeMultiplier);
            StatBonus = Math.Max(0.0, statBonus);
        }

        /// <summary>1 to 4, as printed on the control.</summary>
        public int Multiplier { get; }

        /// <summary>What it does to the safety stage's own days. Never to the rest of the run.</summary>
        public double TimeMultiplier { get; }

        /// <summary>Added to every safety figure. Deliberately small.</summary>
        public double StatBonus { get; }
    }

    /// <summary>
    /// The ONE safety library.
    ///
    /// **Three modules, four tiers each, and the tiers are not a ladder of the same thing.** ASSA
    /// stops incidents happening. Red teaming does nothing about that and gives the company a chance
    /// to walk away from a penalty that has already been decided. Data protection does a little of
    /// both and is the only one aimed at the specific catastrophe that ends campaigns: a personal
    /// data leak with a nine figure fine behind it.
    ///
    /// **Only data protection is locked at tier zero.** A company knows how to point its own model at
    /// itself on day one; it does not know how to isolate user data, and that is the tier that has to
    /// be bought before any of it works.
    ///
    /// **Effort is not a fourth module and must not become one.** It buys a fraction of a percent for
    /// a large amount of calendar, so it is a thing to reach for when a run is already going to be
    /// slow rather than a lever anybody pulls by default. If it ever looks worth taking every time,
    /// the numbers are wrong.
    /// </summary>
    public static class SafetyModuleCatalog
    {
        public const string CatalogVersion = "2026.08.16";

        /// <summary>Tiers per module. Every module has exactly this many.</summary>
        public const int TierCount = 4;

        private static readonly SafetyTier[] Entries =
        {
            // ---------------------------------------------------------------- ASSA
            //
            // Automatic Self Safety Auditioning. The model is shut in a room with itself and told to
            // find the way out. Every tier is the same idea with more of it.

            new(SafetyModule.Assa, 0, "research_assa0_basic",
                ResearchNodeId.None, extraDays: 6, extraCostUsd: 100_000,
                riskReduction: 0.04, saveChance: 0.0, perModelBonus: 0.02, perModelCap: 5),

            new(SafetyModule.Assa, 1, "research_assa1_licensed",
                ResearchNodeId.LicensedStackedAssa, extraDays: 30, extraCostUsd: 480_000,
                riskReduction: 0.10, saveChance: 0.0, perModelBonus: 0.02, perModelCap: 5),

            new(SafetyModule.Assa, 2, "research_assa2_advanced",
                ResearchNodeId.AdvancedAssa, extraDays: 62, extraCostUsd: 1_900_000,
                riskReduction: 0.18, saveChance: 0.0, perModelBonus: 0.02, perModelCap: 5),

            new(SafetyModule.Assa, 3, "research_assa3_ecosystem",
                ResearchNodeId.AssaEcosystem, extraDays: 104, extraCostUsd: 6_400_000,
                riskReduction: 0.30, saveChance: 0.0, perModelBonus: 0.02, perModelCap: 5),

            // ---------------------------------------------------------------- red teaming
            //
            // These do not lower the risk of anything. They are the appeal after the verdict.

            new(SafetyModule.RedTeam, 0, "research_red0_basic_teaming",
                ResearchNodeId.None, extraDays: 4, extraCostUsd: 60_000,
                riskReduction: 0.0, saveChance: 0.025, perModelBonus: 0.005, perModelCap: 8),

            new(SafetyModule.RedTeam, 1, "research_red1_automated_teaming",
                ResearchNodeId.AutomatedRedTeaming, extraDays: 22, extraCostUsd: 420_000,
                riskReduction: 0.0, saveChance: 0.055, perModelBonus: 0.008, perModelCap: 8),

            new(SafetyModule.RedTeam, 2, "research_red2_adversarial_campaigns",
                ResearchNodeId.AdversarialCampaigns, extraDays: 48, extraCostUsd: 1_600_000,
                riskReduction: 0.0, saveChance: 0.10, perModelBonus: 0.01, perModelCap: 8),

            new(SafetyModule.RedTeam, 3, "research_red3_redteam",
                ResearchNodeId.ContinuousRedTeam, extraDays: 84, extraCostUsd: 5_200_000,
                riskReduction: 0.0, saveChance: 0.175, perModelBonus: 0.02, perModelCap: 8),

            // ---------------------------------------------------------------- data protection
            //
            // The only module whose first tier has to be bought. A company does not accidentally
            // know how to isolate personal data, and this is the line between a bad quarter and the
            // fine that ends the campaign.

            new(SafetyModule.DataProtection, 0, "research_data0_basic_isolation",
                ResearchNodeId.BasicDataIsolation, extraDays: 12, extraCostUsd: 240_000,
                riskReduction: 0.07, saveChance: 0.05, perModelBonus: 0.0, perModelCap: 0),

            new(SafetyModule.DataProtection, 1, "research_data1_encrypted_data",
                ResearchNodeId.EncryptedDataVaults, extraDays: 34, extraCostUsd: 900_000,
                riskReduction: 0.125, saveChance: 0.125, perModelBonus: 0.0, perModelCap: 0),

            new(SafetyModule.DataProtection, 2, "research_data2_differential_privacy",
                ResearchNodeId.DifferentialPrivacy, extraDays: 66, extraCostUsd: 3_100_000,
                riskReduction: 0.215, saveChance: 0.30, perModelBonus: 0.0, perModelCap: 0),

            new(SafetyModule.DataProtection, 3, "research_data3_privacy_training",
                ResearchNodeId.PrivacyPreservingTraining, extraDays: 112, extraCostUsd: 8_800_000,
                riskReduction: 0.30, saveChance: 0.50, perModelBonus: 0.0, perModelCap: 0),
        };

        /// <summary>
        /// The four efforts. `x1` is exactly the neutral option and must stay exactly 1.0.
        /// </summary>
        public static readonly SafetyEffort[] Efforts =
        {
            new(1, 1.00, 0.000),
            new(2, 1.60, 0.005),
            new(3, 2.60, 0.015),
            new(4, 3.60, 0.035)
        };

        private static readonly Dictionary<(SafetyModule, int), SafetyTier> ByKey = BuildIndex();

        public static IReadOnlyList<SafetyTier> All => Entries;

        /// <summary>Every tier of a module, tier zero first.</summary>
        public static List<SafetyTier> TiersOf(SafetyModule module)
        {
            var found = new List<SafetyTier>(TierCount);
            foreach (var entry in Entries)
            {
                if (entry.Module == module)
                {
                    found.Add(entry);
                }
            }

            found.Sort((left, right) => left.Tier.CompareTo(right.Tier));
            return found;
        }

        public static SafetyTier Get(SafetyModule module, int tier) =>
            ByKey.TryGetValue((module, Math.Clamp(tier, 0, TierCount - 1)), out var found)
                ? found
                : ByKey[(module, 0)];

        public static SafetyEffort EffortOf(int multiplier) =>
            Efforts[Math.Clamp(multiplier, 1, Efforts.Length) - 1];

        /// <summary>
        /// The name a module is shown under.
        /// </summary>
        public static string NameOf(SafetyModule module) => module switch
        {
            SafetyModule.Assa => "SELF AUDITING",
            SafetyModule.RedTeam => "RED TEAMING",
            _ => "DATA PROTECTION"
        };

        /// <summary>
        /// What the module is for, in one line, under the name.
        /// </summary>
        public static string PitchOf(SafetyModule module) => module switch
        {
            SafetyModule.Assa => "Stops incidents happening at all.",
            SafetyModule.RedTeam => "Does nothing about the risk. Talks you out of the penalty.",
            _ => "Aimed at the one leak that ends companies."
        };

        private static Dictionary<(SafetyModule, int), SafetyTier> BuildIndex()
        {
            var index = new Dictionary<(SafetyModule, int), SafetyTier>(Entries.Length);
            foreach (var entry in Entries)
            {
                index[(entry.Module, entry.Tier)] = entry;
            }

            return index;
        }
    }
}
