using System;
using System.Collections.Generic;
using Survivor.Core;
using UnityEngine;

namespace Survivor.Strategies.Tick
{
    /// F17 A: each monster/projectile ticks from its own self ticker's FixedUpdate; RunGroup is a no-op (5.8, TAC-F17-2).
    /// Register binds the host's ticker (added by SelfTickTemplateDecorator) and enables it; Unregister disables it.
    public sealed class SelfTickStrategy : ITickStrategy
    {
        private readonly IStepClockReader _clock;
        private readonly Dictionary<IUpdatable, Behaviour> _tickers = new Dictionary<IUpdatable, Behaviour>();
        private readonly int[] _counts = new int[(int)TickGroup.Projectiles + 1];

        /// Creates the strategy; clock (the SimulationClock) is handed to every ticker. Null throws ArgumentNullException.
        public SelfTickStrategy(IStepClockReader clock)
        {
            _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        }

        /// Always PerObjectFixedUpdate.
        public TickStrategyKind Kind => TickStrategyKind.PerObjectFixedUpdate;

        /// Binds updatable to host's MonsterSelfTicker (Monsters) or ProjectileSelfTicker (Projectiles) and enables it.
        /// Throws ArgumentNullException for null arguments, InvalidOperationException when already registered or the host has no ticker.
        public void Register(IUpdatable updatable, GameObject host)
        {
            if (updatable == null)
            {
                throw new ArgumentNullException(nameof(updatable));
            }

            if (host == null)
            {
                throw new ArgumentNullException(nameof(host));
            }

            if (_tickers.ContainsKey(updatable))
            {
                throw new InvalidOperationException("SelfTickStrategy: updatable " + updatable.Id + " is already registered.");
            }

            Behaviour ticker;
            TickGroup group = updatable.Group;
            if (group == TickGroup.Monsters)
            {
                MonsterSelfTicker monsterTicker = host.GetComponent<MonsterSelfTicker>();
                if (monsterTicker != null)
                {
                    monsterTicker.Bind(updatable, _clock);
                }

                ticker = monsterTicker;
            }
            else if (group == TickGroup.Projectiles)
            {
                ProjectileSelfTicker projectileTicker = host.GetComponent<ProjectileSelfTicker>();
                if (projectileTicker != null)
                {
                    projectileTicker.Bind(updatable, _clock);
                }

                ticker = projectileTicker;
            }
            else
            {
                throw new ArgumentOutOfRangeException(nameof(updatable), group, "Unknown tick group.");
            }

            if (ticker == null)
            {
                throw new InvalidOperationException("SelfTickStrategy: host '" + host.name + "' has no self ticker for group " + group
                    + " (SelfTickTemplateDecorator not applied to its template).");
            }

            ticker.enabled = true;
            _tickers.Add(updatable, ticker);
            _counts[(int)group]++;
        }

        /// Disables and unbinds updatable's ticker; safe at any time. Throws InvalidOperationException when it is not registered.
        public void Unregister(IUpdatable updatable)
        {
            if (updatable == null)
            {
                throw new ArgumentNullException(nameof(updatable));
            }

            if (!_tickers.TryGetValue(updatable, out Behaviour ticker))
            {
                throw new InvalidOperationException("SelfTickStrategy: updatable " + updatable.Id + " is not registered.");
            }

            _tickers.Remove(updatable);
            _counts[(int)updatable.Group]--;
            Release(ticker);
        }

        /// No-op: self tickers run from their own FixedUpdate.
        public void RunGroup(TickGroup group, in StepContext context)
        {
        }

        /// Number of registered updatables in group.
        public int RegisteredCount(TickGroup group)
        {
            int g = (int)group;
            if (g < 0 || g >= _counts.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(group), group, "Unknown tick group.");
            }

            return _counts[g];
        }

        /// Disables every registered ticker and forgets all registrations (run reset).
        public void Clear()
        {
            foreach (Behaviour ticker in _tickers.Values)
            {
                Release(ticker);
            }

            _tickers.Clear();
            Array.Clear(_counts, 0, _counts.Length);
        }

        private static void Release(Behaviour ticker)
        {
            // A destroyed host (InstantiateDestroy pool) leaves a fake-null ticker; nothing to disable then.
            if (ticker == null)
            {
                return;
            }

            ticker.enabled = false;
            if (ticker is MonsterSelfTicker monsterTicker)
            {
                monsterTicker.Unbind();
            }
            else if (ticker is ProjectileSelfTicker projectileTicker)
            {
                projectileTicker.Unbind();
            }
        }
    }
}
