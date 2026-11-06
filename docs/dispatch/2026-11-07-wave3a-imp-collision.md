# 웨이브 3a 디스패치 브리핑 — IMP-COLLISION (2026-11-07, PM 작성)

> `general-purpose`(opus), `isolation: worktree`. 충돌 전략 2종(자체 제곱거리·엔진 `Collider2D`)과 물리 바디 동기화·물리 전역 설정·충돌 템플릿 데코레이터를 `Survivor.Strategies`에 구현한다. 웨이브 3의 다른 구현자(PLAYER·MONSTER)와는 독립이다 — 이 브리핑은 Core·Spatial(2a)·LOOP(2b)만 전제한다. SPATIAL C(`Physics2DTargetProvider`)는 이 작업이 머지된 뒤 별도 브리핑으로 디스패치한다(`PhysicsBodySynchronizer.TryGetBody` 의존).
> 디스패치 시점: TECH 15·16회차 + IMP-CORE 경계 인터페이스 머지 뒤(worktree가 origin/main에서 분기하므로 문서·Core가 main에 있어야 한다). 예상 30~45만 토큰, 45~75분.

=== 프롬프트 시작 ===

# 역할: IMP-COLLISION — 충돌 전략 2종 + 물리 바디 동기화

당신은 Unity 6(6000.3.28f1) / C# 뱀서라이크 1차 MVP의 충돌 판정 구현자다. SPEC F18이 요구하는 두 전략 — **B 자체 제곱거리 판정**(`SelfCollisionStrategy`)과 **A 엔진 `Collider2D` 트리거**(`EngineCollisionStrategy`) — 와, 엔진 바디의 유일한 소유자 `PhysicsBodySynchronizer`, 물리 전역 설정 `PhysicsSettingsApplier`, 템플릿 데코레이터 `CollisionTemplateDecorator`, 보조 `CollisionBodyLink`·`PairSet`을 만들고 EditMode·PlayMode 테스트로 두 전략이 같은 Enter/Exit 집합을 내는 것을 보인다.

## 1. 이미 확인된 사실 (다시 조사하지 않는다)

- **작업 디렉터리**: 당신 전용 git worktree다(`pwd`로 확인, 아래 `<cwd>`). origin/main 최신에서 분기했다. **cwd 밖(원본 저장소 `C:\Users\SSAFY\Desktop\borry\indiv`, 다른 worktree)은 읽지도 쓰지도 않는다.** Unity 프로젝트는 `<cwd>\Survivor`.
- **main에 있는 것**: Core 54+파일(웨이브 1, 13회차 확정 — `IPoolStrategy.Clear()`는 2026-11-06, `IDamageable`·`IContactDamageSource`는 2026-11-07 추가; 당신은 쓰지 않는다), Data 44, Strategies `Pool/`(6)·`Spatial/`(3)·`Targeting/`(2, A·B만)·`Tick/`(5), Simulation `Loop/`(22)·`Entities/`(7), 테스트 EditMode 479건·PlayMode 144건(전부 통과; 473은 각 3회, +6은 2026-11-07 경계 인터페이스 테스트). `Scripts/Strategies/Collision/`과 `Tests/*/Collision/`은 **비어 있다** — 당신이 만든다.
- **어셈블리**: `Survivor.Strategies.asmdef`는 Core만 참조한다(IMP-LOOP 소유, **수정 금지**). `Collision/`은 같은 어셈블리의 `Spatial/`(`MonsterSpatialIndex`·`SpatialHash`, internal 멤버 포함)을 쓸 수 있다. Simulation·Data는 참조할 수 없다(5c.1). 테스트 asmdef는 EditMode가 Core·Data·Strategies·Simulation, PlayMode가 Core·Strategies·Simulation을 이미 참조하므로 **asmdef 변경이 필요 없다**. 테스트에서 Simulation(`EntityLifecycle`·`MonsterRegistry`·`SimEntity`)을 실물로 써도 된다.
- **Unity 배치 실행** (첫 실행은 Library 생성으로 2~5분, 이후 15~30초; Bash timeout 600000ms):
  `"C:\Program Files\Unity\Hub\Editor\6000.3.28f1\Editor\Unity.exe" -batchmode -nographics -projectPath "<cwd>\Survivor" -runTests -testPlatform EditMode -testResults "C:\Users\SSAFY\.claude\jobs\f32c1ccc\tmp\coll-em-<n>.xml" -logFile "C:\Users\SSAFY\.claude\jobs\f32c1ccc\tmp\coll-em-<n>.log"` — PlayMode는 `-testPlatform PlayMode`, 파일명 `coll-pm-<n>`. 종료 코드 0 = 전부 통과. 결과는 XML의 `<test-run total= passed= failed= inconclusive= skipped=>`. 컴파일 오류는 로그에서 `grep -c "error CS"`. 이 환경의 가드가 Bash에서 Unity 직접 실행을 막으면 **같은 명령을 담은 래퍼 .sh**(`C:\Users\SSAFY\.claude\jobs\f32c1ccc\tmp\coll-run.sh`)로 돌려도 된다 — 2a·2b 구현자 전원이 그렇게 했고 PM이 직접 재현해 같은 결과를 얻었다. Unity는 한 프로젝트에 한 인스턴스만 돈다(PM은 당신 worktree에서 Unity를 돌리지 않는다).
