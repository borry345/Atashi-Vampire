namespace Survivor.Core
{
    /// Per-step logic for monsters and projectiles only (F17 scope).
    public interface IUpdatable
    {
        /// Entity id of the owner.
        EntityId Id { get; }

        /// Tick group the owner belongs to.
        TickGroup Group { get; }

        /// Writes own state only; side effects via context.Commands.
        void Tick(in StepContext context);
    }
}
