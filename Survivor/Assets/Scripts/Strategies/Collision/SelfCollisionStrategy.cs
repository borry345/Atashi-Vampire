using System;
using System.Collections.Generic;
using Survivor.Core;
using UnityEngine;

namespace Survivor.Strategies
{
    /// Collision strategy B (F18): polls circle overlaps itself with squared distances (R5) and diffs them against the previous
    /// step (TAC-F18-2). Monsters are read from this step's snapshot through the shared MonsterSpatialIndex, which must have
    /// Required == true (TAC-F18-3, D15); every other layer is read from the registered CollisionBody.
    public sealed class SelfCollisionStrategy : ICollisionStrategy
    {
        private const int LayerCount = (int)CollisionLayer.Chest + 1;

        private static readonly Comparison<CollisionBody> ById = (x, y) => x.Id.CompareTo(y.Id);

        private static readonly CollisionChannel[] MonsterChannelsByALayer =
        {
            CollisionChannel.PlayerProjectileVsMonster, CollisionChannel.PlayerOrbVsMonster, CollisionChannel.PlayerZoneVsMonster,
        };

        private static readonly CollisionLayer[] MonsterALayers =
        {
            CollisionLayer.PlayerProjectile, CollisionLayer.PlayerOrb, CollisionLayer.PlayerZone,
        };

        private readonly MonsterSpatialIndex _index;
        private readonly MonsterSnapshotBuffer _snapshot;
        private readonly List<CollisionBody>[] _layers = new List<CollisionBody>[LayerCount];
        private readonly bool[] _layerUnsorted = new bool[LayerCount];
        private readonly PairSet _pairs = new PairSet();
        private readonly List<CollisionEvent> _events = new List<CollisionEvent>();
        private readonly List<int> _candidates = new List<int>();
        private readonly SpatialHash _pickupHash;
        private readonly List<CollisionBody> _pickups = new List<CollisionBody>();
        private bool[] _snapshotRegistered = new bool[512];
        private float _maxPickupRadius;

        /// Creates the strategy over the shared monster index and the snapshot buffer it was built over.
        /// Throws ArgumentNullException for null arguments, ArgumentException when index reads a different buffer.
        public SelfCollisionStrategy(MonsterSpatialIndex index, MonsterSnapshotBuffer snapshot)
        {
            _index = index ?? throw new ArgumentNullException(nameof(index));
            _snapshot = snapshot ?? throw new ArgumentNullException(nameof(snapshot));
            if (!ReferenceEquals(index.Snapshot, snapshot))
            {
                throw new ArgumentException("SelfCollisionStrategy: snapshot must be the buffer the MonsterSpatialIndex was built over.", nameof(snapshot));
            }

            for (int i = 0; i < LayerCount; i++)
            {
                _layers[i] = new List<CollisionBody>();
            }

            // C3: the pickup-only hash shares the monster index cell size; it is refilled every CollectEvents.
            _pickupHash = new SpatialHash(index.CellSize);
        }

        /// SelfSquaredDistance.
        public CollisionStrategyKind Kind => CollisionStrategyKind.SelfSquaredDistance;

        /// (Added beyond 5.9) Exact circle tests (R5) performed by the last CollectEvents (F18 measurement hypothesis).
        public int CheckedPairCount { get; private set; }

        /// Adds body from the next CollectEvents. Throws ArgumentNullException for null, InvalidOperationException when the
        /// body is already registered with a collision strategy.
        public void Register(CollisionBody body)
        {
            if (body == null)
            {
                throw new ArgumentNullException(nameof(body));
            }

            if (body.StrategySlot != -1)
            {
                throw CollisionLog.Fail($"SelfCollisionStrategy.Register: body {body.Id} is already registered with a collision strategy (slot {body.StrategySlot}).");
            }

            List<CollisionBody> list = LayerList(body);
            body.StrategySlot = list.Count;
            list.Add(body);
            _layerUnsorted[(int)body.Layer] = true;
        }

