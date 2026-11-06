# 웨이브 2b 디스패치 브리핑 — IMP-LOOP (2026-11-06, PM 작성)

> `general-purpose`, `model: opus`, `isolation: worktree`. "=== 프롬프트 시작 ===" 이후를 그대로 보낸다. 격리 방식은 DECISIONS 절차-D5. 선행: main에 웨이브 2a + `IPoolStrategy.Clear()`가 머지·푸시돼 있어야 한다.
> **PM 기본값(15회차에 명문화 예정, 아래 본문 L1~L6)**: 웨이브 3·4 소유 타입에 의존하는 부분은 이월하거나 등록식으로 푼다 — `SimulationStateHash.Compute(RunSession)`은 웨이브 4 이월, `PendingCommandQueue`·`SimulationCommandBuffer`는 소유자가 핸들러를 등록하는 구조, `PrefabCatalog`는 카테고리별 컴포넌트 타입을 생성자로 받음, `EntityLifecycle.Spawned` 이벤트 추가.

=== 프롬프트 시작 ===

# 역할: IMP-LOOP — 뱀서라이크 1차 MVP의 시뮬레이션 루프 구현자

당신은 Unity 6 / C# 프로젝트의 **`Survivor.Simulation` 어셈블리의 루프·생명주기 부분**(고정 스텝 시계와 게이트, 단계 파이프라인, 드라이버 3개, 지연 명령 버퍼, 대기 명령 큐, 엔티티 생명주기, 템플릿 카탈로그, 몬스터 스냅샷 레지스트리, 충돌 이벤트 라우터, 타깃 공급자 슬롯, 표시 동기화, 키보드 입력)과 **틱 전략 2종**(`Survivor.Strategies`의 `Tick/`), 그리고 그 **EditMode·PlayMode 테스트**를 구현한다. 이 코드는 게임의 "시간"을 소유한다 — 모든 결정론(같은 시드·입력 → 같은 스텝 결과)이 여기서 성립한다. 플레이어·무기·몬스터·충돌 전략·진행(레벨업·판 세션)·UI는 뒤 웨이브(3·4) 구현자 몫이며, 당신은 그들이 **등록해 끼울 자리**를 만든다.

## 1. 이미 확인된 사실 (다시 조사하지 말 것)

- **당신은 자기 전용 git worktree에서 작업한다.** cwd가 worktree 루트, 저장소 전체가 들어 있다. Unity 프로젝트는 `<cwd>\Survivor`. 다른 경로(원본 저장소 `C:\Users\SSAFY\Desktop\borry\indiv`, 다른 worktree)는 읽지도 쓰지도 않는다. 이번 웨이브는 당신 혼자다.
- Unity **6000.3.28f1**, 에디터 `C:\Program Files\Unity\Hub\Editor\6000.3.28f1\Editor\Unity.exe`, GUI는 닫혀 있다. worktree의 `Survivor/Library`가 없어 **첫 배치 실행은 3~5분**(실측 144~173초), 이후 15~30초.
- 이미 main에 있는 것(전부 읽기만): `Scripts/Core/`(웨이브 1, 54파일 — 당신이 구현할 인터페이스 `IStepPhaseHandler`·`IStepClockReader`·`StepContext`·`SimulationPhase`·`SimulationCommand`·`ISimulationCommands`·`IUpdatable`·`ITickStrategy`·`ICollisionStrategy`·`IPhysicsBodyRegistry`·`CollisionBody`·`CollisionEvent`·`ITargetProvider`·`MonsterSnapshotBuffer`·`IInputSource`·`MoveCommand`·`RngStreams`·`SimulationRules`·`EntityHandleAllocator`·`ProfilerMarkers`·`StepMath`가 다 있다), `Scripts/Data/`(웨이브 2a, `GameDataRegistry.TrySwap`·정의 객체), `Scripts/Strategies/Pool/`(풀 3종 — `IPoolStrategy.Clear()` 포함), `Scripts/Strategies/Spatial|Targeting/`(공간 해시·타깃 A·B), `Scripts/Strategies/Survivor.Strategies.asmdef`(**당신 소유**, 참조 `Survivor.Core`만 — 그대로 둔다), 테스트 asmdef 2개(EditMode 참조 Core·Data·Strategies·TestRunner, PlayMode 참조 Core·Strategies·TestRunner — **각각 `"Survivor.Simulation"` 한 항목만 추가**).
- `Scripts/Simulation/`과 `Survivor.Simulation.asmdef`는 아직 없다 — 당신이 만든다. 참조는 5c.1 표대로 `Survivor.Core`, `Survivor.Data`, `Unity.InputSystem`(패키지 `com.unity.inputsystem` 1.20.1이 manifest에 있다. 어셈블리 이름은 `Unity.InputSystem`. 참조가 실패하면 `Library/ScriptAssemblies`에서 실제 DLL 이름을 확인해 보고에 적는다).
- **문서와 Core 코드가 다르면 코드가 맞다**(13·14회차에서 맞춤). 특히 `EntityId`는 `EntityHandle`(할당기 `EntityHandleAllocator`)이고, `SimulationCommand` 생성자는 `kind`·`source` 필수 + 이름 있는 선택 인자, `Sequence`는 `SimulationCommandBuffer.Enqueue`가 `WithSequence(n)`으로 매긴다(5.4 13회차 주석).
- 웨이브 0 실측(10회차, 6.2): **U1** 서로 다른 `DefaultExecutionOrder`의 `FixedUpdate` 순서가 지정대로 돈다(확인됨). **U2** `Time.captureDeltaTime`은 `maximumDeltaTime` 클램프를 우회한다(0.1에서 6스텝) — 그래서 catch-up 상한 검증은 실제 긴 프레임(`Thread.Sleep(100)` 뒤 다음 프레임 스텝 수 ≤ 3)으로 한다. **U31** Late 순서 `FixedUpdate` 안에서 켠 컴포넌트가 같은 프레임 다음 `FixedUpdate`에 호출된다(확인됨). **U27** batchmode `-nographics`에서 PlayMode 테스트가 돈다.
- 테스트 실행 명령(PM이 그대로 다시 돌린다):
  ```
  "C:\Program Files\Unity\Hub\Editor\6000.3.28f1\Editor\Unity.exe" -batchmode -nographics -projectPath "<cwd>\Survivor" -runTests -testPlatform EditMode -testResults "C:\Users\SSAFY\.claude\jobs\f32c1ccc\tmp\loop-em-<n>.xml" -logFile "C:\Users\SSAFY\.claude\jobs\f32c1ccc\tmp\loop-em-<n>.log"
  ```
  PlayMode는 `-testPlatform PlayMode`에 `loop-pm-<n>`. Bash timeout 600000ms. 컴파일 오류면 테스트 0건 + 로그 `error CS`. 종료 코드 0/2/3 = 통과/실패 있음/실행 실패. XML `<test-run>`의 `total/passed/failed/inconclusive/skipped`. 전체 스위트에는 EditMode 420건(Core 205 + Data 147 + Spatial 68)·PlayMode 84+α건(Pool)이 함께 돈다 — 깨지면 당신 변경(asmdef 참조·공유 타입)이 원인일 가능성이 크니 먼저 본다. Unity를 Bash에서 직접 실행하는 것이 가드에 막히면 래퍼 `.sh`를 job tmp에 두고 그 안의 명령은 위와 동일하게 유지한 뒤 보고에 적는다(2a 구현자 3명이 그렇게 했다).
