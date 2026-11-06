using System;
using System.Collections.Generic;
using Survivor.Core;
using Survivor.Simulation;
using Survivor.Strategies;
using Survivor.Strategies.Pool;
using Survivor.Strategies.Tick;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Survivor.Tests.PlayMode.Loop
{
    // Test-only state hash (L1): FNV-1a 64 over step, player position and every live entity (ascending Id: index, generation,
    // position bits). Floats are hashed by bit pattern (5.16). Moves to Simulation as SimulationStateHash in wave 4.
    internal static class LoopPmStateHash
    {
        private const ulong OffsetBasis = 14695981039346656037UL;
        private const ulong Prime = 1099511628211UL;

        internal static ulong Compute(long step, Vector2 player, EntityLifecycle lifecycle)
        {
            var entities = new List<SimEntity>(lifecycle.ActiveEntities);
            entities.Sort((a, b) => a.Id.CompareTo(b.Id));
            ulong hash = OffsetBasis;
            hash = AddLong(hash, step);
            hash = AddFloat(hash, player.x);
            hash = AddFloat(hash, player.y);
            for (int i = 0; i < entities.Count; i++)
            {
                hash = AddInt(hash, entities[i].Id.Index);
                hash = AddInt(hash, entities[i].Id.Generation);
                hash = AddInt(hash, (int)entities[i].Category);
                hash = AddFloat(hash, entities[i].Position.x);
                hash = AddFloat(hash, entities[i].Position.y);
            }

            return hash;
        }

        private static ulong AddFloat(ulong hash, float value)
        {
            return AddInt(hash, BitConverter.ToInt32(BitConverter.GetBytes(value), 0));
        }

        private static ulong AddLong(ulong hash, long value)
        {
            hash = AddInt(hash, (int)value);
            return AddInt(hash, (int)(value >> 32));
        }

        private static ulong AddInt(ulong hash, int value)
        {
            for (int i = 0; i < 4; i++)
            {
                hash ^= (byte)(value >> (8 * i));
                hash *= Prime;
            }

            return hash;
        }
    }

    // PlayerMove 0 stand-in for PlayerMovement: a deterministic scripted path (the drivers read Position through Func<Vector2>).
    internal sealed class LoopPmPlayer : IStepPhaseHandler
    {
        internal Vector2 Position;

        public void OnPhase(SimulationPhase phase, in StepContext context)
        {
            // Turns every 120 steps through the four axis directions at 0.04 units per step.
            int leg = (int)(context.Step / 120 % 4);
            Vector2 direction = leg == 0 ? Vector2.right : leg == 1 ? Vector2.up : leg == 2 ? Vector2.left : Vector2.down;
            Position += direction * 0.04f;
        }
    }

    // WeaponTick 0 stand-in: every 13 steps two monsters around the player, every 7 steps a projectile toward the nearest monster.
    internal sealed class LoopPmSpawnRequests : IStepPhaseHandler
    {
        private readonly LoopPmPlayer _player;

        internal LoopPmSpawnRequests(LoopPmPlayer player)
        {
            _player = player;
        }

        public void OnPhase(SimulationPhase phase, in StepContext context)
        {
            if (context.Step % 13 == 0)
            {
                context.Commands.Enqueue(new SimulationCommand(SimulationCommandKind.SpawnMonsters, EntityHandle.None,
                    position: context.PlayerPosition, radius: 6f, count: 2));
            }

            if (context.Step % 7 == 3 && context.Targets.TryFindNearest(context.PlayerPosition, out TargetHit hit))
            {
                context.Commands.Enqueue(new SimulationCommand(SimulationCommandKind.SpawnPlayerProjectile, EntityHandle.None,
                    position: _player.Position, direction: MoveMath.DirectionOrDefault(hit.Position - _player.Position)));
            }
        }
    }

    // Lifecycle handlers of the scenario's spawn commands (stand-ins for MONSTER/PLAYER handlers).
    internal sealed class LoopPmSpawnHandler : ISimulationCommandHandler
    {
        private readonly EntityLifecycle _lifecycle;
        private readonly DeterministicRng _rng;

        internal LoopPmSpawnHandler(EntityLifecycle lifecycle, DeterministicRng rng)
        {
            _lifecycle = lifecycle;
            _rng = rng;
        }

        public void Handle(in SimulationCommand command, in StepContext context)
        {
            if (command.Kind == SimulationCommandKind.SpawnMonsters)
            {
                for (int i = 0; i < command.Count; i++)
                {
                    Vector2 point = SpawnGeometry.PointOnCircle(command.Position, command.Radius, _rng.NextFloat01());
                    _lifecycle.Spawn<LoopPmMover>(EntityCategory.Monster, point, 0, m =>
                    {
                        m.Launch(Vector2.zero);
                        m.Body.Radius = 0.4f;
                    });
                }

                return;
            }

            Vector2 direction = command.Direction;
            _lifecycle.Spawn<LoopPmMover>(EntityCategory.PlayerProjectile, command.Position, 40, p =>
            {
                p.Launch(direction);
                p.Body.Radius = 0.2f;
            });
        }
    }

    // Records every phase it is registered for, with the step and whether the context was already final.
    internal sealed class LoopPmPhaseRecorder : IStepPhaseHandler
    {
        internal readonly List<string> Log = new List<string>();

        public void OnPhase(SimulationPhase phase, in StepContext context)
        {
            Log.Add(phase + "#" + context.Step);
        }
    }

    // Throws once in the given phase of the given step.
    internal sealed class LoopPmThrowOnce : IStepPhaseHandler
    {
        private readonly long _step;
        internal bool Thrown;

        internal LoopPmThrowOnce(long step)
        {
            _step = step;
        }

        public void OnPhase(SimulationPhase phase, in StepContext context)
        {
            if (!Thrown && context.Step == _step)
            {
                Thrown = true;
                throw new InvalidOperationException("scripted failure");
            }
        }
    }

    // Lifecycle order 90 stand-in for StepOutcomeGate: closes the gate inside step `step` with `reason`.
    internal sealed class LoopPmCloseAt : IStepPhaseHandler
    {
        private readonly SimulationClock _clock;
        private readonly long _step;
        private readonly GateCloseReason _reason;

        internal LoopPmCloseAt(SimulationClock clock, long step, GateCloseReason reason)
        {
            _clock = clock;
            _step = step;
            _reason = reason;
        }

        public void OnPhase(SimulationPhase phase, in StepContext context)
        {
            if (context.Step == _step)
            {
                _clock.CloseAfterCurrentStep(_reason);
            }
        }
    }

    // Full loop assembled the way the composition root will: real pool A, real target provider A, both tick strategies,
    // the three drivers on one GameObject. Collision is a no-event double (COLLISION is wave 3).
    internal sealed class LoopPmRig
    {
        internal const string ObjectName = "LoopPmRig";

        internal readonly SimulationClock Clock = new SimulationClock();
        internal readonly SimulationPipeline Pipeline = new SimulationPipeline();
        internal readonly SimulationRules Rules = new SimulationRules();
        internal readonly SimulationCommandBuffer Commands = new SimulationCommandBuffer();
        internal readonly MonsterSnapshotBuffer Snapshot = new MonsterSnapshotBuffer();
        internal readonly EntityHandleAllocator Ids = new EntityHandleAllocator();
        internal readonly LoopPmPlayer Player = new LoopPmPlayer();
        internal readonly TargetProviderSlot Targets;
        internal readonly PrefabCatalog Catalog;
        internal readonly CustomPoolStrategy Pool = new CustomPoolStrategy();
        internal readonly ITickStrategy Ticks;
        internal readonly LoopPmCollisions Collisions = new LoopPmCollisions(null);
        internal readonly EntityLifecycle Lifecycle;
        internal readonly MonsterRegistry Registry;
        internal readonly CollisionEventRouter Router;
        internal readonly GameObject Host;
        internal readonly SimulationFrameCounter FrameCounter;

        internal LoopPmRig(TickStrategyKind tick, bool withScenario = true)
        {
            Targets = new TargetProviderSlot(Clock);
            Targets.Replace(new BruteForceTargetProvider());
            Ticks = tick == TickStrategyKind.CentralTick ? (ITickStrategy)new CentralTickStrategy() : new SelfTickStrategy(Clock);
            var types = new Dictionary<EntityCategory, Type>();
            foreach (EntityCategory category in Enum.GetValues(typeof(EntityCategory)))
            {
                types.Add(category, typeof(LoopPmMover));
            }

            Catalog = new PrefabCatalog(types, new IEntityTemplateDecorator[] { new PoolTemplateDecorator(), new SelfTickTemplateDecorator() });
            Lifecycle = new EntityLifecycle(Pool, Ids, Ticks, Collisions, new LoopPmPhysics(null), Catalog, Clock);
            Registry = new MonsterRegistry(Lifecycle, Snapshot);
            Router = new CollisionEventRouter(Collisions);
            Commands.RegisterHandler(SimulationCommandKind.Despawn, new EntityDespawnCommandHandler(Lifecycle));

            Pipeline.AddHandler(SimulationPhase.BuildIndex, 0, Registry);
            Pipeline.AddHandler(SimulationPhase.Collision, 0, Router);
            Pipeline.AddHandler(SimulationPhase.Resolve, 0, Router);
            Pipeline.AddHandler(SimulationPhase.Lifecycle, 0, Commands);
            Pipeline.AddHandler(SimulationPhase.Lifecycle, 10, new LifetimeSystem(Lifecycle));
            if (withScenario)
            {
                var spawns = new LoopPmSpawnHandler(Lifecycle, new RngStreams(20261106UL).Get(RngStream.Spawn));
                Commands.RegisterHandler(SimulationCommandKind.SpawnMonsters, spawns);
                Commands.RegisterHandler(SimulationCommandKind.SpawnPlayerProjectile, spawns);
                Pipeline.AddHandler(SimulationPhase.PlayerMove, 0, Player);
                Pipeline.AddHandler(SimulationPhase.WeaponTick, 0, new LoopPmSpawnRequests(Player));
            }

            Host = new GameObject(ObjectName);
            FrameCounter = Host.AddComponent<SimulationFrameCounter>();
            FrameCounter.Initialize(Clock);
            Host.AddComponent<SimulationDriverEarly>().Initialize(Clock, Pipeline, Rules, Targets, Commands, () => Lifecycle.Ticks, () => Player.Position);
            Host.AddComponent<SimulationDriverMid>().Initialize(Clock, Pipeline, () => Lifecycle.Ticks, Snapshot, Targets);
            Host.AddComponent<SimulationDriverLate>().Initialize(Clock, Pipeline, Rules);
            Host.AddComponent<PresentationSync>().Initialize(Lifecycle);
        }

        internal ulong Hash()
        {
            return LoopPmStateHash.Compute(Clock.Step, Player.Position, Lifecycle);
        }

        // Ends the run the way RunSession.Reset does (steps 1-2), then destroys every object the rig and its pool created.
        internal void Dispose()
        {
            if (!Clock.StepActive)
            {
                Lifecycle.DespawnAll(DespawnReason.RunReset);
            }

            Ticks.Clear();
            Object.DestroyImmediate(Host);
            LoopPmWorld.DestroyTestObjects();
        }
    }
}
