using System;

namespace Survivor.Core
{
    /// Maps collision layers to Unity layers (rule R30): Unity layer = 8 + (int)CollisionLayer.
    public static class CollisionLayerMapping
    {
        /// Unity layer index of the first collision layer.
        public const int FirstUnityLayer = 8;

        /// Unity layer index (8..17) for a collision layer; throws ArgumentOutOfRangeException for an undefined layer.
        public static int UnityLayerOf(CollisionLayer layer)
        {
            if (layer > CollisionLayer.Chest)
            {
                throw new ArgumentOutOfRangeException(nameof(layer), layer, "Unknown CollisionLayer.");
            }

            return FirstUnityLayer + (int)layer;
        }

        /// Single-bit Unity layer mask (1 << UnityLayerOf(layer)) for queries such as Physics2D.OverlapCircle.
        public static int MaskOf(CollisionLayer layer)
        {
            return 1 << UnityLayerOf(layer);
        }
    }
}
