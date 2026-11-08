using System;
using System.Collections.Generic;
using Survivor.Core;
using Survivor.Data;

namespace Survivor.Simulation
{
    /// One owned weapon in a slot: the definition captured at creation (kept across hot reload, 5.14), the 1-based level,
    /// the next fire step (TAC-F2-2), the behavior chosen by WeaponFactory and, for orbit/zone weapons, the hit tracker.
    public sealed class WeaponInstance
    {
        private readonly List<SimEntity> _children = new List<SimEntity>();

        internal WeaponInstance(WeaponDefinition definition, int level, int slotIndex, long nextFireStep, WeaponAction action,
                                ContactDamageTracker tracker)
        {
            Definition = definition;
            Level = level;
            SlotIndex = slotIndex;
            NextFireStep = nextFireStep;
            Action = action;
            Tracker = tracker;
        }

        /// Definition captured when the instance was created.
        public WeaponDefinition Definition { get; }

        /// Weapon id (Definition.Id).
        public string Id => Definition.Id;

        /// Behavior of the definition (projectile, homing, orbit, zone).
        public WeaponBehavior Behavior => Definition.Behavior;

        /// 1-based level.
        public int Level { get; private set; }

        /// Parameters of the current level (levels[Level - 1], TAC-F6-7).
        public WeaponLevelDefinition LevelDefinition => Definition.Levels[Level - 1];

        /// Slot the instance occupies.
        public int SlotIndex { get; }

        /// First step at which the weapon may fire again (TAC-F2-2); also hashed by the state hash (5.16).
        public long NextFireStep { get; internal set; }

        /// Persistent-hit tracker (orbit and zone weapons); null for projectile weapons.
        public ContactDamageTracker Tracker { get; }

        /// Live child entities (orbs, zones) spawned for this instance, in spawn order.
        public IReadOnlyList<SimEntity> Children => _children;

        internal WeaponAction Action { get; }

        // R13: orbit hit interval is P1-scaled (the only W2/E2 interval); zone hit interval is the definition value (TAC-F2-3).
        internal int HitIntervalFor(float attackSpeedBonus)
        {
            WeaponLevelDefinition level = LevelDefinition;
            return Behavior == WeaponBehavior.Orbit
                ? StatMath.ApplyAttackSpeed(level.HitIntervalSteps, attackSpeedBonus)
                : Math.Max(1, level.HitIntervalSteps);
        }

        internal void SetLevel(int level, float attackSpeedBonus)
        {
            if (level < 1 || level > Definition.Levels.Count)
            {
                throw new ArgumentOutOfRangeException(nameof(level), level,
                    "Weapon " + Definition.Id + " has levels 1.." + Definition.Levels.Count + ".");
            }

            Level = level;
            Tracker?.SetInterval(HitIntervalFor(attackSpeedBonus));
        }

        // P1 change: only the orbit tracker interval depends on it; projectile/zone cooldowns read the bonus when they fire.
        internal void RefreshAttackSpeed(float attackSpeedBonus)
        {
            if (Tracker != null && Behavior == WeaponBehavior.Orbit)
            {
                Tracker.SetInterval(HitIntervalFor(attackSpeedBonus));
            }
        }

        internal void Tick(in StepContext context, in WeaponTickArgs args)
        {
            Action.Tick(this, context, args);
        }

        internal void AddChild(SimEntity child)
        {
            _children.Add(child);
        }

        internal void RemoveChild(SimEntity child)
        {
            _children.Remove(child);
        }

        internal OrbitOrb FindOrb(int orbIndex)
        {
            for (int i = 0; i < _children.Count; i++)
            {
                if (_children[i] is OrbitOrb orb && orb.OrbIndex == orbIndex)
                {
                    return orb;
                }
            }

            return null;
        }
    }

    // Per-step inputs WeaponInventory hands to every weapon behavior.
    internal readonly struct WeaponTickArgs
    {
        internal readonly EntityHandle Source;
        internal readonly float AttackSpeedBonus;
        internal readonly ICollisionStrategy Collisions;

        internal WeaponTickArgs(EntityHandle source, float attackSpeedBonus, ICollisionStrategy collisions)
        {
            Source = source;
            AttackSpeedBonus = attackSpeedBonus;
            Collisions = collisions;
        }
    }
}
