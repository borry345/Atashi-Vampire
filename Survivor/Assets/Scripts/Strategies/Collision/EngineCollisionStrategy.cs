using System;
using System.Collections.Generic;
using Survivor.Core;
using UnityEngine;

namespace Survivor.Strategies
{
    /// Collision strategy A (F18): Unity Collider2D triggers. CollectEvents syncs every enabled body (MovePosition), runs
    /// Physics2D.Simulate once and uses the trigger contacts only as candidate pairs; the reported overlap state is the R5
    /// CircleMath.Overlaps result, so events are the R5 net transitions of the candidates, sorted (TAC-F18-4, R5 gate).
    /// The only caller of Physics2D.Simulate. Bodies must be registered with the synchronizer before this strategy.
    public sealed class EngineCollisionStrategy : ICollisionStrategy
    {
        private static readonly Comparison<CollisionEvent> EventComparison = (x, y) => x.CompareTo(y);

        private readonly PhysicsBodySynchronizer _bodies;
        private readonly List<CollisionBody> _registered = new List<CollisionBody>();
        private readonly List<QueuedCallback> _queue = new List<QueuedCallback>();
        // Pairs Box2D currently reports as touching, with both bodies (A = channel A side).
        private readonly Dictionary<PairSet.Key, BodyPair> _activePairs = new Dictionary<PairSet.Key, BodyPair>();
        // Pairs whose Enter was reported and whose Exit was not yet (the self strategy's previous-step set).
        private readonly Dictionary<PairSet.Key, BodyPair> _reportedPairs = new Dictionary<PairSet.Key, BodyPair>();
        // Step scratch: active pairs + pairs queued this step + reported pairs.
        private readonly Dictionary<PairSet.Key, BodyPair> _candidates = new Dictionary<PairSet.Key, BodyPair>();
        private readonly Dictionary<PairSet.Key, bool> _touched = new Dictionary<PairSet.Key, bool>();
        private readonly List<PairSet.Key> _removedKeys = new List<PairSet.Key>();
        private readonly List<CollisionEvent> _events = new List<CollisionEvent>();
        private readonly Predicate<PairSet.Key> _containsRemovedId;
        private EntityHandle _removedId;

        /// Creates the strategy over bodies and sets their Rigidbody2D.collisionDetectionMode to mode (5.9).
        /// Throws ArgumentNullException when bodies is null.
        public EngineCollisionStrategy(PhysicsBodySynchronizer bodies, CollisionDetectionMode2D mode)
        {
            _bodies = bodies ?? throw new ArgumentNullException(nameof(bodies));
            _bodies.DetectionMode = mode;
            DetectionMode = mode;
            _containsRemovedId = key => key.A == _removedId || key.B == _removedId;
        }

        /// EngineCollider2D.
        public CollisionStrategyKind Kind => CollisionStrategyKind.EngineCollider2D;

        /// (Added beyond 5.9) Collision detection mode this strategy was created with (E1 Discrete, E2 Discrete and Continuous).
        public CollisionDetectionMode2D DetectionMode { get; }

        /// (Added beyond 5.9) Trigger callbacks received during the last CollectEvents, before any filtering (F18 measurement hypothesis).
        public int CallbackCount { get; private set; }

        /// (Added beyond 5.9) Callbacks dropped since construction/Clear because the layer pair has no channel; stays 0 when the layer matrix
        /// filters as intended (R30, U12).
        public int DroppedCallbackCount { get; private set; }

        /// (Added beyond 5.9) Pairs since construction/Clear that received callbacks in one Simulate without a net state change (typically
        /// Enter then Exit when a body registered this step meets another body's previous position), so no event was reported.
        public int CancelledTransitionCount { get; private set; }

        /// (Added beyond 5.9) (step, pair) occurrences since construction/Clear where Box2D reports the pair as touching but R5 is
        /// false (Box2D contact tolerance), so the pair is not reported as overlapping in that step.
        public int BoundaryDeferredCount { get; private set; }

        /// Adds body (already registered with the synchronizer) from the next CollectEvents. Throws ArgumentNullException for
        /// null, InvalidOperationException when body is already registered with a strategy or not with the synchronizer.
        public void Register(CollisionBody body)
        {
            if (body == null)
            {
                throw new ArgumentNullException(nameof(body));
            }

            if (body.StrategySlot != -1)
            {
                throw CollisionLog.Fail($"EngineCollisionStrategy.Register: body {body.Id} is already registered with a collision strategy (slot {body.StrategySlot}).");
            }

            if (!_bodies.IsRegistered(body))
            {
                throw CollisionLog.Fail($"EngineCollisionStrategy.Register: body {body.Id} is not registered with the PhysicsBodySynchronizer.");
            }

            body.StrategySlot = _registered.Count;
            _registered.Add(body);
            _bodies.LinkOf(body).Sink = this;
            _bodies.RefreshDrivesTransform(body);
        }

