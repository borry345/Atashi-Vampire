using System;
using System.Collections.Generic;
using System.Globalization;
using Survivor.Core;
using Survivor.Simulation;
using UnityEngine;

namespace Survivor.Dev
{
    /// IMGUI dev panel (5.15): status panel (5.15.4) on top, controls in tabs below. English fixed labels; font "Malgun Gothic"
    /// from the OS (Hangul display names), the default font when that fails. Hidden = enabled false, so neither OnGUI nor the
    /// actual-speed sampling runs (no cost while closed, SPEC F19 64). Reads the host and calls DevController only.
    public sealed class DevOverlay : MonoBehaviour
    {
        /// OS font requested for the overlay.
        public const string FontName = "Malgun Gothic";

        /// Overlay font size in points.
        public const int FontSize = 14;

        private const int SampleCapacity = 512;
        private static readonly string[] Tabs = { "Run", "Player", "Enemies", "Time", "Strategy", "Visual", "Data (5b)" };

        private readonly List<string> _lines = new List<string>(32);
        private readonly float[] _sampleSeconds = new float[SampleCapacity];
        private readonly int[] _sampleSteps = new int[SampleCapacity];
        private int _sampleCount;
        private int _sampleHead;

        private DevController _controller;
        private DevVisualizer _visualizer;
        private Font _font;
        private GUIStyle _label;
        private Rect _window = new Rect(10f, 10f, 460f, 680f);
        private Vector2 _scroll;
        private int _tab;

        private string _seedText = "4242";
        private string _pathRadiusText = "6";
        private string _pathSpeedText = "4";
        private string _healthText = "50";
        private string _xpText = "10";
        private string _spawnIdText = "M1";
        private string _spawnCountText = "50";
        private string _onlySpeciesText = string.Empty;
        private string _holdText = "100";
        private bool _stopSpawns;
        private string _despawnText = "45";
        private string _jumpText = "36000";
        private string _scaleText = "1";
        private readonly string[] _loadoutIds = { "W1", "W2", "W3", "W4" };
        private readonly string[] _loadoutLevels = { "5", "5", "5", "5" };

        /// Font the overlay draws with; null means the engine default (SPEC F19 65: allowed, Hangul may not render).
        public Font OverlayFont => _font;

        /// Actual speed over the last unscaled second (or the shorter window since opening): steps / 60 per second (5.15.1); 0 before the first sample.
        public float ActualSpeed
        {
            get
            {
                float seconds = 0f;
                int steps = 0;
                for (int i = 0; i < _sampleCount && seconds < 1f; i++)
                {
                    int index = (_sampleHead - 1 - i + SampleCapacity) % SampleCapacity;
                    seconds += _sampleSeconds[index];
                    steps += _sampleSteps[index];
                }

                return seconds > 0f ? steps / (StepMath.StepsPerSecond * seconds) : 0f;
            }
        }

        /// Binds the controller and the visualizer (labels) and creates the OS font once. Null controller throws.
        public void Initialize(DevController controller, DevVisualizer visualizer)
        {
            _controller = controller ?? throw new ArgumentNullException(nameof(controller));
            _visualizer = visualizer;
            _font = CreateOverlayFont(() => Font.CreateDynamicFontFromOSFont(FontName, FontSize));
            DevLog.Info("Overlay font: " + (_font != null ? FontName : "engine default") + ".");
        }

        // Font factory guard: any failure falls back to the default font (null) without an error (SPEC F19 65).
        internal static Font CreateOverlayFont(Func<Font> factory)
        {
            try
            {
                return factory != null ? factory() : null;
            }
            catch (Exception e)
            {
                DevLog.Info("OS font unavailable (" + e.GetType().Name + "); using the default font.");
                return null;
            }
        }

        // Status panel lines from the host right now (5.15.4); used by OnGUI and the tests.
        internal void BuildStatusLines(List<string> linesOut)
        {
            DevStatusInput input = _controller.BuildStatusInput(ActualSpeed);
            DevStatusFormatter.Format(in input, linesOut);
        }

        private void OnEnable()
        {
            _sampleCount = 0;
            _sampleHead = 0;
        }

        // Actual-speed window: LastFrameSteps is captured by SimulationFrameCounter (order -10000) earlier in this frame.
        private void Update()
        {
            if (_controller == null)
            {
                return;
            }

            _sampleSeconds[_sampleHead] = Time.unscaledDeltaTime;
            _sampleSteps[_sampleHead] = _controller.Host.LastFrameSteps;
            _sampleHead = (_sampleHead + 1) % SampleCapacity;
            _sampleCount = Mathf.Min(_sampleCount + 1, SampleCapacity);
        }

