using System;
using Survivor.Core;
using Survivor.Data;
using UnityEngine;

namespace Survivor.Simulation
{
    /// The player (not a pooled entity, 5.12 rev 16): authoritative Position plus a body circle (PlayerBody, 1:1) and a magnet
    /// circle (PlayerMagnet, 2:1) registered directly to the run's collision strategy and the physics registry (5c.3 #1 exception).
    /// Both bodies share the injected Host; Rigidbody2D/colliders are added by PhysicsBodySynchronizer, never here.
    public sealed class PlayerEntity
    {
        private readonly EntityHandleAllocator _ids;
        private readonly IPhysicsBodyRegistry _physicsBodies;
        private readonly Func<GameData> _currentData;
        private readonly PassiveInventory _passives;
        private ICollisionStrategy _collisions;

        /// Creates the player over the run allocator, physics registry, current-data source (GameDataRegistry.Current), passives
        /// (P3 pickup bonus) and the GameObject both bodies use as Host. Null arguments throw ArgumentNullException.
        public PlayerEntity(EntityHandleAllocator ids, IPhysicsBodyRegistry physicsBodies, Func<GameData> currentData,
                            PassiveInventory passives, GameObject host)
        {
            _ids = ids ?? throw new ArgumentNullException(nameof(ids));
            _physicsBodies = physicsBodies ?? throw new ArgumentNullException(nameof(physicsBodies));
            _currentData = currentData ?? throw new ArgumentNullException(nameof(currentData));
            _passives = passives ?? throw new ArgumentNullException(nameof(passives));
            Host = host != null ? host : throw new ArgumentNullException(nameof(host));
            Body = new CollisionBody { Layer = CollisionLayer.PlayerBody, Host = host };
            MagnetBody = new CollisionBody { Layer = CollisionLayer.PlayerMagnet, Host = host };
        }

        /// Authoritative position; PlayerMove keeps Body/MagnetBody.Position equal to it.
        public Vector2 Position { get; private set; }

        /// CollisionLayer.PlayerBody circle, Id 1:1 after RegisterForRun.
        public CollisionBody Body { get; }

        /// CollisionLayer.PlayerMagnet circle, Id 2:1 after RegisterForRun.
        public CollisionBody MagnetBody { get; }

        /// GameObject shared by both bodies (presentation is the composition root's job).
        public GameObject Host { get; }

        /// True between RegisterForRun and UnregisterFromRun.
        public bool IsRegistered => _collisions != null;

        /// RunSession.Reset step 4, right after EntityHandleAllocator.Reset(): allocates body then magnet ids, sets radii from the
        /// current player stats (magnet with the P3 bonus), places both bodies at position and registers them to collisions and
        /// the physics registry. Throws InvalidOperationException when already registered, ArgumentNullException for null.
        public void RegisterForRun(ICollisionStrategy collisions, Vector2 position)
        {
            if (collisions == null)
            {
                throw new ArgumentNullException(nameof(collisions));
            }

            if (_collisions != null)
            {
                throw new InvalidOperationException("PlayerEntity.RegisterForRun: the player is already registered; call UnregisterFromRun first.");
            }

            PlayerStatsDefinition stats = Stats();
            Body.Id = _ids.Allocate();
            MagnetBody.Id = _ids.Allocate();
            Body.Radius = stats.Radius;
            Body.IsLarge = false;
            MagnetBody.Radius = MagnetRadius(stats);
            MagnetBody.IsLarge = false;
            MoveTo(position);
            _physicsBodies.Register(Body);
            collisions.Register(Body);
            _physicsBodies.Register(MagnetBody);
            collisions.Register(MagnetBody);
            _collisions = collisions;
        }

        /// RunSession.Reset before step 1: unregisters both bodies from the remembered strategy and the physics registry.
        /// Ids are not freed (step 4 resets the allocator). No-op when not registered.
        public void UnregisterFromRun()
        {
            if (_collisions == null)
            {
                return;
            }

            _collisions.Unregister(MagnetBody);
            _physicsBodies.Unregister(MagnetBody);
            _collisions.Unregister(Body);
            _physicsBodies.Unregister(Body);
            _collisions = null;
        }

        /// Recomputes the magnet radius (pickupRadius x (1 + P3 bonus), R13); on change notifies the registered strategy.
        public void RefreshMagnetRadius()
        {
            float radius = MagnetRadius(Stats());
            if (radius == MagnetBody.Radius)
            {
                return;
            }

            MagnetBody.Radius = radius;
            _collisions?.NotifyRadiusChanged(MagnetBody);
        }

        internal void MoveTo(Vector2 position)
        {
            Position = position;
            Body.Position = position;
            MagnetBody.Position = position;
        }

        private float MagnetRadius(PlayerStatsDefinition stats)
        {
            return StatMath.ApplyMultiplier(stats.PickupRadius, _passives.BonusOf(PassiveStat.PickupRadius));
        }

        private PlayerStatsDefinition Stats()
        {
            PlayerStatsDefinition stats = _currentData()?.PlayerStats;
            if (stats == null)
            {
                throw new InvalidOperationException("PlayerEntity: no player stats are loaded.");
            }

            return stats;
        }
    }
}
