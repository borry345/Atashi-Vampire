using System;
using Survivor.Core;
using Survivor.Data;

namespace Survivor.Simulation
{
    /// Lifecycle order 0 handler of SpawnGem (Position, Count = experience) and SpawnChest (Position). Radii are read from the
    /// current player stats pickups at spawn time.
    public sealed class DropCommandHandler : ISimulationCommandHandler
    {
        private readonly EntityLifecycle _lifecycle;
        private readonly GameDataRegistry _data;

        /// Creates the handler; null arguments throw ArgumentNullException.
        public DropCommandHandler(EntityLifecycle lifecycle, GameDataRegistry data)
        {
            _lifecycle = lifecycle ?? throw new ArgumentNullException(nameof(lifecycle));
            _data = data ?? throw new ArgumentNullException(nameof(data));
        }

        /// Spawns the gem or chest. Throws InvalidOperationException for a kind this handler does not own or a gem with Count < 1.
        public void Handle(in SimulationCommand command, in StepContext context)
        {
            PickupsDefinition pickups = _data.Current.PlayerStats.Pickups;
            switch (command.Kind)
            {
                case SimulationCommandKind.SpawnGem:
                    if (command.Count < 1)
                    {
                        throw new InvalidOperationException("DropCommandHandler: SpawnGem needs Count >= 1 (got " + command.Count + ").");
                    }

                    int xp = command.Count;
                    float gemRadius = pickups.GemRadius;
                    _lifecycle.Spawn<ExperienceGem>(EntityCategory.Gem, command.Position, 0, g => g.Initialize(xp, gemRadius));
                    break;
                case SimulationCommandKind.SpawnChest:
                    float chestRadius = pickups.ChestRadius;
                    _lifecycle.Spawn<Chest>(EntityCategory.Chest, command.Position, 0, c => c.Initialize(chestRadius));
                    break;
                default:
                    throw new InvalidOperationException("DropCommandHandler does not handle " + command.Kind + ".");
            }
        }
    }
}
