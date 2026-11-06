using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using Survivor.Core;
using Survivor.Data;
using Survivor.Simulation;
using Survivor.Strategies.Tick;
using Survivor.Tests.EditMode.Loop;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Survivor.Tests.EditMode.Monsters
{
    // Loads the real data files from StreamingAssets, optionally patched as text before validation.
    internal static class MonEmData
    {
        private static readonly DataFileKind[] Kinds =
        {
            DataFileKind.Weapons, DataFileKind.Monsters, DataFileKind.Combinations, DataFileKind.SpawnTable,
            DataFileKind.Passives, DataFileKind.LevelUpPool, DataFileKind.PlayerStats,
        };

        internal static Dictionary<DataFileKind, string> RealTexts()
        {
            string root = Path.Combine(Application.dataPath, "StreamingAssets", GameDataLoader.DataFolder);
            var texts = new Dictionary<DataFileKind, string>();
            foreach (DataFileKind kind in Kinds)
            {
                texts[kind] = File.ReadAllText(Path.Combine(root, GameDataLoader.FileNameOf(kind)));
            }

            return texts;
        }

        // Real data with each (file, old, new) text replacement applied; every replacement must hit.
        internal static (GameData data, DataValidationReport report) Load(params (DataFileKind kind, string from, string to)[] patches)
        {
            Dictionary<DataFileKind, string> texts = RealTexts();
            foreach ((DataFileKind kind, string from, string to) in patches)
            {
                Assert.IsTrue(texts[kind].Contains(from), "patch target not found in " + kind + ": " + from);
                texts[kind] = texts[kind].Replace(from, to);
            }

            (GameData data, DataValidationReport report) loaded = GameDataLoader.LoadFromTexts(texts);
            Assert.IsTrue(loaded.report.IsPlayable, "test data must stay playable");
            return loaded;
        }
    }

    // Pool double: instantiates (EditMode-safe) and reuses inactive instances per template; destroys everything with DestroyImmediate.
    internal sealed class MonEmPool : IPoolStrategy
    {
        private readonly Dictionary<GameObject, Stack<GameObject>> _free = new Dictionary<GameObject, Stack<GameObject>>();
        private readonly Dictionary<GameObject, GameObject> _templateOf = new Dictionary<GameObject, GameObject>();
        private readonly List<GameObject> _all = new List<GameObject>();

        public PoolStrategyKind Kind => PoolStrategyKind.CustomPool;

        public int ActiveCount { get; private set; }

        internal int CreatedCount => _all.Count;

        public GameObject Acquire(GameObject template, Vector2 position)
        {
            GameObject instance;
            if (_free.TryGetValue(template, out Stack<GameObject> stack) && stack.Count > 0)
            {
                instance = stack.Pop();
            }
            else
            {
                instance = Object.Instantiate(template);
                instance.name = "MonEm." + template.name;
                _all.Add(instance);
                _templateOf[instance] = template;
            }

            instance.transform.position = position;
            instance.SetActive(true);
            ActiveCount++;
            return instance;
        }

        public void Release(GameObject instance)
        {
            if (!_templateOf.TryGetValue(instance, out GameObject template) || !instance.activeSelf)
            {
                throw new InvalidOperationException("MonEmPool: foreign or double release.");
            }

            instance.SetActive(false);
            if (!_free.TryGetValue(template, out Stack<GameObject> stack))
            {
                stack = new Stack<GameObject>();
                _free[template] = stack;
            }

            stack.Push(instance);
            ActiveCount--;
        }

        public void Prewarm(GameObject template, int count)
        {
        }

        public void ReleaseAll()
        {
            throw new NotSupportedException("MonEmPool: run resets go through EntityLifecycle.DespawnAll in these tests.");
        }

        public void Clear()
        {
        }

        internal void DestroyAll()
        {
            for (int i = 0; i < _all.Count; i++)
            {
                if (_all[i] != null)
                {
                    Object.DestroyImmediate(_all[i]);
                }
            }

            _all.Clear();
            _free.Clear();
            _templateOf.Clear();
        }
    }

    internal sealed class MonEmPhysics : IPhysicsBodyRegistry
    {
        public void Register(CollisionBody body)
        {
        }

        public void Unregister(CollisionBody body)
        {
        }
    }

    // Phase handler that forwards to a delegate the test can swap at any time.
    internal sealed class MonEmPhaseHook : IStepPhaseHandler
    {
        internal Action<StepContext> Action;

        public void OnPhase(SimulationPhase phase, in StepContext context)
        {
            Action?.Invoke(context);
        }
    }

    // Stand-in for the gem/chest handler when a test only counts drop commands.
    internal sealed class MonEmDropCounter : ISimulationCommandHandler
    {
        internal int Gems;
        internal int Chests;

        public void Handle(in SimulationCommand command, in StepContext context)
        {
            if (command.Kind == SimulationCommandKind.SpawnGem)
            {
                Gems++;
            }
            else
            {
                Chests++;
            }
        }
    }

    internal sealed class MonEmNoSpawnPolicy : ISpawnPolicy
    {
        public void CollectSpawns(in StepContext context, int liveMonsterCount, DeterministicRng spawnRng, List<string> monsterIdsOut)
        {
        }

        public bool BossSpawnDue(long step)
        {
            return false;
        }
    }

    // The monster side wired the way the composition root will (5c.3 #9 orders), with a real lifecycle over the real
    // Monster/EnemyProjectile/EnemyHazardZone/ExperienceGem/Chest components, central ticks and scripted collision events.
    // Resolve 10 and Lifecycle 90 hooks stand in for the player's damage resolver and the step outcome gate.
    internal sealed class MonEmRig : IDisposable
    {
        internal const ulong Seed = 20261107UL;

        internal readonly SimulationClock Clock = new SimulationClock();
        internal readonly SimulationPipeline Pipeline = new SimulationPipeline();
        internal readonly SimulationRules Rules = new SimulationRules();
        internal readonly SimulationCommandBuffer Commands = new SimulationCommandBuffer();
        internal readonly MonsterSnapshotBuffer Snapshot = new MonsterSnapshotBuffer();
        internal readonly EntityHandleAllocator Ids = new EntityHandleAllocator();
        internal readonly CentralTickStrategy Ticks = new CentralTickStrategy();
        internal readonly LoopEmCollisions Collisions = new LoopEmCollisions(null);
        internal readonly MonEmPool Pool = new MonEmPool();
        internal readonly RngStreams Rng = new RngStreams(Seed);
        internal readonly GameDataRegistry Data = new GameDataRegistry();
        internal readonly PrefabCatalog Catalog;
        internal readonly EntityLifecycle Lifecycle;
        internal readonly MonsterRegistry Registry;
        internal readonly CollisionEventRouter Router;
        internal readonly MonsterSpawner Spawner;
        internal readonly MonsterCommandHandler MonsterCommands;
        internal readonly DropCommandHandler Drops;
        internal readonly MonEmDropCounter DropCounter;
        internal readonly DespawnSystem Despawner;
        internal readonly KillPipeline Kills;
        internal readonly PickupSystem Pickups;
        internal readonly PickupRegistry PickupCounts;
        internal readonly MonEmPhaseHook Resolve10 = new MonEmPhaseHook();
        internal readonly MonEmPhaseHook Lifecycle90 = new MonEmPhaseHook();
        internal readonly List<int> XpLog = new List<int>();
        internal readonly List<long> ChestLog = new List<long>();
        internal readonly List<DeterministicRng> ChestRngLog = new List<DeterministicRng>();
        internal readonly List<SimEntity> SpawnedLog = new List<SimEntity>();
        internal readonly List<EntityHandle> SpawnedIds = new List<EntityHandle>();
        internal readonly List<(EntityHandle id, DespawnReason reason)> DespawnedLog = new List<(EntityHandle, DespawnReason)>();
        internal Vector2 Player;

        internal MonEmRig(GameData data = null, DataValidationReport report = null, bool countDropsOnly = false)
        {
            if (data == null)
            {
                (data, report) = MonEmData.Load();
            }

            Data.Initialize(data, report ?? new DataValidationReport());
            var types = new Dictionary<EntityCategory, Type>
            {
                { EntityCategory.Monster, typeof(Monster) },
                { EntityCategory.EnemyProjectile, typeof(EnemyProjectile) },
                { EntityCategory.EnemyZone, typeof(EnemyHazardZone) },
                { EntityCategory.Gem, typeof(ExperienceGem) },
                { EntityCategory.Chest, typeof(Chest) },
            };
            Catalog = new PrefabCatalog(types, new IEntityTemplateDecorator[0]);
            Lifecycle = new EntityLifecycle(Pool, Ids, Ticks, Collisions, new MonEmPhysics(), Catalog, Clock);
            Lifecycle.Spawned += e =>
            {
                SpawnedLog.Add(e);
                SpawnedIds.Add(e.Id);
            };
            Lifecycle.Despawned += (e, reason) => DespawnedLog.Add((e.Id, reason));
            Registry = new MonsterRegistry(Lifecycle, Snapshot);
            Router = new CollisionEventRouter(Collisions);
            Spawner = new MonsterSpawner(Lifecycle, Data, Rng);
            Spawner.Bind(new MonEmNoSpawnPolicy());
            MonsterCommands = new MonsterCommandHandler(Lifecycle, Data, Rng, Spawner);
            Despawner = new DespawnSystem(Lifecycle, Registry, Data);
            Kills = new KillPipeline(Registry, Data, Rng);
            Pickups = new PickupSystem(Lifecycle, Data, Rng, xp => XpLog.Add(xp), (rng, context) =>
            {
                ChestRngLog.Add(rng);
                ChestLog.Add(context.Step);
            });
            PickupCounts = new PickupRegistry(Lifecycle);

            Commands.RegisterHandler(SimulationCommandKind.Despawn, new EntityDespawnCommandHandler(Lifecycle));
            Commands.RegisterHandler(SimulationCommandKind.SpawnEnemyProjectile, MonsterCommands);
            Commands.RegisterHandler(SimulationCommandKind.SpawnEnemyZone, MonsterCommands);
            Commands.RegisterHandler(SimulationCommandKind.SpawnMonsters, MonsterCommands);
            ISimulationCommandHandler dropHandler;
            if (countDropsOnly)
            {
                DropCounter = new MonEmDropCounter();
                dropHandler = DropCounter;
            }
            else
            {
                Drops = new DropCommandHandler(Lifecycle, Data);
                dropHandler = Drops;
            }

            Commands.RegisterHandler(SimulationCommandKind.SpawnGem, dropHandler);
            Commands.RegisterHandler(SimulationCommandKind.SpawnChest, dropHandler);
            Router.SetHandler(CollisionChannel.PlayerMagnetVsGem, Pickups);
            Router.SetHandler(CollisionChannel.PlayerBodyVsGem, Pickups);
            Router.SetHandler(CollisionChannel.PlayerBodyVsChest, Pickups);

            Pipeline.AddHandler(SimulationPhase.BuildIndex, 0, Registry);
            Pipeline.AddHandler(SimulationPhase.WeaponTick, 10, Pickups);
            Pipeline.AddHandler(SimulationPhase.Collision, 0, Router);
            Pipeline.AddHandler(SimulationPhase.Resolve, 0, Router);
            Pipeline.AddHandler(SimulationPhase.Resolve, 10, Resolve10);
            Pipeline.AddHandler(SimulationPhase.Resolve, 30, Kills);
            Pipeline.AddHandler(SimulationPhase.Lifecycle, 0, Commands);
            Pipeline.AddHandler(SimulationPhase.Lifecycle, 10, new LifetimeSystem(Lifecycle));
            Pipeline.AddHandler(SimulationPhase.Lifecycle, 20, Despawner);
            Pipeline.AddHandler(SimulationPhase.Lifecycle, 30, Spawner);
            Pipeline.AddHandler(SimulationPhase.Lifecycle, 90, Lifecycle90);
            Clock.Open();
        }

        internal StepContext Context => new StepContext(Clock.Step, Player, Rules, null, Commands);

        // Runs n full steps the way the three drivers do (tick groups before the phase handlers of MonsterTick/ProjectileTick).
        internal void Step(int n = 1)
        {
            for (int i = 0; i < n; i++)
            {
                StepOnce();
            }
        }

        internal void JumpTo(long step)
        {
            Clock.RequestJump(step);
        }

        // Spawns one monster through the dev command path (radius 0, cap ignored) in the next step; returns it.
        internal Monster SpawnMonster(string id, Vector2 position)
        {
            int before = SpawnedLog.Count;
            Commands.Enqueue(new SimulationCommand(SimulationCommandKind.SpawnMonsters, EntityHandle.None, position: position,
                definitionId: id, count: 1, ignoreCap: true));
            Step();
            Assert.AreEqual(before + 1, SpawnedLog.Count, "exactly one monster spawned");
            return (Monster)SpawnedLog[SpawnedLog.Count - 1];
        }

        // Id the most recent spawn had (still valid after the entity was despawned and its Id cleared).
        internal EntityHandle LastSpawnedId => SpawnedIds[SpawnedIds.Count - 1];

        internal void Enqueue(in SimulationCommand command)
        {
            Commands.Enqueue(command);
        }

        // Applies damage to each monster (in the given order) in the next step's Resolve order 10, like WeaponDamageResolver.
        internal void DamageNextStep(float amount, params Monster[] monsters)
        {
            Resolve10.Action = context =>
            {
                foreach (Monster monster in monsters)
                {
                    LookUpDamageable(monster.Id)?.ApplyDamage(amount, context.Step);
                }

                Resolve10.Action = null;
            };
        }

        // The lookup the composition root injects into the player's damage code.
        internal IDamageable LookUpDamageable(EntityHandle id)
        {
            return Lifecycle.TryGet(id, out SimEntity e) ? e as IDamageable : null;
        }

        internal IContactDamageSource LookUpContactSource(EntityHandle id)
        {
            return Lifecycle.TryGet(id, out SimEntity e) ? e as IContactDamageSource : null;
        }

        internal List<T> Live<T>() where T : SimEntity
        {
            var list = new List<T>();
            foreach (SimEntity entity in Lifecycle.ActiveEntities)
            {
                if (entity is T typed)
                {
                    list.Add(typed);
                }
            }

            list.Sort((a, b) => a.Id.CompareTo(b.Id));
            return list;
        }

        internal void AddEvent(CollisionChannel channel, EntityHandle b, CollisionEventKind kind = CollisionEventKind.Enter)
        {
            Collisions.NextEvents.Add(new CollisionEvent(Clock.Step, kind, channel, new EntityHandle(1, 1), b));
        }

        public void Dispose()
        {
            if (Clock.StepActive)
            {
                Clock.AbortStep();
            }

            Lifecycle.DespawnAll(DespawnReason.RunReset);
            Ticks.Clear();
            Pool.DestroyAll();
            foreach (EntityCategory category in new[]
                     {
                         EntityCategory.Monster, EntityCategory.EnemyProjectile, EntityCategory.EnemyZone, EntityCategory.Gem,
                         EntityCategory.Chest,
                     })
            {
                Object.DestroyImmediate(Catalog.GetTemplate(category));
            }
        }

        private void StepOnce()
        {
            Assert.IsTrue(Clock.TryBeginStep(long.MaxValue), "the rig gate is open");
            try
            {
                for (SimulationPhase phase = SimulationPhase.Begin; phase <= SimulationPhase.Lifecycle; phase++)
                {
                    Clock.SetPhase(phase);
                    StepContext context = Context;
                    Clock.SetContext(context);
                    if (phase == SimulationPhase.MonsterTick)
                    {
                        Ticks.RunGroup(TickGroup.Monsters, context);
                    }
                    else if (phase == SimulationPhase.ProjectileTick)
                    {
                        Ticks.RunGroup(TickGroup.Projectiles, context);
                    }

                    Pipeline.RunPhase(phase, context);
                    if (phase == SimulationPhase.Collision)
                    {
                        Collisions.NextEvents.Clear();
                    }
                }
            }
            catch
            {
                Clock.AbortStep();
                throw;
            }

            Clock.EndStep(long.MaxValue);
        }
    }
}
