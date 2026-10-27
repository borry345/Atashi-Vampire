> 분할 파일 — 원본 `TECH_SPEC.md`(5회차)를 2026-10-27에 제목 단위로 나눈 것이다. 내용은 원본 그대로이며 색인·개정 이력은 `tech/README.md`에 있다. 수정은 tech-spec-writer만 한다.

## 5c. 소유권 맵·인터페이스 계약 초안 (Ownership Map & Contract Draft)

**이 절이 5절과 어긋나면 이 절이 우선한다.** 구현자는 각자 스크래치 디렉터리(`<프로젝트>/.scratch/<IMP 이름>/`)에서 아래 최종 경로와 같은 상대 경로로 작업한다(ORCHESTRATION.md "구현 에이전트 디스패치 원칙"). 남의 파일이 필요하면 자기 스크래치 안에 스텁을 두고, 조립 시 소유자 버전만 채택된다.

### 5c.1 폴더 구조와 어셈블리 (2회차 수정, A8)

Unity 프로젝트 루트 = `indiv/Survivor/` [제안, 사용자 결정]. 하네스 문서는 `indiv/` 루트에 그대로 둔다.

```
Survivor/
  Assets/
    Scripts/
      Core/            Survivor.Core.asmdef
      Data/            Survivor.Data.asmdef
      Simulation/      Survivor.Simulation.asmdef
        Loop/ Entities/ Player/ Weapons/ Monsters/ Pickups/ Progression/
      Strategies/      Survivor.Strategies.asmdef
        Pool/ Spatial/ Targeting/ Tick/ Collision/
      UI/              Survivor.UI.asmdef
      App/             Survivor.App.asmdef
      Dev/             Survivor.Dev.asmdef           (defineConstraints: ["!SURVIVOR_NO_DEV"])
    Editor/            Survivor.Editor.asmdef        (Editor only)
    UI/Fonts/          TTF, TMP Font Asset            (사용자 생성)
    Resources/         AssetKeyRegistry.asset        (사용자 생성)
    StreamingAssets/Data/  weapons.json monsters.json combinations.json spawnTable.json passives.json levelUpPool.json playerStats.json
    Scenes/            Main.unity                    (Bootstrap 오브젝트 1개 + Camera. 나머지는 코드로 생성)
    Tests/
      EditMode/        Survivor.Tests.EditMode.asmdef (Editor platform)
      PlayMode/        Survivor.Tests.PlayMode.asmdef
      Dev/             Survivor.Tests.Dev.asmdef      (defineConstraints: ["!SURVIVOR_NO_DEV"])
  docs/devlog.md
  README.md
```

**참조 방향 표 (어셈블리 → 참조 가능 어셈블리)**. 이 표에 없는 참조는 금지다. `Tests/EditMode/Architecture/AsmdefReferenceTests.cs`가 `.asmdef` JSON을 읽어 이 표와 비교한다(IMP-FLOW 소유, 웨이브 4).

| 어셈블리 | 참조 가능 | 외부 패키지 |
|---|---|---|
| `Survivor.Core` | (없음) | UnityEngine 모듈만 |
| `Survivor.Data` | Core | Newtonsoft.Json |
| `Survivor.Simulation` | Core, Data | Unity.InputSystem(`KeyboardInputSource`) |
| `Survivor.Strategies` | Core | - |
| `Survivor.UI` | Core, Data, Simulation | UnityEngine.UI, Unity.TextMeshPro, Unity.InputSystem |
| `Survivor.App` | Core, Data, Simulation, Strategies, UI | UnityEngine.UI, Unity.InputSystem(`InputSystemUIInputModule`), Unity.RenderPipelines.Universal.Runtime(`Light2D`) |
| `Survivor.Dev` | Core, Data, Simulation, Strategies, App | Unity.InputSystem, Unity.RenderPipelines.Core.Runtime(`RenderPipelineManager`) |
| `Survivor.Editor` | (없음, `EditorPrefs` 키 문자열만 쓴다) | - |
| `Survivor.Tests.EditMode` / `.PlayMode` | Core, Data, Simulation, Strategies, UI, App | nunit, UnityEngine.TestRunner |
| `Survivor.Tests.Dev` | 위 전부 + Dev | nunit, UnityEngine.TestRunner |

**전수 점검 (2회차)** — 1회차 클래스 전부를 이 표에 대어 본 결과와 조치:

| 위반 (1회차) | 조치 (2회차) |
|---|---|
| `PrefabCatalog`(Simulation)가 `PooledObject`·`CollisionBodyLink`·셀프 티커(Strategies)를 붙임 | Core `IEntityTemplateDecorator` + Strategies 데코레이터 3종, App이 주입(5.6) |
| 셀프 티커(Strategies)가 `SimulationClock`·`SimulationContextHub`(Simulation)를 읽음 | Core `IStepClockReader`, `SimulationClock`이 구현, App이 `SelfTickStrategy`에 주입(5.8). 정적 허브 삭제 |
| `MonsterSpatialIndex.Rebuild`(Strategies)를 파이프라인에 넣으려면 Simulation의 `IStepPhaseHandler`가 필요("조립 시 연결"로 흐려져 있었음) | `SimulationPhase`·`IStepPhaseHandler`를 Core로 이동(5.3) |
| `EntityCategory`(Simulation)를 데코레이터(Strategies)가 알아야 함 | Core로 이동(5.4) |
| 엔진 충돌 바디·콜라이더 켜기를 `EntityLifecycle`(Simulation)이 알 수 없음 | Core `IPhysicsBodyRegistry` + `PhysicsBodySynchronizer`(Strategies), App이 주입(5.9.1) |
| App이 Dev를 참조(`App refs ... Dev`) → SPEC F19 4회차 "코드 제거 빌드" 불가 | Dev → App으로 뒤집고 `DevModeEntry` 자가 부착 + `defineConstraints`(5.15, D31) |
| Editor가 Dev를 참조 | 참조 제거(EditorPrefs 키만 씀) |
| `KeyboardInputSource`(Simulation)가 Input System 패키지를 쓰는데 1회차 표에 외부 참조 없음 | Simulation 외부 패키지 열에 명시 |
| `MovingObjectSpec.CollisionLayerTag` 별칭(Data) | Core `CollisionLayer` 직접 사용(5.11) |
| UI가 흐름 제어(App)를 불러야 함 | UI는 이벤트만 노출, `GameFlowController`(App)가 구독(5.17) |

