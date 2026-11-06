using System;

namespace Survivor.Core
{
    /// Independent per-purpose streams so disabling one system (e.g. level-up) never shifts another's sequence.
    public sealed class RngStreams
    {
        private const ulong GoldenGamma = 0x9E3779B97F4A7C15UL;

        private readonly DeterministicRng[] _streams = new DeterministicRng[(int)RngStream.LevelUp + 1];

        /// Creates the stream set for a master seed.
        public RngStreams(ulong masterSeed)
        {
            MasterSeed = masterSeed;
        }

        /// Seed every stream is derived from.
        public ulong MasterSeed { get; private set; }

        /// Returns the stream's generator (same instance until Reseed); seed = SplitMix64(masterSeed ^ (0x9E3779B97F4A7C15UL * (ulong)stream)).
        public DeterministicRng Get(RngStream stream)
        {
            int slot = (int)stream;
            if (slot < (int)RngStream.Spawn || slot > (int)RngStream.LevelUp)
            {
                throw new ArgumentOutOfRangeException(nameof(stream), stream, "Unknown RngStream.");
            }

            DeterministicRng rng = _streams[slot];
            if (rng == null)
            {
                ulong seed = SplitMix64(unchecked(MasterSeed ^ (GoldenGamma * (ulong)stream)));
                rng = new DeterministicRng(seed, (ulong)stream);
                _streams[slot] = rng;
            }

            return rng;
        }

        /// Replaces the master seed; every stream restarts from its newly derived seed.
        public void Reseed(ulong masterSeed)
        {
            MasterSeed = masterSeed;
            Array.Clear(_streams, 0, _streams.Length);
        }

        // One SplitMix64 output for state x (Steele, Lea, Flood 2014), including the generator's "x += gamma" step.
        private static ulong SplitMix64(ulong x)
        {
            unchecked
            {
                ulong z = x + GoldenGamma;
                z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
                z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
                return z ^ (z >> 31);
            }
        }
    }
}
