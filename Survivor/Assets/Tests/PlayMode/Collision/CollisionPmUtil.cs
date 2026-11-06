using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using Survivor.Core;
using Survivor.Strategies;
using UnityEngine;
using UnityEngine.TestTools;

// Physics2D.autoSyncTransforms is deprecated in 6000.3 but is part of the saved/restored globals.
#pragma warning disable CS0618

namespace Survivor.Tests.PlayMode.Collision
{
    /// Saves every Physics2D global the collision tests touch and restores it afterwards, so Loop/Pool PlayMode tests keep
    /// their FixedUpdate simulation (C11).
    internal sealed class Physics2DGlobalsScope
    {
        private const int FirstOurs = CollisionLayerMapping.FirstUnityLayer;
        private const int LastOurs = CollisionLayerMapping.FirstUnityLayer + 9;

        private readonly SimulationMode2D _simulationMode;
        private readonly bool _callbacksOnDisable;
        private readonly bool _queriesHitTriggers;
        private readonly bool _autoSyncTransforms;
        private readonly float _defaultContactOffset;
        private readonly bool[,] _ignore = new bool[LastOurs - FirstOurs + 1, 32];

        internal Physics2DGlobalsScope()
        {
            _simulationMode = Physics2D.simulationMode;
            _callbacksOnDisable = Physics2D.callbacksOnDisable;
            _queriesHitTriggers = Physics2D.queriesHitTriggers;
            _autoSyncTransforms = Physics2D.autoSyncTransforms;
            _defaultContactOffset = Physics2D.defaultContactOffset;
            for (int ours = FirstOurs; ours <= LastOurs; ours++)
            {
                for (int other = 0; other < 32; other++)
                {
                    _ignore[ours - FirstOurs, other] = Physics2D.GetIgnoreLayerCollision(ours, other);
                }
            }
        }

        internal void Restore()
        {
            for (int ours = FirstOurs; ours <= LastOurs; ours++)
            {
                for (int other = 0; other < 32; other++)
                {
                    Physics2D.IgnoreLayerCollision(ours, other, _ignore[ours - FirstOurs, other]);
                }
            }

            Physics2D.defaultContactOffset = _defaultContactOffset;
            Physics2D.autoSyncTransforms = _autoSyncTransforms;
            Physics2D.queriesHitTriggers = _queriesHitTriggers;
            Physics2D.callbacksOnDisable = _callbacksOnDisable;
            Physics2D.simulationMode = _simulationMode;
        }
    }

    /// Host objects, body construction, formatting and log expectations for the collision PlayMode tests.
    internal static class CollisionPmUtil
    {
        internal const string NamePrefix = "CollTest";

        internal static CollisionBody Body(int index, CollisionLayer layer, float x, float y, float radius, bool withHost = true, int generation = 1)
        {
            var body = new CollisionBody
            {
                Id = new EntityHandle(index, generation),
                Layer = layer,
                Position = new Vector2(x, y),
                Radius = radius,
            };
            if (withHost)
            {
                body.Host = new GameObject(NamePrefix + "_" + layer + "_" + index);
            }

            return body;
        }

        internal static void DestroyAll()
        {
            foreach (GameObject gameObject in Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (gameObject != null && gameObject.name.StartsWith(NamePrefix))
                {
                    Object.DestroyImmediate(gameObject);
                }
            }
        }

        internal static PhysicsBodyRequirements EngineRequirements()
        {
            return ColliderActivationPolicy.Resolve(new StrategySelection(
                PoolStrategyKind.CustomPool, TargetProviderKind.SpatialHash, TickStrategyKind.CentralTick, CollisionStrategyKind.EngineCollider2D));
        }

        internal static PhysicsBodyRequirements SelfRequirements()
        {
            return ColliderActivationPolicy.Resolve(new StrategySelection(
                PoolStrategyKind.CustomPool, TargetProviderKind.SpatialHash, TickStrategyKind.CentralTick, CollisionStrategyKind.SelfSquaredDistance));
        }

