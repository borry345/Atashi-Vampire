using Survivor.Simulation;

namespace Survivor.Tests.PlayMode.Loop
{
    // Test-only entity that is not an IUpdatable (like gems or chests).
    public sealed class LoopPmPlainEntity : SimEntity
    {
        internal int ReleaseCount;

        protected override void OnReleased()
        {
            ReleaseCount++;
        }
    }
}