        /// Removes body and silently forgets its pairs (no Exit, TAC-F18-5). Throws ArgumentNullException for null,
        /// InvalidOperationException when body is not registered here.
        public void Unregister(CollisionBody body)
        {
            if (body == null)
            {
                throw new ArgumentNullException(nameof(body));
            }

            List<CollisionBody> list = RequireRegistered(body, "Unregister");
            int slot = body.StrategySlot;
            int last = list.Count - 1;
            if (slot != last)
            {
                CollisionBody moved = list[last];
                list[slot] = moved;
                moved.StrategySlot = slot;
                _layerUnsorted[(int)body.Layer] = true;
            }

            list.RemoveAt(last);
            body.StrategySlot = -1;
            _pairs.RemoveBody(body.Id);
        }

        /// Nothing to update (Radius is read every step); validates the registration. Throws like Unregister.
        public void NotifyRadiusChanged(CollisionBody body)
        {
            if (body == null)
            {
                throw new ArgumentNullException(nameof(body));
            }

            RequireRegistered(body, "NotifyRadiusChanged");
        }

        /// Enter/Exit events of step sorted by CollisionEvent.CompareTo; the list is reused until the next call.
        public IReadOnlyList<CollisionEvent> CollectEvents(long step)
        {
            using (ProfilerMarkers.Collision.Auto())
            {
                _events.Clear();
                CheckedPairCount = 0;
                SortDirtyLayers();
                MarkRegisteredSnapshotEntries();
                _pairs.Begin();

                for (int i = 0; i < MonsterALayers.Length; i++)
                {
                    CollectAgainstMonsters(_layers[(int)MonsterALayers[i]], MonsterChannelsByALayer[i]);
                }

                List<CollisionBody> playerBodies = _layers[(int)CollisionLayer.PlayerBody];
                CollectAgainstMonsters(playerBodies, CollisionChannel.PlayerBodyVsMonster);
                CollectLinear(_layers[(int)CollisionLayer.EnemyProjectile], playerBodies, CollisionChannel.EnemyProjectileVsPlayerBody);
                CollectLinear(_layers[(int)CollisionLayer.EnemyZone], playerBodies, CollisionChannel.EnemyZoneVsPlayerBody);

                RebuildPickupHash();
                CollectAgainstPickups(_layers[(int)CollisionLayer.PlayerMagnet], CollisionLayer.PlayerMagnet);
                CollectAgainstPickups(playerBodies, CollisionLayer.PlayerBody);

                _pairs.Commit(step, _events);
            }

            return _events;
        }

        /// Forgets every body, pair and pickup entry (run reset); registered bodies get StrategySlot = -1.
        public void Clear()
        {
            for (int i = 0; i < LayerCount; i++)
            {
                List<CollisionBody> list = _layers[i];
                for (int j = 0; j < list.Count; j++)
                {
                    list[j].StrategySlot = -1;
                }

                list.Clear();
                _layerUnsorted[i] = false;
            }

            _pairs.Clear();
            _events.Clear();
            _pickupHash.Clear();
            _pickups.Clear();
            _candidates.Clear();
            CheckedPairCount = 0;
        }

        private List<CollisionBody> LayerList(CollisionBody body)
        {
            int layer = (int)body.Layer;
            if (layer < 0 || layer >= LayerCount)
            {
                throw new ArgumentOutOfRangeException(nameof(body), body.Layer, "Unknown CollisionLayer.");
            }

            return _layers[layer];
        }

        private List<CollisionBody> RequireRegistered(CollisionBody body, string operation)
        {
            List<CollisionBody> list = LayerList(body);
            int slot = body.StrategySlot;
            if (slot < 0 || slot >= list.Count || !ReferenceEquals(list[slot], body))
            {
                throw CollisionLog.Fail($"SelfCollisionStrategy.{operation}: body {body.Id} is not registered.");
            }

            return list;
        }

        // R10: every layer list is iterated in ascending Id; re-sort only when membership changed, then fix the slots.
        private void SortDirtyLayers()
        {
            for (int i = 0; i < LayerCount; i++)
            {
                if (!_layerUnsorted[i])
                {
                    continue;
                }

                List<CollisionBody> list = _layers[i];
                list.Sort(ById);
                for (int j = 0; j < list.Count; j++)
                {
                    list[j].StrategySlot = j;
                }

                _layerUnsorted[i] = false;
            }
        }

