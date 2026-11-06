using UnityEngine;

namespace Survivor.Data
{
    /// Resolved visual of an asset key: sprite (never null), tint and optional prefab override.
    public readonly struct VisualAsset
    {
        /// Sprite to render.
        public readonly Sprite Sprite;

        /// Tint color.
        public readonly Color Color;

        /// Prefab replacing the generated template, or null.
        public readonly GameObject PrefabOverride;

        /// Creates a visual.
        public VisualAsset(Sprite sprite, Color color, GameObject prefabOverride)
        {
            Sprite = sprite;
            Color = color;
            PrefabOverride = prefabOverride;
        }
    }
}
