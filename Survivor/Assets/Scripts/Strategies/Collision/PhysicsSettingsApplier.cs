using Survivor.Core;
using UnityEngine;

namespace Survivor.Strategies
{
    /// Single owner of every Physics2D global setting (5.9.1). No other code writes Physics2D globals.
    public static class PhysicsSettingsApplier
    {
        /// (Added beyond 5.9) Contact offset Apply() sets; measured as settable and readable back (U30).
        public const float DefaultContactOffset = 0.0001f;

        private const int UnityLayerCount = 32;

        /// simulationMode = Script, callbacksOnDisable = false, queriesHitTriggers = true, autoSyncTransforms = false,
        /// defaultContactOffset = 0.0001, and a layer matrix where only the nine channels collide (R30). Idempotent.
        public static void Apply()
        {
            Physics2D.simulationMode = SimulationMode2D.Script;
            Physics2D.callbacksOnDisable = false;
            Physics2D.queriesHitTriggers = true;
            // Deprecated in 6000.3 (CS0618) but still honoured; false is the documented design value (U28).
#pragma warning disable CS0618
            Physics2D.autoSyncTransforms = false;
#pragma warning restore CS0618
            Physics2D.defaultContactOffset = DefaultContactOffset;
            ApplyLayerMatrix();
        }

        // C9: every pair that involves a collision layer (8..17) is ignored, then only the nine channel pairs are enabled,
        // so our bodies never touch each other outside a channel nor anything on another layer.
        private static void ApplyLayerMatrix()
        {
            int first = CollisionLayerMapping.FirstUnityLayer;
            int last = CollisionLayerMapping.UnityLayerOf(CollisionLayer.Chest);
            for (int ours = first; ours <= last; ours++)
            {
                for (int other = 0; other < UnityLayerCount; other++)
                {
                    Physics2D.IgnoreLayerCollision(ours, other, true);
                }
            }

            for (int i = 0; i < CollisionChannelTable.ChannelCount; i++)
            {
                CollisionChannelTable.GetLayers((CollisionChannel)i, out CollisionLayer a, out CollisionLayer b);
                Physics2D.IgnoreLayerCollision(CollisionLayerMapping.UnityLayerOf(a), CollisionLayerMapping.UnityLayerOf(b), false);
            }
        }
    }
}