- 이름 접두사는 쓰지 않는다. 네임스페이스와 asmdef로 충돌을 막는다. 다만 **클래스명은 프로젝트 전체에서 고유**해야 한다(Unity가 MonoBehaviour를 파일명으로 찾으므로, CODING_GUIDE §2.2).
- 프리팹 파일(`.prefab`)·UXML은 만들지 않는다. 템플릿은 `PrefabCatalog`, UI는 `UiFactory`가 코드로 만든다. 에이전트가 에디터 없이 작업할 수 있게 하고, 병렬 작업에서 YAML 충돌을 피하기 위해서다.

### 5c.2 파일 소유권 맵 (2회차 수정)

| 소유자 | 파일 (경로는 `Assets/` 기준) |
|---|---|
| **IMP-CORE** | `Scripts/Core/`: `Survivor.Core.asmdef`, `EntityId.cs`, `EntityIdAllocator.cs`, `DeterministicRng.cs`, `RngStreams.cs`, `StepMath.cs`, `CircleMath.cs`, `CollisionChannelTable.cs`, `CollisionLayerMapping.cs`(2회차), `TargetOrdering.cs`, `SpawnGeometry.cs`, `HealWeight.cs`, `WeightedSampler.cs`, `UpgradeOrdering.cs`, `StatMath.cs`, `MoveMath.cs`, `ProgressionRules.cs`(2회차), `KillRules.cs`(2회차), `MonsterSizeRules.cs`(2회차), `GameRules.cs`, `ProfilerMarkers.cs`, `ColliderActivationPolicy.cs`(+`PhysicsBodyRequirements`), `SimulationRules.cs`, `StrategySelection.cs`, `StepContext.cs`, `SimulationPhase.cs`(2회차 이동), `IStepPhaseHandler.cs`(2회차 이동), `IStepClockReader.cs`(2회차), `IEntityTemplateDecorator.cs`(2회차), `IPhysicsBodyRegistry.cs`(2회차), `EntityCategory.cs`(2회차 이동), `SimulationCommand.cs`, `ISimulationCommands.cs`, `IInputSource.cs`, `MoveCommand.cs`, `IPoolStrategy.cs`, `ITargetProvider.cs`, `MonsterSnapshot.cs`, `MonsterSnapshotBuffer.cs`, `TargetHit.cs`, `IUpdatable.cs`, `ITickStrategy.cs`, `ICollisionStrategy.cs`, `CollisionBody.cs`, `CollisionEvent.cs`, `CollisionEnums.cs`, `StrategyKinds.cs`, `ISpawnPolicy.cs`, `DespawnReason.cs`; `Tests/EditMode/Core/*` |
| **IMP-DATA** | `Scripts/Data/` 전부: `Survivor.Data.asmdef`, `Definitions/*.cs`, `GameData.cs`, `GameDataLoader.cs`, `GameDataRegistry.cs`, `JsonTreeReader.cs`, `Binders/*Binder.cs`(7개), `DataValidationReport.cs`, `ValidationIssue.cs`, `TunnelingInvariantValidator.cs`, `MovingObjectSpec.cs`, `PlayabilityChecker.cs`, `XpCurve.cs`, `AssetKeyRegistry.cs`, `AssetKeyResolver.cs`, `VisualAsset.cs`, `DefaultShapeFactory.cs`; `StreamingAssets/Data/*.json` 7개; `Tests/EditMode/Data/*`(픽스처 포함, E3, CP949 픽스처) |
| **IMP-LOOP** | `Scripts/Simulation/Loop/`: `SimulationClock.cs`, `GateCloseReason.cs`(2회차), `SimulationFrameCounter.cs`, `SimulationPipeline.cs`, `SimulationDriverEarly.cs`, `SimulationDriverMid.cs`, `SimulationDriverLate.cs`, `SimulationCommandBuffer.cs`, `ISimulationCommandHandler.cs`, `EntityDespawnCommandHandler.cs`(2회차), `PendingCommandQueue.cs`, `CollisionEventRouter.cs`, `ICollisionEventHandler.cs`, `TargetProviderSlot.cs`(2회차), `SimulationSettingsApplier.cs`(시간 설정만), `SimulationStateHash.cs`, `KeyboardInputSource.cs`; `Scripts/Simulation/Entities/`: `SimEntity.cs`, `EntityLifecycle.cs`, `LifetimeSystem.cs`(2회차), `PrefabCatalog.cs`, `PoolPrewarmCounts.cs`, `PresentationSync.cs`, `MonsterRegistry.cs`; `Scripts/Strategies/Tick/`: `CentralTickStrategy.cs`, `SelfTickStrategy.cs`, `MonsterSelfTicker.cs`, `ProjectileSelfTicker.cs`, `SelfTickTemplateDecorator.cs`(2회차); `Survivor.Simulation.asmdef`, `Survivor.Strategies.asmdef`; `Tests/EditMode/Loop/*`, `Tests/PlayMode/Loop/*`(고정 스텝·프레임레이트 결정론·게이트) |
| **IMP-POOL** | `Scripts/Strategies/Pool/`: `PooledObject.cs`, `PoolTemplateDecorator.cs`(2회차), `CustomPoolStrategy.cs`, `InstantiatePoolStrategy.cs`, `UnityObjectPoolStrategy.cs`; `Tests/PlayMode/Pool/*` |
| **IMP-SPATIAL** | `Scripts/Strategies/Spatial/`: `SpatialHash.cs`, `MonsterSpatialIndex.cs`, `SpatialHashSettings.cs`; `Scripts/Strategies/Targeting/`: `BruteForceTargetProvider.cs`, `SpatialHashTargetProvider.cs`, `Physics2DTargetProvider.cs`; `Tests/EditMode/Spatial/*`, `Tests/PlayMode/Targeting/*` |
| **IMP-COLLISION** | `Scripts/Strategies/Collision/`: `SelfCollisionStrategy.cs`, `EngineCollisionStrategy.cs`, `CollisionBodyLink.cs`, `PhysicsBodySynchronizer.cs`(2회차), `PhysicsSettingsApplier.cs`(2회차, 1회차 `PhysicsLayerSetup` 대체), `CollisionTemplateDecorator.cs`(2회차), `PairSet.cs`; `Tests/EditMode/Collision/*`, `Tests/PlayMode/Collision/*`(E1·E2 하네스) |
| **IMP-PLAYER** | `Scripts/Simulation/Player/`: `PlayerEntity.cs`, `PlayerMovement.cs`, `PlayerHealth.cs`, `PlayerDamageReceiver.cs`, `PassiveInventory.cs`, `PlayerProgressionApplier.cs`(2회차); `Scripts/Simulation/Weapons/`: `WeaponInventory.cs`, `WeaponInstance.cs`, `WeaponFactory.cs`, `ProjectileWeapon.cs`, `OrbitWeapon.cs`, `ZoneWeapon.cs`, `PlayerProjectile.cs`, `OrbitOrb.cs`, `DamageZone.cs`, `ContactDamageTracker.cs`, `WeaponDamageResolver.cs`(2회차), `PlayerHitHandler.cs`(2회차), `PlayerSpawnCommandHandler.cs`(2회차), `EvolutionService.cs`, `ChestEffectResolver.cs`, `ChestEffect.cs`, `WeaponLoadoutEntry.cs`(2회차); `Tests/EditMode/Player/*` |
| **IMP-MONSTER** | `Scripts/Simulation/Monsters/`: `Monster.cs`, `MonsterBehaviorState.cs`, `EnemyProjectile.cs`, `EnemyHazardZone.cs`, `MonsterSpawner.cs`, `SpawnScheduler.cs`, `TableSpawnPolicy.cs`, `DespawnSystem.cs`, `KillPipeline.cs`, `MonsterCommandHandler.cs`; `Scripts/Simulation/Pickups/`: `ExperienceGem.cs`, `Chest.cs`, `PickupSystem.cs`, `PickupRegistry.cs`(2회차, 활성 젬 수), `DropCommandHandler.cs`(2회차, 1회차 `DropService` 대체); `Tests/EditMode/Monsters/*` |
| **IMP-FLOW** | `Scripts/Simulation/Progression/`: `ExperienceSystem.cs`, `UpgradeOption.cs`, `LevelUpCandidateBuilder.cs`, `LevelUpSelector.cs`, `LevelUpSession.cs`(2회차), `StepOutcomeGate.cs`(2회차), `RunSession.cs`, `BestRecordStore.cs`; `Scripts/UI/` 전부(`Survivor.UI.asmdef`, `UiFactory.cs`(2회차), `TitleView.cs`, `HudView.cs`, `LevelUpView.cs`, `PauseMenuView.cs`, `ResultView.cs`, `DataErrorView.cs`, `UiInputHandler.cs`); `Scripts/App/`: `Survivor.App.asmdef`, `GameBootstrap.cs`(+`IDevModeHost` 구현, Global Light 2D 생성, (4회차) 부팅 시 최초 데이터 로드 호출 — 5.11.0), `IDevModeHost.cs`(2회차), `GameFlowController.cs`, `FlowState.cs`, `RunOutcome.cs`(2회차), `StrategyFactory.cs`, `DisplaySetup.cs`(2회차), `WindowSizeSelector.cs`(2회차), `CameraFollow.cs`, `BackgroundTiler.cs`; `Scenes/Main.unity`; `Tests/EditMode/Progression/*`, `Tests/EditMode/Architecture/AsmdefReferenceTests.cs`(2회차), `Tests/PlayMode/Flow/*` |
| **IMP-DEV** | `Scripts/Dev/` 전부(5.15 표의 클래스 + `Survivor.Dev.asmdef`); `Editor/DevModeMenu.cs`, `Editor/Survivor.Editor.asmdef`; `Tests/Dev/*`(+`Survivor.Tests.Dev.asmdef`) |
| **사용자** | `ProjectSettings/*`, `Packages/manifest.json`(패키지 추가), `Resources/AssetKeyRegistry.asset`, `UI/Fonts/*`(TTF·TMP Font Asset), TMP Essential Resources |
| **doc-writer** | `docs/devlog.md`(틀), `README.md`(3회차: 기능 브랜치 머지마다 갱신, 5c.2a) |

