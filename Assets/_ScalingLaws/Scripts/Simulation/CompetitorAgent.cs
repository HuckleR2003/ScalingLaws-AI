using System;
using System.Collections.Generic;
using ScalingLaws.Core;
using ScalingLaws.Data;

namespace ScalingLaws.Simulation
{
    /// <summary>A rival's live product, as the market sees it. No internals.</summary>
    public readonly struct RivalModel
    {
        public RivalModel(
            CompetitorId competitor,
            string displayName,
            double capability,
            double brandStrength,
            double priceMultiplier,
            GameDate releaseDate)
        {
            Competitor = competitor;
            DisplayName = displayName ?? string.Empty;
            Capability = Math.Clamp(SimUnits.Finite(capability), 0.0, 100.0);
            BrandStrength = Math.Clamp(SimUnits.Finite(brandStrength), 0.0, 1.0);
            PriceMultiplier = Math.Clamp(SimUnits.Finite(priceMultiplier, 1.0), 0.05, 20.0);
            ReleaseDate = releaseDate;
        }

        public CompetitorId Competitor { get; }
        public string DisplayName { get; }
        public double Capability { get; }
        public double BrandStrength { get; }
        public double PriceMultiplier { get; }
        public GameDate ReleaseDate { get; }

        public override string ToString() => $"{DisplayName} (cap {Capability:0.0})";
    }

    /// <summary>
    /// One rival lab, playing its own game.
    ///
    /// The historical release table is its plan, not its script. Every day the agent looks at what
    /// is coming and can move off the plan in two directions:
    ///
    ///   wait      a new accelerator generation lands soon after the planned launch, so a patient
    ///             lab holds, trains on the better silicon and comes out meaningfully stronger
    ///   rush      the player just shipped something that beats it badly, so a proud lab pulls the
    ///             launch forward and ships something less finished than it wanted to
    ///
    /// Between launches capability still drifts upward, because rivals run the same upgrade grid the
    /// player does. That is what makes the frontier a slope rather than a staircase.
    /// </summary>
    public sealed class CompetitorAgent
    {
        /// <summary>How far ahead a lab looks for a hardware launch worth waiting for.</summary>
        public const int HardwareWatchWindowDays = 210;

        /// <summary>Days after a launch before the new silicon has produced a better model.</summary>
        public const int HardwareRampDays = 150;

        /// <summary>Capability a successful wait is worth. Roughly a generation of compute.</summary>
        public const double PatientWaitBonus = 4.5;

        /// <summary>Capability given up by pulling a launch forward.</summary>
        public const double RushPenalty = 2.5;

        /// <summary>Player lead that provokes a rushed response.</summary>
        public const double RushTriggerGap = 6.0;

        public const int MaximumRushDays = 60;

        /// <summary>Capability a lab gains per day from its own post-release upgrades.</summary>
        public const double DriftPerDay = 0.0055;

        /// <summary>Ceiling on drift between two launches, so upgrades never replace a new model.</summary>
        public const double MaximumDrift = 4.0;

        private readonly Queue<CompetitorRelease> plan = new();

        public CompetitorAgent(CompetitorId competitor, string labName, CompetitorStrategy strategy)
        {
            Competitor = competitor;
            LabName = string.IsNullOrWhiteSpace(labName) ? competitor.ToString() : labName;
            Strategy = strategy;
        }

        public CompetitorId Competitor { get; }
        public string LabName { get; }
        public CompetitorStrategy Strategy { get; }

        public bool HasShipped { get; private set; }
        public string LiveModelName { get; private set; } = string.Empty;
        public double LiveCapability { get; private set; }
        public double LiveBrand { get; private set; }
        public double LivePrice { get; private set; } = 1.0;
        public GameDate LiveReleaseDate { get; private set; }

        /// <summary>Days the lab has pushed its next launch back to wait for better hardware.</summary>
        public int AccumulatedDelayDays { get; private set; }

        /// <summary>True while the lab is deliberately sitting out a hardware transition.</summary>
        public bool IsWaitingForHardware { get; private set; }

        public HardwareGenerationId WaitingFor { get; private set; } = HardwareGenerationId.None;

        public GameDate NextReleaseDate { get; private set; }
        public bool HasPlannedRelease { get; private set; }

        private CompetitorRelease pending;
        private double pendingCapabilityAdjustment;
        private double drift;

