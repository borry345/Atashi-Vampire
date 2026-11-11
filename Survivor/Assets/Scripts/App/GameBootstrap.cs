using System;
using System.Collections.Generic;
using System.Globalization;
using Survivor.Core;
using Survivor.Data;
using Survivor.Simulation;
using Survivor.Strategies;
using Survivor.Strategies.Pool;
using Survivor.Strategies.Tick;
using Survivor.UI;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.Rendering.Universal;

namespace Survivor.App
{
    /// Composition root (5c.3 #9 memo, 5c.3a, 5.11.0): the only place that creates and wires the simulation, strategies,
    /// presentation and UI. Owners expose handlers; every registration (pipeline orders, command buffer kinds, router channels,
    /// pending owner kinds) is made here so one assembly test sees the whole wiring. Also the IDevModeHost of dev mode (5.15).
    /// Main.unity holds only this component, the camera and the global light; everything else is created in Awake.
    [DefaultExecutionOrder(-5000)]
    public sealed class GameBootstrap : MonoBehaviour, IDevModeHost
    {
        // Test hooks (set before the scene loads, cleared by the test): a different StreamingAssets root and best-record key.
        internal static string DataRootOverride;
        internal static string BestRecordKeyOverride;

        private SimulationRules _rules;
        private SimulationClock _clock;
        private SimulationPipeline _pipeline;
        private SimulationCommandBuffer _commands;
        private MonsterSnapshotBuffer _snapshot;
        private EntityHandleAllocator _ids;
        private RngStreams _rng;
        private GameDataRegistry _data;
        private MonsterSpatialIndex _index;
        private PhysicsBodySynchronizer _bodies;
        private StrategyFactory _factory;
        private TargetProviderSlot _targets;
        private PrefabCatalog _catalog;
        private EntityLifecycle _lifecycle;
        private MonsterRegistry _registry;
        private CollisionEventRouter _router;
        private PendingCommandQueue _pending;
        private GameObject _playerHost;
        private KeyboardInputSource _keyboard;
        private PassiveInventory _passives;
        private PlayerHealth _health;
        private PlayerEntity _player;
        private PlayerMovement _movement;
        private WeaponInventory _weapons;
        private PlayerSpawnCommandHandler _playerSpawns;
        private PlayerHitHandler _hitHandler;
        private WeaponDamageResolver _damageResolver;
        private PlayerDamageReceiver _receiver;
        private PlayerProgressionApplier _applier;
        private EvolutionService _evolution;
        private ChestEffectResolver _chests;
        private MonsterSpawner _spawner;
        private MonsterCommandHandler _monsterCommands;
        private DropCommandHandler _drops;
        private DespawnSystem _despawner;
        private KillPipeline _kills;
        private PickupSystem _pickups;
        private PickupRegistry _pickupRegistry;
        private ExperienceSystem _experience;
        private LevelUpSession _levelUpSession;
        private StepOutcomeGate _gate;
        private RunSession _session;
        private GameFlowController _flow;
        private Camera _camera;
        private SimulationFrameCounter _frameCounter;
        private AssetKeyResolver _resolver;
        private EntityPresenter _presenter;

        /// Raised when a run ends by death or clear.
        public event Action<RunOutcome> RunEnded;

        /// Runtime rules.
        public SimulationRules Rules => _rules;

        /// Step clock and run gate.
        public SimulationClock Clock => _clock;

        /// Between-steps command queue.
        public PendingCommandQueue Commands => _pending;

        /// Game data registry (boot load).
        public GameDataRegistry Data => _data;

        /// Run session.
        public RunSession Session => _session;

        /// Monster spatial index.
        public MonsterSpatialIndex SpatialIndex => _index;

        /// Selection applied at the next run start (ShippingDefault until changed).
        public StrategySelection NextRunSelection { get; set; } = StrategySelection.ShippingDefault;