메모: `Survivor.Strategies.asmdef`는 IMP-LOOP가 소유한다(먼저 디스패치되는 쪽). 다른 Strategies 구현자는 스크래치에 스텁 asmdef를 둔다.

#### 5c.2a 기능별 필수 기록 초안 의무 (2회차 신규, SPEC F14 4회차 매핑 표, 3회차 수정)

SPEC F14는 "기능별 필수 기록이 없으면 그 기능은 미완료"라고 정했고, 기록 시점은 "기능 브랜치 머지 커밋에 해당 devlog/README 변경이 포함된다"로 검증한다. 기록 파일(`README.md`, `docs/devlog.md`)의 소유자는 doc-writer·사용자이므로, 구현자는 **완료 보고에 초안을 담는다**. (3회차) **구현자의 기록 초안은 해당 기능 브랜치 머지 전에 소유자(doc-writer 또는 사용자)가 README/devlog에 반영해 같은 머지 커밋에 포함한다.** 반영되지 않은 채 머지하지 않는다. team-lead는 초안이 없거나, 머지 커밋에 해당 기록 변경이 없으면 그 구현 작업을 완료로 받지 않는다. 측정 결과(가설에 대한 결과·해석)는 사용자가 측정을 마친 시점의 별도 커밋으로 devlog에 추가한다(측정은 웨이브 6 이후 사용자 수행이므로).

