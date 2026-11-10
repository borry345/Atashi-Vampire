using System;
using Survivor.App;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Survivor.Dev
{
    /// Dev mode entry point (TAC-F19-1): after the first scene loads, checks the command line exactly once; without --dev it
    /// returns at once and no dev MonoBehaviour exists. With it, attaches to the scene's GameBootstrap (found by type, U40:
    /// AfterSceneLoad runs after Awake) and turns best-time recording off (SPEC F8).
    public static class DevModeEntry
    {
        private static DevModeRoot _root;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void OnAfterSceneLoad()
        {
            Run(Environment.GetCommandLineArgs());
        }

        // The single startup decision; returns the root when dev mode attached, otherwise null.
        internal static DevModeRoot Run(string[] args)
        {
            if (!DevModeActivation.IsRequested(args))
            {
                return null;
            }

            GameBootstrap host = Object.FindAnyObjectByType<GameBootstrap>();
            if (host == null)
            {
                DevLog.Warning("Dev mode requested but the scene has no GameBootstrap; not attached.");
                return null;
            }

            return Attach(host);
        }

        // Test hook and the attach path: one DevModeRoot in the host's scene (not DontDestroyOnLoad), RecordBestTime false.
        internal static DevModeRoot Attach(IDevModeHost host)
        {
            if (host == null)
            {
                throw new ArgumentNullException(nameof(host));
            }

            if (_root != null)
            {
                return _root;
            }

            var root = new GameObject("DevMode");
            _root = root.AddComponent<DevModeRoot>();
            _root.Initialize(host);
            host.Rules.RecordBestTime = false;
            DevLog.Info("Dev mode attached (F1 toggles the overlay; best time is not recorded).");
            return _root;
        }

        // Test hook: destroys the attached root (if any) so the next Attach starts fresh.
        internal static void Detach()
        {
            if (_root != null)
            {
                Object.Destroy(_root.gameObject);
            }

            _root = null;
        }
    }
}
