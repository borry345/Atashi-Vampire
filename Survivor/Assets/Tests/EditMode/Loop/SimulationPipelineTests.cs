using System;
using System.Collections.Generic;
using NUnit.Framework;
using Survivor.Core;
using Survivor.Simulation;

namespace Survivor.Tests.EditMode.Loop
{
    /// Ordered phase handler registry (5.3, 5c.3 #7 and #9).
    public sealed class SimulationPipelineTests
    {
        [Test]
        public void RunPhase_CallsHandlersInAscendingOrder_RegardlessOfAddOrder()
        {
            var log = new List<string>();
            var pipeline = new SimulationPipeline();
            pipeline.AddHandler(SimulationPhase.Lifecycle, 90, new LoopEmPhaseProbe(log, "gate"));
            pipeline.AddHandler(SimulationPhase.Lifecycle, 0, new LoopEmPhaseProbe(log, "buffer"));
            pipeline.AddHandler(SimulationPhase.Lifecycle, 10, new LoopEmPhaseProbe(log, "lifetime"));
            pipeline.AddHandler(SimulationPhase.Begin, 0, new LoopEmPhaseProbe(log, "pending"));

            pipeline.RunPhase(SimulationPhase.Lifecycle, new StepContext(7, default, null, null, null));

            CollectionAssert.AreEqual(new[] { "buffer@Lifecycle#7", "lifetime@Lifecycle#7", "gate@Lifecycle#7" }, log);
            Assert.AreEqual(3, pipeline.HandlerCount(SimulationPhase.Lifecycle));
            Assert.AreEqual(0, pipeline.HandlerCount(SimulationPhase.End));
        }

        [Test]
        public void AddHandler_SamePhaseAndOrder_Throws()
        {
            var pipeline = new SimulationPipeline();
            pipeline.AddHandler(SimulationPhase.BuildIndex, 10, new LoopEmPhaseProbe(new List<string>(), "a"));
            Assert.Throws<InvalidOperationException>(() =>
                pipeline.AddHandler(SimulationPhase.BuildIndex, 10, new LoopEmPhaseProbe(new List<string>(), "b")));
            Assert.DoesNotThrow(() => pipeline.AddHandler(SimulationPhase.Resolve, 10, new LoopEmPhaseProbe(new List<string>(), "c")),
                "the same order in another phase is fine");
        }

        [Test]
        public void AddHandler_Null_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => new SimulationPipeline().AddHandler(SimulationPhase.Begin, 0, null));
        }

        [Test]
        public void RunPhase_HandlerException_Propagates()
        {
            var pipeline = new SimulationPipeline();
            pipeline.AddHandler(SimulationPhase.Resolve, 0, new ThrowingHandler());
            Assert.Throws<InvalidOperationException>(() => pipeline.RunPhase(SimulationPhase.Resolve, default));
        }

        private sealed class ThrowingHandler : IStepPhaseHandler
        {
            public void OnPhase(SimulationPhase phase, in StepContext context)
            {
                throw new InvalidOperationException("boom");
            }
        }
    }
}
