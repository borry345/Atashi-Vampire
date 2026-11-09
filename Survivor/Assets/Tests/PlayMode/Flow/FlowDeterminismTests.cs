using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using Survivor.Core;
using Survivor.Simulation;
using UnityEngine;
using UnityEngine.TestTools;

namespace Survivor.Tests.PlayMode.Flow
{
    /// Gate determinism (5c.5 wave 4, TAC-0-4, TAC-0-7): the same seed with level-ups, pauses and resumes mixed in gives the same
    /// SimulationStateHash and the same round steps at 30 / 60 / 144 FPS, and camera follow is exact (TAC-F1-6).
    public sealed class FlowDeterminismTests : FlowPmFixture
    {
        private const ulong Seed = 777UL;
        private const long Steps = 1800;

        [UnityTest]
        public IEnumerator SameSeed_LevelUpsAndPauses_SameHashAt30_60_144Fps()
        {
            yield return LoadMain();
            var hashes = new List<ulong>();
            var roundSteps = new List<List<long>>();
            foreach (float frame in new[] { 1f / 30f, 1f / 60f, 1f / 144f })
            {
                var rounds = new List<long>();
                int pauses = 0;
                var input = new FlowPmInput();
                input.Seek = () =>
                {
                    List<ExperienceGem> gems = Live<ExperienceGem>();
                    return gems.Count > 0 ? gems[0].Position : (Vector2?)null;
                };
                UseScript(Seed, input);
                Host.Rules.StopAtStep = Steps;
                Time.captureDeltaTime = frame;
                Flow.StartRun();
                // Two levels of stored experience at step 0 (deterministic: queued before the first step), plus natural ones.
                Host.Commands.Enqueue(PendingCommand.AddXp(13));
                yield return PlayUntilStop(new long[] { 300, 900 }, 4, rounds, p => pauses = p);
                Time.captureDeltaTime = 0f;
                Assert.AreEqual(Steps, Clock.Step);
                Assert.GreaterOrEqual(rounds.Count, 2, "level-up rounds happened");
                Assert.AreEqual(3, pauses, "one pause on the level-up screen and two while playing");
                ulong hash = SimulationStateHash.Compute(Host.Session);
                TestContext.Out.WriteLine("frame=1/" + Mathf.RoundToInt(1f / frame) + " rounds@" + string.Join(",", rounds) + " level="
                    + Host.Session.Experience.Level + " kills=" + Host.Session.KillPipeline.KillCount + " live=" + Host.Lifecycle.ActiveCount
                    + " hash=0x" + hash.ToString("X16"));
                hashes.Add(hash);
                roundSteps.Add(rounds);
                Host.Rules.StopAtStep = long.MaxValue;
            }

            Assert.AreEqual(hashes[0], hashes[1], "30 vs 60 FPS");
            Assert.AreEqual(hashes[0], hashes[2], "30 vs 144 FPS");
            CollectionAssert.AreEqual(roundSteps[0], roundSteps[1], "round steps 30 vs 60 FPS");
            CollectionAssert.AreEqual(roundSteps[0], roundSteps[2], "round steps 30 vs 144 FPS");
            Assert.AreEqual(1L, roundSteps[0][0], "the stored experience closes the gate after step 0 (s + 1)");
            Assert.AreEqual(1L, roundSteps[0][1], "the second stored level opens without a step");
        }

        [UnityTest]
        public IEnumerator Camera_SitsExactlyOnThePlayer()
        {
            yield return LoadMain();
            UseScript(Seed, new FlowPmInput(), new FlowPmNoSpawnPolicy());
            Flow.StartRun();
            yield return WaitFor(() => Clock.Step >= 200, "the player walked");
            Flow.TogglePause();
            yield return Frames(2);
            Vector2 player = Host.Player.Position;
            Assert.AreNotEqual(Vector2.zero, player);
            Vector3 camera = Host.MainCamera.transform.position;
            Assert.AreEqual(0f, (new Vector2(camera.x, camera.y) - player).magnitude, "TAC-F1-6: no interpolation or lag");
            Assert.AreEqual(-10f, camera.z);
            Assert.AreEqual(player, (Vector2)Host.PlayerHost.transform.position, "the player circle is on the simulated position");
        }
    }
}