        internal static PhysicsBodyRequirements PhysicsTargetRequirements()
        {
            return ColliderActivationPolicy.Resolve(new StrategySelection(
                PoolStrategyKind.CustomPool, TargetProviderKind.Physics2DQuery, TickStrategyKind.CentralTick, CollisionStrategyKind.SelfSquaredDistance));
        }

        internal static string Format(IReadOnlyList<CollisionEvent> events)
        {
            if (events.Count == 0)
            {
                return "(none)";
            }

            var builder = new StringBuilder();
            for (int i = 0; i < events.Count; i++)
            {
                builder.Append(events[i]).Append('\n');
            }

            return builder.ToString();
        }

        internal static void ExpectCollisionLog(LogType type, string level, string messagePattern)
        {
            LogAssert.Expect(type, new Regex(@"^\[\d{2}:\d{2}:\d{2}\.\d{3}\] \[" + level + @"\] \[Collision\] " + messagePattern));
        }
    }

    /// One collision world over real GameObjects: synchronizer + either strategy, stepped like BuildIndex -> Collision.
    internal sealed class CollisionPmWorld
    {
        private readonly List<CollisionBody> _bodies = new List<CollisionBody>();
        private readonly MonsterSnapshotBuffer _snapshot;
        private readonly MonsterSpatialIndex _index;

        private CollisionPmWorld(PhysicsBodyRequirements requirements)
        {
            Bodies = new PhysicsBodySynchronizer(requirements);
            _snapshot = new MonsterSnapshotBuffer();
            _index = new MonsterSpatialIndex(2f, _snapshot) { Required = true };
        }

        internal PhysicsBodySynchronizer Bodies { get; }

        internal ICollisionStrategy Strategy { get; private set; }

        internal IReadOnlyList<CollisionBody> Registered => _bodies;

        internal static CollisionPmWorld Engine(CollisionDetectionMode2D mode)
        {
            var world = new CollisionPmWorld(CollisionPmUtil.EngineRequirements());
            world.Strategy = new EngineCollisionStrategy(world.Bodies, mode);
            return world;
        }

        internal static CollisionPmWorld Self()
        {
            var world = new CollisionPmWorld(CollisionPmUtil.SelfRequirements());
            world.Strategy = new SelfCollisionStrategy(world._index, world._snapshot);
            return world;
        }

        /// EntityLifecycle.Spawn order: physicsBodies.Register then collisions.Register.
        internal CollisionBody Add(CollisionBody body)
        {
            Bodies.Register(body);
            Strategy.Register(body);
            _bodies.Add(body);
            return body;
        }

        /// EntityLifecycle.Despawn order: collisions.Unregister then physicsBodies.Unregister.
        internal void Remove(CollisionBody body)
        {
            Strategy.Unregister(body);
            Bodies.Unregister(body);
            _bodies.Remove(body);
        }

        /// BuildIndex (snapshot of registered monsters, synchronizer order 5, index order 10) then Collision; returns a copy.
        internal List<CollisionEvent> Step(long step)
        {
            _snapshot.Clear();
            var monsters = new List<CollisionBody>();
            foreach (CollisionBody body in _bodies)
            {
                if (body.Layer == CollisionLayer.Monster)
                {
                    monsters.Add(body);
                }
            }

            monsters.Sort((a, b) => a.Id.CompareTo(b.Id));
            foreach (CollisionBody monster in monsters)
            {
                _snapshot.Add(new MonsterSnapshot { Id = monster.Id, Position = monster.Position, Radius = monster.Radius, IsLarge = monster.IsLarge });
            }

            Bodies.OnPhase(SimulationPhase.BuildIndex, default);
            _index.OnPhase(SimulationPhase.BuildIndex, default);
            return new List<CollisionEvent>(Strategy.CollectEvents(step));
        }
    }
}
