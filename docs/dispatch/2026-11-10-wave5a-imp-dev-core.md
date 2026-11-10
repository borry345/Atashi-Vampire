# 웨이브 5a 디스패치 브리핑 — IMP-DEV-CORE (2026-11-10, PM 작성, **초안 — SPEC 11회차·TECH 20회차 반영 뒤 확정**)

> `general-purpose`(opus), 자기 전용 worktree(origin/main에서 분기 — TECH 19·20회차와 SPEC 11회차가 푸시된 뒤 디스패치). 웨이브 5를 둘로 나눈다: **5a = 개발자 모드 본체**(진입·오버레이·현황 패널·제어·시각화·자동 경로·스폰 정책·호스트 ★ 3개·`LastTargetId`), **5b = 측정·로그**(통계·측정 세션·가드·프리셋·CSV·충돌 이벤트 로그·N스텝 자동 종료·핫 리로드·덤프·E1·E2). 사용자의 당면 목적(버그 탐색·동작 확인)은 5a로 충족된다. 예상 40~55만 토큰(4b 44만과 같은 규모).

=== 프롬프트 시작 ===

# 역할: IMP-DEV-CORE — 뱀서라이크 1차 MVP의 개발자 모드 본체 구현자

당신은 `Survivor.Dev` 어셈블리(`Scripts/Dev/**`)와 그 테스트(`Tests/Dev/**`)를 만든다. 개발자 모드는 **값과 객체를 주입하는 방식으로만** 게임에 개입한다(TAC-F19-2): `IDevModeHost`(App `GameBootstrap`이 구현)를 통해 `SimulationRules` 필드, `IInputSource`·`ISpawnPolicy` 교체, `StrategySelection`, `PendingCommandQueue` 명령, `Time.timeScale`만 만진다. App을 포함한 어떤 게임 어셈블리도 Dev를 참조하지 않는다. 이번 5a 범위는 아래 §1-범위 표의 "5a" 열이다. 측정·CSV·충돌 로그·핫 리로드·N스텝 자동 종료는 **5b**(다음 디스패치)라 만들지 않는다 — 오버레이에 그 자리(비활성 버튼 또는 "(5b)" 레이블)만 둔다.

## 1. 이미 확인된 사실 (다시 조사하지 말 것)

