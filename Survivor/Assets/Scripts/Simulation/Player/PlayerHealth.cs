using System;
using Survivor.Core;

namespace Survivor.Simulation
{
    /// Player health. MaxHp is read once per run (ResetForRun) and does not follow hot reload (5c.3 #10). Death is read by
    /// StepOutcomeGate through IsDead; no events are raised.
    public sealed class PlayerHealth
    {
        /// Current health in [0, MaxHp].
        public float Hp { get; private set; }

        /// Maximum health of the run.
        public float MaxHp { get; private set; }

        /// True when Hp <= 0.
        public bool IsDead => Hp <= 0f;

        /// Run start: MaxHp = Hp = maxHealth (finite and > 0, else ArgumentOutOfRangeException).
        public void ResetForRun(float maxHealth)
        {
            if (!(maxHealth > 0f) || float.IsInfinity(maxHealth))
            {
                throw new ArgumentOutOfRangeException(nameof(maxHealth), maxHealth, "Max health must be finite and positive.");
            }

            MaxHp = maxHealth;
            Hp = maxHealth;
        }

        /// Subtracts amount (finite and >= 0, else ArgumentOutOfRangeException); Hp never goes below 0.
        public void TakeDamage(float amount)
        {
            if (!(amount >= 0f) || float.IsInfinity(amount))
            {
                throw new ArgumentOutOfRangeException(nameof(amount), amount, "Damage must be finite and non-negative.");
            }

            Hp = Math.Max(0f, Hp - amount);
        }

        /// R23: Hp = min(MaxHp, Hp + MaxHp * HealRatio).
        public void Heal()
        {
            Hp = ProgressionRules.ApplyHeal(Hp, MaxHp);
        }

        /// Dev SetHealth: Hp = hp clamped to [0, MaxHp] (NaN throws ArgumentOutOfRangeException).
        public void SetHp(float hp)
        {
            if (float.IsNaN(hp))
            {
                throw new ArgumentOutOfRangeException(nameof(hp), hp, "Health must be a number.");
            }

            Hp = Math.Min(MaxHp, Math.Max(0f, hp));
        }
    }
}
