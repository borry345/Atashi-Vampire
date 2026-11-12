using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Survivor.Core;
using Survivor.Data;

namespace Survivor.Simulation
{
    /// The only writer of WeaponInventory / PassiveInventory / heal (A11, 5.14). FLOW (level-up), chests and dev commands call it.
    /// First-ownership rule (TAC-F2-2): every new weapon instance starts with NextFireStep = the step its ownership applied.
    public sealed class PlayerProgressionApplier
    {
        private readonly SimulationClock _clock;
        private readonly WeaponInventory _weapons;
        private readonly PassiveInventory _passives;
        private readonly PlayerHealth _health;
        private readonly PlayerEntity _player;
        private readonly Func<GameData> _currentData;
        private readonly List<SimEntity> _children = new List<SimEntity>();

        /// Creates the applier; currentData returns GameDataRegistry.Current. Null arguments throw ArgumentNullException.
        public PlayerProgressionApplier(SimulationClock clock, WeaponInventory weapons, PassiveInventory passives, PlayerHealth health,
                                        PlayerEntity player, Func<GameData> currentData)
        {
            _clock = clock ?? throw new ArgumentNullException(nameof(clock));
            _weapons = weapons ?? throw new ArgumentNullException(nameof(weapons));
            _passives = passives ?? throw new ArgumentNullException(nameof(passives));
            _health = health ?? throw new ArgumentNullException(nameof(health));
            _player = player ?? throw new ArgumentNullException(nameof(player));
            _currentData = currentData ?? throw new ArgumentNullException(nameof(currentData));
        }

        /// Increments on every weapon or passive change (level-up, chest, evolution, loadout, run start); dev measurement
        /// sessions compare it to detect a changed loadout (TAC-F19-5).
        public int ChangeCount { get; private set; }

        /// The latest chest results (TAC-F4-9), written by ChestEffectResolver; emptied by ResetForRun (LastSequence is kept).
        public ChestResultLog ChestResults { get; } = new ChestResultLog();

        // Read by ChestEffectResolver for the passive upgrade candidates (TAC-F4-10); writes still go through this class (A11).
        internal PassiveInventory Passives => _passives;

        /// Run start (RunSession.Reset step 4, after DespawnAll and the player registration): empties weapons and passives, resets
        /// health to the current MaxHealth, gives the starting weapon at level 1 with NextFireStep = clock.Step (TAC-F2-1) and
        /// empties ChestResults. Throws InvalidOperationException when player stats or the starting weapon are missing from the
        /// current data.
        public void ResetForRun()
        {
            GameData data = Data();
            PlayerStatsDefinition stats = data.PlayerStats ?? throw new InvalidOperationException("PlayerProgressionApplier: no player stats are loaded.");
            _weapons.Clear();
            _passives.Clear();
            _health.ResetForRun(stats.MaxHealth);
            ChestResults.Clear();
            if (!data.TryGetWeapon(stats.StartingWeaponId, out WeaponDefinition starting))
            {
                throw new InvalidOperationException("PlayerProgressionApplier: starting weapon '" + stats.StartingWeaponId + "' is not defined.");
            }

            _weapons.SetSlot(0, WeaponFactory.Create(starting, 1, 0, _clock.Step, AttackSpeedBonus()));
            _player.RefreshMagnetRadius();
            ChangeCount++;
        }

        /// Applies a level-up choice; only while clock.CloseReason == LevelUp && !clock.StepActive && !clock.IsRunning (TAC-0-1 2),
        /// else InvalidOperationException. WeaponNew adds a level-1 instance to the first free slot (NextFireStep = clock.Step);
        /// WeaponUpgrade/PassiveUpgrade add one level (R22 checked); PassiveNew adds level 1; Heal heals. An option that is not
        /// applicable (unknown id, already owned, slots full, not upgradable) throws InvalidOperationException. Children are not
        /// touched here: orb counts are reconciled by the next WeaponTick (TAC-F6-8).
        public void ApplyLevelUpOption(in UpgradeOption option)
        {
            // IsRunning is checked too: CloseReason keeps its last value after Open(), so it alone does not prove the gate is closed.
            if (_clock.CloseReason != GateCloseReason.LevelUp || _clock.StepActive || _clock.IsRunning)
            {
                throw new InvalidOperationException("PlayerProgressionApplier.ApplyLevelUpOption is only valid while the gate is closed for a level-up"
                    + " (close reason " + _clock.CloseReason + ", step active " + _clock.StepActive + ", running " + _clock.IsRunning + ").");
            }

            switch (option.Kind)
            {
                case UpgradeKind.WeaponNew:
                    AddWeapon(option.TargetId, _clock.Step);
                    break;
                case UpgradeKind.WeaponUpgrade:
                    UpgradeWeapon(_weapons.IndexOf(option.TargetId), option.TargetId);
                    break;
                case UpgradeKind.PassiveNew:
                    AddPassive(option.TargetId);
                    break;
                case UpgradeKind.PassiveUpgrade:
                    UpgradePassive(option.TargetId);
                    break;
                case UpgradeKind.Heal:
                    Heal();
                    break;
                default:
                    throw new InvalidOperationException("PlayerProgressionApplier: unknown upgrade kind " + option.Kind + ".");
            }
        }

