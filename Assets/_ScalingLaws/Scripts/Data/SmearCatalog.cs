using System.Collections.Generic;

namespace ScalingLaws.Data
{
    /// <summary>How loudly the company is willing to be seen doing this.</summary>
    public enum SmearTier
    {
        /// <summary>Anonymous posts in the places people already argue.</summary>
        Whisper = 0,

        /// <summary>A journalist is given a story and a direction to take it in.</summary>
        Briefing = 1,

        /// <summary>A commissioned study with a conclusion decided before the work started.</summary>
        Report = 2,

        /// <summary>Bought coverage across every channel at once, for a fortnight.</summary>
        Campaign = 3
    }

    public readonly struct SmearDefinition
    {
        public SmearDefinition(SmearTier tier, long costUsd, double brandDamage,
            double backfireChance, double relationCost, int quietDays)
        {
            Tier = tier;
            CostUsd = costUsd;
            BrandDamage = brandDamage;
            BackfireChance = backfireChance;
            RelationCost = relationCost;
            QuietDays = quietDays;
        }

        public SmearTier Tier { get; }
        public long CostUsd { get; }

        /// <summary>What it takes off the target's standing when it lands.</summary>
        public double BrandDamage { get; }

        /// <summary>The chance it is traced back and lands on the company that paid for it.</summary>
        public double BackfireChance { get; }

        /// <summary>What it costs the relationship, and it is charged whatever the outcome.</summary>
        public double RelationCost { get; }

        /// <summary>How long before this lab can be targeted again.</summary>
        public int QuietDays { get; }

        public string DisplayName => Loc.T(NameKey);
        public string Note => Loc.T(NoteKey);

        public string NameKey => Tier switch
        {
            SmearTier.Whisper => "smear.whisper.name",
            SmearTier.Briefing => "smear.briefing.name",
            SmearTier.Report => "smear.report.name",
            _ => "smear.campaign.name"
        };

        public string NoteKey => Tier switch
        {
            SmearTier.Whisper => "smear.whisper.note",
            SmearTier.Briefing => "smear.briefing.note",
            SmearTier.Report => "smear.report.note",
            _ => "smear.campaign.note"
        };
    }

    /// <summary>
    /// Paying to make a competitor look worse, and the four sizes it comes in.
    ///
    /// **Every axis climbs together and none of them crosses.** A dearer tier does more damage, is
    /// more likely to be traced back, and costs the relationship more. That is the whole design:
    /// there is no clever tier, only an appetite for risk, and `NoTierIsSimplyBetterThanAnother`
    /// fails if one is ever cheaper and safer and stronger at once.
    ///
    /// **Cheap tiers are far better value per dollar and that is deliberate.** Whispering buys about
    /// ten times the standing per dollar that a bought campaign does. What the expensive tiers buy
    /// is *magnitude now*, the same trade the marketing channels make, and a company that needs a
    /// rival damaged this quarter cannot get there by whispering harder.
    ///
    /// This is the one system in the game where the honest play and the effective play come apart,
    /// so the interface says the backfire chance out loud rather than hiding it in a tooltip.
    /// </summary>
    public static class SmearCatalog
    {
        /// <summary>
        /// What a backfire costs, as a multiple of the damage that was aimed at the target.
        ///
        /// **Above one on purpose.** Being caught paying for this has to hurt more than the thing
        /// would have gained, or the expected value makes it a straightforward purchase and the
        /// decision stops being a decision.
        /// </summary>
        public const double BackfireSeverity = 1.5;

        private static readonly SmearDefinition[] Entries =
        {
            new(SmearTier.Whisper, 40_000, 0.020, 0.06, -6.0, 30),
            new(SmearTier.Briefing, 180_000, 0.050, 0.14, -12.0, 60),
            new(SmearTier.Report, 650_000, 0.100, 0.26, -20.0, 120),
            new(SmearTier.Campaign, 2_200_000, 0.180, 0.42, -30.0, 240)
        };

        public static IReadOnlyList<SmearDefinition> All => Entries;

        public static SmearDefinition Get(SmearTier tier)
        {
            foreach (var entry in Entries)
            {
                if (entry.Tier == tier)
                {
                    return entry;
                }
            }

            return Entries[0];
        }
    }
}
