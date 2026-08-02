using System;

namespace ScalingLaws.Core
{
    /// <summary>
    /// xorshift32 with a serializable state word. UnityEngine.Random is global mutable state and
    /// cannot be replayed, so the simulation never touches it: every roll comes from an instance
    /// that lives in the save file.
    /// </summary>
    public sealed class DeterministicRandom
    {
        private const uint DefaultSeed = 0x5CA1AB1E;

        private uint state;

        public DeterministicRandom(uint seed = DefaultSeed)
        {
            state = seed == 0 ? DefaultSeed : seed;
        }

        /// <summary>The whole generator. Save this, restore it, get the same campaign back.</summary>
        public uint State
        {
            get => state;
            set => state = value == 0 ? DefaultSeed : value;
        }

        public uint NextUInt()
        {
            state ^= state << 13;
            state ^= state >> 17;
            state ^= state << 5;
            return state;
        }

        /// <summary>Uniform in [0, 1).</summary>
        public double NextDouble() => NextUInt() / 4294967296.0;

        /// <summary>Uniform in [minInclusive, maxExclusive). Returns the lower bound if the range is empty.</summary>
        public int NextInt(int minInclusive, int maxExclusive)
        {
            if (maxExclusive <= minInclusive)
            {
                return minInclusive;
            }

            var range = (uint)(maxExclusive - minInclusive);
            return minInclusive + (int)(NextUInt() % range);
        }

        /// <summary>Uniform in [min, max].</summary>
        public double NextRange(double min, double max) => min + (max - min) * NextDouble();

        /// <summary>
        /// Standard normal via Box-Muller. Training outcomes use this: a run lands near its
        /// projection, not exactly on it.
        /// </summary>
        public double NextGaussian(double mean = 0.0, double standardDeviation = 1.0)
        {
            var u1 = Math.Max(NextDouble(), 1e-12);
            var u2 = NextDouble();
            var magnitude = Math.Sqrt(-2.0 * Math.Log(u1));
            return mean + standardDeviation * magnitude * Math.Cos(2.0 * Math.PI * u2);
        }

        public bool NextChance(double probability) => NextDouble() < Math.Clamp(probability, 0.0, 1.0);

        /// <summary>
        /// A child generator seeded from this one. Lets a subsystem roll without shifting the
        /// parent stream, which keeps unrelated systems from desyncing each other.
        /// </summary>
        public DeterministicRandom Fork() => new(NextUInt());
    }
}
