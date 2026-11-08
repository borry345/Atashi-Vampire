# 웨이브 3d 디스패치 브리핑 — IMP-SPATIAL C (2026-11-09, PM 작성, **초안 — 사용자 승인 뒤 디스패치**)

> `general-purpose`, `model: opus`, `isolation: worktree`. "=== 프롬프트 시작 ===" 이후를 그대로 보낸다(또는 worktree 안의 이 파일을 읽게 한다). 웨이브 2a에서 이월된 타깃 공급자 C(`Physics2DTargetProvider`)와 `Tests/PlayMode/Targeting/*`만. 선행 조건(IMP-COLLISION의 `PhysicsBodySynchronizer` 확정, main `51b9e5f`)은 충족됐다. TECH 17회차 개정과 독립이다(5.07은 17회차 대상이 아님).

=== 프롬프트 시작 ===

# 역할: IMP-SPATIAL C — 뱀서라이크 1차 MVP의 `Physics2D` 타깃 공급자 구현자

당신은 Unity 6 / C# 프로젝트의 세 번째 타깃 공급자 **C `Physics2DTargetProvider`**(`Survivor.Strategies`, `Targeting/`)와 그 **PlayMode 테스트**(`Tests/PlayMode/Targeting/`)를 구현한다. A(`BruteForceTargetProvider`)·B(`SpatialHashTargetProvider`)는 웨이브 2a에서 끝나 main에 있다. 타깃 공급자는 무기 전용 최근접 몬스터 질의이고, 측정 실험(F16)에서 서로 교체되며 **세 구현의 반환이 비트 단위로 같아야** 한다(TAC-F16-1 PM: A=B=C).

## 1. 이미 확인된 사실 (다시 조사하지 말 것)

- **당신은 자기 전용 git worktree에서 작업한다.** cwd가 worktree 루트, Unity 프로젝트는 `<cwd>\Survivor`. 다른 경로(원본 저장소 `C:\Users\SSAFY\Desktop\borry\indiv` 등)는 읽지도 쓰지도 않는다. 동시에 도는 다른 구현자는 없다.
- Unity **6000.3.28f1**, 에디터 `C:\Program Files\Unity\Hub\Editor\6000.3.28f1\Editor\Unity.exe`, GUI는 닫혀 있다. worktree의 `Survivor/Library`가 없어 **첫 배치 실행은 3~5분**, 이후 EditMode 15초 안팎, PlayMode 1~2분.
- 기준선(main, 2026-11-09): **EditMode 615 / PlayMode 185**, 전부 통과. 당신은 EditMode 테스트를 추가하지 않으므로 EditMode는 확인용 1회(615 유지), PlayMode는 185 + 당신 테스트 수.
- 이미 main에 있고 당신이 **읽기만** 하는 것:
  - Core: `ITargetProvider`(`Kind`, `OnSnapshotBuilt(MonsterSnapshotBuffer)`, `TryFindNearest(Vector2, out TargetHit)`), `TargetProviderKind.Physics2DQuery`, `MonsterSnapshotBuffer`(`Count`, 인덱서, `MaxHashedRadius`, `Bounds`(AABB `Rect`), `TryGetIndex(EntityHandle, out int)`), `MonsterSnapshot`(`Id`·`Position`·`Radius`·`IsLarge`), `TargetHit`, `TargetOrdering.IsBetter`(R6), `CollisionBody`(`Id`·`Layer`·`Position`·`Radius`·`Host`…), `CollisionLayer.Monster`, `CollisionLayerMapping.MaskOf(CollisionLayer)`·`UnityLayerOf`·`FirstUnityLayer = 8`, `PhysicsBodyRequirements(monsterColliders, allColliders, syncMonstersAtBuildIndex, simulateAtCollision)`, `ColliderActivationPolicy.Resolve(in StrategySelection)`, `StrategySelection`, `ProfilerMarkers.Targeting`.
  - Strategies(COLLISION, 웨이브 3 확정): `PhysicsBodySynchronizer(PhysicsBodyRequirements initial)` — `Register`/`Unregister(CollisionBody)`, `ApplyRequirements(next)`, `OnPhase(BuildIndex)`(`SyncMonstersAtBuildIndex`일 때 `SyncMonstersForQuery()`), `SyncMonstersForQuery()`(켜진 몬스터 바디의 `rb.position = body.Position`, ID 오름차순), **`TryGetBody(Collider2D, out CollisionBody)`**(등록된 콜라이더 → 바디, 아니면 false), `Requirements`·`Count`·`DetectionMode`. `PhysicsSettingsApplier.Apply()`(simulationMode Script, `callbacksOnDisable` false, `queriesHitTriggers` true, 레이어 행렬 9채널, `DefaultContactOffset`). 바디의 콜라이더는 `CircleCollider2D.isTrigger = true`, `Rigidbody2D` Kinematic.
  - Strategies(SPATIAL A·B): `BruteForceTargetProvider`, `SpatialHashTargetProvider(MonsterSpatialIndex)`, `MonsterSpatialIndex(snapshot, cellSize)`(`Required`, `OnPhase(BuildIndex)`로 재구축). **A·B의 `TryFindNearest`가 제곱거리를 계산하는 식과 `IsBetter` 호출 순서를 그대로 복제한다**(비트 동일성의 전제, 기준선 2).
  - 테스트 어셈블리: `Tests/PlayMode/Survivor.Tests.PlayMode.asmdef`는 이미 `Survivor.Core`·`Survivor.Strategies`·`Survivor.Simulation`을 참조한다 — **asmdef 수정 불필요**. 같은 어셈블리의 `Tests/PlayMode/Collision/CollisionPmUtil.cs`에 internal 도우미가 있다(`Physics2DGlobalsScope`: 물리 전역 설정 저장·복원, `CollisionPmUtil.Body(index, layer, x, y, radius, withHost, generation)`: 호스트 GameObject 달린 `CollisionBody` 생성, `CollisionPmUtil.DestroyAll()`, `CollisionPmUtil.PhysicsTargetRequirements()`: C용 요구사항, `CollisionPmWorld`). **읽고 호출해도 되지만 수정하지 않는다**(COLLISION 소유). 자기 테스트에 필요한 다른 도우미는 `Tests/PlayMode/Targeting/` 안에 만든다.