        /// Collision decorator of the next run (null = none).
        public Func<ICollisionStrategy, ICollisionStrategy> NextRunCollisionDecorator { get; set; }

        /// Input override of the next run (null = keyboard).
        public IInputSource InputOverride { get; set; }

        /// Spawn policy override of the next run (null = spawn table).
        public ISpawnPolicy SpawnPolicyOverride { get; set; }

        /// Fixed master seed (null = a fresh seed per run).
        public ulong? FixedSeed { get; set; }

        /// MonsterSpawner.BossSpawnedThisRun as is.
        public bool BossSpawnedThisRun => _spawner.BossSpawnedThisRun;
        public int LastFrameSteps => _frameCounter.LastFrameSteps;     // Steps run in the latest frame (dev actual speed).
        public int ActiveGemCount => _pickupRegistry.ActiveGemCount;   // Live experience gems (dev status, CSV).
        public int LiveMonsterCount => _registry.Count;                // Live monsters, boss included (dev status, CSV).
        public ChestResultLog ChestResults => _applier.ChestResults;   // Latest chest results (dev command log).

        /// Flow controller (screen state).
        public GameFlowController Flow => _flow;

        internal bool IsDataPlayable => _data.Current != null && _data.LastReport != null && _data.LastReport.IsPlayable;
        internal SimulationPipeline Pipeline => _pipeline;
        internal SimulationCommandBuffer CommandBuffer => _commands;
        internal EntityHandleAllocator Ids => _ids;
        internal PhysicsBodySynchronizer Bodies => _bodies;
        internal TargetProviderSlot Targets => _targets;
        internal PrefabCatalog Catalog => _catalog;
        internal EntityLifecycle Lifecycle => _lifecycle;
        internal CollisionEventRouter Router => _router;
        internal PlayerEntity Player => _player;
        internal GameObject PlayerHost => _playerHost;
        internal KeyboardInputSource KeyboardInput => _keyboard;
        internal PlayerMovement Movement => _movement;
        internal PlayerDamageReceiver DamageReceiver => _receiver;
        internal PlayerProgressionApplier Applier => _applier;
        internal MonsterSpawner Spawner => _spawner;
        internal KillPipeline Kills => _kills;
        internal PickupSystem Pickups => _pickups;
        internal PickupRegistry PickupRegistry => _pickupRegistry;
        internal SimulationFrameCounter FrameCounter => _frameCounter;
        internal Camera MainCamera => _camera;
        internal EntityPresenter Presenter => _presenter;

        /// Builds the provider for kind and queues SwitchTargetProvider with the shared requirements callback (TAC-F16-5).
        public void RequestTargetProviderSwitch(TargetProviderKind kind)
        {
            ITargetProvider provider = _factory.CreateTarget(kind);
            _pending.Enqueue(new SwitchTargetProviderCommand(provider, ApplyRequirements));
            AppLog.Info("Target provider switch to " + kind + " queued for the next step.");
        }

        /// Starts a new run through the flow (reset with NextRunSelection, requirements, HUD, gate open).
        public void StartNewRun()
        {
            _flow.StartRun();
        }

        // Run boundary used by start, restart and back-to-title (TAC-F8-2 App side): strategies of NextRunSelection (+ decorator)
        // -> RunSession.Reset -> prewarm when the pool object changed -> spatial index Clear -> requirements callback. The gate
        // stays closed; the flow opens it (or not, for back-to-title).
        internal void ResetRun()
        {
            StrategySelection selection = NextRunSelection;
            RunStrategies next = _factory.Create(in selection, _lifecycle.Pool, NextRunCollisionDecorator);
            bool poolChanged = !ReferenceEquals(next.Pool, _lifecycle.Pool);
            _session.Reset(in next);
            if (poolChanged)
            {
                PoolPrewarmCounts.PrewarmAll(next.Pool, _catalog);
            }

            _index.Clear();
            ApplyRequirements();
        }

        internal void NotifyRunEnded(in RunOutcome outcome)
        {
            RunEnded?.Invoke(outcome);
        }