- **자기 전용 git worktree**. cwd가 worktree 루트, Unity 프로젝트는 `<cwd>/Survivor`. 다른 경로는 읽지도 쓰지도 않는다. 동시에 도는 구현자는 없다. Unity **6000.3.28f1**(`C:\Program Files\Unity\Hub\Editor\6000.3.28f1\Editor\Unity.exe`), GUI는 닫혀 있다. 패키지 URP 17.3.0(2D 렌더러)·ugui 2.0.0(TMP 포함)·InputSystem 1.20.1(활성 핸들러 = 새 Input System만)·Newtonsoft 3.2.2. 한 프로젝트에 Unity 인스턴스 하나.
- **기준선(main)**: EditMode **719** / PlayMode **260**, 전부 통과. 36조합 결정론 스위트 해시 `0x7AE2FE65ACEFF8F4`(소요 40~56초), 게이트 결정론 해시 `0xF8CB841EBAE36519`. 당신은 EditMode·PlayMode 수를 바꾸지 않고(`Tests/Dev`는 별도 어셈블리) 기존 테스트가 전부 그대로 통과해야 한다. 테스트 실행 래퍼: `C:\Users\SSAFY\.claude\jobs\f32c1ccc\tmp\flow-run.sh <EditMode|PlayMode> <tag>`(worktree 루트에서; 결과 `flow-<tag>.xml/.log`가 그 폴더에 남는다). 접두사 `dev-`. 임시 파일은 그 폴더에만.
- **호스트**: `Scripts/App/IDevModeHost.cs`(public interface, 15멤버 — 5.15 블록과 이름·타입 일치)와 `GameBootstrap`(public `MonoBehaviour`, `Main.unity`의 `GameBootstrap` 오브젝트, `Awake`에서 조립). **정적 `Current`는 없다** — `Object.FindAnyObjectByType<GameBootstrap>()`로 얻는다(DECISIONS 9절 P1). 못 찾으면 로그 1줄 뒤 붙지 않는다. `GameBootstrap.Flow`(public `GameFlowController`: `State`·`ResumeState`·`ShowsRuntimeError`·`LastOutcome`·`StartRun()`·`ExitToTitle()`·`TogglePause()`·`Quit()`), 그 외 `Presenter`·`FrameCounter`·`_registry` 등은 internal/private — **`IDevModeHost`에 없는 것은 쓰지 않는다**(아래 ★ 3개를 당신이 추가한다).
- **★ 당신이 추가하는 호스트 멤버 3개**(TECH 5.15 19회차, 줄 단위 수정권): `int LastFrameSteps`(= `SimulationFrameCounter.LastFrameSteps`), `int ActiveGemCount`(= `PickupRegistry.ActiveGemCount`), `int LiveMonsterCount`(= `MonsterRegistry.Count`, 보스 포함). `IDevModeHost.cs`에 선언 3줄, `GameBootstrap.cs`에 구현 각 1줄(`_frameCounter`·`_pickups`·`_registry` 같은 private 필드명은 코드에서 확인). 그 두 파일의 **다른 줄은 손대지 않는다**.
- **★ `WeaponInstance.LastTargetId`**(TECH 5.12 19회차): `public EntityHandle LastTargetId { get; internal set; }` 속성 1개 추가 + `ProjectileWeapon.cs:19` 근처와 `ZoneWeapon.cs:26` 근처의 `TryFindNearest` 호출 직후 `weapon.LastTargetId = hit.Id;` 대입 1줄씩(실패 시 공급자가 `hit = default`를 두므로 분기 없음). `PlayerProjectile`(유도 재조준)은 제외. 세 파일의 다른 줄은 손대지 않는다. `EntityHandle` 기본값 = "없음".
- **IDevModeHost 의미(코드 확정)**: `NextRunSelection`·`NextRunCollisionDecorator`·`InputOverride`·`SpawnPolicyOverride`·`FixedSeed`는 **다음 판 시작(`Reset`)에만** 적용(TAC-F8-2 ④). `RequestTargetProviderSwitch(kind)`만 즉시(다음 Begin). `StartNewRun()`은 상태 검사 없이 `GameFlowController.StartRun()`에 위임한다(데이터 플레이 불가일 때만 거부) — **호출 책임은 당신**: `DevController`가 `Flow.State`가 Title·Result·Paused일 때만 부른다. `RunEnded`는 사망·클리어 결과 처리 뒤 발생. `BossSpawnedThisRun`은 `SpawnBossNow` 전 검사용("Boss: one per run" 표시, 명령 넣지 않음).
- **시계(5.03, `SimulationClock`)**: `Step`, `IsRunning`, `GameSeconds`, `CloseReason`, `CloseSequence`, `Open()`, `Close(reason)`, `CloseAfterCurrentStep(reason)`, `RequestSingleStep()`, `RequestJump(targetStep)`(과거면 `ArgumentOutOfRangeException` — 당신이 먼저 거부해 오버레이에 표시, TAC-F19-3), `Reset()`. `GameFlowController`는 `StopAtStep`·`SingleStepDone`·`Paused`·`RunReset` 닫힘을 무시한다(5.17 "no-op; dev handles"). **알려진 동작**: `StopAtStep`로 닫혀도 `Flow.State`는 Playing으로 남아 ESC가 닫힘 사유를 덮어쓴다 → `StopAtStep`는 5b 배치 전용이라 5a에서는 쓰지 않는다. 한 스텝 진행은 `RequestSingleStep()`만.
- **대기 명령(5.15, `PendingCommandQueue`·`PendingCommand` 정적 팩토리)**: `SetLoadout(object loadout)`(Payload는 `IReadOnlyList<WeaponLoadoutEntry>` — PLAYER 타입, 5.14), `SetHealth(float)`, `AddXp(int)`, `LevelUpNow()`, `SpawnMonstersNow(id, count, position, radius, ignoreCap)`, `SpawnBossNow(id, position)`, `SpawnChestNow(position)`, `SetDespawnDistance(float)`. 핸들러 등록은 App이 이미 했다 — 당신은 `host.Commands.Enqueue(...)`만 한다. 보스 ID는 `host.Data.Current.SpawnTable.BossMonsterId`(정확한 속성명은 Data 코드에서).
- **배속(5.15.1, D9)**: `SetTimeScale(x)` = `Time.timeScale = Mathf.Clamp(x, GameRules.MinTimeScale, GameRules.MaxTimeScale)`; 이어서 `Time.maximumDeltaTime = StepMath.MaxCatchUpSteps * StepMath.StepSeconds / Time.timeScale`. 판 시작마다 초기화는 **당신이** `RunEnded` 처리와 자기 `StartNewRun` 호출 경로에서 한다(App은 하지 않음, P3). 실제 배속 = 최근 1초(비스케일) `LastFrameSteps` 합 / 60.
- **자동 경로(5.05)**: `AutoPathInputSource(Vector2 center, float radius, float speedUnitsPerSecond, float startAngleRad) : IInputSource`. 스텝 s의 목표점 = `center + radius·(cos φ, sin φ)`, `φ = start + (speed/radius)·(s+1)/60`; 반환 방향 = `(목표점 − 현재 위치)` 정규화, `SpeedOverride = min(speed, 거리 × 60)`. `IInputSource`의 실제 시그니처는 `Scripts/Core/IInputSource.cs`에서 읽는다(현재 위치를 어떻게 받는지 포함).
- **스폰 정책(5.15 표)**: `ISpawnPolicy { void CollectSpawns(in StepContext context, int liveMonsterCount, DeterministicRng spawnRng, List<string> monsterIdsOut); bool BossSpawnDue(long step); }`. `DevSpawnPolicy`: 스폰 중단 / 특정 종만 / N 유지(죽거나 디스폰되면 같은 스텝에 보충 — `liveMonsterCount` 인자로 판단) / 즉시 N기(`SpawnMonstersNow` 명령 쪽이 아니라 정책이 다음 스텝에 채우는 방식 중 PM [기본값]: **즉시 N기는 `SpawnMonstersNow` 명령**, N 유지는 정책). 보스 자동 등장 없음(`BossSpawnDue` = false). N을 바꾸면 D를 R26으로 자동 설정: N ≤ 400 → 45, 초과 → `45·√(N/400)` (`SetDespawnDistance` 명령). 400 초과는 `Rules.MonsterCap = int.MaxValue`(상한 해제 토글).
- **시각화(TAC-F19-4, U33 미실측)**: `DevVisualizer`는 `RenderPipelineManager.endCameraRendering` 콜백 + `GL` 선. 전부 꺼지면 콜백 해제 + `enabled = false`. 그릴 것: 공간 해시 셀 경계 + 셀별 수(`host.SpatialIndex` — `CellCounts`·`CellSize`·`Required`가 있다; 비활성이면 "index inactive"), 충돌 반경(위치 원본은 `SimEntity.Position`, 반지름 `Body.Radius` — 엔티티 열거는 `host.Session.Lifecycle`에서; 5.10에서 공개 열거 API를 읽는다), 무기 → 타깃 선(`host.Session.Weapons`의 각 인스턴스 `LastTargetId` → `Lifecycle`에서 핸들 조회), 스폰 원 r=20·디스폰 원 D. U33이 안 되면(그려지지 않음) `LineRenderer` 풀로 대체하고 보고.
- **오버레이(5.15 표, 사용자 결정 2026-11-10)**: IMGUI 한 창, **영어 레이블**, 폰트는 `Font.CreateDynamicFontFromOSFont("Malgun Gothic", 14)`(null이면 기본 폰트). 숨기면 `enabled = false`(`OnGUI` 0회). F1 토글은 `DevModeRoot.Update`에서 `Keyboard.current?.f1Key.wasPressedThisFrame`(새 Input System). **현황 패널(Status)** 필수(사용자: "어떤 방식으로든 보유 스킬이나 기타 정보에 대한 현황이 꼭 표시"): 보유 무기 `displayName Lv n`(진화는 `(evolved)`), 패시브 `displayName Lv n`, 현재 몬스터 체력 배율(SPEC 11회차 — 스폰 테이블 구간별 `hpMultiplier`. **웨이브 6(IMP-DATA·MONSTER·FLOW)에서 Data API가 생긴 뒤 연결한다. 5a에서는 `HP mult: n/a` 고정 문자열**로 두고 `DevStatusFormatter`에 인자 자리만 만든다 — 코드에 없는 멤버를 참조하지 않는다), 게임 시간·스텝, 시드(`FixedSeed` 또는 "random"), 생존 몬스터 수, 활성 젬 수, 현재 전략 조합 4종 + CCD + 셀 크기, 실제 배속, 흐름 상태. 텍스트 조립은 **순수 함수**(예: `DevStatusFormatter.Build(...)` static, Unity 객체 의존 없음)로 두어 EditMode에서 검증한다.
- **활성화(TAC-F19-1·10, U16·U40 미실측)**: `DevModeEntry`의 `[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]`가 `DevModeActivation.IsRequested(Environment.GetCommandLineArgs())`를 정확히 1회 호출. 거짓이면 즉시 반환(Dev `MonoBehaviour` 0개). `IsRequested`: `--dev` 대소문자 무시; 에디터에서는 `EditorPrefs` `Survivor.DevModeOnPlay`도 본다(`#if UNITY_EDITOR`). 참이면 `FindAnyObjectByType<GameBootstrap>()` → `DevModeRoot` 생성(`DontDestroyOnLoad` 아님 — 씬 하나) → `Rules.RecordBestTime = false`. **테스트 훅** PM [기본값]: `internal static DevModeRoot DevModeEntry.Attach(IDevModeHost host)` / `internal static void Detach()` + `Scripts/Dev/DevAssemblyInfo.cs`의 `InternalsVisibleTo("Survivor.Tests.Dev")`. 배치 테스트 실행 인자에는 `--dev`가 없다(있어도 안 된다 — 기존 PlayMode 260이 Dev 없이 돌아야 함).
- **Editor 메뉴**: `Assets/Editor/Survivor.Editor.asmdef`는 **이미 있다**(4d, 참조 Core·Data, Editor 전용, autoReferenced false). 당신은 그 폴더에 `DevModeMenu.cs`만 추가한다: 메뉴 `Survivor/Dev Mode On Play`(체크 토글, `EditorPrefs` 키 `Survivor.DevModeOnPlay`). asmdef 참조 추가가 필요하면(없어야 정상 — `EditorPrefs`뿐) 보고.
- **asmdef 참조 표(5c.1)는 `Tests/EditMode/Architecture/AsmdefReferenceTests.cs`의 `Table`이 원본**이고 이미 `Survivor.Dev`·`Survivor.Tests.Dev` 행이 있다. 당신의 두 asmdef는 그 행과 **정확히** 맞아야 한다(그 테스트가 EM에 포함돼 있어 틀리면 빨강). `Survivor.Dev`: 참조 Core, Data, Simulation, Strategies, App + `Unity.InputSystem`, `Unity.RenderPipelines.Core.Runtime`(`RenderPipelineManager`); `defineConstraints: ["!SURVIVOR_NO_DEV"]`; `autoReferenced` false 권장(표가 자동 참조를 어떻게 보는지 코드에서 확인). `Survivor.Tests.Dev`: `Tests/PlayMode/Survivor.Tests.PlayMode.asmdef`를 본떠(`overrideReferences: true`, `precompiledReferences: ["nunit.framework.dll"]`, `defineConstraints: ["UNITY_INCLUDE_TESTS", "!SURVIVOR_NO_DEV"]`) 참조 Core, Data, Simulation, Strategies, UI, App, Dev + `UnityEngine.TestRunner`, `UnityEditor.TestRunner`. `includePlatforms: []`(EditMode·PlayMode 테스트를 한 어셈블리에 두면 EditMode 테스트가 플레이어 빌드에 들어가는 문제는 `UNITY_INCLUDE_TESTS`로 막힌다 — 기존 PlayMode asmdef와 같은 방식). 표의 행과 당신 asmdef가 어긋나는데 표가 틀렸다고 판단되면 **고치지 말고 보고**(FLOW 소유).
- **PlayMode 테스트 픽스처**: `Tests/PlayMode/Flow/FlowPmFixture.cs`(씬 `Assets/Scenes/Main.unity`를 경로로 로드, `GameBootstrap.BestRecordKeyOverride`·`DataRootOverride` 훅(internal static — `InternalsVisibleTo`는 `Survivor.Tests.PlayMode`·`EditMode`뿐이라 **당신 어셈블리에서는 안 보인다**), `Time.captureDeltaTime = 1/60`, 종료 시 빈 씬 생성 후 언로드·`Physics2D`/시간 전역 복원)는 다른 어셈블리라 재사용 불가 — 같은 골격을 `Tests/Dev/DevPmFixture.cs`로 **최소한으로 복제**한다(훅 2개는 못 쓰므로 기록 키 오염을 피하려면 `Rules.RecordBestTime = false`를 Attach 직후 — 어차피 Dev가 하는 일). 복제는 60줄 안팎으로 제한.
- **데이터**: `host.Data`는 `GameDataRegistry`(인스턴스, `.Current` = `GameData`). 무기 정의 `displayName`(한글), 패시브 `displayName`, 스폰 테이블 구간. 무기 레벨·보유는 `host.Session.Weapons`(`WeaponInventory`)·`Passives`(`PassiveInventory`)에서 읽는다 — 공개 열거 API 이름은 5.12·코드에서.
- SPEC F19 설계 제약: 게임 로직에 `if (devMode)` 분기를 흩뿌리지 않는다. 당신이 게임 어셈블리에 쓰는 줄은 위 줄 단위 수정권 **8줄**(호스트 3+3, 무기 1+1)뿐이다.

