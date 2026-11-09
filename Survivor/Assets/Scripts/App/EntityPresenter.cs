using System;
using System.Collections.Generic;
using Survivor.Core;
using Survivor.Data;
using Survivor.Simulation;
using UnityEngine;

namespace Survivor.App
{
    /// Presentation of pooled entities (5.11 "applied to the SpriteRenderer at spawn time"): on every Spawned it resolves the
    /// entity's asset key and draws the sprite, tint and sorting order on a child "Visual" sized to the body diameter, and keeps
    /// that size in step with the radius each frame. The host keeps scale 1 and its own (empty) SpriteRenderer, because the host
    /// carries the engine collider and is moved by the simulation or PresentationSync; this component never writes the host
    /// transform and nothing the simulation reads. PrefabOverride of the resolved visual is ignored (spawn-side concern).
    [DefaultExecutionOrder(-50)]
    public sealed class EntityPresenter : MonoBehaviour
    {
        /// Name of the child object that carries the entity's SpriteRenderer.
        internal const string VisualName = "Visual";

        private EntityLifecycle _lifecycle;
        private AssetKeyResolver _resolver;
        private Func<GameData> _currentData;

        /// Subscribes to lifecycle.Spawned; resolver turns keys into visuals, currentData supplies weapon and pickup keys. Null
        /// arguments throw ArgumentNullException.
        public void Initialize(EntityLifecycle lifecycle, AssetKeyResolver resolver, Func<GameData> currentData)
        {
            if (lifecycle == null)
            {
                throw new ArgumentNullException(nameof(lifecycle));
            }

            _resolver = resolver ?? throw new ArgumentNullException(nameof(resolver));
            _currentData = currentData ?? throw new ArgumentNullException(nameof(currentData));
            Unsubscribe();
            _lifecycle = lifecycle;
            _lifecycle.Spawned += OnSpawned;
        }

        /// Sorting order of a category: areas under everything, pickups, monsters, then projectiles and orbs (player circle is 10).
        internal static int SortingOrderOf(EntityCategory category)
        {
            switch (category)
            {
                case EntityCategory.PlayerZone:
                case EntityCategory.EnemyZone:
                    return -10;
                case EntityCategory.Gem:
                case EntityCategory.Chest:
                    return 0;
                case EntityCategory.Monster:
                    return 5;
                default:
                    return 8;
            }
        }

        /// Visual diameter (world units, = local scale of the 1-unit default circle) for a body radius.
        internal static float DiameterOf(float radius)
        {
            return radius * 2f;
        }

        /// Asset key of a weapon id in data; "" when the id, the weapon or the data is missing.
        internal static string WeaponKeyOf(string weaponId, GameData data)
        {
            return data != null && data.TryGetWeapon(weaponId, out WeaponDefinition weapon) ? weapon.AssetKey ?? string.Empty : string.Empty;
        }

        /// Asset key the entity is drawn with; "" (which resolves to the fallback) when its definition or data is missing.
        internal static string KeyOf(SimEntity entity, GameData data)
        {
            string key;
            switch (entity)
            {
                case Monster monster:
                    key = monster.Definition?.AssetKey;
                    break;
                case EnemyProjectile shot:
                    key = shot.Definition?.AssetKey;
                    break;
                case EnemyHazardZone hazard:
                    key = hazard.Pattern?.AssetKey;
                    break;
                case PlayerProjectile projectile:
                    key = WeaponKeyOf(projectile.WeaponId, data);
                    break;
                case OrbitOrb orb:
                    key = WeaponKeyOf(orb.WeaponId, data);
                    break;
                case DamageZone zone:
                    key = WeaponKeyOf(zone.WeaponId, data);
                    break;
                case ExperienceGem _:
                    key = data?.PlayerStats?.Pickups?.GemAssetKey;
                    break;
                case Chest _:
                    key = data?.PlayerStats?.Pickups?.ChestAssetKey;
                    break;
                default:
                    key = null;
                    break;
            }

            return key ?? string.Empty;
        }

        /// The child "Visual" of host, or null when it has not been created yet.
        internal static Transform FindVisual(Transform host)
        {
            return host.childCount > 0 ? host.Find(VisualName) : null;
        }

        // Test hook: the tint the resolver gives the entity's key (what its Visual must show).
        internal Color ResolvedColorOf(SimEntity entity)
        {
            return _resolver.Resolve(KeyOf(entity, _currentData())).Color;
        }

        // Test hook: swaps in a resolver without a registry (every key falls back to magenta) and redraws every live entity.
        internal void UseResolverWithoutRegistry()
        {
            _resolver = new AssetKeyResolver(null, null);
            IReadOnlyList<SimEntity> active = _lifecycle.ActiveEntities;
            for (int i = 0; i < active.Count; i++)
            {
                Apply(active[i]);
            }
        }

        private void OnSpawned(SimEntity entity)
        {
            Apply(entity);
        }

        // Called on every spawn, pool reuse included: a reused host may now hold a different definition, so everything is reapplied.
        private void Apply(SimEntity entity)
        {
            VisualAsset visual = _resolver.Resolve(KeyOf(entity, _currentData()));
            Transform host = entity.transform;
            Transform child = FindVisual(host);
            SpriteRenderer spriteRenderer;
            if (child == null)
            {
                var visualObject = new GameObject(VisualName);
                child = visualObject.transform;
                child.SetParent(host, false);
                spriteRenderer = visualObject.AddComponent<SpriteRenderer>();
            }
            else
            {
                spriteRenderer = child.GetComponent<SpriteRenderer>();
                if (spriteRenderer == null)
                {
                    spriteRenderer = child.gameObject.AddComponent<SpriteRenderer>();
                }
            }

            child.localPosition = Vector3.zero;
            child.localRotation = Quaternion.identity;
            spriteRenderer.sprite = visual.Sprite != null ? visual.Sprite : DefaultShapeFactory.Circle;
            spriteRenderer.color = visual.Color;
            spriteRenderer.sortingOrder = SortingOrderOf(entity.Category);
            float diameter = DiameterOf(entity.Body.Radius);
            child.localScale = new Vector3(diameter, diameter, 1f);
        }

        // Orbit and zone radii grow with weapon levels after the spawn, so the size is re-checked every frame (no position writes).
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
                Transform child = FindVisual(entity.transform);
                if (child == null)
                {
                    continue;
                }

                float diameter = DiameterOf(entity.Body.Radius);
                if (child.localScale.x != diameter)
                {
                    child.localScale = new Vector3(diameter, diameter, 1f);
                }
            }
        }

        private void OnDestroy()
        {
            Unsubscribe();
        }

        private void Unsubscribe()
        {
            if (_lifecycle != null)
            {
                _lifecycle.Spawned -= OnSpawned;
                _lifecycle = null;
            }
        }
    }
}
