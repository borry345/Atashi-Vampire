using System;
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using Survivor.App;
using Survivor.Core;
using Survivor.Dev;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;
#if UNITY_EDITOR
using UnityEditor.SceneManagement;
#endif

namespace Survivor.Tests.Dev
{
    /// Minimal copy of FlowPmFixture (other assembly): loads Main.unity by path, fixes the frame at 1/60, and afterwards
    /// detaches dev mode, unloads the scene and restores the Physics2D / time globals that GameBootstrap or dev mode change.
    public abstract class DevPmFixture
    {
        protected const string ScenePath = "Assets/Scenes/Main.unity";
        protected const int MaxFrames = 20000;
        private const int FirstLayer = CollisionLayerMapping.FirstUnityLayer;
        private const int LayerCount = 10;

        private static int _emptySceneCounter;
        private readonly bool[,] _ignore = new bool[LayerCount, 32];
        private SimulationMode2D _simulationMode;
        private bool _callbacksOnDisable, _queriesHitTriggers, _autoSyncTransforms;
        private float _contactOffset, _fixedDeltaTime, _maximumDeltaTime, _timeScale;

        protected GameBootstrap Host { get; private set; }

        [SetUp]
        public void SaveGlobals()
        {
            _simulationMode = Physics2D.simulationMode;
            _callbacksOnDisable = Physics2D.callbacksOnDisable;
            _queriesHitTriggers = Physics2D.queriesHitTriggers;
            _autoSyncTransforms = Physics2D.autoSyncTransforms;
            _contactOffset = Physics2D.defaultContactOffset;
            for (int i = 0; i < LayerCount; i++)
            {
                for (int other = 0; other < 32; other++)
                {
                    _ignore[i, other] = Physics2D.GetIgnoreLayerCollision(FirstLayer + i, other);
                }
            }

            _fixedDeltaTime = Time.fixedDeltaTime;
            _maximumDeltaTime = Time.maximumDeltaTime;
            _timeScale = Time.timeScale;
            Time.captureDeltaTime = 1f / 60f;
        }

        [UnityTearDown]
        public IEnumerator UnloadAndRestore()
        {
            DevModeEntry.Detach();
            Time.captureDeltaTime = 0f;
            Host = null;
            Scene empty = SceneManager.CreateScene("DevPmEmpty" + (++_emptySceneCounter));
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

            Assert.AreEqual(0, Object.FindObjectsByType<DevModeRoot>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length, "dev root gone");
            for (int i = 0; i < LayerCount; i++)
            {
                for (int other = 0; other < 32; other++)
                {
                    Physics2D.IgnoreLayerCollision(FirstLayer + i, other, _ignore[i, other]);
                }
            }

            Physics2D.defaultContactOffset = _contactOffset;
            Physics2D.autoSyncTransforms = _autoSyncTransforms;
            Physics2D.queriesHitTriggers = _queriesHitTriggers;
            Physics2D.callbacksOnDisable = _callbacksOnDisable;
            Physics2D.simulationMode = _simulationMode;
            Time.fixedDeltaTime = _fixedDeltaTime;
            Time.maximumDeltaTime = _maximumDeltaTime;
            Time.timeScale = _timeScale;
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

        /// Yields frames until condition holds; fails after maxFrames.
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
    }
}