        private void OnGUI()
        {
            if (_controller == null)
            {
                return;
            }

            if (_label == null)
            {
                _label = new GUIStyle(GUI.skin.label) { richText = false, wordWrap = false };
                if (_font != null)
                {
                    _label.font = _font;
                }
            }

            Font previous = GUI.skin.font;
            if (_font != null)
            {
                GUI.skin.font = _font;
            }

            _window = GUILayout.Window(0x5D3F, _window, DrawWindow, "Dev mode (F1)");
            if (_visualizer != null && _visualizer.enabled)
            {
                _visualizer.DrawLabels(_label);
            }

            GUI.skin.font = previous;
        }

        private void DrawWindow(int id)
        {
            BuildStatusLines(_lines);
            for (int i = 0; i < _lines.Count; i++)
            {
                GUILayout.Label(_lines[i], _label);
            }

            if (!string.IsNullOrEmpty(_controller.LastMessage))
            {
                GUILayout.Label("> " + _controller.LastMessage, _label);
            }

            _tab = GUILayout.SelectionGrid(_tab, Tabs, 4);
            _scroll = GUILayout.BeginScrollView(_scroll, GUILayout.ExpandHeight(true));
            switch (_tab)
            {
                case 0: DrawRunTab(); break;
                case 1: DrawPlayerTab(); break;
                case 2: DrawEnemiesTab(); break;
                case 3: DrawTimeTab(); break;
                case 4: DrawStrategyTab(); break;
                case 5: DrawVisualTab(); break;
                default: DrawDataTab(); break;
            }

            GUILayout.EndScrollView();
            GUI.DragWindow(new Rect(0f, 0f, 10000f, 20f));
        }

        private void DrawRunTab()
        {
            if (GUILayout.Button("Start new run (from Title / Result / Paused)"))
            {
                _controller.TryStartNewRun();
            }

            SimulationRules rules = _controller.Host.Rules;
            GUILayout.Label("Seed (next run): " + (_controller.Host.FixedSeed.HasValue
                ? _controller.Host.FixedSeed.Value.ToString(CultureInfo.InvariantCulture) : "random"));
            GUILayout.BeginHorizontal();
            _seedText = GUILayout.TextField(_seedText, GUILayout.Width(160f));
            if (GUILayout.Button("Fix seed") && ulong.TryParse(_seedText, NumberStyles.Integer, CultureInfo.InvariantCulture, out ulong seed))
            {
                _controller.SetFixedSeed(seed);
            }

            if (GUILayout.Button("Random"))
            {
                _controller.SetFixedSeed(null);
            }

            GUILayout.EndHorizontal();
            bool levelUp = GUILayout.Toggle(rules.LevelUpEnabled, "Level-up enabled");
            if (levelUp != rules.LevelUpEnabled)
            {
                _controller.SetLevelUpEnabled(levelUp);
            }

            bool autoPath = _controller.Host.InputOverride is AutoPathInputSource;
            GUILayout.Label("Auto path (next run): " + (autoPath ? "on" : "off"));
            GUILayout.BeginHorizontal();
            GUILayout.Label("r", GUILayout.Width(14f));
            _pathRadiusText = GUILayout.TextField(_pathRadiusText, GUILayout.Width(60f));
            GUILayout.Label("speed", GUILayout.Width(44f));
            _pathSpeedText = GUILayout.TextField(_pathSpeedText, GUILayout.Width(60f));
            if (GUILayout.Button("On"))
            {
                _controller.TrySetAutoPath(true, ParseFloat(_pathRadiusText), ParseFloat(_pathSpeedText));
            }

            if (GUILayout.Button("Off"))
            {
                _controller.TrySetAutoPath(false, 0f, 0f);
            }

            GUILayout.EndHorizontal();
        }

