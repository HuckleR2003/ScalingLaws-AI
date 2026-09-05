using System;
using System.Collections.Generic;
using ScalingLaws.Core;
using ScalingLaws.Data;

namespace ScalingLaws.Simulation
{
    /// <summary>A thing that is true about the company for a while, and then is not.</summary>
    public enum ModelEffectKind
    {
        None = 0,

        /// <summary>Something caught. More people are arriving than the product explains.</summary>
        Viral = 1,

        /// <summary>The clean slate a company gets exactly once.</summary>
        FirstRelease = 2,

        /// <summary>A year with no penalty and no scandal. People trust you with their data.</summary>
        SafeHarbour = 3,

        /// <summary>Something went wrong in public and people are leaving over it.</summary>
        Backlash = 4,

        /// <summary>A campaign is running. Presentation only: marketing does its own work.</summary>
        Campaign = 5
    }

    /// <summary>
    /// One temporary effect: what it is, when it started, how long it lasts, how hard it pulls.
    ///
    /// **Everything here expires.** That is the whole point of the type. A permanent modifier is a
    /// balance change; a modifier with a date on it is a window the player can decide what to do
    /// with, which is what turns "I went viral" from a number into "I went viral, do I raise the
    /// price or ship the next model while people are looking".
    /// </summary>
    public sealed class ModelEffect
    {
        public ModelEffect(ModelEffectKind kind, GameDate startedOn, int days, double magnitude,
            int modelIndex = -1)
        {
            Kind = kind;
            StartedOn = startedOn;
            Days = Math.Clamp(days, 1, 4000);
            Magnitude = Math.Clamp(SimUnits.Finite(magnitude), -0.95, 3.0);
            ModelIndex = modelIndex;
        }

        public ModelEffectKind Kind { get; }
        public GameDate StartedOn { get; }
        public int Days { get; }

        /// <summary>Signed. Positive pulls people in, negative pushes them out.</summary>
        public double Magnitude { get; }

        /// <summary>Which live model this is about, or -1 when it is about the company.</summary>
        public int ModelIndex { get; }

        public int DaysLeft(GameDate today) =>
            Math.Max(0, Days - Math.Max(0, today.DayIndex - StartedOn.DayIndex));

        public bool IsActive(GameDate today) => DaysLeft(today) > 0;

        /// <summary>
        /// How much of the effect is still working, 0 to 1.
        ///
        /// **It fades rather than stopping.** A viral spike that ends on a Tuesday, at full
        /// strength, is a cliff in the user chart that no real product has ever had. The last
        /// quarter of the window tapers, so the shape on the graph is a rise and a slide.
        /// </summary>
        public double Strength(GameDate today)
        {
            var left = DaysLeft(today);

            if (left <= 0)
            {
                return 0.0;
            }

            var taper = Math.Max(1, Days / 4);
            return left >= taper ? 1.0 : left / (double)taper;
        }

        /// <summary>What to multiply demand by while this is running.</summary>
        public double Multiplier(GameDate today) => 1.0 + Magnitude * Strength(today);

        /// <summary>
        /// How far the company's guess at the remaining time can be from the truth, either way.
        ///
        /// The author's call, and it is a design decision rather than a rounding: **nobody inside a
        /// company knows how long a wave of attention or a bad week is going to last.** A badge that
        /// counted down exactly would turn a story into a timer, and a player would plan around the
        /// last day of it the way they plan around a training run.
        /// </summary>
        public const double EstimateTolerance = 0.40;

        /// <summary>
        /// What the company thinks is left, which is not what is left.
        ///
        /// **The skew is fixed for the life of the effect, not rolled per repaint.** A figure that
        /// re-randomised every frame would read as a broken counter rather than as an estimate, and
        /// one that re-randomised per day would let a player average it out over a week and recover
        /// the true number. Derived from what makes this effect this effect, so it is the same guess
        /// every time anything asks, survives a save, and replays identically.
        ///
        /// Not `string.GetHashCode` and not `DeterministicRandom`: the first is randomised per
        /// process, so the same window would read differently after a restart, and the second is a
        /// stream whose position would then depend on how often the interface asked.
        ///
        /// Because the skew is a multiplier, the guess converges on the truth as the window closes
        /// and the badge disappears when the effect really ends. A player told ninety days and cut
        /// off at sixty five has learned something true about estimates.
        /// </summary>
        public int EstimatedDaysLeft(GameDate today)
        {
            var left = DaysLeft(today);

            if (left <= 0)
            {
                return 0;
            }

            return Math.Max(1, (int)Math.Round(left * (1.0 + Skew)));
        }

        /// <summary>Between -EstimateTolerance and +EstimateTolerance, fixed for this effect.</summary>
        private double Skew
        {
            get
            {
                unchecked
                {
                    var seed = (uint)((int)Kind + 1) * 2654435761u
                        + (uint)(StartedOn.DayIndex + 1) * 40503u;

                    seed ^= seed >> 13;
                    seed *= 2246822519u;
                    seed ^= seed >> 15;

                    var unit = seed % 10_000u / 10_000.0;
                    return (unit * 2.0 - 1.0) * EstimateTolerance;
                }
            }
        }