        /// Chest effect 2: upgrades the weapon in slotIndex by one level (R22); InvalidOperationException when the slot is empty or
        /// the weapon is not upgradable. Orb count changes are reconciled by the next WeaponTick.
        public void ApplyChestUpgrade(int slotIndex)
        {
            if (!_weapons.TryGetSlot(slotIndex, out WeaponInstance weapon))
            {
                throw new InvalidOperationException("PlayerProgressionApplier.ApplyChestUpgrade: weapon slot " + slotIndex + " is empty.");
            }

            UpgradeWeapon(slotIndex, weapon.Id);
        }

        /// Chest effect 2 for a passive (TAC-F4-10): upgrades the passive in slotIndex by one level (R22) and refreshes its effects
        /// like a level-up; InvalidOperationException when the slot is outside [0, MaxPassiveSlots), empty, or not upgradable.
        public void ApplyChestPassiveUpgrade(int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= GameRules.MaxPassiveSlots || _passives.Slots[slotIndex] == null)
            {
                throw new InvalidOperationException("PlayerProgressionApplier.ApplyChestPassiveUpgrade: passive slot " + slotIndex + " is empty.");
            }

            UpgradePassive(_passives.Slots[slotIndex].Id);
        }

        /// Chest effect 1 (Resolve): replaces the weapon in slotIndex with rule's result weapon at level 1, NextFireStep =
        /// context.Step (TAC-F7-2). Every live child of the original gets Despawn(Replaced) (Source = player id, ascending id), the
        /// original id joins ConsumedByEvolution, and one Info line is logged. Throws ArgumentNullException for a null rule and
        /// InvalidOperationException when the slot does not hold rule.WeaponId or the result weapon is not defined.
        public void ApplyEvolution(int slotIndex, CombinationRule rule, in StepContext context)
        {
            if (rule == null)
            {
                throw new ArgumentNullException(nameof(rule));
            }

            if (!_weapons.TryGetSlot(slotIndex, out WeaponInstance original)
                || !string.Equals(original.Id, rule.WeaponId, StringComparison.Ordinal))
            {
                throw new InvalidOperationException("PlayerProgressionApplier.ApplyEvolution: slot " + slotIndex + " does not hold " + rule.WeaponId + ".");
            }

            if (!Data().TryGetWeapon(rule.ResultWeaponId, out WeaponDefinition result))
            {
                throw new InvalidOperationException("PlayerProgressionApplier.ApplyEvolution: result weapon " + rule.ResultWeaponId + " is not defined.");
            }

            DespawnChildren(original, context);
            _weapons.SetSlot(slotIndex, WeaponFactory.Create(result, 1, slotIndex, context.Step, AttackSpeedBonus()));
            _weapons.AddConsumedByEvolution(original.Id);
            ChangeCount++;
            LoopLog.Info("Weapon evolved: slot " + slotIndex.ToString(CultureInfo.InvariantCulture) + " " + original.Id + " -> " + result.Id
                + " at step " + context.Step.ToString(CultureInfo.InvariantCulture) + ".");
        }

        /// R23 heal (chest 2 and level-up Heal); returns the amount actually restored (hp after - hp before, 0 at full health).
        public float Heal()
        {
            float before = _health.Hp;
            _health.Heal();
            return _health.Hp - before;
        }

