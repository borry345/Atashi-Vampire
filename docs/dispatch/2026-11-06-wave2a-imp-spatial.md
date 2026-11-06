# 웨이브 2a 디스패치 브리핑 — IMP-SPATIAL (2026-11-06, PM 작성)

> `general-purpose`, `model: opus`, `isolation: worktree`. "=== 프롬프트 시작 ===" 이후를 그대로 보낸다. 격리 방식은 DECISIONS 절차-D5.
> **PM 기본값**: `Physics2DTargetProvider`(구현 C)는 `PhysicsBodySynchronizer.TryGetBody`(IMP-COLLISION, 웨이브 3)에 의존하므로 **웨이브 3으로 미룬다**(5c.4가 SPATIAL을 웨이브 2에 두면서 이 의존을 놓쳤다 — tech 14회차에서 정정). 이번 디스패치는 A·B와 공간 해시만.

=== 프롬프트 시작 ===

# 역할: IMP-SPATIAL — 뱀서라이크 1차 MVP의 공간 해시·타깃 공급자 구현자

당신은 Unity 6 / C# 프로젝트의 **공간 해시(`SpatialHash`, `MonsterSpatialIndex`, `SpatialHashSettings`)**와 **타깃 공급자 2종(A `BruteForceTargetProvider`, B `SpatialHashTargetProvider`)**, 그 **EditMode 테스트**를 구현한다(`Survivor.Strategies`의 `Spatial/`·`Targeting/`). 세 번째 공급자 C(`Physics2DTargetProvider`)는 웨이브 3 충돌 구현자의 클래스에 의존하므로 **이번에는 만들지 않는다**. 타깃 공급자는 무기 전용 최근접 몬스터 질의이고, 측정 실험(F16)에서 서로 교체되며 **반환이 비트 단위로 같아야** 한다.

## 1. 이미 확인된 사실 (다시 조사하지 말 것)

- **당신은 자기 전용 git worktree에서 작업한다.** cwd가 worktree 루트, Unity 프로젝트는 `<cwd>\Survivor`. 다른 경로(원본 저장소 `C:\Users\SSAFY\Desktop\borry\indiv` 등)는 읽지도 쓰지도 않는다. IMP-DATA·IMP-POOL이 같은 시각에 각자의 worktree에서 일한다 — 당신 worktree에 그들의 파일이 없는 것이 정상이다.
- Unity **6000.3.28f1**, 에디터 `C:\Program Files\Unity\Hub\Editor\6000.3.28f1\Editor\Unity.exe`, GUI는 닫혀 있다. worktree의 `Survivor/Library`가 없어 **첫 배치 실행은 3~5분**, 이후 15초 안팎.
- 이미 main에 있는 것: `Scripts/Core/`(웨이브 1 완료, 읽기만), `Scripts/Strategies/Survivor.Strategies.asmdef`(**뼈대, 참조 `Survivor.Core`만. 소유자 IMP-LOOP** — 수정하지 않는다), `Tests/EditMode/Survivor.Tests.EditMode.asmdef`(참조: Survivor.Core, UnityEngine.TestRunner, UnityEditor.TestRunner — **당신은 `references`에 `"Survivor.Strategies"` 한 항목만 추가**; DATA가 `Survivor.Data`를 같은 자리에 추가하므로 PM이 합친다), `Tests/PlayMode/Survivor.Tests.PlayMode.asmdef`(C의 PlayMode 테스트가 웨이브 3으로 밀렸으므로 이번에는 건드리지 않는다).
- Core에서 당신이 쓰는 것(`Scripts/Core/` 코드가 원본 — 문서와 다르면 코드가 맞다): `ITargetProvider`, `TargetProviderKind`, `MonsterSnapshot`, `MonsterSnapshotBuffer`, `TargetHit(id, position, distanceSquared)`, `TargetOrdering.IsBetter`(R6), `MonsterSizeRules.IsLarge`(R25 — 스냅샷의 `IsLarge`는 LOOP의 `MonsterRegistry`가 채워 넣는다; 당신은 그 플래그를 읽기만), `IStepPhaseHandler`·`SimulationPhase`·`StepContext`(5.03), `StrategySelection.DefaultSpatialHashCellSize`(**public const 2f, 13회차** — 셀 크기 기본값의 단일 소유자), `EntityHandle`(옛 이름 `EntityId`, 13회차 개명), `ProfilerMarkers.Targeting`·`SpatialIndex`(R16).
- 웨이브 0 실측 중 당신과 관련된 것: U32(Script 모드 `rb.position` 대입이 즉시 `OverlapCircle`에 반영)는 C 전용이라 이번엔 무관.
- 테스트 실행 명령(PM이 그대로 다시 돌린다):
  ```
  "C:\Program Files\Unity\Hub\Editor\6000.3.28f1\Editor\Unity.exe" -batchmode -nographics -projectPath "<cwd>\Survivor" -runTests -testPlatform EditMode -testResults "C:\Users\SSAFY\.claude\jobs\f32c1ccc\tmp\spatial-em-<n>.xml" -logFile "C:\Users\SSAFY\.claude\jobs\f32c1ccc\tmp\spatial-em-<n>.log"
  ```
  Bash timeout 600000ms. 컴파일 오류면 테스트 0건 + 로그 `error CS`. 종료 코드 0/2/3. XML `<test-run>`의 `total/passed/failed/inconclusive/skipped`. 전체 스위트에는 Core 205건이 함께 돈다.
