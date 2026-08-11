using System;
using ScalingLaws.Core;

namespace ScalingLaws.Simulation
{
    /// <summary>
    /// What the player's users think, and where their number is heading.
    ///
    /// Every figure here is derived from the market that already exists. Nothing is stored, so there
    /// is no save bump and no second copy of the truth that can drift from the standing it describes.
    /// </summary>
    public readonly struct UserSentiment
    {
        /// <summary>How far a forecast has to run before it earns another arrow.</summary>
        public const double ArrowStep = 0.06;

        public UserSentiment(double users, double satisfaction, double momentum, double bestRivalUsers)
        {
            Users = Math.Max(0.0, SimUnits.Finite(users));
            Satisfaction = Math.Clamp(SimUnits.Finite(satisfaction), 0.0, 1.0);
            Momentum = Math.Clamp(SimUnits.Finite(momentum), -1.0, 1.0);
            BestRivalUsers = Math.Max(0.0, SimUnits.Finite(bestRivalUsers));
        }

        /// <summary>People using something the player built.</summary>
        public double Users { get; }

        /// <summary>
        /// How much the player's users prefer what they have to their next best option, on nothing to
        /// everything. One means the player is the best thing available to them; low means they are
        /// still here but would rather be somewhere else, which is exactly the state that precedes
        /// losing them.
        /// </summary>
        public double Satisfaction { get; }

        /// <summary>
        /// Where the user count is going, not where it has been.
        ///
        /// Audiences move a fraction of the way toward what they would prefer each day, so the gap
        /// between where they are and where they are heading is a real forecast rather than a
        /// projection of past growth. Positive means people are arriving.
        /// </summary>
        public double Momentum { get; }

        /// <summary>The largest single rival, for one honest comparison rather than a leaderboard.</summary>
        public double BestRivalUsers { get; }

        /// <summary>
        /// The forecast as arrows, minus three to plus three. Three means the market is moving hard.
        /// </summary>
        public int Arrows
        {
            get
            {
                var steps = (int)(Math.Abs(Momentum) / ArrowStep);
                return Math.Sign(Momentum) * Math.Clamp(steps, 0, 3);
            }
        }

        /// <summary>A word for the satisfaction figure, so the banner is readable without a legend.</summary>
        public string Mood => Satisfaction switch
        {
            >= 0.85 => "DELIGHTED",
            >= 0.65 => "HAPPY",
            >= 0.45 => "CONTENT",
            >= 0.25 => "RESTLESS",
            _ => "LEAVING"
        };
    }
}
