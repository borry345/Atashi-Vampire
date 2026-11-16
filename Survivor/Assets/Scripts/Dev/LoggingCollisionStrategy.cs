using System;
using System.Collections.Generic;
using Survivor.Core;

namespace Survivor.Dev
{
    /// Decorator around the run's real collision strategy (5.15, TAC-F19-2), installed only at a run start through
    /// IDevModeHost.NextRunCollisionDecorator: every call goes to the inner strategy unchanged and the CollectEvents result is
    /// copied into a CollisionEventLog. Kind is the inner strategy's Kind, because GameBootstrap.ApplyRequirements reads
    /// lifecycle.Collisions.Kind for the spatial index requirement and R19.
    public sealed class LoggingCollisionStrategy : ICollisionStrategy
    {
        private readonly ICollisionStrategy _inner;
        private readonly CollisionEventLog _log;

        /// Wraps inner and records into log (both required).
        public LoggingCollisionStrategy(ICollisionStrategy inner, CollisionEventLog log)
        {
            _inner = inner ?? throw new ArgumentNullException(nameof(inner));
            _log = log ?? throw new ArgumentNullException(nameof(log));
        }

        /// The wrapped strategy.
        public ICollisionStrategy Inner => _inner;

        /// The log the events go to.
        public CollisionEventLog Log => _log;

        /// The inner strategy's kind.
        public CollisionStrategyKind Kind => _inner.Kind;

        /// Forwards to the inner strategy.
        public void Register(CollisionBody body)
        {
            _inner.Register(body);
        }

        /// Forwards to the inner strategy.
        public void Unregister(CollisionBody body)
        {
            _inner.Unregister(body);
        }

        /// Forwards to the inner strategy.
        public void NotifyRadiusChanged(CollisionBody body)
        {
            _inner.NotifyRadiusChanged(body);
        }

        /// The inner strategy's events of step, unchanged, after copying them into the log.
        public IReadOnlyList<CollisionEvent> CollectEvents(long step)
        {
            IReadOnlyList<CollisionEvent> events = _inner.CollectEvents(step);
            _log.Append(events);
            return events;
        }

        /// Forwards to the inner strategy (the log keeps what was recorded).
        public void Clear()
        {
            _inner.Clear();
        }
    }
}
