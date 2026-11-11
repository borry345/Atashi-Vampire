using System;
using System.Collections.Generic;
using Survivor.Core;
using Survivor.Data;
using Survivor.Simulation;
using UnityEngine;

namespace Survivor.App
{
    /// Orbit path outline (5.17.2, SPEC F06 33): one unfilled circle per weapon slot holding an Orbit weapon (W2, E2), centered
    /// on the player host and sized to the slot's current level orbitRadius. Display only: no SimEntity, body or command, never
    /// writes anything the simulation reads, and ignores Rules.WeaponsEnabled so the rings stay while attacks are off.
    public sealed class OrbitRingPresenter : MonoBehaviour
    {
        private const int PointCount = 64;
        private const float LineWidth = 0.05f;
        private const int SortingOrder = 7;
        private static readonly Color RingColor = new Color(1f, 1f, 1f, 0.35f);

        private WeaponInventory _weapons;
        private Material _material;
        private LineRenderer[] _rings;
        private float[] _radii;
        private Vector2[] _unitCircle;
        private Vector3[] _points;

        /// Number of rings currently shown (test hook).
        internal int VisibleRingCount
        {
            get
            {
                int count = 0;
                if (_rings == null)
                {
                    return 0;
                }

                for (int s = 0; s < _rings.Length; s++)
                {
                    if (_rings[s].enabled)
                    {
                        count++;
                    }
                }

                return count;
            }
        }

        /// True when the shared line material was created (test hook; false means no rings are ever drawn).
        internal bool HasLineMaterial => _material != null;

        /// Radius the ring of slot is drawn with, or 0 when that ring is hidden (test hook).
        internal float RingRadiusOf(int slot)
        {
            return _rings != null && _rings[slot].enabled ? _radii[slot] : 0f;
        }

        /// Creates one hidden ring per weapon slot under this host; null weapons throws ArgumentNullException. Without the
        /// Sprites/Default shader it logs one warning and draws no rings.
        public void Initialize(WeaponInventory weapons)
        {
            _weapons = weapons ?? throw new ArgumentNullException(nameof(weapons));
            Shader shader = Shader.Find("Sprites/Default");
            if (shader == null)
            {
                AppLog.Warning("No line shader (Sprites/Default); orbit rings are not drawn.");
                return;
            }

            _material = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
            _unitCircle = new Vector2[PointCount];
            for (int i = 0; i < PointCount; i++)
            {
                float angle = i * (2f * Mathf.PI / PointCount);
                _unitCircle[i] = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
            }

            _points = new Vector3[PointCount];
            _rings = new LineRenderer[GameRules.MaxWeaponSlots];
            _radii = new float[GameRules.MaxWeaponSlots];
            for (int s = 0; s < _rings.Length; s++)
            {
                var ringObject = new GameObject("OrbitRing." + s);
                ringObject.transform.SetParent(transform, false);
                LineRenderer ring = ringObject.AddComponent<LineRenderer>();
                ring.loop = true;
                ring.useWorldSpace = false;
                ring.positionCount = PointCount;
                ring.widthMultiplier = LineWidth;
                ring.sortingOrder = SortingOrder;
                ring.startColor = RingColor;
                ring.endColor = RingColor;
                ring.sharedMaterial = _material;
                ring.enabled = false;
                _rings[s] = ring;
                _radii[s] = -1f;
            }
        }

        // Reads the slots every frame, so level-ups, chest evolutions and dev loadouts show on the next frame.
        private void LateUpdate()
        {
            if (_rings == null)
            {
                return;
            }

            IReadOnlyList<WeaponInstance> slots = _weapons.Slots;
            for (int s = 0; s < _rings.Length; s++)
            {
                WeaponInstance weapon = s < slots.Count ? slots[s] : null;
                LineRenderer ring = _rings[s];
                if (weapon == null || weapon.Behavior != WeaponBehavior.Orbit)
                {
                    if (ring.enabled)
                    {
                        ring.enabled = false;
                    }

                    continue;
                }

                float radius = weapon.LevelDefinition.OrbitRadius;
                if (radius != _radii[s])
                {
                    _radii[s] = radius;
                    for (int i = 0; i < PointCount; i++)
                    {
                        _points[i] = new Vector3(_unitCircle[i].x * radius, _unitCircle[i].y * radius, 0f);
                    }

                    ring.SetPositions(_points);
                }

                if (!ring.enabled)
                {
                    ring.enabled = true;
                }
            }
        }

        private void OnDestroy()
        {
            if (_material != null)
            {
                Destroy(_material);
                _material = null;
            }
        }
    }
}
