using System.Collections.Generic;
using Survivor.App;
using Survivor.Core;
using Survivor.Simulation;
using UnityEngine;
using UnityEngine.Rendering;

namespace Survivor.Dev
{
    /// Debug drawing (TAC-F19-4, SPEC F19 visualization): spatial hash cells with per-cell counts, collision radii, weapon ->
    /// target lines (WeaponInstance.LastTargetId) and the spawn / despawn rings, as GL lines from the URP endCameraRendering
    /// callback. With every option off the callback is removed and the component disabled, so it costs nothing.
    public sealed class DevVisualizer : MonoBehaviour
    {
        private const int CircleSegments = 24;
        private static readonly Color GridColor = new Color(0.4f, 0.8f, 1f, 0.25f);
        private static readonly Color CellColor = new Color(0.3f, 0.9f, 1f, 0.9f);
        private static readonly Color RadiusColor = new Color(1f, 1f, 0.2f, 0.8f);
        private static readonly Color MagnetColor = new Color(0.4f, 1f, 0.4f, 0.5f);
        private static readonly Color TargetColor = new Color(1f, 0.3f, 0.3f, 0.9f);
        private static readonly Color SpawnColor = new Color(0.3f, 1f, 0.3f, 0.8f);
        private static readonly Color DespawnColor = new Color(1f, 0.5f, 0.1f, 0.8f);

        private IDevModeHost _host;
        private DevController _controller;
        private Material _material;
        private bool _materialMissingLogged;
        private bool _subscribed;
        private bool _showGrid;
        private bool _showRadii;
        private bool _showTargets;
        private bool _showSpawnRings;

        /// Spatial hash cell boundaries and per-cell monster counts (labels only while the overlay is open).
        public bool ShowGrid
        {
            get => _showGrid;
            set { _showGrid = value; Refresh(); }
        }

        /// Collision radius circles of every live entity and the player body / magnet.
        public bool ShowRadii
        {
            get => _showRadii;
            set { _showRadii = value; Refresh(); }
        }

        /// Line from the player to each weapon's last chosen target.
        public bool ShowTargets
        {
            get => _showTargets;
            set { _showTargets = value; Refresh(); }
        }

        /// Spawn circle (r = 20) and despawn circle (D) around the player.
        public bool ShowSpawnRings
        {
            get => _showSpawnRings;
            set { _showSpawnRings = value; Refresh(); }
        }

        /// True while any option is on.
        public bool AnyOn => _showGrid || _showRadii || _showTargets || _showSpawnRings;

        // True while the endCameraRendering callback is registered (tests).
        internal bool IsSubscribed => _subscribed;

        /// Binds the host and controller; starts with everything off (disabled, no callback).
        public void Initialize(IDevModeHost host, DevController controller)
        {
            _host = host;
            _controller = controller;
            Refresh();
        }

        /// Draws the per-cell counts as IMGUI labels; called from DevOverlay.OnGUI (overlay open) only.
        public void DrawLabels(GUIStyle style)
        {
            if (!_showGrid || _host == null || !_host.SpatialIndex.Required)
            {
                return;
            }

            Camera camera = Camera.main;
            if (camera == null)
            {
                return;
            }

            float cellSize = _host.SpatialIndex.CellSize;
            foreach (KeyValuePair<long, int> cell in _host.SpatialIndex.CellCounts)
            {
                Vector2 center = CellOrigin(cell.Key, cellSize) + new Vector2(cellSize * 0.5f, cellSize * 0.5f);
                Vector3 screen = camera.WorldToScreenPoint(center);
                if (screen.z < 0f)
                {
                    continue;
                }

                GUI.Label(new Rect(screen.x - 12f, Screen.height - screen.y - 10f, 40f, 20f), cell.Value.ToString(), style);
            }
        }

        private void Refresh()
        {
            bool on = AnyOn && _host != null;
            if (on && !_subscribed)
            {
                RenderPipelineManager.endCameraRendering += OnEndCameraRendering;
                _subscribed = true;
            }
            else if (!on && _subscribed)
            {
                RenderPipelineManager.endCameraRendering -= OnEndCameraRendering;
                _subscribed = false;
            }

            enabled = on;
        }

        private void OnDestroy()
        {
            if (_subscribed)
            {
                RenderPipelineManager.endCameraRendering -= OnEndCameraRendering;
                _subscribed = false;
            }

            if (_material != null)
            {
                Destroy(_material);
            }
        }

        private void OnEndCameraRendering(ScriptableRenderContext context, Camera camera)
        {
            if (camera == null || camera.cameraType != CameraType.Game || !EnsureMaterial())
            {
                return;
            }

            RunSession session = _host.Session;
            Vector2 player = session.Player.Position;
            _material.SetPass(0);
            GL.PushMatrix();
            GL.LoadProjectionMatrix(camera.projectionMatrix);
            GL.modelview = camera.worldToCameraMatrix;
            GL.Begin(GL.LINES);
            if (_showGrid && _host.SpatialIndex.Required)
            {
                DrawGrid(camera);
            }

            if (_showRadii)
            {
                DrawRadii(session);
            }

            if (_showTargets)
            {
                DrawTargets(session, player);
            }

            if (_showSpawnRings)
            {
                Circle(player, GameRules.SpawnRadius, SpawnColor);
                float d = _controller != null ? _controller.DespawnDistanceInUse : 0f;
                if (d > 0f)
                {
                    Circle(player, d, DespawnColor);
                }
            }

            GL.End();
            GL.PopMatrix();
        }

