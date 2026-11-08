using System;
using Survivor.Core;
using Survivor.Data;
using UnityEngine;

namespace Survivor.Simulation
{
    // W1/E1 (projectile) and W4 (homing): fires at the nearest monster when step >= NextFireStep, weapons are enabled and a
    // target exists; without a target nothing fires and NextFireStep stays (TAC-F2-2). Projectiles are spawned by command (TAC-F6-8).
    internal sealed class ProjectileWeapon : WeaponAction
    {
        internal override void Tick(WeaponInstance weapon, in StepContext context, in WeaponTickArgs args)
        {
            if (!context.Rules.WeaponsEnabled || context.Step < weapon.NextFireStep)
            {
                return;
            }

            if (!context.Targets.TryFindNearest(context.PlayerPosition, out TargetHit hit))
            {
                return;
            }

            WeaponLevelDefinition level = weapon.LevelDefinition;
            Vector2 aim = ToUnit(MoveMath.DirectionOrDefault(hit.Position - context.PlayerPosition));
            int count = Math.Max(1, level.ProjectileCount);
            for (int i = 0; i < count; i++)
            {
                // projectileCount > 1 splits spreadDegrees evenly from -spread/2 to +spread/2 (TAC-F6-2).
                double offsetDegrees = count == 1 ? 0d : -level.SpreadDegrees * 0.5 + level.SpreadDegrees * i / (count - 1);
                Vector2 direction = Rotate(aim, offsetDegrees);
                context.Commands.Enqueue(new SimulationCommand(
                    SimulationCommandKind.SpawnPlayerProjectile, args.Source,
                    position: context.PlayerPosition, direction: direction, definitionId: weapon.Id,
                    levelIndex: weapon.Level - 1, slotIndex: weapon.SlotIndex));
            }

            weapon.NextFireStep = context.Step + StatMath.ApplyAttackSpeed(level.CooldownSteps, args.AttackSpeedBonus);
        }

        // Unit vector of a non-zero v (callers pass DirectionOrDefault output, so v is never exactly zero).
        internal static Vector2 ToUnit(Vector2 v)
        {
            double length = Math.Sqrt((double)v.x * v.x + (double)v.y * v.y);
            if (length <= 0d)
            {
                return Vector2.right;
            }

            return new Vector2((float)(v.x / length), (float)(v.y / length));
        }

        private static Vector2 Rotate(Vector2 v, double degrees)
        {
            if (degrees == 0d)
            {
                return v;
            }

            double radians = degrees * Math.PI / 180d;
            double cos = Math.Cos(radians);
            double sin = Math.Sin(radians);
            return new Vector2((float)(v.x * cos - v.y * sin), (float)(v.x * sin + v.y * cos));
        }
    }
}
