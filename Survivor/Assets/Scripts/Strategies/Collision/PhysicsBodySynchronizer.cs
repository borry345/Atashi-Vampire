using System;
using System.Collections.Generic;
using Survivor.Core;
using UnityEngine;

namespace Survivor.Strategies
{
    /// The only code that writes Rigidbody2D.position / MovePosition and enables/disables simulation colliders (5.9.1).
    /// It writes rb.position only and does not call Physics2D.SyncTransforms (measured U32). BuildIndex order 5 phase handler.
    public sealed class PhysicsBodySynchronizer : IPhysicsBodyRegistry, IStepPhaseHandler
    {
        private readonly List<Entry> _slots = new List<Entry>();
        private readonly List<Entry> _sorted = new List<Entry>();
        private readonly Dictionary<Collider2D, Entry> _byCollider = new Dictionary<Collider2D, Entry>();
        private PhysicsBodyRequirements _requirements;
        private CollisionDetectionMode2D _detectionMode = CollisionDetectionMode2D.Discrete;

        /// Creates an empty synchronizer with the requirements of the first run (R19).
        public PhysicsBodySynchronizer(PhysicsBodyRequirements initial)
        {
            _requirements = initial;
        }

        /// (Added beyond 5.9) Requirements currently applied.
        public PhysicsBodyRequirements Requirements => _requirements;

        /// (Added beyond 5.9) Number of registered bodies.
        public int Count => _slots.Count;

        /// (Added beyond 5.9) Rigidbody2D.collisionDetectionMode of every registered body (5.9); set by EngineCollisionStrategy's constructor.
        public CollisionDetectionMode2D DetectionMode
        {
            get => _detectionMode;
            set
            {
                _detectionMode = value;
                for (int i = 0; i < _sorted.Count; i++)
                {
                    _sorted[i].Rigidbody.collisionDetectionMode = value;
                }
            }
        }

        /// Adds body: attaches the engine components to body.Host when missing (C1), teleports rb and transform to body.Position
        /// and enables the collider when the requirements say so (R19). Throws ArgumentNullException for null,
        /// InvalidOperationException when the body is already registered or has no Host.
        public void Register(CollisionBody body)
        {
            if (body == null)
            {
                throw new ArgumentNullException(nameof(body));
            }

            if (body.PhysicsSlot != -1)
            {
                throw CollisionLog.Fail($"PhysicsBodySynchronizer.Register: body {body.Id} is already registered (slot {body.PhysicsSlot}).");
            }

            if (body.Host == null)
            {
                throw CollisionLog.Fail($"PhysicsBodySynchronizer.Register: body {body.Id} has no Host GameObject.");
            }

            if (body.Host.TryGetComponent(out CircleCollider2D existing) && _byCollider.ContainsKey(existing))
            {
                throw CollisionLog.Fail($"PhysicsBodySynchronizer.Register: host '{body.Host.name}' already carries a registered body.");
            }

            CollisionTemplateDecorator.EnsureComponents(body.Host, body.Layer);
            var entry = new Entry(
                body,
                body.Host.GetComponent<Rigidbody2D>(),
                body.Host.GetComponent<CircleCollider2D>(),
                body.Host.GetComponent<CollisionBodyLink>());

            Rigidbody2D rb = entry.Rigidbody;
            rb.bodyType = RigidbodyType2D.Kinematic;
            rb.useFullKinematicContacts = true;
            rb.collisionDetectionMode = _detectionMode;
            entry.Collider.isTrigger = true;
            entry.Collider.offset = Vector2.zero;
            entry.Collider.radius = body.Radius;
            entry.AppliedRadius = body.Radius;
            entry.Link.Body = body;
            body.DrivesTransform = false;

            // TAC-F18-4: place the body before it can be simulated so a pooled host is never swept from its old position.
            body.Host.transform.position = new Vector3(body.Position.x, body.Position.y, body.Host.transform.position.z);
            rb.position = body.Position;
            SetEnabled(entry, _requirements.CollidersEnabledFor(body.Layer));

            body.PhysicsSlot = _slots.Count;
            _slots.Add(entry);
            _sorted.Insert(SortedInsertIndex(body.Id), entry);
            _byCollider.Add(entry.Collider, entry);
        }

        /// Disables the collider and simulation of body (no Exit: callbacksOnDisable = false) and forgets it.
        /// Throws ArgumentNullException for null, InvalidOperationException when body is not registered here.
        public void Unregister(CollisionBody body)
        {
            if (body == null)
            {
                throw new ArgumentNullException(nameof(body));
            }

            Entry entry = RequireRegistered(body, "Unregister");
            SetEnabled(entry, false);
            entry.Link.Body = null;
            entry.Link.Sink = null;
            _byCollider.Remove(entry.Collider);
            _sorted.RemoveAt(SortedIndexOf(entry));

            int slot = body.PhysicsSlot;
            int last = _slots.Count - 1;
            if (slot != last)
            {
                Entry moved = _slots[last];
                _slots[slot] = moved;
                moved.Body.PhysicsSlot = slot;
            }

            _slots.RemoveAt(last);
            body.PhysicsSlot = -1;
        }