        public void QueuePlan(CompetitorRelease release) => plan.Enqueue(release);

        /// <summary>Capability the market sees today, including drift since the last launch.</summary>
        public double CurrentCapability(GameDate date)
        {
            if (!HasShipped)
            {
                return 0.0;
            }

            var elapsed = Math.Max(0, date.DayIndex - LiveReleaseDate.DayIndex);
            drift = Math.Min(MaximumDrift, elapsed * DriftPerDay);
            return Math.Clamp(LiveCapability + drift, 0.0, 100.0);
        }

        public bool TryGetLiveModel(GameDate date, out RivalModel model)
        {
            if (!HasShipped)
            {
                model = default;
                return false;
            }

            model = new RivalModel(
                Competitor,
                LiveModelName,
                CurrentCapability(date),
                LiveBrand,
                LivePrice,
                LiveReleaseDate);
            return true;
        }

        /// <summary>
        /// One day of decision making. Returns true on a day the lab shipped something.
        /// </summary>
        public bool Think(GameDate date, double playerCapability, DeterministicRandom random)
        {
            EnsurePending(date, random);

            if (!HasPlannedRelease)
            {
                return false;
            }

            ConsiderWaitingForHardware(date);
            ConsiderRushing(date, playerCapability, random);

            if (date < NextReleaseDate)
            {
                return false;
            }

            Ship(date);
            return true;
        }

        private void EnsurePending(GameDate date, DeterministicRandom random)
        {
            if (HasPlannedRelease)
            {
                return;
            }

            if (plan.Count > 0)
            {
                pending = plan.Dequeue();
                NextReleaseDate = pending.ReleaseDate;
                pendingCapabilityAdjustment = 0.0;
                HasPlannedRelease = true;
                return;
            }

            // The table has run out. The lab keeps going on its own cadence rather than stopping,
            // because the race does not end when the reference data does.
            if (!HasShipped)
            {
                return;
            }

            var cadence = CadenceDays();
            var gain = CapabilityGainPerRelease() * random.NextRange(0.75, 1.25);
            pending = new CompetitorRelease(
                Competitor,
                $"{LabName} next",
                date.AddDays(cadence),
                Math.Clamp(LiveCapability + drift + gain, 0.0, 100.0),
                LiveBrand,
                LivePrice,
                isProjection: true);
            NextReleaseDate = pending.ReleaseDate;
            pendingCapabilityAdjustment = 0.0;
            HasPlannedRelease = true;
        }

        /// <summary>
        /// The mechanic that makes rivals feel like they know something. A lab that is not in a
        /// hurry checks whether better silicon lands soon after its planned launch, and if it does,
        /// it holds. The player who ships into that window looks ahead for a season and then gets
        /// overtaken by something that was waiting on purpose.
        /// </summary>
        private void ConsiderWaitingForHardware(GameDate date)
        {
            if (IsWaitingForHardware || !IsPatient() || date >= NextReleaseDate)
            {
                return;
            }

            if (NextReleaseDate.DayIndex - date.DayIndex > HardwareWatchWindowDays)
            {
                return;
            }

            if (!HardwareCatalog.TryGetNextAcceleratorLaunch(date, out var launch))
            {
                return;
            }

            var launchIsAfterPlan = launch.ReleaseDate > NextReleaseDate;
            var launchIsCloseEnough = launch.ReleaseDate.DayIndex - NextReleaseDate.DayIndex <= HardwareWatchWindowDays;
            if (!launchIsAfterPlan || !launchIsCloseEnough)
            {
                return;
            }

            var newDate = launch.ReleaseDate.AddDays(HardwareRampDays);
            AccumulatedDelayDays += newDate.DayIndex - NextReleaseDate.DayIndex;
            NextReleaseDate = newDate;
            pendingCapabilityAdjustment += PatientWaitBonus;
            IsWaitingForHardware = true;
            WaitingFor = launch.Id;
        }

