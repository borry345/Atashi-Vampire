using Survivor.Core;
using UnityEngine;

namespace Survivor.Strategies
{
    /// Bridges a host's trigger callbacks to the engine collision strategy (5.9). PhysicsBodySynchronizer sets Body on Register;
    /// EngineCollisionStrategy sets itself as the sink on Register. Callbacks without a body or sink are ignored.
    public sealed class CollisionBodyLink : MonoBehaviour
    {
        /// Body registered with the synchronizer for this host; null while unregistered.
        internal CollisionBody Body { get; set; }

        /// Engine strategy that receives this host's callbacks; null unless the body is registered with one.
        internal EngineCollisionStrategy Sink { get; set; }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (Sink != null && Body != null)
            {
                Sink.OnTrigger(Body, other, CollisionEventKind.Enter);
            }
        }

        private void OnTriggerExit2D(Collider2D other)
        {
            if (Sink != null && Body != null)
            {
                Sink.OnTrigger(Body, other, CollisionEventKind.Exit);
            }
        }
    }
}