        /// Dev loadout (Begin, A11): slot order = list order, at most MaxWeaponSlots entries; an entry whose WeaponId is null
        /// leaves that slot empty (no definition, level or duplicate check). A weapon that stays in the same slot keeps its
        /// instance (cooldown, children) and takes the requested level; every other current weapon's children get
        /// Despawn(Replaced) and every new weapon starts with NextFireStep = context.Step. Validates everything first:
        /// ArgumentNullException for null, ArgumentException for too many entries, unknown ids, duplicates or levels out of range.
        public void SetLoadout(IReadOnlyList<WeaponLoadoutEntry> loadout, in StepContext context)
        {
            if (loadout == null)
            {
                throw new ArgumentNullException(nameof(loadout));
            }

            if (loadout.Count > GameRules.MaxWeaponSlots)
            {
                throw new ArgumentException("SetLoadout: " + loadout.Count + " weapons exceed " + GameRules.MaxWeaponSlots + " slots.", nameof(loadout));
            }

            GameData data = Data();
            var definitions = new WeaponDefinition[loadout.Count];
            for (int i = 0; i < loadout.Count; i++)
            {
                WeaponLoadoutEntry entry = loadout[i];
                if (entry.WeaponId == null)
                {
                    continue;
                }

                if (!data.TryGetWeapon(entry.WeaponId, out WeaponDefinition definition))
                {
                    throw new ArgumentException("SetLoadout: weapon '" + entry.WeaponId + "' is not defined.", nameof(loadout));
                }

                if (entry.Level < 1 || entry.Level > definition.Levels.Count)
                {
                    throw new ArgumentException("SetLoadout: level " + entry.Level + " of " + entry.WeaponId + " is outside 1.."
                        + definition.Levels.Count + ".", nameof(loadout));
                }

                for (int j = 0; j < i; j++)
                {
                    if (string.Equals(loadout[j].WeaponId, entry.WeaponId, StringComparison.Ordinal))
                    {
                        throw new ArgumentException("SetLoadout: weapon '" + entry.WeaponId + "' appears twice.", nameof(loadout));
                    }
                }

                definitions[i] = definition;
            }

            float bonus = AttackSpeedBonus();
            for (int slot = 0; slot < GameRules.MaxWeaponSlots; slot++)
            {
                _weapons.TryGetSlot(slot, out WeaponInstance current);
                bool wanted = slot < loadout.Count && loadout[slot].WeaponId != null;
                if (current != null && wanted && string.Equals(current.Id, loadout[slot].WeaponId, StringComparison.Ordinal))
                {
                    current.SetLevel(loadout[slot].Level, bonus);
                    continue;
                }

                if (current != null)
                {
                    DespawnChildren(current, context);
                }

                _weapons.SetSlot(slot, wanted ? WeaponFactory.Create(definitions[slot], loadout[slot].Level, slot, context.Step, bonus) : null);
            }

            ChangeCount++;
            LoopLog.Info("Loadout set at step " + context.Step.ToString(CultureInfo.InvariantCulture) + ": " + Describe(loadout) + ".");
        }

