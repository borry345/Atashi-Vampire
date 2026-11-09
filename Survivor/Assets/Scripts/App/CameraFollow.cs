using System;
using UnityEngine;

namespace Survivor.App
{
    /// Puts the camera exactly on the simulated player position every frame (TAC-F1-6): no interpolation, delay or damping.
    [DefaultExecutionOrder(0)]
    public sealed class CameraFollow : MonoBehaviour
    {
        private Func<Vector2> _target;

        /// Binds the authoritative position source (e.g. () => player.Position); null throws ArgumentNullException.
        public void Initialize(Func<Vector2> target)
        {
            _target = target ?? throw new ArgumentNullException(nameof(target));
        }

        private void LateUpdate()
        {
            if (_target == null)
            {
                return;
            }

            Vector2 p = _target();
            Transform t = transform;
            t.position = new Vector3(p.x, p.y, t.position.z);
        }
    }
}
