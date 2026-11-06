using System;
using System.Collections.Generic;
using UnityEngine;

namespace Survivor.Data
{
    /// Editor-authored table from string asset keys to sprites/prefabs; the instance lives at Resources/AssetKeyRegistry.asset (created by the user).
    [CreateAssetMenu(menuName = "Survivor/Asset Key Registry")]
    public sealed class AssetKeyRegistry : ScriptableObject
    {
        /// Resources path of the registry instance (without extension).
        public const string ResourcePath = "AssetKeyRegistry";

        /// One key mapping.
        [Serializable]
        public sealed class Entry
        {
            /// Asset key used in JSON (e.g. "shape.monster.green").
            public string Key;

            /// Sprite to show; the default circle is used when empty.
            public Sprite Sprite;

            /// Tint color.
            public Color Color = Color.white;

            /// Optional prefab replacing the generated template.
            public GameObject PrefabOverride;
        }

        /// Key mappings; the first entry wins for a duplicated key.
        public List<Entry> Entries;

        /// Loads Resources/AssetKeyRegistry.asset; null when the user has not created it.
        public static AssetKeyRegistry LoadDefault()
        {
            return Resources.Load<AssetKeyRegistry>(ResourcePath);
        }
    }
}
