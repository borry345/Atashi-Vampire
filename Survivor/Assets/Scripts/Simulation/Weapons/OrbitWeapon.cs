using System;
using System.Collections.Generic;
using Survivor.Core;
using Survivor.Data;
using UnityEngine;

namespace Survivor.Simulation
{
    // W2/E2: orbs exist while the weapon is owned (no lifetime, no fire cooldown). Every WeaponTick it writes each orb's position
    // from the step number (TAC-F6-4, no accumulated error), then reconciles the orb count with the level's orbCount: missing
    // indices get SpawnPlayerOrb (only while weapons are enabled, TAC-F2-5), surplus orbs get Despawn(Replaced) from the
    // highest index down (TAC-F6-8).
    internal sealed class OrbitWeapon : WeaponAction
    {
        private const double FullTurn = 2d * Math.PI;

        private readonly List<OrbitOrb> _surplus = new List<OrbitOrb>();

        internal override void Tick(WeaponInstance weapon, in StepContext context, in WeaponTickArgs args)
        {
            WeaponLevelDefinition level = weapon.LevelDefinition;
            int orbCount = Math.Max(0, level.OrbCount);
            _surplus.Clear();
            IReadOnlyList<SimEntity> children = weapon.Children;
            for (int i = 0; i < children.Count; i++)
            {
                if (!(children[i] is OrbitOrb orb))
                {
                    continue;
                }

                if (orb.OrbIndex >= orbCount)
                {
                    _surplus.Add(orb);
                    continue;
                }

                orb.MoveTo(OrbPosition(context.PlayerPosition, level, orb.OrbIndex, orbCount, context.Step));
                if (orb.Body.Radius != level.OrbRadius)
                {
                    // Upgrades may grow the orb; the strategy must hear about radius changes (ICollisionStrategy contract).
                    orb.Body.Radius = level.OrbRadius;
                    args.Collisions?.NotifyRadiusChanged(orb.Body);
                }
            }

            _surplus.Sort((a, b) => b.OrbIndex.CompareTo(a.OrbIndex));
            for (int i = 0; i < _surplus.Count; i++)
            {
                context.Commands.Enqueue(new SimulationCommand(
                    SimulationCommandKind.Despawn, args.Source, target: _surplus[i].Id, reason: DespawnReason.Replaced));
            }

            _surplus.Clear();
            if (!context.Rules.WeaponsEnabled)
            {
                return;
            }

            for (int index = 0; index < orbCount; index++)
            {
                if (weapon.FindOrb(index) != null)
                {
                    continue;
                }

                context.Commands.Enqueue(new SimulationCommand(
                    SimulationCommandKind.SpawnPlayerOrb, args.Source,
                    position: OrbPosition(context.PlayerPosition, level, index, orbCount, context.Step),
                    radius: level.OrbRadius, definitionId: weapon.Id, levelIndex: weapon.Level - 1,
                    slotIndex: weapon.SlotIndex, orbIndex: index));
            }
        }

        // playerPos + R(cos t, sin t), t = t0 + w*step + 2*pi*i/orbCount with t0 = 0 (TAC-F6-4); computed in double from the step.
        internal static Vector2 OrbPosition(Vector2 playerPosition, WeaponLevelDefinition level, int orbIndex, int orbCount, long step)
        {
            double theta = (double)level.AngularSpeedRadPerStep * step + FullTurn * orbIndex / orbCount;
            return new Vector2(
                playerPosition.x + (float)(level.OrbitRadius * Math.Cos(theta)),
                playerPosition.y + (float)(level.OrbitRadius * Math.Sin(theta)));
        }
    }
}