| 구현자 | 초안으로 낼 기록 | SPEC 매핑 |
|---|---|---|
| IMP-PLAYER | F1·F2·F6·F7 설계 이유 각 1단락 | 게임플레이 F1~F8 → README |
| IMP-MONSTER | F3·F4 설계 이유 각 1단락(젬 디스폰 규칙을 넣지 않은 이유 포함) | 게임플레이 → README |
| IMP-FLOW | F5·F8 설계 이유 각 1단락(게이트·우선순위·창 자동 선택 포함) | 게임플레이 → README |
| IMP-POOL | F9 측정 가설 + A(자체 풀)와 C(`ObjectPool<T>`)의 차이 한국어 설명 | 측정 F9 → devlog, SPEC F9 |
| IMP-SPATIAL | F16 측정 가설(교차점 예상 근거, 셀 크기 선택 이유) | 측정 F16 → devlog |
| IMP-LOOP | F17 측정 가설(호출 오버헤드 예상) + 고정 스텝·게이트 설계 요약 | 측정 F17 → devlog |
| IMP-COLLISION | F18 측정 가설 + E1·E2 실행 결과 요약(U6 결과 포함) | 측정 F18 → devlog |
| IMP-DATA | F11 스키마 문서 초안(필드·필수/선택·예시) + "JSON만으로 추가" 시연 절차 | F11 → README/docs (시연 기록 자체는 사용자) |
| IMP-DEV | F19 개발자 모드 사용법 절 초안(`--dev`, F1, 측정 절차, CSV 열 설명) + F10 측정 CSV 생성 확인 | F19 → README, F10 → 측정 CSV |

#### 5c.2b 구현자별 읽을 파일 (6회차 신규)

전원 공통 필수(행마다 반복하지 않는다): `spec/00-common.md`, `tech/README.md`, `tech/02-feature-tac.md`(자기 기능 TAC), `tech/5a-shared-rules.md`, `tech/5c-ownership-contracts.md`.

"필수"는 그 구현자가 소유한 파일(5c.2)을 만들기 위해 반드시 읽어야 하는 절이고, "계약만"은 소비자로서 시그니처만 알면 되는 절이다. 근거는 5c.2 소유권 맵의 파일·클래스 이름, 5.0.1 표의 구현 클래스 소속, 각 5.xx 제목의 소유·구현자 표기, 3절 기능 목록의 구현자 열이다. 선행 실측 U 번호는 5c.4 "선행 조건"과 6.3 집계에서, D 번호는 6.3 집계의 "PM 확인 권장"에서 옮겼다.

