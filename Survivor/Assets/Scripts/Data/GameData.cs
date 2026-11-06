using System;
using System.Collections.Generic;

namespace Survivor.Data
{
    /// Immutable loaded data set; consumers never parse JSON. Parts may be null/empty when the report is not playable.
    public sealed class GameData
    {
        private readonly Dictionary<string, WeaponDefinition> _weaponsById = new Dictionary<string, WeaponDefinition>(StringComparer.Ordinal);
        private readonly Dictionary<string, MonsterDefinition> _monstersById = new Dictionary<string, MonsterDefinition>(StringComparer.Ordinal);
        private readonly Dictionary<string, PassiveDefinition> _passivesById = new Dictionary<string, PassiveDefinition>(StringComparer.Ordinal);

        internal GameData(
            IReadOnlyList<WeaponDefinition> weapons,
            IReadOnlyList<MonsterDefinition> monsters,
            IReadOnlyList<CombinationRule> combinations,
            SpawnTableDefinition spawnTable,
            IReadOnlyList<PassiveDefinition> passives,
            LevelUpPoolDefinition levelUpPool,
            PlayerStatsDefinition playerStats)
        {
            Weapons = weapons ?? new WeaponDefinition[0];
            Monsters = monsters ?? new MonsterDefinition[0];
            Combinations = combinations ?? new CombinationRule[0];
            SpawnTable = spawnTable;
            Passives = passives ?? new PassiveDefinition[0];
            LevelUpPool = levelUpPool ?? LevelUpPoolDefinition.UniformFallback;
            PlayerStats = playerStats;

            for (int i = 0; i < Weapons.Count; i++)
            {
                _weaponsById[Weapons[i].Id] = Weapons[i];
            }

            for (int i = 0; i < Monsters.Count; i++)
            {
                _monstersById[Monsters[i].Id] = Monsters[i];
            }

            for (int i = 0; i < Passives.Count; i++)
            {
                _passivesById[Passives[i].Id] = Passives[i];
            }
        }

        /// Valid weapons in file order.
        public IReadOnlyList<WeaponDefinition> Weapons { get; }

        /// Valid monsters in file order.
        public IReadOnlyList<MonsterDefinition> Monsters { get; }

        /// Valid combination rules in file order; empty when the file is missing or broken.
        public IReadOnlyList<CombinationRule> Combinations { get; }

        /// Spawn table; null when missing or invalid (not playable).
        public SpawnTableDefinition SpawnTable { get; }

        /// Valid passives in file order; empty when the file is missing or broken.
        public IReadOnlyList<PassiveDefinition> Passives { get; }

        /// Level-up pool; LevelUpPoolDefinition.UniformFallback when missing or broken.
        public LevelUpPoolDefinition LevelUpPool { get; }

        /// Player base stats; null when missing or invalid (not playable).
        public PlayerStatsDefinition PlayerStats { get; }

        /// Looks up a weapon by id (ordinal, case-sensitive).
        public bool TryGetWeapon(string id, out WeaponDefinition def)
        {
            if (id == null)
            {
                def = null;
                return false;
            }

            return _weaponsById.TryGetValue(id, out def);
        }

        /// Looks up a monster by id (ordinal, case-sensitive).
        public bool TryGetMonster(string id, out MonsterDefinition def)
        {
            if (id == null)
            {
                def = null;
                return false;
            }

            return _monstersById.TryGetValue(id, out def);
        }

        /// Looks up a passive by id (ordinal, case-sensitive).
        public bool TryGetPassive(string id, out PassiveDefinition def)
        {
            if (id == null)
            {
                def = null;
                return false;
            }

            return _passivesById.TryGetValue(id, out def);
        }
    }
}
