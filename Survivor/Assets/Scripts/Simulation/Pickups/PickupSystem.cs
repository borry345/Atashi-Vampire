using System;
using System.Collections.Generic;
using Survivor.Core;
using Survivor.Data;

namespace Survivor.Simulation
{
    /// Gem attraction and gem / chest pickup (D16). Channel 6 Enter starts attraction, channel 7 Enter collects a gem
    /// (addXp), channel 8 Enter opens a chest (openChest with the Chest stream); Exit events are ignored and consumed entities'
    /// later events are dropped. WeaponTick order 10 moves attracted gems toward the player in ascending Id order.
    /// The player side is reached only through the two constructor delegates (5c.3 #13).
    public sealed class PickupSystem : IStepPhaseHandler, ICollisionEventHandler
    {
        private readonly EntityLifecycle _lifecycle;
        private readonly GameDataRegistry _data;
        private readonly RngStreams _rng;
        private readonly Action<int> _addXp;
        private readonly Action<DeterministicRng, StepContext> _openChest;
        private readonly List<EntityHandle> _attractedIds = new List<EntityHandle>();
        private readonly List<ExperienceGem> _attracted = new List<ExperienceGem>();

        /// Creates the system; addXp receives a collected gem's experience, openChest resolves a chest effect.
        /// Null arguments throw ArgumentNullException.
        public PickupSystem(EntityLifecycle lifecycle, GameDataRegistry data, RngStreams rng, Action<int> addXp,
                            Action<DeterministicRng, StepContext> openChest)
        {
            _lifecycle = lifecycle ?? throw new ArgumentNullException(nameof(lifecycle));
            _data = data ?? throw new ArgumentNullException(nameof(data));
            _rng = rng ?? throw new ArgumentNullException(nameof(rng));
            _addXp = addXp ?? throw new ArgumentNullException(nameof(addXp));
            _openChest = openChest ?? throw new ArgumentNullException(nameof(openChest));
            _lifecycle.Despawning += OnDespawning;
        }

        /// Gems currently being pulled toward the player.
        public int AttractedGemCount => _attracted.Count;

        /// Run reset: forgets every attracted gem.
        public void Clear()
        {
            _attractedIds.Clear();
            _attracted.Clear();
        }

        /// Channels 6, 7 and 8 (Enter only). Throws InvalidOperationException for any other channel (wiring error).
        public void Handle(in CollisionEvent e, in StepContext context)
        {
            if (e.Channel != CollisionChannel.PlayerMagnetVsGem && e.Channel != CollisionChannel.PlayerBodyVsGem
                && e.Channel != CollisionChannel.PlayerBodyVsChest)
            {
                throw new InvalidOperationException("PickupSystem does not handle channel " + e.Channel + ".");
            }

            if (e.Kind != CollisionEventKind.Enter || !_lifecycle.TryGet(e.B, out SimEntity entity) || entity.IsConsumed)
            {
                return;
            }

            if (e.Channel == CollisionChannel.PlayerBodyVsChest)
            {
                if (entity is Chest)
                {
                    _openChest(_rng.Get(RngStream.Chest), context);
                    Consume(entity, context);
                }

                return;
            }

            if (!(entity is ExperienceGem gem))
            {
                return;
            }

            if (e.Channel == CollisionChannel.PlayerMagnetVsGem)
            {
                Attract(gem);
            }
            else
            {
                _addXp(gem.XpValue);
                Consume(gem, context);
            }
        }

        /// WeaponTick: moves each attracted gem GemAttractSpeedPerStep toward the player, landing on it instead of overshooting.
        public void OnPhase(SimulationPhase phase, in StepContext context)
        {
            if (phase != SimulationPhase.WeaponTick || _attracted.Count == 0)
            {
                return;
            }

            float speed = _data.Current.PlayerStats.Pickups.GemAttractSpeedPerStep;
            for (int i = 0; i < _attracted.Count; i++)
            {
                ExperienceGem gem = _attracted[i];
                gem.MoveTo(StepMove.Toward(gem.Position, context.PlayerPosition, speed));
            }
        }

        private void Attract(ExperienceGem gem)
        {
            if (gem.IsAttracted)
            {
                return;
            }

            gem.IsAttracted = true;
            int index = ~_attractedIds.BinarySearch(gem.Id);
            _attractedIds.Insert(index, gem.Id);
            _attracted.Insert(index, gem);
        }

        private static void Consume(SimEntity entity, in StepContext context)
        {
            entity.MarkConsumed();
            context.Commands.Enqueue(new SimulationCommand(SimulationCommandKind.Despawn, entity.Id, target: entity.Id,
                reason: DespawnReason.Consumed));
        }

        private void OnDespawning(SimEntity entity)
        {
            if (entity.Category != EntityCategory.Gem)
            {
                return;
            }

            int index = _attractedIds.BinarySearch(entity.Id);
            if (index >= 0)
            {
                _attractedIds.RemoveAt(index);
                _attracted.RemoveAt(index);
            }
        }
    }
}