- 임시 파일은 `C:\Users\SSAFY\.claude\jobs\f32c1ccc\tmp\`에 `loop-` 접두사로. worktree 안에 임시 파일 금지. 파일은 LF·UTF-8(BOM 없음). **개행 검사는 바이트로 한다**(`tr -cd '\r' | wc -c` 또는 python) — 이 셸의 `grep -c $'\r'`은 틀린 답을 낸다. Simulation·Strategies 코드에 한국어는 없어야 정상이다(사용자 노출 문자열이 없다).
- 읽기 전용 git 명령(`git status`, `git diff`, `git show`, `git log`, `git config --get`)은 허용한다. 상태를 바꾸는 git 명령은 전부 금지(3절).

## 2. 배타적 파일 소유권

### 소유 경로 (이 밖에는 만들거나 고치지 않는다)

1. `Survivor/Assets/Scripts/Simulation/Survivor.Simulation.asmdef`(★ 신규, 참조 Core·Data·Unity.InputSystem, `rootNamespace` `Survivor.Simulation`, autoReferenced true).
2. `Survivor/Assets/Scripts/Simulation/Loop/**` — 5c.2 IMP-LOOP 행: `SimulationClock.cs`, `GateCloseReason.cs`, `SimulationFrameCounter.cs`, `SimulationPipeline.cs`, `SimulationDriverEarly.cs`, `SimulationDriverMid.cs`, `SimulationDriverLate.cs`, `SimulationCommandBuffer.cs`, `ISimulationCommandHandler.cs`, `EntityDespawnCommandHandler.cs`, `PendingCommandQueue.cs`, `SwitchTargetProviderCommand.cs`, `CollisionEventRouter.cs`, `ICollisionEventHandler.cs`, `TargetProviderSlot.cs`, `SimulationSettingsApplier.cs`, `KeyboardInputSource.cs`. (`SimulationStateHash.cs`는 **이번에 만들지 않는다** — L1.)
3. `Survivor/Assets/Scripts/Simulation/Entities/**` — `SimEntity.cs`, `EntityLifecycle.cs`, `LifetimeSystem.cs`, `PrefabCatalog.cs`, `PoolPrewarmCounts.cs`, `PresentationSync.cs`, `MonsterRegistry.cs`.
4. `Survivor/Assets/Scripts/Strategies/Tick/**` — `CentralTickStrategy.cs`, `SelfTickStrategy.cs`, `MonsterSelfTicker.cs`, `ProjectileSelfTicker.cs`, `SelfTickTemplateDecorator.cs`.
5. `Survivor/Assets/Scripts/Strategies/Survivor.Strategies.asmdef` — 당신 소유지만 **바꿀 것이 없다**(참조 Core만). 바꿔야 한다고 믿으면 반박으로.
6. `Survivor/Assets/Tests/EditMode/Loop/**`, `Survivor/Assets/Tests/PlayMode/Loop/**`.
7. `Survivor/Assets/Tests/EditMode/Survivor.Tests.EditMode.asmdef`·`Tests/PlayMode/Survivor.Tests.PlayMode.asmdef` — `references`에 `"Survivor.Simulation"` 한 항목 추가만.
8. 행에 없는 파일을 추가해야 했다면 보고에 이유를 적는다(클래스명 전역 고유, 파일당 공개 타입 1개 — 열거형 묶음·문서가 함께 적은 것만 예외). Unity가 자동 생성하는 `.meta`(손으로 만들지 않는다).

### 건드리지 않는 것

- `Scripts/Core/**`(인터페이스가 부족하다고 느껴도 고치지 말고 반박으로 — Core 변경은 IMP-CORE가 PM 승인 뒤 한다), `Scripts/Data/**`, `Scripts/Strategies/Pool|Spatial|Targeting/**`, `Scripts/Simulation/Player|Weapons|Monsters|Pickups|Progression/**`(없음, 웨이브 3·4), `Scripts/App|UI|Dev/**`(없음), `Scenes/`, `Settings/`, `ProjectSettings/`, `Packages/`.
- **스텁 금지.** `PlayerEntity`·`Monster`·`RunSession`·`StepOutcomeGate`·`SelfCollisionStrategy` 등 남의 타입을 만들지 않는다. 테스트가 필요로 하는 것은 **테스트 어셈블리 안의 테스트 전용 더블**(테스트 `SimEntity` 파생 클래스, 아무것도 하지 않는 `ICollisionStrategy`/`IPhysicsBodyRegistry`, 기록용 `ITargetProvider`)로 만든다. 풀은 main의 `CustomPoolStrategy`(실물)를 쓸 수 있다 — PlayMode 테스트 asmdef가 Strategies를 참조한다. EditMode에서도 `Survivor.Strategies` 참조가 있으니 실물 풀을 써도 되지만, `Instantiate`가 EditMode에서 되는지는 당신이 확인한다(안 되면 테스트 더블 풀).
- `indiv` 루트 문서는 읽기만. **`RUN_LOG.md`는 열지도 쓰지도 않는다.** 문서 결함은 보고에 적는다.

## 3. 검증 재현성

- `Library/`·`Temp/`·`Logs/` 삭제 금지. 패키지 설치·`manifest.json` 수정 = BLOCKED 보고. `git commit`/`push`/`reset`/`checkout`/`stash`/`git rm`, `rm -rf` 금지. 파일 단위 삭제만.
- 같은 도구 호출이 같은 이유로 2회 연속 실패하면 세 번째를 시도하지 말고 보고.
- **보고 수치는 PM이 같은 명령으로 재현 가능해야 한다.** 마지막 3회 XML 경로를 적는다.

## 4. 결정성

- 테스트에 `UnityEngine.Random`·시드 없는 `System.Random`·`DateTime.Now`·`Time.*`(읽기 포함 — 단 `Time.captureDeltaTime` 설정과 `Time.fixedDeltaTime` 확인은 PlayMode 고정 스텝 테스트의 수단이라 허용) 금지. 난수는 `RngStreams`/`DeterministicRng` 고정 시드.
- **고정 시간 대기 금지.** PlayMode에서 프레임 경계는 `yield return null`/`WaitForFixedUpdate`로, 스텝 수는 `SimulationClock.Step`으로 판정한다. catch-up 상한 테스트의 `Thread.Sleep(100)`은 "긴 프레임을 만드는 수단"이라 예외(5c.5 고정 스텝 행).
- **합격 기준: EditMode 전체 스위트 연속 3회 전회 동일 + PlayMode 전체 스위트 연속 3회 전회 동일(각 실패 0).** 여섯 줄 그대로 보고.
- 결정론 테스트(5c.5 "고정 스텝" 행): `Time.captureDeltaTime`을 1/30, 1/60, 1/144로 바꿔 각각 600스텝 → **테스트 전용 상태 해시**(테스트 엔티티들의 (Id, Position) FNV-1a, L1 참고) 일치. 틱 전략 2종 × 같은 시나리오도 해시 일치(TAC-F17 "두 구현 결과 동일").

## 5. 기준선 — 정상 동작인데 결함으로 오판하기 쉬운 것, 그리고 PM 기본값

### 5-A. PM 기본값 (뒤 웨이브 타입에 의존하는 곳을 이번 웨이브에서 푸는 방법 — 설계 변경이 아니라 등록식 구현. 15회차에 명문화)

- **L1. `SimulationStateHash.Compute(RunSession)`은 웨이브 4로 이월.** `RunSession`은 IMP-FLOW(웨이브 4) 소유라 지금 시그니처를 만들 수 없다. 이번에는 `SimulationStateHash.cs`를 만들지 않는다. 대신 FNV-1a 64 누적 도우미를 **테스트 어셈블리 안**에 두고(비트 패턴 해시, 5.16 주석 규칙) 결정론 테스트에 쓴다. 웨이브 4에서 당신(또는 FLOW)이 그 도우미를 Simulation으로 옮겨 공개 `Compute`를 만든다.
- **L2. `SimulationCommandBuffer`와 `PendingCommandQueue`는 "소유자가 핸들러를 등록"하는 구조.** 명령 종류별 핸들러는 PLAYER·MONSTER·FLOW가 웨이브 3·4에서 등록한다. 당신은:
  - `SimulationCommandBuffer : ISimulationCommands, IStepPhaseHandler`(Lifecycle order 0): `Enqueue`가 `WithSequence(n)`(소스별 순번), Lifecycle에서 `SimulationCommand.Compare`(R10: Source, Sequence)로 정렬 뒤 `RegisterHandler(SimulationCommandKind kind, ISimulationCommandHandler handler)`로 등록된 핸들러에 넘긴다. 같은 종류 중복 등록 → `InvalidOperationException`(5c.3 #7). **미등록 종류의 명령이 들어오면 `InvalidOperationException`**(스텁으로 조용히 버리지 않는다). 같은 대상의 `Despawn`이 둘이면 둘째 무시(5.4). 처리 뒤 버퍼를 비운다. 이번에 등록되는 실물 핸들러는 `EntityDespawnCommandHandler`(Despawn → `EntityLifecycle.Despawn`) 하나다.
  - `PendingCommandQueue : IStepPhaseHandler`(Begin order 0): 스텝 밖(Update)에서 `Enqueue`된 대기 명령을 다음 스텝 Begin에서 넣은 순서대로 적용한다. 이번에 **실물로 구현하는 명령**: `SwitchTargetProvider`(5.7 `SwitchTargetProviderCommand` 구조체 — `slot.Replace(cmd.Provider)` → `cmd.ApplyRequirements()`), `SwapData(GameData, DataValidationReport)`(`GameDataRegistry.TrySwap` 호출, 실패하면 `[Loop]` Warning 한 줄), `SpawnMonstersNow(definitionId, count, position, radius, ignoreCap)`·`SpawnBossNow(definitionId, position)`·`SpawnChestNow(position)`(각각 `SimulationCommand`(Source = `EntityHandle.None`)로 변환해 `ISimulationCommands.Enqueue` — 생성 자체는 같은 스텝 Lifecycle의 MONSTER 핸들러가 한다), `SetDespawnDistance(float)`(`SimulationRules.DespawnDistance`·`DespawnDistanceOverridden`). PLAYER·FLOW 상태가 필요한 `SetLoadout`·`SetHealth`·`AddXp`·`LevelUpNow`는 **종류만 열거형에 두고 `RegisterHandler(kind, Action<PendingCommand>)`로 소유자가 나중에 끼운다** — 미등록이면 `InvalidOperationException`. 페이로드 구조체 이름·필드는 당신이 정하고 보고의 계약 대조표에 적는다(★ 후보).
- **L3. `PrefabCatalog`는 카테고리별 `SimEntity` 파생 컴포넌트 타입을 생성자로 받는다.** `Monster`·`PlayerProjectile` 등 파생 클래스는 PLAYER·MONSTER(웨이브 3) 소유다. `PrefabCatalog(IReadOnlyDictionary<EntityCategory, Type> entityComponentTypes, IReadOnlyList<IEntityTemplateDecorator> decorators)`(★ 후보)로 받아, 카테고리 8종의 비활성 템플릿 `GameObject`를 코드로 만들고(`SpriteRenderer` + 해당 `SimEntity` 파생 컴포넌트 `AddComponent(type)`), 데코레이터 전부를 순서대로 적용한다(TAC-F9-7 — 전략과 무관하게 항상 전부). `Type`이 `SimEntity` 파생이 아니면 `ArgumentException`. 레지스트리 프리팹 덮어쓰기(F11, `VisualAsset.PrefabOverride`)는 "프리팹을 복제한 뒤 같은 데코레이터 적용"(5.6) — 이번에는 덮어쓰기 경로도 구현하되 테스트는 null(덮어쓰기 없음) 경로 중심. 템플릿 접근은 `GetTemplate(EntityCategory)`.
- **L4. `EntityLifecycle.Spawned` 이벤트 추가(★).** 5.10의 `Spawn` 순서 마지막 "registry add (MonsterRegistry / PickupRegistry ...)"에서 `PickupRegistry`는 MONSTER 소유다. 생명주기가 레지스트리를 직접 알지 않도록 `public event Action<SimEntity> Spawned;`를 그 자리에서 발행하고, `MonsterRegistry`(당신)와 뒤 웨이브의 `PickupRegistry`가 구독한다. `Despawned`는 문서대로(`OnReleased`·ID 해제 전, 구독자가 `Id`를 읽을 수 있음).
- **L5. 전략 교체와 풀 정리(사용자 결정 2026-11-06, DECISIONS 2절).** `EntityLifecycle.ReplaceStrategies(pool, ticks, collisions)`는 판 경계에서 `DespawnAll` 뒤에만 불리며, **풀 객체가 바뀌면 옛 풀의 `Clear()`를 부른다**(같은 객체면 부르지 않음). 그 뒤 새 풀의 `Prewarm`(카테고리별 `PoolPrewarmCounts`)은 **조립 루트(App)의 몫**이므로 당신은 `PrewarmAll(IPoolStrategy pool, PrefabCatalog)` 같은 도우미를 제공만 한다(★ 후보, 위치는 `PoolPrewarmCounts` 또는 `EntityLifecycle`). `ITickStrategy.Clear()`·`ICollisionStrategy.Clear()`는 `RunSession.Reset` ②가 부른다(TAC-F8-2) — 당신은 부르지 않는다. 활성 엔티티가 남은 상태에서 `ReplaceStrategies`가 불리면 `InvalidOperationException`.
- **L6. 로그 포매터는 영역별 internal.** Core에 공용 포매터가 없다(5c.3 #8 14회차). `[HH:mm:ss.fff] [LEVEL] [Loop] message` 형식의 internal `LoopLog`(파일 추가 허용)를 만든다. 스텝마다 남기는 로그 금지. 드라이버 예외 로그 형식은 5c.3 #7: `[시각] [ERROR] phase=<단계> step=<n> <메시지>`.

### 5-B. 기준선

1. **게이트 규칙(5.3)**: 스텝 안 닫기는 `CloseAfterCurrentStep` 하나(호출자: `StepOutcomeGate`(FLOW, 나중)와 `EndStep`), 호출 즉시 `IsRunning = false`, 진행 중 스텝은 End까지 돌고 `Step++`. 먼저 닫은 이유가 남는다. `Close`는 스텝 밖에서만(StepActive면 throw), `Open`은 FLOW만(StepActive면 throw). `RequestSingleStep`은 열고 정확히 한 스텝 뒤 `SingleStepDone`으로 닫는다. `RequestJump`는 앞으로만(뒤면 `ArgumentOutOfRangeException`), 다음 `TryBeginStep` 안에서 Begin 전에 적용. `Reset`은 Step 0·게이트 닫힘(`RunReset`).
2. **예외 경로(5.3·5c.3 #7)**: 드라이버 3개가 각자 한 곳에서 단계 실행 예외를 잡아 로그 뒤 `AbortStep()` — 그 스텝은 거기서 중단, 남은 단계·End를 돌지 않고 `Step` 불변, 게이트 `Error`로 닫힘. 테스트는 핸들러가 던지는 더블로 확인한다.
3. **`TryBeginStep`이 거짓이면 같은 `FixedUpdate`의 Mid·Late·셀프 티커는 `StepActive == false`를 보고 아무것도 하지 않는다.** `SimulationFrameCounter`(order −10000, `Update`)가 `StepsThisFrame`을 0으로 되돌린다. catch-up은 `Time.maximumDeltaTime = 0.05`(3스텝)로 제한된다(`SimulationSettingsApplier.Apply()`가 `fixedDeltaTime = StepMath.StepSeconds`, `maximumDeltaTime = StepMath.MaxCatchUpSteps * StepMath.StepSeconds`만 설정 — Physics2D 설정은 COLLISION 몫).
4. **`StepContext`는 PlayerMove 뒤 드라이버가 확정한다.** Begin·PlayerMove 핸들러는 이전 위치를 본다. `Targets`는 BuildIndex부터 유효(`TargetProviderSlot`), `Commands`는 `SimulationCommandBuffer`. 플레이어 위치 공급자(`PlayerMovement`, PLAYER)가 아직 없으므로 드라이버는 **위치를 주는 `Func<Vector2>`/작은 인터페이스를 조립 시 받는다**(★ 후보, 보고에). `IStepClockReader.CurrentContext`는 StepActive이고 phase ≥ MonsterTick일 때만 유효.
5. **단계 핸들러 순서는 5c.3 #9 표로 고정**이고 `SimulationPipeline.AddHandler`의 같은 (phase, order) 중복은 throw. 당신이 이번에 등록하는 핸들러: Begin 0 `PendingCommandQueue`, BuildIndex 0 `MonsterRegistry`, Collision 0 `CollisionEventRouter`(수집), Resolve 0 라우터 디스패치, Lifecycle 0 `SimulationCommandBuffer`, 10 `LifetimeSystem`. 등록 자체는 조립 루트가 하므로 당신은 "등록 도우미"(예: `LoopHandlers.RegisterAll(pipeline, …)`) 또는 테스트에서 직접 등록 — 어느 쪽이든 보고에.
6. **중앙 틱 모드**: `MonsterTick`을 Early에서, `ProjectileTick`을 Mid에서 `ITickStrategy.RunGroup`으로. 객체별 모드에서는 `RunGroup`이 no-op이고 셀프 티커(`DefaultExecutionOrder` −500/500)가 `if (!_clock.StepActive) return; _owner.Tick(_clock.CurrentContext);`만 한다(5.8). `SelfTickStrategy(IStepClockReader clock)`. `Register(updatable, host)`는 호스트의 티커에 연결 + `enabled = true`, `Unregister`는 `false`(TAC-F17-2). 마커: Early가 `Tick.Monsters`를 열고 Mid가 닫고, Mid가 `Tick.Projectiles`를 열고 Late가 닫는다(5.3 드라이버 주석; Core `ProfilerMarkers` 필드를 쓴다).
7. **`CentralTickStrategy`(TAC-F17-1)**: 그룹별 `List<IUpdatable>` 등록 순서 순회. 순회 중 해제 → 슬롯 null → 순회 뒤 압축. 순회 중 등록 → 대기 목록 → 순회 뒤 붙임(정상 경로에서는 생성이 Lifecycle이라 일어나지 않지만 방어 코드로). `RegisteredCount(group)`, `Clear()`.
8. **생성·회수는 Lifecycle 단계(또는 판 리셋 중)에서만**(TAC-0-8): `Spawn`/`Despawn`은 `clock.CurrentPhase == Lifecycle`이 아니고 판 리셋 중도 아니면 `InvalidOperationException`. "판 리셋 중"은 `DespawnAll`이 켜는 내부 플래그로 표현한다(★ 후보). `Spawn` 순서: `pool.Acquire → ids.Allocate → Bind → initialize(entity) → ticks.Register(if IUpdatable) → physicsBodies.Register(Body) → collisions.Register(Body) → Spawned 이벤트(L4)`. `Despawn` 순서는 정확히 역순: `레지스트리(Spawned 구독자 쪽) → collisions.Unregister → physicsBodies.Unregister → ticks.Unregister → Despawned 이벤트 → OnReleased → ids.Free → pool.Release`. `Despawn`은 `!ids.IsLive(id)`면 false(예외 없음). `DespawnAll`은 **Id 오름차순**. `lifetimeSteps == 0` → `ExpireStep = long.MaxValue`, `> 0` → `SpawnStep + lifetimeSteps`, `< 0` → `ArgumentOutOfRangeException`.
9. **판정 참여 한 문장(5.4)**: 스텝 s의 Lifecycle에서 생성된 엔티티는 s의 Collision에 참여하지 않고 s+1부터, 첫 Tick도 s+1. `SimEntity.Tick`에는 `Debug.Assert(ctx.Step > SpawnStep)`만. `LifetimeSystem`(order 10)은 `Step >= ExpireStep`인 엔티티를 `Despawn(LifetimeExpired)`, `long.MaxValue`는 건너뜀.
10. **`SimEntity`(5.10)**: `MonoBehaviour` 추상, `FixedUpdate` 없음. `Position`은 원본이고 `Body.Position`으로 미러(`CollisionBody`는 Core — 코드를 읽어 `Body` 생성·바인딩 방법을 정한다). `IsConsumed`는 Resolve에서 `MarkConsumed`. `Bind(id, spawnStep, expireStep)`·`MarkConsumed`는 internal, `OnReleased`는 protected abstract.
11. **`MonsterRegistry`(BuildIndex 0)**: `Spawned`/`Despawned`로 `Monster` 카테고리만 추적(이번에는 카테고리와 `SimEntity`로 판단 — `Monster` 클래스는 없다), 스냅샷은 Core `MonsterSnapshotBuffer`에 **Id 오름차순**으로 `Add`, `IsLarge`는 `MonsterSizeRules.IsLarge(radius)`(R25)로 채운다. 스냅샷 완성 직후 `SimulationDriverMid`가 `TargetProviderSlot.OnSnapshotBuilt(snapshot)`을 **직접** 부른다(BuildIndex 핸들러 전부 끝난 뒤, 5c.3 #9).
12. **`TargetProviderSlot : ITargetProvider`**(핸들러 아님): 현재 공급자를 감싸 `StepContext.Targets`로 나간다. `Replace(next)`는 Begin(대기 명령) 또는 판 리셋에서만 — `StepActive && phase != Begin`이면 throw. 공급자가 없을 때 `TryFindNearest`는 false.
13. **`CollisionEventRouter`**: Collision 0에서 `ICollisionStrategy.CollectEvents(step)`를 받아 보관, Resolve 0에서 **채널별 등록 핸들러**(`ICollisionEventHandler`, `Register(channel, handler)` 중복 → throw)에 `CollisionEvent.CompareTo` 순서로 디스패치. `ReplaceStrategy(ICollisionStrategy)`는 판 경계에서만. 이번에 등록되는 실물 핸들러는 없다(전부 웨이브 3) — 미등록 채널의 이벤트는 **무시가 아니라 `InvalidOperationException`**? 아니다: 여기는 예외가 아니라 **버린다**(채널 핸들러가 아직 없는 것은 정상 상태일 수 있다 — 예: 측정 조합). 이 판단이 틀렸다고 보면 반박으로.
14. **`PresentationSync.LateUpdate`(order −100)**: 활성 엔티티 전부 `transform.position = Position`을 프레임당 1회, 단 `Body.DrivesTransform == true`면 건너뜀(5c.3 #6). 활성 목록은 `EntityLifecycle`에서 받는다.
15. **`KeyboardInputSource : MonoBehaviour, IInputSource`**(TAC-F1-7, 읽을 것): 새 Input System으로 WASD/방향키 → 정규화 방향. ESC·숫자·F1은 다루지 않는다. `ReadMove(step, pos)`는 `Time.*`를 읽지 않는다. EditMode 테스트 불가(입력 장치) — 컴파일·`MoveCommand` 반환 규칙만 확인하고 미확정으로 적는다.
16. **`PoolPrewarmCounts`(Q13)**: Monster 450, PlayerProjectile 300, EnemyProjectile 100, PlayerOrb 16, PlayerZone 16, EnemyZone 8, Gem 600, Chest 16. 상수 클래스(Simulation).
17. **Strategies는 Core만 참조**(5c.1). `Tick/` 코드에 Simulation 타입을 쓰면 위반이다 — 셀프 티커는 `IStepClockReader`·`IUpdatable`만 안다.
18. **풀 전략 실측(2a)**: B의 `Destroy`는 1프레임 뒤 적용(U22)이지만 비활성화가 먼저라 같은 프레임 다음 스텝에 간섭하지 않는다. 풀은 상태를 초기화하지 않는다 — 초기화는 당신의 `Spawn`의 `initialize(entity)`가 한다(TAC-F9-6).
19. **테스트 함정(14회차 기준선)**: test-framework 1.6에서 `LogAssert.ignoreFailingMessages`는 `[SetUp]`에서 켜도 본문에 이어지지 않는다 — 오류 로그를 의도적으로 내는 테스트는 본문에서 켠다(또는 `LogAssert.Expect`).
20. **데이터**: `GameDataRegistry.Current`는 "참조할 때마다 읽는" 소비자용(5c.3 #10). `SwapData`는 다음 스텝 Begin에서 `TrySwap` — 플레이 불가면 기존 유지·false. 정의 객체 setter는 internal이라 테스트에서 데이터가 필요하면 `GameDataLoader.LoadFromTexts`로 만든다(5c.5 14회차 관례).

## 6. 금지

- 시크릿 파일 열람 금지. git 상태 변경 명령·패키지 설치·`rm -rf` 금지.
- **단언을 느슨하게 고쳐 초록 금지.** 공개 시그니처·이름·네임스페이스·열거형 순서 변경 금지(반박으로 올리고 문서대로 구현). 5-A의 ★ 후보는 **새로 정하는 것**이라 당신이 이름을 정하되 보고에 전부 적는다.
- 주석 영어 산문, 공개 멤버 한 줄 요약(`CODING_GUIDE.md` §3). 네이밍 §2.2. 파일명 = 주 클래스명. 파일당 공개 타입 1개.
- 스텝마다 로그 금지(측정 오염). 보고에 시크릿·긴 로그 원문 금지.

## 7. 보고 형식 (압축)

1. 만든 파일 목록(경로별 타입·구현 절·TAC). 5c.2 IMP-LOOP 행 대조(빠진 — `SimulationStateHash.cs` 이월 표기 — ·추가한 파일과 이유).
2. 테스트 결과: EditMode 3회 + PlayMode 3회 원문(total/passed/failed/inconclusive/skipped, XML, 종료 코드) + PM 재현 명령(`<cwd>` 채워서). Loop 테스트만의 건수(EM/PM 각각).
3. 환경 수치: 첫 배치 시간, Library 생성, Unity.InputSystem 참조 방식, EditMode에서 `Instantiate`/실물 풀 사용 가능 여부, 결정론 테스트의 3 FPS 해시값(같아야 함)과 소요.
4. **계약 대조표**: 5.3·5.4·5.8·5.10·5.7(슬롯·명령)의 공개 타입별 "문서 그대로 / 차이(무엇)" + **★ 후보 전부**(L2 페이로드·`RegisterHandler` 시그니처, L3 `PrefabCatalog` 생성자, L4 `Spawned`, L5 `PrewarmAll`, 5-B #4 플레이어 위치 공급, #5 등록 도우미, #8 리셋 플래그, L6 `LoopLog`).
5. 반박: 기준선·문서·PM 기본값이 틀렸다고 믿는 항목(번호·근거·처리). 특히 5-B #13(미등록 채널 이벤트 버림)과 L2(미등록 명령 예외)의 비대칭이 맞는지 당신 의견.
6. 읽을 목록 밖 파일을 열었다면 무엇을 왜.
7. **F17 기록 초안**(5c.2a 의무 — 보고에 담는다, 파일은 만들지 않는다): README 쪽 "고정 스텝·게이트 설계 요약" 5~10줄 + devlog 쪽 "F17 측정 가설(호출 오버헤드 예상)" 10~15줄. 가설은 가설로 표시, 수치는 당신 실측만.
8. 확신하지 못하는 것 — 미확정으로. (항상 마지막)

## 8. 읽을 문서 (이 밖의 기획 문서는 열지 말 것)

전원 공통: `spec/00-common.md`, `tech/README.md`(색인만), `tech/02-feature-tac.md` **3.0 절(TAC-0-1~10)·F17 절(TAC-F17-1~3)·TAC-F1-7·TAC-F8-2·TAC-F16-5·TAC-F9-6·7만**, `tech/5a-shared-rules.md`(R1·R3·R10·R11·R16·R25), `tech/5c-ownership-contracts.md`(5c.1·5c.2 IMP-LOOP 행·5c.2a·5c.3 전부·5c.5).
IMP-LOOP 필수(5c.2b): `spec/F17-update-strategy.md`, `spec/F01-player-move.md`(입력 소스 부분만), `tech/5.03-step-clock.md`, `tech/5.04-commands-spawn.md`, `tech/5.05-input.md`, `tech/5.06-pool.md`(5.6·5.6.1), `tech/5.07-targeting.md`(슬롯·`SwitchTargetProviderCommand` 부분, 88~115행 근처), `tech/5.08-tick.md`, `tech/5.10-lifecycle.md`, `tech/5.16-state-hash.md`(이월 범위 확인용).
계약만: `tech/5.09-collision.md`(`CollisionBody`·`CollisionEvent`·`ICollisionStrategy` — 코드가 더 빠르다), `tech/5.11-data-loader.md`(5.11.1 `GameDataRegistry`), `tech/5.14-progression.md`(100~125행 `RunStrategies`·`RunSession.Reset` 주석 — 당신이 제공할 교체 진입점의 호출자), `tech/5.15-dev-mode.md`(78행 근처 `PendingCommandQueue` 명령 종류).
코드: `Survivor/Assets/Scripts/Core/*.cs` 전부(공개 API 원본), `Scripts/Strategies/Pool/CustomPoolStrategy.cs`·`PooledObject.cs`(테스트에서 실물 풀을 쓸 때), `Scripts/Data/GameDataRegistry.cs`, `Scripts/Strategies/Spatial/MonsterSpatialIndex.cs`(`IStepPhaseHandler` 구현 예), `Tests/EditMode/Core/*`(테스트 형식 참고 1~2개). `CODING_GUIDE.md` §1~§4·§7.
주지 않는 것: `RUN_LOG.md`, `DECISIONS.md`(필요한 결정 요지는 위에), 다른 `spec/F*.md`, `tech/01`·`06`·`5.12`·`5.13`·`5.17`·`5.18`.

## 작업 순서 제안

1. 문서·Core 코드를 읽고 5c.2 IMP-LOOP 행 + 5c.3 #9 표에서 체크리스트를 만든다. ★ 후보의 이름을 먼저 정해 적어 둔다.
2. `Survivor.Simulation.asmdef` + 테스트 asmdef 참조 2줄 + `GateCloseReason.cs` 하나만 둔 상태로 EditMode 배치 1회 — 파이프라인·Unity.InputSystem 참조 확인(Library 생성, 3~5분).
3. `SimulationClock` → `SimulationPipeline` → `SimulationCommandBuffer`·`EntityDespawnCommandHandler` → `SimEntity`·`EntityLifecycle`·`LifetimeSystem`·`PoolPrewarmCounts`·`PrefabCatalog` → `MonsterRegistry`·`TargetProviderSlot`·`SwitchTargetProviderCommand`·`PendingCommandQueue` → `CollisionEventRouter`·`ICollisionEventHandler` → 틱 전략 2종·셀프 티커·데코레이터 → 드라이버 3개·`SimulationFrameCounter`·`PresentationSync`·`SimulationSettingsApplier` → `KeyboardInputSource`.
4. EditMode 테스트(시계·게이트 규칙 전부, 파이프라인 중복 throw, 명령 정렬 R10·중복 Despawn·미등록 예외, 생명주기 순서·역순·단계 제한·수명 규칙·이벤트 시점·`DespawnAll` 오름차순·`ReplaceStrategies`+`Clear`, `LifetimeSystem`, 중앙 틱 순회 중 등록/해제, 셀프 틱 등록/enabled, 스냅샷 오름차순·`IsLarge`, 슬롯 교체 제한, 대기 명령 Begin 적용·`SwitchTargetProvider` 순서·`SwapData` 실패 유지, 라우터 등록 중복·디스패치 순서).
5. PlayMode 테스트(드라이버 단계 순서 1 FixedUpdate 안, 3 FPS 600스텝 해시 일치, 긴 프레임 catch-up ≤ 3, 스텝 안 닫기 → `Step == s+1`, 예외 → `AbortStep` 뒤 `Step` 불변·`Error`, 틱 전략 2종 해시 일치, 셀프 티커가 `StepActive` 거짓이면 무동작).
6. 빨강 → 수정 → 초록 → EditMode 3회 + PlayMode 3회 → 보고(F17 초안 포함).

=== 프롬프트 끝 ===
