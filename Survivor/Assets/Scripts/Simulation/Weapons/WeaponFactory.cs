using System;
using Survivor.Core;
using Survivor.Data;

namespace Survivor.Simulation
{
    // Builds weapon instances from definitions; the behavior object is chosen from WeaponDefinition.Behavior only (TAC-F6-1).
    internal static class WeaponFactory
    {
        private static readonly WeaponAction Projectile = new ProjectileWeapon();
        private static readonly WeaponAction Orbit = new OrbitWeapon();
        private static readonly WeaponAction Zone = new ZoneWeapon();

        // projectile/homing -> ProjectileWeapon (homing makes the projectiles re-aim), orbit -> OrbitWeapon, zone -> ZoneWeapon.
        // nextFireStep is the step the weapon became owned (first-ownership rule, TAC-F2-2).
        internal static WeaponInstance Create(WeaponDefinition definition, int level, int slotIndex, long nextFireStep, float attackSpeedBonus)
        {
            if (definition == null)
            {
                throw new ArgumentNullException(nameof(definition));
            }

            if (definition.Levels == null || level < 1 || level > definition.Levels.Count)
            {
                throw new ArgumentOutOfRangeException(nameof(level), level,
                    "Weapon " + definition.Id + " has levels 1.." + (definition.Levels == null ? 0 : definition.Levels.Count) + ".");
            }

            WeaponAction action;
            bool tracked;
            switch (definition.Behavior)
            {
                case WeaponBehavior.Projectile:
                case WeaponBehavior.Homing:
                    action = Projectile;
                    tracked = false;
                    break;
                case WeaponBehavior.Orbit:
                    action = Orbit;
                    tracked = true;
                    break;
                case WeaponBehavior.Zone:
                    action = Zone;
                    tracked = true;
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(definition), definition.Behavior, "Unknown weapon behavior.");
            }

            // The tracker starts at 1 and gets the real interval from SetLevel so both paths share HitIntervalFor.
            var instance = new WeaponInstance(definition, level, slotIndex, nextFireStep, action, tracked ? new ContactDamageTracker(1) : null);
            instance.SetLevel(level, attackSpeedBonus);
            return instance;
        }
    }

    // Per-behavior WeaponTick logic; stateless, all state lives on the WeaponInstance.
    internal abstract class WeaponAction
    {
        internal abstract void Tick(WeaponInstance weapon, in StepContext context, in WeaponTickArgs args);
    }
}