        /// Dev passive loadout (Begin, A11, TAC-F19-20): slot order = list order, at most MaxPassiveSlots entries; an entry whose
        /// PassiveId is null leaves that slot empty, and an all-empty (or empty) list is allowed. A passive that stays in the same
        /// slot keeps its instance (and its captured definition) and takes the requested level; any other slot gets a new
        /// instance from the current data. Validates everything first: ArgumentNullException for null, ArgumentException for too
        /// many entries, unknown ids, duplicates or levels out of range. Refreshes passive effects once and logs one Info line.
        public void SetPassiveLoadout(IReadOnlyList<PassiveLoadoutEntry> loadout, in StepContext context)
        {
            if (loadout == null)
            {
                throw new ArgumentNullException(nameof(loadout));
            }

            if (loadout.Count > GameRules.MaxPassiveSlots)
            {
                throw new ArgumentException("SetPassiveLoadout: " + loadout.Count + " passives exceed " + GameRules.MaxPassiveSlots + " slots.", nameof(loadout));
            }

            GameData data = Data();
            var definitions = new PassiveDefinition[loadout.Count];
            for (int i = 0; i < loadout.Count; i++)
            {
                PassiveLoadoutEntry entry = loadout[i];
                if (entry.PassiveId == null)
                {
                    continue;
                }

                for (int j = 0; j < i; j++)
                {
                    if (string.Equals(loadout[j].PassiveId, entry.PassiveId, StringComparison.Ordinal))
                    {
                        throw new ArgumentException("SetPassiveLoadout: passive '" + entry.PassiveId + "' appears twice.", nameof(loadout));
                    }
                }

                // A passive kept in its slot keeps the definition captured when it was acquired (5.14 hot reload rule).
                PassiveInstance current = _passives.Slots[i];
                PassiveDefinition definition;
                if (current != null && string.Equals(current.Id, entry.PassiveId, StringComparison.Ordinal))
                {
                    definition = current.Definition;
                }
                else if (!data.TryGetPassive(entry.PassiveId, out definition))
                {
                    throw new ArgumentException("SetPassiveLoadout: passive '" + entry.PassiveId + "' is not defined.", nameof(loadout));
                }

                if (entry.Level < 1 || entry.Level > definition.Levels.Count)
                {
                    throw new ArgumentException("SetPassiveLoadout: level " + entry.Level + " of " + entry.PassiveId + " is outside 1.."
                        + definition.Levels.Count + ".", nameof(loadout));
                }

                definitions[i] = definition;
            }

            for (int slot = 0; slot < GameRules.MaxPassiveSlots; slot++)
            {
                PassiveInstance current = _passives.Slots[slot];
                bool wanted = slot < loadout.Count && loadout[slot].PassiveId != null;
                if (current != null && wanted && string.Equals(current.Id, loadout[slot].PassiveId, StringComparison.Ordinal))
                {
                    current.Level = loadout[slot].Level;
                    continue;
                }

                _passives.SetSlot(slot, wanted ? new PassiveInstance(definitions[slot], loadout[slot].Level) : null);
            }

            OnPassivesChanged();
            LoopLog.Info("Passive loadout set at step " + context.Step.ToString(CultureInfo.InvariantCulture) + ": " + Describe(loadout) + ".");
        }

        /// Dev SetHealth (Begin): Hp = hp clamped to [0, MaxHp].
        public void SetHealth(float hp)
        {
            _health.SetHp(hp);
        }

        /// PendingCommandQueue handler for SetLoadout (registered by the composition root): Payload must be an
        /// IReadOnlyList&lt;WeaponLoadoutEntry&gt;, else ArgumentException.
        public void HandleSetLoadout(PendingCommand command, StepContext context)
        {
            if (!(command.Payload is IReadOnlyList<WeaponLoadoutEntry> loadout))
            {
                throw new ArgumentException("SetLoadout payload must be IReadOnlyList<WeaponLoadoutEntry> (got "
                    + (command.Payload == null ? "null" : command.Payload.GetType().Name) + ").", nameof(command));
            }

            SetLoadout(loadout, context);
        }

        /// PendingCommandQueue handler for SetPassiveLoadout (registered by the composition root): Payload must be an
        /// IReadOnlyList&lt;PassiveLoadoutEntry&gt;, else ArgumentException.
        public void HandleSetPassiveLoadout(PendingCommand command, StepContext context)
        {
            if (!(command.Payload is IReadOnlyList<PassiveLoadoutEntry> loadout))
            {
                throw new ArgumentException("SetPassiveLoadout payload must be IReadOnlyList<PassiveLoadoutEntry> (got "
                    + (command.Payload == null ? "null" : command.Payload.GetType().Name) + ").", nameof(command));
            }

            SetPassiveLoadout(loadout, context);
        }

        /// PendingCommandQueue handler for SetHealth (registered by the composition root): SetHealth(command.Value).
        public void HandleSetHealth(PendingCommand command, StepContext context)
        {
            SetHealth(command.Value);
        }

        private void AddWeapon(string weaponId, long ownedStep)
        {
            if (!Data().TryGetWeapon(weaponId, out WeaponDefinition definition))
            {
                throw new InvalidOperationException("PlayerProgressionApplier: weapon '" + weaponId + "' is not defined.");
            }

            if (_weapons.Owns(weaponId))
            {
                throw new InvalidOperationException("PlayerProgressionApplier: weapon " + weaponId + " is already owned (upgrade it instead).");
            }

            if (definition.EvolvedOnly)
            {
                throw new InvalidOperationException("PlayerProgressionApplier: weapon " + weaponId + " is evolution-only.");
            }

            int slot = _weapons.FirstFreeSlot();
            if (slot < 0)
            {
                throw new InvalidOperationException("PlayerProgressionApplier: every weapon slot is taken.");
            }

            _weapons.SetSlot(slot, WeaponFactory.Create(definition, 1, slot, ownedStep, AttackSpeedBonus()));
            ChangeCount++;
        }

