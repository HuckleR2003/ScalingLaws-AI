using System;
using System.Collections.Generic;
using ScalingLaws.Core;
using ScalingLaws.Data;

namespace ScalingLaws.Simulation
{
    /// <summary>
    /// A model that finished training and went live.
    ///
    /// <see cref="Capability"/> here is measured, not projected: it is what the finished run actually
    /// landed on. Nothing writes a projection into this type.
    /// </summary>
    public sealed class DeployedModel
    {
        private double price = 1.0;

        public DeployedModel(
            string name,
            ArchitectureId architecture,
            double capability,
            GameDate releaseDate,
            double activeParameterCount,
            double priceMultiplier,
            ModelType type = ModelType.General,
            string family = null,
            int assaTier = 0,
            int redTeamTier = 0,
            int dataProtectionTier = -1,
            int safetyEffort = 1)
        {
            // **The protection travels with the model, not with the company.** A run hardened two
            // years ago is still the run that was hardened two years ago, and a company that has
            // since researched everything does not retroactively protect a model it shipped before
            // it knew how. That is the whole reason these live here rather than being looked up.
            AssaTier = assaTier;
            RedTeamTier = redTeamTier;
            DataProtectionTier = dataProtectionTier;
            SafetyEffort = safetyEffort;
            Name = string.IsNullOrWhiteSpace(name) ? "Untitled model" : name.Trim();
            Family = string.IsNullOrWhiteSpace(family) ? Name : family.Trim();
            Type = type == ModelType.None ? ModelType.General : type;
            Architecture = architecture;
            Capability = Math.Clamp(SimUnits.Finite(capability), 0.0, 100.0);
            ReleaseDate = releaseDate;
            ActiveParameterCount = Math.Max(1e6, SimUnits.Finite(activeParameterCount, 1e6));
            PriceMultiplier = priceMultiplier;
            Traits = ModelTraitSet.AtMarketPar(releaseDate);
        }

        /// <summary>
        /// Post-training upgrades. These keep moving after release, which is the only reason a model
        /// shipped in March is still competitive in December.
        /// </summary>
        public ModelTraitSet Traits { get; private set; }

        /// <summary>
        /// Every version of this model that has shipped, and who is on which.
        ///
        /// Seeded with the release itself the first time anybody asks, so a model that has never
        /// been updated still reads as one version at a hundred per cent rather than as an empty
        /// list the screens have to special-case.
        /// </summary>
        public ReleaseLine Line
        {
            get
            {
                if (line.Count == 0)
                {
                    line.Publish(Name, ReleaseDate, Capability, 0.0, 0.0);
                }

                return line;
            }
        }

        private ReleaseLine line = new();

        /// <summary>
        /// Writes the first version, at the terms the company was actually selling on.
        ///
        /// **Idempotent, and it has to be**, because the lazy getter above will invent a base version
        /// at zero the moment anything reads <see cref="Line"/>. Release calls this before anybody
        /// looks, so the first entry carries a real price; calling it twice is a no-op rather than a
        /// second identical release.
        /// </summary>
        public void SeedLine(double priceUsdPerMonth, double freeTokensPerDay)
        {
            if (line.Count > 0)
            {
                return;
            }

            line.Publish(Name, ReleaseDate, Capability, priceUsdPerMonth, freeTokensPerDay);
        }

        /// <summary>Puts a saved version list back, shares as written.</summary>
        public void RestoreLine(ReleaseLine saved)
        {
            if (saved is { Count: > 0 })
            {
                line = saved;
            }
        }

        /// <summary>Replaces the whole trait set. Used by save loading, not by gameplay.</summary>
        public void RestoreTraits(ModelTraitSet traits)
        {
            if (traits != null)
            {
                Traits = traits;
            }
        }

        /// <summary>
        /// What the market actually scores: the run's measured capability plus whatever the upgrades
        /// have earned above market par, minus whatever neglect has cost. Still measured, never
        /// projected. Equal to <see cref="Capability"/> on release day.
        /// </summary>
        public double EffectiveCapability(GameDate date) =>
            Math.Clamp(Capability + Traits.CapabilityBonus(date), 0.0, 100.0);

        /// <summary>Brand the upgrades earn above par, minus what falling behind costs.</summary>
        public double BrandBonus(GameDate date) => Traits.BrandBonus(date);

        /// <summary>Multiplier on serving cost from the Optimisation and Speed lines.</summary>
        public double EfficiencyMultiplier(GameDate date) => Traits.EfficiencyMultiplier(date);

        public string Name { get; }
        public ArchitectureId Architecture { get; }

        /// <summary>What it is for. Read by the demand split and by the price the market accepts.</summary>
        public ModelType Type { get; }

        /// <summary>Measured capability of the finished run. Never a projection.</summary>
        public double Capability { get; }

        public GameDate ReleaseDate { get; }

        /// <summary>Parameters that fire per token. Sets what a served token costs to produce.</summary>
        public double ActiveParameterCount { get; }

        /// <summary>
        /// How the parameters were arranged, which is a permanent property of the model.
        ///
        /// Kept here rather than recomputed because the market reads it every day: a deep model is
        /// dearer to serve at the same size, and that has to survive the run, the shelf and the save.
        /// </summary>
        public ModelShape Shape { get; private set; } = ModelShape.Balanced;

        /// <summary>Sets the shape. Called once, by whatever built this model.</summary>
        public void SetShape(ModelShape shape) => Shape = shape;

        /// <summary>Price relative to the market average. The one lever that still works on an old model.</summary>
        public double PriceMultiplier
        {
            get => price;
            set => price = Math.Clamp(SimUnits.Finite(value, 1.0), 0.05, 10.0);
        }