## 2. 배타적 파일 소유권

### 소유 경로

1. `Survivor/Assets/Scripts/Dev/**` — `Survivor.Dev.asmdef`, `DevAssemblyInfo.cs`, `DevModeEntry.cs`, `DevModeActivation.cs`, `DevModeRoot.cs`, `DevOverlay.cs`, `DevController.cs`, `DevStatusFormatter.cs`(이름 제안), `DevSpawnPolicy.cs`, `AutoPathInputSource.cs`, `DevVisualizer.cs`, `DevLog.cs`(영역 `[Dev]`). 5b 클래스(`LoggingCollisionStrategy`·`CollisionEventLog`·`StatsCollector`·`Measurement*`·`CsvWriter`·`HotReloadService`·`DataDumpWriter`)는 만들지 않는다.
2. `Survivor/Assets/Editor/DevModeMenu.cs`(신규; asmdef는 기존 것 사용, 수정 금지).
3. `Survivor/Assets/Tests/Dev/**` — `Survivor.Tests.Dev.asmdef`, EditMode·PlayMode 테스트, `DevPmFixture.cs`.
4. **줄 단위 수정권(그 줄 외 수정 금지, diff로 증명)**: `Scripts/App/IDevModeHost.cs`(+3 선언), `Scripts/App/GameBootstrap.cs`(+3 구현), `Scripts/Simulation/Weapons/WeaponInstance.cs`(+1 속성), `Scripts/Simulation/Weapons/ProjectileWeapon.cs`(+1 대입), `Scripts/Simulation/Weapons/ZoneWeapon.cs`(+1 대입).
5. Unity 자동 생성 `.meta`.

