using System;
using System.Collections.Generic;
using Survivor.Core;

namespace Survivor.Simulation
{
    /// Collision order 0 collects the strategy's events, Resolve order 0 dispatches them to the channel handlers in sorted order (5.9).
    /// Events of a channel without a handler are dropped (a channel may legitimately have no consumer yet).
    public sealed class CollisionEventRouter : IStepPhaseHandler
    {
        private const int ChannelCount = (int)CollisionChannel.PlayerBodyVsChest + 1;

        private readonly ICollisionEventHandler[] _handlers = new ICollisionEventHandler[ChannelCount];
        private readonly List<CollisionEvent> _events = new List<CollisionEvent>();
        private ICollisionStrategy _strategy;

        /// Creates the router over strategy; null throws ArgumentNullException.
        public CollisionEventRouter(ICollisionStrategy strategy)
        {
            _strategy = strategy ?? throw new ArgumentNullException(nameof(strategy));
        }

        /// Collision strategy events are collected from.
        public ICollisionStrategy Strategy => _strategy;

        /// Events collected in this step's Collision phase and not yet dispatched.
        public int PendingEventCount => _events.Count;

        /// Registers the single handler of channel; throws InvalidOperationException when the channel already has one,
        /// ArgumentNullException for null.
        public void SetHandler(CollisionChannel channel, ICollisionEventHandler handler)
        {
            if (handler == null)
            {
                throw new ArgumentNullException(nameof(handler));
            }

            int c = ChannelIndex(channel);
            if (_handlers[c] != null)
            {
                throw new InvalidOperationException("CollisionEventRouter: channel " + channel + " already has a handler.");
            }

            _handlers[c] = handler;
        }

        /// Run boundary only: switches the strategy for the next run and drops undispatched events; null throws ArgumentNullException.
        public void ReplaceStrategy(ICollisionStrategy strategy)
        {
            _strategy = strategy ?? throw new ArgumentNullException(nameof(strategy));
            _events.Clear();
        }

        /// Collision: copies CollectEvents(step) (sorted by CompareTo). Resolve: dispatches them in that order, then forgets them.
        public void OnPhase(SimulationPhase phase, in StepContext context)
        {
            if (phase == SimulationPhase.Collision)
            {
                _events.Clear();
                IReadOnlyList<CollisionEvent> collected = _strategy.CollectEvents(context.Step);
                for (int i = 0; i < collected.Count; i++)
                {
                    _events.Add(collected[i]);
                }

                // Strategies already sort; the copy is re-sorted so dispatch order never depends on a strategy bug (R10).
                _events.Sort();
            }
            else if (phase == SimulationPhase.Resolve)
            {
                try
                {
                    for (int i = 0; i < _events.Count; i++)
                    {
                        CollisionEvent e = _events[i];
                        ICollisionEventHandler handler = _handlers[(int)e.Channel];
                        if (handler != null)
                        {
                            handler.Handle(e, context);
                        }
                    }
                }
                finally
                {
                    _events.Clear();
                }
            }
        }

        private static int ChannelIndex(CollisionChannel channel)
        {
            int c = (int)channel;
            if (c < 0 || c >= ChannelCount)
            {
                throw new ArgumentOutOfRangeException(nameof(channel), channel, "Unknown collision channel.");
            }

            return c;
        }
    }
}
