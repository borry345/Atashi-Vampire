using System;
using System.Collections.Generic;
using Survivor.Core;
using Survivor.Simulation;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Survivor.Tests.PlayMode.Loop
{
    // Recording pool double: Instantiate on demand, LIFO reuse per template, DestroyImmediate on Clear; logs call order.
    internal sealed class LoopPmPool : IPoolStrategy
    {
        private readonly List<string> _log;
        private readonly string _name;
        private readonly Dictionary<GameObject, Stack<GameObject>> _inactive = new Dictionary<GameObject, Stack<GameObject>>();
        private readonly Dictionary<GameObject, GameObject> _templateOf = new Dictionary<GameObject, GameObject>();
        private readonly HashSet<GameObject> _active = new HashSet<GameObject>();

        internal LoopPmPool(List<string> log, string name = "pool", PoolStrategyKind kind = PoolStrategyKind.CustomPool)
        {
            _log = log;
            _name = name;
            Kind = kind;
        }

        internal int ClearCount;
        internal readonly List<KeyValuePair<GameObject, int>> Prewarms = new List<KeyValuePair<GameObject, int>>();

        public PoolStrategyKind Kind { get; }

        public int ActiveCount => _active.Count;

        public GameObject Acquire(GameObject template, Vector2 position)
        {
            _log?.Add(_name + ".Acquire");
            if (!_inactive.TryGetValue(template, out Stack<GameObject> stack))
            {
                stack = new Stack<GameObject>();
                _inactive.Add(template, stack);
            }

            GameObject instance = stack.Count > 0 ? stack.Pop() : Object.Instantiate(template);
            _templateOf[instance] = template;
            instance.transform.position = position;
            instance.SetActive(true);
            _active.Add(instance);
            return instance;
        }

        public void Release(GameObject instance)
        {
            _log?.Add(_name + ".Release");
            if (!_active.Remove(instance))
            {
                throw new InvalidOperationException("LoopPmPool: double or foreign release.");
            }

            instance.SetActive(false);
            _inactive[_templateOf[instance]].Push(instance);
        }

        public void Prewarm(GameObject template, int count)
        {
            Prewarms.Add(new KeyValuePair<GameObject, int>(template, count));
        }

        public void ReleaseAll()
        {
            foreach (GameObject instance in new List<GameObject>(_active))
            {
                Release(instance);
            }
        }

        public void Clear()
        {
            ClearCount++;
            _log?.Add(_name + ".Clear");
            foreach (Stack<GameObject> stack in _inactive.Values)
            {
                foreach (GameObject instance in stack)
                {
                    Object.DestroyImmediate(instance);
                }
            }

            _inactive.Clear();
        }
    }

    // Records Register/Unregister; RunGroup ticks registered updatables in order (enough for lifecycle tests).
    internal sealed class LoopPmTicks : ITickStrategy
    {
        private readonly List<string> _log;
        internal readonly List<IUpdatable> Registered = new List<IUpdatable>();

        internal LoopPmTicks(List<string> log, TickStrategyKind kind = TickStrategyKind.CentralTick)
        {
            _log = log;
            Kind = kind;
        }

        public TickStrategyKind Kind { get; }

        public void Register(IUpdatable updatable, GameObject host)
        {
            _log?.Add("ticks.Register");
            Registered.Add(updatable);
        }

        public void Unregister(IUpdatable updatable)
        {
            _log?.Add("ticks.Unregister");
            Registered.Remove(updatable);
        }

        public void RunGroup(TickGroup group, in StepContext context)
        {
            foreach (IUpdatable updatable in Registered.ToArray())
            {
                if (updatable.Group == group)
                {
                    updatable.Tick(context);
                }
            }
        }

        public int RegisteredCount(TickGroup group)
        {
            return Registered.FindAll(u => u.Group == group).Count;
        }

        public void Clear()
        {
            Registered.Clear();
        }
    }

    // Records registrations and returns a scripted (possibly unsorted) event list from CollectEvents.
    internal sealed class LoopPmCollisions : ICollisionStrategy
    {
        private readonly List<string> _log;
        internal readonly List<CollisionBody> Bodies = new List<CollisionBody>();
        internal readonly List<CollisionEvent> NextEvents = new List<CollisionEvent>();
        internal readonly List<long> CollectedSteps = new List<long>();

        internal LoopPmCollisions(List<string> log, CollisionStrategyKind kind = CollisionStrategyKind.SelfSquaredDistance)
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

    internal sealed class LoopPmPhysics : IPhysicsBodyRegistry
    {
        private readonly List<string> _log;

        internal LoopPmPhysics(List<string> log)
        {
            _log = log;
        }

        public void Register(CollisionBody body)
        {
            _log?.Add("physics.Register");
        }

        public void Unregister(CollisionBody body)
        {
            _log?.Add("physics.Unregister");
        }
    }

    // Records snapshot deliveries and queries; answers with a fixed hit.
    internal sealed class LoopPmTargetProvider : ITargetProvider
    {
        private readonly List<string> _log;
        private readonly string _name;
        internal MonsterSnapshotBuffer LastSnapshot;
        internal int SnapshotCount;
        internal int QueryCount;

        internal LoopPmTargetProvider(List<string> log, string name, TargetProviderKind kind = TargetProviderKind.BruteForce)
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

    // Logs "phase#step position" and " ctx" when the clock's CurrentContext already equals the handler's context.
    internal sealed class LoopPmContextProbe : IStepPhaseHandler
    {
        private readonly List<string> _log;
        private readonly SimulationClock _clock;

        internal LoopPmContextProbe(List<string> log, SimulationClock clock)
        {
            _log = log;
            _clock = clock;
        }

        public void OnPhase(SimulationPhase phase, in StepContext context)
        {
            bool final = phase >= SimulationPhase.MonsterTick && _clock.CurrentContext.Step == context.Step
                && _clock.CurrentContext.PlayerPosition == context.PlayerPosition;
            _log.Add(phase + "#" + context.Step + " " + context.PlayerPosition + (final ? " ctx" : ""));
        }
    }

    internal sealed class LoopPmDecorator : IEntityTemplateDecorator
    {
        private readonly List<string> _log;
        private readonly string _name;

        internal LoopPmDecorator(List<string> log, string name)
        {
            _log = log;
            _name = name;
        }

        public void Decorate(GameObject template, EntityCategory category)
        {
            _log.Add(_name + ":" + category + ":" + template.activeSelf);
        }
    }

    // Lifecycle world over recording doubles; Lifecycle-phase work runs inside InStep.
    internal sealed class LoopPmWorld
    {
        internal const string ObjectPrefix = "Template.";

        internal readonly List<string> Log = new List<string>();
        internal readonly SimulationClock Clock = new SimulationClock();
        internal readonly EntityHandleAllocator Ids = new EntityHandleAllocator();
        internal readonly SimulationRules Rules = new SimulationRules();
        internal readonly LoopPmPool Pool;
        internal readonly LoopPmTicks Ticks;
        internal readonly LoopPmCollisions Collisions;
        internal readonly LoopPmPhysics Physics;
        internal readonly PrefabCatalog Catalog;
        internal readonly EntityLifecycle Lifecycle;

        internal LoopPmWorld()
        {
            Pool = new LoopPmPool(Log);
            Ticks = new LoopPmTicks(Log);
            Collisions = new LoopPmCollisions(Log);
            Physics = new LoopPmPhysics(Log);
            var types = new Dictionary<EntityCategory, Type>();
            foreach (EntityCategory category in Enum.GetValues(typeof(EntityCategory)))
            {
                types.Add(category, typeof(LoopPmTickEntity));
            }

            types[EntityCategory.Gem] = typeof(LoopPmPlainEntity);
            types[EntityCategory.Chest] = typeof(LoopPmPlainEntity);
            Catalog = new PrefabCatalog(types, new IEntityTemplateDecorator[0]);
            Lifecycle = new EntityLifecycle(Pool, Ids, Ticks, Collisions, Physics, Catalog, Clock);
        }

        internal StepContext Context(ISimulationCommands commands = null, ITargetProvider targets = null)
        {
            return new StepContext(Clock.Step, Vector2.zero, Rules, targets, commands);
        }

        // Runs body inside one step whose current phase is phase, then ends the step (Step++).
        internal void InStep(SimulationPhase phase, Action body)
        {
            if (!Clock.IsRunning)
            {
                Clock.Open();
            }

            Assert(Clock.TryBeginStep(long.MaxValue), "step did not begin");
            Clock.SetPhase(phase);
            try
            {
                body();
            }
            finally
            {
                Clock.EndStep(long.MaxValue);
            }
        }

        internal LoopPmTickEntity SpawnTick(EntityCategory category, Vector2 position, long lifetime = 0)
        {
            return Lifecycle.Spawn<LoopPmTickEntity>(category, position, lifetime, e =>
            {
                e.EventLog = Log;
                e.Ids = Ids;
            });
        }

        internal void AdvanceSteps(int count)
        {
            for (int i = 0; i < count; i++)
            {
                InStep(SimulationPhase.Begin, () => { });
            }
        }

        private static void Assert(bool condition, string message)
        {
            if (!condition)
            {
                throw new InvalidOperationException(message);
            }
        }

        // Destroys every template, clone and catalog root the loop tests created in the scene.
        internal static void DestroyTestObjects()
        {
            foreach (GameObject gameObject in Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (gameObject != null && (gameObject.name.StartsWith(ObjectPrefix) || gameObject.name.StartsWith("PrefabCatalog.")
                    || gameObject.name.StartsWith("LoopPm")))
                {
                    Object.DestroyImmediate(gameObject);
                }
            }
        }
    }
}