        private void DrawPlayerTab()
        {
            SimulationRules rules = _controller.Host.Rules;
            bool invulnerable = GUILayout.Toggle(!rules.PlayerTakesDamage, "Invulnerable");
            if (invulnerable == rules.PlayerTakesDamage)
            {
                _controller.SetInvulnerable(invulnerable);
            }

            bool weapons = GUILayout.Toggle(rules.WeaponsEnabled, "Attack on");
            if (weapons != rules.WeaponsEnabled)
            {
                _controller.SetWeaponsEnabled(weapons);
            }

            GUILayout.Label("Loadout (id, level; empty id = no weapon):");
            for (int i = 0; i < _loadoutIds.Length; i++)
            {
                GUILayout.BeginHorizontal();
                _loadoutIds[i] = GUILayout.TextField(_loadoutIds[i], GUILayout.Width(80f));
                _loadoutLevels[i] = GUILayout.TextField(_loadoutLevels[i], GUILayout.Width(40f));
                GUILayout.EndHorizontal();
            }

            if (GUILayout.Button("Set loadout now"))
            {
                var loadout = new List<WeaponLoadoutEntry>(_loadoutIds.Length);
                for (int i = 0; i < _loadoutIds.Length; i++)
                {
                    if (!string.IsNullOrEmpty(_loadoutIds[i]))
                    {
                        loadout.Add(new WeaponLoadoutEntry(_loadoutIds[i].Trim(), ParseInt(_loadoutLevels[i])));
                    }
                }

                _controller.TrySetLoadout(loadout);
            }

            GUILayout.BeginHorizontal();
            _healthText = GUILayout.TextField(_healthText, GUILayout.Width(80f));
            if (GUILayout.Button("Set health"))
            {
                _controller.TrySetHealth(ParseFloat(_healthText));
            }

            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            _xpText = GUILayout.TextField(_xpText, GUILayout.Width(80f));
            if (GUILayout.Button("Add XP"))
            {
                _controller.TryAddXp(ParseInt(_xpText));
            }

            if (GUILayout.Button("Level up now"))
            {
                _controller.TryLevelUpNow();
            }

            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            if (GUILayout.Button(_controller.Host.BossSpawnedThisRun ? "Spawn boss (one per run)" : "Spawn boss"))
            {
                _controller.TrySpawnBossNow();
            }

            if (GUILayout.Button("Spawn chest"))
            {
                _controller.TrySpawnChestNow();
            }

            GUILayout.EndHorizontal();
        }

        private void DrawEnemiesTab()
        {
            GUILayout.Label("Spawn now (ring r=20):");
            GUILayout.BeginHorizontal();
            _spawnIdText = GUILayout.TextField(_spawnIdText, GUILayout.Width(60f));
            _spawnCountText = GUILayout.TextField(_spawnCountText, GUILayout.Width(60f));
            if (GUILayout.Button("Spawn"))
            {
                _controller.TrySpawnMonstersNow(_spawnIdText.Trim(), ParseInt(_spawnCountText));
            }

            GUILayout.EndHorizontal();
            ISpawnPolicy current = _controller.Host.SpawnPolicyOverride;
            GUILayout.Label("Spawn policy (next run): " + DescribePolicy(current));
            _stopSpawns = GUILayout.Toggle(_stopSpawns, "Stop table spawns");
            GUILayout.BeginHorizontal();
            GUILayout.Label("Only species", GUILayout.Width(90f));
            _onlySpeciesText = GUILayout.TextField(_onlySpeciesText, GUILayout.Width(60f));
            GUILayout.Label("Hold N (0 = off)", GUILayout.Width(110f));
            _holdText = GUILayout.TextField(_holdText, GUILayout.Width(60f));
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Apply (next run)"))
            {
                _controller.TryConfigureSpawns(_stopSpawns, _onlySpeciesText.Trim(), ParseInt(_holdText));
            }

            if (GUILayout.Button("Table spawns"))
            {
                _stopSpawns = false;
                _controller.TryConfigureSpawns(false, null, 0);
            }

            GUILayout.EndHorizontal();
            GUILayout.Label("D in use: " + _controller.DespawnDistanceInUse.ToString("0.##", CultureInfo.InvariantCulture)
                + "  (hold N sets D by R26)");
            GUILayout.BeginHorizontal();
            _despawnText = GUILayout.TextField(_despawnText, GUILayout.Width(80f));
            if (GUILayout.Button("Set D now"))
            {
                _controller.TrySetDespawnDistance(ParseFloat(_despawnText));
            }

            GUILayout.EndHorizontal();
            bool lifted = _controller.Host.Rules.MonsterCap == int.MaxValue;
            bool lift = GUILayout.Toggle(lifted, "Lift the 400 monster cap");
            if (lift != lifted)
            {
                _controller.SetMonsterCapLifted(lift);
            }
        }

