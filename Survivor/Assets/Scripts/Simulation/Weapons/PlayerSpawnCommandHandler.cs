using System;
using Survivor.Core;
using Survivor.Data;

namespace Survivor.Simulation
{
    /// Lifecycle order 0 handler of SpawnPlayerProjectile / SpawnPlayerOrb / SpawnPlayerZone (5.4), registered by the composition
    /// root. Reads the definition of the instance currently in command.SlotIndex (no registry lookup, 5c.3 #10); a command whose
    /// slot no longer holds command.DefinitionId (replaced in the same step) is dropped and counted.
    /// Lifetimes: projectile LifetimeSteps, zone DurationSteps, orb 0 = none (TAC-F6-4).
    public sealed class PlayerSpawnCommandHandler : ISimulationCommandHandler
    {
        private readonly EntityLifecycle _lifecycle;
        private readonly WeaponInventory _weapons;

        /// Creates the handler; null arguments throw ArgumentNullException.
        public PlayerSpawnCommandHandler(EntityLifecycle lifecycle, WeaponInventory weapons)
        {
            _lifecycle = lifecycle ?? throw new ArgumentNullException(nameof(lifecycle));
            _weapons = weapons ?? throw new ArgumentNullException(nameof(weapons));
        }

        /// Commands dropped because the slot's weapon or level no longer matched (or the orb index already existed).
        public int DroppedCommandCount { get; private set; }

        /// Spawns the entity the command describes through EntityLifecycle.Spawn.
        public void Handle(in SimulationCommand command, in StepContext context)
        {
            if (!_weapons.TryGetSlot(command.SlotIndex, out WeaponInstance weapon)
                || !string.Equals(weapon.Id, command.DefinitionId, StringComparison.Ordinal)
                || command.LevelIndex < 0 || command.LevelIndex >= weapon.Definition.Levels.Count)
            {
                DroppedCommandCount++;
                return;
            }

            WeaponLevelDefinition level = weapon.Definition.Levels[command.LevelIndex];
            switch (command.Kind)
            {
                case SimulationCommandKind.SpawnPlayerProjectile:
                    SpawnProjectile(command, weapon, level);
                    break;
                case SimulationCommandKind.SpawnPlayerOrb:
                    SpawnOrb(command, weapon, level);
                    break;
                case SimulationCommandKind.SpawnPlayerZone:
                    SpawnZone(command, weapon, level);
                    break;
                default:
                    throw new InvalidOperationException("PlayerSpawnCommandHandler cannot handle " + command.Kind + ".");
            }
        }

        private void SpawnProjectile(in SimulationCommand command, WeaponInstance weapon, WeaponLevelDefinition level)
        {
            string weaponId = weapon.Id;
            int slot = weapon.SlotIndex;
            UnityEngine.Vector2 direction = command.Direction;
            bool homing = weapon.Behavior == WeaponBehavior.Homing;
            _lifecycle.Spawn<PlayerProjectile>(EntityCategory.PlayerProjectile, command.Position, Math.Max(1, level.LifetimeSteps),
                p => p.Initialize(weaponId, slot, level.Damage, direction, level.ProjectileSpeedPerStep, level.ProjectileRadius,
                    homing, level.PierceCount));
        }

        private void SpawnOrb(in SimulationCommand command, WeaponInstance weapon, WeaponLevelDefinition level)
        {
            int orbIndex = command.OrbIndex;
            if (weapon.FindOrb(orbIndex) != null)
            {
                DroppedCommandCount++;
                return;
            }

            OrbitOrb orb = _lifecycle.Spawn<OrbitOrb>(EntityCategory.PlayerOrb, command.Position, 0,
                o => o.Initialize(weapon, orbIndex, level.OrbRadius));
            weapon.AddChild(orb);
        }

        private void SpawnZone(in SimulationCommand command, WeaponInstance weapon, WeaponLevelDefinition level)
        {
            DamageZone zone = _lifecycle.Spawn<DamageZone>(EntityCategory.PlayerZone, command.Position, Math.Max(1, level.DurationSteps),
                z => z.Initialize(weapon, level.ZoneRadius));
            weapon.AddChild(zone);
        }
    }
}
