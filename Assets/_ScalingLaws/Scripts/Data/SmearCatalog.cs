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

        // ---- and what the lab on the other end does about it ---------------------------------------
        //
        // **Only when it was traced back.** A smear that lands still costs the relationship, because
        // they know; nobody files a case on knowing. Proof is what the backfire roll is, and keeping
        // the two apart is what stops the backfire from being the same penalty charged twice.

        /// <summary>
        /// What their lawyers will take to drop it, as a share of what they would ask a court for.
        ///
        /// Settling has to be the cheap answer or nobody would ever take it, and it has to cost
        /// something real or refusing would not be a decision. A third is both.
        /// </summary>
        public const double SettlementShare = 0.34;

        /// <summary>
        /// What they ask a court for, per point of standing the campaign was aimed at taking.
        ///
        /// Read off the damage rather than off the price paid, because a court is pricing the harm
        /// and not the invoice. A whisper is worth about half a million and a bought campaign about
        /// five, which is real money to a young company and survivable to a large one.
        /// </summary>
        public const long DamagesPerBrandPointUsd = 27_000_000;

        /// <summary>Nothing smaller than this is worth anybody's filing fee.</summary>
        public const long LeastDamagesUsd = 400_000;

        /// <summary>
        /// The chance they go to court after being refused.
        ///
        /// **Not a certainty, and it should not be.** Refusing is a real gamble rather than a
        /// delayed bill: most of the time the letter is the whole of it, which is what makes the
        /// times it is not worth being afraid of.
        /// </summary>
        public const double SuitChanceAfterRefusal = 0.45;

        /// <summary>
        /// The chance they file anyway when the letter is simply ignored.
        ///
        /// Higher than a refusal, because a refusal is an answer and silence is not. This is the one
        /// number here that is about manners rather than about money.
        /// </summary>
        public const double SuitChanceAfterSilence = 0.60;

        /// <summary>What a traced smear costs the relationship on top of the campaign's own charge.</summary>
        public const double TracedRelationCost = -14.0;

        /// <summary>What they would ask a court for over a campaign of this size.</summary>
        public static long DamagesFor(SmearTier tier)
        {
            var damage = Get(tier).BrandDamage;

            return System.Math.Max(LeastDamagesUsd,
                (long)System.Math.Round(damage * DamagesPerBrandPointUsd));
        }

        /// <summary>What they would take today to drop it.</summary>
        public static long SettlementFor(SmearTier tier) =>
            System.Math.Max(LeastDamagesUsd / 2L,
                (long)System.Math.Round(DamagesFor(tier) * SettlementShare));

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
