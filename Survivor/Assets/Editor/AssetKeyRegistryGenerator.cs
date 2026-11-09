using System.Collections.Generic;
using System.IO;
using Survivor.Data;
using UnityEditor;
using UnityEngine;

namespace Survivor.Editor
{
    /// Creates (or tops up) Assets/Resources/AssetKeyRegistry.asset with one default-circle entry per asset key found in the
    /// StreamingAssets JSON; the tint comes from the last key token. Existing entries are never changed so hand edits survive.
    public static class AssetKeyRegistryGenerator
    {
        /// Folder that holds the registry so AssetKeyRegistry.LoadDefault finds it.
        public const string ResourcesFolder = "Assets/Resources";

        /// Project path of the generated registry asset.
        public const string AssetPath = ResourcesFolder + "/" + AssetKeyRegistry.ResourcePath + ".asset";

        // Area keys (player zones, boss hazard zones) are drawn translucent so units under them stay visible.
        private const float AreaAlpha = 0.35f;

        private static readonly Dictionary<string, Color32> TokenColors = new Dictionary<string, Color32>
        {
            { "green", new Color32(0x3D, 0xBE, 0x5A, 0xFF) },
            { "orange", new Color32(0xF2, 0x8C, 0x28, 0xFF) },
            { "purple", new Color32(0x9B, 0x59, 0xD0, 0xFF) },
            { "red", new Color32(0xE0, 0x40, 0x40, 0xFF) },
            { "white", new Color32(0xF2, 0xF2, 0xF2, 0xFF) },
            { "gold", new Color32(0xE6, 0xB4, 0x22, 0xFF) },
            { "pink", new Color32(0xF0, 0x62, 0x92, 0xFF) },
            { "cyan", new Color32(0x30, 0xC8, 0xD8, 0xFF) },
            { "blue", new Color32(0x3F, 0x7F, 0xE0, 0xFF) },
            { "yellow", new Color32(0xF2, 0xD4, 0x3D, 0xFF) },
        };

        /// Menu entry; same as Generate.
        [MenuItem("Survivor/Create Default Asset Key Registry")]
        public static void CreateFromMenu()
        {
            Generate();
        }

        /// Batch entry (-executeMethod Survivor.Editor.AssetKeyRegistryGenerator.Generate): loads the JSON data, then creates the
        /// registry or appends only the keys it lacks, and saves. The outcome is logged.
        public static void Generate()
        {
            (GameData data, DataValidationReport report) = GameDataLoader.LoadAll(Application.streamingAssetsPath);
            if (data == null)
            {
                Debug.LogError("[Editor] AssetKeyRegistryGenerator: game data could not be loaded (" + report + ").");
                return;
            }

            List<(string key, bool isArea)> keys = CollectKeys(data);
            if (!AssetDatabase.IsValidFolder(ResourcesFolder))
            {
                AssetDatabase.CreateFolder("Assets", Path.GetFileName(ResourcesFolder));
            }

            AssetKeyRegistry registry = AssetDatabase.LoadAssetAtPath<AssetKeyRegistry>(AssetPath);
            bool created = registry == null;
            if (created)
            {
                registry = ScriptableObject.CreateInstance<AssetKeyRegistry>();
            }

            if (registry.Entries == null)
            {
                registry.Entries = new List<AssetKeyRegistry.Entry>();
            }

            var present = new HashSet<string>();
            foreach (AssetKeyRegistry.Entry entry in registry.Entries)
            {
                if (entry != null && !string.IsNullOrEmpty(entry.Key))
                {
                    present.Add(entry.Key);
                }
            }

            int added = 0;
            foreach ((string key, bool isArea) in keys)
            {
                if (!present.Add(key))
                {
                    continue;
                }

                registry.Entries.Add(new AssetKeyRegistry.Entry { Key = key, Sprite = null, Color = ColorOf(key, isArea), PrefabOverride = null });
                added++;
            }

            if (created)
            {
                AssetDatabase.CreateAsset(registry, AssetPath);
            }
            else if (added > 0)
            {
                EditorUtility.SetDirty(registry);
            }

            AssetDatabase.SaveAssets();
            Debug.Log("[Editor] AssetKeyRegistryGenerator: " + (created ? "created " : "updated ") + AssetPath + ", added " + added
                + " of " + keys.Count + " keys, " + registry.Entries.Count + " entries in total.");
        }

        // Every asset key the data references, in file order and without duplicates; isArea marks zone and hazard keys.
        private static List<(string key, bool isArea)> CollectKeys(GameData data)
        {
            var keys = new List<(string key, bool isArea)>();
            var seen = new HashSet<string>();

            void Add(string key, bool isArea)
            {
                if (!string.IsNullOrEmpty(key) && seen.Add(key))
                {
                    keys.Add((key, isArea));
                }
            }

            foreach (MonsterDefinition monster in data.Monsters)
            {
                Add(monster.AssetKey, false);
                if (monster.Ranged != null && monster.Ranged.Projectile != null)
                {
                    Add(monster.Ranged.Projectile.AssetKey, false);
                }

                if (monster.BossPatterns != null)
                {
                    foreach (BossPatternDefinition pattern in monster.BossPatterns)
                    {
                        if (pattern.Kind == BossPatternKind.HazardZone)
                        {
                            Add(pattern.AssetKey, true);
                        }
                    }
                }
            }

            foreach (WeaponDefinition weapon in data.Weapons)
            {
                Add(weapon.AssetKey, weapon.Behavior == WeaponBehavior.Zone);
                Add(weapon.IconKey, false);
            }

            if (data.PlayerStats != null && data.PlayerStats.Pickups != null)
            {
                Add(data.PlayerStats.Pickups.GemAssetKey, false);
                Add(data.PlayerStats.Pickups.ChestAssetKey, false);
            }

            return keys;
        }

        // Tint from the last dot-separated token (white when unknown); area keys get AreaAlpha.
        private static Color ColorOf(string key, bool isArea)
        {
            int dot = key.LastIndexOf('.');
            string token = dot >= 0 ? key.Substring(dot + 1) : key;
            Color color = TokenColors.TryGetValue(token, out Color32 known) ? (Color)known : Color.white;
            color.a = isArea ? AreaAlpha : 1f;
            return color;
        }
    }
}
