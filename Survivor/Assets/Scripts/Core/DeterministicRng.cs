using System;

namespace Survivor.Core
{
    /// PCG32 generator (O'Neill, XSH-RR). Never use UnityEngine.Random/System.Random in simulation code.
    public sealed class DeterministicRng
    {
        private const ulong Multiplier = 6364136223846793005UL;

        private ulong _state;
        private readonly ulong _increment;

        /// Seeds like the reference pcg32_srandom_r (initstate = seed, initseq = streamId).
        public DeterministicRng(ulong seed, ulong streamId)
        {
            unchecked
            {
                _state = 0UL;
                _increment = (streamId << 1) | 1UL;
                NextUInt();
                _state += seed;
                NextUInt();
            }
        }

        /// Returns the next 32-bit output.
        public uint NextUInt()
        {
            unchecked
            {
                ulong oldState = _state;
                _state = oldState * Multiplier + _increment;
                uint xorShifted = (uint)(((oldState >> 18) ^ oldState) >> 27);
                int rotation = (int)(oldState >> 59);
                return (xorShifted >> rotation) | (xorShifted << ((-rotation) & 31));
            }
        }

        /// Returns (NextUInt() >> 8) * (1f / 16777216f), range [0, 1).
        public float NextFloat01()
        {
            return (NextUInt() >> 8) * (1f / 16777216f);
        }

        /// Returns an unbiased int in [0, maxExclusive) via rejection; throws ArgumentOutOfRangeException if maxExclusive <= 0.
        public int NextInt(int maxExclusive)
        {
            if (maxExclusive <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(maxExclusive), maxExclusive, "maxExclusive must be positive.");
            }

            uint bound = (uint)maxExclusive;
            uint threshold = unchecked(0u - bound) % bound;
            while (true)
            {
                uint value = NextUInt();
                if (value >= threshold)
                {
                    return (int)(value % bound);
                }
            }
        }

        /// Returns NextFloat01() < probability.
        public bool Chance(float probability)
        {
            return NextFloat01() < probability;
        }
    }
}