### 건드리지 않는 것

- 위 5파일의 다른 줄, `Scripts/Core|Data|Strategies|Simulation|UI|App/**`의 나머지 전부, `Tests/EditMode|PlayMode/**` 전부(`AsmdefReferenceTests.cs` 포함), `Scenes/**`, `Settings/**`, `ProjectSettings/**`, `Packages/**`, `StreamingAssets/**`, `Resources/**`, `TextMesh Pro/**`, `Editor/Survivor.Editor.asmdef`, `Editor/AssetKeyRegistryGenerator.cs`.
- 소유 밖 결함은 **고치지 말고 보고**(PM이 소유자를 재개한다). `indiv` 루트 문서는 읽기만. **`RUN_LOG.md`·`docs/dispatch/**`는 열지 않는다.**

## 3. 검증 재현성

- `Library/`·`Temp/`·`Logs/` 삭제 금지. 패키지 수정 = BLOCKED. `git commit`/`push`/`reset`/`git rm`, `rm -rf` 금지.
- 같은 도구 호출이 같은 이유로 2회 연속 실패하면 세 번째를 시도하지 말고 보고.
- **보고 수치는 PM이 같은 명령으로 재현 가능**해야 한다. 마지막 3회 XML 경로. 기존 EditMode 719·PlayMode 260이 **그대로** 통과해야 하고, 당신 테스트는 `Survivor.Tests.Dev` 수로 따로 보고한다(같은 `-testPlatform` 실행에 합산되어 나오므로 719/260 + Dev 수를 분리해 적는다).
- 메모리: Unity 배치 인스턴스는 한 번에 하나. 테스트 실행 중 다른 Unity를 띄우지 않는다.

