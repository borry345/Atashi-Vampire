using UnityEngine;

namespace Survivor.Tests.PlayMode.Collision
{
    /// Counts trigger callbacks on its host (measurement helper).
    public sealed class TriggerRecorder : MonoBehaviour
    {
        public int Enters { get; private set; }
        public int Exits { get; private set; }

        private void OnTriggerEnter2D(Collider2D other)
        {
            Enters++;
        }

        private void OnTriggerExit2D(Collider2D other)
        {
            Exits++;
        }
    }
}
