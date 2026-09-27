using System;
using System.Collections.Generic;
using ScalingLaws.Core;
using ScalingLaws.Data;

namespace ScalingLaws.Simulation
{
    /// <summary>Why a relation moved, and by how much. One line of a company's memory.</summary>
    public readonly struct RelationEntry
    {
        public RelationEntry(CompetitorId lab, GameDate date, double delta, string reasonKey,
            string subject)
        {
            Lab = lab;
            Date = date;
            Delta = SimUnits.Finite(delta);
            this.reasonKey = reasonKey;
            Subject = subject ?? string.Empty;
        }

        private readonly string reasonKey;

        public CompetitorId Lab { get; }
        public GameDate Date { get; }

        /// <summary>Signed. Negative is something you did to them.</summary>
        public double Delta { get; }

        /// <summary>A name the sentence needs: a person, a model, a campaign.</summary>
        public string Subject { get; }

        /// <summary>What happened, in a sentence, resolved when it is read.</summary>
        public string Reason => string.IsNullOrEmpty(Subject)
            ? Loc.T(reasonKey)
            : Loc.T(reasonKey, Subject);

        /// <summary>The key itself, for a test that wants to know which thing happened.</summary>
        public string ReasonKey => reasonKey;
    }

    /// <summary>
    /// What every other lab thinks of you, and why.
    ///
    /// **The history is the feature, not the number.** A relation of minus sixty-three is a fact
    /// nobody can act on. "Minus sixty-three, and eleven of that was the researcher you called in
    /// 2024" is a company the player remembers, and it is the difference between a hostility meter
    /// and a grudge.
    ///
    /// Relations drift back toward neutral, slowly, because companies forget. They do not forget
    /// fast enough for that to be a strategy: at <see cref="DriftPerDay"/> a serious insult takes
    /// most of a year to fade, which is long enough that the player plans around it rather than
    /// waits it out.
    /// </summary>
    public sealed class RivalRelations
    {
        // **The scale itself lives in `Data/RelationScale`**, because the offers catalogue has
        // to name a band and `Data/` may not depend on `Simulation/`. These forward rather than
        // being deleted, so no caller anywhere had to move, and there is still exactly one place
        // each number is written down.
        public const double Worst = RelationScale.Worst;
        public const double Best = RelationScale.Best;
        public const double Start = RelationScale.Start;
        public const double CousinBaseline = RelationScale.CousinBaseline;
        public const double DriftPerDay = RelationScale.DriftPerDay;
        public const double FriendlyAbove = RelationScale.FriendlyAbove;
        public const double NeutralAbove = RelationScale.NeutralAbove;
        public const double TenseAbove = RelationScale.TenseAbove;
        public const double HostileAbove = RelationScale.HostileAbove;

        public static double BaselineFor(CompetitorId lab) => RelationScale.BaselineFor(lab);

        /// <summary>Entries kept. Past this it is an archive nobody reads, not a memory.</summary>
        public const int HistoryKept = 40;

        private readonly Dictionary<CompetitorId, double> standing = new();
        private readonly List<RelationEntry> history = new();

        /// <summary>Everything that ever moved a relation, newest last.</summary>
        public IReadOnlyList<RelationEntry> History => history;

        /// <summary>Where a lab stands. Neutral for anybody nothing has happened with.</summary>
        public double With(CompetitorId lab) =>
            standing.TryGetValue(lab, out var value) ? value : BaselineFor(lab);

        public RelationBand BandWith(CompetitorId lab) => BandFor(With(lab));

        public static RelationBand BandFor(double value) => RelationScale.BandFor(value);

        public static string NameOf(RelationBand band) => RelationScale.NameOf(band);

        /// <summary>What a band means for how that lab behaves, in one sentence.</summary>
        public static string NoteFor(RelationBand band) => RelationScale.NoteFor(band);

        /// <summary>
        /// Moves a relation and records why.
        ///
        /// **Nothing may move a relation without a reason.** The reason is the whole mechanic: a
        /// number that changed for no stated cause is indistinguishable from a bug, and the player
        /// has no way to learn what they did.
        /// </summary>
        public void Record(CompetitorId lab, GameDate date, double delta, string reasonKey,
            string subject = "")
        {
            if (string.IsNullOrEmpty(reasonKey))
            {
                throw new ArgumentException(
                    "A relation cannot move without a reason the player can read.", nameof(reasonKey));
            }

            var moved = SimUnits.Finite(delta);

            if (Math.Abs(moved) < 0.0001)
            {
                return;
            }

            standing[lab] = Math.Clamp(With(lab) + moved, Worst, Best);
            history.Add(new RelationEntry(lab, date, moved, reasonKey, subject));

            if (history.Count > HistoryKept)
            {
                history.RemoveAt(0);
            }
        }

        /// <summary>Everything that happened with one lab, newest first.</summary>
        public List<RelationEntry> HistoryWith(CompetitorId lab)
        {
            var found = new List<RelationEntry>();

            for (var index = history.Count - 1; index >= 0; index--)
            {
                if (history[index].Lab == lab)
                {
                    found.Add(history[index]);
                }
            }

            return found;
        }

        /// <summary>
        /// A day of forgetting. Everything moves toward neutral and nothing crosses it.
        ///
        /// The drift is not recorded in the history. Time passing is not a thing that happened.
        /// </summary>
        public void Advance()
        {
            if (standing.Count == 0)
            {
                return;
            }

            var labs = new List<CompetitorId>(standing.Keys);

            foreach (var lab in labs)
            {
                var value = standing[lab];
                var home = BaselineFor(lab);
                var gap = value - home;

                if (Math.Abs(gap) <= DriftPerDay)
                {
                    standing[lab] = home;
                    continue;
                }

                standing[lab] = value - Math.Sign(gap) * DriftPerDay;
            }
        }

        /// <summary>Every lab anything has ever happened with.</summary>
        public IEnumerable<CompetitorId> Known => standing.Keys;

        /// <summary>Restores a saved campaign. Values are clamped; unknown labs are dropped.</summary>
        public void Restore(IReadOnlyList<int> labs, IReadOnlyList<double> values,
            IReadOnlyList<int> historyLabs, IReadOnlyList<int> historyDays,
            IReadOnlyList<double> historyDeltas, IReadOnlyList<string> historyKeys,
            IReadOnlyList<string> historySubjects)
        {
            standing.Clear();
            history.Clear();

            if (labs != null && values != null)
            {
                for (var index = 0; index < labs.Count && index < values.Count; index++)
                {
                    if (!Enum.IsDefined(typeof(CompetitorId), labs[index]))
                    {
                        continue;
                    }

                    standing[(CompetitorId)labs[index]] =
                        Math.Clamp(SimUnits.Finite(values[index]), Worst, Best);
                }
            }

            if (historyLabs == null || historyKeys == null)
            {
                return;
            }

            for (var index = 0; index < historyLabs.Count; index++)
            {
                if (!Enum.IsDefined(typeof(CompetitorId), historyLabs[index])
                    || index >= historyKeys.Count
                    || string.IsNullOrEmpty(historyKeys[index]))
                {
                    continue;
                }

                var day = historyDays != null && index < historyDays.Count ? historyDays[index] : 0;
                var delta = historyDeltas != null && index < historyDeltas.Count
                    ? historyDeltas[index]
                    : 0.0;

                var subject = historySubjects != null && index < historySubjects.Count
                    ? historySubjects[index]
                    : string.Empty;

                history.Add(new RelationEntry(
                    (CompetitorId)historyLabs[index], new GameDate(Math.Max(0, day)),
                    delta, historyKeys[index], subject));
            }
        }
    }
}
