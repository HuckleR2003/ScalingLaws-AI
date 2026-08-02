using System;
using ScalingLaws.Core;

namespace ScalingLaws.Simulation
{
    /// <summary>
    /// The law the game is named after, and the ONE place model quality is computed.
    ///
    /// Loss follows the Chinchilla parametric form (Hoffmann et al., 2022):
    ///
    ///     L(N, D) = E + A / N^alpha + B / D^beta
    ///
    /// with N parameters and D training tokens. The constants below are the corrected fit rather
    /// than the ones printed in the original paper: the published values do not reproduce the
    /// paper's own compute-optimal ratio, while these land it at roughly 20 tokens per parameter,
    /// which is the lesson the game is built to teach.
    ///
    /// Training cost is the standard C = 6 * N * D FLOP, scaled by the share of parameters that are
    /// active on each token, which is where sparse mixtures earn their keep.
    ///
    /// Nothing here touches company state, dates or money. It is pure arithmetic so it can be tested
    /// on its own and trusted everywhere else.
    /// </summary>
    public static class ScalingLaw
    {
        /// <summary>Irreducible loss. No amount of compute goes below this.</summary>
        public const double IrreducibleLoss = 1.82;

        public const double ParameterCoefficient = 482.01;
        public const double ParameterExponent = 0.3478;
        public const double TokenCoefficient = 2085.43;
        public const double TokenExponent = 0.3658;

        /// <summary>FLOPs per parameter per token for a forward and backward pass.</summary>
        public const double FlopPerParameterPerToken = 6.0;

        /// <summary>Reducible loss that scores zero capability. A model this bad is a demo, not a product.</summary>
        public const double CapabilityReferenceReducibleLoss = 0.60;

        /// <summary>Reducible loss that scores 100. Nothing in the campaign timeline reaches it.</summary>
        public const double CapabilityFloorReducibleLoss = 0.01;

        private static readonly double CapabilitySpan =
            Math.Log(CapabilityReferenceReducibleLoss / CapabilityFloorReducibleLoss);

        /// <summary>
        /// Cross-entropy loss for a run, in nats per token. Both arguments are absolute counts, not
        /// billions.
        /// </summary>
        public static double Loss(double parameters, double tokens)
        {
            var safeParameters = Math.Max(1e6, SimUnits.Finite(parameters, 1e6));
            var safeTokens = Math.Max(1e6, SimUnits.Finite(tokens, 1e6));

            return IrreducibleLoss
                + ParameterCoefficient / Math.Pow(safeParameters, ParameterExponent)
                + TokenCoefficient / Math.Pow(safeTokens, TokenExponent);
        }

        /// <summary>
        /// Turns loss into the 0 to 100 capability index the whole game speaks in.
        ///
        /// The mapping is linear in the logarithm of reducible loss, so every halving of reducible
        /// loss is worth the same number of points. That is what makes the treadmill honest: a fixed
        /// capability gain always costs roughly the same multiple of compute, no matter where you
        /// start. With these constants a compute-optimal run gains 10.0 points per ten times the
        /// budget, because reducible loss falls as C^-(alpha*beta/(alpha+beta)).
        ///
        /// This index is a coarse relative class, never a benchmark score. It exists to say "ahead",
        /// "level" or "a generation behind".
        /// </summary>
        public static double CapabilityFromLoss(double loss)
        {
            var reducible = Math.Max(SimUnits.Finite(loss, 3.0) - IrreducibleLoss, CapabilityFloorReducibleLoss);
            var capability = 100.0 * Math.Log(CapabilityReferenceReducibleLoss / reducible) / CapabilitySpan;
            return Math.Clamp(capability, 0.0, 100.0);
        }

        /// <summary>Capability straight from a parameter and token count.</summary>
        public static double Capability(double parameters, double tokens)
        {
            return CapabilityFromLoss(Loss(parameters, tokens));
        }

