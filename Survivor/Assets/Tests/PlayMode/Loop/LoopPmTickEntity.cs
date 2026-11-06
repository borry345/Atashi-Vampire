using System.Collections.Generic;
using Survivor.Core;
using Survivor.Simulation;
using UnityEngine;

namespace Survivor.Tests.PlayMode.Loop
{
    // Test-only updatable entity: records ticks and releases into the world's event log.
    public sealed class LoopPmTickEntity : SimEntity, IUpdatable
    {
        internal List<string> EventLog;
        internal EntityHandleAllocator Ids;
        internal readonly List<long> TickedSteps = new List<long>();
        internal int ReleaseCount;

        public TickGroup Group => Category == EntityCategory.Monster ? TickGroup.Monsters : TickGroup.Projectiles;

        public void Tick(in StepContext context)
        {
            Debug.Assert(context.Step > SpawnStep);
            TickedSteps.Add(context.Step);
        }

        internal void SetPosition(Vector2 position)
        {
            Position = position;
        }

        protected override void OnReleased()
        {
            ReleaseCount++;
            EventLog?.Add("OnReleased live=" + (Ids != null && Ids.IsLive(Id)));
        }
    }
}