        /// <summary>A lab that has just been beaten badly stops polishing and ships.</summary>
        private void ConsiderRushing(GameDate date, double playerCapability, DeterministicRandom random)
        {
            if (!IsProud() || date >= NextReleaseDate)
            {
                return;
            }

            var lead = playerCapability - CurrentCapability(date);
            if (lead < RushTriggerGap || !random.NextChance(0.02))
            {
                return;
            }

            var pullForward = Math.Min(MaximumRushDays, NextReleaseDate.DayIndex - date.DayIndex);
            if (pullForward <= 0)
            {
                return;
            }

            NextReleaseDate = NextReleaseDate.AddDays(-pullForward);
            pendingCapabilityAdjustment -= RushPenalty;
            IsWaitingForHardware = false;
            WaitingFor = HardwareGenerationId.None;
        }

        private void Ship(GameDate date)
        {
            LiveModelName = pending.DisplayName;
            LiveCapability = Math.Clamp(pending.Capability + pendingCapabilityAdjustment, 0.0, 100.0);
            LiveBrand = pending.BrandStrength;
            LivePrice = pending.PriceMultiplier;
            LiveReleaseDate = date;
            HasShipped = true;
            drift = 0.0;

            HasPlannedRelease = false;
            IsWaitingForHardware = false;
            WaitingFor = HardwareGenerationId.None;
            pendingCapabilityAdjustment = 0.0;
        }

        private bool IsPatient() =>
            Strategy == CompetitorStrategy.PatientScaler || Strategy == CompetitorStrategy.EnterpriseFocus;

        private bool IsProud() =>
            Strategy == CompetitorStrategy.FrontierRace || Strategy == CompetitorStrategy.FastFollower;

        private int CadenceDays() => Strategy switch
        {
            CompetitorStrategy.FrontierRace => 210,
            CompetitorStrategy.PatientScaler => 330,
            CompetitorStrategy.CostLeader => 260,
            CompetitorStrategy.OpenWeights => 300,
            CompetitorStrategy.EnterpriseFocus => 350,
            _ => 270
        };

        private double CapabilityGainPerRelease() => Strategy switch
        {
            CompetitorStrategy.FrontierRace => 3.6,
            CompetitorStrategy.PatientScaler => 5.4,
            CompetitorStrategy.CostLeader => 2.8,
            CompetitorStrategy.OpenWeights => 3.2,
            CompetitorStrategy.EnterpriseFocus => 3.4,
            _ => 3.0
        };

        /// <summary>Restores a loaded agent without replaying its history.</summary>
        public void Restore(
            bool hasShipped,
            string liveModelName,
            double liveCapability,
            double liveBrand,
            double livePrice,
            GameDate liveReleaseDate,
            GameDate nextReleaseDate,
            bool hasPlannedRelease,
            int accumulatedDelayDays,
            bool isWaitingForHardware)
        {
            HasShipped = hasShipped;
            LiveModelName = liveModelName ?? string.Empty;
            LiveCapability = Math.Clamp(SimUnits.Finite(liveCapability), 0.0, 100.0);
            LiveBrand = Math.Clamp(SimUnits.Finite(liveBrand), 0.0, 1.0);
            LivePrice = Math.Clamp(SimUnits.Finite(livePrice, 1.0), 0.05, 20.0);
            LiveReleaseDate = liveReleaseDate;
            NextReleaseDate = nextReleaseDate;
            AccumulatedDelayDays = Math.Max(0, accumulatedDelayDays);
            IsWaitingForHardware = isWaitingForHardware;

            // The pending release itself is never serialized: it is re-pulled from the plan queue on
            // the next tick, which the field has already wound forward to the right position.
            // Trusting a saved flag here would let a restored agent ship an empty release.
            _ = hasPlannedRelease;
            HasPlannedRelease = false;
        }

        /// <summary>Drops any queued plan. Used when a save rebuilds the field from scratch.</summary>
        public void ClearPlan()
        {
            plan.Clear();
            HasPlannedRelease = false;
        }

        /// <summary>
        /// Discards planned releases that already happened before a date, so a loaded campaign does
        /// not ship 2023 models in 2026. Everything still ahead of the date stays queued.
        /// </summary>
        public void SkipPlannedReleasesUpTo(GameDate date)
        {
            while (plan.Count > 0 && plan.Peek().ReleaseDate <= date)
            {
                plan.Dequeue();
            }
        }

        /// <summary>Number of planned releases still ahead. Diagnostic, and used by tests.</summary>
        public int PlannedReleasesRemaining => plan.Count;

        public override string ToString() =>
            $"{LabName} [{Strategy}] cap {LiveCapability:0.0}, next {NextReleaseDate}";
    }
}
