using System;
using System.Collections.Generic;
using ScalingLaws.Core;
using ScalingLaws.Data;

namespace ScalingLaws.Simulation
{
    /// <summary>How a rival feels about you, as a band rather than a number.</summary>
    public enum RelationBand
    {
        /// <summary>Actively looking for a way to cost you something.</summary>
        Rivalry = 0,

        /// <summary>Competing against your interests on purpose.</summary>
        Hostile = 1,

        /// <summary>Cooling. Nothing has happened yet and something is going to.</summary>
        Tense = 2,

        /// <summary>Competitive and professional. Where everybody starts.</summary>
        Neutral = 3,

        /// <summary>Would take the call, and might say yes.</summary>
        Friendly = 4
    }

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
        /// <summary>The scale. Symmetric on purpose: being liked is as reachable as being hated.</summary>
        public const double Worst = -100.0;
        public const double Best = 100.0;

        /// <summary>Where every lab starts. Competitive, professional, no history.</summary>
        public const double Start = 0.0;

        /// <summary>How much a relation returns toward neutral each day.</summary>
        public const double DriftPerDay = 0.035;

        /// <summary>Entries kept. Past this it is an archive nobody reads, not a memory.</summary>
        public const int HistoryKept = 40;

        // ---- the bands, and they are what the interface shows ------------------------------------
        public const double FriendlyAbove = 40.0;
        public const double NeutralAbove = 5.0;
        public const double TenseAbove = -35.0;
        public const double HostileAbove = -70.0;

        private readonly Dictionary<CompetitorId, double> standing = new();
        private readonly List<RelationEntry> history = new();

        /// <summary>Everything that ever moved a relation, newest last.</summary>
        public IReadOnlyList<RelationEntry> History => history;

        /// <summary>Where a lab stands. Neutral for anybody nothing has happened with.</summary>
        public double With(CompetitorId lab) =>
            standing.TryGetValue(lab, out var value) ? value : Start;

        public RelationBand BandWith(CompetitorId lab) => BandFor(With(lab));

        public static RelationBand BandFor(double value) =>
            value >= FriendlyAbove ? RelationBand.Friendly
            : value >= NeutralAbove ? RelationBand.Neutral
            : value >= TenseAbove ? RelationBand.Tense
            : value >= HostileAbove ? RelationBand.Hostile
            : RelationBand.Rivalry;

        public static string NameOf(RelationBand band) => band switch
        {
            RelationBand.Friendly => Loc.T("relation.friendly"),
            RelationBand.Neutral => Loc.T("relation.neutral"),
            RelationBand.Tense => Loc.T("relation.tense"),
            RelationBand.Hostile => Loc.T("relation.hostile"),
            _ => Loc.T("relation.rivalry")
        };

        /// <summary>What a band means for how that lab behaves, in one sentence.</summary>
        public static string NoteFor(RelationBand band) => band switch
        {
            RelationBand.Friendly => Loc.T("relation.friendly.note"),
            RelationBand.Neutral => Loc.T("relation.neutral.note"),
            RelationBand.Tense => Loc.T("relation.tense.note"),
            RelationBand.Hostile => Loc.T("relation.hostile.note"),
            _ => Loc.T("relation.rivalry.note")
        };

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

                if (Math.Abs(value) <= DriftPerDay)
                {
                    standing[lab] = 0.0;
                    continue;
                }

                standing[lab] = value - Math.Sign(value) * DriftPerDay;
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