        /// Applies next to every registered body in ascending Id (collider + Rigidbody2D.simulated). Called only by the App
        /// requirements callback at run start or a target provider switch (Begin).
        public void ApplyRequirements(PhysicsBodyRequirements next)
        {
            _requirements = next;
            for (int i = 0; i < _sorted.Count; i++)
            {
                Entry entry = _sorted[i];
                SetEnabled(entry, next.CollidersEnabledFor(entry.Body.Layer));
            }
        }

        /// BuildIndex: SyncMonstersForQuery() when the requirements ask for it, measured under Survivor.Targeting (R16).
        public void OnPhase(SimulationPhase phase, in StepContext context)
        {
            if (phase != SimulationPhase.BuildIndex || !_requirements.SyncMonstersAtBuildIndex)
            {
                return;
            }

            using (ProfilerMarkers.Targeting.Auto())
            {
                SyncMonstersForQuery();
            }
        }

        /// rb.position = body.Position (and pending radius updates) for every enabled monster body, ascending Id (U32).
        public void SyncMonstersForQuery()
        {
            for (int i = 0; i < _sorted.Count; i++)
            {
                Entry entry = _sorted[i];
                if (!entry.Enabled || entry.Body.Layer != CollisionLayer.Monster)
                {
                    continue;
                }

                ApplyRadius(entry);
                entry.Rigidbody.position = entry.Body.Position;
            }
        }

        /// Engine only: pending radius updates, then MovePosition(body.Position) for every enabled body, ascending Id.
        public void SyncAllForSimulation()
        {
            for (int i = 0; i < _sorted.Count; i++)
            {
                Entry entry = _sorted[i];
                if (!entry.Enabled)
                {
                    continue;
                }

                ApplyRadius(entry);
                entry.Rigidbody.MovePosition(entry.Body.Position);
            }
        }

        /// (Added beyond 5.9) Marks body's collider radius for an update at the next sync (C7). Throws like Unregister.
        public void NotifyRadiusChanged(CollisionBody body)
        {
            if (body == null)
            {
                throw new ArgumentNullException(nameof(body));
            }

            RequireRegistered(body, "NotifyRadiusChanged").RadiusDirty = true;
        }

        /// Body registered for collider's host; false for null or an unregistered collider.
        public bool TryGetBody(Collider2D collider, out CollisionBody body)
        {
            if (collider != null && _byCollider.TryGetValue(collider, out Entry entry))
            {
                body = entry.Body;
                return true;
            }

            body = null;
            return false;
        }

        /// True when body is registered with this synchronizer.
        internal bool IsRegistered(CollisionBody body)
        {
            int slot = body.PhysicsSlot;
            return slot >= 0 && slot < _slots.Count && ReferenceEquals(_slots[slot].Body, body);
        }

        /// Link of a registered body (caller checked IsRegistered).
        internal CollisionBodyLink LinkOf(CollisionBody body)
        {
            return _slots[body.PhysicsSlot].Link;
        }

        private Entry RequireRegistered(CollisionBody body, string operation)
        {
            if (!IsRegistered(body))
            {
                throw CollisionLog.Fail($"PhysicsBodySynchronizer.{operation}: body {body.Id} is not registered.");
            }

            return _slots[body.PhysicsSlot];
        }

        private static void SetEnabled(Entry entry, bool enabled)
        {
            bool switchedOn = enabled && !entry.Enabled;
            entry.Rigidbody.simulated = enabled;
            entry.Collider.enabled = enabled;
            entry.Enabled = enabled;
            if (switchedOn)
            {
                // A collider switched on mid-run must not start from a stale engine position.
                ApplyRadius(entry);
                entry.Rigidbody.position = entry.Body.Position;
            }
        }

        private static void ApplyRadius(Entry entry)
        {
            float radius = entry.Body.Radius;
            if (entry.RadiusDirty || entry.AppliedRadius != radius)
            {
                entry.Collider.radius = radius;
                entry.AppliedRadius = radius;
                entry.RadiusDirty = false;
            }
        }

        private int SortedInsertIndex(EntityHandle id)
        {
            int low = 0;
            int high = _sorted.Count;
            while (low < high)
            {
                int mid = low + ((high - low) >> 1);
                if (_sorted[mid].Body.Id.CompareTo(id) <= 0)
                {
                    low = mid + 1;
                }
                else
                {
                    high = mid;
                }
            }

            return low;
        }

        private int SortedIndexOf(Entry entry)
        {
            int start = SortedInsertIndex(entry.Body.Id) - 1;
            for (int i = start; i >= 0 && _sorted[i].Body.Id == entry.Body.Id; i--)
            {
                if (ReferenceEquals(_sorted[i], entry))
                {
                    return i;
                }
            }

            // Id changed while registered (contract violation by the owner); fall back to a linear search.
            return _sorted.IndexOf(entry);
        }

        private sealed class Entry
        {
            internal readonly CollisionBody Body;
            internal readonly Rigidbody2D Rigidbody;
            internal readonly CircleCollider2D Collider;
            internal readonly CollisionBodyLink Link;
            internal bool Enabled;
            internal bool RadiusDirty;
            internal float AppliedRadius;

            internal Entry(CollisionBody body, Rigidbody2D rigidbody, CircleCollider2D collider, CollisionBodyLink link)
            {
                Body = body;
                Rigidbody = rigidbody;
                Collider = collider;
                Link = link;
            }
        }
    }
}
