using System;
using System.Globalization;
using System.Linq;
using System.Text;
using ScalingLaws.Core;
using ScalingLaws.Data;
using ScalingLaws.Simulation;
using UnityEditor;
using Debug = UnityEngine.Debug;

namespace ScalingLaws.Editor
{
    /// <summary>
    /// The best model a player could build on each date, against the best a rival has on sale.
    ///
    /// **This answers a different question from the deep campaign.** That probe asks how a scripted
    /// operator fares, and an operator can lose because it plays badly. This one asks whether the
    /// game leaves any room at all: every node the calendar allows already researched, all the money
    /// in the world, and the best shape a sweep can find. If that is behind the field, no player can
    /// catch up however well they play, and the fault is the game's rather than the player's.
    ///
    /// Two arms. "Calendar" researches each node on the first day it may be started, which is faster
    /// than anybody can actually research, so it is still an upper bound. "Everything" has the whole
    /// tree on day one, which is the sandbox.
    /// </summary>
    public static class CeilingProbe
    {
        private static readonly CultureInfo Culture = CultureInfo.InvariantCulture;

        [MenuItem("Scaling Laws/Probe the capability ceiling")]
        public static void Run()
        {
            var report = new StringBuilder();

            foreach (var everything in new[] { false, true })
            {
                report.AppendLine();
                report.AppendLine(everything
                    ? "==== EVERYTHING RESEARCHED ON DAY ONE"
                    : "==== EACH NODE RESEARCHED THE DAY THE CALENDAR ALLOWS IT");
                report.AppendLine("  date        best  rival  frontier  gap   params(B)  tokens(B)  PF-days      rent cost    world rev/yr");

                var simulation = new CompanySimulation(new CompanyState("Ceiling", 4242u));
                var state = simulation.State;

                if (everything)
                {
                    simulation.UnlockEveryResearchNode();
                }

                // A model on sale good enough to generate data from, or the synthetic corpus, which
                // is the largest one there is, would stay shut for the whole run: it asks for a live
                // model of capability 40 and this company would otherwise never ship anything.
                state.AddDeployedModel(new DeployedModel("seed", ArchitectureId.DenseTransformer,
                    90.0, state.Date, 2e10, 1.0));

                // Enough fleet that memory never blocks the question.
                state.CashUsd = 5_000_000_000_000L;
                simulation.SetRentedPetaflops(60_000.0);

                for (var day = 0; day < 14 * 365; day++)
                {
                    simulation.AdvanceDay();

                    while (state.TryDequeueEvent(out _))
                    {
                    }

                    state.CashUsd = 5_000_000_000_000L;

                    if (!everything)
                    {
                        UnlockWhatTheCalendarAllows(simulation);
                    }

                    if (day % 182 != 0)
                    {
                        continue;
                    }

                    // A model left alone for years is retired, and with it the synthetic corpus
                    // would shut. A real company keeps something on sale.
                    if (state.BestCapability < 50.0)
                    {
                        state.AddDeployedModel(new DeployedModel("seed", ArchitectureId.DenseTransformer,
                            90.0, state.Date, 2e10, 1.0));
                    }

                    var best = Best(simulation, out var parameters, out var tokens, out var petaflopDays);
                    var market = simulation.Market;
                    var rival = BestRival(simulation);
                    var rent = petaflopDays * market.RentPricePerPetaflopDayUsd;
                    var worldRevenue = MarketModel.DemandOn(state.Date) * 1000.0
                                       * MarketModel.PriceOn(state.Date) * 365.0;

                    report.AppendLine(string.Format(Culture,
                        "  {0}  {1,5:0.0}  {2,5:0.0}  {3,6:0.0}  {4,5:+0.0;-0.0}  {5,9:N0}  {6,9:N0}  {7,11:N0}  {8,14:C0}  {9,14:C0}",
                        state.Date, best, rival, market.FrontierCapability, best - rival,
                        parameters, tokens, petaflopDays, rent, worldRevenue)
                        + string.Format(Culture, "  physics {0,5:0.0}", FrontierPhysics.ReachableOn(state.Date)));
                }
            }

            Debug.Log(report.ToString());
        }

        private static void UnlockWhatTheCalendarAllows(CompanySimulation simulation)
        {
            // Repeated until nothing more opens, because a prerequisite held today can make another
            // node open today.
            var state = simulation.State;
            var opened = true;

            while (opened)
            {
                opened = false;

                foreach (var node in ResearchTree.All)
                {
                    if (state.UnlockedResearch.Contains(node.Id)
                        || node.EarliestDate.DayIndex > state.Date.DayIndex
                        || node.Prerequisites.Any(need => !state.UnlockedResearch.Contains(need)))
                    {
                        continue;
                    }

                    simulation.UnlockResearchNode(node.Id);
                    opened = true;
                }
            }
        }

        private static double BestRival(CompanySimulation simulation)
        {
            var best = 0.0;

            foreach (var agent in simulation.State.Rivals.Agents)
            {
                if (agent.TryGetLiveModel(simulation.State.Date, out var model))
                {
                    best = Math.Max(best, model.Capability);
                }
            }

            return best;
        }

        private static double Best(CompanySimulation simulation, out double bestParameters,
            out double bestTokens, out double bestPetaflopDays)
        {
            var state = simulation.State;
            var ceiling = simulation.ParameterCeilingBillions();
            var best = 0.0;
            bestParameters = 0.0;
            bestTokens = 0.0;
            bestPetaflopDays = 0.0;

            foreach (var architecture in state.AdoptedArchitectures.ToList())
            {
                for (var step = 0; step <= 16; step++)
                {
                    var parameters = Math.Min(ceiling, Math.Pow(10.0, step / 16.0 * Math.Log10(ceiling)));

                    foreach (var perParameter in new[] { 5.0, 10.0, 20.0, 40.0, 80.0, 200.0 })
                    {
                        var blueprint = TrainingPlanner.OptimalBlueprintForBudget(
                                "probe", architecture, 1000.0, state.OwnedDataSources)
                            .WithParameters(parameters)
                            .WithTokens(parameters * perParameter);

                        var supply = DatasetCatalog.Blend(blueprint.DataSources,
                            blueprint.TrainingTokensBillions, state.Date, state.BestCapability,
                            state.Founder.DataSupplyMultiplier).AvailableTokensBillions;

                        if (blueprint.TrainingTokensBillions > supply)
                        {
                            blueprint = blueprint.WithTokens(supply * 0.99);
                        }

                        var projection = simulation.Project(blueprint);

                        if (projection.ProjectedCapability > best)
                        {
                            best = projection.ProjectedCapability;
                            bestParameters = blueprint.ParameterCountBillions;
                            bestTokens = blueprint.TrainingTokensBillions;
                            bestPetaflopDays = projection.TrainingPetaflopDays;
                        }
                    }
                }
            }

            return best;
        }
    }
}
