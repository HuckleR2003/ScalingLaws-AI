using System;
using System.Collections.Generic;

namespace ScalingLaws.Data
{
    /// <summary>
    /// A part of a country's machinery the company's models can be put in charge of.
    ///
    /// Values are written into saves and must never be renumbered.
    /// </summary>
    public enum StateSector
    {
        None = 0,

        /// <summary>Forms, permits, benefits, tax. The first one, and the one nobody objects to.</summary>
        Bureaucracy = 1,

        /// <summary>Freight, ports, power grid scheduling. Enormous, and it fails visibly.</summary>
        Logistics = 2,

        /// <summary>Fiscal modelling and the central bank's forecast.</summary>
        Economy = 3,

        /// <summary>Triage, rota planning, epidemiology.</summary>
        Health = 4,

        /// <summary>Curriculum, assessment, where the teachers go.</summary>
        Education = 5,

        /// <summary>Policing, fraud, border control. The one with a body count when it is wrong.</summary>
        Security = 6,

        /// <summary>Treaty drafting, negotiation modelling, what to say and when.</summary>
        Diplomacy = 7,

        /// <summary>Doctrine, procurement, threat assessment. Nobody comes back from this one.</summary>
        Defence = 8
    }

    /// <summary>
    /// One sector, and what putting a model in charge of it is worth and costs.
    ///
    /// **Every field is a lever that already exists.** The fee joins the books as revenue, the
    /// petaflops come out of the same pool that serves customers, the megawatts join the same power
    /// bill the server room pays, and the failure lands through the same penalty path an incident
    /// does. Nothing here is a second economy; it is the existing one at a scale where the numbers
    /// stop being about users.
    /// </summary>
    public sealed class StateSectorDefinition
    {
        public StateSectorDefinition(StateSector sector, string nameKey, string blurbKey,
            long feeUsdPerDay, double petaflopsRequired, double megawattsRequired,
            long failureCostUsd, double failureWeight, int researchPoints, long researchCashUsd,
            StateSector[] requires = null)
        {
            Sector = sector;
            this.nameKey = nameKey;
            this.blurbKey = blurbKey;

            FeeUsdPerDay = Math.Max(0L, feeUsdPerDay);
            PetaflopsRequired = Math.Max(0.0, petaflopsRequired);
            MegawattsRequired = Math.Max(0.0, megawattsRequired);
            FailureCostUsd = Math.Max(0L, failureCostUsd);
            FailureWeight = Math.Clamp(failureWeight, 0.0, 4.0);
            ResearchPoints = Math.Max(0, researchPoints);
            ResearchCashUsd = Math.Max(0L, researchCashUsd);

            Requires = requires ?? Array.Empty<StateSector>();
        }

        private readonly string nameKey;
        private readonly string blurbKey;

        public StateSector Sector { get; }

        /// <summary>Resolved per read, so a language change mid-campaign reaches it.</summary>
        public string DisplayName => Loc.T(nameKey);

        public string Blurb => Loc.T(blurbKey);

        /// <summary>What the state pays for this sector, every day, on top of the base contract.</summary>
        public long FeeUsdPerDay { get; }

        /// <summary>
        /// Capacity this sector holds, permanently.
        ///
        /// **Taken off the top, before customers.** A state does not queue behind consumer traffic,
        /// and that is the entire trade the endgame is built on: every sector adopted is capacity
        /// the paying public no longer has, so the company has to keep building or watch its own
        /// market walk while it collects a government fee.
        /// </summary>
        public double PetaflopsRequired { get; }

        /// <summary>Power drawn around the clock, into the same bill the fleet already pays.</summary>
        public double MegawattsRequired { get; }

        /// <summary>What one failure in this sector costs. State scale, not user scale.</summary>
        public long FailureCostUsd { get; }

        /// <summary>
        /// How much this sector adds to the chance of something going wrong.
        ///
        /// Deliberately not proportional to the fee. Bureaucracy pays modestly and almost never
        /// fails; Defence pays enormously and is the most dangerous thing a company can agree to.
        /// A player reading only the fee column is being invited to make exactly that mistake.
        /// </summary>
        public double FailureWeight { get; }

        /// <summary>Research points to teach the models this sector's work.</summary>
        public int ResearchPoints { get; }

        /// <summary>And the cash alongside it. Small next to the points, as everywhere in the tree.</summary>
        public long ResearchCashUsd { get; }

        /// <summary>Sectors that have to be running first. A state does not start with its army.</summary>
        public IReadOnlyList<StateSector> Requires { get; }

        /// <summary>Dollars of daily fee per petaflop held. The number the board sorts on.</summary>
        public double FeePerPetaflop =>
            PetaflopsRequired <= 0.0 ? 0.0 : FeeUsdPerDay / PetaflopsRequired;
    }

    /// <summary>
    /// The eight sectors, and the shape of the decision they make together.
    ///
    /// **This is the end of the game and it is deliberately a trap with a ladder in it.** Each
    /// sector is more money than anything else in the campaign has ever paid, and each one takes
    /// capacity away from the customers who got the company here, draws power that shows up on the
    /// same bill as the basement, and raises the chance of a failure whose price is measured in
    /// billions rather than in reputation.
    ///
    /// The ordering is not by value. Bureaucracy is the safe one, Defence is the one that ends
    /// campaigns, and the fee column alone would tell a player to take Defence first.
    ///
    /// **`NoSectorIsSimplyBetterThanAnother` is the guard**, the same rule the marketing channels
    /// and the smear tiers are held to: the moment one sector pays more, costs less and is safer
    /// than another, the board stops being a decision and becomes a shopping list in a fixed order.
    /// </summary>
    public static class StateSectorCatalog
    {
        public const string CatalogVersion = "sectors-2026-09-04";

