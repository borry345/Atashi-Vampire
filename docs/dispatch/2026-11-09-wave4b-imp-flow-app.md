# 웨이브 4b 디스패치 브리핑 — IMP-FLOW-APP (2026-11-09, PM 작성, **초안 — 4a 머지 + 사용자 선행 조건 + 승인 뒤 디스패치**)

> `general-purpose`, `model: opus`, `isolation: worktree`. "=== 프롬프트 시작 ===" 이후를 그대로 보낸다(또는 worktree 안의 이 파일을 읽게 한다).
> **선행 조건 (디스패치 전 PM이 확인)**:
> 1. 4a(IMP-FLOW-SIM) main 머지 — 4a 보고(2026-11-09)의 시그니처를 1절 "FLOW(4a)" 항목에 반영 완료. 머지 뒤 기준선 숫자(EM 667 / PM 202)만 어셈블리 결과로 재확인한다.
> 2. **사용자**: ✓ 완료(2026-11-09, PM 확인). TMP Essential Resources는 `Assets/TextMesh Pro/`에, 한글 TMP Font Asset은 **`Assets/TextMesh Pro/Fonts/Noto_Sans_KR/NotoSansKR-VariableFont_wght SDF.asset`**(GUID `c1e7308fdc465514fb90510f9257fb1b`)에 있다 — 5b·5c.2의 `Assets/UI/Fonts/`가 아니라 이 경로가 실제다(TECH 18회차에 반영 예정). 아틀라스는 **Static** 4096², 포인트 30, 글리프 11,970개(한글 음절 11,172 전부 + ASCII 95 + 기호 706 — 화살표·수학 기호 포함). Dynamic이 아니므로 런타임 글리프 추가는 없고 이 밖의 문자는 네모로 나온다 → UI 문자열은 한글·ASCII·기본 기호만 쓴다.
> 3. **사용자**: Player Settings(5b — Windowed 1280x720, Resizable·Allow Fullscreen Switch·Run In Background 끔, Active Input Handling = Input System Package). 4b가 끝난 뒤 `ProjectSettings/EditorBuildSettings.asset`의 씬 목록에 `Assets/Scenes/Main.unity`를 추가(사용자 소유 파일이라 구현자는 건드리지 않는다. PlayMode 테스트는 `EditorSceneManager.LoadSceneInPlayMode`로 경로 로드하므로 빌드 목록 없이도 돈다).
> 4. 기준선 숫자(EM·PM)를 4a 머지 뒤 값으로 바꾼다.

=== 프롬프트 시작 ===

# 역할: IMP-FLOW-APP — 뱀서라이크 1차 MVP의 조립 루트·흐름·UI·화면 설정 구현자

