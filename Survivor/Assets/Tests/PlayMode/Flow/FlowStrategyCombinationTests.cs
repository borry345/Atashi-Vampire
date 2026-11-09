using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using NUnit.Framework;
using Survivor.App;
using Survivor.Core;
using Survivor.Simulation;
using UnityEngine;
using UnityEngine.TestTools;

namespace Survivor.Tests.PlayMode.Flow
{
    /// Strategy combination determinism (5c.5 wave 4): pool 3 x tick 2 x collision 2 x target 3 = 36 runs of 1,800 steps with the
    /// same seed, level-up off, invulnerable, scripted path, W1-W4 at level 5 and 200 monsters kept alive; every hash must equal
    /// the shipping-default hash. Each case starts its run with StartNewRun, so the reset path runs for every combination.
    public sealed class FlowStrategyCombinationTests : FlowPmFixture
    {
        private const ulong Seed = 4242UL;
        private const long Steps = 1800;
        private static readonly Stopwatch Total = new Stopwatch();
        private static ulong? _reference;

        // "Pool/Tick/Collision/Target", pool outermost (fewer pool replacements between consecutive cases).
        private static IEnumerable<string> Combinations()
        {
            foreach (PoolStrategyKind pool in new[] { PoolStrategyKind.CustomPool, PoolStrategyKind.InstantiateDestroy, PoolStrategyKind.UnityObjectPool })
            {
                foreach (TickStrategyKind tick in new[] { TickStrategyKind.CentralTick, TickStrategyKind.PerObjectFixedUpdate })
                {
                    foreach (CollisionStrategyKind collision in new[] { CollisionStrategyKind.SelfSquaredDistance, CollisionStrategyKind.EngineCollider2D })
                    {
                        foreach (TargetProviderKind target in new[] { TargetProviderKind.SpatialHash, TargetProviderKind.BruteForce, TargetProviderKind.Physics2DQuery })
                        {
                            yield return pool + "/" + tick + "/" + collision + "/" + target;
                        }
                    }
                }
            }
        }

        [Test]
        public void Combinations_AreAllThirtySix()
        {
            var unique = new HashSet<string>(Combinations());
            Assert.AreEqual(36, unique.Count);
        }

        [UnityTest]
        [Timeout(600000)]
        public IEnumerator Combination_HashEqualsTheShippingDefault([ValueSource(nameof(Combinations))] string combination)
        {
            string[] parts = combination.Split('/');
            var selection = new StrategySelection(
                (PoolStrategyKind)Enum.Parse(typeof(PoolStrategyKind), parts[0]),
                (TargetProviderKind)Enum.Parse(typeof(TargetProviderKind), parts[3]),
                (TickStrategyKind)Enum.Parse(typeof(TickStrategyKind), parts[1]),
                (CollisionStrategyKind)Enum.Parse(typeof(CollisionStrategyKind), parts[2]));
            yield return LoadMain();
            Total.Start();
            try
            {
                if (!_reference.HasValue)
                {
                    var referenceOut = new ulong[1];
                    yield return Run(StrategySelection.ShippingDefault, referenceOut);
                    _reference = referenceOut[0];
                    TestContext.Out.WriteLine("reference (shipping default) hash=0x" + _reference.Value.ToString("X16"));
                }

                var result = new ulong[1];
                yield return Run(selection, result);
                Assert.AreEqual(_reference.Value, result[0], combination + " differs from the shipping default");
            }
            finally
            {
                Total.Stop();
                TestContext.Out.WriteLine("combination tests total so far " + Total.Elapsed.TotalSeconds.ToString("F1") + " s");
            }
        }

        private IEnumerator Run(StrategySelection selection, ulong[] hashOut)
        {
            Stopwatch watch = Stopwatch.StartNew();
            UseScript(Seed, new FlowPmInput(), new FlowPmCrowdPolicy(200, 12, "M1", "M2", "M3"));
            Host.Rules.LevelUpEnabled = false;
            Host.Rules.PlayerTakesDamage = false;
            Host.Rules.StopAtStep = Steps;
            Host.NextRunSelection = selection;
            Time.captureDeltaTime = 0.1f;
            Host.StartNewRun();
            Assert.AreEqual(selection.Pool, Host.Lifecycle.Pool.Kind);
            Assert.AreEqual(selection.Tick, Host.Lifecycle.Ticks.Kind);
            Assert.AreEqual(selection.Collision, Host.Lifecycle.Collisions.Kind);
            Assert.AreEqual(selection.Target, Host.Targets.Kind);
            Host.Commands.Enqueue(PendingCommand.SetLoadout(new[]
            {
                new WeaponLoadoutEntry("W1", 5), new WeaponLoadoutEntry("W2", 5), new WeaponLoadoutEntry("W3", 5), new WeaponLoadoutEntry("W4", 5),
            }));
            int peak = 0;
            yield return WaitFor(() =>
            {
                peak = Mathf.Max(peak, Host.Spawner.LiveNonBossCount);
                return Clock.CloseReason == GateCloseReason.StopAtStep && !Clock.IsRunning;
            }, "1,800 steps", 5000);
            Time.captureDeltaTime = 0f;
            Assert.AreEqual(FlowState.Playing, Flow.State, "no result or error during the run");
            Assert.AreEqual(Steps, Clock.Step);
            Assert.AreEqual(4, Host.Session.Weapons.Count, "the loadout was applied");
            Assert.Greater(peak, 150, "the crowd was kept near 200");
            Assert.Greater(Host.Session.KillPipeline.KillCount, 0, "weapons killed monsters");
            hashOut[0] = SimulationStateHash.Compute(Host.Session);
            TestContext.Out.WriteLine(selection.Pool + "/" + selection.Tick + "/" + selection.Collision + "/" + selection.Target + " kills="
                + Host.Session.KillPipeline.KillCount + " live=" + Host.Lifecycle.ActiveCount + " peakMonsters=" + peak + " hp="
                + Host.Session.Health.Hp + " hash=0x" + hashOut[0].ToString("X16") + " (" + watch.Elapsed.TotalSeconds.ToString("F1") + " s)");
            Host.Rules.StopAtStep = long.MaxValue;
        }
    }
}