        // The single requirements callback (TAC-F8-2, TAC-F16-5, 5.7): Required from the current target provider and collision
        // strategy, then R19 for the bodies. Run start and a provider switch (Begin) both call this.
        private void ApplyRequirements()
        {
            TargetProviderKind target = _targets.Kind;
            CollisionStrategyKind collision = _lifecycle.Collisions.Kind;
            _index.Required = target == TargetProviderKind.SpatialHash || collision == CollisionStrategyKind.SelfSquaredDistance;
            var current = new StrategySelection(_lifecycle.Pool.Kind, target, _lifecycle.Ticks.Kind, collision);
            _bodies.ApplyRequirements(ColliderActivationPolicy.Resolve(in current));
        }

        // 5.11.0 boot order, then assembly, presentation and UI.
        private void Awake()
        {
            SimulationSettingsApplier.Apply();
            PhysicsSettingsApplier.Apply();
            DisplaySetup.Apply();
            string root = DataRootOverride ?? Application.streamingAssetsPath;
            (GameData data, DataValidationReport report) = GameDataLoader.LoadAll(root);
            _data = new GameDataRegistry();
            _data.Initialize(data, report);
            AppLog.Info("Data loaded: playable=" + report.IsPlayable + ", blocking=" + report.BlockingCount.ToString(CultureInfo.InvariantCulture)
                + ", warnings=" + report.WarningCount.ToString(CultureInfo.InvariantCulture) + ".");
            // Key warnings go to the log only (null report) so they never mix with the title's data error list (5.11 rev 14).
            AssetKeyRegistry registry = AssetKeyRegistry.LoadDefault();
            _resolver = new AssetKeyResolver(registry, null);
            if (data != null)
            {
                _resolver.ValidateKeys(data);
            }

            AppLog.Info("Asset key registry " + (registry != null ? "loaded." : "missing; entities use the magenta fallback circle."));

            EnsureGlobalLight();
            Assemble();
            BuildPresentation();
            BuildUi();
        }