        public override string ToString() =>
            $"{Kind} {Magnitude:+0.0%;-0.0%} for {Days}d from {StartedOn}";
    }

    /// <summary>
    /// Everything currently true about the company that will stop being true.
    ///
    /// One list rather than a field per effect, because the set of effects is going to grow and a
    /// boolean per kind is a second place to forget to expire something. Expiry happens in exactly
    /// one method and every reader asks the same question.
    /// </summary>
    public sealed class EffectBook
    {
        /// <summary>Days with no penalty and no scandal before the company earns Safe Harbour.</summary>
        /// <summary>
        /// A clean record this long earns Safe Harbour, and an incident restarts the clock.
        ///
        /// **Two years, and it does two jobs.** It is what has to be survived to earn the effect in
        /// the first place, and it is the wait after losing one: an incident sets the clock back to
        /// that day, so the earliest it can return is two years later. Raised from one year when the
        /// duration came down from ten, because a penalty of "wait a year" against an effect worth
        /// two is not a penalty.
        /// </summary>
        public const int SafeHarbourDays = 730;

        /// <summary>
        /// How long a grant lasts before it has to be earned again.
        ///
        /// **Was 3,650, which is most of a campaign.** A playtest earned it and was told it had
        /// 2,930 days left, at which point it is not something you keep, it is something you own.
        /// Two years, and `AwardSafeHarbourIfEarned` re-grants the day after it lapses to a company
        /// that has stayed out of trouble, so the renewal costs no code and no click.
        /// </summary>
        public const int SafeHarbourGrantDays = 730;

        /// <summary>What a clean year is worth: people arriving, and staying.</summary>
        public const double SafeHarbourMagnitude = 0.10;

        /// <summary>The window a company gets for being new, and what it is worth.</summary>
        public const int FirstReleaseDaysLow = 60;
        public const int FirstReleaseDaysHigh = 180;
        public const double FirstReleaseMagnitude = 0.20;

        /// <summary>
        /// How long a backlash runs: four months to thirteen, drawn on the day it starts.
        ///
        /// **The spread is the point.** It used to be a function of severity alone, so a player who
        /// had seen one severe incident knew exactly how long the next one would last and could plan
        /// the release calendar around it. A regulator's decision is not a countdown you can read
        /// off a table, and the whole reason `EstimatedDaysLeft` exists is that the company does not
        /// get to know either.
        ///
        /// Author's call, 2026-09-03. It was 63 to 113 days.
        /// </summary>
        public const int BacklashDaysLow = 122;

        public const int BacklashDaysHigh = 396;

        /// <summary>
        /// How much of the fan base the worst backlash can take, as a share of what they would
        /// otherwise be.
        ///
        /// **It moves the target, not the stock, and that distinction is the whole feature.** Taking
        /// fans directly would be a fine the player pays and cannot answer. Lowering what the fan
        /// base is pulling toward means the ordinary things that earn a following — serving people
        /// well, being liked, shipping something worth talking about — genuinely fight it, and a
        /// company that works through a bad year keeps more of its people than one that sulks.
        ///
        /// Fans drift down at `Standing.FanDecayPerDay`, so a quarter off the target is not a
        /// quarter off the count on day one. Over four months it is about a seventh of the way
        /// there, over thirteen most of it: the length drawn above decides how much of the damage
        /// actually lands.
        /// </summary>
        public const double BacklashFanLoss = 0.25;

        /// <summary>The magnitude a backlash reaches at the worst severity, for scaling the above.</summary>
        public const double BacklashWorstMagnitude = 0.34;

        private readonly List<ModelEffect> effects = new();

        public IReadOnlyList<ModelEffect> All => effects;

        /// <summary>Only the ones still running, newest first.</summary>
        public List<ModelEffect> Active(GameDate today)
        {
            var live = new List<ModelEffect>();

            for (var index = effects.Count - 1; index >= 0; index--)
            {
                if (effects[index].IsActive(today))
                {
                    live.Add(effects[index]);
                }
            }

            return live;
        }

        public bool Has(ModelEffectKind kind, GameDate today)
        {
            foreach (var effect in effects)
            {
                if (effect.Kind == kind && effect.IsActive(today))
                {
                    return true;
                }
            }

            return false;
        }

        public ModelEffect Find(ModelEffectKind kind, GameDate today)
        {
            foreach (var effect in effects)
            {
                if (effect.Kind == kind && effect.IsActive(today))
                {
                    return effect;
                }
            }

            return null;
        }

