using System;
using System.Collections.Generic;
using Survivor.Core;
using Survivor.Data;
using Survivor.Simulation;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Survivor.Tests.EditMode.Player
{
    // Records every command with the step it was queued in, then forwards it to the real buffer.
    internal sealed class PlrRecorder : ISimulationCommands
    {
        private readonly SimulationCommandBuffer _buffer;
        private readonly SimulationClock _clock;
        internal readonly List<(long step, SimulationPhase phase, SimulationCommand command)> Log =
            new List<(long, SimulationPhase, SimulationCommand)>();

        internal PlrRecorder(SimulationCommandBuffer buffer, SimulationClock clock)
        {
            _buffer = buffer;
            _clock = clock;
        }

        public void Enqueue(in SimulationCommand command)
        {
            Log.Add((_clock.Step, _clock.CurrentPhase, command));
            _buffer.Enqueue(command);
        }

        internal List<SimulationCommand> At(long step, SimulationCommandKind kind)
        {
            var result = new List<SimulationCommand>();
            for (int i = 0; i < Log.Count; i++)
            {
                if (Log[i].step == step && Log[i].command.Kind == kind)
                {
                    result.Add(Log[i].command);
                }
            }

            return result;
        }

        internal int Count(SimulationCommandKind kind)
        {
            int count = 0;
            for (int i = 0; i < Log.Count; i++)
            {
                if (Log[i].command.Kind == kind)
                {
                    count++;
                }
            }

            return count;
        }
    }

    // Runs actions queued by a test at a fixed phase/order (Lifecycle 50: spawn stand-ins; Resolve 5: direct calls; Lifecycle 90: gate).
    internal sealed class PlrHook : IStepPhaseHandler
    {
        internal readonly List<Action<StepContext>> Pending = new List<Action<StepContext>>();
        internal Action<StepContext> EveryStep;

        public void OnPhase(SimulationPhase phase, in StepContext context)
        {
            EveryStep?.Invoke(context);
            var copy = new List<Action<StepContext>>(Pending);
            Pending.Clear();
            for (int i = 0; i < copy.Count; i++)
            {
                copy[i](context);
            }
        }
    }

    // Channel 8 stand-in for PickupSystem: opens one chest per Enter through the resolver, in event order (TAC-F4-4).
    internal sealed class PlrChestProbe : ICollisionEventHandler
    {
        private readonly Func<ChestEffectResolver> _resolver;
        private readonly DeterministicRng _rng;
        internal readonly List<ChestEffect> Effects = new List<ChestEffect>();

        internal PlrChestProbe(Func<ChestEffectResolver> resolver, DeterministicRng rng)
        {
            _resolver = resolver;
            _rng = rng;
        }

        public void Handle(in CollisionEvent e, in StepContext context)
        {
            if (e.Kind == CollisionEventKind.Enter)
            {
                Effects.Add(_resolver().Resolve(_rng, context));
            }
        }
    }

    // Player-side assembly in the 5c.3 #9 order, stepped by hand like the three drivers (no MonoBehaviour driver in EditMode).
    internal sealed class PlrRig
    {
        internal readonly SimulationClock Clock = new SimulationClock();
        internal readonly EntityHandleAllocator Ids = new EntityHandleAllocator();
        internal readonly PlrPool Pool = new PlrPool();
        internal readonly PlrTicks Ticks = new PlrTicks();
        internal readonly PlrPhysics Physics = new PlrPhysics();
        internal readonly PlrCollisions Collisions = new PlrCollisions();
        internal readonly PlrTargets Targets = new PlrTargets();
        internal readonly PlrInput Input = new PlrInput();
        internal readonly SimulationRules Rules = new SimulationRules();
        internal readonly SimulationPipeline Pipeline = new SimulationPipeline();
        internal readonly SimulationCommandBuffer Buffer = new SimulationCommandBuffer();
        internal readonly PlrRecorder Commands;
        internal readonly PrefabCatalog Catalog;
        internal readonly EntityLifecycle Lifecycle;
        internal readonly CollisionEventRouter Router;
        internal readonly GameObject Host;
        internal readonly PassiveInventory Passives = new PassiveInventory();
        internal readonly PlayerHealth Health = new PlayerHealth();
        internal readonly PlayerEntity Player;
        internal readonly PlayerMovement Movement;
        internal readonly WeaponInventory Weapons;
        internal readonly PlayerSpawnCommandHandler SpawnHandler;
        internal readonly PlayerHitHandler HitHandler;
        internal readonly WeaponDamageResolver DamageResolver;
        internal readonly PlayerDamageReceiver Receiver;
        internal readonly PlayerProgressionApplier Applier;
        internal readonly EvolutionService Evolution;
        internal readonly ChestEffectResolver Chests;
        internal readonly PlrChestProbe ChestProbe;
        internal readonly DeterministicRng ChestRng = new RngStreams(20261107UL).Get(RngStream.Chest);
        internal readonly PlrHook BeginHook = new PlrHook();
        internal readonly PlrHook LifecycleHook = new PlrHook();
        internal readonly PlrHook ResolveHook = new PlrHook();
        internal readonly PlrHook GateHook = new PlrHook();
        internal readonly Dictionary<EntityHandle, PlrMonster> Monsters = new Dictionary<EntityHandle, PlrMonster>();
        internal GameData Data;
        private int _nextFakeIndex = 1000;

        internal PlrRig(GameData data = null, bool startRun = true, Vector2 start = default)
        {
            Data = data ?? PlrTestData.Real();
            Commands = new PlrRecorder(Buffer, Clock);
            Catalog = new PrefabCatalog(new Dictionary<EntityCategory, Type>
            {
                { EntityCategory.PlayerProjectile, typeof(PlayerProjectile) },
                { EntityCategory.PlayerOrb, typeof(OrbitOrb) },
                { EntityCategory.PlayerZone, typeof(DamageZone) },
                // Stand-ins: the real Monster/EnemyProjectile types belong to IMP-MONSTER; only their ids and Despawned matter here.
                { EntityCategory.Monster, typeof(DamageZone) },
                { EntityCategory.EnemyProjectile, typeof(DamageZone) },
            }, new IEntityTemplateDecorator[0]);
            Lifecycle = new EntityLifecycle(Pool, Ids, Ticks, Collisions, Physics, Catalog, Clock);
            Router = new CollisionEventRouter(Collisions);
            Host = new GameObject("PlrHost");
            Func<GameData> current = () => Data;
            Player = new PlayerEntity(Ids, Physics, current, Passives, Host);
            Movement = new PlayerMovement(Player, Input, Passives, current);
            Weapons = new WeaponInventory(Player, Passives, Lifecycle);
            SpawnHandler = new PlayerSpawnCommandHandler(Lifecycle, Weapons);
            Func<EntityHandle, IDamageable> damageables = id => Monsters.TryGetValue(id, out PlrMonster m) ? m : null;
            Func<EntityHandle, IContactDamageSource> sources = id => Monsters.TryGetValue(id, out PlrMonster m) ? m : null;
            HitHandler = new PlayerHitHandler(Lifecycle, Weapons, damageables);
            DamageResolver = new WeaponDamageResolver(Weapons, damageables);
            Receiver = new PlayerDamageReceiver(Health, current, sources, Lifecycle);
            Applier = new PlayerProgressionApplier(Clock, Weapons, Passives, Health, Player, current);
            Evolution = new EvolutionService(Weapons, Passives, current);
            Chests = new ChestEffectResolver(Evolution, Applier, Weapons);
            ChestProbe = new PlrChestProbe(() => Chests, ChestRng);

            Buffer.RegisterHandler(SimulationCommandKind.Despawn, new EntityDespawnCommandHandler(Lifecycle));
            Buffer.RegisterHandler(SimulationCommandKind.SpawnPlayerProjectile, SpawnHandler);
            Buffer.RegisterHandler(SimulationCommandKind.SpawnPlayerOrb, SpawnHandler);
            Buffer.RegisterHandler(SimulationCommandKind.SpawnPlayerZone, SpawnHandler);
            Router.SetHandler(CollisionChannel.PlayerProjectileVsMonster, HitHandler);
            Router.SetHandler(CollisionChannel.PlayerOrbVsMonster, HitHandler);
            Router.SetHandler(CollisionChannel.PlayerZoneVsMonster, HitHandler);
            Router.SetHandler(CollisionChannel.PlayerBodyVsMonster, Receiver);
            Router.SetHandler(CollisionChannel.EnemyProjectileVsPlayerBody, Receiver);
            Router.SetHandler(CollisionChannel.EnemyZoneVsPlayerBody, Receiver);
            Router.SetHandler(CollisionChannel.PlayerBodyVsChest, ChestProbe);

            Pipeline.AddHandler(SimulationPhase.Begin, 0, BeginHook);
            Pipeline.AddHandler(SimulationPhase.Begin, 10, Movement);
            Pipeline.AddHandler(SimulationPhase.PlayerMove, 0, Movement);
            Pipeline.AddHandler(SimulationPhase.WeaponTick, 0, Weapons);
            Pipeline.AddHandler(SimulationPhase.Collision, 0, Router);
            Pipeline.AddHandler(SimulationPhase.Resolve, 0, Router);
            Pipeline.AddHandler(SimulationPhase.Resolve, 5, ResolveHook);
            Pipeline.AddHandler(SimulationPhase.Resolve, 10, DamageResolver);
            Pipeline.AddHandler(SimulationPhase.Resolve, 20, Receiver);
            Pipeline.AddHandler(SimulationPhase.Lifecycle, 0, Buffer);
            Pipeline.AddHandler(SimulationPhase.Lifecycle, 10, new LifetimeSystem(Lifecycle));
            Pipeline.AddHandler(SimulationPhase.Lifecycle, 50, LifecycleHook);
            Pipeline.AddHandler(SimulationPhase.Lifecycle, 90, GateHook);

            Player.RegisterForRun(Collisions, start);
            if (startRun)
            {
                Applier.ResetForRun();
            }

            Clock.Open();
        }

        internal EntityHandle PlayerId => Player.Body.Id;

        internal StepContext Context()
        {
            return new StepContext(Clock.Step, Player.Position, Rules, Targets, Commands);
        }

        // Monster stand-in outside the lifecycle (id 1000+:1); targetable and damageable through the lookups.
        internal PlrMonster AddMonster(Vector2 position, float hp = 1000f, float contactDamage = 10f)
        {
            var monster = new PlrMonster(new EntityHandle(_nextFakeIndex++, 1), position, hp, contactDamage);
            Monsters.Add(monster.Id, monster);
            Targets.Monsters.Add(monster);
            return monster;
        }

        // Spawns a lifecycle entity of category (stand-in type) in this step's Lifecycle; the returned box is filled then.
        internal PlrMonster[] SpawnLifecycleMonster(EntityCategory category, Vector2 position, float hp = 1000f, float contactDamage = 10f,
                                                    bool targetable = true)
        {
            var box = new PlrMonster[1];
            LifecycleHook.Pending.Add(ctx =>
            {
                DamageZone entity = Lifecycle.Spawn<DamageZone>(category, position, 0, null);
                var monster = new PlrMonster(entity.Id, position, hp, contactDamage);
                Monsters.Add(entity.Id, monster);
                if (targetable)
                {
                    Targets.Monsters.Add(monster);
                }

                box[0] = monster;
            });
            return box;
        }

        internal void DespawnInLifecycle(EntityHandle id, DespawnReason reason)
        {
            LifecycleHook.Pending.Add(ctx => Lifecycle.Despawn(id, reason));
        }

        internal void Step()
        {
            if (!Clock.TryBeginStep(long.MaxValue))
            {
                throw new InvalidOperationException("PlrRig.Step: the gate is closed (" + Clock.CloseReason + ").");
            }

            long step = Clock.Step;
            var before = new StepContext(step, Player.Position, Rules, Targets, Commands);
            Clock.SetContext(before);
            Run(SimulationPhase.Begin, before);
            Run(SimulationPhase.PlayerMove, before);
            var context = new StepContext(step, Player.Position, Rules, Targets, Commands);
            Clock.SetContext(context);
            Run(SimulationPhase.MonsterTick, context);
            Run(SimulationPhase.BuildIndex, context);
            Run(SimulationPhase.WeaponTick, context);
            Clock.SetPhase(SimulationPhase.ProjectileTick);
            Ticks.RunGroup(TickGroup.Projectiles, context);
            Pipeline.RunPhase(SimulationPhase.ProjectileTick, context);
            Run(SimulationPhase.Collision, context);
            Run(SimulationPhase.Resolve, context);
            Run(SimulationPhase.Lifecycle, context);
            Clock.EndStep(long.MaxValue);
        }

        internal void Steps(int count)
        {
            for (int i = 0; i < count; i++)
            {
                Step();
            }
        }

        // Closes the gate for a level-up inside the next step (StepOutcomeGate stand-in), so ApplyLevelUpOption becomes legal.
        internal void StepAndCloseForLevelUp()
        {
            GateHook.Pending.Add(ctx => Clock.CloseAfterCurrentStep(GateCloseReason.LevelUp));
            Step();
        }

        internal List<T> Live<T>() where T : SimEntity
        {
            var result = new List<T>();
            IReadOnlyList<SimEntity> active = Lifecycle.ActiveEntities;
            for (int i = 0; i < active.Count; i++)
            {
                if (active[i] is T t && active[i].Category != EntityCategory.Monster && active[i].Category != EntityCategory.EnemyProjectile)
                {
                    result.Add(t);
                }
            }

            result.Sort((a, b) => a.Id.CompareTo(b.Id));
            return result;
        }

        internal void Destroy()
        {
            Pool.DestroyAll();
            foreach (EntityCategory category in new[]
                     {
                         EntityCategory.PlayerProjectile, EntityCategory.PlayerOrb, EntityCategory.PlayerZone,
                         EntityCategory.Monster, EntityCategory.EnemyProjectile,
                     })
            {
                Object.DestroyImmediate(Catalog.GetTemplate(category));
            }

            Object.DestroyImmediate(Host);
        }

        private void Run(SimulationPhase phase, in StepContext context)
        {
            Clock.SetPhase(phase);
            Pipeline.RunPhase(phase, context);
        }
    }
}