- **읽기 전용 git 명령**(`git status`, `git diff`, `git show`, `git log`, `git config --get`)은 허용한다. 상태를 바꾸는 git 명령은 전부 금지(6절).
- **임시 파일**은 `C:\Users\SSAFY\.claude\jobs\f32c1ccc\tmp\`에 `coll-` 접두사로. worktree 안에 임시 파일 금지. 파일은 LF·UTF-8(BOM 없음). **개행 검사는 바이트로 한다**(`tr -cd '\r' | wc -c` 또는 python) — 이 셸의 `grep -c $'\r'`은 틀린 답을 낸다. Strategies 코드에 한국어는 없어야 정상이다(사용자 노출 문자열 없음). 주석 규칙은 `CODING_GUIDE.md` §3.
- **Core 시그니처(코드가 원본, 전부 main에 있음)**: `CollisionLayer`(10, PlayerBody=0 … Chest=9), `CollisionChannel`(9), `CollisionEventKind`, `CollisionEvent(step, kind, channel, a, b)`(`CompareTo` = Step, Channel, A, B, Kind; `ToString()` CSV), `CollisionChannelTable.ChannelCount`·`TryGetChannel(x, y, out ch, out xIsA)`·`GetLayers(ch, out a, out b)`, `CollisionLayerMapping.FirstUnityLayer = 8`·`UnityLayerOf(layer)`·`MaskOf(layer)`, `CircleMath.Overlaps(a, ra, b, rb)`(R5 `≤`), `CollisionBody`(공개 필드 `Id`·`Layer`·`Position`·`Radius`·`IsLarge`·`Host`·`StrategySlot = -1`·`PhysicsSlot = -1`·`DrivesTransform`), `ICollisionStrategy`(`Kind`·`Register`·`Unregister`·`NotifyRadiusChanged`·`CollectEvents(long step)`·`Clear`), `IPhysicsBodyRegistry`(`Register`·`Unregister`), `PhysicsBodyRequirements`(`MonsterColliders`·`AllColliders`·`SyncMonstersAtBuildIndex`·`SimulateAtCollision`·`CollidersEnabledFor(layer)`), `ColliderActivationPolicy.Resolve(in StrategySelection)`, `StrategySelection`(`Collision`·`EngineDetectionMode`·`SpatialHashCellSize` 등, `Baseline`·`ShippingDefault`), `MonsterSnapshotBuffer`(`Count`, 인덱서 `ref readonly MonsterSnapshot this[int]`, `MaxHashedRadius`, `Bounds`, `TryGetIndex(id, out index)`, `Clear`, `Add`), `MonsterSnapshot`(`Id`·`Position`·`Radius`·`IsLarge`), `ProfilerMarkers.Collision`·`Targeting`, `StepMath.StepSeconds`, `IStepPhaseHandler.OnPhase(phase, in context)`, `SimulationPhase`.
- **Spatial(2a) API**: `MonsterSpatialIndex(float cellSize, MonsterSnapshotBuffer snapshot)`, `Required { get; set; }`, `OnPhase`(BuildIndex에서 `Required`일 때만 재구축), `QueryCircle(center, radius, List<int> snapshotIndicesOut)` — **출력 목록을 비운 뒤** 셀 범위 `radius + MaxHashedRadius`의 **상위 집합** + 모든 `IsLarge` 항목을 **스냅샷 인덱스 오름차순**으로 채운다(정확 판정 없음 → 당신이 R5로 다시 판정한다). internal `LargeIndices`·`Snapshot`·`HasHashedEntries`(같은 어셈블리라 보인다). `SpatialHash(float cellSize)`·`Insert(int payload, Vector2)`·`QueryCells(center, radius, List<int>)`·`Clear`·`Count`·`CellSize`(픽업 전용 해시에 재사용). 스냅샷 인덱스는 `EntityHandle` 인덱스가 아니다 — `snapshot[i].Id`로 ID를 읽는다.
- **LOOP(2b)가 당신 코드를 부르는 방식**: `EntityLifecycle.Spawn` 순서 `pool.Acquire → ids.Allocate → Bind → initialize → ticks.Register → physicsBodies.Register(Body) → collisions.Register(Body) → Spawned`, `Despawn`은 정확히 역순(`Despawning → collisions.Unregister → physicsBodies.Unregister → ticks.Unregister → Despawned → OnReleased → ids.Free → pool.Release`). `SimEntity.Bind`가 `Body`를 처음 한 번 만들고 `Body.Host = gameObject`, `Body.Layer`를 카테고리와 같은 이름의 `CollisionLayer`로 둔다(8 카테고리 ↔ 8 레이어, `PlayerBody`·`PlayerMagnet`은 카테고리가 아니다 — 플레이어 몸·자석 바디는 IMP-PLAYER가 `CollisionBody`를 직접 만들어 등록한다, ID 1:1·2:1). `CollisionEventRouter`(LOOP)는 Collision order 0에서 `strategy.CollectEvents(step)`를 부르고 Resolve order 0에서 정렬 순서대로 채널 핸들러에 넘긴다. `MonsterRegistry`(LOOP, BuildIndex order 0)가 `MonsterSnapshotBuffer`를 채우고, `MonsterSpatialIndex`(BuildIndex 10)가 재구축한다. `PhysicsBodySynchronizer`는 BuildIndex order 5 핸들러로 조립 루트가 등록한다(5c.3 #9).
- **Unity 레이어**: `ProjectSettings/TagManager.asset`(사용자 소유, **수정 금지**)의 레이어 8~17은 **이름이 없다**. 코드는 `gameObject.layer = CollisionLayerMapping.UnityLayerOf(layer)`(정수 인덱스)로 두고, 레이어 매트릭스는 `Physics2D.IgnoreLayerCollision(int, int, bool)`(정수)로 설정한다. 이름 없는 인덱스에 설정이 먹는지는 **U12**이며 당신의 PlayMode 테스트가 확인한다(`Physics2D.GetIgnoreLayerCollision`으로 읽어 단언).
- **웨이브 0 실측(사실로 쓴다)**: U5 Kinematic–Kinematic(`useFullKinematicContacts = true`) 트리거 Enter·Exit 수신, sleepMode 변경 불필요. U7 `simulationMode = Script`에서 `Physics2D.Simulate()` 호출 안에 콜백이 **동기**로 온다. U8 `callbacksOnDisable = false`면 콜라이더 `enabled = false`에 Exit 없음. U9 거리 = 반지름 합(정확히 접촉)에서 Box2D도 Enter → R5 `≤`와 같은 쪽. U30 `defaultContactOffset = 0.0001` 설정·읽기 일치. U32 Script 모드에서 `rb.position` 직접 대입은 `Simulate`·`SyncTransforms` 없이 즉시 `OverlapCircle`에 반영(`queriesHitTriggers = true`). U31 `FixedUpdate` 안에서 켠 컴포넌트는 같은 프레임 다음 스텝부터 호출.
- **당신이 확인할 미확정(PlayMode 테스트로, 보고 3절)**: **U4** 새 `Rigidbody2D`의 `collisionDetectionMode` 기본값이 `Discrete`인지. **U6** 키네마틱 트리거에 `Continuous`가 영향을 주는지(빠른 바디가 얇은 대상을 한 스텝에 통과하는 배치에서 Discrete/Continuous의 Enter 유무·시점 차이 — E2의 의미를 좌우한다). **U10** `MovePosition`이 스윕인지(위 배치에서 Discrete `MovePosition` vs `rb.position` 대입). **U11 나머지** 콜라이더 `enabled = true` 직후 `OverlapCircle`에 바로 잡히는지. **U12** 위. **U27** batchmode `-nographics`에서 물리 PlayMode 테스트가 도는지(당신 테스트가 돌면 확인). **U28** 6000.3에서 쓰는 `Physics2D` API가 전부 컴파일·동작하는지. 결과는 확인/틀림/부분으로 수치와 함께.

## 2. 당신이 배타적으로 소유하는 파일 (이 밖에는 수정 금지)

- `Survivor/Assets/Scripts/Strategies/Collision/**` — 5c.2 목록: `SelfCollisionStrategy.cs`, `EngineCollisionStrategy.cs`, `CollisionBodyLink.cs`, `PhysicsBodySynchronizer.cs`, `PhysicsSettingsApplier.cs`, `CollisionTemplateDecorator.cs`, `PairSet.cs`. 파일 추가는 허용(파일당 공개 타입 1개, internal 도우미 권장 — 예: `CollisionLog`), 보고에 적는다.
- `Survivor/Assets/Tests/EditMode/Collision/**`, `Survivor/Assets/Tests/PlayMode/Collision/**`.
- `.meta`는 Unity가 만든다(그대로 둔다).
- **수정 금지**: `Survivor.Strategies.asmdef`(LOOP 소유), 테스트 asmdef 2개(변경 불필요), `Scripts/Core/**`, `Scripts/Data/**`, `Scripts/Simulation/**`, `Scripts/Strategies/Pool|Spatial|Targeting|Tick/**`, `Tests/*/Loop|Pool|Spatial|Data|Core/**`, `ProjectSettings/**`, `Packages/**`. 테스트 더블이 필요하면 **당신 테스트 폴더 안에** 만든다. 같은 테스트 어셈블리의 기존 internal 도우미(`Tests/PlayMode/Loop/LoopPmRig.cs`·`LoopPmDoubles.cs`, `Tests/EditMode/Spatial/SpatialTestUtil.cs`)는 **읽고 재사용할 수 있으나 수정하지 않는다**.
- 남의 영역에서 결함을 찾으면 고치지 말고 보고에 적는다("소유 밖 결함").

## 3. 검증 재현성

- `Library/`·`Temp/`·`Logs/` 삭제 금지. 패키지 설치·`manifest.json`·`ProjectSettings` 수정 = BLOCKED로 보고(이유만).
- 당신이 보고하는 수치는 PM이 당신 worktree와 main 조립본에서 **그대로 다시 돌려 같은 값**이 나와야 한다.
- 합격 기준: **EditMode 전체 스위트 연속 3회 + PlayMode 전체 스위트 연속 3회, 각 실패·inconclusive 0**, 기존 479·144건이 그대로 통과. 결과를 요약하지 말고 6회 수치를 그대로 보고한다.

## 4. 결정성

- 테스트에 `UnityEngine.Random`·시드 없는 `System.Random`·`DateTime.Now`·`Time.*` 읽기 금지(예외: `Time.fixedDeltaTime` 확인). 난수는 `DeterministicRng` 고정 시드. 고정 시간 대기 금지(PlayMode 프레임 경계는 `yield return null`/`WaitForFixedUpdate`).
- 자체 전략의 순회는 전부 `EntityHandle` 오름차순(R10)이고 출력은 `CollisionEvent.CompareTo` 순으로 정렬돼 있어야 한다(키가 유일하므로 정렬 안정성은 무관).
- 엔진 전략도 같은 정렬 출력을 낸다. 콜백 도착 순서에 의존하는 코드가 없어야 한다.

## 5. 설계 계약과 PM 기본값

### 5-A. 문서가 정한 것 (그대로 구현)

- **5.9 코드 블록 시그니처** 그대로: `SelfCollisionStrategy(MonsterSpatialIndex index, MonsterSnapshotBuffer snapshot)`, `EngineCollisionStrategy(PhysicsBodySynchronizer bodies, CollisionDetectionMode2D mode)`, `CollisionBodyLink : MonoBehaviour`, `PhysicsBodySynchronizer(PhysicsBodyRequirements initial) : IPhysicsBodyRegistry, IStepPhaseHandler`(`Register`·`Unregister`·`ApplyRequirements(next)`·`OnPhase`(BuildIndex: `SyncMonstersAtBuildIndex`면 `SyncMonstersForQuery()`)·`SyncMonstersForQuery()`·`SyncAllForSimulation()`·`TryGetBody(Collider2D, out CollisionBody)`), `static PhysicsSettingsApplier.Apply()`. 네임스페이스는 5.9.1 코드 블록대로 `Survivor.Strategies`(Spatial·Targeting과 같음; Pool·Tick은 하위 네임스페이스를 썼지만 이 영역은 문서가 명시했다). 공개 이름·시그니처를 바꾸지 않는다 — 다르게 해야 한다면 반박으로 올리고 문서대로 구현한다. **추가** 공개 멤버는 허용(★로 보고).
- **TAC-F18-2** 자체판: 채널마다 이번 스텝 겹침 쌍 집합을 계산해 이전 스텝과 비교, 새 쌍 Enter·사라진 쌍 Exit, 지속 겹침 스텝에는 이벤트 없음. 판정은 `CircleMath.Overlaps`(`≤`).
- **TAC-F18-3 / D15** 후보 추림: `PlayerProjectile`·`PlayerOrb`·`PlayerZone` → Monster는 `MonsterSpatialIndex.QueryCircle`(+ 큰 몬스터는 결과에 이미 포함됨) 뒤 R5 재판정; `PlayerMagnet`·`PlayerBody` → Gem·Chest는 **자체 소유 `SpatialHash`**(픽업 전용); `EnemyProjectile`·`EnemyZone` → PlayerBody와 `PlayerBody` → Monster는 플레이어 1개 기준 선형 또는 해시 질의.
- **TAC-F18-4** 엔진판 `CollectEvents(step)`: ① `bodies.SyncAllForSimulation()`(등록 바디 전부 ID 순 `MovePosition(body.Position)`, 반지름 바뀐 바디는 `CircleCollider2D.radius` 갱신) ② `Physics2D.Simulate(StepMath.StepSeconds)` ③ 콜백 큐를 ID로 바꾸고 정렬해 반환. `Register` 때 `rb.position`·`transform.position`을 바로 대입(풀에서 나온 객체가 옛 위치에서 스윕되지 않게). `EngineCollisionStrategy`만 `Physics2D.Simulate`를 부른다.
- **TAC-F18-5** 등록 해제 시 두 전략 모두 Exit 없음. 자체판은 그 바디의 쌍을 조용히 지운다(이전 집합에서 제거 — 다음 스텝에 Exit이 나오지 않게). 엔진판은 `callbacksOnDisable = false` + "등록되지 않은 바디의 콜백은 버림" 필터.
- **TAC-F18-6** `Register` 직후 첫 `CollectEvents`에서 이미 겹쳐 있으면 Enter(두 전략 모두).
- **TAC-F18-8** 정렬·비교 키 = `CollisionEvent.CompareTo`. A는 항상 채널의 A측 레이어 바디(`TryGetChannel`의 `xIsA`).
- **5.9 엔진 바디 구성**: `Rigidbody2D.bodyType = Kinematic`, `useFullKinematicContacts = true`, `collisionDetectionMode = mode`, `CircleCollider2D.isTrigger = true`. **5.6 데코레이터**: `CollisionTemplateDecorator.Decorate(template, category)`는 비활성 `CircleCollider2D`(isTrigger) + `Rigidbody2D`(Kinematic, `simulated = false`) + `CollisionBodyLink`를 붙이고 `layer = R30`. 여러 번 불려도 컴포넌트 1개씩, null → `ArgumentNullException`(`PoolTemplateDecorator`와 같은 규약).
- **5.9.1 `PhysicsSettingsApplier.Apply()`**: `simulationMode = Script`, `callbacksOnDisable = false`, `queriesHitTriggers = true`, `autoSyncTransforms = false`, `defaultContactOffset = 0.0001f`, 레이어 매트릭스 = `CollisionChannelTable`의 9채널만 충돌(R30). 다른 코드는 `Physics2D` 전역 설정을 건드리지 않는다.
- **5.9 소유자 한 줄**: 바디 위치·콜라이더 켜짐 = `PhysicsBodySynchronizer`만, `Simulate` = `EngineCollisionStrategy`만. **`PhysicsBodySynchronizer`는 `rb.position`만 쓰고 `Physics2D.SyncTransforms`를 부르지 않는다**(U32). `SyncMonstersForQuery`는 몬스터 바디만 ID 순 `rb.position = body.Position`; `SyncAllForSimulation`은 켜진 바디 전부 ID 순 `MovePosition`.
- **R16 마커**: `CollectEvents` 전체(동기화·`Simulate` 포함)는 `ProfilerMarkers.Collision`, BuildIndex의 `SyncMonstersForQuery`는 `ProfilerMarkers.Targeting` 안에서 잰다(5.9 ④).
- **R19 요구사항**: 콜라이더 켜짐 = `requirements.CollidersEnabledFor(body.Layer)`. `ApplyRequirements(next)`는 등록된 바디의 콜라이더를 ID 순으로 맞춘다(판 시작·타깃 공급자 교체 때 App 콜백만 부른다).
- **5c.3 #7 오류 규약**: 계약 위반은 `InvalidOperationException`(같은 바디 중복 `Register`, 등록되지 않은 바디 `Unregister`·`NotifyRadiusChanged`, 같은 바디를 두 전략에 등록 등). 인자 null은 `ArgumentNullException`. 던지기 전에 `[HH:mm:ss.fff] [ERROR] [Collision] …` 한 줄(internal 도우미, 5c.3 #8 — Core에 공용 포매터 없음, 영역별 internal 유지가 PM 확정). 스텝마다 남기는 로그 금지.

### 5-B. PM 기본값 (문서에 없거나 모호한 곳 — 이렇게 하고, 더 나은 근거가 있으면 반박으로 올린다)

- **C1. 플레이어·자석 호스트의 물리 컴포넌트**: `PrefabCatalog` 템플릿 8종에는 데코레이터가 컴포넌트를 미리 붙이지만, 플레이어 몸(1:1)·자석(2:1) 바디의 `Host`는 IMP-PLAYER가 만든 GameObject라 데코레이터를 거치지 않는다. `PhysicsBodySynchronizer.Register(body)`는 `body.Host`에 `Rigidbody2D`·`CircleCollider2D`·`CollisionBodyLink`가 **없으면 데코레이터와 같은 구성으로 `AddComponent`**하고, 있으면 그대로 쓴다(멱등). `body.Host == null`이면 `InvalidOperationException`(엔진 바디는 호스트가 필요하다; 자체 전략은 `Host`를 쓰지 않는다). 레이어도 이때 `UnityLayerOf(body.Layer)`로 맞춘다.
- **C2. `Rigidbody2D.simulated`**: 콜라이더 `enabled`와 함께 토글한다(꺼진 바디는 `simulated = false`로 Box2D 비용 0). `DrivesTransform`은 **항상 false**로 둔다 — 이 설계에서는 위치의 원본이 `SimEntity.Position`이고 엔진은 `MovePosition`으로 따라가므로 엔진이 transform을 소유하는 모드가 없다(5c.3 #6의 "엔진 모드 바디는 쓰지 않는다"는 이 플래그가 참일 때의 규칙이고, 참으로 만드는 경로는 두지 않는다). 다르게 보면 반박.
- **C3. 픽업 전용 해시**: 셀 크기 = `index.CellSize`(같은 값, 별도 설정 없음). 매 `CollectEvents`에서 등록된 Gem·Chest 바디로 **다시 채운다**(`Clear` → `Insert`, 최대 616개라 싸고 상태가 없어 결정적). 증분 갱신은 하지 않는다. 비용은 보고에 적는다.
- **C4. 몬스터 쪽 좌표의 출처**: 자체 전략은 몬스터의 위치·반지름·`IsLarge`를 **스냅샷**(`snapshot[i]`)에서 읽는다(몬스터는 MonsterTick에서만 움직이고 BuildIndex가 그 뒤라 스냅샷 = 현재). 투사체·궤도 원·장판·플레이어 쪽은 등록된 `CollisionBody`의 현재 `Position`·`Radius`를 읽는다(ProjectileTick이 BuildIndex 뒤). 스냅샷에 없는 몬스터 바디(등록됐지만 아직 스냅샷에 안 들어온 — 생성 스텝)는 그 스텝 판정에서 빠진다 — TAC-F18-6과 일치한다(생성 스텝 s의 Collision에는 없고 s+1부터). **자체 전략은 `MonsterSpatialIndex.Required == true`를 전제**한다(App 계약, 5.7 14회차). 테스트에서는 `Required = true`로 두고 `index.OnPhase(BuildIndex, …)`를 당신이 부른다.
- **C5. 등록 바디 보관**: 레이어별 목록을 두고 `StrategySlot`(자체)·`PhysicsSlot`(동기화기)로 O(1) 해제(swap-remove 뒤 슬롯 갱신). 순회가 ID 오름차순이어야 하는 곳(R10: 동기화 `MovePosition`·`rb.position` 대입, 자체 선형 순회, `ApplyRequirements`)은 **순회 전에 ID로 정렬한 사본**을 쓰거나 정렬 삽입을 유지한다 — 방법은 당신이 고르되 보고에 적고, 결정성 테스트(같은 등록을 다른 순서로 해도 같은 이벤트 목록)로 보인다.
- **C6. 엔진 콜백 큐**: `CollisionBodyLink`는 자기 `CollisionBody`와, 동기화기가 `Register` 때 넣어 준 큐 참조(또는 소유 전략 참조)만 안다. `OnTriggerEnter2D/Exit2D(Collider2D other)`에서 `bodies.TryGetBody(other, out otherBody)`로 상대를 찾고(없으면 버림 = 미등록 필터), `CollisionChannelTable.TryGetChannel(my.Layer, other.Layer, out ch, out iAmA)`로 채널·A/B를 정해 큐에 넣는다. **같은 쌍이 두 링크에서 두 번 들어오므로**(양쪽 콜라이더가 각각 콜백을 받는다) 큐는 (채널, A, B, 종류) 중복을 제거한다 — [기본값] A측 링크의 콜백만 채택(`iAmA`일 때만 넣기). 채널이 없는 쌍(레이어 매트릭스가 막아 원래 오지 않아야 함)은 버리고 **카운트만 세어** `★ DroppedCallbackCount`로 노출한다(U12 확인에 쓴다).
- **C7. `NotifyRadiusChanged`**: 자체는 할 일 없음(매 스텝 `body.Radius`를 읽는다). 동기화기/엔진은 더티 표시 뒤 다음 `SyncAllForSimulation`(또는 `SyncMonstersForQuery`)에서 `CircleCollider2D.radius`를 갱신. `ICollisionStrategy.NotifyRadiusChanged`는 엔진 전략이 동기화기에 위임한다(동기화기에 ★ 공개 메서드 추가 허용).
- **C8. `Clear()`**(판 리셋): 자체 — 쌍 집합·등록 목록·픽업 해시 비움. 엔진 — 큐·쌍 상태 비움(바디 등록 해제는 `EntityLifecycle.DespawnAll`이 이미 역순으로 했다; `Clear` 뒤 남은 등록 바디가 있으면 `InvalidOperationException`이 아니라 **경고 로그 + 비움** — 리셋 경로의 방어).
- **C9. 레이어 매트릭스 범위**: `Apply()`는 8~17 사이의 모든 쌍을 `IgnoreLayerCollision(i, j, true)`로 끈 뒤 9채널 쌍만 `false`로 켠다. 8~17과 그 밖 레이어(0~7, 18~31)의 쌍도 끈다(우리 바디가 다른 레이어와 부딪히지 않게). `Apply()`는 멱등이고 몇 번 불러도 같다. 테스트는 `Physics2D.GetIgnoreLayerCollision`으로 9채널 켜짐·나머지 꺼짐을 전부 단언한다(45쌍 + 바깥 쌍 표본).
- **C10. 전략 선택과 요구사항의 조합 표(5.9.1)**: 자체 전략 + 비 Physics2D 타깃이면 콜라이더 전부 꺼짐·`Simulate` 없음. 당신의 PlayMode 동등성 테스트는 "자체 전략(콜라이더 꺼짐) vs 엔진 전략(전부 켜짐)"으로 같은 스크립트 배치를 돌려 **같은 `CollisionEvent` 목록**(비교 키 전체)을 단언한다 — 웨이브 3의 **E1 하네스 골격**이다(실제 E1은 웨이브 4 뒤 게임 시나리오로). E2 골격은 같은 배치를 `Discrete`·`Continuous` 두 모드로(U6 결과를 함께 보고).
- **C11. 테스트 환경**: EditMode 테스트 asmdef는 Editor 전용이라 그 안의 `SimEntity` 파생 더블은 `AddComponent`가 안 된다(2b 실측). 자체 전략은 GameObject가 필요 없으니 **EditMode에서 `CollisionBody`·`MonsterSnapshotBuffer`·`MonsterSpatialIndex`만으로** 시험한다. 엔진 전략·동기화기·데코레이터·설정은 PlayMode. `Physics2D` 전역 설정은 에디터 프로세스에 남으므로 PlayMode 테스트의 `[TearDown]`에서 `simulationMode`만은 원래 값으로 되돌린다(다른 PlayMode 테스트 — Loop·Pool — 가 `FixedUpdate` 기반이라 영향은 없어야 하지만 확인한다: 전체 스위트 3회가 그 검증이다).
- **C12. 측정 가설용 카운터(★ 허용)**: 자체 전략에 이번 스텝 검사한 쌍 수(`CheckedPairCount`), 엔진 전략에 콜백 수(`CallbackCount`)를 읽기 전용으로 노출해도 된다 — F18 측정 가설(7절)에 수치를 붙이기 위해서다. 스텝당 할당 0을 목표로 하되(`CollectEvents` 반환 목록 재사용, 5.9 `list reused until next call`) 측정은 안 한다.

### 5-C. 기준선 — 정상 동작인데 결함으로 오판하기 쉬운 것

1. `QueryCircle`은 **상위 집합**이고 **목록을 비운다**(덧붙이지 않음). 큰 몬스터는 결과에 **항상** 들어 있다 — 따로 더하면 중복이다.
2. 스냅샷 인덱스 ≠ `EntityHandle` 인덱스. `snapshot[i].Id`가 ID다.
3. `MonsterSpatialIndex.Required == false`면 `OnPhase`가 재구축하지 않는다(가드 없음). 결과가 오래되거나 인덱서 예외가 나면 당신 쪽 전제(`Required`) 문제다.
4. Box2D는 **정확히 접촉(거리 = 반지름 합)에서 Enter**를 낸다(U9) — R5 `≤`와 같다. `defaultContactOffset = 0.0001`은 그대로.
5. 키네마틱 트리거 콜백은 `useFullKinematicContacts = true`가 없으면 오지 않는다(U5).
6. `callbacksOnDisable = false`에서 콜라이더를 끄면 Exit이 **오지 않는 것이 정상**(U8, TAC-F18-5).
7. Script 모드에서 `rb.position` 대입은 `Simulate` 없이 즉시 쿼리에 반영되고(U32), `transform.position` 대입은 `SyncTransforms`가 필요하다 — 설계는 `rb.position`을 쓴다. `autoSyncTransforms = false`.
8. `MovePosition`은 **다음 `Simulate`에서** 적용된다(그 전 쿼리는 옛 위치, U11). `Register`가 `rb.position`을 직접 대입하는 이유다.
9. test-framework 1.6: `LogAssert.ignoreFailingMessages`는 `[SetUp]`에서 켜도 본문에 이어지지 않는다 — 오류 로그를 의도적으로 내는 테스트는 **본문에서** 켠다. 켤 때 `IgnoreFailingMessages:true` 로그 한 줄이 남는다.
10. 첫 배치 실행은 Library 생성으로 2~5분이다. 기존 테스트 479·144건이 당신 변경 전에도 전부 통과한다 — 실패가 보이면 당신 변경(또는 Physics2D 전역 설정 잔존)이 원인이다.
11. `CollisionBody`의 `StrategySlot`·`PhysicsSlot`은 각 소유자만 쓴다 — 두 전략이 동시에 한 바디를 등록하는 일은 없다(`ReplaceStrategies`는 `DespawnAll` 뒤).
12. `Thread.Sleep(100)`은 catch-up 테스트(LOOP)의 예외였다 — 당신 테스트에는 필요 없다.

## 6. 금지

- 시크릿 파일 열람 금지. git 상태 변경 명령(`commit`/`push`/`reset`/`checkout`/`stash`/`git rm`)·패키지 설치·`rm -rf` 금지. 파일 단위 삭제만.
- **단언을 느슨하게 고쳐 초록 금지.** 공개 시그니처·이름·네임스페이스·열거형 순서 변경 금지(반박으로 올리고 문서대로 구현).
- 스텝마다 로그 금지(측정 오염). 보고에 시크릿·긴 로그 원문 금지. 로그·보고에는 예외 `message`만.
- Simulation·Data 타입 참조 금지(Strategies는 Core만). 남의 타입 스텁 금지 — 필요한 더블은 테스트 어셈블리 안에.
- `RUN_LOG.md`·`DECISIONS.md`·`docs/**`·`spec/**`·`tech/**` 수정 금지(읽기만).

## 7. 보고 형식 (압축 — PM은 원본 데이터가 아니라 결론을 받는다)

1. **만든 파일**(5c.2 IMP-COLLISION 행과 대조, 추가 파일과 이유), 네임스페이스, 테스트 건수(EM·PM 각각, 기존 479·144 + α).
2. **테스트 결과 표**: EM 1~3·PM 1~3의 total/passed/failed/inconclusive/skipped/종료 코드/XML 경로, 재현 명령(래퍼면 래퍼 내용).
3. **미확정 실측 결과**: U4·U6·U10·U11·U12·U27·U28 각각 확인/틀림/부분 + 수치·배치 설명. U6은 E2의 의미에 직결되므로 배치(속도·반지름·모드)와 결과를 상세히.
4. **계약 대조표**: 문서 그대로 / 문서와 다른 점(코드가 옳았던 근거) / ★ 추가 공개 멤버 전부(C6 `DroppedCallbackCount`, C7 동기화기 메서드, C12 카운터 등).
5. **PM 기본값 C1~C12 각각**: 따랐다 / 반박(근거).
6. **동등성 결과**: 자체 vs 엔진 스크립트 배치 N개의 이벤트 목록 일치 여부, 불일치가 있었다면 첫 불일치 스텝·거리·반지름 합(5c.5 E1 행 형식). Discrete vs Continuous 결과.
7. **F18 기록 초안**(5c.2a 의무 — 보고에만 담고 파일은 만들지 않는다): (a) devlog용 **F18 측정 가설**(엔진 vs 자체의 비용 구조 — 400기 콜라이더 유지·`Simulate`·콜백 vs 해시 질의·쌍 비교; N에 따른 예상; 두 전략의 메모리·GC 차이 예상) — "가설"임을 명시. (b) **E1·E2 실행 결과 요약(골격)**: 이번 웨이브의 합성 배치 결과와 U6 결과, 실제 E1·E2는 웨이브 4 뒤 게임 시나리오로. (c) README용 두 전략 설계 요약 1단락.
8. **읽을 목록 밖에서 연 파일**(8절 목록 밖의 기획 문서·코드 — 열었다면 이유).
9. **확신하지 못하는 것** — 추측하지 말고 미확정으로. (항상 마지막)

## 8. 읽을 문서 (이 목록 밖의 기획 문서는 열지 마세요. 열어야 했다면 보고에 적으세요 — 목록의 결함입니다)

- 전원 공통: `spec/00-common.md`(3.0 H·I), `tech/README.md`, `tech/00-header.md`(범례만), `tech/02-feature-tac.md`(3.0 TAC-0-*, F18 전부, F16-3·F16-6, F9-7, F3-12·F4-3·F6-9(채널 소비 쪽 기대)), `tech/5a-shared-rules.md`(R5·R10·R14·R16·R18·R19·R25·R30), `tech/5c-ownership-contracts.md`(5c.1~5c.5 전부 — 당신 행과 5c.3 #6·#7·#8·#9, 5c.5 자체 충돌·E1·E2 행).
- 필수: `spec/F18-collision-strategy.md`, `tech/5.09-collision.md`(전체), `tech/5.06-pool.md`(데코레이터 문단 + 5.6.1 템플릿 사실), `tech/5.07-targeting.md`(5.7.1 `QueryCircle` 의미, `Required` 계약).
- 계약만(시그니처): `tech/5.03-step-clock.md`(`SimulationPhase`·`IStepPhaseHandler`·`StepContext`), `tech/5.04-commands-spawn.md`(`EntityCategory`), `tech/5.10-lifecycle.md`(`Spawn`/`Despawn` 순서), `tech/06-open-questions.md`(6.2 U4~U12·U27·U28·U30·U32, D15·D27·D30).
- 코드(원본): `Scripts/Core/`의 `CollisionEnums.cs`·`CollisionEvent.cs`·`CollisionBody.cs`·`CollisionChannelTable.cs`·`CollisionLayerMapping.cs`·`ColliderActivationPolicy.cs`·`CircleMath.cs`·`ICollisionStrategy.cs`·`IPhysicsBodyRegistry.cs`·`IEntityTemplateDecorator.cs`·`MonsterSnapshot.cs`·`MonsterSnapshotBuffer.cs`·`ProfilerMarkers.cs`·`StrategySelection.cs`·`StepMath.cs`·`EntityHandle.cs`; `Scripts/Strategies/Spatial/*.cs`(전부), `Scripts/Strategies/Pool/PoolTemplateDecorator.cs`, `Scripts/Strategies/Tick/SelfTickTemplateDecorator.cs`(데코레이터 관례); 읽기만: `Scripts/Simulation/Entities/SimEntity.cs`·`EntityLifecycle.cs`·`MonsterRegistry.cs`, `Scripts/Simulation/Loop/CollisionEventRouter.cs`; 테스트 관례: `Tests/EditMode/Spatial/SpatialTestUtil.cs`, `Tests/PlayMode/Loop/LoopPmRig.cs`·`LoopPmDoubles.cs`, `Tests/PlayMode/Pool/PoolTestObjects.cs`.
- 결정 배경이 필요하면 `DECISIONS.md`의 "충돌" 관련 절만. `RUN_LOG.md`는 열지 않는다.

=== 프롬프트 끝 ===
