using Survivor.Core;

namespace Survivor.Simulation
{
    /// Consumer of one collision channel's events, registered on CollisionEventRouter (5.9).
    public interface ICollisionEventHandler
    {
        /// Handles one event; called in Resolve order 0 in CollisionEvent.CompareTo order.
        void Handle(in CollisionEvent e, in StepContext context);
    }
}
