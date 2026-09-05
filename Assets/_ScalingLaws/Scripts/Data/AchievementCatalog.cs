using System.Collections.Generic;

namespace ScalingLaws.Data
{
    /// <summary>What an achievement is about, for grouping on a screen.</summary>
    public enum AchievementGroup
    {
        Cash = 1,
        Models = 2,
        ModelTypes = 3,
        Architecture = 4,
        Hardware = 5,
        Research = 6,
        Regulator = 7,
        Survival = 8,
        Market = 9,
        Time = 10
    }

    /// <summary>
    /// The one number an achievement watches.
    ///
    /// Kept as an enum rather than a delegate because this file is <c>Data/</c>, which holds data
    /// and lookups and no rules. Reading the number out of a campaign is a rule, so it lives in
    /// <c>Simulation/AchievementEvaluator</c> and this only names which number it wants.
    ///
    /// <see cref="NotWiredYet"/> is the honest one. Three achievements below describe something the
    /// simulation does not currently count. They carry their name, their note and their Steam id so
    /// the identifier is reserved and the copy is written, and the evaluator never awards them.
    /// </summary>
    public enum AchievementMetric
    {
        NotWiredYet = 0,
        AgenticModels,
        AsiNode,
        AutomationModels,
        BankruptciesLifetime,
        BestCapability,
        BestFamilyGeneration,
        CampaignStarted,
        CapitalSpentUsd,
        CashUsd,
        CleanFourYears,
        CodingModels,
        CompanySold,
        ConversationalModels,
        DatacenterOnline,
        DaysInDebt,
        DistinctModelTypes,
        Fans,

        /// <summary>
        /// A cabinet with a fan in it delivering more than the same cabinet packed with cards.
        ///
        /// A fact about the floor as it stands, so it is a number rather than a moment: the
        /// evaluator can work it out on any day and get the same answer.
        /// </summary>
        FanBeatsACard,
        FinesPaidUsd,
        FreeTokensBillions,

        /// <summary>
        /// Days under water, counted only while the fleet is actually busy.
        ///
        /// `DaysInDebt` alone is already watched by another entry. This is the harsher version: in
        /// debt **and** serving at load, which is the month where the cluster bills for work that
        /// is not covering itself.
        /// </summary>
        DaysInDebtAtLoad,
        FullSafetyRelease,
        Headcount,
        LabsAcquired,
        LiveModels,
        ModelsOnOwnFamily,
        OwnFamilies,
        ReleasedModels,
        ResearchNodes,
        ServerRoom,
        SuperintelligenceNodes,
        TaxPaidUsd,
        YearReached
    }

    /// <summary>
    /// Stable identity for one achievement.
    ///
    /// Values are written out rather than left implicit, for the same reason every other enum in
    /// this project does it: a member inserted in the middle would silently renumber the ones after
    /// it. Nothing persists these numbers today, because <see cref="AchievementDefinition.ApiName"/>
    /// is what the store writes, but an enum that is safe to reorder is one less thing to remember.
    /// </summary>
    public enum AchievementId
    {
        Cash1 = 1,
        Cash2 = 2,
        Cash3 = 3,
        Cash4 = 4,
        Cash5 = 5,
        Model1 = 6,
        Model2 = 7,
        Model3 = 8,
        Model4 = 9,
        Model5 = 10,
        TypeCoding = 11,
        TypeChat = 12,
        TypeAgent = 13,
        TypeAuto = 14,
        TypeAll = 15,
        Arch1 = 16,
        Arch3 = 17,
        Arch5 = 18,
        HwRoom = 19,
        HwDatacenter = 20,
        HwCapex = 21,
        HwSeven = 22,
        Res1 = 23,
        Res10 = 24,
        ResEra4 = 25,
        ResAsi = 26,
        ResAll = 27,
        RegFive = 28,
        RegClean = 29,
        RegFines = 30,
        RegSafe = 31,
        SurvDebt = 32,
        SurvBust1 = 33,
        SurvBust3 = 34,
        SurvBust10 = 35,
        Surv2036 = 36,
        SurvLoss = 37,
        MktFans = 38,
        MktLive8 = 39,
        MktTeam20 = 40,
        MktFree = 41,
        MktBuy = 42,
        TimeStart = 43,
        TimeTax = 44,
        Time2026 = 45,
        TimeFrontier = 46,
        TimeSold = 47,
    }

    /// <summary>One achievement: what it is called, what it watches, and where it stops.</summary>
    public sealed class AchievementDefinition
    {
        public AchievementDefinition(
            AchievementId id,
            string apiName,
            AchievementGroup group,
            int level,
            AchievementMetric metric,
            double threshold,
            string nameKey,
            string noteKey)
        {
            Id = id;
            ApiName = apiName;
            Group = group;
            Level = level < 1 ? 1 : level > 5 ? 5 : level;
            Metric = metric;
            Threshold = threshold;
            NameKey = nameKey;
            NoteKey = noteKey;
        }

