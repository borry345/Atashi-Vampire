using System;
using System.Collections.Generic;
using Survivor.Core;
using Survivor.Simulation;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Survivor.Tests.EditMode.Loop
{
    // Records registrations and returns a scripted (possibly unsorted) event list from CollectEvents.
    internal sealed class LoopEmCollisions : ICollisionStrategy
    {
        private readonly List<string> _log;
        internal readonly List<CollisionBody> Bodies = new List<CollisionBody>();
        internal readonly List<CollisionEvent> NextEvents = new List<CollisionEvent>();
        internal readonly List<long> CollectedSteps = new List<long>();

        internal LoopEmCollisions(List<string> log, CollisionStrategyKind kind = CollisionStrategyKind.SelfSquaredDistance)
        {
            _log = log;
            Kind = kind;
        }

        public CollisionStrategyKind Kind { get; }

        public void Register(CollisionBody body)
        {
            _log?.Add("collisions.Register");
            Bodies.Add(body);
        }

        public void Unregister(CollisionBody body)
        {
            _log?.Add("collisions.Unregister");
            Bodies.Remove(body);
        }

        public void NotifyRadiusChanged(CollisionBody body)
        {
        }

        public IReadOnlyList<CollisionEvent> CollectEvents(long step)
        {
            CollectedSteps.Add(step);
            return NextEvents;
        }

        public void Clear()
        {
            Bodies.Clear();
        }
    }

    // Records snapshot deliveries and queries; answers with a fixed hit.
    internal sealed class LoopEmTargetProvider : ITargetProvider
    {
        private readonly List<string> _log;
        private readonly string _name;
        internal MonsterSnapshotBuffer LastSnapshot;
        internal int SnapshotCount;
        internal int QueryCount;

        internal LoopEmTargetProvider(List<string> log, string name, TargetProviderKind kind = TargetProviderKind.BruteForce)
        {
            _log = log;
            _name = name;
            Kind = kind;
        }

        public TargetProviderKind Kind { get; }

        public void OnSnapshotBuilt(MonsterSnapshotBuffer snapshot)
        {
            _log?.Add(_name + ".OnSnapshotBuilt");
            LastSnapshot = snapshot;
            SnapshotCount++;
        }

        public bool TryFindNearest(Vector2 point, out TargetHit hit)
        {
            QueryCount++;
            hit = default;
            return true;
        }
    }

    internal sealed class LoopEmCommandHandler : ISimulationCommandHandler
    {
        internal readonly List<SimulationCommand> Handled = new List<SimulationCommand>();
        internal Action<SimulationCommand> OnHandle;

        public void Handle(in SimulationCommand command, in StepContext context)
        {
            Handled.Add(command);
            OnHandle?.Invoke(command);
        }
    }

    internal sealed class LoopEmCommandSink : ISimulationCommands
    {
        internal readonly List<SimulationCommand> Commands = new List<SimulationCommand>();

        public void Enqueue(in SimulationCommand command)
        {
            Commands.Add(command);
        }
    }

    internal sealed class LoopEmEventHandler : ICollisionEventHandler
    {
        private readonly List<string> _log;
        private readonly string _name;

        internal LoopEmEventHandler(List<string> log, string name)
        {
            _log = log;
            _name = name;
        }

        public void Handle(in CollisionEvent e, in StepContext context)
        {
            _log.Add(_name + ":" + e);
        }
    }

    internal sealed class LoopEmPhaseProbe : IStepPhaseHandler
    {
        private readonly List<string> _log;
        private readonly string _name;

        internal LoopEmPhaseProbe(List<string> log, string name)
        {
            _log = log;
            _name = name;
        }

        public void OnPhase(SimulationPhase phase, in StepContext context)
        {
            _log.Add(_name + "@" + phase + "#" + context.Step);
        }
    }

    internal static class LoopEmObjects
    {
        // Destroys every GameObject the loop EditMode tests created in the edit scene.
        internal static void DestroyTestObjects()
        {
            foreach (GameObject gameObject in Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (gameObject != null && gameObject.name.StartsWith("LoopEm"))
                {
                    Object.DestroyImmediate(gameObject);
                }
            }
        }
    }
}
