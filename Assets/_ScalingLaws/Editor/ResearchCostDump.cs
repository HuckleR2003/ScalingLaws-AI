using System.Linq;
using System.Text;
using ScalingLaws.Data;
using ScalingLaws.Simulation;
using UnityEditor;
using UnityEngine;

namespace ScalingLaws.Editor
{
    /// <summary>
    /// Two tables nobody could read off the code: what the research tree charges, and what a token
    /// costs to serve against what the market pays for it.
    ///
    /// **Both were written to answer a question that had been answered from memory three times.**
    /// The tree's prices are derived from one cash figure per node, so the points a node costs are
    /// nowhere in the catalog; and the serving economics are four multipliers in three files
    /// against a price curve in a fourth. Printed rather than asserted: whether a number is right
    /// is a design call, and this measures.
    /// </summary>
    public static class ResearchCostDump
    {
        [MenuItem("Scaling Laws/Print the research prices")]
        public static void Run()
        {
            var report = new StringBuilder();
            foreach (var node in ResearchTree.All.OrderBy(n => n.EarliestDate.DayIndex))
            {
                report.AppendLine($"DUMP {node.EarliestDate} {node.Id,-28} {node.Track,-16} opt={node.OptionalTechnology,-5} "
                    + $"pts {ResearchBudget.PointCostOf(node.CostUsd),6} cash {ResearchBudget.CashCostOf(node.CostUsd),12:N0} "
                    + $"days {node.DurationDays,4} pfd {node.PetaflopDaysRequired,10:N0} "
                    + $"arch {node.UnlocksArchitecture} data {node.UnlocksData} needs [{string.Join(",", node.Prerequisites)}]");
            }
            Debug.Log(report.ToString());
        }

        /// <summary>
        /// Year by year: the market rate, what renting a petaflop-day costs, and what a served
        /// million tokens costs at three model sizes. The ratio at the end of each is the whole
        /// question: above one, serving at the market rate loses money on every token.
        /// </summary>
        [MenuItem("Scaling Laws/Print the serving economics")]
        public static void Economics()
        {
            var report = new StringBuilder();
            for (var year = 2022; year <= 2036; year++)
            {
                var date = ScalingLaws.Core.GameDate.FromCalendar(year, 7, 1);
                var market = MarketModel.PriceOn(date);
                var generation = MarketModel.RentableGenerationOn(date);
                var rentDay = MarketModel.RentPricePerPetaflopHourUsd(generation, MarketModel.ScarcityOn(date)) * 24.0;
                var line = $"ECON {year} market {market,8:0.000}/M  rent {rentDay,8:0.0}/PFday  gen {generation}  algo {MarketModel.AlgorithmicEfficiencyOn(date),6:0.0}";
                foreach (var billions in new[] { 20.0, 200.0, 2000.0 })
                {
                    var flop = 2.0 * billions * 1e9 * DeployedModel.ServingDistillationFactor;
                    var tokensPerPfDay = 1e15 * 86400.0 * CompanySimulation.InferenceUtilization / flop;
                    var costPerMillion = rentDay / tokensPerPfDay * 1e6;
                    line += $"  {billions,5}B: {costPerMillion,8:0.000}/M ({costPerMillion / market,6:0.00}x)";
                }
                var usage = AudienceCatalog.AverageTokensPerUserPerDay(date)
                            / AudienceCatalog.AverageTokensPerUserPerDay(ScalingLaws.Core.GameDate.Start);
                var fixedRate = MonetizationPolicy.OpeningSubscriptionUsdPerMonth
                                / (MonetizationCatalog.TokensPerSubscriberPerMonth / 1e6);
                line += $"  usage x{usage,6:0.0}  $80 fixed = {fixedRate / market,7:0.0}x market, with usage = {fixedRate / usage / market,6:0.00}x";
                report.AppendLine(line);
            }
            Debug.Log(report.ToString());
        }
    }
}