        private void UpgradeWeapon(int slotIndex, string weaponId)
        {
            if (!_weapons.TryGetSlot(slotIndex, out WeaponInstance weapon))
            {
                throw new InvalidOperationException("PlayerProgressionApplier: weapon " + weaponId + " is not owned.");
            }

            if (!ProgressionRules.IsWeaponUpgradable(weapon.Level, weapon.Definition.Levels.Count, weapon.Definition.EvolvedOnly))
            {
                throw new InvalidOperationException("PlayerProgressionApplier: weapon " + weapon.Id + " at level " + weapon.Level + " cannot be upgraded.");
            }

            weapon.SetLevel(weapon.Level + 1, AttackSpeedBonus());
            ChangeCount++;
        }

        private void AddPassive(string passiveId)
        {
            if (!Data().TryGetPassive(passiveId, out PassiveDefinition definition))
            {
                throw new InvalidOperationException("PlayerProgressionApplier: passive '" + passiveId + "' is not defined.");
            }

            if (_passives.Owns(passiveId))
            {
                throw new InvalidOperationException("PlayerProgressionApplier: passive " + passiveId + " is already owned (upgrade it instead).");
            }

            int slot = _passives.FirstFreeSlot();
            if (slot < 0)
            {
                throw new InvalidOperationException("PlayerProgressionApplier: every passive slot is taken.");
            }

            _passives.SetSlot(slot, new PassiveInstance(definition, 1));
            OnPassivesChanged();
        }

        private void UpgradePassive(string passiveId)
        {
            PassiveInstance passive = _passives.Find(passiveId)
                ?? throw new InvalidOperationException("PlayerProgressionApplier: passive " + passiveId + " is not owned.");
            if (!ProgressionRules.IsPassiveUpgradable(passive.Level, passive.Definition.Levels.Count))
            {
                throw new InvalidOperationException("PlayerProgressionApplier: passive " + passiveId + " at level " + passive.Level + " cannot be upgraded.");
            }

            passive.Level++;
            OnPassivesChanged();
        }

        // P1 reaches orbit tracker intervals from the next computation (TAC-F2-3); P3 resizes the magnet (5.12 rev 16).
        private void OnPassivesChanged()
        {
            _weapons.RefreshAttackSpeed(AttackSpeedBonus());
            _player.RefreshMagnetRadius();
            ChangeCount++;
        }

        private void DespawnChildren(WeaponInstance weapon, in StepContext context)
        {
            _children.Clear();
            for (int i = 0; i < weapon.Children.Count; i++)
            {
                _children.Add(weapon.Children[i]);
            }

            _children.Sort((a, b) => a.Id.CompareTo(b.Id));
            for (int i = 0; i < _children.Count; i++)
            {
                context.Commands.Enqueue(new SimulationCommand(
                    SimulationCommandKind.Despawn, _player.Body.Id, target: _children[i].Id, reason: DespawnReason.Replaced));
            }

            _children.Clear();
        }

        private float AttackSpeedBonus()
        {
            return _passives.BonusOf(PassiveStat.AttackSpeed);
        }

        private GameData Data()
        {
            return _currentData() ?? throw new InvalidOperationException("PlayerProgressionApplier: no game data is loaded.");
        }

        private static string Describe(IReadOnlyList<WeaponLoadoutEntry> loadout)
        {
            var builder = new StringBuilder("[");
            for (int i = 0; i < loadout.Count; i++)
            {
                if (i > 0)
                {
                    builder.Append(", ");
                }

                if (loadout[i].WeaponId == null)
                {
                    builder.Append('-');
                    continue;
                }

                builder.Append(loadout[i].WeaponId).Append(':').Append(loadout[i].Level.ToString(CultureInfo.InvariantCulture));
            }

            return builder.Append(']').ToString();
        }

        private static string Describe(IReadOnlyList<PassiveLoadoutEntry> loadout)
        {
            var builder = new StringBuilder("[");
            for (int i = 0; i < loadout.Count; i++)
            {
                if (i > 0)
                {
                    builder.Append(", ");
                }

                if (loadout[i].PassiveId == null)
                {
                    builder.Append('-');
                    continue;
                }

                builder.Append(loadout[i].PassiveId).Append(':').Append(loadout[i].Level.ToString(CultureInfo.InvariantCulture));
            }

            return builder.Append(']').ToString();
        }
    }
}
