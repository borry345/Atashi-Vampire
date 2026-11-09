using System;
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using Survivor.App;
using Survivor.Core;
using Survivor.Simulation;
using Survivor.Tests.PlayMode.Collision;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;
#if UNITY_EDITOR
using UnityEditor.SceneManagement;
#endif

namespace Survivor.Tests.PlayMode.Flow
{
    /// Base of the flow tests: loads Assets/Scenes/Main.unity by path (no build list needed), isolates the best record under a
    /// test key, and afterwards unloads the scene and restores Physics2D / time globals so later fixtures are untouched.
    public abstract class FlowPmFixture
    {
        protected const string ScenePath = "Assets/Scenes/Main.unity";
        protected const string RecordKey = "BestSurvivalSteps.FlowTest";
        protected const int MaxFrames = 20000;

        private static int _emptySceneCounter;
        private Physics2DGlobalsScope _physics;
        private float _fixedDeltaTime;
        private float _maximumDeltaTime;

        protected GameBootstrap Host { get; private set; }

        protected GameFlowController Flow => Host.Flow;

        protected SimulationClock Clock => Host.Clock;

        [SetUp]
        public void SaveGlobals()
        {
            _physics = new Physics2DGlobalsScope();
            _fixedDeltaTime = Time.fixedDeltaTime;
            _maximumDeltaTime = Time.maximumDeltaTime;
            // Batch frames are far shorter than a step; a fixed 1/60 frame gives about one step per frame so frame budgets hold.
            Time.captureDeltaTime = 1f / 60f;
            PlayerPrefs.DeleteKey(RecordKey);
            GameBootstrap.BestRecordKeyOverride = RecordKey;
            GameBootstrap.DataRootOverride = null;
        }

        [UnityTearDown]
        public IEnumerator UnloadAndRestore()
        {
            Time.captureDeltaTime = 0f;
            GameBootstrap.BestRecordKeyOverride = null;
            GameBootstrap.DataRootOverride = null;
            Host = null;
            Scene empty = SceneManager.CreateScene("FlowPmEmpty" + (++_emptySceneCounter));
            SceneManager.SetActiveScene(empty);
            var others = new List<Scene>();
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                Scene scene = SceneManager.GetSceneAt(i);
                if (scene != empty && scene.isLoaded)
                {
                    others.Add(scene);
                }
            }

            foreach (Scene scene in others)
            {
                AsyncOperation unload = SceneManager.UnloadSceneAsync(scene);
                while (unload != null && !unload.isDone)
                {
                    yield return null;
                }
            }

            PlayerPrefs.DeleteKey(RecordKey);
            Assert.AreEqual(0, Object.FindObjectsByType<GameBootstrap>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length,
                "Main.unity unloaded");
            _physics.Restore();
            Time.fixedDeltaTime = _fixedDeltaTime;
            Time.maximumDeltaTime = _maximumDeltaTime;
        }

        /// Loads Main.unity (single) and waits one frame after GameBootstrap.Awake.
        protected IEnumerator LoadMain()
        {
#if UNITY_EDITOR
            AsyncOperation load = EditorSceneManager.LoadSceneAsyncInPlayMode(ScenePath, new LoadSceneParameters(LoadSceneMode.Single));
            while (!load.isDone)
            {
                yield return null;
            }
#else
            Assert.Ignore("Main.unity is loaded by path only in the editor.");
            yield break;
#endif
            GameBootstrap[] hosts = Object.FindObjectsByType<GameBootstrap>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            Assert.AreEqual(1, hosts.Length, "Main.unity has exactly one GameBootstrap");
            Host = hosts[0];
            yield return null;
        }

        /// Yields frames until condition holds; fails after MaxFrames.
        protected static IEnumerator WaitFor(Func<bool> condition, string what, int maxFrames = MaxFrames)
        {
            int frames = 0;
            while (!condition())
            {
                if (++frames > maxFrames)
                {
                    Assert.Fail("Timed out after " + maxFrames + " frames waiting for: " + what);
                }

                yield return null;
            }
        }

        /// Yields count frames.
        protected static IEnumerator Frames(int count)
        {
            for (int i = 0; i < count; i++)
            {
                yield return null;
            }
        }

        /// Common deterministic run settings: fixed seed and the scripted input.
        protected void UseScript(ulong seed, IInputSource input, ISpawnPolicy policy = null)
        {
            Host.FixedSeed = seed;
            Host.InputOverride = input;
            Host.SpawnPolicyOverride = policy;
        }

        /// Live entities of type T in ascending id order.
        protected List<T> Live<T>() where T : SimEntity
        {
            var list = new List<T>();
            foreach (SimEntity entity in Host.Lifecycle.ActiveEntities)
            {
                if (entity is T typed)
                {
                    list.Add(typed);
                }
            }

            list.Sort((a, b) => a.Id.CompareTo(b.Id));
            return list;
        }

        /// Plays the flow the way a player would until the clock closes with StopAtStep: every level-up round is answered with
        /// option 0 one frame after it opened; pauseAtSteps pause once at the first frame whose step reaches each value and
        /// resume after pauseFrames frames. Records the step of every opened round (rounds) and how many pauses happened.
        protected IEnumerator PlayUntilStop(long[] pauseAtSteps, int pauseFrames, List<long> rounds, Action<int> pauses)
        {
            int nextPause = 0;
            int paused = 0;
            int frames = 0;
            while (!(Clock.CloseReason == GateCloseReason.StopAtStep && !Clock.IsRunning))
            {
                if (++frames > MaxFrames)
                {
                    Assert.Fail("run did not reach StopAtStep (step " + Clock.Step + ", state " + Flow.State + ")");
                }

                if (Flow.State == FlowState.LevelUp)
                {
                    long round = Host.Session.LevelUpSession.RoundId;
                    rounds.Add(Clock.Step);
                    if (rounds.Count == 1)
                    {
                        // ESC on the first level-up screen: paused with the round kept, then back to it (TAC-F5-7).
                        Flow.TogglePause();
                        Assert.AreEqual(FlowState.Paused, Flow.State);
                        yield return Frames(pauseFrames);
                        Flow.TogglePause();
                        Assert.AreEqual(FlowState.LevelUp, Flow.State);
                        Assert.AreEqual(round, Host.Session.LevelUpSession.RoundId);
                        paused++;
                    }

                    yield return null;
                    Flow.LevelUp.Choose(0);
                    LevelUpSession session = Host.Session.LevelUpSession;
                    Assert.IsTrue(session.IsOpen ? session.RoundId == round + 1 : Flow.State == FlowState.Playing,
                        "a chosen round either opens the next round in the same call or resumes play");
                    continue;
                }

                if (Flow.State == FlowState.Playing && pauseAtSteps != null && nextPause < pauseAtSteps.Length
                    && Clock.Step >= pauseAtSteps[nextPause])
                {
                    nextPause++;
                    Flow.TogglePause();
                    Assert.AreEqual(FlowState.Paused, Flow.State);
                    long stepAtPause = Clock.Step;
                    yield return Frames(pauseFrames);
                    Assert.AreEqual(stepAtPause, Clock.Step, "no step runs while paused (TAC-0-5)");
                    Flow.TogglePause();
                    paused++;
                    continue;
                }

                Assert.AreNotEqual(FlowState.Result, Flow.State, "the scripted run must not end");
                Assert.AreNotEqual(FlowState.DataError, Flow.State, "no step may abort");
                yield return null;
            }

            pauses?.Invoke(paused);
        }
    }
}