## 4. 결정성

- 고정 시간 대기를 정착 수단으로 쓰지 않는다. PlayMode 테스트는 `clock.Step`·`CloseSequence`·프레임 수 상한(`MaxFrames`)으로 기다린다.
- 결정론 테스트(SPEC F19 수용 기준 "같은 시드·같은 설정으로 N스텝 → 몬스터 수·위치 동일"): 고정 시드 4242, 레벨업 비활성, 자동 경로(반지름 6, 속도 4), 무적, `DevSpawnPolicy` N=100 유지, 600스텝 × **2회 연속 실행**(같은 씬 로드 2회) → `SimulationStateHash.Compute(host.Session)` 일치 + `LiveMonsterCount` 일치. 합격 기준은 "전체 스위트 연속 3회 전회 동일".
- `Time.timeScale`·`maximumDeltaTime`·`captureDeltaTime`·vSync·`targetFrameRate`는 테스트가 바꿨으면 반드시 복원한다(다른 픽스처가 본다).

## 5. 기준선 — 정상 동작인데 결함으로 오판하기 쉬운 것, 그리고 PM 기본값

1. `GameFlowController`는 `StopAtStep`·`SingleStepDone`·`Paused`·`RunReset` 닫힘을 무시한다 — 정상. 한 스텝 진행 뒤 게이트가 `SingleStepDone`으로 닫혀 있으면 **당신이** 다음 스텝 요청 때 `RequestSingleStep()`을 다시 부른다(5.03의 정확한 의미 — 열고 1스텝 뒤 닫는지 — 는 코드에서 읽는다).
2. `StartNewRun()`은 Playing 중에도 리셋한다 — App은 검사하지 않는다(P5). 당신이 상태를 검사한다.
3. `InputOverride`·`SpawnPolicyOverride`·`FixedSeed`·`NextRunSelection`은 **다음 판**부터다. 오버레이에 "(next run)" 표기. 타깃 공급자만 즉시.
4. `SpawnBossNow`는 보스가 이미 나왔으면 큐가 조용히 소비한다 — 당신이 먼저 `BossSpawnedThisRun`을 보고 표시.
5. `PendingCommand.SpawnMonstersNow`는 `count <= 0`이면 예외, `SetDespawnDistance`는 양의 유한값 아니면 예외 — 오버레이 입력을 먼저 걸러라.
6. 배치(`-nographics`)에서는 `OnGUI`가 돌지 않을 수 있고 OS 폰트가 null일 수 있다 — 테스트는 `OnGUI`·폰트에 의존하지 않는다(폰트 null 경로가 정상 동작임을 테스트로 확인).
7. `Survivor.Tests.PlayMode`는 `Survivor.Data`를 참조하지 않는다(정리 후보) — 당신 어셈블리는 참조하므로 무관.
8. EditMode 테스트에서 `SimEntity` 파생 더블은 `AddComponent` 불가(Editor 전용 어셈블리) — Dev EditMode 테스트는 순수 C#(`DevStatusFormatter`, `DevModeActivation.IsRequested`, `AutoPathInputSource` 기하, `DevSpawnPolicy` 산출 목록, R26 D 계산, 배속 클램프·`maximumDeltaTime` 허용 오차 1e-6)만.
9. `PlayerEntity`는 루트 `Player.Magnet` 오브젝트를 따로 만든다 — 씬 언로드 때 같이 사라지므로 PlayMode 픽스처는 신경 쓰지 않아도 된다(EditMode 리그만 수동 정리가 필요했다).
10. 엔진 충돌 전략 `Clear()`가 경고를 낼 수 있다 — 정리 후보, 당신 책임 아님.
11. 36조합 스위트가 40~56초 걸린다 — 정상.
12. `LogAssert.ignoreFailingMessages`는 `[SetUp]`에서 켜도 본문으로 이어지지 않는다(test-framework 1.6) — 오류 로그를 내는 테스트는 본문에서 `LogAssert.Expect`.
13. PM 기본값(반박하려면 먼저 실측): 테스트 훅 이름 `Attach/Detach`; 즉시 N기는 명령·N 유지는 정책; `StopAtStep`·측정·로그·핫 리로드는 5b; 현황 패널은 매 프레임 갱신(오버레이 열렸을 때만); 시각화 레이블(셀별 수)은 IMGUI `GUI.Label`로 오버레이가 열려 있을 때만; 상자 즉시 스폰 위치·보스 소환 위치는 플레이어 위치 + 오프셋(3, 0); 즉시 N기 생성 반지름은 스폰 반경 20.