        /// <summary>
        /// The product line this model belongs to, or empty for a line of its own.
        /// See <see cref="ModelBlueprint.Family"/> for why only one member of a line competes.
        /// </summary>
        public string Family { get; }

        /// <summary>Models in the same line are the same product to a buyer. Empty lines never match.</summary>
        public bool SharesLineWith(DeployedModel other) =>
            other != null && Family.Length > 0
            && string.Equals(Family, other.Family, StringComparison.OrdinalIgnoreCase);

        public bool IsRetired { get; private set; }

        /// <summary>The day it came off sale, or the default date while it is still selling.</summary>
        public GameDate RetiredOn { get; private set; } = new(GameDate.MinimumDayIndex);

        public void Retire() => IsRetired = true;

        /// <summary>Retires it and records when, which is what a history page needs to draw a span.</summary>
        public void RetireOn(GameDate date)
        {
            if (IsRetired)
            {
                return;
            }

            IsRetired = true;
            RetiredOn = date;
        }

        // ---- what this model did while it was on sale ------------------------------------------
        //
        // Foundations for the model history screen. These are **records of what happened**, not
        // derived quantities: nothing can recompute what a model earned in 2024 from the state of the
        // company in 2031, so they are accumulated as it happens and they have to be saved. That is
        // the same reasoning that made LastQuality persistent, and the opposite mistake to the three
        // times something causal was dropped from a save because it looked derived.

        /// <summary>Every dollar this model has been credited with since release.</summary>
        public long LifetimeRevenueUsd { get; private set; }

        /// <summary>Days it has spent on sale. Not the calendar age: a shelved model stops counting.</summary>
        /// <inheritdoc cref="ModelBlueprint.AssaTier"/>
        public int AssaTier { get; }

        /// <inheritdoc cref="ModelBlueprint.RedTeamTier"/>
        public int RedTeamTier { get; }

        /// <inheritdoc cref="ModelBlueprint.DataProtectionTier"/>
        public int DataProtectionTier { get; }

        /// <inheritdoc cref="ModelBlueprint.SafetyEffort"/>
        public int SafetyEffort { get; }

        public int DaysOnSale { get; private set; }

        /// <summary>The most people it ever held at once.</summary>
        public double PeakUsers { get; private set; }

        /// <summary>How many days of its own trading a model keeps, which is what a banner draws.</summary>
        public const int RecentDays = 31;

        private readonly List<long> recentRevenue = new();

        /// <summary>
        /// What this model earned on each of its last thirty one days on sale, oldest first.
        ///
        /// **A record, like the lifetime figure above it, and for a sharper reason.** A day's take is
        /// a share of the company's revenue weighted by the users this model held and its capability
        /// against its siblings, and all three of those are gone by tomorrow. Nothing in the state
        /// can recover what the second product earned last Tuesday.
        ///
        /// A month rather than everything: it exists so a corner banner can show whether this
        /// product is climbing or sliding, and the chart is thirty one columns wide.
        /// </summary>
        public IReadOnlyList<long> RecentRevenueUsd => recentRevenue;

        /// <summary>Books a day of trading against this model.</summary>
        public void RecordDay(long revenueUsd, double users)
        {
            var earned = Math.Max(0L, revenueUsd);

            LifetimeRevenueUsd += earned;
            DaysOnSale++;
            PeakUsers = Math.Max(PeakUsers, Math.Max(0.0, SimUnits.Finite(users)));

            recentRevenue.Add(earned);

            while (recentRevenue.Count > RecentDays)
            {
                recentRevenue.RemoveAt(0);
            }
        }

        /// <summary>Restores the last month of trading. Only the save is allowed to call this.</summary>
        public void RestoreRecentRevenue(IEnumerable<long> days)
        {
            recentRevenue.Clear();

            if (days == null)
            {
                return;
            }

            foreach (var day in days)
            {
                recentRevenue.Add(Math.Max(0L, day));
            }

            while (recentRevenue.Count > RecentDays)
            {
                recentRevenue.RemoveAt(0);
            }
        }

        /// <summary>Restores the record on load. Only the save is allowed to call this.</summary>
        public void RestoreHistory(long revenueUsd, int daysOnSale, double peakUsers, GameDate retiredOn)
        {
            LifetimeRevenueUsd = Math.Max(0L, revenueUsd);
            DaysOnSale = Math.Max(0, daysOnSale);
            PeakUsers = Math.Max(0.0, SimUnits.Finite(peakUsers));
            RetiredOn = retiredOn;
        }

        public bool IsLiveOn(GameDate date) => !IsRetired && date.IsOnOrAfter(ReleaseDate);

        public double AgeYears(GameDate date) => Math.Max(0.0, ReleaseDate.YearsUntil(date));

        /// <summary>
        /// What a model is actually served at, as a share of the parameters that were active during
        /// training.
        ///
        /// Nobody serves the training artefact. Production traffic runs on a quantised, distilled,
        /// heavily cached descendant, and the bulk of it never touches the flagship weights at all.
        /// Without this the arithmetic says a frontier model can serve about a tenth of a percent of
        /// the demand it attracts, market share becomes decorative because everyone is capacity
        /// bound, and capability stops mattering to revenue. Which is not how the industry works.
        /// </summary>
        public const double ServingDistillationFactor = 0.15;

        /// <summary>FLOPs to produce one served token, before the architecture's serving multiplier.</summary>
        public double InferenceFlopPerToken => 2.0 * ActiveParameterCount * ServingDistillationFactor;

        public override string ToString() => $"{Name} (cap {Capability:0.0}, live {ReleaseDate})";
    }
}
