using UnityEngine;

namespace Survivor.Strategies.Pool
{
    /// Pool bookkeeping attached to every entity template: the source template and whether the instance is acquired.
    [DisallowMultipleComponent]
    public sealed class PooledObject : MonoBehaviour
    {
        /// Template this instance was cloned from; null on the template itself and before the first acquire.
        public GameObject Template { get; internal set; }

        /// True while the instance is acquired from a pool strategy.
        public bool IsActive { get; internal set; }

        // Strategy that last handed this instance out; used to reject foreign releases.
        internal object Owner { get; set; }

        // Acquire order inside the owning strategy; makes ReleaseAll order deterministic.
        internal long AcquireSerial { get; set; }
    }
}