        /// <summary>
        /// What the contract pays before a single sector is running.
        ///
        /// A retainer for being the company the state chose. Large enough that signing is worth it
        /// on its own, small enough that it is not the endgame by itself.
        /// </summary>
        public const long BaseFeeUsdPerDay = 1_400_000L;

        /// <summary>Capacity the contract holds before any sector, for the state's own systems.</summary>
        public const double BasePetaflops = 240.0;

        private static readonly StateSectorDefinition[] Entries =
        {
            // ---- the safe end -------------------------------------------------------------------
            new(StateSector.Bureaucracy, "sector.bureaucracy", "sector.bureaucracy.blurb",
                feeUsdPerDay: 900_000L, petaflopsRequired: 180.0, megawattsRequired: 14.0,
                failureCostUsd: 700_000_000L, failureWeight: 0.35,
                researchPoints: 2_600, researchCashUsd: 40_000_000L),

            new(StateSector.Education, "sector.education", "sector.education.blurb",
                feeUsdPerDay: 1_100_000L, petaflopsRequired: 260.0, megawattsRequired: 20.0,
                failureCostUsd: 1_200_000_000L, failureWeight: 0.50,
                researchPoints: 3_100, researchCashUsd: 55_000_000L,
                requires: new[] { StateSector.Bureaucracy }),

            // ---- the middle ---------------------------------------------------------------------
            new(StateSector.Health, "sector.health", "sector.health.blurb",
                feeUsdPerDay: 2_400_000L, petaflopsRequired: 520.0, megawattsRequired: 41.0,
                failureCostUsd: 4_500_000_000L, failureWeight: 1.10,
                researchPoints: 5_200, researchCashUsd: 120_000_000L,
                requires: new[] { StateSector.Bureaucracy }),

            new(StateSector.Logistics, "sector.logistics", "sector.logistics.blurb",
                feeUsdPerDay: 3_100_000L, petaflopsRequired: 780.0, megawattsRequired: 62.0,
                failureCostUsd: 3_800_000_000L, failureWeight: 0.85,
                researchPoints: 5_800, researchCashUsd: 140_000_000L,
                requires: new[] { StateSector.Bureaucracy }),

            new(StateSector.Economy, "sector.economy", "sector.economy.blurb",
                feeUsdPerDay: 4_600_000L, petaflopsRequired: 900.0, megawattsRequired: 71.0,
                failureCostUsd: 9_000_000_000L, failureWeight: 1.45,
                researchPoints: 7_400, researchCashUsd: 210_000_000L,
                requires: new[] { StateSector.Logistics }),

            // ---- the end nobody comes back from ---------------------------------------------------
            new(StateSector.Security, "sector.security", "sector.security.blurb",
                feeUsdPerDay: 5_200_000L, petaflopsRequired: 1_150.0, megawattsRequired: 92.0,
                failureCostUsd: 12_000_000_000L, failureWeight: 1.90,
                researchPoints: 8_600, researchCashUsd: 260_000_000L,
                requires: new[] { StateSector.Bureaucracy, StateSector.Health }),

            new(StateSector.Diplomacy, "sector.diplomacy", "sector.diplomacy.blurb",
                feeUsdPerDay: 6_400_000L, petaflopsRequired: 1_020.0, megawattsRequired: 80.0,
                failureCostUsd: 15_000_000_000L, failureWeight: 2.20,
                researchPoints: 9_800, researchCashUsd: 300_000_000L,
                requires: new[] { StateSector.Economy }),

            new(StateSector.Defence, "sector.defence", "sector.defence.blurb",
                feeUsdPerDay: 9_500_000L, petaflopsRequired: 1_600.0, megawattsRequired: 128.0,
                failureCostUsd: 28_000_000_000L, failureWeight: 3.40,
                researchPoints: 13_500, researchCashUsd: 480_000_000L,
                requires: new[] { StateSector.Security, StateSector.Diplomacy })
        };

        public static IReadOnlyList<StateSectorDefinition> All => Entries;

        public static StateSectorDefinition Get(StateSector sector)
        {
            foreach (var entry in Entries)
            {
                if (entry.Sector == sector)
                {
                    return entry;
                }
            }

            return Entries[0];
        }

        /// <summary>
        /// Everything a country would pay if the company ran all of it.
        ///
        /// Quoted on the board as the horizon rather than as a target: taking all eight is possible
        /// and is almost certainly the run that ends in a bankruptcy nobody saw coming, because the
        /// failure weights add up faster than the fees do.
        /// </summary>
        public static long EverythingPerDay()
        {
            var total = BaseFeeUsdPerDay;

            foreach (var entry in Entries)
            {
                total += entry.FeeUsdPerDay;
            }

            return total;
        }

        /// <summary>And what all eight would hold, which is the number that ends companies.</summary>
        public static double EverythingPetaflops()
        {
            var total = BasePetaflops;

            foreach (var entry in Entries)
            {
                total += entry.PetaflopsRequired;
            }

            return total;
        }
    }
}