        private void Assemble()
        {
            StrategySelection selection = NextRunSelection;
            Func<GameData> current = () => _data.Current;

            // LOOP (5c.3 #9, LoopPmRig).
            _rules = new SimulationRules();
            _clock = new SimulationClock();
            _pipeline = new SimulationPipeline();
            _commands = new SimulationCommandBuffer();
            _snapshot = new MonsterSnapshotBuffer();
            _ids = new EntityHandleAllocator();
            _rng = new RngStreams(0UL);
            _index = new MonsterSpatialIndex(selection.SpatialHashCellSize, _snapshot);
            _bodies = new PhysicsBodySynchronizer(ColliderActivationPolicy.Resolve(in selection));
            _factory = new StrategyFactory(_clock, _index, _snapshot, _bodies);
            _targets = new TargetProviderSlot(_clock);
            // Every decorator on every template regardless of the run's strategies (TAC-F9-7): the catalog lives as long as
            // the app while the strategies change per run.
            _catalog = new PrefabCatalog(new Dictionary<EntityCategory, Type>
            {
                { EntityCategory.PlayerProjectile, typeof(PlayerProjectile) },
                { EntityCategory.PlayerOrb, typeof(OrbitOrb) },
                { EntityCategory.PlayerZone, typeof(DamageZone) },
                { EntityCategory.Monster, typeof(Monster) },
                { EntityCategory.EnemyProjectile, typeof(EnemyProjectile) },
                { EntityCategory.EnemyZone, typeof(EnemyHazardZone) },
                { EntityCategory.Gem, typeof(ExperienceGem) },
                { EntityCategory.Chest, typeof(Chest) },
            }, new IEntityTemplateDecorator[] { new PoolTemplateDecorator(), new SelfTickTemplateDecorator(), new CollisionTemplateDecorator() });
            RunStrategies initial = _factory.Create(in selection, null, null);
            _lifecycle = new EntityLifecycle(initial.Pool, _ids, initial.Tick, initial.Collision, _bodies, _catalog, _clock);
            PoolPrewarmCounts.PrewarmAll(initial.Pool, _catalog);
            _targets.Replace(initial.Target);
            _registry = new MonsterRegistry(_lifecycle, _snapshot);
            _router = new CollisionEventRouter(initial.Collision);
            _pending = new PendingCommandQueue(_targets, _data, _rules);

            // PLAYER (5c.3a).
            _playerHost = new GameObject("Player");
            var input = new GameObject("Input");
            input.transform.SetParent(transform, false);
            _keyboard = input.AddComponent<KeyboardInputSource>();
            _passives = new PassiveInventory();
            _health = new PlayerHealth();
            _player = new PlayerEntity(_ids, _bodies, current, _passives, _playerHost);
            _movement = new PlayerMovement(_player, _keyboard, _passives, current);
            _weapons = new WeaponInventory(_player, _passives, _lifecycle);
            _playerSpawns = new PlayerSpawnCommandHandler(_lifecycle, _weapons);
            _hitHandler = new PlayerHitHandler(_lifecycle, _weapons, LookUpDamageable);
            _damageResolver = new WeaponDamageResolver(_weapons, LookUpDamageable);
            _receiver = new PlayerDamageReceiver(_health, current, LookUpContactSource, _lifecycle);
            _applier = new PlayerProgressionApplier(_clock, _weapons, _passives, _health, _player, current);
            _evolution = new EvolutionService(_weapons, _passives, current);
            _chests = new ChestEffectResolver(_evolution, _applier, _weapons);

            // MONSTER / PICKUP (5c.3a) and FLOW (4a).
            _spawner = new MonsterSpawner(_lifecycle, _data, _rng);
            _monsterCommands = new MonsterCommandHandler(_lifecycle, _data, _rng, _spawner);
            _drops = new DropCommandHandler(_lifecycle, _data);
            _despawner = new DespawnSystem(_lifecycle, _registry, _data);
            _kills = new KillPipeline(_registry, _data, _rng);
            _experience = new ExperienceSystem(current);
            _pickups = new PickupSystem(_lifecycle, _data, _rng, _experience.AddXp, (rng, context) => _chests.Resolve(rng, context));
            _pickupRegistry = new PickupRegistry(_lifecycle);
            _levelUpSession = new LevelUpSession();
            _gate = new StepOutcomeGate(_clock, _health, _kills, _experience, current);
            _session = new RunSession(_clock, _lifecycle, _router, _targets, _ids, _rng, _commands, _pending, _player, _movement, _health,
                _weapons, _passives, _receiver, _applier, _pickups, _kills, _spawner, _experience, _levelUpSession, _gate, NextSeed,
                () => SpawnPolicyOverride ?? new TableSpawnPolicy(_data), () => InputOverride ?? _keyboard);

            // Command buffer: all nine kinds (5.4).
            _commands.RegisterHandler(SimulationCommandKind.Despawn, new EntityDespawnCommandHandler(_lifecycle));
            _commands.RegisterHandler(SimulationCommandKind.SpawnPlayerProjectile, _playerSpawns);
            _commands.RegisterHandler(SimulationCommandKind.SpawnPlayerOrb, _playerSpawns);
            _commands.RegisterHandler(SimulationCommandKind.SpawnPlayerZone, _playerSpawns);
            _commands.RegisterHandler(SimulationCommandKind.SpawnEnemyProjectile, _monsterCommands);
            _commands.RegisterHandler(SimulationCommandKind.SpawnEnemyZone, _monsterCommands);
            _commands.RegisterHandler(SimulationCommandKind.SpawnMonsters, _monsterCommands);
            _commands.RegisterHandler(SimulationCommandKind.SpawnGem, _drops);
            _commands.RegisterHandler(SimulationCommandKind.SpawnChest, _drops);

            // Router: channels 0..8 (5.9).
            _router.SetHandler(CollisionChannel.PlayerProjectileVsMonster, _hitHandler);
            _router.SetHandler(CollisionChannel.PlayerOrbVsMonster, _hitHandler);
            _router.SetHandler(CollisionChannel.PlayerZoneVsMonster, _hitHandler);
            _router.SetHandler(CollisionChannel.PlayerBodyVsMonster, _receiver);
            _router.SetHandler(CollisionChannel.EnemyProjectileVsPlayerBody, _receiver);
            _router.SetHandler(CollisionChannel.EnemyZoneVsPlayerBody, _receiver);
            _router.SetHandler(CollisionChannel.PlayerMagnetVsGem, _pickups);
            _router.SetHandler(CollisionChannel.PlayerBodyVsGem, _pickups);
            _router.SetHandler(CollisionChannel.PlayerBodyVsChest, _pickups);

            // Pending owner kinds (5.15) and the boss flag.
            _pending.RegisterHandler(PendingCommandKind.SetLoadout, _applier.HandleSetLoadout);
            _pending.RegisterHandler(PendingCommandKind.SetHealth, _applier.HandleSetHealth);
            _pending.RegisterHandler(PendingCommandKind.AddXp, _experience.HandleAddXp);
            _pending.RegisterHandler(PendingCommandKind.LevelUpNow, _experience.HandleLevelUpNow);
            _pending.RegisterHandler(PendingCommandKind.SetSpawnsEnabled, _spawner.HandleSetSpawnsEnabled);
            _pending.BossSpawnedThisRun = () => _spawner.BossSpawnedThisRun;

            // Phase handlers in the 5c.3 #9 orders.
            _pipeline.AddHandler(SimulationPhase.Begin, 0, _pending);
            _pipeline.AddHandler(SimulationPhase.Begin, 10, _movement);
            _pipeline.AddHandler(SimulationPhase.PlayerMove, 0, _movement);
            _pipeline.AddHandler(SimulationPhase.BuildIndex, 0, _registry);
            _pipeline.AddHandler(SimulationPhase.BuildIndex, 5, _bodies);
            _pipeline.AddHandler(SimulationPhase.BuildIndex, 10, _index);
            _pipeline.AddHandler(SimulationPhase.WeaponTick, 0, _weapons);
            _pipeline.AddHandler(SimulationPhase.WeaponTick, 10, _pickups);
            _pipeline.AddHandler(SimulationPhase.Collision, 0, _router);
            _pipeline.AddHandler(SimulationPhase.Resolve, 0, _router);
            _pipeline.AddHandler(SimulationPhase.Resolve, 10, _damageResolver);
            _pipeline.AddHandler(SimulationPhase.Resolve, 20, _receiver);
            _pipeline.AddHandler(SimulationPhase.Resolve, 30, _kills);
            _pipeline.AddHandler(SimulationPhase.Lifecycle, 0, _commands);
            _pipeline.AddHandler(SimulationPhase.Lifecycle, 10, new LifetimeSystem(_lifecycle));
            _pipeline.AddHandler(SimulationPhase.Lifecycle, 20, _despawner);
            _pipeline.AddHandler(SimulationPhase.Lifecycle, 30, _spawner);
            _pipeline.AddHandler(SimulationPhase.Lifecycle, 90, _gate);

            // Drivers (5.3).
            var simulation = new GameObject("Simulation");
            simulation.transform.SetParent(transform, false);
            _frameCounter = simulation.AddComponent<SimulationFrameCounter>();
            _frameCounter.Initialize(_clock);
            simulation.AddComponent<SimulationDriverEarly>().Initialize(_clock, _pipeline, _rules, _targets, _commands, () => _lifecycle.Ticks,
                () => _player.Position);
            simulation.AddComponent<SimulationDriverMid>().Initialize(_clock, _pipeline, () => _lifecycle.Ticks, _snapshot, _targets);
            simulation.AddComponent<SimulationDriverLate>().Initialize(_clock, _pipeline, _rules);
            simulation.AddComponent<PresentationSync>().Initialize(_lifecycle);
            _presenter = simulation.AddComponent<EntityPresenter>();
            _presenter.Initialize(_lifecycle, _resolver, current);
        }