        private void DrawGrid(Camera camera)
        {
            float cellSize = _host.SpatialIndex.CellSize;
            float halfHeight = camera.orthographic ? camera.orthographicSize : 10f;
            float halfWidth = halfHeight * camera.aspect;
            Vector3 eye = camera.transform.position;
            float minX = Mathf.Floor((eye.x - halfWidth) / cellSize) * cellSize;
            float minY = Mathf.Floor((eye.y - halfHeight) / cellSize) * cellSize;
            float maxX = eye.x + halfWidth;
            float maxY = eye.y + halfHeight;
            GL.Color(GridColor);
            for (float x = minX; x <= maxX; x += cellSize)
            {
                GL.Vertex3(x, minY, 0f);
                GL.Vertex3(x, maxY, 0f);
            }

            for (float y = minY; y <= maxY; y += cellSize)
            {
                GL.Vertex3(minX, y, 0f);
                GL.Vertex3(maxX, y, 0f);
            }

            foreach (KeyValuePair<long, int> cell in _host.SpatialIndex.CellCounts)
            {
                Vector2 origin = CellOrigin(cell.Key, cellSize);
                Square(origin, cellSize, CellColor);
            }
        }

        private static void DrawRadii(RunSession session)
        {
            IReadOnlyList<SimEntity> entities = session.Lifecycle.ActiveEntities;
            for (int i = 0; i < entities.Count; i++)
            {
                SimEntity entity = entities[i];
                if (entity.Body != null && entity.Body.Radius > 0f)
                {
                    Circle(entity.Position, entity.Body.Radius, RadiusColor);
                }
            }

            PlayerEntity player = session.Player;
            if (player.Body != null)
            {
                Circle(player.Position, player.Body.Radius, RadiusColor);
            }

            if (player.MagnetBody != null)
            {
                Circle(player.Position, player.MagnetBody.Radius, MagnetColor);
            }
        }

        private static void DrawTargets(RunSession session, Vector2 player)
        {
            IReadOnlyList<WeaponInstance> slots = session.Weapons.Slots;
            GL.Color(TargetColor);
            for (int i = 0; i < slots.Count; i++)
            {
                WeaponInstance weapon = slots[i];
                if (weapon == null || !weapon.LastTargetId.IsValid || !session.Lifecycle.TryGet(weapon.LastTargetId, out SimEntity target))
                {
                    continue;
                }

                GL.Vertex3(player.x, player.y, 0f);
                GL.Vertex3(target.Position.x, target.Position.y, 0f);
            }
        }

        // R14 key -> lower-left corner of the cell.
        private static Vector2 CellOrigin(long key, float cellSize)
        {
            int cx = (int)(key >> 32);
            int cy = unchecked((int)(key & 0xFFFFFFFFL));
            return new Vector2(cx * cellSize, cy * cellSize);
        }

        private static void Square(Vector2 origin, float size, Color color)
        {
            GL.Color(color);
            Vector2 a = origin;
            Vector2 b = origin + new Vector2(size, 0f);
            Vector2 c = origin + new Vector2(size, size);
            Vector2 d = origin + new Vector2(0f, size);
            Segment(a, b);
            Segment(b, c);
            Segment(c, d);
            Segment(d, a);
        }

        private static void Circle(Vector2 center, float radius, Color color)
        {
            GL.Color(color);
            Vector2 previous = center + new Vector2(radius, 0f);
            for (int i = 1; i <= CircleSegments; i++)
            {
                float angle = i * 2f * Mathf.PI / CircleSegments;
                Vector2 next = center + new Vector2(radius * Mathf.Cos(angle), radius * Mathf.Sin(angle));
                Segment(previous, next);
                previous = next;
            }
        }

        private static void Segment(Vector2 a, Vector2 b)
        {
            GL.Vertex3(a.x, a.y, 0f);
            GL.Vertex3(b.x, b.y, 0f);
        }

        // Unlit vertex-colored material for GL lines; logs once and draws nothing when no such shader is available.
        private bool EnsureMaterial()
        {
            if (_material != null)
            {
                return true;
            }

            Shader shader = Shader.Find("Hidden/Internal-Colored") ?? Shader.Find("Sprites/Default");
            if (shader == null)
            {
                if (!_materialMissingLogged)
                {
                    _materialMissingLogged = true;
                    DevLog.Warning("No line shader (Hidden/Internal-Colored, Sprites/Default); visualization draws nothing.");
                }

                return false;
            }

            _material = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
            _material.SetInt("_ZTest", (int)CompareFunction.Always);
            _material.SetInt("_ZWrite", 0);
            _material.SetInt("_Cull", (int)CullMode.Off);
            return true;
        }
    }
}
