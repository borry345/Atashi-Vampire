using UnityEngine;

namespace Survivor.Simulation
{
    /// Per-monster behavior state: M2 charge phase, M3 fire cooldown, M4 per-pattern cooldowns. All times are step numbers.
    public sealed class MonsterBehaviorState
    {
        /// M2 (charger) state machine phase (TAC-F3-7).
        public enum ChargePhase
        {
            Approach = 0,
            Charge = 1,
            Cooldown = 2,
        }

        private static readonly long[] NoPatterns = new long[0];

        private long[] _patternNextAllowed = NoPatterns;

        /// Current M2 phase; Approach for every other behavior.
        public ChargePhase Phase { get; internal set; }

        /// M2: first step at which the current Charge / Cooldown phase is over.
        public long PhaseEndStep { get; internal set; }

        /// M2: unit direction fixed when the charge started.
        public Vector2 ChargeDirection { get; internal set; }

        /// M3: first step at which the next projectile may be fired (starts at the spawn step).
        public long NextFireStep { get; internal set; }

        /// Number of boss patterns tracked.
        public int PatternCount => _patternNextAllowed.Length;

        /// M4: first step at which pattern index may be used again; throws IndexOutOfRangeException for a bad index.
        public long NextPatternStep(int index)
        {
            return _patternNextAllowed[index];
        }

        internal void SetNextPatternStep(int index, long step)
        {
            _patternNextAllowed[index] = step;
        }

        // Spawn: every cooldown starts at the spawn step, so the first use happens on the first Tick (spawn step + 1).
        internal void Reset(long spawnStep, int patternCount)
        {
            Phase = ChargePhase.Approach;
            PhaseEndStep = spawnStep;
            ChargeDirection = Vector2.right;
            NextFireStep = spawnStep;
            if (_patternNextAllowed.Length != patternCount)
            {
                _patternNextAllowed = patternCount == 0 ? NoPatterns : new long[patternCount];
            }

            for (int i = 0; i < _patternNextAllowed.Length; i++)
            {
                _patternNextAllowed[i] = spawnStep;
            }
        }
    }
}