## 6. 금지

- 시크릿 파일 열람 금지. git 변경 명령·패키지 설치·`rm -rf` 금지.
- **단언을 느슨하게 고쳐 초록 금지.** 기존 테스트 719/260 중 하나라도 빨강이면 수정이 아니라 보고(당신의 8줄이 원인인지 먼저 확인).
- 문서 공개 시그니처·이름 변경 금지(반박으로). `IDevModeHost`에 ★ 3개 외 멤버 추가 금지.
- 주석 영어 산문, 공개 멤버 한 줄 요약(`CODING_GUIDE.md` §3). 네이밍 §2.2. 파일명 = 주 클래스명. 파일당 공개 타입 1개. 오버레이 레이블은 영어(데이터 유래 한글 문자열은 그대로 표시).
- 로그: `DevLog`(`[Dev]`) 또는 `Debug.Log` 최소. 프레임마다 로그 금지. 로그·보고에 시크릿 금지.
- 리플렉션으로 internal/private에 접근 금지(`FrameCounter`·`_registry` 등은 ★ 멤버로 노출한 것만).

## 7. 보고 형식 (압축)

1. 만든 파일 목록(타입, TAC·절). 5c.2 IMP-DEV 행 대조. 줄 단위 수정권 5파일의 **diff 줄 수**(각 +3/+3/+1/+1/+1이어야 함).
2. 테스트 결과: EditMode 3회 + PlayMode 3회 원문(기존 719/260 + Dev 수 분리) + PM 재현 명령 + 결정론 테스트 해시.
3. 계약 대조표(5.15 표 × 구현, `IDevModeHost` 18멤버) + ★ 추가 공개 멤버 전부(의미 한 줄).
4. **실측 결과**: U16(`--dev` 인자 수신 — 배치 `-executeMethod` 또는 에디터로 확인한 방법), U33(URP `endCameraRendering` + GL 가시성 — 배치에서 못 보면 "미확인"), U40(AfterSceneLoad가 `Awake` 뒤인지 — `FindAnyObjectByType` 성공 여부로).
5. **사용자가 할 일**: 에디터에서 켜는 법(메뉴), 빌드 실행 인자, F1, 오버레이 사용법 한 문단(F19 README 절 초안의 씨앗).
6. PM 기본값 중 따르지 않은 것과 이유(먼저 실측). 소유 밖 결함(고치지 않고). 읽을 목록 밖 파일.
7. 확신하지 못하는 것(항상 마지막).