        public AchievementId Id { get; }

        /// <summary>
        /// The identifier Steam would use, and the key the store writes.
        ///
        /// Uppercase with underscores because that is the shape Steamworks wants, and fixed forever
        /// once a build ships: a renamed API name is a new achievement and everybody who earned the
        /// old one loses it.
        /// </summary>
        public string ApiName { get; }

        public AchievementGroup Group { get; }

        /// <summary>1 to 5. Presentation only; nothing gates on it.</summary>
        public int Level { get; }

        public AchievementMetric Metric { get; }

        /// <summary>The value the metric has to reach. Booleans use 1.</summary>
        public double Threshold { get; }

        /// <summary>Phrase-book key for the title. A literal, never built by concatenation.</summary>
        public string NameKey { get; }

        /// <summary>Phrase-book key for the one-line note under the title.</summary>
        public string NoteKey { get; }

        /// <summary>True when the simulation does not yet count what this describes.</summary>
        public bool NeedsCounter => Metric == AchievementMetric.NotWiredYet;

        public override string ToString() => $"{ApiName} (level {Level})";
    }

    /// <summary>
    /// Every achievement in the game, in one table.
    ///
    /// **Nothing here decides anything.** The table names a number and a threshold; reading the
    /// number out of a campaign is <c>Simulation/AchievementEvaluator</c> and remembering what has
    /// been earned is <c>Persistence/AchievementStore</c>. Splitting it three ways is what keeps
    /// this file free of both game rules and UnityEngine.
    ///
    /// Achievements are deliberately **not** part of a save. They belong to the player rather than
    /// to one campaign, which is how Steam works and which matters in a game that is allowed to be
    /// lost: starting again does not take back what was earned.
    /// </summary>
    public static class AchievementCatalog
    {
        /// <summary>Recorded by the store, so a later table can tell what a file was written by.</summary>
        public const string CatalogVersion = "achievements-2026-09-04";

        public static IReadOnlyList<AchievementDefinition> All => Entries;

        /// <summary>The one this id names, or null. Never throws on an unknown id.</summary>
        public static AchievementDefinition Get(AchievementId id)
        {
            foreach (var entry in Entries)
            {
                if (entry.Id == id)
                {
                    return entry;
                }
            }

            return null;
        }

        /// <summary>The one this Steam name belongs to, or null.</summary>
        public static AchievementDefinition ByApiName(string apiName)
        {
            if (string.IsNullOrEmpty(apiName))
            {
                return null;
            }

            foreach (var entry in Entries)
            {
                if (entry.ApiName == apiName)
                {
                    return entry;
                }
            }

            return null;
        }

