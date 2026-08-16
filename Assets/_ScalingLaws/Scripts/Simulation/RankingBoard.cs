using System;
using System.Collections.Generic;
using ScalingLaws.Core;
using ScalingLaws.Data;

namespace ScalingLaws.Simulation
{
    /// <summary>One row of the live standings.</summary>
    public readonly struct RankingEntry
    {
        public RankingEntry(
            int position,
            string labName,
            bool isPlayer,
            double capability,
            double marketShare,
            double brand,
            double score,
            string modelName,
            CompetitorId competitor)
        {
            Position = Math.Max(1, position);
            LabName = labName ?? string.Empty;
            IsPlayer = isPlayer;
            Capability = Math.Clamp(SimUnits.Finite(capability), 0.0, 100.0);
            MarketShare = Math.Clamp(SimUnits.Finite(marketShare), 0.0, 1.0);
            Brand = Math.Clamp(SimUnits.Finite(brand), 0.0, 1.0);
            Score = Math.Clamp(SimUnits.Finite(score), 0.0, 100.0);
            ModelName = modelName ?? string.Empty;
            Competitor = competitor;
        }

        public int Position { get; }
        public string LabName { get; }
        public bool IsPlayer { get; }
        public double Capability { get; }
        public double MarketShare { get; }
        public double Brand { get; }

        /// <summary>The single number the board sorts on, 0 to 100.</summary>
        public double Score { get; }

        public string ModelName { get; }

        /// <summary>
        /// Which lab this is, so a board can show its mark rather than only its name.
        ///
        /// <see cref="CompetitorId.None"/> for the player, who has no mark of their own and gets an
        /// initial instead. Carried here rather than looked up from the name, because two labs could
        /// be renamed to the same string and the mark would follow the wrong one.
        /// </summary>
        public CompetitorId Competitor { get; }

        public override string ToString() =>
            $"{Position}. {LabName} {Score:0.0} (cap {Capability:0.0}, share {MarketShare:P1})";
    }

    /// <summary>
    /// The ONE place standings are computed. The equivalent of the OS rating screen in the tycoon
    /// games this is modelled on, with one difference that matters: the score here is derived from
    /// the same numbers the simulation runs on, so it can never drift away from what is actually
    /// happening. The most common complaint about Smartphone Tycoon was that its ratings looked
    /// random against the specifications. This cannot be, because there is nothing else to read.
    /// </summary>
    public static class RankingBoard
    {
        public const double CapabilityWeight = 0.55;
        public const double ShareWeight = 0.30;
        public const double BrandWeight = 0.15;

        public static double Score(double capability, double marketShare, double brand)
        {
            var capabilityPart = Math.Clamp(capability, 0.0, 100.0) * CapabilityWeight;
            var sharePart = Math.Clamp(marketShare, 0.0, 1.0) * 100.0 * ShareWeight;
            var brandPart = Math.Clamp(brand, 0.0, 1.0) * 100.0 * BrandWeight;
            return Math.Clamp(capabilityPart + sharePart + brandPart, 0.0, 100.0);
        }

        /// <summary>
        /// The full board on a given day, best first. Shares are the same logit split the revenue
        /// side uses, so a lab's position on this board and its income never disagree.
        /// </summary>
        public static List<RankingEntry> Build(
            CompanyState state,
            MarketConditions market,
            CompetitorField field)
        {
            var rivals = field != null ? field.LiveModels(market.Date) : new List<RivalModel>();

            var playerScore = MarketShareModel.PlayerScore(state.DeployedModels, state.Reputation, market);
            var rivalScore = MarketShareModel.RivalScore(market.Date, rivals);
            var total = playerScore + rivalScore;

            var rows = new List<RankingEntry>(rivals.Count + 1);

            var bestPlayerModel = MarketShareModel.BestLiveModel(state.DeployedModels, market.Date);
            if (bestPlayerModel != null)
            {
                var playerShare = total <= 0.0 ? 0.0 : playerScore / total;
                var playerBrand = Math.Clamp(state.Reputation + bestPlayerModel.BrandBonus(market.Date), 0.0, 1.0);
                var capability = bestPlayerModel.EffectiveCapability(market.Date);
                rows.Add(new RankingEntry(
                    1,
                    state.CompanyName,
                    true,
                    capability,
                    playerShare,
                    playerBrand,
                    Score(capability, playerShare, playerBrand),
                    bestPlayerModel.Name,
                    CompetitorId.None));
            }

            foreach (var rival in rivals)
            {
                var weight = Math.Exp(MarketShareModel.Utility(
                    rival.Capability,
                    rival.BrandStrength,
                    rival.PriceMultiplier,
                    rival.ReleaseDate.YearsUntil(market.Date)));
                var share = total <= 0.0 ? 0.0 : weight / total;

                rows.Add(new RankingEntry(
                    1,
                    LabDisplayName(rival.Competitor),
                    false,
                    rival.Capability,
                    share,
                    rival.BrandStrength,
                    Score(rival.Capability, share, rival.BrandStrength),
                    rival.DisplayName,
                    rival.Competitor));
            }

            // Labs that exist but have not shipped.
            //
            // **The board used to be a list of products, and it read as a list of companies.** Four
            // rivals were added specifically so the player could watch them get fined and fold, and
            // three of them were invisible until the day they released something — so the collapse
            // had no build-up and the market looked empty for the first two years. A lab that has
            // been founded is on the board from the day it is founded, at the bottom, with nothing
            // to its name. That is exactly what it deserves and exactly what makes the fall legible.
            var shipped = new HashSet<CompetitorId>();

            foreach (var rival in rivals)
            {
                shipped.Add(rival.Competitor);
            }

            foreach (var dossier in LabDossiers.All)
            {
                if (shipped.Contains(dossier.Competitor)
                    || !market.Date.IsOnOrAfter(dossier.Founded))
                {
                    continue;
                }

                rows.Add(new RankingEntry(
                    1,
                    dossier.Name,
                    false,
                    0.0,
                    0.0,
                    0.0,
                    0.0,
                    string.Empty,
                    dossier.Competitor));
            }

            rows.Sort(static (left, right) => right.Score.CompareTo(left.Score));

            var ranked = new List<RankingEntry>(rows.Count);
            for (var index = 0; index < rows.Count; index++)
            {
                var row = rows[index];
                ranked.Add(new RankingEntry(
                    index + 1,
                    row.LabName,
                    row.IsPlayer,
                    row.Capability,
                    row.MarketShare,
                    row.Brand,
                    row.Score,
                    row.ModelName,
                    row.Competitor));
            }

            return ranked;
        }

        /// <summary>The player's place on the board, or zero when they have nothing live.</summary>
        public static int PlayerPosition(IReadOnlyList<RankingEntry> board)
        {
            if (board == null)
            {
                return 0;
            }

            for (var index = 0; index < board.Count; index++)
            {
                if (board[index].IsPlayer)
                {
                    return board[index].Position;
                }
            }

            return 0;
        }

        private static string LabDisplayName(CompetitorId competitor) =>
            CompetitorCatalog.NameOf(competitor);
    }
}
