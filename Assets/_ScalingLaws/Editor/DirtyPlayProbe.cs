using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ScalingLaws.Data;
using ScalingLaws.Simulation;
using UnityEditor;
using Debug = UnityEngine.Debug;

namespace ScalingLaws.Editor
{
    /// <summary>
    /// What happens to a company that plays dirty until somebody notices.
    ///
    /// **Written because a normal campaign never gets there.** The deep probe smeared a rival once
    /// a year for fourteen years and landed exactly one: the quiet period between campaigns is long,
    /// so a player who occasionally whispers about a rival will never see the other half of that
    /// mechanism. The threat letter, the countdown, the settlement and the call from their lawyers
    /// all exist and none of them can be observed by playing normally, which is the same shape as a
    /// mechanism with no button on it.
    ///
    /// So this one pushes: it smears as often as it is allowed, at the tier it can afford, and
    /// reports the first time each stage of the consequence actually happens.
    /// </summary>
    public static class DirtyPlayProbe
    {
        private const int Days = 8 * 365;

        [MenuItem("Scaling Laws/Play dirty and see who notices")]
        public static void Play()
        {
            var report = new StringBuilder();

            foreach (var tier in new[] { SmearTier.Whisper, SmearTier.Briefing, SmearTier.Campaign })
            {
                RunOne(tier, report);
            }

            Debug.Log(report.ToString());
        }

        private static void RunOne(SmearTier tier, StringBuilder report)
        {
            var simulation = new CompanySimulation(new CompanyState("Prometheus AI", 4242));
            var state = simulation.State;

            state.CashUsd = 50_000_000_000L;
            simulation.SetRentedPetaflops(4_000.0);

            var attempts = 0;
            var landed = 0;
            var traced = 0;
            var refusals = new Dictionary<string, int>();

            var firstThreat = -1;
            var firstLetter = -1;
            var firstCall = -1;
            var verdicts = new List<string>();

            for (var day = 0; day < Days; day++)
            {
                simulation.AdvanceDay();
                Drain(day);

                var target = state.Rivals.Agents.FirstOrDefault();

                if (target == null)
                {
                    continue;
                }

                attempts++;

                if (simulation.TrySmear(target.Competitor, tier, out var backfired, out var note))
                {
                    landed++;
                }
                else
                {
                    var key = note ?? "no reason given";
                    refusals[key] = refusals.TryGetValue(key, out var count) ? count + 1 : 1;
                }

                // **Drained again, straight after the click.** The shell drains on the same pass
                // that the player acts on, and the phone's condition asks for a threat whose clock
                // has not ticked yet. Leaving the event in the queue until the next day is the
                // probe answering a different question: by then the threat is a day old.
                Drain(day);

                if (state.SmearThreat != null && firstThreat < 0)
                {
                    firstThreat = day;
                }

                if (firstLetter < 0
                    && state.Mail.All.Any(letter => letter.Kind == MailKind.LegalThreat))
                {
                    firstLetter = day;
                }
            }

            void Drain(int day)
            {
                while (state.TryDequeueEvent(out var entry))
                {
                    // The exact condition the shell rings the phone on. Checked here rather than
                    // trusted, because the call is the part the author asked about and it is wired
                    // in the interface, where no simulation test can see it.
                    if (entry.Type == CompanyEventType.SmearThreatened
                        && state.SmearThreat is { IsAnswered: false, DaysElapsed: 0 }
                        && firstCall < 0)
                    {
                        firstCall = day;
                    }

                    if (entry.Type == CompanyEventType.SmearBackfired)
                    {
                        traced++;
                    }

                    if (entry.Type == CompanyEventType.LawsuitFiled
                        || entry.Type == CompanyEventType.LawsuitDecided)
                    {
                        verdicts.Add($"      day {day,5}  {entry.Type}: {entry.Message}");
                    }
                }
            }

            report.AppendLine();
            report.AppendLine($"  ---- {tier}, pushed every day for {Days} days "
                + new string('-', 30));

            report.AppendLine($"      tried {attempts}, landed {landed}, traced back {traced}");
            report.AppendLine($"      first threat on day {Say(firstThreat)}, "
                + $"first letter in the inbox {Say(firstLetter)}, "
                + $"their lawyers ring {Say(firstCall)}");

            if (verdicts.Count > 0)
            {
                report.AppendLine("      and in court:");

                foreach (var line in verdicts.Take(6))
                {
                    report.AppendLine(line);
                }
            }

            foreach (var pair in refusals.OrderByDescending(entry => entry.Value).Take(4))
            {
                report.AppendLine($"      {pair.Value,6}x refused: {pair.Key}");
            }
        }

        private static string Say(int day) => day < 0 ? "never" : "day " + day;
    }
}