| 구현자 | spec/ 필수 | tech/ 필수 | tech/ 계약만 (소비자로서 인터페이스 절) | 비고 |
|---|---|---|---|---|
| IMP-CORE | (공통만) | `5.00-assemblies-interfaces.md`, `5.01-entity-id.md`, `5.02-rng.md`, `5.03-step-clock.md`, `5.04-commands-spawn.md`, `5.05-input.md`, `5.06-pool.md`, `5.07-targeting.md`, `5.08-tick.md`, `5.09-collision.md` | `5.15-dev-mode.md` | 5.3~5.9는 제목이 "Core 계약"이고 5c.2의 Core 파일(`IStepPhaseHandler`, `ISimulationCommands`, `IInputSource`, `IPoolStrategy`, `ITargetProvider`, `ITickStrategy`, `ICollisionStrategy`, `IPhysicsBodyRegistry` 등)이 거기서 정의된다. 5.15는 Core 소유 `SimulationRules.cs`의 `ChestDropChance`가 5.15에 적혀 있어서(0절 3회차 #5) 넣었으나 정의 전체가 5.15에 있는지는 본문 미확인이라 "계약만"에 둔다. 선행 실측 없음(웨이브 1) |
| IMP-DATA | `spec/F11-data-driven.md` | `5.11-data-loader.md`, `5.13-json-schemas.md` | `5.09-collision.md` | 5.9는 `MovingObjectSpec`이 Core `CollisionLayer`를 직접 쓰기 때문(5c.1 전수 점검표). 선행 실측 없음(웨이브 2, Core 확정) |
| IMP-LOOP | `spec/F17-update-strategy.md`, `spec/F01-player-move.md`(입력 소스 부분) | `5.03-step-clock.md`, `5.04-commands-spawn.md`, `5.05-input.md`, `5.06-pool.md`, `5.07-targeting.md`, `5.08-tick.md`, `5.10-lifecycle.md`, `5.16-state-hash.md` | `5.09-collision.md`, `5.11-data-loader.md`, `01-overview.md` | 5.7은 LOOP 소유 `TargetProviderSlot` 때문(5.0.1, 5c.3 #9). 5.6은 제목의 "IMP-LOOP 구현"(`SelfTickTemplateDecorator`). 5.9는 LOOP 소유 `CollisionEventRouter`·`ICollisionEventHandler`의 정의 위치가 제목으로는 확인되지 않아 "계약만". 01은 2.2(c)가 게이트 설계 근거(0절 A3)라서. 선행 실측 U1·U2·U31 |
| IMP-POOL | `spec/F09-pooling.md` | `5.06-pool.md` | `5.04-commands-spawn.md`, `5.10-lifecycle.md` | 5.4는 데코레이터가 쓰는 `EntityCategory`(5c.1 전수 점검표), 5.10은 풀의 유일한 호출자 `EntityLifecycle`(5c.3 #1). 선행 실측 없음 |
| IMP-SPATIAL | `spec/F16-targeting-strategy.md` | `5.07-targeting.md` | `5.03-step-clock.md`, `5.09-collision.md` | 5.3은 `MonsterSpatialIndex`가 구현하는 `IStepPhaseHandler`(5.0.1). 5.9는 `Physics2DTargetProvider`(C)가 엔진 바디·`PhysicsBodySynchronizer`에 기대는지 제목만으로 판단할 수 없어 "계약만". 5c.4에 선행 실측 없음 |
| IMP-COLLISION | `spec/F18-collision-strategy.md` | `5.09-collision.md`, `5.06-pool.md` | `5.03-step-clock.md`, `5.04-commands-spawn.md`, `5.10-lifecycle.md` | 5.6은 제목의 "IMP-COLLISION 구현"(`CollisionTemplateDecorator`). 5.3은 `PhysicsBodySynchronizer`의 단계 핸들러 계약, 5.4는 `EntityCategory`, 5.10은 `IPhysicsBodyRegistry`를 부르는 쪽. 선행 실측 U5·U7·U8·U30·U32 |
| IMP-PLAYER | `spec/F01-player-move.md`, `spec/F02-auto-attack.md`, `spec/F06-weapons.md`, `spec/F07-evolution.md`, `spec/F04-drops-pickup.md`(상자 효과), `spec/F05-level-up.md`(선택 적용) | `5.12-damage-rules.md`, `5.14-progression.md` | `5.02-rng.md`, `5.03-step-clock.md`, `5.04-commands-spawn.md`, `5.05-input.md`, `5.07-targeting.md`, `5.08-tick.md`, `5.09-collision.md`, `5.10-lifecycle.md`, `5.11-data-loader.md`, `5.13-json-schemas.md`, `5.15-dev-mode.md` | 5.14는 `PlayerProgressionApplier`(0절 A11)·`EvolutionService`·`ChestEffectResolver`. 5.10은 궤도 원 수명 없음 규약(`Spawn<T>` 주석, 5회차) 때문에 해당 주석은 정독한다. 5.13은 무기·패시브·플레이어 스탯 정의의 소비자라서 "계약만". 5.15는 개발자 무기 구성 처리(0절 A11)의 입력 쪽만. 선행 실측 없음 |
| IMP-MONSTER | `spec/F03-monsters.md`, `spec/F04-drops-pickup.md` | `5.12-damage-rules.md` | `5.02-rng.md`, `5.03-step-clock.md`, `5.04-commands-spawn.md`, `5.08-tick.md`, `5.09-collision.md`, `5.10-lifecycle.md`, `5.11-data-loader.md`, `5.13-json-schemas.md`, `5.15-dev-mode.md` | 제목에 IMP-MONSTER가 적힌 5.xx는 5.12뿐이다. 5.4는 `MonsterCommandHandler`·`DropCommandHandler`가 구현하는 `ISimulationCommandHandler`, 5.8은 `Monster`·`EnemyProjectile`의 `IUpdatable`, 5.9는 `PickupSystem`의 `ICollisionEventHandler`(채널 6~8), 5.13은 몬스터·스폰 테이블 정의 소비, 5.15는 `SimulationRules.ChestDropChance`·`SpawnPolicyOverride`(0절 3회차 #3·#5). 선행 실측 없음 |
| IMP-FLOW | `spec/F05-level-up.md`, `spec/F08-stage-flow.md`, `spec/F01-player-move.md`(카메라 스냅), `spec/F13-build-demo.md`(창 설정 코드) | `5.03-step-clock.md`, `5.11-data-loader.md`(5.11.0), `5.14-progression.md`, `5.15-dev-mode.md`(`IDevModeHost`), `5.17-flow-state.md`, `5.18-screen.md` | `5.04-commands-spawn.md`, `5.05-input.md`, `5.06-pool.md`, `5.07-targeting.md`, `5.08-tick.md`, `5.09-collision.md`, `5.10-lifecycle.md`, `5.16-state-hash.md`, `5b-external-settings.md` | 5.3은 FLOW 소유 `StepOutcomeGate`와 `Open`/`Close`의 게이트 규칙(5c.3 #11). 5.11.0은 `GameBootstrap`이 최초 로드를 부른다(0절 4회차 ③). 5.14는 FLOW 소유 `LevelUpSession`·`LevelUpCandidateBuilder`·`StepOutcomeGate`(0절 A4·A5). 5.15는 FLOW 소유 `IDevModeHost.cs`의 정의(0절 3회차 #3)만 필요하다. "계약만" 9개는 조립 루트(`GameBootstrap`·`StrategyFactory`)가 주입하는 대상이다(5c.1 전수 점검표). 선행 실측 U35·U36·U42(권장). D25 |
| IMP-DEV | `spec/F19-dev-mode.md`, `spec/F10-measurement.md` | `5.15-dev-mode.md`, `5.05-input.md` | `5.03-step-clock.md`, `5.04-commands-spawn.md`, `5.07-targeting.md`, `5.09-collision.md`, `5.11-data-loader.md`, `5.14-progression.md`, `5.16-state-hash.md`, `5.17-flow-state.md`, `5b-external-settings.md` | 5.5는 제목의 "IMP-DEV 자동 경로"(`AutoPathInputSource`). 5.9는 `LoggingCollisionStrategy` 데코레이터와 `CollisionEvent.CompareTo` 로그 순서. 5.3은 배속·`StopAtStep`·`EndStep`, 5.11은 핫 리로드, 5.14는 개발자 무기 구성, 5b는 URP 시각화(U33·U34). `ISpawnPolicy`(`DevSpawnPolicy`)의 정의 절은 제목으로 확인되지 않아 5.4를 "계약만"에 둔다. 선행 실측 U33·U38·U40(권장). D29·D31 |
| doc-writer | `spec/F12-readme.md`, `spec/F14-quality-records.md` | `01-overview.md`, `5.00-assemblies-interfaces.md` | (없음) | 웨이브 6 다이어그램 검증 기준이 5.0.1과 2.2(c)다(5c.4 웨이브 6). 기능별 기록 초안의 내용은 각 구현자 완료 보고에서 받는다(5c.2a) |
| tester | 시험하는 기능 그룹의 `spec/F<nn>-*.md`만 | 시험하는 기능의 TAC(`02-feature-tac.md` 해당 절)만 | 시험 대상 클래스가 구현하는 5.xx | 공통 필수 중 `02-feature-tac.md`는 해당 TAC만, `5c-ownership-contracts.md`는 5c.5 테스트 전략 위주로 읽는다. 실측 항목을 확인할 때는 `06-open-questions.md` 6.2의 해당 U |
| 사용자 (F13~F15·5b) | `spec/F13-build-demo.md`, `spec/F14-quality-records.md`, `spec/F15-dev-environment.md`, `spec/F10-measurement.md`(측정 수행 시) | `5b-external-settings.md`, `01-overview.md`(2.3 U 표), `06-open-questions.md`(6.2) | `5.15-dev-mode.md`, `5.18-screen.md` | 웨이브 0 실측 스파이크 (a)~(j) = U1·U2·U5·U7·U8·U9·U11·U30·U31·U32·U35·U36(5c.4 메모). 5.15는 F10 측정 절차(5.15.2·5.15.3), 5.18은 창·빌드 설정(0절 A13)이 5b와 나뉘어 있어서 |

읽기 범위는 디스패치 프롬프트에 이 표의 행을 그대로 옮긴다. 표에 없는 파일을 구현자가 열어야 했다면 그것은 이 표의 결함이므로 완료 보고에 적게 한다.

### 5c.3 계층 간 핵심 계약 요약 (구현자가 반드시 지킬 것)

1. (2회차 수정) 엔티티 생성·회수는 Lifecycle 단계에서 `EntityLifecycle.Spawn/Despawn`만 쓴다(R11). 다른 단계에서는 `SimulationCommand`를 넣는다. 풀·ID 할당기·물리 바디를 직접 부르지 않는다.
2. (2회차 수정) `IUpdatable.Tick`은 자기 상태만 쓰고, 부작용은 `context.Commands.Enqueue`로 넘긴다. 생성 스텝에는 Tick이 오지 않는다(5.4).
3. 집합 순회·피해 적용은 `EntityId` 오름차순이다(R10). `Dictionary`/`HashSet`를 순서가 중요한 곳에서 순회하지 않는다.
4. 시간은 스텝 정수, 속도는 스텝당 값으로만 계산한다. 초 단위 상수가 코드에 나타나면 안 된다(R1·R3).
5. 난수는 `RngStreams.Get(stream)`만 쓴다. 스트림 배정: 스폰 종·각도 = Spawn, 소환 각도 = Summon, 상자 드랍 = Drop, 상자 효과 = Chest, 레벨업 = LevelUp.
6. 위치의 원본은 `SimEntity.Position`이다. `transform.position`은 `PresentationSync.LateUpdate`(실행 순서 −100)가 프레임당 1회 쓴다. 단 `Body.DrivesTransform == true`인 엔진 모드 바디는 쓰지 않는다 [실측 U10]. `Rigidbody2D`·`Collider2D`는 `PhysicsBodySynchronizer`만 만진다(5.9.1).
7. (2회차 수정) 오류 규약: 계약 위반(중복 반환, 같은 채널에 핸들러 두 번 등록, 같은 순서값의 단계 핸들러, Lifecycle 밖 생성, 스텝 밖 `CloseAfterCurrentStep` 등)은 `InvalidOperationException`을 던진다. 데이터 오류는 던지지 않고 `DataValidationReport`에 넣는다. 런타임의 예상 가능한 실패(타깃 없음 등)는 `bool Try...` 패턴으로 처리한다. 예외를 잡는 곳은 드라이버 3개 각각 한 곳이고, 잡으면 `[시각] [ERROR] phase=<단계> step=<n> <메시지>`를 남긴 뒤 `SimulationClock.AbortStep()`(그 스텝 중단, 게이트 `Error`로 닫힘)을 부르고, FLOW가 결과 화면이 아니라 오류 표시로 간다(CODING_GUIDE §4·§5).
8. 로그 형식: `[HH:mm:ss.fff] [LEVEL] [Area] message` (Area = Data/Loop/Pool/Collision/Flow/Dev). 판 시작·종료·레벨업·진화·핫 리로드·전략 변경·측정 시작/종료는 성공해도 한 줄을 남긴다(§5). 스텝마다 남기는 로그는 금지한다(측정 오염).
9. (2회차 수정) 단계 핸들러 등록 순서(`order`)는 아래로 고정한다. 바꾸려면 PM 승인이 필요하다.

| 단계 | order: 핸들러 (소유자) |
|---|---|
| Begin | 0 `PendingCommandQueue`(LOOP: 개발자 명령, `SwapData`, `SwitchTargetProvider`), 10 입력 소비 `PlayerMovement`(PLAYER, `ReadMove` 캐시) |
| PlayerMove | 0 `PlayerMovement`(PLAYER) → 드라이버가 `StepContext` 확정 |
| MonsterTick | (전략) |
| BuildIndex | 0 `MonsterRegistry` 스냅샷(LOOP), 5 `PhysicsBodySynchronizer`(COLLISION, R19가 요구할 때만), 10 `MonsterSpatialIndex`(SPATIAL, `Required`일 때만). (4회차) 핸들러가 모두 끝난 직후 `SimulationDriverMid`가 `TargetProviderSlot.OnSnapshotBuilt(snapshot)`을 **직접 호출**한다(LOOP → SPATIAL 구현). `TargetProviderSlot`은 단계 핸들러가 아니다(5.7) |
| WeaponTick | 0 `WeaponInventory`(PLAYER, 슬롯 순서: 발사 명령·궤도 원 위치·자식 보정), 10 젬 끌림 `PickupSystem`(MONSTER) |
| ProjectileTick | (전략) |
| Collision | 0 `CollisionEventRouter` 수집(LOOP) |
| Resolve | 0 라우터 디스패치(LOOP: 채널 0 투사체 명중·소모, 1·2 트래커 Enter/Exit, 3·5 겹침 기록, 4 M3 투사체 후보·소모, 6 젬 끌림 시작, 7 젬 획득(경험치), 8 상자 효과(진화·강화·**회복**)), 10 `WeaponDamageResolver` 지속 피해(PLAYER), 20 `PlayerDamageReceiver`(PLAYER: 회복 뒤 피해), 30 `KillPipeline`(MONSTER: ID 순 처치 확정 → 처치 수 → 드랍 판정 → 명령) |
| Lifecycle | 0 `SimulationCommandBuffer`(LOOP: 정렬 → 생성·회수 핸들러), 10 `LifetimeSystem`(LOOP; (5회차) `ExpireStep = long.MaxValue`인 수명 없는 엔티티는 건너뜀), 20 `DespawnSystem`(MONSTER), 30 `MonsterSpawner`(MONSTER: 일반 스폰·보스), 90 `StepOutcomeGate`(FLOW: 사망 > 클리어 > 레벨업, 게이트 닫기) |
| End | (3회차) 단계 핸들러 없음 — `SimulationDriverLate`가 Lifecycle 뒤에 `SimulationClock.EndStep`을 **직접 호출**한다(LOOP) |

10. (2회차 수정) 데이터 접근: 보유·활성 객체(무기 인스턴스, 몬스터, 보유 패시브)는 생성 시점의 정의 참조를 보관한다. 스폰 테이블·플레이어 기본 스탯(체력·최대 체력 제외)·레벨업 풀·조합 규칙은 참조할 때마다 `GameDataRegistry.Current`를 읽는다(5.11.1).
11. (2회차 신규) 게이트: 스텝 안 닫기 = `StepOutcomeGate`와 `EndStep`의 `CloseAfterCurrentStep`, 예외 중단 = 드라이버의 `AbortStep`, 스텝 밖 닫기 = FLOW의 `Close`, 열기 = FLOW의 `Open`만(5.3).
12. (2회차 신규) 겹침·쿨다운 기록을 가진 클래스는 `EntityLifecycle.Despawned`를 구독해 대상 기록을 지운다(TAC-0-10).

### 5c.4 권장 디스패치 순서 (2회차 수정)

| 웨이브 | 구현자 (병렬) | 선행 조건 | 산출물 검증 |
|---|---|---|---|
| 0 | 사용자: Unity 프로젝트 생성(URP 2D)·설정(5b 앞 5줄), **Unity 동작 실측 스파이크**(아래 (a)~(j)) | 없음 | 실측 결과를 RUN_LOG에 기록 |
| 1 | **IMP-CORE** (단독) | 웨이브 0의 프로젝트 폴더 | EditMode Core 테스트 통과 |
| 2 | **IMP-DATA**, **IMP-LOOP**, **IMP-POOL**, **IMP-SPATIAL** | Core 확정. LOOP는 U1·U2·U31 결과 필요 | 각자 EM 테스트 |
| 3 | **IMP-COLLISION**, **IMP-PLAYER**, **IMP-MONSTER** | 웨이브 2 조립 완료(공간 인덱스, 생명주기·명령 버퍼, 정의 객체). COLLISION은 U5·U7·U8·U30·U32 결과 필요 | EM + PM(E1·E2 하네스 골격) |
| 4 | **IMP-FLOW** (조립 루트·UI·흐름·화면 설정) | 웨이브 3(특히 `PlayerProgressionApplier`). U35·U36·U42 결과 권장 | 타이틀 → 플레이 → 레벨업 → 결과 PM 테스트, 게이트 결정론, 전략 조합 결정론, asmdef 참조 테스트 |
| 5 | **IMP-DEV** | 웨이브 4(`IDevModeHost`). U33·U38·U40 결과 권장 | N스텝 자동 종료, 측정 가드·CSV 열, 충돌 로그, E1·E2 실행, `SURVIVOR_NO_DEV` 빌드 컴파일 |
| 6 | (3회차) doc-writer(README·devlog **최종 정리와 다이어그램 검증**(5.0.1·2.2(c) 기준)만. 기능별 기록은 웨이브 1~5의 각 기능 브랜치 머지 때 이미 반영됨, 5c.2a), 사용자(F10 측정·F13·F14) | 웨이브 5 | 다이어그램과 5.0.1 인터페이스 목록 일치 |

**1회차 대비 변경**: 웨이브 구성과 순서는 같다. 바뀐 것은 ① 웨이브 0 스파이크 항목이 5개에서 10개로 늘었고(U30·U31·U32·U35·U36 추가), ② 웨이브별 선행 실측이 구체화됐으며(LOOP는 U31, COLLISION은 U30·U32), ③ IMP-DEV가 IMP-FLOW의 `IDevModeHost`에 의존하게 됐다(Dev → App, 원래도 웨이브 4 뒤였으므로 순서 영향 없음), ④ 웨이브 4 검증에 asmdef 참조 테스트와 게이트 결정론이 추가됐다. (3회차) ⑤ 기능별 기록 반영은 각 웨이브의 기능 브랜치 머지에 묶이고, 웨이브 6은 최종 정리·다이어그램 검증으로 줄었다.

웨이브 0 실측 스파이크 메모: 빈 URP 2D 프로젝트에 스크립트 1개를 넣어 (a) 서로 다른 `DefaultExecutionOrder`의 `FixedUpdate` 순서(U1), (b) `Time.captureDeltaTime = 0.1`에서 프레임당 `FixedUpdate` 횟수(3이어야 함, U2), (c) `simulationMode = Script` + `Simulate()` 호출 중 키네마틱-키네마틱 트리거 Enter/Exit 발생 여부와 시점(U5·U7), (d) `callbacksOnDisable = false`에서 비활성화 시 Exit 유무(U8), (e) `MovePosition` 뒤 `OverlapCircle` 반영 시점(U11), (f) Late 순서 `FixedUpdate` 안에서 켠 컴포넌트가 같은 프레임 다음 `FixedUpdate`에 호출되는지(U31), (g) `rb.position` 직접 대입 + `SyncTransforms` 뒤 `OverlapCircle`이 트리거를 찾는지(U32), (h) `defaultContactOffset` 최솟값과 반지름 합 경계 겹침 판정(U30·U9), (i) `Screen.mainWindowDisplayInfo.workArea` 값과 `SetResolution` 뒤 클라이언트 크기(U35), (j) Allow Fullscreen Switch 끔에서 Alt+Enter(U36)를 로그로 확인한다. 구현 코드가 아니라 실측용이므로 결과만 RUN_LOG에 남기고 버린다. 이 결과에 따라 5.9 엔진판 바디 구성(Kinematic인지 Dynamic + gravityScale 0인지)이나 F17 A의 생성 시점이 바뀔 수 있다.

### 5c.5 테스트 전략 (2회차 추가 항목 표시)

| 대상 | 위치 | 방법 |
|---|---|---|
| 공유 규칙 R1~R30 | EM | 경계값 표 테스트(0.5스텝 반올림, 음수 좌표 셀 키, `(1−r)²` 끝점, 동거리 타이브레이크, R22 경계, R23 상한, R26 N=400/401, R27 분모 0, R28 문화권·따옴표, R29 스테일 회차) |
| RNG 분포 | EM | 시드 고정: 상자 10,000회 ≈ 2%(±0.4%p), 상자 효과 1,000회 70:30(±3%p), 레벨업 1,000회 빈도 순서, 회복 빈도의 체력 단조성 |
| 스폰 스케줄 | EM | 구간별 60초 스폰 수 = rate × 60 정확히, 상한 시 버림, 구간 밖 종 0 |
| JSON 로더·검증 | EM | 7종 × (정상 / 깨진 JSON / 필수 누락 / 타입 오류 / ID 중복 / 참조 오류 / 알 수 없는 키 / 에셋 키 없음 / (2회차) CP949 바이트) 픽스처. 필수 4종 차단, 선택 3종 폴백, 플레이 불가 4기준 |
| **E3** | EM | `GameDataLoader.LoadAll(<프로젝트>/Assets/StreamingAssets)` → `TunnelingInvariantValidator.EnumerateMovingObjects` 전부가 `v/60 ≤ r`인지 단언. 게임을 돌리지 않는다 |
| 타깃 공급자 동등성 | EM(A·B) / PM(C) | 무작위 배치 10,000건 + 격자점 동거리 배치 + 큰 몬스터 포함. 반환 `TargetHit` 비트 일치. (2회차) C는 자체 충돌 조합과 엔진 충돌 조합 둘 다 |
| 자체 충돌 Enter/Exit | EM | 스크립트 배치 시퀀스로 기대 이벤트 목록과 비교. 등록 해제 시 Exit 없음, 지속 겹침 스텝 이벤트 없음 |
| 피해 틱 | EM | `ContactDamageTracker`: Enter 즉시 1회, 간격, Exit 후 재진입 악용 불가, 세대 다른 같은 인덱스에 기록 미적용. `PlayerDamageReceiver`: 400기 겹침에도 무적 스텝마다 1회, (2회차) 겹친 몬스터 디스폰 뒤 피해 없음 |
| 고정 스텝 | PM | `Time.captureDeltaTime`을 1/30, 1/60, 1/144로 바꿔 각각 600스텝 → `SimulationStateHash` 일치. 1/10에서 프레임당 스텝 ≤ 3 [실측 U3] |
| 게이트 (2회차) | PM | 레벨업·사망·클리어가 스텝 s에서 일어나는 시나리오 × 3 FPS: 닫힌 뒤 `Step == s+1`, 해시 일치. 연속 3레벨: 회차 1·2·3이 스텝 없이 열림. 같은 프레임 키+클릭: 수락 1회. 단계 중 예외: `AbortStep` 후 `Step` 불변·오류 표시 |
| 생성 시점 (2회차) | PM | W3 첫 피해 스텝 = 발사 스텝 + 1을 충돌 2 × 틱 2 조합에서 확인 |
| 전략 조합 결정론 | PM | 시드 고정, 레벨업 비활성, 자동 경로, 무적, 무기 W1~W4 5단계, N=200 유지, 1,800스텝. 조합 3×3×2×2 = 36개 전부의 상태 해시 일치(시간이 오래 걸리면 기준선 대비 단일 변경 7개 + 전부 켬 1개로 줄인다) |
| **E1** | PM | 위 시나리오(타깃은 공간 해시, D30)를 `Collision = Self`와 `Engine/Discrete`로 각각 3,600스텝 실행하고 `LoggingCollisionStrategy` 로그를 (스텝, 채널, A, B, 종류) 집합으로 비교((4회차) `CollisionEvent.CompareTo` 순서). 다르면 첫 불일치 스텝과 두 바디의 거리·반지름 합을 출력 |
| **E2** | PM | 같은 시나리오를 `Engine/Discrete`와 `Engine/Continuous`로 실행해 같은 방식((4회차) 같은 (스텝, 채널, A, B, 종류) 키)으로 비교. U6 실측 결과에 따라 "트리거에는 CCD가 적용되지 않아 자명하게 같음"이 확인되면 그 사실을 devlog에 기록한다 |
| 흐름 | PM | 타이틀 → 시작 → 사망 → 결과 → 재시작 → 상태 초기화(ID 1:1부터, 스텝 0, 풀 활성 0). (2회차) ESC 중 레벨업 선택지 유지, 타이틀로 나가기 기록 미갱신, 포커스 상실 일시정지 |
| 화면 (2회차) | EM / PM | `WindowSizeSelector.Choose` 표(1920x1040 → 1600x900, 1280x680 → 1280x720 등) / 세 해상도 UI 스크린샷 |
| asmdef 참조 (2회차) | EM | 5c.1 참조 방향 표와 `.asmdef` 비교 |
| 개발자 모드 | PM(`Tests/Dev`) | N스텝 자동 종료 정확성, 측정 가드 4조건 거부, 워밍업 300스텝 제외, CSV 헤더 키·본문 열 이름과 순서, BOM, 시각화 끔 시 콜백 해제, `--dev` 없는 실행에서 Dev 타입 인스턴스 0개, 최고 기록 미저장 |

실행: 에디터 Test Runner 또는 `Unity.exe -batchmode -projectPath Survivor -runTests -testPlatform EditMode -testResults em.xml`(PlayMode도 같은 방식) [실측 U27]. 개발자 모드의 Development Build 측정(F10)은 사용자가 수행한다.

---

