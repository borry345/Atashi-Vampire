using System;
using System.Collections.Generic;
using Survivor.Core;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Survivor.Tests.EditMode.Player
{
    // Instantiates from the template and keeps every clone so TearDown can destroy them (EditMode: DestroyImmediate).
    internal sealed class PlrPool : IPoolStrategy
    {
        internal readonly List<GameObject> Created = new List<GameObject>();
        private readonly Dictionary<GameObject, Stack<GameObject>> _free = new Dictionary<GameObject, Stack<GameObject>>();
        private readonly Dictionary<GameObject, GameObject> _templateOf = new Dictionary<GameObject, GameObject>();
        private int _active;

        public PoolStrategyKind Kind => PoolStrategyKind.CustomPool;

        public int ActiveCount => _active;

        public GameObject Acquire(GameObject template, Vector2 position)
        {
            if (!_free.TryGetValue(template, out Stack<GameObject> free))
            {
                free = new Stack<GameObject>();
                _free.Add(template, free);
            }

            GameObject instance = free.Count > 0 ? free.Pop() : null;
            if (instance == null)
            {
                instance = Object.Instantiate(template);
                instance.name = "PlrEntity." + template.name;
                Created.Add(instance);
                _templateOf.Add(instance, template);
            }

            instance.transform.position = position;
            instance.SetActive(true);
            _active++;
            return instance;
        }

        public void Release(GameObject instance)
        {
            instance.SetActive(false);
            _free[_templateOf[instance]].Push(instance);
            _active--;
        }

        public void Prewarm(GameObject template, int count)
        {
        }

        public void ReleaseAll()
        {
        }

        public void Clear()
        {
        }

        internal void DestroyAll()
        {
            for (int i = 0; i < Created.Count; i++)
            {
                if (Created[i] != null)
                {
                    Object.DestroyImmediate(Created[i]);
                }
            }

            Created.Clear();
        }
    }

    // Central tick in ascending id order.
    internal sealed class PlrTicks : ITickStrategy
    {
        private readonly List<IUpdatable> _items = new List<IUpdatable>();

        public TickStrategyKind Kind => TickStrategyKind.CentralTick;

        public void Register(IUpdatable updatable, GameObject host)
        {
            _items.Add(updatable);
        }

        public void Unregister(IUpdatable updatable)
        {
            _items.Remove(updatable);
        }

        public void RunGroup(TickGroup group, in StepContext context)
        {
            _items.Sort((a, b) => a.Id.CompareTo(b.Id));
            var copy = new List<IUpdatable>(_items);
            for (int i = 0; i < copy.Count; i++)
            {
                if (copy[i].Group == group)
                {
                    copy[i].Tick(context);
                }
            }
        }

        public int RegisteredCount(TickGroup group)
        {
            int count = 0;
            for (int i = 0; i < _items.Count; i++)
            {
                if (_items[i].Group == group)
                {
                    count++;
                }
            }

            return count;
        }

        public void Clear()
        {
            _items.Clear();
        }
    }

    internal sealed class PlrPhysics : IPhysicsBodyRegistry
    {
        internal readonly List<CollisionBody> Bodies = new List<CollisionBody>();

        public void Register(CollisionBody body)
        {
            Bodies.Add(body);
        }

        public void Unregister(CollisionBody body)
        {
            Bodies.Remove(body);
        }
    }

    // Scripted events per step; records which bodies were registered when each step collected (participation, TAC-0-8).
    internal sealed class PlrCollisions : ICollisionStrategy
    {
        internal readonly List<CollisionBody> Bodies = new List<CollisionBody>();
        internal readonly Dictionary<long, List<CollisionEvent>> Script = new Dictionary<long, List<CollisionEvent>>();
        internal readonly Dictionary<long, List<EntityHandle>> RegisteredAtCollect = new Dictionary<long, List<EntityHandle>>();
        internal readonly List<CollisionBody> RadiusNotifications = new List<CollisionBody>();
        private static readonly List<CollisionEvent> Empty = new List<CollisionEvent>();

        public CollisionStrategyKind Kind => CollisionStrategyKind.SelfSquaredDistance;

        public void Register(CollisionBody body)
        {
            Bodies.Add(body);
        }

        public void Unregister(CollisionBody body)
        {
            Bodies.Remove(body);
        }

        public void NotifyRadiusChanged(CollisionBody body)
        {
            RadiusNotifications.Add(body);
        }

        public IReadOnlyList<CollisionEvent> CollectEvents(long step)
        {
            var ids = new List<EntityHandle>();
            for (int i = 0; i < Bodies.Count; i++)
            {
                ids.Add(Bodies[i].Id);
            }

            RegisteredAtCollect[step] = ids;
            if (!Script.TryGetValue(step, out List<CollisionEvent> events))
            {
                return Empty;
            }

            events.Sort();
            return events;
        }

        public void Clear()
        {
            Bodies.Clear();
        }

        internal void Add(long step, CollisionEventKind kind, CollisionChannel channel, EntityHandle a, EntityHandle b)
        {
            if (!Script.TryGetValue(step, out List<CollisionEvent> events))
            {
                events = new List<CollisionEvent>();
                Script.Add(step, events);
            }

            events.Add(new CollisionEvent(step, kind, channel, a, b));
        }
    }

    // Monster stand-in for the Core boundary interfaces (MONSTER's Monster is not in this worktree).
    internal sealed class PlrMonster : IDamageable, IContactDamageSource
    {
        internal readonly EntityHandle Id;
        internal Vector2 Position;
        internal float Hp;
        internal readonly List<(float amount, long step)> Hits = new List<(float, long)>();

        internal PlrMonster(EntityHandle id, Vector2 position, float hp = 1000f, float contactDamage = 10f)
        {
            Id = id;
            Position = position;
            Hp = hp;
            ContactDamage = contactDamage;
        }

        public bool IsAlive => Hp > 0f;

        public float ContactDamage { get; set; }

        public void ApplyDamage(float amount, long step)
        {
            if (!IsAlive)
            {
                return;
            }

            Hits.Add((amount, step));
            Hp -= amount;
        }
    }

    // Brute-force nearest over the registered monster stand-ins (distance, then smaller id).
    internal sealed class PlrTargets : ITargetProvider
    {
        internal readonly List<PlrMonster> Monsters = new List<PlrMonster>();
        internal int QueryCount;

        public TargetProviderKind Kind => TargetProviderKind.BruteForce;

        public void OnSnapshotBuilt(MonsterSnapshotBuffer snapshot)
        {
        }

        public bool TryFindNearest(Vector2 point, out TargetHit hit)
        {
            QueryCount++;
            hit = default;
            bool found = false;
            for (int i = 0; i < Monsters.Count; i++)
            {
                PlrMonster m = Monsters[i];
                if (!m.IsAlive)
                {
                    continue;
                }

                float d = (m.Position - point).sqrMagnitude;
                if (!found || d < hit.DistanceSquared || (d == hit.DistanceSquared && m.Id < hit.Id))
                {
                    hit = new TargetHit(m.Id, m.Position, d);
                    found = true;
                }
            }

            return found;
        }
    }

    internal sealed class PlrInput : IInputSource
    {
        internal MoveCommand Next = MoveCommand.None;
        internal readonly List<(long step, Vector2 position)> Calls = new List<(long, Vector2)>();

        public MoveCommand ReadMove(long step, Vector2 currentPosition)
        {
            Calls.Add((step, currentPosition));
            return Next;
        }
    }

    internal sealed class PlrCommandSink : ISimulationCommands
    {
        internal readonly List<SimulationCommand> Commands = new List<SimulationCommand>();

        public void Enqueue(in SimulationCommand command)
        {
            Commands.Add(command);
        }
    }
}