- 임시 파일은 `C:\Users\SSAFY\.claude\jobs\f32c1ccc\tmp\`에 `spatial-` 접두사로. worktree 안에 임시 파일 금지. 파일은 LF·UTF-8(BOM 없음). Strategies 코드에 한국어는 없어야 정상이다.

## 2. 배타적 파일 소유권

### 소유 경로 (이 밖에는 만들거나 고치지 않는다)

1. `Survivor/Assets/Scripts/Strategies/Spatial/**` — `SpatialHash.cs`, `MonsterSpatialIndex.cs`, `SpatialHashSettings.cs`.
2. `Survivor/Assets/Scripts/Strategies/Targeting/**` — `BruteForceTargetProvider.cs`, `SpatialHashTargetProvider.cs`. (`Physics2DTargetProvider.cs`는 이번에 만들지 않는다.)
3. `Survivor/Assets/Tests/EditMode/Spatial/**`.
4. `Survivor/Assets/Tests/EditMode/Survivor.Tests.EditMode.asmdef` — `references`에 `"Survivor.Strategies"` 추가만.
5. Unity가 자동 생성하는 `.meta`(손으로 만들지 않는다).

### 건드리지 않는 것

- `Scripts/Core/**`, `Scripts/Strategies/Survivor.Strategies.asmdef`(LOOP), `Scripts/Strategies/Pool/**`(POOL), `Scripts/Strategies/Collision/**`(없음, COLLISION), `Scripts/Simulation/**`(없음, LOOP), `Tests/PlayMode/**`, `Scenes/`, `Settings/`, `ProjectSettings/`, `Packages/`.
- **스텁 금지.** `MonsterRegistry`(스냅샷을 채우는 쪽)·`TargetProviderSlot`·`SimulationDriverMid`는 LOOP 것이다. 테스트는 Core의 `MonsterSnapshotBuffer`를 직접 채워 쓴다(`Add`는 ID 오름차순 — 테스트도 그 순서를 지킨다).
- `indiv` 루트 문서는 읽기만. **`RUN_LOG.md`는 열지도 쓰지도 않는다.**

## 3. 검증 재현성

- `Library/`·`Temp/`·`Logs/` 삭제 금지. 패키지 수정 = BLOCKED 보고. `git commit`/`push`/`reset`/`git rm`, `rm -rf` 금지.
- 같은 도구 호출이 같은 이유로 2회 연속 실패하면 세 번째를 시도하지 말고 보고.
- **보고 수치는 PM이 같은 명령으로 재현 가능해야 한다.** 마지막 3회 XML 경로를 적는다.

## 4. 결정성

- 테스트에 `UnityEngine.Random`·시드 없는 `System.Random`·`DateTime.Now`·`Time.*` 금지. 무작위 배치는 Core `DeterministicRng`에 고정 시드를 준다.
- **A=B 동등성 테스트**(5c.5, TAC-F16-1): 무작위 배치 10,000건 + 격자점 동거리 배치 + 큰 몬스터 포함 배치에서 두 공급자의 `TryFindNearest` 결과(`bool`과 `TargetHit`의 `Id`·`Position`·`DistanceSquared`)가 **비트 단위로** 같아야 한다(`float` 비교는 `==`가 아니라 비트 또는 `Equals` — 허용 오차 비교는 이 테스트에서는 틀린 단언이다). 질의점도 다양하게(몬스터 바로 위, 셀 경계, 아주 멀리, 음수 좌표).
- **합격 기준: EditMode 전체 스위트 연속 3회 전회 동일(총 N, 통과 N, 실패 0).** 세 줄 그대로 보고.

## 5. 기준선 — 정상 동작인데 결함으로 오판하기 쉬운 것

1. **셀 키는 R14 그대로**: `cx = FloorToInt(x / cellSize)`, `cy = FloorToInt(y / cellSize)`, `key = ((long)cx << 32) | (uint)cy`. 음수 좌표에서 `(uint)cy` 캐스팅이 의도된 것이다(하위 32비트에 2의 보수 그대로). `Mathf.FloorToInt`를 쓴다(`(int)` 캐스팅은 음수에서 0 쪽으로 잘려 틀린다).
2. **거리는 제곱거리 float**이고 타이브레이크는 `TargetOrdering.IsBetter(d2, id, bestD2, bestId)`(R6) 한 곳이다. 두 공급자가 **같은 순서로 같은 식**을 써야 비트 동일성이 나온다 — A(전수)와 B(해시)는 후보를 보는 순서가 달라도 `IsBetter`의 엄격한 타이브레이크 때문에 결과가 같아야 한다. 만약 다르면 `IsBetter`를 다르게 쓴 것이다. `(b − a).sqrMagnitude`를 쓰고 직접 `dx*dx+dy*dy`를 쓰지 않는다(연산 순서가 달라 비트가 갈릴 수 있다 — 둘 다 같은 식을 쓰면 상관없지만 안전하게 통일).
3. **큰 몬스터(`IsLarge`)는 해시에 들어가지 않고** `QueryCircle`·고리 탐색 뒤 **항상 별도로 전수 검사**한다(5.07, R25, TAC-F16-6). `QueryCircle`의 결과에는 모든 `IsLarge` 항목이 포함된다(5.07 코드 주석). `IsLarge` 판정 자체는 LOOP가 스냅샷에 넣어 준다 — 당신은 재판정하지 않는다.
4. **고리 탐색 종료 조건**(TAC-F16-2): "고리 k를 끝냈고 `bestD2 ≤ (k·cellSize)²`" 또는 "점유 셀 범위를 벗어남". 두 번째 조건이 없으면 몬스터가 아주 멀리 하나뿐일 때 무한히 돈다. `MaxHashedRadius`(스냅샷)는 반지름 보정이 필요하면 쓴다 — 단 최근접 질의는 **중심점 거리** 기준이다(R6, SPEC F16 "가까운 적" = 중심 거리). 반지름을 거리에서 빼지 않는다.
5. **`MonsterSpatialIndex`는 `IStepPhaseHandler`로 BuildIndex order 10에 등록**되고, `OnPhase(BuildIndex)`에서 `Required`일 때만 재구축한다(5.07, 5c.3 #9). `Required`는 조립 루트가 R19·전략 선택으로 정한다(당신은 프로퍼티만). 등록 자체는 LOOP/App 몫이다. 다른 단계에서 `OnPhase`가 불리면 아무것도 하지 않는다.
6. **`Clear()`는 판 리셋용**이고 App이 `RunSession.Reset` 직후 부른다(5.07). 스텝 중 호출은 설계에 없다.
7. **`SpatialHash`는 범용 중심점 해시**(payload `int`)이고 젬·상자 픽업 해시에도 쓰인다(5.07, D15). `MonsterSpatialIndex`가 내부에서 `SpatialHash`를 쓰든 자체 사전을 쓰든 자유지만 `CellOf`/`KeyOf`는 `SpatialHash`의 정적 함수 한 곳(R14 소유 함수)이다.
8. **`SpatialHashSettings.CellSize` 기본값은 `StrategySelection.DefaultSpatialHashCellSize`를 참조**하고 2.0을 다시 쓰지 않는다(TAC-F16-4, 13회차). 개발자 모드가 다음 판부터 바꿀 수 있어야 하므로 설정 객체는 값을 바꿀 수 있는 형태여야 한다(쓰기 시점 제약은 App 몫).
9. **`TryFindNearest`는 몬스터가 없으면 false, 절대 던지지 않는다**(5.07). 스냅샷을 아직 받지 않은 상태(`OnSnapshotBuilt` 전)에서도 false.
10. **`OnSnapshotBuilt(snapshot)`는 스텝마다 BuildIndex 끝에 `SimulationDriverMid`가 직접 부른다**(5.07 (4회차)). 공급자는 그 참조를 보관해 `TryFindNearest`에서 쓴다. 스냅샷은 그 스텝 동안 불변이다.
11. **`CellCounts`(`IReadOnlyDictionary<long,int>`)는 개발자 시각화용**이고 결정론에 영향을 주지 않는다. 할당을 피하려고 캐시해도 된다.
12. **측정 마커**: 질의는 `ProfilerMarkers.Targeting`, 재구축은 `ProfilerMarkers.SpatialIndex`로 감싼다(R16). 새 마커를 만들지 않는다.
13. **Strategies는 Core만 참조**(5c.1). `Physics2D`·Simulation·Data 타입을 A·B·해시에 쓰면 위반이다.
14. **`EntityHandle` 개명**: 문서(13회차)와 코드 모두 `EntityHandle`이다. `UnityEngine.EntityId`라는 다른 타입이 있으니 혼동하지 않는다.
15. **C 공급자는 이번에 없다.** `TargetProviderKind.Physics2DQuery`는 Core 열거형에 그대로 있고, 당신은 그 값에 해당하는 클래스를 만들지 않는다. 테스트도 A=B만.

## 6. 금지

- 시크릿 파일 열람 금지. git 변경 명령·패키지 설치·`rm -rf` 금지.
- **단언을 느슨하게 고쳐 초록 금지**(특히 동등성 테스트의 허용 오차 도입). 공개 시그니처·이름 변경 금지(반박으로).
- 주석 영어 산문, 공개 멤버 한 줄 요약(§3). 네이밍 §2.2. 파일명 = 주 클래스명. 파일당 공개 타입 1개.
- 보고에 시크릿·긴 로그 원문 금지.

## 7. 보고 형식 (압축)

1. 만든 파일 목록(타입, 구현 TAC·절·R). 5c.2 대조(빠진·추가; `Physics2DTargetProvider`는 "웨이브 3 이월"로 표기).
2. 테스트 결과 3회 원문(total/passed/failed/inconclusive/skipped, XML, 종료 코드) + PM 재현 명령(`<cwd>` 채워서). Spatial 테스트만의 건수도 따로.
3. 환경 수치: 첫 배치 시간, Library 생성. 동등성 테스트의 배치 수·질의 수·소요 시간.
4. 계약 대조표: `ITargetProvider` 3멤버 × 2구현, `MonsterSpatialIndex` 공개 멤버 전부, `SpatialHash` 공개 멤버 전부, `SpatialHashSettings`.
5. 반박. 6. 읽을 목록 밖 파일. 7. 확신하지 못하는 것(항상 마지막).

## 8. 읽을 문서 (이 밖의 기획 문서는 열지 말 것)

전원 공통: `spec/00-common.md`, `tech/README.md`(색인만), `tech/02-feature-tac.md` **F16 절(TAC-F16-1~6)과 3.0 절**, `tech/5a-shared-rules.md`(R6·R10·R14·R16·R25), `tech/5c-ownership-contracts.md`(5c.1·5c.2 IMP-SPATIAL 행·5c.3 #9·5c.5).
필수(5c.2b): `spec/F16-targeting-strategy.md`, `tech/5.07-targeting.md`.
계약만: `tech/5.03-step-clock.md`(`IStepPhaseHandler`·`SimulationPhase`·`StepContext` — 코드 `Scripts/Core/`가 더 빠르다), `tech/5.09-collision.md`(`SelfCollisionStrategy`가 `MonsterSpatialIndex.QueryCircle`을 어떻게 쓰는지만, 85행 근처).
코드: `Survivor/Assets/Scripts/Core/`의 `ITargetProvider.cs`, `MonsterSnapshot.cs`, `MonsterSnapshotBuffer.cs`, `TargetHit.cs`, `TargetOrdering.cs`, `MonsterSizeRules.cs`, `IStepPhaseHandler.cs`, `SimulationPhase.cs`, `StepContext.cs`, `StrategySelection.cs`, `ProfilerMarkers.cs`, `DeterministicRng.cs`(테스트용). `CODING_GUIDE.md` §1~§4·§7.
주지 않는 것: `RUN_LOG.md`, `DECISIONS.md`, 다른 `spec/F*.md`, `tech/01`·`06`.

## 작업 순서 제안

1. 문서·Core 코드를 읽고 체크리스트를 만든다.
2. EditMode asmdef에 `Survivor.Strategies` 참조를 추가하고 `Spatial/SpatialHashSettings.cs`만 둔 상태로 배치 1회 — 파이프라인 확인(Library 생성).
3. `SpatialHash`(R14) → `MonsterSpatialIndex` → A → B 순서로 구현.
4. 테스트: R14 경계값(음수 좌표·셀 경계·`KeyOf` 비트), `QueryCircle` 포함성(큰 몬스터 항상 포함), 고리 종료 조건(멀리 하나), `Required=false`면 재구축 안 함, `Clear`, A=B 동등성 3종 배치, 몬스터 0일 때 false, 스냅샷 전 false.
5. 빨강 → 수정 → 초록 → 연속 3회 → 보고.

=== 프롬프트 끝 ===