        /// Removes body and silently forgets its pairs (no Exit, TAC-F18-5). Throws ArgumentNullException for null,
        /// InvalidOperationException when body is not registered here.
        public void Unregister(CollisionBody body)
        {
            if (body == null)
            {
                throw new ArgumentNullException(nameof(body));
            }

            RequireRegistered(body, "Unregister");
            int slot = body.StrategySlot;
            int last = _registered.Count - 1;
            if (slot != last)
            {
                CollisionBody moved = _registered[last];
                _registered[slot] = moved;
                moved.StrategySlot = slot;
            }

            _registered.RemoveAt(last);
            body.StrategySlot = -1;
            if (_bodies.IsRegistered(body))
            {
                _bodies.LinkOf(body).Sink = null;
                _bodies.RefreshDrivesTransform(body);
            }

            _removedId = body.Id;
            RemovePairsOfRemovedId(_activePairs);
            RemovePairsOfRemovedId(_reportedPairs);
            _removedId = EntityHandle.None;
        }

        /// Forwards to the synchronizer, which updates the collider radius at the next sync (C7). Throws like Unregister.
        public void NotifyRadiusChanged(CollisionBody body)
        {
            if (body == null)
            {
                throw new ArgumentNullException(nameof(body));
            }

            RequireRegistered(body, "NotifyRadiusChanged");
            _bodies.NotifyRadiusChanged(body);
        }

        /// Sync -> Physics2D.Simulate(StepSeconds) -> fold queued callbacks into the Box2D contact set -> R5 net transitions of
        /// the candidate pairs as events sorted by CollisionEvent.CompareTo, all under Survivor.Collision (R16). The list is
        /// reused until the next call.
        public IReadOnlyList<CollisionEvent> CollectEvents(long step)
        {
            using (ProfilerMarkers.Collision.Auto())
            {
                _events.Clear();
                _queue.Clear();
                CallbackCount = 0;

                _bodies.SyncAllForSimulation();
                Physics2D.Simulate(StepMath.StepSeconds);

                // Fold the callbacks into the Box2D contact set. One Simulate can report Enter and then Exit for the same pair
                // when a body registered this step overlaps another body's previous-step position (measured in wave 3); such a
                // pair only counts as a cancelled transition at the Box2D level.
                _touched.Clear();
                for (int i = 0; i < _queue.Count; i++)
                {
                    QueuedCallback callback = _queue[i];
                    if (!_touched.ContainsKey(callback.Pair))
                    {
                        _touched.Add(callback.Pair, _activePairs.ContainsKey(callback.Pair));
                    }

                    if (callback.Kind == CollisionEventKind.Enter)
                    {
                        _activePairs[callback.Pair] = callback.Bodies;
                    }
                    else
                    {
                        _activePairs.Remove(callback.Pair);
                    }
                }

                foreach (KeyValuePair<PairSet.Key, bool> touched in _touched)
                {
                    if (_activePairs.ContainsKey(touched.Key) == touched.Value)
                    {
                        CancelledTransitionCount++;
                    }
                }

                // R5 gate: Box2D only proposes candidates; the reported state is CircleMath.Overlaps with the same function and
                // argument order as the self strategy (A side first). Reported pairs stay candidates so an Exit is reported in
                // the step R5 turns false even without a Box2D Exit; queued pairs are judged at their end-of-step positions.
                _candidates.Clear();
                foreach (KeyValuePair<PairSet.Key, BodyPair> active in _activePairs)
                {
                    _candidates[active.Key] = active.Value;
                }

                for (int i = 0; i < _queue.Count; i++)
                {
                    _candidates[_queue[i].Pair] = _queue[i].Bodies;
                }

                foreach (KeyValuePair<PairSet.Key, BodyPair> reported in _reportedPairs)
                {
                    _candidates[reported.Key] = reported.Value;
                }

                // Pairs are independent and the events are sorted below, so the iteration order does not matter.
                foreach (KeyValuePair<PairSet.Key, BodyPair> candidate in _candidates)
                {
                    PairSet.Key key = candidate.Key;
                    BodyPair pair = candidate.Value;
                    bool exact = CircleMath.Overlaps(pair.A.Position, pair.A.Radius, pair.B.Position, pair.B.Radius);
                    bool reported = _reportedPairs.ContainsKey(key);
                    if (exact && !reported)
                    {
                        _events.Add(new CollisionEvent(step, CollisionEventKind.Enter, key.Channel, key.A, key.B));
                        _reportedPairs.Add(key, pair);
                    }
                    else if (!exact && reported)
                    {
                        _events.Add(new CollisionEvent(step, CollisionEventKind.Exit, key.Channel, key.A, key.B));
                        _reportedPairs.Remove(key);
                    }

                    if (!exact && _activePairs.ContainsKey(key))
                    {
                        BoundaryDeferredCount++;
                    }
                }

                _candidates.Clear();
                _queue.Clear();
                _touched.Clear();
                _events.Sort(EventComparison);
            }

            return _events;
        }