        /// <summary>
        /// Adds an effect, replacing any live one of the same kind.
        ///
        /// **One of each at a time.** Two viral windows stacking would multiply, and two campaigns
        /// backfiring at once would read as the game being broken rather than as the player having
        /// been unlucky twice.
        /// </summary>
        public void Add(ModelEffect effect, GameDate today)
        {
            if (effect == null)
            {
                return;
            }

            effects.RemoveAll(existing => existing.Kind == effect.Kind);
            effects.Add(effect);
        }

        /// <summary>Drops everything that has run its course. Called once a day.</summary>
        public void Advance(GameDate today) =>
            effects.RemoveAll(effect => !effect.IsActive(today));

        /// <summary>
        /// Ends one kind early.
        ///
        /// Safe Harbour is the reason this exists: twelve months of nothing going wrong, and one
        /// incident takes it. An effect that limped to its natural end after the thing it described
        /// stopped being true would be the game lying.
        /// </summary>
        public bool End(ModelEffectKind kind) => effects.RemoveAll(effect => effect.Kind == kind) > 0;

        /// <summary>
        /// Everything multiplied together: what demand is doing beyond what the product explains.
        ///
        /// Campaign is skipped because marketing already moves awareness on its own, and counting
        /// it here as well would pay for the same spend twice.
        /// </summary>
        public double DemandMultiplier(GameDate today)
        {
            var total = 1.0;

            foreach (var effect in effects)
            {
                if (effect.Kind == ModelEffectKind.Campaign || !effect.IsActive(today))
                {
                    continue;
                }

                total *= effect.Multiplier(today);
            }

            return Math.Clamp(SimUnits.Finite(total, 1.0), 0.15, 4.0);
        }

        /// <summary>
        /// How much of the fan base a live backlash is holding down, 0 to
        /// <see cref="BacklashFanLoss"/>.
        ///
        /// Applied to `Standing.FanTarget` rather than to the count, so the player's own work pulls
        /// against it every day. Scaled by how bad the incident was and tapered by the effect's own
        /// `Strength`, so it fades out on the same curve the demand penalty does rather than ending
        /// on a Tuesday.
        /// </summary>
        public double FanPressure(GameDate today)
        {
            var backlash = Find(ModelEffectKind.Backlash, today);

            if (backlash == null)
            {
                return 0.0;
            }

            var severity = Math.Clamp(
                Math.Abs(backlash.Magnitude) / BacklashWorstMagnitude, 0.0, 1.0);

            return Math.Clamp(
                BacklashFanLoss * severity * backlash.Strength(today), 0.0, BacklashFanLoss);
        }

        /// <summary>Restores a loaded campaign.</summary>
        public void Restore(IReadOnlyList<int> kinds, IReadOnlyList<int> startDays,
            IReadOnlyList<int> days, IReadOnlyList<double> magnitudes, IReadOnlyList<int> modelIndices)
        {
            effects.Clear();

            if (kinds == null)
            {
                return;
            }

            for (var index = 0; index < kinds.Count; index++)
            {
                if (!Enum.IsDefined(typeof(ModelEffectKind), kinds[index]))
                {
                    continue;
                }

                var kind = (ModelEffectKind)kinds[index];

                if (kind == ModelEffectKind.None)
                {
                    continue;
                }

                effects.Add(new ModelEffect(
                    kind,
                    new GameDate(startDays != null && index < startDays.Count
                        ? Math.Max(0, startDays[index]) : 0),
                    days != null && index < days.Count ? days[index] : 1,
                    magnitudes != null && index < magnitudes.Count ? magnitudes[index] : 0.0,
                    modelIndices != null && index < modelIndices.Count ? modelIndices[index] : -1));
            }
        }

        /// <summary>The name of an effect, for a badge.</summary>
        public static string NameOf(ModelEffectKind kind) => kind switch
        {
            ModelEffectKind.Viral => Loc.T("effect.viral"),
            ModelEffectKind.FirstRelease => Loc.T("effect.first"),
            ModelEffectKind.SafeHarbour => Loc.T("effect.harbour"),
            ModelEffectKind.Backlash => Loc.T("effect.backlash"),
            ModelEffectKind.Campaign => Loc.T("effect.campaign"),
            _ => string.Empty
        };

        /// <summary>And what it is doing, which is the half a badge cannot show.</summary>
        public static string NoteFor(ModelEffectKind kind) => kind switch
        {
            ModelEffectKind.Viral => Loc.T("effect.viral.note"),
            ModelEffectKind.FirstRelease => Loc.T("effect.first.note"),
            ModelEffectKind.SafeHarbour => Loc.T("effect.harbour.note"),
            ModelEffectKind.Backlash => Loc.T("effect.backlash.note"),
            ModelEffectKind.Campaign => Loc.T("effect.campaign.note"),
            _ => string.Empty
        };

        /// <summary>True when this one is bad news, so a badge can colour itself.</summary>
        public static bool IsBad(ModelEffectKind kind) => kind == ModelEffectKind.Backlash;
    }
}
