using System;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using ScalingLaws.Core;
using ScalingLaws.Data;
using ScalingLaws.Simulation;
using UnityEditor;
using Debug = UnityEngine.Debug;

namespace ScalingLaws.Editor
{
    /// <summary>
    /// What a simulated day actually costs, and what makes it cost more.
    ///
    /// **Because a day is not free and the game runs three of them a second.** A frame at sixty is
    /// 16.7 milliseconds; a day that costs ten of them is a stutter every time the clock turns over,
    /// and at X3 that is three stutters a second. Nobody had measured it.
    ///
    /// The deep campaign reported 10.6 ms a day, which is the probe's own arithmetic rather than the
    /// game's: that operator runs a full training projection every idle day. This measures
    /// `AdvanceDay` alone, and then measures it again as the things a long campaign accumulates pile
    /// up, because that is where a cost that is fine in year one stops being fine in year ten.
    /// </summary>
    public static class TickCostProbe
    {
        private const int Days = 730;

        private static readonly CultureInfo Culture = CultureInfo.InvariantCulture;

        [MenuItem("Scaling Laws/Measure what a day costs")]
        public static void Measure()
        {
            var report = new StringBuilder();
            report.AppendLine("what one simulated day costs, averaged over " + Days + " days");
            report.AppendLine();

            foreach (var models in new[] { 0, 1, 10, 50, 150, 339 })
            {
                Run(models, staff: 6, basement: false, report);
            }

            report.AppendLine();
            Run(10, staff: 60, basement: false, report);
            Run(10, staff: 6, basement: true, report);

            Debug.Log(report.ToString());
        }

        private static void Run(int models, int staff, bool basement, StringBuilder report)
        {
            var simulation = new CompanySimulation(new CompanyState("Prometheus AI", 4242));
            var state = simulation.State;

            state.CashUsd = 200_000_000_000L;
            simulation.SetRentedPetaflops(20_000.0);

            for (var index = 0; index < models; index++)
            {
                // Its own line each, which is what a player gets by naming every release something
                // new, and what the deep campaign ended up with.
                var model = new DeployedModel(
                    "Aurora " + index, ArchitectureId.DenseTransformer, 30.0 + index * 0.1,
                    state.Date, 2e10, 1.0, ModelType.General, "Line " + index);

                state.AddDeployedModel(model);
                model.SeedLine(MonetizationPolicy.OpeningSubscriptionUsdPerMonth, 0.0);
            }

            for (var index = 0; index < staff; index++)
            {
                state.Staff.Add(new Hire(StaffRole.ResearchScientist, 3, state.Date));
            }

            if (basement)
            {
                simulation.TryOpenServerRoom(true, out _);
                state.Hall.Stock(state.Hall.TotalSlots);
            }

            // A hundred days of settling, so the measurement is of a running campaign rather than
            // of the first tick after everything was created.
            for (var day = 0; day < 100; day++)
            {
                simulation.AdvanceDay();

                while (state.TryDequeueEvent(out _))
                {
                }
            }

            var clock = Stopwatch.StartNew();

            for (var day = 0; day < Days; day++)
            {
                simulation.AdvanceDay();

                while (state.TryDequeueEvent(out _))
                {
                }
            }

            clock.Stop();

            var perDay = clock.Elapsed.TotalMilliseconds / Days;

            report.AppendLine(string.Format(Culture,
                "   {0,4} models, {1,3} staff, basement {2,-5}   {3,7:0.000} ms a day   "
                + "{4}",
                models, staff, basement, perDay, Verdict(perDay)));
        }

        /// <summary>
        /// A day against a frame, because that is the only comparison that matters here.
        ///
        /// The clock runs at up to three days a second, so a day has to fit inside a frame with room
        /// for the interface that is drawn in the same one.
        /// </summary>
        private static string Verdict(double perDay) =>
            perDay < 1.0 ? "fine"
            : perDay < 4.0 ? "noticeable at X3"
            : perDay < 16.7 ? "a visible hitch on every day"
            : "longer than a frame: the game stops";
    }
}
