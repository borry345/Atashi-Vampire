# 웨이브 4c 디스패치 브리핑 — FIX-COLLISION-PLAYER (2026-11-09, PM 작성, 승인됨 "추천대로 진행")

> `general-purpose`, `model: opus`, **isolation 없음** — 4b 구현자의 worktree 안에서 작업한다(새 worktree를 만들면 4b 산출물이 없다).
> 배경: 4b(IMP-FLOW-APP)의 36 전략 조합 결정론 테스트가 엔진 충돌 조합 12개에서 빨강이고, 실물 `PhysicsBodySynchronizer` + `PlayerEntity` 조합이 자석 등록에서 예외를 내는 통합 결함이 드러났다. 둘 다 4b 소유 밖(COLLISION·PLAYER)이라 별도 수정자를 보낸다. 끝나면 4b + 이 수정을 함께 main에 머지한다.

=== 프롬프트 시작 ===

# 역할: FIX-COLLISION-PLAYER — 엔진 충돌 희귀 불일치 조사·수정 + 플레이어 자석 등록 수정

당신은 뱀서라이크 1차 MVP(Unity 6000.3.28f1, C#)에서 두 결함을 고친다. **B**(주 작업): 엔진 충돌 전략(`EngineCollider2D`)이 자체 충돌 전략(`SelfSquaredDistance`)과 한 시드에서 1,500스텝 뒤 갈리는 원인을 찾아 COLLISION 소유 코드에서 고친다. **A**(소형): `PlayerEntity`가 몸·자석 두 `CollisionBody`에 같은 Host GameObject를 주어 실물 동기화기가 두 번째 등록을 거부하는 문제를 PLAYER 소유 코드에서 고치고, App의 우회를 제거한다.

## 1. 이미 확인된 사실 (다시 조사하지 말 것)

### 작업 위치
- **모든 작업은 `C:\Users\SSAFY\Desktop\borry\indiv\.claude\worktrees\agent-ad57740e5cd25f84a` 안에서만** 한다(브랜치 `worktree-agent-ad57740e5cd25f84a`, HEAD 6054145 = 4b 산출물 체크포인트). Unity 프로젝트는 그 아래 `Survivor/`. 원본 저장소 `C:\Users\SSAFY\Desktop\borry\indiv`의 파일은 **이 브리핑 파일 외에는 읽지도 쓰지도 않는다**(기획 문서 `spec/`·`tech/`는 worktree 안의 사본을 읽는다 — 내용이 같다).
- 테스트 실행(worktree 루트에서, Unity 인스턴스는 한 번에 하나, Bash `timeout` 600000):
  ```
  bash "C:/Users/SSAFY/.claude/jobs/f32c1ccc/tmp/flow-run.sh" EditMode fix-em1
  bash "C:/Users/SSAFY/.claude/jobs/f32c1ccc/tmp/flow-run.sh" PlayMode fix-pm1
  ```
  태그는 `fix-` 접두사로 매 실행 바꾼다. 결과는 `C:/Users/SSAFY/.claude/jobs/f32c1ccc/tmp/flow-<tag>.xml/.log`. 임시 파일도 그 폴더에만. 한 실행 3~6분.
- **기준선(worktree HEAD 6054145, PM 재현)**: EditMode **703/703**. PlayMode **254 중 242 통과·12 실패**. 실패 12건은 전부 `Tests/PlayMode/Flow/FlowStrategyCombinationTests.Combination_HashEqualsTheShippingDefault`의 `*/*/EngineCollider2D/SpatialHash`와 `*/*/EngineCollider2D/BruteForce`(풀 3 × 틱 2). 12개의 해시는 서로 같고(0xBD3023F07279F393) 기준 `StrategySelection.ShippingDefault`(CustomPool/SpatialHash/CentralTick/Self, 0x7AE2FE65ACEFF8F4)와만 다르다. `EngineCollider2D/Physics2DQuery` 6개를 포함한 나머지 24개는 기준과 같다. 이 테스트는 **수정 금지**다 — 당신의 합격 기준은 이 테스트가 그대로 36/36 초록이 되는 것이다.
- 조합 테스트 설정: `Seed = 4242`, `Steps = 1800`, `FlowPmCrowdPolicy(200, 12, "M1", "M2", "M3")`, 레벨업 비활성, 무적, `Host.Rules.StopAtStep = Steps`. 리그는 `Tests/PlayMode/Flow/FlowPmFixture.cs`(+`FlowPmDoubles.cs`)로, `Main.unity`를 로드해 실물 `GameBootstrap`(`IDevModeHost`)을 쓴다. 조합 1개 0.6~1.3초.

### B — 알려진 증상 (4b 구현자가 임시 진단 테스트로 찾음, 진단 테스트는 삭제됨)
- 첫 분기: seed 4242, `CustomPool/CentralTick/EngineCollider2D/BruteForce`, **step 1500**. 궤도 원(OrbitOrb) **3:1** 위치 (2.5833, −0.0000) 반지름 0.5와 몬스터 **25:3** 위치 (3.1015, −0.0992) 반지름 0.5 — 중심 거리 ≈0.528(겹침). step 1499에서도 ≈0.897로 이미 겹쳐 있었다. 자체판은 step 1500에 이 몬스터를 처치(kills 445), 엔진판은 hp 4가 남았다(kills 444).
- `SimulationStateHash`는 **몬스터 hp를 포함**한다(`Scripts/Simulation/Loop/SimulationStateHash.cs`). 따라서 step 1499까지 해시가 같았다는 것은 그때까지 두 판의 피해 이력이 완전히 같았다는 뜻이고, step 1500에 **엔진판만 피해 1회를 놓쳤다**. 겹침이 두 스텝 연속인데 한 스텝만 다르므로 "한 스텝 늦는 체계적 지연"이 아니라 특정 경로의 드문 경우다.
- 기존 `Tests/PlayMode/Collision/CollisionEquivalenceTests`(무작위 배치, Self == EngineDiscrete, EngineDiscrete == EngineContinuous)는 통과한다. 즉 일반적인 MovePosition 경로는 동치다.
- 엔진 경로(`tech/5.09`, 코드 확인됨): `PhysicsBodySynchronizer`가 유일하게 `rb.position`/`MovePosition`을 쓴다. `Register`는 호스트에 컴포넌트를 붙이고 `transform.position`·`rb.position`을 즉시 대입(TAC-F18-4, 풀 호스트 재사용 시 옛 위치에서 스윕되지 않게). `SyncAllForSimulation()`은 엔진 전용으로 반지름 갱신 후 **켜진 모든 바디에 `MovePosition(body.Position)`**(Id 오름차순). `SyncMonstersForQuery()`는 Physics2D 타깃일 때 BuildIndex(order 5)에서 **몬스터만 `rb.position` 대입**. `EngineCollisionStrategy.CollectEvents(step)`는 Sync → `Physics2D.Simulate(StepMath.StepSeconds)` 1회 → `CollisionBodyLink`의 `OnTriggerEnter2D/Exit2D` 콜백 큐를 **쌍별 순 전이**(한 Simulate 안에서 Enter 뒤 Exit면 이벤트 없음, `CancelledTransitionCount`)로 접어 `CollisionEvent.CompareTo` 순으로 정렬해 돌려준다. 바디: Kinematic, `useFullKinematicContacts = true`, `isTrigger = true`, `collisionDetectionMode = 선택값`(ShippingDefault/조합 테스트는 `StrategySelection`의 기본 `EngineDetectionMode` — 코드 확인). `Unregister`는 콜라이더를 끄고(Exit 콜백 없음, `callbacksOnDisable = false`) 잊는다.
- **갈리는 조합과 안 갈리는 조합의 유일한 차이**: Physics2D 타깃 조합은 BuildIndex에서 몬스터 `rb.position`이 매 스텝 대입된 뒤 Collision 단계에서 다시 `MovePosition`(같은 위치) → Simulate. 갈리는 조합은 몬스터도 `MovePosition`만 받는다. 그러므로 원인은 **`MovePosition`만으로 움직이는 몬스터 바디**와 관련이 깊다. 후보(추측 — 실측으로 좁힐 것): ① `MovePosition` 스윕과 Box2D 접촉 갱신 시점(접촉 판정이 이전 위치 기준인 경우), ② 풀 호스트 재등록(25:3은 핸들 25의 세 번째 세대 = 재사용된 풀 호스트; `Register`의 즉시 대입과 그 스텝의 `MovePosition`·Simulate 상호작용, Enter→Exit 순 전이 접힘), ③ 반지름 갱신 보류(`NotifyRadiusChanged`가 다음 Sync에서 적용), ④ 콜백 큐 정렬·중복 처리, ⑤ `OrbitOrb.Overlaps`/`ContactDamageTracker`가 Enter·Exit을 소비하는 방식이 두 전략의 이벤트 순서·중복에 다르게 반응. 궤도 원은 매 스텝 크게 움직이고(공전) 몬스터는 플레이어 쪽으로 천천히 움직인다.
- 당신이 할 조사: **임시 진단 테스트**(`Tests/PlayMode/Collision/` 또는 `Tests/PlayMode/Flow/`에 새 파일 — 끝나면 삭제하거나 회귀 테스트로 다듬는다)로 seed 4242 두 조합을 돌리며 step 1490~1502 구간의 (3:1, 25:3) 쌍에 대해 두 전략의 `CollisionEvent` 목록, 두 바디의 `Position`·`Radius`, 엔진판 `rb.position`·`transform.position`·콜라이더 enabled·`CancelledTransitionCount`, 25:3의 스폰(등록) 스텝, 그리고 `OrbitOrb.Overlaps`의 내용을 스텝별로 로그한다. 이벤트 목록을 얻는 방법은 `CollisionEventRouter`에 들어가기 전의 `ICollisionStrategy.CollectEvents` 반환값을 감싸는 래퍼 전략(테스트 코드)으로 — `IDevModeHost.NextRunCollisionDecorator`가 정확히 그 용도다(`Func<ICollisionStrategy, ICollisionStrategy>` 형태 — `Scripts/App/IDevModeHost.cs`에서 시그니처 확인).
- 원인을 찾으면 **COLLISION 소유 코드에서** 고친다(아래 2절). 고친 뒤 **같은 경우를 재현하는 작은 회귀 테스트**를 `Tests/PlayMode/Collision/`에 남긴다(가능하면 조합 리그 없이 `CollisionPmWorld`로 최소 재현 — 안 되면 조합 리그를 써도 되지만 36조합 전체를 다시 돌리는 테스트는 만들지 않는다). 회귀 테스트는 **수정 전 빨강 → 수정 후 초록**을 수치로 보고한다.
- 원인이 Unity/Box2D 자체의 의미론(예: 킨마틱 `MovePosition` 접촉 갱신 시점)이라 **자체 충돌과 완전히 같게 만들 수 없다고 판단되면**: 고치지 말고 근거(스텝별 로그 발췌)와 선택지(동기화 방식 변경의 비용·위험)를 보고하고 멈춘다. 그 판단은 PM이 한다. 테스트를 건드려 초록을 만드는 것은 어느 경우에도 금지다.

### A — 확인된 사실
- `Scripts/Simulation/Player/PlayerEntity.cs` 27~28행: `Body = new CollisionBody { Layer = PlayerBody, Host = host }`, `MagnetBody = new CollisionBody { Layer = PlayerMagnet, Host = host }`. `RegisterForRun`은 `_physicsBodies.Register(Body)` → `collisions.Register(Body)` → `_physicsBodies.Register(MagnetBody)` → `collisions.Register(MagnetBody)` 순.
- `Scripts/Strategies/Collision/PhysicsBodySynchronizer.cs` 64~67행: `body.Host.TryGetComponent(out CircleCollider2D existing) && _byCollider.ContainsKey(existing)`이면 `"host '…' already carries a registered body."` 예외. 호스트 하나 = 콜라이더 하나가 동기화기의 계약이다(`CollisionTemplateDecorator.EnsureComponents`가 Rigidbody2D·CircleCollider2D·CollisionBodyLink 각 1개를 보장). 그러므로 실물 동기화기에 `PlayerEntity`를 그대로 등록하면 자석 등록에서 **전략과 무관하게** 예외다(동기화기 `Register`는 요구사항과 관계없이 호출되고, 콜라이더 켜기만 요구사항을 따른다).
- 4a `FlowEmRig`·PLAYER `PlrRig`는 가짜 `IPhysicsBodyRegistry`를 써서 이 결함을 못 잡았다. 4b `GameBootstrap.cs` 250·257행이 우회: `_magnetHost = new GameObject("Player.Magnet")` → `_player.MagnetBody.Host = _magnetHost`(`CollisionBody.Host`는 Core의 공개 필드). `GameBootstrap.MagnetHost`(internal, 129행)와 `Tests/PlayMode/Flow/FlowAssemblyTests.cs` 144행(`AreNotSame(Body.Host, MagnetBody.Host, "workaround: …")`)이 그 우회를 참조한다.
- **PM [기본값] 수정 방향**: `PlayerEntity` 생성자가 자석용 자식 GameObject를 스스로 만든다 — `new GameObject("Magnet")`를 `host.transform` 아래에 `SetParent(host.transform, false)`로 두고 `MagnetBody.Host`로 쓴다. 공개 시그니처(생성자 5인자)는 바꾸지 않는다. ★ 읽기 속성 `MagnetHost`를 추가해도 된다(보고에 ★ 표시). 주석의 "Both bodies share the injected Host"는 사실에 맞게 고친다. App의 우회(250·257행, `MagnetHost` 속성)는 제거하고, `FlowAssemblyTests` 144행은 "자석은 몸통과 다른 host(PlayerEntity가 만든 자식)"로 메시지만 바꾸거나 `PlayerEntity.MagnetHost`를 단언한다. **PLAYER 쪽 EM 테스트**(`Tests/EditMode/Player/PlayerEntityMovementTests.cs` 등)에 "자석 host는 몸통 host의 자식이며 둘이 다르다" 단언 1건을 추가한다. 수정 전에 `GameBootstrap` 우회만 제거한 상태로 PlayMode를 한 번 돌려 **예외가 실제로 나는 것을 확인**(빨강)한 뒤 고친다 — 4b 구현자는 코드로만 추론했다.

## 2. 배타적 파일 소유권 (이 밖의 파일 수정 금지)

1. **COLLISION**: `Survivor/Assets/Scripts/Strategies/Collision/**`(`EngineCollisionStrategy.cs`, `PhysicsBodySynchronizer.cs`, `CollisionBodyLink.cs`, `CollisionTemplateDecorator.cs`, `SelfCollisionStrategy.cs` — 자체판은 기준이므로 **의미를 바꾸지 않는다**), `Survivor/Assets/Tests/PlayMode/Collision/**`(기존 단언 완화 금지, 회귀 테스트·임시 진단 추가 가능), `Survivor/Assets/Tests/EditMode/Collision/**`(있으면).
2. **PLAYER(A만)**: `Survivor/Assets/Scripts/Simulation/Player/PlayerEntity.cs`, `Survivor/Assets/Tests/EditMode/Player/**`(단언 추가만).
3. **App(A 우회 제거만)**: `Survivor/Assets/Scripts/App/GameBootstrap.cs`의 자석 우회 3곳(129·250·257행 부근)과 그 필드. 다른 줄은 손대지 않는다. `Survivor/Assets/Tests/PlayMode/Flow/FlowAssemblyTests.cs` 144행 단언 1개. **`FlowStrategyCombinationTests.cs`·`FlowPmFixture.cs`·`FlowPmDoubles.cs`·다른 Flow 테스트는 수정 금지** — B 진단에 필요하면 `Tests/PlayMode/Flow/` 또는 `Tests/PlayMode/Collision/`에 **새 파일**을 만든다(끝나면 삭제하거나 회귀 테스트로 남김).
4. Unity 자동 생성 `.meta`(새 파일 것).

건드리지 않는 것: `Scripts/Core/**`(`CollisionBody`·`StrategySelection` 포함 — 바꿔야 한다고 판단되면 보고), `Scripts/Simulation/**`(PlayerEntity.cs 외 전부 — `OrbitOrb`·`ContactDamageTracker`·`WeaponInventory`·`PlayerHitHandler`는 **읽기만**, 원인이 그쪽이면 고치지 말고 보고), `Scripts/App/**`(GameBootstrap 우회 3곳 외), `Scripts/UI/**`, `Scripts/Data/**`, `Scenes/**`, `ProjectSettings/**`, `Packages/**`, `StreamingAssets/**`, `TextMesh Pro/**`, 기존 테스트 파일의 기존 단언. 원본 저장소의 `RUN_LOG.md`·`DECISIONS.md`·`docs/`·`spec/`·`tech/`는 쓰지 않는다.

## 3. 검증 재현성

- `Library/`·`Temp/`·`Logs/` 삭제 금지. 패키지 수정 = BLOCKED. `git commit`/`push`/`reset`/`stash`/`rm -rf` 금지(PM이 체크포인트를 만든다). 같은 도구 호출이 같은 이유로 2회 연속 실패하면 세 번째를 시도하지 말고 보고.
- 보고 수치는 PM이 같은 명령으로 재현 가능해야 한다. 마지막 3회 XML 경로를 적는다.

## 4. 결정성

- 테스트에 `UnityEngine.Random`·시드 없는 `System.Random`·`DateTime.Now`·실시간 대기 금지.
- **합격 기준**: `FlowStrategyCombinationTests` 36/36 초록 **그대로**(테스트 무수정), 회귀 테스트 수정 전 빨강 → 후 초록, 기존 `CollisionEquivalenceTests` 전부 초록 유지, **EditMode 전체 3회 전회 동일 + PlayMode 전체 3회 전회 동일(실패 0)**. 여섯 줄 그대로 보고.
- 수정이 동기화 방식을 바꾼다면(예: 특정 바디를 `rb.position` 대입으로) **Physics2D 타깃 조합·`CollisionEquivalenceTests`·`PhysicsMeasurementTests`가 여전히 통과하는지**와 `Physics2DTargetProvider`(스냅샷 위치 + 물리 질의)에 영향이 없는지 함께 확인하고 보고한다.

## 5. 기준선 — 정상 동작인데 결함으로 오판하기 쉬운 것

1. `EngineCollisionStrategy.CancelledTransitionCount`는 정상 동작이다(TAC-F18-4 ③ 개정: 이 스텝에 등록된 바디가 다른 바디의 **이전 위치**와 만나 Enter→Exit가 한 Simulate에 나오면 이벤트 없음). 이 수가 0이 아닌 것 자체는 결함이 아니다 — 다만 1500스텝 사건과 연관되는지는 확인 대상.
2. `Unregister`는 Exit 콜백을 내지 않는다(`callbacksOnDisable = false`) — 디스폰 시 `WeaponInventory`가 `Overlaps`를 Exit으로 재생한다(5.12). 자체판도 같다.
3. 엔진판 `Clear()`는 바디가 남아 있으면 예외가 아니라 경고 로그(17회차 PM 판정).
4. `Physics2DTargetProvider`는 콜라이더가 꺼져 있으면 `TryFindNearest` false, 로그 없음.
5. 풀 전략이 바뀌는 조합 전환에서 `EntityLifecycle.ReplaceStrategies`가 옛 풀을 `Clear()`(`Object.Destroy`)한다 — 조합 테스트가 풀을 바꿀 때 Destroy가 나는 것이 정상.
6. 조합 테스트의 `InstantiateDestroy` 풀은 엔티티마다 새 GameObject라 "풀 호스트 재사용"(후보 ②)이 없는데도 **같은 해시로 갈린다**(0xBD3023F07279F393) — 따라서 후보 ②(재등록)는 단독 원인이 아닐 가능성이 크다. 이 사실을 먼저 활용하라.
7. 배치 PlayMode에서 `Keyboard.current`는 null — 리그는 `FlowPmInput`으로 입력을 넣는다.
8. `BestRecordStore`는 PlayerPrefs — Flow 리그가 테스트 키로 격리한다. 건드리지 않는다.

## 6. 금지

- 시크릿 파일 열람 금지. git 변경 명령·패키지 설치·`rm -rf` 금지.
- **단언 완화·조합 제외·허용 오차·Ignore로 초록 만들기 금지.** 자체 충돌 전략의 의미를 바꿔 엔진판에 맞추는 것도 금지(자체판이 기준이다).
- 문서 공개 시그니처·이름 변경 금지(반박으로). 주석은 영어 산문, 공개 멤버 한 줄 요약(`CODING_GUIDE.md` §3). 파일당 공개 타입 1개.
- 프레임마다 로그 금지(진단 로그는 임시 테스트 안에서만, 완료 전 제거).

## 7. 보고 형식 (압축)

1. **B 원인**: 스텝별 로그 발췌(1498~1501, 두 전략의 이벤트·위치)와 한 단락 설명. 어느 후보였는지, 왜 Physics2D 타깃 조합은 무사했는지.
2. **B 수정**: 바뀐 파일·메서드와 의미 변화 한 줄씩. 회귀 테스트 이름 + 수정 전 빨강(실패 메시지) → 수정 후 초록.
3. **A 수정**: 우회 제거만 했을 때의 실제 예외 메시지(빨강 증거), 수정 내용, ★ 추가 공개 멤버.
4. 테스트: EM 3회 + PM 3회 원문 여섯 줄 + XML 경로 + 36조합 총 소요.
5. 소유 밖에서 고쳐야 한다고 판단한 것(고치지 않고 위치·이유).
6. PM 기본값을 따르지 않은 것과 근거. 7. 읽을 목록 밖에서 연 파일. 8. **확신하지 못하는 것 — 추측하지 말고 미확정으로** (항상 마지막). 마지막에 `git status --short`.

## 8. 읽을 문서 (worktree 안의 사본)

- `tech/5.09-collision-strategy.md`(또는 `tech/5.09*.md`) 전체, `tech/02-feature-tac.md`의 **F18 절**(TAC-F18-1~5)·TAC-F9-7, `tech/5.12-damage-rules.md`의 "플레이어 등록 경로" 블록(16·17회차)과 `PlayerEntity`·`ContactDamageTracker`·`OrbitOrb` 계약 블록, `tech/5a-shared-rules.md` R19·R6, `tech/5c-ownership-contracts.md` 5c.3 #1·#7.
- 코드: `Scripts/Strategies/Collision/**` 전부, `Scripts/Core/CollisionBody.cs`·`CollisionEvent.cs`·`StrategySelection.cs`·`ColliderActivationPolicy.cs`, `Scripts/Simulation/Player/PlayerEntity.cs`, `Scripts/Simulation/Weapons/OrbitOrb.cs`·`ContactDamageTracker`(위치는 `Scripts/Simulation/**`에서 찾는다)·`WeaponInventory.cs`(Overlaps 재생 부분), `Scripts/Simulation/Loop/SimulationStateHash.cs`, `Scripts/App/IDevModeHost.cs`·`GameBootstrap.cs`(우회 3곳과 `NextRunCollisionDecorator` 적용 지점), 테스트 `Tests/PlayMode/Collision/CollisionEquivalenceTests.cs`·`CollisionPmUtil.cs`·`CollisionPmFixture.cs`·`TriggerRecorder.cs`, `Tests/PlayMode/Flow/FlowStrategyCombinationTests.cs`·`FlowPmFixture.cs`·`FlowPmDoubles.cs`(읽기만), `Tests/EditMode/Player/PlrRig.cs`·`PlayerEntityMovementTests.cs`.
- 주지 않는 것: `RUN_LOG.md`, `DECISIONS.md`, `spec/F*.md`(F18 제외 — `spec/F18-collision-strategy.md`는 읽어도 됨), 다른 `tech/5.x`.

## 작업 순서 제안

1. A 먼저(작고 독립적): 우회 제거 → PlayMode 1회로 예외 확인 → `PlayerEntity` 수정 → EM 단언 추가 → EM·PM 각 1회.
2. B 진단: 래퍼 전략 + 임시 테스트로 seed 4242 두 조합의 1490~1502 로그 → 원인 확정.
3. B 수정 + 최소 회귀 테스트(빨강 확인 후 초록) → 36조합 확인 → 임시 진단 제거.
4. EM 3회 + PM 3회 → 보고.

=== 프롬프트 끝 ===