        /// Forgets queued callbacks and pair state (run reset). Bodies still registered at this point are a reset-path bug
        /// upstream: they are logged as a warning and dropped instead of throwing (C8).
        public void Clear()
        {
            if (_registered.Count > 0)
            {
                CollisionLog.Warn($"EngineCollisionStrategy.Clear: {_registered.Count} body(ies) still registered; dropping them.");
                for (int i = 0; i < _registered.Count; i++)
                {
                    CollisionBody body = _registered[i];
                    body.StrategySlot = -1;
                    if (_bodies.IsRegistered(body))
                    {
                        _bodies.LinkOf(body).Sink = null;
                        _bodies.RefreshDrivesTransform(body);
                    }
                }

                _registered.Clear();
            }

            _queue.Clear();
            _activePairs.Clear();
            _reportedPairs.Clear();
            _candidates.Clear();
            _events.Clear();
            _touched.Clear();
            CallbackCount = 0;
            DroppedCallbackCount = 0;
            CancelledTransitionCount = 0;
            BoundaryDeferredCount = 0;
        }

        /// Called by CollisionBodyLink from OnTriggerEnter2D/Exit2D of self's host.
        internal void OnTrigger(CollisionBody self, Collider2D other, CollisionEventKind kind)
        {
            CallbackCount++;
            if (!IsRegisteredHere(self) || !_bodies.TryGetBody(other, out CollisionBody otherBody) || !IsRegisteredHere(otherBody))
            {
                return;   // TAC-F18-5: callbacks involving an unregistered body are dropped.
            }

            if (!CollisionChannelTable.TryGetChannel(self.Layer, otherBody.Layer, out CollisionChannel channel, out bool selfIsA))
            {
                DroppedCallbackCount++;
                return;
            }

            // Both hosts receive the callback; only the A-side one is queued (C6).
            if (!selfIsA)
            {
                return;
            }

            _queue.Add(new QueuedCallback(new PairSet.Key(channel, self.Id, otherBody.Id), new BodyPair(self, otherBody), kind));
        }

        private bool IsRegisteredHere(CollisionBody body)
        {
            int slot = body.StrategySlot;
            return slot >= 0 && slot < _registered.Count && ReferenceEquals(_registered[slot], body);
        }

        private void RequireRegistered(CollisionBody body, string operation)
        {
            if (!IsRegisteredHere(body))
            {
                throw CollisionLog.Fail($"EngineCollisionStrategy.{operation}: body {body.Id} is not registered.");
            }
        }

        private void RemovePairsOfRemovedId(Dictionary<PairSet.Key, BodyPair> pairs)
        {
            _removedKeys.Clear();
            foreach (PairSet.Key key in pairs.Keys)
            {
                if (_containsRemovedId(key))
                {
                    _removedKeys.Add(key);
                }
            }

            for (int i = 0; i < _removedKeys.Count; i++)
            {
                pairs.Remove(_removedKeys[i]);
            }

            _removedKeys.Clear();
        }

        private readonly struct QueuedCallback
        {
            internal readonly PairSet.Key Pair;
            internal readonly BodyPair Bodies;
            internal readonly CollisionEventKind Kind;

            internal QueuedCallback(PairSet.Key pair, BodyPair bodies, CollisionEventKind kind)
            {
                Pair = pair;
                Bodies = bodies;
                Kind = kind;
            }
        }

        // Both bodies of a pair, A = channel A side. The references come from the trigger callback (CollisionBodyLink.Body and
        // the synchronizer's collider lookup) and stay registered here because Unregister drops every pair of the removed id.
        private readonly struct BodyPair
        {
            internal readonly CollisionBody A;
            internal readonly CollisionBody B;

            internal BodyPair(CollisionBody a, CollisionBody b)
            {
                A = a;
                B = b;
            }
        }
    }
}
