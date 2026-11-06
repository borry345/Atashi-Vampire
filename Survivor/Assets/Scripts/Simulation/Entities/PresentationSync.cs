using System;
using System.Collections.Generic;
using UnityEngine;

namespace Survivor.Simulation
{
    /// Writes transform.position = SimEntity.Position once per frame for every live entity, except bodies whose transform
    /// the engine collision mode drives (Body.DrivesTransform, 5c.3 #6).
    [DefaultExecutionOrder(-100)]
    public sealed class PresentationSync : MonoBehaviour
    {
        private EntityLifecycle _lifecycle;

        /// Binds the lifecycle whose live entities are synced; null throws ArgumentNullException.
        public void Initialize(EntityLifecycle lifecycle)
        {
            _lifecycle = lifecycle ?? throw new ArgumentNullException(nameof(lifecycle));
        }

        private void LateUpdate()
        {
            if (_lifecycle == null)
            {
                return;
            }

            IReadOnlyList<SimEntity> active = _lifecycle.ActiveEntities;
            for (int i = 0; i < active.Count; i++)
            {
                SimEntity entity = active[i];
                if (entity.Body.DrivesTransform)
                {
                    continue;
                }

                Transform t = entity.transform;
                Vector2 p = entity.Position;
                t.position = new Vector3(p.x, p.y, t.position.z);
            }
        }
    }
}