        /// <summary>
        /// Training FLOPs for a run. <paramref name="activeParameterFraction"/> is the share of
        /// parameters that fire on each token: 1.0 for a dense model, far less for a mixture.
        /// </summary>
        public static double TrainingFlop(double parameters, double tokens, double activeParameterFraction = 1.0)
        {
            var safeParameters = Math.Max(0.0, SimUnits.Finite(parameters));
            var safeTokens = Math.Max(0.0, SimUnits.Finite(tokens));
            var safeFraction = Math.Clamp(SimUnits.Finite(activeParameterFraction, 1.0), 0.02, 1.0);
            return FlopPerParameterPerToken * safeParameters * safeFraction * safeTokens;
        }

        /// <summary>Training budget in petaflop/s-days, the unit the rest of the simulation trades in.</summary>
        public static double TrainingPetaflopDays(double parameters, double tokens, double activeParameterFraction = 1.0)
        {
            return SimUnits.FlopToPetaflopDays(TrainingFlop(parameters, tokens, activeParameterFraction));
        }

        /// <summary>
        /// Parameter count that minimizes loss for a fixed FLOP budget. Derived by minimizing
        /// L(N, C / (6 f N)) in N:
        ///
        ///     N* = [ alpha * A * C^beta / (beta * B * (6 f)^beta) ] ^ (1 / (alpha + beta))
        /// </summary>
        public static double OptimalParameters(double trainingFlop, double activeParameterFraction = 1.0)
        {
            var flop = Math.Max(1e15, SimUnits.Finite(trainingFlop, 1e15));
            var fraction = Math.Clamp(SimUnits.Finite(activeParameterFraction, 1.0), 0.02, 1.0);
            var costPerParameterToken = FlopPerParameterPerToken * fraction;

            var numerator = ParameterExponent * ParameterCoefficient * Math.Pow(flop, TokenExponent);
            var denominator = TokenExponent * TokenCoefficient * Math.Pow(costPerParameterToken, TokenExponent);
            var ratio = numerator / denominator;

            return Math.Pow(ratio, 1.0 / (ParameterExponent + TokenExponent));
        }

        /// <summary>Token count that pairs with <see cref="OptimalParameters"/> for the same budget.</summary>
        public static double OptimalTokens(double trainingFlop, double activeParameterFraction = 1.0)
        {
            var flop = Math.Max(1e15, SimUnits.Finite(trainingFlop, 1e15));
            var fraction = Math.Clamp(SimUnits.Finite(activeParameterFraction, 1.0), 0.02, 1.0);
            var parameters = OptimalParameters(flop, fraction);
            return flop / (FlopPerParameterPerToken * fraction * parameters);
        }

        /// <summary>
        /// The famous ratio, for a given budget. It drifts slowly upward with scale rather than
        /// sitting exactly on 20, which is what the fit actually says.
        /// </summary>
        public static double OptimalTokensPerParameter(double trainingFlop, double activeParameterFraction = 1.0)
        {
            var parameters = OptimalParameters(trainingFlop, activeParameterFraction);
            return parameters <= 0.0 ? 0.0 : OptimalTokens(trainingFlop, activeParameterFraction) / parameters;
        }

        /// <summary>
        /// Best capability obtainable from a FLOP budget if the shape were chosen perfectly. The
        /// yardstick a blueprint gets measured against.
        /// </summary>
        public static double BestCapabilityForBudget(double trainingFlop, double activeParameterFraction = 1.0)
        {
            var parameters = OptimalParameters(trainingFlop, activeParameterFraction);
            var tokens = OptimalTokens(trainingFlop, activeParameterFraction);
            return Capability(parameters, tokens);
        }

        /// <summary>
        /// How much of the compute a run is actually converting into capability, 0 to 1. An
        /// undertrained giant and an overtrained dwarf both score badly here while burning the same
        /// budget as a well-shaped run.
        /// </summary>
        public static double ShapeEfficiency(double parameters, double tokens, double activeParameterFraction = 1.0)
        {
            var flop = TrainingFlop(parameters, tokens, activeParameterFraction);
            if (flop <= 0.0)
            {
                return 0.0;
            }

            var best = BestCapabilityForBudget(flop, activeParameterFraction);
            if (best <= 0.0)
            {
                return 0.0;
            }

            return Math.Clamp(Capability(parameters, tokens) / best, 0.0, 1.0);
        }
    }
}