        // Camera (orthographic size 9, exact follow), endless background and the player circle (TAC-F1-6, TAC-F8-11).
        private void BuildPresentation()
        {
            _camera = Camera.main;
            if (_camera == null)
            {
                var cameraObject = new GameObject("Main Camera") { tag = "MainCamera" };
                cameraObject.transform.position = new Vector3(0f, 0f, -10f);
                _camera = cameraObject.AddComponent<Camera>();
            }

            _camera.orthographic = true;
            _camera.orthographicSize = GameRules.CameraOrthographicSize;
            _camera.gameObject.AddComponent<CameraFollow>().Initialize(() => _player.Position);
            var background = new GameObject("Background");
            float viewHeight = GameRules.CameraOrthographicSize * 2f;
            background.AddComponent<BackgroundTiler>().Initialize(_camera.transform, new Vector2(viewHeight * 16f / 9f, viewHeight));
            _playerHost.AddComponent<PlayerPresenter>().Initialize(_player);
            _playerHost.AddComponent<OrbitRingPresenter>().Initialize(_weapons);
        }

        // Overlay canvas, EventSystem with the Input System UI module, six views, keyboard shortcuts, then the flow (TAC-F8-7, F8-12).
        private void BuildUi()
        {
            Canvas canvas = UiFactory.CreateCanvas("UI");
            Transform root = canvas.transform;
            if (EventSystem.current == null && FindAnyObjectByType<EventSystem>() == null)
            {
                var eventSystem = new GameObject("EventSystem");
                eventSystem.AddComponent<EventSystem>();
                eventSystem.AddComponent<InputSystemUIInputModule>().AssignDefaultActions();
            }

            HudView hud = HudView.Create(root);
            TitleView title = TitleView.Create(root);
            DataErrorView error = DataErrorView.Create(root);
            LevelUpView levelUp = LevelUpView.Create(root);
            PauseMenuView pause = PauseMenuView.Create(root);
            ResultView result = ResultView.Create(root);
            UiInputHandler input = canvas.gameObject.AddComponent<UiInputHandler>();
            input.Initialize(levelUp);
            var bestRecord = new BestRecordStore(BestRecordKeyOverride ?? BestRecordStore.DefaultKey);
            _flow = gameObject.AddComponent<GameFlowController>();
            _flow.Initialize(this, title, hud, levelUp, pause, result, error, input, bestRecord);
        }

        // 5b: Main.unity already has a Global Light 2D; create one (intensity 1) only when the scene has none [U34].
        private static void EnsureGlobalLight()
        {
            foreach (Light2D light in FindObjectsByType<Light2D>(FindObjectsSortMode.None))
            {
                if (light.lightType == Light2D.LightType.Global)
                {
                    return;
                }
            }

            var lightObject = new GameObject("Global Light 2D");
            var created = lightObject.AddComponent<Light2D>();
            created.lightType = Light2D.LightType.Global;
            created.intensity = 1f;
        }

        // Seed of each run: the dev seed, or one wall-clock read at the run boundary (TAC-0-2 exception for normal play).
        private ulong NextSeed()
        {
            return FixedSeed ?? (ulong)DateTime.UtcNow.Ticks;
        }

        private IDamageable LookUpDamageable(EntityHandle id)
        {
            return _lifecycle.TryGet(id, out SimEntity e) ? e as IDamageable : null;
        }

        private IContactDamageSource LookUpContactSource(EntityHandle id)
        {
            return _lifecycle.TryGet(id, out SimEntity e) ? e as IContactDamageSource : null;
        }
    }
}