## 8. 읽을 문서 (이 목록 밖의 기획 문서는 열지 마세요. 열어야 했다면 보고에 적으세요)

- 공통: `spec/00-common.md`(3.0 B·F·H), `tech/README.md`(색인만), `tech/02-feature-tac.md`(TAC-F19-1~10, TAC-F16-5, TAC-F8-2), `tech/5a-shared-rules.md`(R20·R26), `tech/5c-ownership-contracts.md`(5c.1 참조 표, 5c.2 IMP-DEV 행, 5c.3a 조립 표, 5c.5 기준선 문단).
- 필수: `spec/F19-dev-mode.md`, `tech/5.15-dev-mode.md`(5.15.2·5.15.3은 5b — 훑기만), `tech/5.05-input.md`, `tech/5.17-flow-state.md`, `tech/5.14-progression.md`(`SetLoadout`·`WeaponLoadoutEntry`·`LevelUpNow`), `tech/5.03-step-clock.md`(`RequestSingleStep`·`RequestJump`·`StopAtStep`).
- 계약만: `tech/5.07-targeting.md`(`TargetProviderKind`), `tech/5.09-collision.md`(`DrivesTransform` 의미 — 위치 원본은 `SimEntity.Position`), `tech/5.10-lifecycle.md`(엔티티 열거), `tech/5.11-data-loader.md`(`GameDataRegistry.Current`, 정의 `displayName`), `tech/5.12-damage-rules.md`(`WeaponInstance`·인벤토리 열거), `tech/5.16-state-hash.md`.
- 결정 배경: `DECISIONS.md` 9절(개발자 모드)만.
- 코드: `Scripts/App/IDevModeHost.cs`·`GameBootstrap.cs`·`GameFlowController.cs`, `Scripts/Core/IInputSource.cs`·`ISpawnPolicy`가 있는 파일·`SimulationRules.cs`·`GameRules.cs`·`StepMath.cs`·`EntityHandle.cs`, `Scripts/Simulation/Loop/SimulationClock.cs`·`PendingCommand*.cs`·`SimulationStateHash.cs`, `Scripts/Simulation/Weapons/WeaponInstance.cs`·`ProjectileWeapon.cs`·`ZoneWeapon.cs`·`WeaponInventory.cs`, `Scripts/Simulation/Player/PassiveInventory.cs`, `Scripts/Strategies/Spatial/MonsterSpatialIndex.cs`, `Tests/PlayMode/Flow/FlowPmFixture.cs`(복제 원본), `Tests/EditMode/Architecture/AsmdefReferenceTests.cs`(`Table`), 두 테스트 asmdef.