- 실측 사실(이미 확인됨 — 재검증 불필요): U32 Script 모드에서 `rb.position` 대입은 즉시 `OverlapCircle`에 반영된다(`Physics2D.SyncTransforms()` 불필요). U11 콜라이더 활성화 직후 `OverlapCircle` 반영. U12 이름 없는 레이어 8~17을 인덱스로 쓰는 레이어 행렬 유효. U27 `-nographics` batchmode에서 물리 PlayMode 테스트 통과. U28 `Physics2D.autoSyncTransforms`는 deprecated(CS0618) — COLLISION이 pragma로 감쌌고 당신은 그 API를 부르지 않는다.
- 테스트 실행 명령(PM이 그대로 다시 돌린다):
  ```
  "C:\Program Files\Unity\Hub\Editor\6000.3.28f1\Editor\Unity.exe" -batchmode -nographics -projectPath "<cwd>\Survivor" -runTests -testPlatform PlayMode -testResults "C:\Users\SSAFY\.claude\jobs\f32c1ccc\tmp\spc-pm-<n>.xml" -logFile "C:\Users\SSAFY\.claude\jobs\f32c1ccc\tmp\spc-pm-<n>.log"
  ```
  EditMode는 `-testPlatform EditMode`, 결과 `spc-em-<n>.xml`. Bash timeout 600000ms. 컴파일 오류면 테스트 0건 + 로그 `error CS`. 종료 코드 0/2/3. XML `<test-run>`의 `total/passed/failed/inconclusive/skipped`. 한 프로젝트에 Unity 인스턴스는 하나만 — EditMode·PlayMode를 동시에 띄우지 않는다.
