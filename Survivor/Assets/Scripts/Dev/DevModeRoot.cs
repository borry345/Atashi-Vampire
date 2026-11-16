using System;
using Survivor.App;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Survivor.Dev
{
    /// Exists only while dev mode is active (TAC-F19-10): owns the controller, overlay, visualizer and measurement session,
    /// toggles the overlay with F1 (the only key watched, here only), lets the controller notice run resets and drives the
    /// measurement session, whose StopAtStep end calls AutoStop (TAC-F19-3: quit, or stop playing in the editor). StopAtStep
    /// set outside a measurement is left alone (the gate just stays closed).
    public sealed class DevModeRoot : MonoBehaviour
    {
        private DevController _controller;
        private DevOverlay _overlay;
        private DevVisualizer _visualizer;
        private MeasurementSession _measurement;

        /// Action layer over the host.
        public DevController Controller => _controller;

        /// IMGUI panel (disabled while hidden).
        public DevOverlay Overlay => _overlay;

        /// GL visualization (disabled while every option is off).
        public DevVisualizer Visualizer => _visualizer;

        /// Measurement session (5.15.2).
        public MeasurementSession Measurement => _measurement;

        /// Builds the parts for host; flow is the host's screen flow when it is the GameBootstrap. Null host throws.
        public void Initialize(IDevModeHost host)
        {
            if (host == null)
            {
                throw new ArgumentNullException(nameof(host));
            }

            GameFlowController flow = host is GameBootstrap bootstrap ? bootstrap.Flow : null;
            _controller = new DevController(host, flow);
            _visualizer = gameObject.AddComponent<DevVisualizer>();
            _visualizer.Initialize(host, _controller);
            _overlay = gameObject.AddComponent<DevOverlay>();
            _overlay.Initialize(_controller, _visualizer);
            _overlay.enabled = false;
            _measurement = new MeasurementSession(_controller, _overlay, _visualizer) { AutoStop = QuitOrStopPlaying };
            _overlay.BindMeasurement(_measurement);
        }

        // TAC-F19-3 / PM default: the batch measurement ends the program; the editor leaves play mode instead.
        private static void QuitOrStopPlaying()
        {
            DevLog.Info("Measurement finished at StopAtStep; stopping.");
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        /// Shows or hides the overlay (F1); hidden = enabled false, so OnGUI and status collection stop.
        public void ToggleOverlay()
        {
            if (_overlay != null)
            {
                _overlay.enabled = !_overlay.enabled;
            }
        }

        private void Update()
        {
            if (_controller == null)
            {
                return;
            }

            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && keyboard.f1Key.wasPressedThisFrame)
            {
                ToggleOverlay();
            }

            _controller.Poll();
            _measurement.Poll();
        }

        private void OnDestroy()
        {
            _measurement?.Dispose();
            _controller?.Dispose();
        }
    }
}
