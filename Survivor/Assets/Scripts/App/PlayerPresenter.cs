using System;
using Survivor.Data;
using Survivor.Simulation;
using UnityEngine;

namespace Survivor.App
{
    /// Presentation of the player (PlayerEntity leaves it to the composition root): moves the player host to the simulated
    /// position once per frame and shows a circle sized to the body radius on a child, so the host (which carries the engine
    /// collider) keeps scale 1. Writes nothing the simulation reads. Skips the position write while the engine simulation drives
    /// the host transform (Body.DrivesTransform, 5c.3 #6).
    [DefaultExecutionOrder(-100)]
    public sealed class PlayerPresenter : MonoBehaviour
    {
        private const int SortingOrder = 10;
        private static readonly Color PlayerColor = new Color(0.35f, 0.75f, 1f, 1f);

        private PlayerEntity _player;
        private Transform _visual;

        /// Binds player (its Host must be this GameObject) and creates the circle child; null throws ArgumentNullException.
        public void Initialize(PlayerEntity player)
        {
            _player = player ?? throw new ArgumentNullException(nameof(player));
            var visual = new GameObject("Player.Visual");
            _visual = visual.transform;
            _visual.SetParent(transform, false);
            var spriteRenderer = visual.AddComponent<SpriteRenderer>();
            spriteRenderer.sprite = DefaultShapeFactory.Circle;
            spriteRenderer.color = PlayerColor;
            spriteRenderer.sortingOrder = SortingOrder;
        }

        private void LateUpdate()
        {
            if (_player == null)
            {
                return;
            }

            if (!_player.Body.DrivesTransform)
            {
                Transform t = transform;
                Vector2 p = _player.Position;
                t.position = new Vector3(p.x, p.y, t.position.z);
            }

            float diameter = _player.Body.Radius > 0f ? _player.Body.Radius * 2f : 1f;
            _visual.localScale = new Vector3(diameter, diameter, 1f);
        }
    }
}
