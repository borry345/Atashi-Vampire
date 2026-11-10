using Survivor.Core;
using Survivor.Data;
using UnityEngine;

namespace Survivor.Simulation
{
    // W3: when the cooldown allows, lays a zone by command (TAC-F6-5). placement nearestEnemy needs a target (none: no command,
    // cooldown kept, retried next step); placement player never queries targets and always spends the cooldown.
    internal sealed class ZoneWeapon : WeaponAction
    {
        internal override void Tick(WeaponInstance weapon, in StepContext context, in WeaponTickArgs args)
        {
            if (!context.Rules.WeaponsEnabled || context.Step < weapon.NextFireStep)
            {
                return;
            }

            WeaponLevelDefinition level = weapon.LevelDefinition;
            Vector2 position;
            if (level.Placement == ZonePlacement.Player)
            {
                position = context.PlayerPosition;
            }
            else
            {
                if (!context.Targets.TryFindNearest(context.PlayerPosition, out TargetHit hit))
                {
                    return;
                }
                weapon.LastTargetId = hit.Id;

                position = hit.Position;
            }

            context.Commands.Enqueue(new SimulationCommand(
                SimulationCommandKind.SpawnPlayerZone, args.Source,
                position: position, radius: level.ZoneRadius, definitionId: weapon.Id,
                levelIndex: weapon.Level - 1, slotIndex: weapon.SlotIndex));

            // P1 shortens how often zones are laid, never the zone's own hit interval (TAC-F2-3).
            weapon.NextFireStep = context.Step + StatMath.ApplyAttackSpeed(level.CooldownSteps, args.AttackSpeedBonus);
        }
    }
}