        private void DrawTimeTab()
        {
            GUILayout.BeginHorizontal();
            foreach (float preset in new[] { 0.25f, 0.5f, 1f, 2f, 3f })
            {
                if (GUILayout.Button("x" + preset.ToString("0.##", CultureInfo.InvariantCulture)))
                {
                    _controller.SetTimeScale(preset);
                }
            }

            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            _scaleText = GUILayout.TextField(_scaleText, GUILayout.Width(80f));
            if (GUILayout.Button("Set scale (0.25..3)"))
            {
                _controller.SetTimeScale(ParseFloat(_scaleText));
            }

            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            _jumpText = GUILayout.TextField(_jumpText, GUILayout.Width(100f));
            if (GUILayout.Button("Jump to step (forward only)"))
            {
                if (long.TryParse(_jumpText, NumberStyles.Integer, CultureInfo.InvariantCulture, out long target))
                {
                    _controller.TryJumpToStep(target);
                }
            }

            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Step once"))
            {
                _controller.TryStepOnce();
            }

            GUI.enabled = _controller.IsSingleStepMode;
            if (GUILayout.Button("Resume"))
            {
                _controller.TryResume();
            }

            GUI.enabled = true;
            GUILayout.EndHorizontal();
            GUILayout.Label("N-step auto stop: (5b)");
        }

        private void DrawStrategyTab()
        {
            StrategySelection next = _controller.Host.NextRunSelection;
            GUILayout.Label("Target applies now; the others at the next run.");
            GUILayout.Label("Pool: " + next.Pool);
            int pool = GUILayout.Toolbar((int)next.Pool, Enum.GetNames(typeof(PoolStrategyKind)));
            if (pool != (int)next.Pool)
            {
                _controller.SetNextPool((PoolStrategyKind)pool);
            }

            GUILayout.Label("Target: " + _controller.CurrentTarget);
            int target = GUILayout.Toolbar((int)next.Target, Enum.GetNames(typeof(TargetProviderKind)));
            if (target != (int)next.Target)
            {
                _controller.SetTarget((TargetProviderKind)target);
            }

            GUILayout.Label("Tick: " + next.Tick);
            int tick = GUILayout.Toolbar((int)next.Tick, Enum.GetNames(typeof(TickStrategyKind)));
            if (tick != (int)next.Tick)
            {
                _controller.SetNextTick((TickStrategyKind)tick);
            }

            GUILayout.Label("Collision: " + next.Collision);
            int collision = GUILayout.Toolbar((int)next.Collision, Enum.GetNames(typeof(CollisionStrategyKind)));
            if (collision != (int)next.Collision)
            {
                _controller.SetNextCollision((CollisionStrategyKind)collision);
            }

            bool continuous = next.EngineDetectionMode == CollisionDetectionMode2D.Continuous;
            bool ccd = GUILayout.Toggle(continuous, "Engine CCD (Continuous)");
            if (ccd != continuous)
            {
                _controller.SetNextCcd(ccd ? CollisionDetectionMode2D.Continuous : CollisionDetectionMode2D.Discrete);
            }

            GUILayout.Label("Cell size " + _controller.Host.SpatialIndex.CellSize.ToString("0.##", CultureInfo.InvariantCulture)
                + " (set at boot)");
        }

        private void DrawVisualTab()
        {
            if (_visualizer == null)
            {
                GUILayout.Label("Visualizer unavailable.");
                return;
            }

            _visualizer.ShowGrid = GUILayout.Toggle(_visualizer.ShowGrid, "Spatial hash grid + counts"
                + (_controller.Host.SpatialIndex.Required ? string.Empty : " (index inactive)"));
            _visualizer.ShowRadii = GUILayout.Toggle(_visualizer.ShowRadii, "Collision radii");
            _visualizer.ShowTargets = GUILayout.Toggle(_visualizer.ShowTargets, "Weapon -> target lines");
            _visualizer.ShowSpawnRings = GUILayout.Toggle(_visualizer.ShowSpawnRings, "Spawn (20) / despawn (D) rings");
        }

        private static void DrawDataTab()
        {
            GUI.enabled = false;
            GUILayout.Button("Hot reload JSON (5b)");
            GUILayout.Button("Dump data (5b)");
            GUILayout.Button("Collision log (5b)");
            GUILayout.Button("Measurement (5b)");
            GUI.enabled = true;
        }

        private static string DescribePolicy(ISpawnPolicy policy)
        {
            if (!(policy is DevSpawnPolicy dev))
            {
                return policy == null ? "table" : policy.GetType().Name;
            }

            return (dev.StopSpawns ? "stopped" : "table") + (dev.OnlySpeciesId != null ? ", only " + dev.OnlySpeciesId : string.Empty)
                + (dev.HoldCount > 0 ? ", hold " + dev.HoldCount.ToString(CultureInfo.InvariantCulture) : string.Empty);
        }

        private static float ParseFloat(string text)
        {
            return float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out float value) ? value : float.NaN;
        }

        private static int ParseInt(string text)
        {
            return int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int value) ? value : -1;
        }
    }
}