당신은 `Survivor.App`·`Survivor.UI` 어셈블리 전체와 씬 `Main.unity`를 만든다. 웨이브 1~4a가 만든 모든 객체를 **`GameBootstrap` 한곳에서 생성·배선**하고(5c.3 #9 메모·16회차 배선 규칙: 소유자는 핸들러만 공개하고 등록 코드는 조립 루트가 갖는다), `GameFlowController`가 게이트 닫힘 이유를 폴링해 타이틀 → 플레이 → 일시정지 → 레벨업 → 결과 → 데이터 오류 상태를 바꾸며, uGUI 뷰 6개가 그것을 표시한다. 당신이 끝나면 **게임이 처음으로 실행된다**. 개발자 모드(`Survivor.Dev`)는 웨이브 5이고, 당신은 그것이 꽂힐 `IDevModeHost`를 `GameBootstrap`에 구현만 한다.

## 1. 이미 확인된 사실 (다시 조사하지 말 것)

- **자기 전용 git worktree**에서 작업한다. cwd가 worktree 루트, Unity 프로젝트는 `<cwd>/Survivor`. 다른 경로는 읽지도 쓰지도 않는다. 동시에 도는 구현자는 없다.
- Unity **6000.3.28f1**, 에디터 `C:\Program Files\Unity\Hub\Editor\6000.3.28f1\Editor\Unity.exe`, GUI는 닫혀 있다. URP 2D 템플릿 프로젝트(`Assets/Settings/Renderer2D.asset`·`UniversalRP.asset`, `Lit2DSceneTemplate`), 패키지 URP 17.3.0·ugui 2.0.0(TMP 포함)·InputSystem 1.20.1·Newtonsoft 3.2.2. `Assets/Settings/InputSystem_Actions.inputactions`(템플릿 기본)가 있다. **TMP Essential Resources와 한글 TMP Font Asset은 사용자가 넣어 두었다**(`Assets/TextMesh Pro/`, 폰트는 위 헤더 2번 경로) — worktree에 없으면 BLOCKED로 보고하고 멈춘다.
- 기준선(main, 4a 머지 뒤): **EditMode 667 / PlayMode 202**, 전부 통과. 당신은 EditMode(`Architecture/`, `App/`)와 PlayMode(`Flow/`)를 추가한다.
- 씬: `Assets/Scenes/SampleScene.unity`(URP 2D 템플릿: `Main Camera`, `Global Light 2D`)만 있다. **`Main.unity`는 당신이 만든다** — PM [기본값]: `SampleScene.unity`를 복사해 `Main.unity`로 두고(`Global Light 2D`가 이미 있으면 `GameBootstrap`은 만들지 않는다, 5b), `GameBootstrap` 컴포넌트를 가진 빈 GameObject 하나를 추가한다(YAML 직접 편집 또는 에디터 스크립트 1회 실행 — 방법은 당신이 정하되 결과 씬을 PlayMode 테스트가 `EditorSceneManager.LoadSceneInPlayMode`로 열어 검증한다). 나머지(드라이버 3개, `PresentationSync`, `SimulationFrameCounter`, `KeyboardInputSource`, Canvas·EventSystem·뷰, `CameraFollow`·`BackgroundTiler`)는 **`GameBootstrap`이 런타임에 코드로 만든다**(씬에 손으로 배치한 참조가 없어야 테스트 리그가 같은 경로를 탄다). `SampleScene.unity`는 지우지 않는다(빌드 목록은 사용자 소유 `ProjectSettings/`).
- **부팅 순서(5.11.0, `GameBootstrap.Awake`)**: `SimulationSettingsApplier.Apply()`(Simulation, 고정 스텝·시간) → `PhysicsSettingsApplier.Apply()`(Strategies) → `DisplaySetup.Apply()`(App, `Screen.SetResolution`의 유일한 호출자, 1회) → `GameDataLoader.LoadAll(Application.streamingAssetsPath)` → `new GameDataRegistry()` + `Initialize(data, report)` → `report.IsPlayable`이면 `FlowState.Title`, 아니면 `FlowState.DataError`(TAC-F8-4: 시작 버튼 비활성 + `BlockingIssues`를 파일명·정의 ID·필드와 함께 표시). 스폰 테이블 `bossMonsterId` 무효는 Warning만 — 시작 허용(TAC-F8-10, 경고 로그 "보스 정의가 없어 클리어할 수 없다"는 로더가 남긴다고 가정 — 로더 코드에서 확인, 없으면 보고).
- **조립 순서 = 5c.3a 조립 표 그대로**(생성자 인자는 코드 이름) + 4a가 추가한 FLOW 객체. 아래는 PM이 코드에서 확인한 시그니처다:
  - LOOP: `new SimulationClock()`, `new SimulationPipeline()`, `new SimulationRules()`(필드 기본값), `new SimulationCommandBuffer()`, `new MonsterSnapshotBuffer()`, `new EntityHandleAllocator()`, `new RngStreams(seed)`, `new TargetProviderSlot(clock)` → `Replace(provider)`, `new PrefabCatalog(entityComponentTypes, decorators)`(8 카테고리 사전: `PlayerProjectile→PlayerProjectile`, `PlayerOrb→OrbitOrb`, `PlayerZone→DamageZone`, `Monster→Monster`, `EnemyProjectile→EnemyProjectile`, `EnemyZone→EnemyHazardZone`, `Gem→ExperienceGem`, `Chest→Chest`; 데코레이터는 전략 선택에 따라 `PoolTemplateDecorator`(풀이 CustomPool/UnityObjectPool일 때 — 5.6에서 확인)·`SelfTickTemplateDecorator`(틱이 PerObjectFixedUpdate일 때)·`CollisionTemplateDecorator()`(엔진 충돌일 때 — 5.9에서 확인)), `new EntityLifecycle(pool, ids, ticks, collisions, physicsBodies, prefabs, clock)`, `new MonsterRegistry(lifecycle, snapshot)`, `new CollisionEventRouter(strategy)`, `new PendingCommandQueue(targets, data, rules)`(+ `BossSpawnedThisRun = () => spawner.BossSpawnedThisRun`), `new LifetimeSystem(lifecycle)`, `new EntityDespawnCommandHandler(lifecycle)`. 드라이버(MonoBehaviour, `AddComponent` 뒤 `Initialize`): `SimulationDriverEarly.Initialize(clock, pipeline, rules, targets, commands, () => lifecycle.Ticks, () => player.Position)`, `SimulationDriverMid.Initialize(clock, pipeline, () => lifecycle.Ticks, snapshot, targets)`, `SimulationDriverLate.Initialize(clock, pipeline, rules)`, `PresentationSync.Initialize(lifecycle)`, `SimulationFrameCounter.Initialize(clock)`. `KeyboardInputSource`는 MonoBehaviour(`AddComponent`). **조립 예시 코드: `Tests/PlayMode/Loop/LoopPmRig.cs`(LOOP 전체), `Tests/PlayMode/Collision/CollisionPmUtil.cs`의 `CollisionPmWorld`(엔진·자체 충돌 + 동기화기 + 공간 인덱스), `Tests/EditMode/Player/PlrRig.cs`·`Tests/EditMode/Monsters/MonEmRig.cs`, 그리고 4a의 `Tests/EditMode/Progression/` 전체 조립 리그** — 읽고 따라 하되 수정하지 않는다.
  - Strategies(`StrategyFactory`가 `StrategySelection`에서 만든다, 5c.1: App만 Strategies를 참조): 풀 `new InstantiatePoolStrategy()`/`new CustomPoolStrategy()`/`new UnityObjectPoolStrategy()`(`PoolStrategyKind`), 틱 `new CentralTickStrategy()`/`new SelfTickStrategy(clock)`(`TickStrategyKind`), 타깃 `new BruteForceTargetProvider()`/`new SpatialHashTargetProvider(index)`/`new Physics2DTargetProvider(bodies, monsterLayerMask)`(`TargetProviderKind`; 마스크는 `CollisionLayerMapping`(Core)에서 몬스터 레이어 — 코드 확인), 충돌 `new SelfCollisionStrategy(index, snapshot)`/`new EngineCollisionStrategy(bodies, selection.EngineDetectionMode)`(`CollisionStrategyKind`). 공용: `new MonsterSpatialIndex(selection.SpatialHashCellSize, snapshot)`(BuildIndex order 10, `Required` 플래그), `new PhysicsBodySynchronizer(ColliderActivationPolicy.Resolve(in selection))`(BuildIndex order 5, `IPhysicsBodyRegistry`로 `EntityLifecycle`·`PlayerEntity`에 넘김, `ApplyRequirements(next)`). **요구사항 콜백**(TAC-F8-2·F16-5, 판 시작과 공급자 교체가 같은 클로저): `index.Required = (타깃이 SpatialHash) || (충돌이 Self)` 재계산 → `bodies.ApplyRequirements(ColliderActivationPolicy.Resolve(in selection))`. `StrategySelection.ShippingDefault`가 기본 선택이고 `Baseline`도 있다(Core).
  - PLAYER·MONSTER·PICKUP: 5c.3a 표 24행 그대로(생성자·단계 등록·라우터 채널 0~8·명령 버퍼 핸들러 9종·대기 명령 소유자 종류 4개 `SetLoadout`·`SetHealth`(→ `PlayerProgressionApplier.HandleSetLoadout/HandleSetHealth`)·`AddXp`·`LevelUpNow`(→ `ExperienceSystem.HandleAddXp/HandleLevelUpNow` — 4a)). `PlayerEntity`의 `host`는 당신이 만드는 플레이어 GameObject다.
  - FLOW(4a, 코드 `Scripts/Simulation/Progression/`): `new ExperienceSystem(currentData)`(`Func<GameData>`; `AddXp(int)`은 음수 예외·int.MaxValue 포화; 대기 명령 `AddXp → experience.HandleAddXp`, `LevelUpNow → experience.HandleLevelUpNow`(둘 다 `(PendingCommand, StepContext)`, 메서드 그룹 등록); `PickupSystem`의 `addXp = experience.AddXp`), `new StepOutcomeGate(clock, health, killPipeline, experience, currentData)` → **Lifecycle order 90** (`PendingOutcome`, `Reset()`), `new LevelUpSession()`(단계 등록 없음; `RoundId` long·`IsOpen`·`Options`·`OpenedFrame`·`Open(options3, frame)`(열려 있으면 InvalidOperationException, 3개가 아니면 ArgumentException)·`TrySubmit(roundId, index, frame, out chosen)`·`Close()`·`Reset()`), `LevelUpCandidateBuilder.Build(data, weapons, passives, hp, maxHp, output)`(정적; `HealTargetId = ""`), `LevelUpSelector.Draw(candidates, rng.Get(RngStream.LevelUp), output3)`(정적, 출력은 추첨 순서), `new RunSession(clock, lifecycle, router, targetSlot, ids, rng, commandBuffer, pendingQueue, player, movement, health, weapons, passives, damageReceiver, progressionApplier, pickupSystem, killPipeline, spawner, experience, levelUpSession, outcomeGate, nextSeed, spawnPolicy, inputSource)`(인자 24개 — 마지막 셋은 `Func<ulong>`·`Func<ISpawnPolicy>`·`Func<IInputSource>`; `Reset(in RunStrategies)`은 `StepActive`면 InvalidOperationException, 전략 null이면 ArgumentException; 읽기 속성 `Clock`·`Lifecycle`·`Player`·`Health`·`Weapons`·`Passives`·`Experience`·`KillPipeline`·`LevelUpSession`·`OutcomeGate`·`Rng`), `new BestRecordStore()`(키 `DefaultKey = "BestSurvivalSteps"`; 테스트는 `new BestRecordStore("...Test")`; `TryGetBest(out int)`, `TryRecord(in RunOutcome, SimulationRules)` — `Kind == None`이면 저장 안 함), `SimulationStateHash.Compute(runSession)`(정적, ulong). `RunOutcome.None` 정적 값. **조립·레벨업 처리의 완전한 예시는 `Tests/EditMode/Progression/FlowEmRig.cs`(특히 `ResolveLevelUps`)와 `RunSessionTests.cs`다 — 읽고 따라 하되 수정하지 않는다.**
- **판 시작·재시작·타이틀로 나가기(App 쪽 순서, TAC-F8-2 8·11회차)**: `StrategyFactory`로 `RunStrategies(pool, tick, collision, target)` 생성(`IDevModeHost.NextRunSelection` + `NextRunCollisionDecorator`(null 아니면 충돌 전략을 감싼다)) → `runSession.Reset(in next)`(0~9는 4a 소유) → `PoolPrewarmCounts.PrewarmAll(next.Pool, catalog)`(풀 객체가 바뀌었을 때만 — 5.10 확인) → `index.Clear()` → 요구사항 콜백 → (UI를 HUD로) → `clock.Open()`. "타이틀로 나가기"는 같은 함수로 리셋만 하고 `Open`하지 않으며 기록을 저장하지 않는다. 시드: `IDevModeHost.FixedSeed ?? (새 시드 — PM [기본값] `(ulong)System.DateTime.UtcNow.Ticks`를 `RunSession`의 시드 공급 `Func<ulong>`에 넘긴다; 시뮬레이션 안이 아니라 판 경계 1회라 TAC-0-1 위반이 아니다)`.
- **흐름(5.17·TAC-F5-4·F5-5·F5-7·F8-3·F8-9)**: `GameFlowController.Update`가 `clock.CloseSequence` 변화를 보고 `CloseReason`별로: `LevelUp` → `Build`+`Draw`(LevelUp 스트림) → `levelUpSession.Open(options, Time.frameCount)` → `FlowState.LevelUp`(뷰 표시). 선택 입력(키 1/2/3·버튼 클릭·포커스 Enter) → `TrySubmit(roundId, index, Time.frameCount, out chosen)` 참이면 **`levelUpSession.Close()`**(TrySubmit은 회차를 닫지 않는다 — 닫지 않고 다음 `Open`을 부르면 InvalidOperationException) → **스텝 없이** `applier.ApplyLevelUpOption(in chosen)` → `experience.TryBeginLevelUp(curve)` 재평가 → 참이면 같은 `Update`에서 다음 회차 `Open`(후보는 적용 후 상태), 거짓이면 `FlowState.Playing` → `clock.Open()`. `Death`/`Clear` → `gate.PendingOutcome` → `bestRecord.TryRecord(in outcome, rules)` → `FlowState.Result`(생존 `StepMath.FormatGameTime`, 처치, 레벨, 최고 기록 또는 "-"), `RunEnded` 이벤트. `Error` → 오류 표시(결과 화면 아님, 기록 저장 없음). `StopAtStep`·`SingleStepDone` → 무시(Dev 몫). `Paused`·`RunReset`은 당신이 닫은 것이라 무시. ESC(`UiInputHandler.Update`, `Keyboard.current`): `Playing` → `clock.Close(Paused)` + `Paused`(복귀 Playing); `LevelUp` → `Paused`(복귀 LevelUp, 세션은 열린 채, 입력 무시); `Paused` → 복귀 상태(복귀가 Playing이면 `clock.Open()`). `OnApplicationFocus(false)` && `Playing` && `rules.AutoPauseOnFocusLoss` → 일시정지(자동 재개 없음). 타이틀 "시작" → 판 시작, "종료" → `Application.Quit()`. 결과 "재시작" → 판 시작, "타이틀" → 리셋만. **UI는 App을 참조하지 않는다**(5c.1: UI → Core·Data·Simulation) — 뷰는 이벤트(`event Action StartRequested` 등)와 `Show/Hide`·`Bind(...)`만 갖고 `GameFlowController`가 구독한다.
- **화면(5.18·TAC-F8-5·F8-7·F1-6·F8-11)**: `WindowSizeSelector.Choose(w, h, 16, 39)`(후보 1920x1080 → 1600x900 → 1280x720, 없으면 1280x720), `DisplaySetup.Apply()`는 `Screen.mainWindowDisplayInfo.workArea`로 `Screen.SetResolution(w, h, FullScreenMode.Windowed)` 1회. Canvas = Screen Space Overlay + `CanvasScaler`(Scale With Screen Size, 1920x1080, Match 0.5) + `EventSystem` + `InputSystemUIInputModule`. 카메라 orthographic size `GameRules.CameraOrthographicSize`(9), `CameraFollow.LateUpdate`(실행 순서 0)가 `player.Position`을 **그대로** 대입(보간 없음), `BackgroundTiler.LateUpdate`(실행 순서 100)가 카메라 위치를 셀 크기 배수로 스냅한 Tiled `SpriteRenderer`. 메뉴는 uGUI `Button` + EventSystem 탐색(방향키·Enter)과 마우스가 같은 콜백(TAC-F8-12).
- **HUD** PM [기본값]: 게임 시간 `StepMath.FormatGameTime(clock.Step)`(R2, 일시정지·레벨업 중 멈춤은 스텝이 안 돌아 자동), 레벨, 체력(`Hp/MaxHp`), 경험치(`Xp/RequiredForNext`). 스프라이트·에셋 키 레지스트리(`Resources/AssetKeyRegistry.asset`, 사용자 생성)는 **이번 범위 밖** — 엔티티 시각은 2b `PresentationSync`가 옮기는 transform에 각 엔티티 타입이 가진 기본 도형(코드 확인; 없으면 마젠타 원 폴백이 다른 웨이브 몫임을 보고)으로 둔다.
- **IDevModeHost 구현**(5.15, `GameBootstrap`): `Rules`, `Clock`, `Commands`(PendingCommandQueue), `Data`, `Session`, `SpatialIndex`, `NextRunSelection {get;set;}`, `NextRunCollisionDecorator`, `InputOverride`, `SpawnPolicyOverride`, `FixedSeed`, `BossSpawnedThisRun`(→ `spawner.BossSpawnedThisRun`), `RequestTargetProviderSwitch(kind)`(StrategyFactory로 공급자 생성 → `new SwitchTargetProviderCommand(provider, 요구사항 콜백)` → `commands.Enqueue`), `StartNewRun()`, `event Action<RunOutcome> RunEnded`. `InputOverride ?? keyboard`·`SpawnPolicyOverride ?? new TableSpawnPolicy(data)`는 `RunSession`에 넘기는 `Func<>` 클로저 안에서 읽는다(판 시작 1회 바인딩).
- 테스트 실행 명령은 4a와 같다(래퍼 `C:\Users\SSAFY\.claude\jobs\f32c1ccc\tmp\flow-run.sh <EditMode|PlayMode> <tag>`, 접두사 `flowb-`). 한 프로젝트에 Unity 인스턴스 하나. 임시 파일은 그 폴더에만. 파일 LF·UTF-8(BOM 없음). **UI 문자열(한글)은 코드에 리터럴로 둔다**(1차 MVP, 로컬라이즈 없음) — 그 파일은 비ASCII가 있는 것이 정상이고 BOM은 여전히 없어야 한다.

## 2. 배타적 파일 소유권

### 소유 경로

1. `Survivor/Assets/Scripts/App/**` — `Survivor.App.asmdef`(참조: Core, Data, Simulation, Strategies, UI + `Unity.InputSystem`, `Unity.RenderPipelines.Universal.Runtime`; `UnityEngine.UI`는 ugui 패키지가 autoReferenced), `GameBootstrap.cs`(+`IDevModeHost` 구현), `IDevModeHost.cs`, `GameFlowController.cs`, `FlowState.cs`, `StrategyFactory.cs`, `DisplaySetup.cs`, `WindowSizeSelector.cs`, `CameraFollow.cs`, `BackgroundTiler.cs`.
2. `Survivor/Assets/Scripts/UI/**` — `Survivor.UI.asmdef`(참조: Core, Data, Simulation + `Unity.TextMeshPro`, `Unity.InputSystem`), `UiFactory.cs`(코드로 Canvas·패널·버튼·TMP 텍스트 생성), `TitleView.cs`, `HudView.cs`, `LevelUpView.cs`, `PauseMenuView.cs`, `ResultView.cs`, `DataErrorView.cs`, `UiInputHandler.cs`. 폰트 에셋은 `Assets/TextMesh Pro/**`(사용자 소유, 읽기만)에 있고 `TMP Settings`의 기본 폰트(`m_defaultFontAsset`)로 지정돼 있으므로 `TMP_Text`를 만들면 자동으로 붙는다 — **폰트를 코드로 로드하지 않는다**(`Resources` 폴더가 아니라 `Resources.Load`도 불가). 한글이 네모로 나오면 당신 코드가 폰트를 덮어쓴 것이다.
3. `Survivor/Assets/Scenes/Main.unity`(신규) + `.meta`.
4. `Survivor/Assets/Tests/EditMode/Architecture/AsmdefReferenceTests.cs`(신규 폴더), `Tests/EditMode/App/**`(신규: `WindowSizeSelector` 표, `StrategyFactory` 종류별 타입), `Tests/PlayMode/Flow/**`(신규).
5. 테스트 asmdef 2개의 `references`에 `Survivor.UI`·`Survivor.App` **각 1줄씩 추가**(5c.2 IMP-CORE 행 관례 — 다른 줄은 손대지 않는다).
6. `Tests/PlayMode/Loop/LoopPmRig.cs`의 **`LoopPmStateHash` → 실물 `SimulationStateHash` 교체**(5c.4 웨이브 4 행이 FLOW에 배정; 그 파일에서 이 교체 외의 변경 금지. 교체가 리그 구조상 불가능하면 — `LoopPmRig`는 `RunSession`이 없다 — 고치지 말고 이유를 보고한다. PM 예상: 불가능, 보고로 끝).
7. Unity 자동 생성 `.meta`.

### 건드리지 않는 것

- `Scripts/Core|Data|Strategies|Simulation/**`(전부 — 4a `Progression/` 포함), `Tests/EditMode/Core|Data|Spatial|Loop|Collision|Player|Monsters|Progression/**`, `Tests/PlayMode/Loop(6번 교체 제외)|Collision|Targeting/**`, `Scenes/SampleScene.unity`, `Settings/**`, `ProjectSettings/**`, `Packages/**`, `StreamingAssets/**`, `TextMesh Pro/**`, `UI/Fonts/**`, `Resources/**`.
- **스텁 금지**: Dev 쪽(`DevController`·`DevModeEntry`·측정)을 만들지 않는다. Simulation·Strategies에 메서드가 모자라면 **고치지 말고 보고**(소유 밖 결함 — PM이 소유자를 재개한다).
- `indiv` 루트 문서는 읽기만. **`RUN_LOG.md`는 열지도 쓰지도 않는다.**

## 3. 검증 재현성

- `Library/`·`Temp/`·`Logs/` 삭제 금지. 패키지 수정 = BLOCKED. `git commit`/`push`/`reset`/`git rm`, `rm -rf` 금지.
- 같은 도구 호출이 같은 이유로 2회 연속 실패하면 세 번째를 시도하지 말고 보고.
- **보고 수치는 PM이 같은 명령으로 재현 가능**해야 한다. 마지막 3회 XML 경로.

## 4. 결정성

- 테스트에 `UnityEngine.Random`·시드 없는 `System.Random`·`DateTime.Now`·`Time.*` 비교 금지(프레임 번호는 `Time.frameCount`를 `TrySubmit`에 넘기는 용도로만). 시드는 `FixedSeed`로 고정.
- **게이트 결정론(5c.5 웨이브 4 PM 테스트)**: 같은 시드로 PlayMode에서 레벨업·일시정지·재개를 섞어 N스텝 돌린 두 판의 `SimulationStateHash`가 같다. **프레임레이트 3종**(`Application.targetFrameRate` 30·60·144 또는 `Time.captureFramerate`)에서도 같다(2b `FixedStepLoopTests` 방식 참고).
- **전략 조합 결정론**: 풀 3 × 틱 2 × 충돌 2 × 타깃 3 = 36 조합을 같은 시드로 1,800스텝(= 30초) 돌려 36개 해시가 **전부 같다**(5c.5 웨이브 4 행). 조합마다 `StartNewRun`을 쓰므로 판 리셋 경로도 함께 검증된다. 시간이 길면(36 × 1,800스텝) 테스트를 조합별 `TestCase`로 나누고 총 소요를 보고.
- **흐름 PM 테스트**: 타이틀 → 시작 → 레벨업 회차 1·2·3(연속 3레벨, 스텝 수 불변, TAC-F5-4) → ESC 일시정지(같은 `RoundId`·같은 3개 유지, TAC-F5-7) → 재개 → 선택 → 플레이 → 사망(`SetHealth` 대기 명령 또는 접촉) → 결과 → 재시작 → 리셋 단언(4a와 같은 항목) → 타이틀. 열린 프레임의 입력이 거부되는 것(`frame > OpenedFrame`)도 PM 테스트.
- **조립 테스트(5c.4 웨이브 4 15·16회차)**: `CollisionEventRouter` 채널 9개 전부 등록(채널마다 `SetHandler` 재호출이 `InvalidOperationException` — 라우터에 조회 API가 없다), `SimulationCommandBuffer.HasHandler` 9종 전부 참, `PendingCommandQueue` 소유자 종류 4개 `Enqueue`가 예외 없음.
- **asmdef 참조 EM 테스트**: `.asmdef` JSON 6+2개를 읽어 5c.1 참조 방향 표(Core→없음, Data→Core, Simulation→Core·Data·InputSystem, Strategies→Core, UI→Core·Data·Simulation·TMP·InputSystem, App→Core·Data·Simulation·Strategies·UI·InputSystem·URP, 테스트→Core·Data·Simulation·Strategies·UI·App)와 비교. 표는 테스트 코드 안의 상수로 적는다. GUID 참조가 아닌 **이름 참조**를 쓴다(`"references": ["Survivor.Core", …]`) — 기존 asmdef가 그렇다.
- **화면 EM 테스트**: `WindowSizeSelector.Choose` 표 — (1920,1040)→1600x900, (2560,1392)→1920x1080, (1280,680)→1280x720, (1936,1119)→1920x1080, (1935,1119)→1600x900 등 경계 포함.
- **합격 기준: EditMode 전체 3회 전회 동일 + PlayMode 전체 3회 전회 동일(실패 0).** 여섯 줄 그대로 보고.

## 5. 기준선 — 정상 동작인데 결함으로 오판하기 쉬운 것, 그리고 PM 기본값

1. `ApplyLevelUpOption`은 `CloseReason == LevelUp && !StepActive && !IsRunning`에서만 — `Open()` 뒤에 부르면 예외가 **정상**. 연속 회차는 `Open()` 전에 전부 처리된다.
2. `LevelUpSession.Open`은 이미 열려 있으면 예외 — 일시정지 복귀에서 다시 `Open`하지 않는다(세션을 닫지 않았으므로 뷰만 다시 보인다).
3. `clock.Close(Paused)`는 `StepActive`면 예외 — `Update`에서 부르므로 스텝 밖이다. `Open()`도 같다.
4. `PendingCommandQueue.Enqueue`는 소유자 종류 핸들러 미등록이면 던진다 — 4개 전부 등록한 뒤에만 Dev가 쓴다.
5. `RunSession.Reset`은 `StepActive`면 예외. 리셋 뒤 `clock.Step == 0`, `!IsRunning` — `Open()`은 App이 부른다.
6. `PlayerEntity`는 `EntityLifecycle`을 거치지 않는다(5c.3 #1 예외). ID 1:1·2:1.
7. `EntityLifecycle.ReplaceStrategies`는 풀 참조가 바뀔 때만 옛 풀 `Clear()`(`Object.Destroy`) — PlayMode에서만 안전. 전략 조합 테스트가 36번 풀을 바꾸므로 Destroy가 36번 일어나는 것이 정상.
8. 엔진 충돌판 `Clear()`는 바디가 남아 있으면 **경고 로그**(예외 아님, 17회차 PM 판정) — 리셋 순서가 맞으면 로그가 없어야 한다. 로그가 보이면 당신 순서 문제다.
9. `Physics2DTargetProvider`는 콜라이더가 꺼져 있으면 `TryFindNearest` false(로그 없음) — 요구사항 콜백이 `ApplyRequirements`로 몬스터 콜라이더를 켜야 한다(`ColliderActivationPolicy.Resolve`가 타깃=Physics2DQuery 또는 충돌=Engine이면 켠다 — Core 코드 확인).
10. `TableSpawnPolicy`의 보스는 36,000스텝 — 흐름 테스트의 클리어는 보스를 `SpawnBossNow` 대기 명령(`PendingCommand.SpawnBossNow(definitionId, position)`, 큐가 직접 처리)으로 소환해 `Monster.ApplyDamage`로 죽이거나, 테스트용 `ISpawnPolicy`를 `SpawnPolicyOverride`로 꽂는다.
11. `BestRecordStore`는 `PlayerPrefs` — 테스트는 `new BestRecordStore("BestSurvivalSteps.FlowTest")`처럼 다른 키로 격리하고 TearDown에서 `PlayerPrefs.DeleteKey`한다. 실제 키 `BestSurvivalSteps`를 테스트가 바꾸지 않는다.
12. `GameDataRegistry.Initialize`는 1회만(두 번째는 예외). 테스트 리그가 `GameBootstrap`을 여러 번 만들면 각자 새 레지스트리다.
13. `KeyboardInputSource`·`UiInputHandler`는 `Keyboard.current`가 null이면 0/무입력 — 배치 PlayMode에서 null이 정상이라, 입력이 필요한 흐름 테스트는 **뷰의 공개 메서드/이벤트를 직접 호출**한다(`levelUpView.Choose(1)` 등). ESC 경로는 `GameFlowController`의 공개 메서드(★ `TogglePause()` 같은 — 당신이 정함)로 테스트.
14. `Screen.SetResolution`은 배치 모드에서 효과가 없고 예외도 없다 — `DisplaySetup.Apply()`는 PM 테스트에서 호출만 되고 EM에서 `Choose` 표를 검증한다.
15. TMP 기본 폰트 에셋은 `TMP_Settings`(Essential Resources)에서 오며 PM이 NotoSansKR SDF(헤더 2번 경로)로 바꿔 두었다 — `TMP_Text` 생성 시 null 경고가 나면 Essentials가 worktree에 없는 것(BLOCKED). `UiFactory`는 `font`를 지정하지 않는다.
16. **최고 기록 저장은 Death/Clear 결과 경로에서만**(TAC-F8-3): `OnApplicationQuit`·`Error`·"타이틀로 나가기"에서 저장 코드가 없어야 한다(PM이 grep으로 확인한다).
17. `RunOutcome.SurvivalSteps`는 게이트가 `context.Step + 1`로 세운 값(4a) — App은 다시 계산하지 않는다.

## 6. 금지

- 시크릿 파일 열람 금지. git 변경 명령·패키지 설치·`rm -rf` 금지.
- **단언을 느슨하게 고쳐 초록 금지**(해시 허용 오차, 조합 일부 제외, 프레임레이트 1종으로 축소 등). 문서 공개 시그니처·이름 변경 금지(반박으로).
- 주석 영어 산문, 공개 멤버 한 줄 요약(`CODING_GUIDE.md` §3). 네이밍 §2.2. 파일명 = 주 클래스명. 파일당 공개 타입 1개.
- UI가 App 타입을 참조하면 asmdef 컴파일이 막는다 — 우회(리플렉션·문자열 타입명) 금지.
- 로그: App 영역 도우미(★ `AppLog`, `[App]`) 또는 `Debug.Log` 최소. 프레임마다 로그 금지.

## 7. 보고 형식 (압축)

1. 만든 파일 목록(타입, TAC·절). 5c.2 IMP-FLOW 행 대조.
2. 테스트 결과: EM 3회 + PM 3회 원문 + PM 재현 명령 + 전략 조합 테스트 총 소요 시간.
3. 계약 대조표(5.17·5.18·5.15 `IDevModeHost` × 구현) + ★ 추가 공개 멤버 전부.
4. **씬 `Main.unity` 구성**(오브젝트 목록, 만든 방법) + **사용자가 할 일**(빌드 씬 목록 추가 등).
5. PM 기본값 중 따르지 않은 것과 이유(먼저 실측).
6. 읽을 목록 밖 파일. 7. **F8 기록 초안 1단락**(창 자동 선택·상태 머신·게이트 폴링 — 5c.2a). 8. 확신하지 못하는 것(항상 마지막).

## 8. 읽을 문서

전원 공통: `spec/00-common.md`, `tech/README.md`(색인만), `tech/02-feature-tac.md` **3.0 절·F5 절·F8 절 전체·TAC-F1-6·TAC-F11-1·F11-5·TAC-F16-5·TAC-F13-2**, `tech/5a-shared-rules.md`(R2·R29), `tech/5c-ownership-contracts.md`(5c.1 표 전체·5c.2 IMP-FLOW 행과 "사용자" 행·5c.2a·5c.3 #1·#7·#8·#9·#10·**5c.3a 전체**·5c.4 웨이브 4 행·5c.5).
필수(5c.2b): `spec/F05-level-up.md`, `spec/F08-stage-flow.md`, `spec/F01-player-move.md`(카메라), `spec/F13-build-demo.md`(창), `tech/5.03-step-clock.md`, `tech/5.11-data-loader.md`(5.11.0·5.11.1), `tech/5.14-progression.md`(전부), `tech/5.15-dev-mode.md`(`IDevModeHost` 블록·대기 명령 종류 문단), `tech/5.17-flow-state.md`, `tech/5.18-screen.md`, `tech/5b-external-settings.md`.
계약만: `tech/5.04`·`5.05`·`5.06`(데코레이터·`PrewarmAll`)·`5.07`(`SwitchTargetProviderCommand`·`Required`)·`5.08`·`5.09`(`PhysicsBodyRequirements`·`ApplyRequirements`·`PhysicsSettingsApplier`)·`5.10`(`ReplaceStrategies`)·`5.16`·`5.02`(`Reseed`)·`5.12`(`PlayerEntity`·`PlayerProgressionApplier` 블록)·`5.13`(5.13.6·7).
코드: `Scripts/Core/`(`StrategySelection`, `StrategyKinds`, `ColliderActivationPolicy`, `CollisionLayerMapping`, `GameRules`, `SimulationRules`, `StepMath`, `RngStreams`), `Scripts/Data/`(`GameDataLoader`, `GameDataRegistry`, `DataValidationReport`, `ValidationIssue`), `Scripts/Simulation/**`(공개 API·주석 — Progression 포함), `Scripts/Strategies/**`(생성자·공개 API), 테스트 리그 4종(`LoopPmRig`, `CollisionPmUtil`, `PlrRig`, `MonEmRig`) + 4a `Tests/EditMode/Progression/*Rig*`, 2b `FixedStepLoopTests`(프레임레이트 결정론 방식). `CODING_GUIDE.md` §1~§4·§7.
주지 않는 것: `RUN_LOG.md`, `DECISIONS.md`, 다른 `spec/F*.md`(F19 포함 — Dev는 웨이브 5), `tech/01`·`06`.

## 작업 순서 제안

1. 문서·코드 읽기 → asmdef 2개 + `AsmdefReferenceTests` → 배치 1회(컴파일 확인).
2. `WindowSizeSelector`·`DisplaySetup`·`FlowState`·`RunOutcome` 소비 + EM 표.
3. `StrategyFactory` + 종류별 EM 테스트.
4. `GameBootstrap` 조립(5c.3a + 4a) + 조립 PM 테스트(채널 9·명령 9·소유자 4).
5. `UiFactory`·뷰 6개·`UiInputHandler` → `GameFlowController` → `Main.unity`.
6. 흐름 PM 테스트 → 게이트 결정론 ×3 FPS → 전략 조합 36.
7. 빨강 → 수정 → 초록 → EM 3회 + PM 3회 → 보고.

=== 프롬프트 끝 ===