        private static readonly AchievementDefinition[] Entries =
        {
            // **The whole ladder came down once somebody measured the game.** `CampaignProbeTests`
            // plays fourteen years in five styles and the richest run peaked at $71M, so the old
            // first rung at $100M was already above every campaign anybody had played and the other
            // four were out by two and three orders of magnitude.
            //
            // These sit deliberately above the probe rather than on it: the scripted operator is
            // crude and a person beats it. The third rung lands just over its best run, the fourth
            // beyond it, and the fifth is a stretch nobody has reached. Re-measure with the probe
            // before moving any of them again.
            //
            // **The first rung has to clear `CompanyState.StartingCashUsd`.** At five million it sat
            // under the twelve the company is handed on day one, so a brand new campaign earned it
            // before doing anything, and `AFreshCompanyHasEarnedNothingButStarting` said so on the
            // first run. Twenty five million is the first figure that is an achievement rather than
            // a starting balance.
            new(AchievementId.Cash1, "ACH_CASH_1", AchievementGroup.Cash, 1,
                AchievementMetric.CashUsd, 25_000_000, "ach.cash1.name", "ach.cash1.note"),
            new(AchievementId.Cash2, "ACH_CASH_2", AchievementGroup.Cash, 2,
                AchievementMetric.CashUsd, 50_000_000, "ach.cash2.name", "ach.cash2.note"),
            new(AchievementId.Cash3, "ACH_CASH_3", AchievementGroup.Cash, 3,
                AchievementMetric.CashUsd, 75_000_000, "ach.cash3.name", "ach.cash3.note"),
            new(AchievementId.Cash4, "ACH_CASH_4", AchievementGroup.Cash, 4,
                AchievementMetric.CashUsd, 100_000_000, "ach.cash4.name", "ach.cash4.note"),
            new(AchievementId.Cash5, "ACH_CASH_5", AchievementGroup.Cash, 5,
                AchievementMetric.CashUsd, 500_000_000, "ach.cash5.name", "ach.cash5.note"),
            new(AchievementId.Model1, "ACH_MODEL_1", AchievementGroup.Models, 1,
                AchievementMetric.ReleasedModels, 1, "ach.model1.name", "ach.model1.note"),
            new(AchievementId.Model2, "ACH_MODEL_2", AchievementGroup.Models, 2,
                AchievementMetric.ReleasedModels, 5, "ach.model2.name", "ach.model2.note"),
            new(AchievementId.Model3, "ACH_MODEL_3", AchievementGroup.Models, 3,
                AchievementMetric.ModelsOnOwnFamily, 12, "ach.model3.name", "ach.model3.note"),
            new(AchievementId.Model4, "ACH_MODEL_4", AchievementGroup.Models, 4,
                AchievementMetric.ReleasedModels, 25, "ach.model4.name", "ach.model4.note"),
            new(AchievementId.Model5, "ACH_MODEL_5", AchievementGroup.Models, 5,
                AchievementMetric.ReleasedModels, 50, "ach.model5.name", "ach.model5.note"),
            new(AchievementId.TypeCoding, "ACH_TYPE_CODING", AchievementGroup.ModelTypes, 1,
                AchievementMetric.CodingModels, 1, "ach.typecoding.name", "ach.typecoding.note"),
            new(AchievementId.TypeChat, "ACH_TYPE_CHAT", AchievementGroup.ModelTypes, 1,
                AchievementMetric.ConversationalModels, 1, "ach.typechat.name", "ach.typechat.note"),
            new(AchievementId.TypeAgent, "ACH_TYPE_AGENT", AchievementGroup.ModelTypes, 2,
                AchievementMetric.AgenticModels, 1, "ach.typeagent.name", "ach.typeagent.note"),
            new(AchievementId.TypeAuto, "ACH_TYPE_AUTO", AchievementGroup.ModelTypes, 2,
                AchievementMetric.AutomationModels, 1, "ach.typeauto.name", "ach.typeauto.note"),
            new(AchievementId.TypeAll, "ACH_TYPE_ALL", AchievementGroup.ModelTypes, 3,
                AchievementMetric.DistinctModelTypes, 5, "ach.typeall.name", "ach.typeall.note"),
            new(AchievementId.Arch1, "ACH_ARCH_1", AchievementGroup.Architecture, 1,
                AchievementMetric.OwnFamilies, 1, "ach.arch1.name", "ach.arch1.note"),
            new(AchievementId.Arch3, "ACH_ARCH_3", AchievementGroup.Architecture, 3,
                AchievementMetric.BestFamilyGeneration, 3, "ach.arch3.name", "ach.arch3.note"),
            new(AchievementId.Arch5, "ACH_ARCH_5", AchievementGroup.Architecture, 4,
                AchievementMetric.BestFamilyGeneration, 5, "ach.arch5.name", "ach.arch5.note"),
            new(AchievementId.HwRoom, "ACH_HW_ROOM", AchievementGroup.Hardware, 1,
                AchievementMetric.ServerRoom, 1, "ach.hwroom.name", "ach.hwroom.note"),
            new(AchievementId.HwDatacenter, "ACH_HW_DATACENTER", AchievementGroup.Hardware, 3,
                AchievementMetric.DatacenterOnline, 1, "ach.hwdatacenter.name", "ach.hwdatacenter.note"),
            new(AchievementId.HwCapex, "ACH_HW_CAPEX", AchievementGroup.Hardware, 4,
                AchievementMetric.CapitalSpentUsd, 5_000_000_000, "ach.hwcapex.name", "ach.hwcapex.note"),
            new(AchievementId.HwSeven, "ACH_HW_SEVEN", AchievementGroup.Hardware, 3,
                AchievementMetric.FanBeatsACard, 1, "ach.hwseven.name", "ach.hwseven.note"),
            new(AchievementId.Res1, "ACH_RES_1", AchievementGroup.Research, 1,
                AchievementMetric.ResearchNodes, 1, "ach.res1.name", "ach.res1.note"),
            new(AchievementId.Res10, "ACH_RES_10", AchievementGroup.Research, 2,
                AchievementMetric.ResearchNodes, 10, "ach.res10.name", "ach.res10.note"),
            new(AchievementId.ResEra4, "ACH_RES_ERA4", AchievementGroup.Research, 3,
                AchievementMetric.SuperintelligenceNodes, 1, "ach.resera4.name", "ach.resera4.note"),
            new(AchievementId.ResAsi, "ACH_RES_ASI", AchievementGroup.Research, 5,
                AchievementMetric.AsiNode, 1, "ach.resasi.name", "ach.resasi.note"),
            new(AchievementId.ResAll, "ACH_RES_ALL", AchievementGroup.Research, 5,
                // **Read off the tree rather than written down.** The copy said "all fifty" and
                // the tree has had fifty five and then fifty nine, so the number was wrong within
                // a day of being typed. Minus one for the starting node, which every campaign has
                // before it researches anything.
                AchievementMetric.ResearchNodes, ResearchTree.All.Count - 1,
                "ach.resall.name", "ach.resall.note"),
            new(AchievementId.RegFive, "ACH_REG_FIVE", AchievementGroup.Regulator, 2,
                AchievementMetric.NotWiredYet, 0, "ach.regfive.name", "ach.regfive.note"),
            new(AchievementId.RegClean, "ACH_REG_CLEAN", AchievementGroup.Regulator, 4,
                AchievementMetric.CleanFourYears, 1, "ach.regclean.name", "ach.regclean.note"),
            new(AchievementId.RegFines, "ACH_REG_FINES", AchievementGroup.Regulator, 2,
                AchievementMetric.FinesPaidUsd, 1_000_000_000, "ach.regfines.name", "ach.regfines.note"),
            new(AchievementId.RegSafe, "ACH_REG_SAFE", AchievementGroup.Regulator, 3,
                AchievementMetric.FullSafetyRelease, 1, "ach.regsafe.name", "ach.regsafe.note"),
            new(AchievementId.SurvDebt, "ACH_SURV_DEBT", AchievementGroup.Survival, 2,
                AchievementMetric.DaysInDebt, 30, "ach.survdebt.name", "ach.survdebt.note"),
            new(AchievementId.SurvBust1, "ACH_SURV_BUST_1", AchievementGroup.Survival, 1,
                AchievementMetric.BankruptciesLifetime, 1, "ach.survbust1.name", "ach.survbust1.note"),
            new(AchievementId.SurvBust3, "ACH_SURV_BUST_3", AchievementGroup.Survival, 3,
                AchievementMetric.BankruptciesLifetime, 3, "ach.survbust3.name", "ach.survbust3.note"),
            new(AchievementId.SurvBust10, "ACH_SURV_BUST_10", AchievementGroup.Survival, 5,
                AchievementMetric.BankruptciesLifetime, 10, "ach.survbust10.name", "ach.survbust10.note"),
            new(AchievementId.Surv2036, "ACH_SURV_2036", AchievementGroup.Survival, 5,
                AchievementMetric.YearReached, 2036, "ach.surv2036.name", "ach.surv2036.note"),
            new(AchievementId.SurvLoss, "ACH_SURV_LOSS", AchievementGroup.Survival, 3,
                AchievementMetric.DaysInDebtAtLoad, 30, "ach.survloss.name", "ach.survloss.note"),
            // Measured best across the probe: 32,134. Same reasoning as the cash ladder.
            new(AchievementId.MktFans, "ACH_MKT_FANS", AchievementGroup.Market, 2,
                AchievementMetric.Fans, 100_000, "ach.mktfans.name", "ach.mktfans.note"),
            new(AchievementId.MktLive8, "ACH_MKT_LIVE8", AchievementGroup.Market, 4,
                AchievementMetric.LiveModels, 8, "ach.mktlive8.name", "ach.mktlive8.note"),
            new(AchievementId.MktTeam20, "ACH_MKT_TEAM20", AchievementGroup.Market, 2,
                AchievementMetric.Headcount, 20, "ach.mktteam20.name", "ach.mktteam20.note"),
            new(AchievementId.MktFree, "ACH_MKT_FREE", AchievementGroup.Market, 3,
                AchievementMetric.FreeTokensBillions, 1000, "ach.mktfree.name", "ach.mktfree.note"),
            new(AchievementId.MktBuy, "ACH_MKT_BUY", AchievementGroup.Market, 4,
                AchievementMetric.LabsAcquired, 1, "ach.mktbuy.name", "ach.mktbuy.note"),
            new(AchievementId.TimeStart, "ACH_TIME_START", AchievementGroup.Time, 1,
                AchievementMetric.CampaignStarted, 1, "ach.timestart.name", "ach.timestart.note"),
            new(AchievementId.TimeTax, "ACH_TIME_TAX", AchievementGroup.Time, 2,
                AchievementMetric.TaxPaidUsd, 1, "ach.timetax.name", "ach.timetax.note"),
            new(AchievementId.Time2026, "ACH_TIME_2026", AchievementGroup.Time, 3,
                AchievementMetric.YearReached, 2026, "ach.time2026.name", "ach.time2026.note"),
            new(AchievementId.TimeFrontier, "ACH_TIME_FRONTIER", AchievementGroup.Time, 5,
                AchievementMetric.BestCapability, 72, "ach.timefrontier.name", "ach.timefrontier.note"),
            new(AchievementId.TimeSold, "ACH_TIME_SOLD", AchievementGroup.Time, 4,
                AchievementMetric.CompanySold, 1, "ach.timesold.name", "ach.timesold.note"),
        };
    }
}