## 작업 순서 제안

1. 문서·코드 읽기 → 두 asmdef + `DevAssemblyInfo` + 빈 `DevModeEntry` → 배치 EditMode 1회(asmdef 표 테스트 초록 확인).
2. 줄 단위 수정 8줄(★ 3+3, `LastTargetId` 1+1) → 기존 EM/PM 1회(719/260 유지 확인).
3. `DevModeActivation`·`AutoPathInputSource`·`DevSpawnPolicy`·`DevStatusFormatter` + EditMode 테스트.
4. `DevModeEntry`(Attach/Detach)·`DevModeRoot`·`DevController`(배속·점프·한 스텝·상태 조작·스폰 제어·무기 구성·전략 토글·무적·공격 on/off·레벨업 비활성·시드·자동 경로) → `DevPmFixture` + PlayMode 테스트(비활성 시 Dev 인스턴스 0, Attach 뒤 `RecordBestTime` false, 점프 과거 거부, 배속 클램프·`maximumDeltaTime` 1e-6, `SpawnMonstersNow` 뒤 수, `SetLoadout` 뒤 레벨, 결정론 2회 해시, `LastTargetId`가 발사 뒤 비어 있지 않음).
5. `DevOverlay`(IMGUI, 영어, OS 폰트 null 폴백, 현황 패널) · `DevVisualizer` · `DevModeMenu`.
6. 빨강 → 수정 → 초록 → EM 3회 + PM 3회 → 보고.

=== 프롬프트 끝 ===
