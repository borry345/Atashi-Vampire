using System;
using Survivor.App;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Survivor.Dev
{
    /// Exists only while dev mode is active (TAC-F19-10): owns the controller, overlay and visualizer, toggles the overlay
    /// with F1 (the only key watched, here only) and lets the controller notice run resets.
    public sealed class DevModeRoot : MonoBehaviour
    {
        private DevController _controller;
        private DevOverlay _overlay;
        private DevVisualizer _visualizer;

        /// Action layer over the host.
        public DevController Controller => _controller;

        /// IMGUI panel (disabled while hidden).
        public DevOverlay Overlay => _overlay;

        /// GL visualization (disabled while every option is off).
        public DevVisualizer Visualizer => _visualizer;

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
        }

        private void OnDestroy()
        {
            _controller?.Dispose();
        }
    }
}
