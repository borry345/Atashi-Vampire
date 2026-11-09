using System;
using System.Collections.Generic;
using NUnit.Framework;
using Survivor.Core;
using Survivor.Data;
using Survivor.Simulation;
using Survivor.Strategies;
using Survivor.Strategies.Tick;
using Survivor.Tests.EditMode.Monsters;
using Survivor.Tests.EditMode.Player;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Survivor.Tests.EditMode.Progression
{
    // Deterministic scripted movement (a function of the step and the simulation state, so two runs with the same seed move
    // identically): walk to the lowest-id live gem when Seek finds one, otherwise a square path.
    internal sealed class FlowInput : IInputSource
    {
        internal bool Still;
        internal Func<Vector2?> Seek;

        public MoveCommand ReadMove(long step, Vector2 currentPosition)
        {
            if (Still)
            {
                return MoveCommand.None;
            }

            Vector2? target = Seek?.Invoke();
            if (target.HasValue)
            {
                Vector2 toTarget = target.Value - currentPosition;
                return toTarget.sqrMagnitude < 1e-6f ? MoveCommand.None : new MoveCommand(toTarget.normalized);
            }

            int leg = (int)(step / 90 % 4);
            Vector2 direction = leg == 0 ? Vector2.right : leg == 1 ? Vector2.up : leg == 2 ? Vector2.left : Vector2.down;
            return new MoveCommand(direction);
        }
    }

    // Boss due at one fixed step, no normal spawns (TAC-F3-6 path through MonsterSpawner, not the dev command).
    internal sealed class FlowBossAtPolicy : ISpawnPolicy
    {
        private readonly long _step;

        internal FlowBossAtPolicy(long step)
        {
            _step = step;
        }

        public void CollectSpawns(in StepContext context, int liveMonsterCount, DeterministicRng spawnRng, List<string> monsterIdsOut)
        {
        }

        public bool BossSpawnDue(long step)
        {
            return step == _step;
        }
    }

    // The whole simulation wired the way the composition root will (5c.3 #9 orders, 5c.3a table): real player, weapons, monsters,
    // pickups, progression and the real SelfCollisionStrategy + MonsterSpatialIndex + BruteForceTargetProvider + CentralTickStrategy.
    // Only the pool is a test double (PlrPool: EditMode-safe instantiate, no Object.Destroy) and is passed again on every reset so
    // ReplaceStrategies never clears it. Steps run by hand like the three drivers. StartRun plays the App part of a run start.
    internal sealed class FlowEmRig : IDisposable
    {
        internal const ulong DefaultSeed = 20261109UL;

        internal readonly SimulationClock Clock = new SimulationClock();
        internal readonly SimulationPipeline Pipeline = new SimulationPipeline();
        internal readonly SimulationRules Rules = new SimulationRules();
        internal readonly SimulationCommandBuffer Commands = new SimulationCommandBuffer();
        internal readonly MonsterSnapshotBuffer Snapshot = new MonsterSnapshotBuffer();
        internal readonly EntityHandleAllocator Ids = new EntityHandleAllocator();
        internal readonly PlrPool Pool = new PlrPool();
        internal readonly RngStreams Rng = new RngStreams(1UL);
        internal readonly GameDataRegistry Data = new GameDataRegistry();
        internal readonly MonsterSpatialIndex Index;
        internal readonly PrefabCatalog Catalog;
        internal readonly EntityLifecycle Lifecycle;
        internal readonly MonsterRegistry Registry;
        internal readonly CollisionEventRouter Router;
        internal readonly TargetProviderSlot Targets;
        internal readonly PendingCommandQueue Pending;
        internal readonly GameObject Host;
        internal readonly FlowInput Input = new FlowInput();
        internal readonly PassiveInventory Passives = new PassiveInventory();
        internal readonly PlayerHealth Health = new PlayerHealth();
        internal readonly PlayerEntity Player;
        internal readonly PlayerMovement Movement;
        internal readonly WeaponInventory Weapons;
        internal readonly PlayerSpawnCommandHandler PlayerSpawns;
        internal readonly PlayerHitHandler HitHandler;
        internal readonly WeaponDamageResolver DamageResolver;
        internal readonly PlayerDamageReceiver Receiver;
        internal readonly PlayerProgressionApplier Applier;
        internal readonly EvolutionService Evolution;
        internal readonly ChestEffectResolver Chests;
        internal readonly MonsterSpawner Spawner;
        internal readonly MonsterCommandHandler MonsterCommands;
        internal readonly DropCommandHandler Drops;
        internal readonly DespawnSystem Despawner;
        internal readonly KillPipeline Kills;
        internal readonly PickupSystem Pickups;
        internal readonly PickupRegistry PickupCounts;
        internal readonly ExperienceSystem Experience;
        internal readonly LevelUpSession LevelUp = new LevelUpSession();
        internal readonly StepOutcomeGate Gate;
        internal readonly RunSession Session;
        internal readonly PlrHook ResolveHook = new PlrHook();
        internal readonly List<RunStrategies> Handed = new List<RunStrategies>();
        internal ulong Seed = DefaultSeed;
        internal Func<ISpawnPolicy> PolicyFactory;
        internal Func<RunStrategies> StrategyFactory;
        internal Func<IReadOnlyList<UpgradeOption>, int> ChooseOption = options => 0;
        internal int Frame;
        internal readonly List<long> RoundLog = new List<long>();

        internal FlowEmRig(GameData data = null, DataValidationReport report = null, bool start = true)
        {
            if (data == null)
            {
                (data, report) = MonEmData.Load();
            }

            Data.Initialize(data, report ?? new DataValidationReport());
            Func<GameData> current = () => Data.Current;
            Index = new MonsterSpatialIndex(StrategySelection.DefaultSpatialHashCellSize, Snapshot) { Required = true };
            Catalog = new PrefabCatalog(new Dictionary<EntityCategory, Type>
            {
                { EntityCategory.PlayerProjectile, typeof(PlayerProjectile) },
                { EntityCategory.PlayerOrb, typeof(OrbitOrb) },
                { EntityCategory.PlayerZone, typeof(DamageZone) },
                { EntityCategory.Monster, typeof(Monster) },
                { EntityCategory.EnemyProjectile, typeof(EnemyProjectile) },
                { EntityCategory.EnemyZone, typeof(EnemyHazardZone) },
                { EntityCategory.Gem, typeof(ExperienceGem) },
                { EntityCategory.Chest, typeof(Chest) },
            }, new IEntityTemplateDecorator[0]);
            var physics = new MonEmPhysics();
            Lifecycle = new EntityLifecycle(Pool, Ids, new CentralTickStrategy(), new SelfCollisionStrategy(Index, Snapshot), physics,
                Catalog, Clock);
            Registry = new MonsterRegistry(Lifecycle, Snapshot);
            Router = new CollisionEventRouter(Lifecycle.Collisions);
            Targets = new TargetProviderSlot(Clock);
            Pending = new PendingCommandQueue(Targets, Data, Rules);

            Host = new GameObject("FlowEm.PlayerHost");
            Player = new PlayerEntity(Ids, physics, current, Passives, Host);
            Movement = new PlayerMovement(Player, Input, Passives, current);
            Weapons = new WeaponInventory(Player, Passives, Lifecycle);
            PlayerSpawns = new PlayerSpawnCommandHandler(Lifecycle, Weapons);
            HitHandler = new PlayerHitHandler(Lifecycle, Weapons, LookUpDamageable);
            DamageResolver = new WeaponDamageResolver(Weapons, LookUpDamageable);
            Receiver = new PlayerDamageReceiver(Health, current, LookUpContactSource, Lifecycle);
            Applier = new PlayerProgressionApplier(Clock, Weapons, Passives, Health, Player, current);
            Evolution = new EvolutionService(Weapons, Passives, current);
            Chests = new ChestEffectResolver(Evolution, Applier, Weapons);

            Spawner = new MonsterSpawner(Lifecycle, Data, Rng);
            MonsterCommands = new MonsterCommandHandler(Lifecycle, Data, Rng, Spawner);
            Drops = new DropCommandHandler(Lifecycle, Data);
            Despawner = new DespawnSystem(Lifecycle, Registry, Data);
            Kills = new KillPipeline(Registry, Data, Rng);
            Experience = new ExperienceSystem(current);
            Pickups = new PickupSystem(Lifecycle, Data, Rng, Experience.AddXp, (rng, context) => Chests.Resolve(rng, context));
            PickupCounts = new PickupRegistry(Lifecycle);
            Gate = new StepOutcomeGate(Clock, Health, Kills, Experience, current);
            PolicyFactory = () => new TableSpawnPolicy(Data);
            Input.Seek = () =>
            {
                List<ExperienceGem> gems = Live<ExperienceGem>();
                return gems.Count > 0 ? gems[0].Position : (Vector2?)null;
            };
            StrategyFactory = () => new RunStrategies(Pool, new CentralTickStrategy(), new SelfCollisionStrategy(Index, Snapshot),
                new BruteForceTargetProvider());
            Session = new RunSession(Clock, Lifecycle, Router, Targets, Ids, Rng, Commands, Pending, Player, Movement, Health, Weapons,
                Passives, Receiver, Applier, Pickups, Kills, Spawner, Experience, LevelUp, Gate, () => Seed, () => PolicyFactory(),
                () => Input);

            Commands.RegisterHandler(SimulationCommandKind.Despawn, new EntityDespawnCommandHandler(Lifecycle));
            Commands.RegisterHandler(SimulationCommandKind.SpawnPlayerProjectile, PlayerSpawns);
            Commands.RegisterHandler(SimulationCommandKind.SpawnPlayerOrb, PlayerSpawns);
            Commands.RegisterHandler(SimulationCommandKind.SpawnPlayerZone, PlayerSpawns);
            Commands.RegisterHandler(SimulationCommandKind.SpawnEnemyProjectile, MonsterCommands);
            Commands.RegisterHandler(SimulationCommandKind.SpawnEnemyZone, MonsterCommands);
            Commands.RegisterHandler(SimulationCommandKind.SpawnMonsters, MonsterCommands);
            Commands.RegisterHandler(SimulationCommandKind.SpawnGem, Drops);
            Commands.RegisterHandler(SimulationCommandKind.SpawnChest, Drops);

            Router.SetHandler(CollisionChannel.PlayerProjectileVsMonster, HitHandler);
            Router.SetHandler(CollisionChannel.PlayerOrbVsMonster, HitHandler);
            Router.SetHandler(CollisionChannel.PlayerZoneVsMonster, HitHandler);
            Router.SetHandler(CollisionChannel.PlayerBodyVsMonster, Receiver);
            Router.SetHandler(CollisionChannel.EnemyProjectileVsPlayerBody, Receiver);
            Router.SetHandler(CollisionChannel.EnemyZoneVsPlayerBody, Receiver);
            Router.SetHandler(CollisionChannel.PlayerMagnetVsGem, Pickups);
            Router.SetHandler(CollisionChannel.PlayerBodyVsGem, Pickups);
            Router.SetHandler(CollisionChannel.PlayerBodyVsChest, Pickups);

            Pending.RegisterHandler(PendingCommandKind.SetLoadout, Applier.HandleSetLoadout);
            Pending.RegisterHandler(PendingCommandKind.SetHealth, Applier.HandleSetHealth);
            Pending.RegisterHandler(PendingCommandKind.AddXp, Experience.HandleAddXp);
            Pending.RegisterHandler(PendingCommandKind.LevelUpNow, Experience.HandleLevelUpNow);
            Pending.BossSpawnedThisRun = () => Spawner.BossSpawnedThisRun;

            Pipeline.AddHandler(SimulationPhase.Begin, 0, Pending);
            Pipeline.AddHandler(SimulationPhase.Begin, 10, Movement);
            Pipeline.AddHandler(SimulationPhase.PlayerMove, 0, Movement);
            Pipeline.AddHandler(SimulationPhase.BuildIndex, 0, Registry);
            Pipeline.AddHandler(SimulationPhase.BuildIndex, 10, Index);
            Pipeline.AddHandler(SimulationPhase.WeaponTick, 0, Weapons);
            Pipeline.AddHandler(SimulationPhase.WeaponTick, 10, Pickups);
            Pipeline.AddHandler(SimulationPhase.Collision, 0, Router);
            Pipeline.AddHandler(SimulationPhase.Resolve, 0, Router);
            Pipeline.AddHandler(SimulationPhase.Resolve, 5, ResolveHook);
            Pipeline.AddHandler(SimulationPhase.Resolve, 10, DamageResolver);
            Pipeline.AddHandler(SimulationPhase.Resolve, 20, Receiver);
            Pipeline.AddHandler(SimulationPhase.Resolve, 30, Kills);
            Pipeline.AddHandler(SimulationPhase.Lifecycle, 0, Commands);
            Pipeline.AddHandler(SimulationPhase.Lifecycle, 10, new LifetimeSystem(Lifecycle));
            Pipeline.AddHandler(SimulationPhase.Lifecycle, 20, Despawner);
            Pipeline.AddHandler(SimulationPhase.Lifecycle, 30, Spawner);
            Pipeline.AddHandler(SimulationPhase.Lifecycle, 90, Gate);

            if (start)
            {
                StartRun();
            }
        }

        internal ulong Hash => SimulationStateHash.Compute(Session);

        // App part of a run start (TAC-F8-2): Reset with fresh strategies, clear the spatial index, requirements (Required stays
        // true here), then open the gate.
        internal void StartRun()
        {
            RunStrategies next = StrategyFactory();
            Handed.Add(next);
            Session.Reset(next);
            Index.Clear();
            Clock.Open();
        }

        internal IDamageable LookUpDamageable(EntityHandle id)
        {
            return Lifecycle.TryGet(id, out SimEntity e) ? e as IDamageable : null;
        }

        internal IContactDamageSource LookUpContactSource(EntityHandle id)
        {
            return Lifecycle.TryGet(id, out SimEntity e) ? e as IContactDamageSource : null;
        }

        internal void Enqueue(in PendingCommand command)
        {
            Pending.Enqueue(command);
        }

        // One full step the way SimulationDriverEarly/Mid/Late run it.
        internal void Step()
        {
            if (!Clock.TryBeginStep(long.MaxValue))
            {
                throw new InvalidOperationException("FlowEmRig.Step: the gate is closed (" + Clock.CloseReason + ").");
            }

            try
            {
                long step = Clock.Step;
                var before = new StepContext(step, Player.Position, Rules, Targets, Commands);
                Clock.SetContext(before);
                Run(SimulationPhase.Begin, before);
                Run(SimulationPhase.PlayerMove, before);
                var context = new StepContext(step, Player.Position, Rules, Targets, Commands);
                Clock.SetContext(context);
                Clock.SetPhase(SimulationPhase.MonsterTick);
                Lifecycle.Ticks.RunGroup(TickGroup.Monsters, context);
                Pipeline.RunPhase(SimulationPhase.MonsterTick, context);
                Run(SimulationPhase.BuildIndex, context);
                Targets.OnSnapshotBuilt(Snapshot);
                Run(SimulationPhase.WeaponTick, context);
                Clock.SetPhase(SimulationPhase.ProjectileTick);
                Lifecycle.Ticks.RunGroup(TickGroup.Projectiles, context);
                Pipeline.RunPhase(SimulationPhase.ProjectileTick, context);
                Run(SimulationPhase.Collision, context);
                Run(SimulationPhase.Resolve, context);
                Run(SimulationPhase.Lifecycle, context);
            }
            catch
            {
                Clock.AbortStep();
                throw;
            }

            Clock.EndStep(long.MaxValue);
        }

        // The flow's level-up handling (TAC-F5-4 steps 2-4) while the gate is closed for a level-up: build + draw from the state
        // at that moment, open the round, submit ChooseOption on a later frame, apply, re-check; then open the clock. No steps run.
        internal void ResolveLevelUps()
        {
            var candidates = new List<(UpgradeOption option, float weight)>();
            var options = new List<UpgradeOption>();
            bool another = Clock.CloseReason == GateCloseReason.LevelUp && !Clock.IsRunning && !Clock.StepActive;
            while (another)
            {
                LevelUpCandidateBuilder.Build(Data.Current, Weapons, Passives, Health.Hp, Health.MaxHp, candidates);
                LevelUpSelector.Draw(candidates, Rng.Get(RngStream.LevelUp), options);
                LevelUp.Open(options, Frame);
                RoundLog.Add(LevelUp.RoundId);
                Frame++;
                Assert.IsTrue(LevelUp.TrySubmit(LevelUp.RoundId, ChooseOption(LevelUp.Options), Frame, out UpgradeOption chosen),
                    "the first submission of a round is accepted");
                LevelUp.Close();
                Applier.ApplyLevelUpOption(chosen);
                another = Experience.TryBeginLevelUp(Data.Current.PlayerStats.XpCurve);
            }

            Clock.Open();
        }

        // Runs until count steps executed (resolving level-ups as the flow would) or the run ended; returns the hash after each step.
        internal List<ulong> Play(int count)
        {
            var hashes = new List<ulong>(count);
            for (int i = 0; i < count; i++)
            {
                if (!Clock.IsRunning)
                {
                    if (Clock.CloseReason != GateCloseReason.LevelUp)
                    {
                        break;
                    }

                    ResolveLevelUps();
                }

                Step();
                hashes.Add(Hash);
            }

            return hashes;
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

        public void Dispose()
        {
            if (Clock.StepActive)
            {
                Clock.AbortStep();
            }

            Player.UnregisterFromRun();
            Lifecycle.DespawnAll(DespawnReason.RunReset);
            Lifecycle.Ticks.Clear();
            Pool.DestroyAll();
            foreach (EntityCategory category in new[]
                     {
                         EntityCategory.PlayerProjectile, EntityCategory.PlayerOrb, EntityCategory.PlayerZone, EntityCategory.Monster,
                         EntityCategory.EnemyProjectile, EntityCategory.EnemyZone, EntityCategory.Gem, EntityCategory.Chest,
                     })
            {
                Object.DestroyImmediate(Catalog.GetTemplate(category));
            }

            Object.DestroyImmediate(Host);
            if (Player != null && Player.MagnetHost != null)
            {
                Object.DestroyImmediate(Player.MagnetHost);
            }
        }

        private void Run(SimulationPhase phase, in StepContext context)
        {
            Clock.SetPhase(phase);
            Pipeline.RunPhase(phase, context);
        }
    }
}