- 임시 파일은 `C:\Users\SSAFY\.claude\jobs\f32c1ccc\tmp\`에 `spc-` 접두사로. worktree 안에 임시 파일 금지. 파일은 LF·UTF-8(BOM 없음). Strategies·테스트 코드에 한국어는 없어야 정상이다. 개행 확인은 `.claude/skills/eol-audit/SKILL.md`의 방법만 쓴다(`grep -c $'\r'`는 이 환경에서 틀린 답을 낸다).

## 2. 배타적 파일 소유권

### 소유 경로 (이 밖에는 만들거나 고치지 않는다)

1. `Survivor/Assets/Scripts/Strategies/Targeting/Physics2DTargetProvider.cs` — 신규 1개.
2. `Survivor/Assets/Tests/PlayMode/Targeting/**` — 신규 폴더.
3. Unity가 자동 생성하는 `.meta`(손으로 만들지 않는다).

### 건드리지 않는 것

- `Scripts/Core/**`, `Scripts/Strategies/Spatial/**`, `Targeting/BruteForceTargetProvider.cs`·`SpatialHashTargetProvider.cs`(A·B — 측정 기준선이라 **수정 금지**, 공용 도우미 추출도 하지 않는다), `Scripts/Strategies/Collision/**`, `Scripts/Strategies/Pool/**`·`Tick/**`, `Scripts/Simulation/**`, `Tests/EditMode/**`, `Tests/PlayMode/Collision/**`·`Loop/**`·`Pool/**`, asmdef 전부, `Scenes/`, `Settings/`, `ProjectSettings/`, `Packages/`.
- 소유 밖 결함을 찾으면 **고치지 말고 보고**한다(예: `TryGetBody`가 기대와 다르면 반박에).
- `indiv` 루트 문서는 읽기만. **`RUN_LOG.md`는 열지도 쓰지도 않는다.**

## 3. 검증 재현성

- `Library/`·`Temp/`·`Logs/` 삭제 금지. 패키지 수정 = BLOCKED 보고. `git commit`/`push`/`reset`/`git rm`, `rm -rf` 금지.
- 같은 도구 호출이 같은 이유로 2회 연속 실패하면 세 번째를 시도하지 말고 보고.
- **보고 수치는 PM이 같은 명령으로 재현 가능해야 한다.** 마지막 3회 XML 경로를 적는다. 래퍼 스크립트를 쓰면 `C:\Users\SSAFY\.claude\jobs\f32c1ccc\tmp\spc-run.sh <EditMode|PlayMode> <tag>`로 두고 보고에 적는다.

## 4. 결정성

- 테스트에 `UnityEngine.Random`·시드 없는 `System.Random`·`DateTime.Now`·`Time.*` 금지. 무작위 배치는 Core `DeterministicRng`에 고정 시드를 준다.
- **A=B=C 동등성 테스트**(TAC-F16-1 PM): 무작위 배치(물리 바디를 매 배치 등록해야 하므로 EditMode의 10,000건 대신 **배치 200건 × 질의점 5개 이상**, 몬스터 수 1·50·400 섞기) + 격자점 동거리 배치 + 큰 몬스터(`IsLarge`, 반지름 큰 것·보스 상당) 포함 배치에서 세 공급자의 `TryFindNearest` 결과(`bool`과 `TargetHit`의 `Id`·`Position`·`DistanceSquared`)가 **비트 단위로** 같아야 한다(`float` 비교는 비트 또는 `Equals` — 허용 오차 비교는 이 테스트에서는 틀린 단언이다). 질의점은 몬스터 바로 위·셀 경계·아주 멀리·음수 좌표를 포함한다.
- **`OverlapCircle`의 결과 순서는 보장되지 않는다.** 그래서 최종 선택은 후보 전부를 `IsBetter`로 훑어 정한다 — 순서에 의존하는 코드(첫 결과 채택 등)는 결정론 위반이다.
- **합격 기준: PlayMode 전체 스위트 연속 3회 전회 동일(총 N, 통과 N, 실패 0) + EditMode 1회 615 유지.** 네 줄 그대로 보고.

## 5. 기준선 — 정상 동작인데 결함으로 오판하기 쉬운 것

1. **알고리즘은 TAC-F16-3 그대로**: 질의는 `Physics2D.OverlapCircle(point, R, ContactFilter2D{layerMask = monsterLayerMask, useTriggers = true}, results)`를 **반경 4부터 두 배씩** 넓히되 상한은 "질의점에서 `snapshot.Bounds`의 가장 먼 꼭짓점까지 거리"다(상한에서도 0건이면 false). 후보가 생기면 그 후보들로 임시 최선 `bestD2`를 구하고 **`R = √bestD2 + 1e-3`으로 한 번 더 질의**한 뒤, 최종 선택은 **스냅샷 위치**(콜라이더 위치가 아니라)와 R6으로 한다. 콜라이더 겹침 판정은 "중심 거리 ≤ R인 몬스터"를 모두 포함하는 상위 집합이므로 재질의로 놓치는 몬스터가 없다. 1e-3은 부동소수 여유이지 허용 오차 비교가 아니다.
2. **비트 동일성의 전제**: 거리는 A·B와 **같은 식**(`BruteForceTargetProvider.cs`에서 읽어 그대로 — 예상 `(snapshotPos - point).sqrMagnitude`)으로 **스냅샷 위치**에서 계산하고, 선택은 `TargetOrdering.IsBetter(d2, id, bestD2, bestId)` 한 곳이다. 콜라이더 → `bodies.TryGetBody(collider, out body)` → `body.Id` → `snapshot.TryGetIndex(body.Id, out i)` → `snapshot[i].Position`. `body.Position`을 거리 계산에 쓰지 않는다(같은 값이어야 하지만 원본은 스냅샷이다). `TryGetIndex`가 false인 콜라이더(스냅샷에 없는 바디 — 플레이어·투사체 레이어는 마스크로 걸러지지만 안전하게)는 무시한다.
3. **같은 콜라이더가 두 번 나올 수 없지만**, 재질의 결과는 첫 질의와 겹친다 — 재질의 결과만으로 최종 선택을 한다(첫 질의 결과를 합치지 않는다. 합쳐도 결과는 같아야 하지만 코드를 단순하게).
4. **요구사항은 조립 루트 몫**: C가 쓰이려면 `PhysicsBodyRequirements.MonsterColliders`·`SyncMonstersAtBuildIndex`가 참이어야 한다(Core `ColliderActivationPolicy.Resolve`가 `TargetProviderKind.Physics2DQuery` 선택에서 그렇게 준다). 테스트는 `CollisionPmUtil.PhysicsTargetRequirements()` 또는 `ColliderActivationPolicy.Resolve(selection)`으로 만든 요구사항을 `PhysicsBodySynchronizer`에 준다. 요구사항이 꺼져 몬스터 콜라이더가 비활성이면 C는 **false를 돌려주고 던지지 않는다**(PM [기본값], 로그 없음) — 테스트 1건.
5. **동기화 시점**: 질의 전에 `bodies.OnPhase(SimulationPhase.BuildIndex, in ctx)`(또는 `SyncMonstersForQuery()`)가 불려 `rb.position`이 스냅샷 위치와 같아야 한다. 실제 루프에서는 BuildIndex order 5(`PhysicsBodySynchronizer`)가 order 10(`MonsterSpatialIndex`) 앞이고, 드라이버가 BuildIndex 끝에 `OnSnapshotBuilt`를 직접 부른다(5.07). C는 `OnSnapshotBuilt(snapshot)`에서 참조만 보관하고 동기화를 호출하지 않는다 — 동기화기는 자기 단계에서 스스로 돈다. 테스트가 이 순서를 재현한다.
6. **`OnSnapshotBuilt(null)`은 참조를 잊고 예외 없음**(A·B와 같다). 스냅샷 전·몬스터 0기·물리 바디 0개 → `TryFindNearest` false, 절대 던지지 않는다. 생성자는 `bodies == null` → `ArgumentNullException`, `monsterLayerMask == 0` → `ArgumentOutOfRangeException`(PM [기본값]).
7. **`Kind`는 `TargetProviderKind.Physics2DQuery`**. 질의 전체를 `ProfilerMarkers.Targeting`으로 감싼다(R16). 새 마커를 만들지 않는다.
8. **`OverlapCircle` 결과 목록은 재사용**(`List<Collider2D>` 필드)해 스텝당 할당을 피한다. 결과 순서 비결정성은 기준선 4절 그대로.
9. **물리 전역 설정**: 테스트는 `PhysicsSettingsApplier.Apply()`를 쓰고 끝나면 `Physics2DGlobalsScope`(internal, 같은 어셈블리)로 복원한다. `queriesHitTriggers`가 false면 트리거 콜라이더가 질의에 안 잡힌다 — 그래서 `ContactFilter2D.useTriggers = true`도 함께 둔다.
10. **Strategies는 Core만 참조**(5c.1). Simulation·Data 타입을 쓰면 위반이다. `UnityEngine.Physics2D`는 허용(5.9 "읽기라 예외").
11. **레이어 마스크의 출처**: 조립 루트가 `CollisionLayerMapping.MaskOf(CollisionLayer.Monster)`를 넘긴다. 테스트도 같은 식으로 만든다. 몬스터 호스트의 `layer`는 `PhysicsBodySynchronizer.Register`가 설정한다(당신이 설정하지 않는다).
12. **큰 몬스터·보스**도 물리 바디가 있으므로 질의에 잡힌다. A·B는 큰 몬스터를 따로 전수 검사하지만 C는 구분이 필요 없다 — 결과만 같으면 된다.

## 6. 금지

- 시크릿 파일 열람 금지. git 변경 명령·패키지 설치·`rm -rf` 금지.
- **단언을 느슨하게 고쳐 초록 금지**(특히 동등성 테스트의 허용 오차 도입, 배치 수 축소로 회피). 공개 시그니처·이름 변경 금지(반박으로).
- 주석 영어 산문, 공개 멤버 한 줄 요약(`CODING_GUIDE.md` §3). 네이밍 §2.2. 파일명 = 주 클래스명. 파일당 공개 타입 1개.
- 보고에 시크릿·긴 로그 원문 금지.

## 7. 보고 형식 (압축)

1. 만든 파일 목록(타입, 구현 TAC·절·R). 5c.2 IMP-SPATIAL 행 대조.
2. 테스트 결과: PlayMode 3회 + EditMode 1회 원문(total/passed/failed/inconclusive/skipped, XML, 종료 코드) + PM 재현 명령(`<cwd>` 채워서). Targeting 테스트만의 건수도 따로.
3. 환경 수치: 첫 배치 시간, 동등성 테스트의 배치 수·질의 수·소요 시간, 질의당 평균 `OverlapCircle` 호출 횟수(두 배 확장 단계 수).
4. 계약 대조표: `ITargetProvider` 3멤버, 생성자, 추가 공개 멤버(★ — 있으면 이유).
5. 반박(PM 전제가 틀렸다고 보면 **먼저 실측**하고 수치와 함께). 6. 읽을 목록 밖 파일. 7. **F16 기록 초안 1단락**(5c.2a: C의 질의 방법·왜 상위 집합 재질의가 필요한지·A·B와의 동일성 근거 — devlog 소재). 8. 확신하지 못하는 것(항상 마지막).

## 8. 읽을 문서 (이 밖의 기획 문서는 열지 말 것. 열어야 했다면 보고에 적는다 — 목록의 결함이다)

전원 공통: `spec/00-common.md`, `tech/README.md`(색인만), `tech/02-feature-tac.md` **F16 절(TAC-F16-1·F16-3·F16-5·F16-6)과 3.0 절**, `tech/5a-shared-rules.md`(R6·R10·R16·R19·R25·R30), `tech/5c-ownership-contracts.md`(5c.1·5c.2 IMP-SPATIAL 행·5c.3 #9·5c.5).
필수(5c.2b): `spec/F16-targeting-strategy.md`, `tech/5.07-targeting.md`(전부 — 특히 TAC-F16-3 설명과 5.7.1), `tech/5.09-collision.md`(5.9.1 `PhysicsBodySynchronizer`·`PhysicsSettingsApplier` 블록과 "조합별 동작" 표).
계약만: `tech/5.03-step-clock.md`(`IStepPhaseHandler`·`SimulationPhase`·`StepContext` — 코드 `Scripts/Core/`가 더 빠르다).
코드: `Scripts/Core/`의 `ITargetProvider.cs`, `MonsterSnapshot.cs`, `MonsterSnapshotBuffer.cs`, `TargetHit.cs`, `TargetOrdering.cs`, `CollisionBody.cs`, `CollisionEnums.cs`, `CollisionLayerMapping.cs`, `ColliderActivationPolicy.cs`, `StrategySelection.cs`, `ProfilerMarkers.cs`, `DeterministicRng.cs`(테스트용); `Scripts/Strategies/Targeting/BruteForceTargetProvider.cs`·`SpatialHashTargetProvider.cs`(거리 식·`IsBetter` 호출 복제용), `Scripts/Strategies/Spatial/MonsterSpatialIndex.cs`(B를 테스트에서 조립하는 데 필요한 생성자·`Required`만); `Scripts/Strategies/Collision/PhysicsBodySynchronizer.cs`·`PhysicsSettingsApplier.cs`(공개 API); `Tests/PlayMode/Collision/CollisionPmUtil.cs`·`CollisionPmFixture.cs`(도우미 사용법); `Tests/EditMode/Spatial/TargetProviderEquivalenceTests.cs`(A=B 테스트의 배치 생성 방식 — 같은 분포를 쓴다). `CODING_GUIDE.md` §1~§4·§7.
주지 않는 것: `RUN_LOG.md`, `DECISIONS.md`, 다른 `spec/F*.md`, `tech/01`·`06`, 다른 `5.xx`.

## 작업 순서 제안

1. 문서·Core·A·B·동기화기 코드를 읽고 체크리스트를 만든다.
2. `Targeting/Physics2DTargetProvider.cs` 뼈대(`Kind`·생성자·`OnSnapshotBuilt`)와 빈 테스트 폴더로 PlayMode 배치 1회 — 파이프라인 확인(Library 생성, 185 유지).
3. 질의 구현(기준선 1·2·3) → 단위 테스트(스냅샷 전 false, 0기 false, 콜라이더 꺼짐 false, 생성자 예외, 멀리 하나(상한 도달), 동거리 타이브레이크, 큰 몬스터) → A=B=C 동등성.
4. 빨강 → 수정 → 초록 → PlayMode 연속 3회 + EditMode 1회 → 보고.

=== 프롬프트 끝 ===
