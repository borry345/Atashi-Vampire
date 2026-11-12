using System;
using Survivor.Data;
using UnityEngine;

namespace Survivor.Simulation
{
    /// One between-steps command (dev tools, hot reload, provider switch) queued in Update and applied in the next Begin.
    /// Build it with the static factories; which fields are meaningful depends on Kind.
    public readonly struct PendingCommand
    {
        /// What the command does.
        public readonly PendingCommandKind Kind;

        /// Monster definition id (SpawnMonstersNow, SpawnBossNow).
        public readonly string DefinitionId;

        /// Monster count (SpawnMonstersNow), XP amount (AddXp) or 1 = on / 0 = off (SetSpawnsEnabled).
        public readonly int Count;

        /// Spawn center (SpawnMonstersNow, SpawnBossNow, SpawnChestNow).
        public readonly Vector2 Position;

        /// Spawn ring radius (SpawnMonstersNow).
        public readonly float Radius;

        /// Spawn with the monster cap lifted (SpawnMonstersNow).
        public readonly bool IgnoreCap;

        /// Health value (SetHealth) or despawn distance (SetDespawnDistance).
        public readonly float Value;

        /// Owner-defined payload, e.g. the loadout of SetLoadout or SetPassiveLoadout (opaque to the queue).
        public readonly object Payload;

        /// New data set (SwapData).
        public readonly GameData Data;

        /// Validation report of Data (SwapData).
        public readonly DataValidationReport Report;

        /// Provider and requirements callback (SwitchTargetProvider).
        public readonly SwitchTargetProviderCommand TargetSwitch;

        private PendingCommand(PendingCommandKind kind, string definitionId = null, int count = 0, Vector2 position = default,
                               float radius = 0f, bool ignoreCap = false, float value = 0f, object payload = null,
                               GameData data = null, DataValidationReport report = null, SwitchTargetProviderCommand targetSwitch = default)
        {
            Kind = kind;
            DefinitionId = definitionId;
            Count = count;
            Position = position;
            Radius = radius;
            IgnoreCap = ignoreCap;
            Value = value;
            Payload = payload;
            Data = data;
            Report = report;
            TargetSwitch = targetSwitch;
        }

        /// Dev: replace the weapon loadout; payload is defined by the SetLoadout handler's owner (PLAYER).
        public static PendingCommand SetLoadout(object loadout)
        {
            return new PendingCommand(PendingCommandKind.SetLoadout, payload: loadout);
        }

        /// Dev: replace the passive loadout; payload is defined by the SetPassiveLoadout handler's owner (PLAYER).
        public static PendingCommand SetPassiveLoadout(object loadout)
        {
            return new PendingCommand(PendingCommandKind.SetPassiveLoadout, payload: loadout);
        }

        /// Dev: set the player's health.
        public static PendingCommand SetHealth(float health)
        {
            return new PendingCommand(PendingCommandKind.SetHealth, value: health);
        }

        /// Dev: add xp experience points.
        public static PendingCommand AddXp(int xp)
        {
            return new PendingCommand(PendingCommandKind.AddXp, count: xp);
        }

        /// Dev: add the experience needed for the next level.
        public static PendingCommand LevelUpNow()
        {
            return new PendingCommand(PendingCommandKind.LevelUpNow);
        }

        /// Dev: spawn count monsters of definitionId on a ring of radius around position; count must be positive (ArgumentOutOfRangeException).
        public static PendingCommand SpawnMonstersNow(string definitionId, int count, Vector2 position, float radius, bool ignoreCap)
        {
            if (count <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(count), count, "Spawn count must be positive.");
            }

            return new PendingCommand(PendingCommandKind.SpawnMonstersNow, definitionId, count, position, radius, ignoreCap);
        }

        /// Dev: spawn the boss definitionId at position (refused in Begin when the boss already spawned this run).
        public static PendingCommand SpawnBossNow(string definitionId, Vector2 position)
        {
            return new PendingCommand(PendingCommandKind.SpawnBossNow, definitionId, 1, position, ignoreCap: true);
        }

        /// Dev: spawn a chest at position.
        public static PendingCommand SpawnChestNow(Vector2 position)
        {
            return new PendingCommand(PendingCommandKind.SpawnChestNow, position: position);
        }

        /// Hot reload: GameDataRegistry.TrySwap(data, report) at the next step boundary; report must not be null (ArgumentNullException).
        public static PendingCommand SwapData(GameData data, DataValidationReport report)
        {
            if (report == null)
            {
                throw new ArgumentNullException(nameof(report));
            }

            return new PendingCommand(PendingCommandKind.SwapData, data: data, report: report);
        }

        /// Runtime target provider switch (TAC-F16-5).
        public static PendingCommand SwitchTargetProvider(in SwitchTargetProviderCommand command)
        {
            if (command.Provider == null)
            {
                throw new ArgumentException("SwitchTargetProviderCommand has no provider.", nameof(command));
            }

            return new PendingCommand(PendingCommandKind.SwitchTargetProvider, targetSwitch: command);
        }

        /// Dev: turn the bound policy's normal spawns on or off from the next step (MonsterSpawner.HandleSetSpawnsEnabled).
        public static PendingCommand SetSpawnsEnabled(bool enabled)
        {
            return new PendingCommand(PendingCommandKind.SetSpawnsEnabled, count: enabled ? 1 : 0);
        }

        /// Dev: override the despawn distance (R26); must be positive and finite (ArgumentOutOfRangeException).
        public static PendingCommand SetDespawnDistance(float distance)
        {
            if (!(distance > 0f) || float.IsInfinity(distance))
            {
                throw new ArgumentOutOfRangeException(nameof(distance), distance, "Despawn distance must be positive and finite.");
            }

            return new PendingCommand(PendingCommandKind.SetDespawnDistance, value: distance);
        }
    }
}