        // C4: a monster takes part only when it is both registered and in this step's snapshot.
        private void MarkRegisteredSnapshotEntries()
        {
            int count = _snapshot.Count;
            if (_snapshotRegistered.Length < count)
            {
                _snapshotRegistered = new bool[Math.Max(count, _snapshotRegistered.Length * 2)];
            }

            Array.Clear(_snapshotRegistered, 0, count);
            List<CollisionBody> monsters = _layers[(int)CollisionLayer.Monster];
            for (int i = 0; i < monsters.Count; i++)
            {
                if (_snapshot.TryGetIndex(monsters[i].Id, out int index))
                {
                    _snapshotRegistered[index] = true;
                }
            }
        }

        private void CollectAgainstMonsters(List<CollisionBody> aBodies, CollisionChannel channel)
        {
            if (aBodies.Count == 0 || _snapshot.Count == 0)
            {
                return;
            }

            for (int i = 0; i < aBodies.Count; i++)
            {
                CollisionBody a = aBodies[i];
                // QueryCircle clears the list and already contains every large monster (superset; exact test below).
                _index.QueryCircle(a.Position, a.Radius, _candidates);
                for (int k = 0; k < _candidates.Count; k++)
                {
                    int index = _candidates[k];
                    if (index >= _snapshot.Count || !_snapshotRegistered[index])
                    {
                        continue;
                    }

                    ref readonly MonsterSnapshot monster = ref _snapshot[index];
                    CheckedPairCount++;
                    if (CircleMath.Overlaps(a.Position, a.Radius, monster.Position, monster.Radius))
                    {
                        _pairs.Add(channel, a.Id, monster.Id);
                    }
                }
            }
        }

        // One player body in practice, so a linear scan is the query (TAC-F18-3).
        private void CollectLinear(List<CollisionBody> aBodies, List<CollisionBody> bBodies, CollisionChannel channel)
        {
            for (int i = 0; i < aBodies.Count; i++)
            {
                CollisionBody a = aBodies[i];
                for (int j = 0; j < bBodies.Count; j++)
                {
                    CollisionBody b = bBodies[j];
                    CheckedPairCount++;
                    if (CircleMath.Overlaps(a.Position, a.Radius, b.Position, b.Radius))
                    {
                        _pairs.Add(channel, a.Id, b.Id);
                    }
                }
            }
        }

        // C3: stateless refill (Clear -> Insert) of gems then chests; payload = index into _pickups.
        private void RebuildPickupHash()
        {
            _pickupHash.Clear();
            _pickups.Clear();
            _maxPickupRadius = 0f;
            AddPickups(_layers[(int)CollisionLayer.Gem]);
            AddPickups(_layers[(int)CollisionLayer.Chest]);
        }

        private void AddPickups(List<CollisionBody> bodies)
        {
            for (int i = 0; i < bodies.Count; i++)
            {
                CollisionBody body = bodies[i];
                _pickupHash.Insert(_pickups.Count, body.Position);
                _pickups.Add(body);
                if (body.Radius > _maxPickupRadius)
                {
                    _maxPickupRadius = body.Radius;
                }
            }
        }

        private void CollectAgainstPickups(List<CollisionBody> aBodies, CollisionLayer aLayer)
        {
            if (aBodies.Count == 0 || _pickups.Count == 0)
            {
                return;
            }

            for (int i = 0; i < aBodies.Count; i++)
            {
                CollisionBody a = aBodies[i];
                _pickupHash.QueryCells(a.Position, a.Radius + _maxPickupRadius, _candidates);
                for (int k = 0; k < _candidates.Count; k++)
                {
                    CollisionBody b = _pickups[_candidates[k]];
                    if (!CollisionChannelTable.TryGetChannel(aLayer, b.Layer, out CollisionChannel channel, out bool aIsA) || !aIsA)
                    {
                        continue;
                    }

                    CheckedPairCount++;
                    if (CircleMath.Overlaps(a.Position, a.Radius, b.Position, b.Radius))
                    {
                        _pairs.Add(channel, a.Id, b.Id);
                    }
                }
            }
        }
    }
}
