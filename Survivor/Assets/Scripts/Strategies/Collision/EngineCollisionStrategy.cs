using System;
using System.Collections.Generic;
using Survivor.Core;
using UnityEngine;

namespace Survivor.Strategies
{
    /// Collision strategy A (F18): Unity Collider2D triggers. CollectEvents syncs every enabled body (MovePosition), runs
    /// Physics2D.Simulate once and returns the queued trigger callbacks as sorted Enter/Exit events (TAC-F18-4).
    /// The only caller of Physics2D.Simulate. Bodies must be registered with the synchronizer before this strategy.
    public sealed class EngineCollisionStrategy : ICollisionStrategy
    {
        private static readonly Comparison<CollisionEvent> EventComparison = (x, y) => x.CompareTo(y);

        private readonly PhysicsBodySynchronizer _bodies;
        private readonly List<CollisionBody> _registered = new List<CollisionBody>();
        private readonly List<QueuedCallback> _queue = new List<QueuedCallback>();
        private readonly HashSet<PairSet.Key> _activePairs = new HashSet<PairSet.Key>();
        private readonly Dictionary<PairSet.Key, bool> _touched = new Dictionary<PairSet.Key, bool>();
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
            }

            _removedId = body.Id;
            _activePairs.RemoveWhere(_containsRemovedId);
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

        /// Sync -> Physics2D.Simulate(StepSeconds) -> queued callbacks as events sorted by CollisionEvent.CompareTo, all under
        /// Survivor.Collision (R16). The list is reused until the next call.
        public IReadOnlyList<CollisionEvent> CollectEvents(long step)
        {
            using (ProfilerMarkers.Collision.Auto())
            {
                _events.Clear();
                _queue.Clear();
                CallbackCount = 0;

                _bodies.SyncAllForSimulation();
                Physics2D.Simulate(StepMath.StepSeconds);

                // Net transition per pair over this Simulate: one Simulate can report Enter and then Exit for the same pair
                // when a body registered this step overlaps another body's previous-step position (apparently a contact update
                // that runs before the move; measured in wave 3). Only the state change from step start to step end is an
                // event, as in the self strategy. Pairs are independent, so the order across pairs does not matter.
                _touched.Clear();
                for (int i = 0; i < _queue.Count; i++)
                {
                    QueuedCallback callback = _queue[i];
                    if (!_touched.ContainsKey(callback.Pair))
                    {
                        _touched.Add(callback.Pair, _activePairs.Contains(callback.Pair));
                    }

                    if (callback.Kind == CollisionEventKind.Enter)
                    {
                        _activePairs.Add(callback.Pair);
                    }
                    else
                    {
                        _activePairs.Remove(callback.Pair);
                    }
                }

                foreach (KeyValuePair<PairSet.Key, bool> touched in _touched)
                {
                    bool activeNow = _activePairs.Contains(touched.Key);
                    if (activeNow != touched.Value)
                    {
                        CollisionEventKind kind = activeNow ? CollisionEventKind.Enter : CollisionEventKind.Exit;
                        _events.Add(new CollisionEvent(step, kind, touched.Key.Channel, touched.Key.A, touched.Key.B));
                    }
                    else
                    {
                        CancelledTransitionCount++;
                    }
                }

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
                    }
                }

                _registered.Clear();
            }

            _queue.Clear();
            _activePairs.Clear();
            _events.Clear();
            _touched.Clear();
            CallbackCount = 0;
            DroppedCallbackCount = 0;
            CancelledTransitionCount = 0;
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

            _queue.Add(new QueuedCallback(new PairSet.Key(channel, self.Id, otherBody.Id), kind));
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

        private readonly struct QueuedCallback
        {
            internal readonly PairSet.Key Pair;
            internal readonly CollisionEventKind Kind;

            internal QueuedCallback(PairSet.Key pair, CollisionEventKind kind)
            {
                Pair = pair;
                Kind = kind;
            }
        }
    }
}
