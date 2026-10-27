> 분할 파일 — 원본 `TECH_SPEC.md`(5회차)를 2026-10-27에 제목 단위로 나눈 것이다. 내용은 원본 그대로이며 색인·개정 이력은 `tech/README.md`에 있다. 수정은 tech-spec-writer만 한다.

## 3. 기능 목록 (Feature List)

spec/의 ID를 그대로 쓴다. 추가·삭제는 없다.

| ID | 이름 | 구분 | 담당 구현자(5c) |
|---|---|---|---|
| F1 | 플레이어 이동 | 구현 | IMP-PLAYER (입력 소스: IMP-LOOP, 카메라 스냅: IMP-FLOW) |
| F2 | 자동 공격 | 구현 | IMP-PLAYER |
| F3 | 몬스터 | 구현 | IMP-MONSTER |
| F4 | 아이템 드랍과 획득 | 구현 | IMP-MONSTER (상자 효과 결정·적용: IMP-PLAYER) |
| F5 | 레벨업과 선택지 | 구현 | IMP-FLOW (선택 적용: IMP-PLAYER `PlayerProgressionApplier`) |
| F6 | 무기 시스템 | 구현 | IMP-PLAYER |
| F7 | 조합(진화) | 구현 | IMP-PLAYER |
| F8 | 스테이지와 게임 종료 흐름 | 구현 | IMP-FLOW (반복 배경·창 설정 포함) |
| F9 | 오브젝트 풀링 | 구현 + 전략 | IMP-POOL |
| F10 | 성능 측정과 비교 기록 | 측정 훅 구현 + 사용자 수행 | IMP-DEV (훅) / 사용자 |
| F11 | 데이터 기반 설계 | 구현 | IMP-DATA |
| F12 | README | 문서 | doc-writer (3회차: 각 기능 브랜치 머지 전에 구현자 초안을 반영해 같은 머지 커밋에 포함(5c.2a), 웨이브 6은 최종 정리·다이어그램 검증) |
| F13 | 빌드와 시연 영상 | 사용자 수행 (빌드 설정 일부 구현) | 사용자 / IMP-FLOW(창 설정 코드) |
| F14 | 품질 관리 기록 | 사용자 수행 | 사용자 (`docs/devlog.md` 틀: doc-writer, 기록 초안: 5c.2a, (3회차) 기능 브랜치 머지 커밋에 포함) |
| F15 | 개발 환경과 저장소 | 사용자 수행 | 사용자 (폴더 구조는 5c.1) |
| F16 | 타깃 탐색 전략 | 구현 + 측정 | IMP-SPATIAL |
| F17 | Update 집중화 전략 | 구현 + 측정 | IMP-LOOP |
| F18 | 충돌 판정 전략 | 구현 + 측정 | IMP-COLLISION |
| F19 | 개발자 모드 | 구현 | IMP-DEV |

공통 기반(3.0 F 고정 스텝, 3.0 G 무한 평면, 3.0 H 객체 ID, 3.0 I 피해 규칙, RNG, 공유 규칙)은 기능 ID가 따로 없으므로 IMP-CORE·IMP-LOOP·IMP-PLAYER가 맡는다.

---

## 4. 기능별 요구사항 및 수용 기준 (Functional Requirements & Acceptance Criteria)

spec/ 3절의 수용 기준 286개(2026-10-27 기계 집계, spec/README.md 방법 기준)는 그대로 유효하며 여기서 반복하지 않는다. 아래는 그 기준을 이 설계로 구현할 때 추가로 지켜야 하는 **기술 수용 기준(TAC)** 이다. 각 항목은 5절의 계약과 5a의 공유 규칙을 근거로 하며, 테스트 위치를 함께 적었다(EM = EditMode, PM = PlayMode).

### 3.0 F·H·I (공통) 고정 스텝·결정론·ID·피해

- TAC-0-1 (2회차 수정): 게임 상태를 바꾸는 코드는 `SimulationPhase` 핸들러나 `IUpdatable.Tick` 안에서만 실행된다. 예외는 둘뿐이다. ① `RunSession.Reset()`(판 경계) ② 레벨업 게이트가 닫힌 동안(`clock.CloseReason == LevelUp && !StepActive`) `PlayerProgressionApplier.ApplyLevelUpOption`(5.14). 개발자 조작·핫 리로드 교체·타깃 공급자 전환은 `PendingCommandQueue`에 넣어 다음 스텝의 `Begin`에서 적용한다. (코드 리뷰 + PM)
- TAC-0-2: 로직 코드는 `Time.deltaTime`, `Time.time`, `Time.fixedDeltaTime`, `UnityEngine.Random`, `System.Random`, `DateTime`을 읽지 않는다. 시간은 `StepContext.Step`, 난수는 `RngStreams`만 쓴다. 예외는 일반 플레이의 시드 생성 1회(App)와 개발자 모드의 프레임 시간 수집(Dev)뿐이다. (정적 검사: grep 테스트를 EM에 둔다)
- TAC-0-3: `Time.fixedDeltaTime == 1f/60f`, `Time.maximumDeltaTime == 0.05f`를 부트스트랩에서 코드로 설정하고 PM 테스트로 확인한다. 한 프레임의 스텝 수가 3을 넘지 않는다 [실측 U2].
- TAC-0-4: 같은 시드와 같은 입력 시퀀스로 `Time.captureDeltaTime`을 1/30, 1/60, 1/144로 바꿔 같은 스텝 수만큼 돌리면 `SimulationStateHash`가 같다 (PM, U3).
- TAC-0-5 (2회차 수정): 일시정지·레벨업·결과 중에는 `SimulationClock.IsRunning == false`라 어떤 단계도 실행되지 않고, F17 A의 셀프 티커도 `Tick`을 호출하지 않는다(`StepActive == false`) (PM).
- TAC-0-6: `IUpdatable.Tick`은 자기 상태와 `StepContext`가 주는 읽기 전용 값만 읽고 쓴다. 다른 엔티티의 생성·소멸·피해는 `ISimulationCommands`로 미룬다 (코드 리뷰 + F17 A/B 동등성 PM).
- TAC-0-7 (2회차 신규): 레벨업이 스텝 s에서 발생하는 시나리오를 30/60/144 FPS로 돌리면, 세 경우 모두 게이트가 닫힌 시점의 `clock.Step == s + 1`이고 상태 해시가 같다. 같은 프레임의 catch-up `FixedUpdate`에서 스텝이 더 돌지 않는다. `Update` 경로에 `CloseAfterCurrentStep` 호출이 없고, `Update` 경로의 `Close` 호출 이유는 `Paused`·`RunReset`뿐이다(grep) (PM).
- TAC-0-8 (2회차 신규): `EntityLifecycle.Spawn`·`Despawn`은 현재 단계가 `Lifecycle`이거나 판 리셋 중일 때만 성공하고, 그 밖에서는 `InvalidOperationException`을 던진다. 스텝 s의 Lifecycle에서 생성된 엔티티는 스텝 s의 `CollectEvents`에 나타나지 않고 s+1부터 나타나며, 첫 `Tick`은 s+1이다 (EM + PM: 두 충돌 전략·두 틱 전략에서 같은 첫 피해 스텝).
- TAC-0-9 (2회차 신규, 3.0 H): 디스폰 뒤 같은 인덱스를 받은 엔티티의 `Generation`이 이전보다 1 크다. 풀 A/B/C 세 전략에서 같은 시나리오의 ID 발급 순서(로그)가 같다 (EM + PM).
- TAC-0-10 (2회차 신규, 3.0 I): 쿨다운·무적 기록은 전부 `long` 스텝 번호이고 `EntityId`(세대 포함)로 키를 잡는다. 디스폰된 대상의 기록은 `EntityLifecycle.Despawned`에서 지운다(등록 해제는 Exit을 내지 않으므로, 이 처리가 없으면 겹침 집합에 죽은 대상이 남는다) (EM).

### F1. 플레이어 이동

- TAC-F1-1: `PlayerMovement`는 `IInputSource.ReadMove(step, position)`만 호출한다. `Keyboard.current`를 참조하는 코드는 `KeyboardInputSource` 한 곳에만 있다.
- TAC-F1-2: 대각 방향은 `MoveMath.ToUnitDirection(x, y)`의 8방향 상수 표를 쓴다. 단일 축과 대각의 스텝당 이동 크기 차이가 1e-6 이하다 (EM).
- TAC-F1-3: 속도 = `StatMath.ApplyMultiplier(PlayerStats.moveSpeedPerStep, P2 보너스)`(R13). `MoveCommand.SpeedOverride ≥ 0`이면 그 값을 쓴다(자동 경로 전용) (EM).
- TAC-F1-4 (2회차 수정): 위치를 제한하거나 보정하는 코드가 없다. (반복 배경은 TAC-F8-11로 옮겼다.)
- TAC-F1-5: 게임패드 입력이 아무 효과도 내지 않는다. `KeyboardInputSource`는 `Keyboard.current`만 읽는다 (PM).
- TAC-F1-6 (2회차 신규): `CameraFollow.LateUpdate`(실행 순서 0, IMP-FLOW)가 매 프레임 카메라 위치를 `PlayerEntity.Position`(시뮬레이션 값)으로 **그대로** 설정한다(보간·지연·감쇠 없음). 카메라 orthographic size는 `GameRules.CameraOrthographicSize`(9)로 부트스트랩에서 1회 설정한다. 플레이 중 아무 프레임에서나 `(camera.xy − player.Position)`의 크기가 0이다 (PM).
- TAC-F1-7 (2회차 신규, 3.0 F): `KeyboardInputSource`는 `Update`마다 키의 눌림 상태를 래치하되, 한 프레임 안에 눌렸다 떼어진 키(`wasPressedThisFrame`)도 그 프레임 래치에 포함한다. `ReadMove`는 래치를 읽기만 하고 지우지 않는다. 래치는 다음 `Update`에서 다시 계산된다(프레임 안의 스텝 1~3회가 같은 입력을 본다) [실측 U17].

### F2. 자동 공격

- TAC-F2-1: 시작 시 `PlayerStats.startingWeaponId`(기본 "W1") 무기를 1단계로 보유한다.
- TAC-F2-2: 무기 쿨다운은 "다음 발사 허용 스텝"(`_nextFireStep`)으로 관리한다. 발사 조건은 `step ≥ _nextFireStep && Rules.WeaponsEnabled && 타깃 있음`이고, 발사하면 `_nextFireStep = step + 유효 쿨다운 스텝`이다. 타깃이 없으면 발사하지 않고 `_nextFireStep`도 그대로 두어 다음 스텝에 다시 시도한다 (EM).
- TAC-F2-3: 유효 쿨다운 = `StatMath.ApplyAttackSpeed(baseCooldownSteps, P1 보너스)` (5a R13) (EM).
- TAC-F2-4: 무기 코드는 `ITargetProvider`(`StepContext.Targets`)만 호출한다. Simulation asmdef에 Strategies 참조가 없으므로 구현 클래스를 직접 참조하면 컴파일되지 않는다.
- TAC-F2-5: 공격 on/off는 `SimulationRules.WeaponsEnabled` 값으로만 제어한다.
- TAC-F2-6 (2회차 신규, 3.0 I): 무기 피해는 일회성(W1·W4: TAC-F6-9) 또는 공격자 측 쌍별 쿨다운(W2·W3: TAC-F6-6)으로만 적용된다. 겹침이 지속되는 스텝에 이벤트가 없으므로 이벤트마다 피해를 주는 코드는 일회성 채널(0)에만 있다 (EM).

### F3. 몬스터

- TAC-F3-1: 행동 타입은 `chaser`(M1), `charger`(M2), `ranged`(M3), `boss`(M4) 4종이고 `Monster`가 `MonsterBehavior` 값으로 분기한다. 새 행동 타입은 코드 추가 대상이다(F11 경계).
- TAC-F3-2: 스폰 수 = `SpawnScheduler.SpawnsDueAtStep(rate, k)` = ⌊(k+1)·rate/60⌋ − ⌊k·rate/60⌋ (k = 구간 시작부터 센 스텝). 정수 초당 속도에서는 1분 스폰 수가 정확히 rate × 60이다(±10% 기준의 상위 보장) (EM).
- TAC-F3-3: 상한(`Rules.MonsterCap`, 기본 400)에 도달하면 그 스텝의 남은 스폰은 버린다. 누적 변수가 없으므로 몰아서 스폰할 수 없다. 상한 판정은 스폰 1기마다 `live < cap`이다 (EM).
- TAC-F3-4: 스폰 위치 = `SpawnGeometry.PointOnCircle(playerPos, GameRules.SpawnRadius, rng.NextFloat01())` 1회 (5a R7). 재추첨 코드가 없다 (EM).
- TAC-F3-5: 디스폰은 `Lifecycle` 단계(order 20)에서 보스를 제외한 몬스터의 `SpawnGeometry.IsBeyondDespawn`(R18)을 검사하고 `EntityLifecycle.Despawn(id, DespawnReason.OutOfRange)`로 회수한다. 이 경로는 `KillPipeline`을 거치지 않으므로 드랍·처치 수가 없다(R24) (EM).
- TAC-F3-6: 보스 스폰 스텝 = `GameRules.BossSpawnStep`(36000). 그 스텝의 `Lifecycle`(order 30)에서 일반 스폰을 끝내고 보스 1기를 상한과 무관하게 스폰한다. 위치는 일반 스폰과 같은 원주다 [기본값, D4].
- TAC-F3-7: M2 상태 기계는 `Approach → Charge(chargeDurationSteps 동안 고정 방향, chargeSpeed) → Cooldown(chargeCooldownSteps, 이 동안 moveSpeed로 추적) → Approach`다. 돌진 트리거는 `dist² ≤ chargeTriggerDistance²`이고 Approach 상태에서만 판정한다 (EM).
- TAC-F3-8: M3는 `dist² ≤ attackRange²`이면 이동을 멈추고, `fireCooldownSteps`마다 `SpawnEnemyProjectile` 명령을 넣는다. 직접 생성하지 않는다 (EM).
- TAC-F3-9: M4는 `moveSpeed`로 추적하며, 패턴 ①(`hazardZone`: 플레이어 위치에 `SpawnEnemyZone` 명령)과 ②(`summon`: 보스 주변 `summonRadius` 원주에 `SpawnMonsters` 명령, 각도는 Summon 스트림)를 각각 독립 쿨다운으로 쓴다. 소환은 상한 검사를 받는다 [기본값, D4] (EM).
- TAC-F3-10: 몬스터 생성·회수는 `EntityLifecycle`만 거친다(5a R11).
- TAC-F3-11 (2회차 신규): 몬스터의 추적·접근 판단은 `StepContext.PlayerPosition`만 읽는다. Monsters 폴더 코드에 `ITargetProvider` 참조가 없다(grep) (EM).
- TAC-F3-12 (2회차 신규, 3.0 I): 적 → 플레이어 피해는 `PlayerDamageReceiver`(5.12) 하나가 처리한다. 무적 스텝 수 = `PlayerStats.invulnerabilitySteps`(R1 `SecondsToSteps`). 한 스텝에 후보(겹친 몸통 접촉 적, 이번 스텝 Enter한 M3 투사체, 겹친 M4 장판)가 여럿이면 **최대 피해 1회**, 같으면 작은 `EntityId`다(몸통 접촉 외 종류까지 같은 규칙을 넓힌 것은 D32). 무적 중 M3 투사체 Enter는 피해 없이 `Despawn(Consumed)` (EM: 400기 겹침에서도 무적 스텝마다 정확히 1회).

### F4. 아이템 드랍과 획득

- TAC-F4-1 (2회차 수정): `KillPipeline`(Resolve order 30)이 이번 스텝 hp ≤ 0이 된 몬스터를 **`EntityId` 오름차순**으로 처리한다. 처치마다 처치 수 +1(R24), 처치 위치에 `SpawnGem` 명령 1개(경험치 = `PlayerStats.pickups.gemXpValue`), `Despawn(Killed)` 명령. 보스 처치는 드랍 없이 `BossKilledThisStep = true`만 세운다 [기본값].
- TAC-F4-2 (2회차 수정, 3회차 수정): 상자 판정 `rng(Drop).Chance(Rules.ChestDropChance)`(기본값 `GameRules.ChestDropChance = 0.02f`, 측정 중 0, 5.15)를 일반 몬스터 처치마다 정확히 1회, TAC-F4-1의 처리 순서대로 뽑는다(확률이 0이어도 뽑는다 — Drop 스트림 소비량을 일정하게 둔다). 참이면 `SpawnChest` 명령 (EM: 시드 고정 10,000회, 허용 오차 [기본값] ±0.4%p).
- TAC-F4-3: 젬은 `PlayerMagnetVsGem` Enter에서 끌림 상태가 되어 매 스텝(WeaponTick order 10) `gemAttractSpeed`로 플레이어를 향해 이동하고, `PlayerBodyVsGem` Enter에서 획득된다(채널 2개, D16). 끌림은 되돌리지 않는다(Exit 무시).
- TAC-F4-4: 상자 효과는 `ChestEffectResolver.Resolve(rng(Chest))`가 결정하고 `PlayerProgressionApplier`가 적용한다. ① `EvolutionService.TryFindFirstEligible()`(무기 슬롯 순서, Q42)가 있으면 진화하고 RNG를 뽑지 않는다. ② 없으면 `Chance(GameRules.ChestUpgradeChance = 0.70f)`가 참이면 강화, 거짓이면 회복이다. 강화 대상은 `ProgressionRules.IsWeaponUpgradable`(R22)이 참인 보유 무기를 슬롯 순서로 나열한 목록에서 `NextInt(count)`로 고른다 [기본값, D23]. 목록이 비면 회복으로 바꾼다 (EM).
- TAC-F4-5 (2회차 수정): 회복은 `ProgressionRules.ApplyHeal(hp, maxHp)`(R23)이다 (EM).
- TAC-F4-6 (2회차 신규): 같은 스텝에 상자 획득(회복)과 플레이어 피해가 겹치면 **회복이 먼저**다. 상자 효과는 Resolve order 0(라우터 디스패치)에서, 플레이어 피해는 order 20에서 적용되기 때문이다(5c.3 #9) (EM).
- TAC-F4-7 (2회차 신규, SPEC F4 4회차): ②의 70:30 비율이 시드 고정 1,000회에서 ±3%p 이내다 (EM).
- TAC-F4-8 (2회차 신규): 젬·상자에는 디스폰 경로가 없다(`DespawnSystem`은 몬스터만 본다). 활성 젬 수 = `PickupRegistry.ActiveGemCount`이며 통계·CSV에 들어간다(5.15.3) (EM).

### F5. 레벨업과 선택지

- TAC-F5-1: 후보 생성 `LevelUpCandidateBuilder.Build(...)`는 후보를 **정규 순서**(5a R12)로 반환한다. 비복원 추출은 `WeightedSampler.SampleWithoutReplacement`(5a R9)이고 RNG는 `LevelUp` 스트림이다 (EM).
- TAC-F5-2: 회복 후보 가중치 = `HealWeight.Compute(wBase, hp, maxHp)`(5a R8). 0이면 후보에서 뺀다. 추출 뒤 3칸에 못 미치면 남은 칸을 `Heal` 채움 항목으로 채운다(만체력이어도) (EM).
- TAC-F5-3: 제외 규칙은 다음과 같다. 새 무기는 무기 슬롯 4개가 차면 제외, 새 패시브는 패시브 3개가 차면 제외, `ProgressionRules.IsWeaponUpgradable`/`IsPassiveUpgradable`(R22)이 거짓인 항목의 강화는 제외, `evolvedOnly` 무기는 항상 제외, 진화로 소모된 원본 무기의 "새 획득"도 제외한다 [기본값, D5] (EM).
- TAC-F5-4 (2회차 재작성, A4): 레벨업 처리 순서는 다음과 같다.
  1. 스텝 s의 `StepOutcomeGate`(Lifecycle order 90)가 사망·클리어가 아니고 `Rules.LevelUpEnabled`이면 `ExperienceSystem.TryBeginLevelUp(curve)`를 1회 호출한다. 참이면(경험치에서 임계값을 빼고 레벨 +1) `CloseAfterCurrentStep(LevelUp)`.
  2. FLOW가 다음 `Update`에서 닫힘을 보고 `LevelUpSession.Open`으로 회차를 연다. 후보는 **그 시점의 상태**로 뽑는다(게이트가 닫혀 상태가 고정되어 있고 LevelUp 스트림은 이 경로만 쓰므로 프레임 타이밍과 무관하게 결정적이다).
  3. 선택이 수락되면(TAC-F5-5) **스텝을 돌리지 않고 즉시** `PlayerProgressionApplier.ApplyLevelUpOption`을 호출한다.
  4. 이어서 `TryBeginLevelUp`을 다시 호출한다. 참이면 **같은 `Update` 안에서 두 번째 회차를 연다**(스텝 없음, 후보는 첫 선택 적용 **후** 상태로 뽑는다). 거짓이면 `clock.Open()`으로 재개한다.
  젬 대량 획득으로 여러 레벨분 경험치가 쌓여도 회차는 하나씩 순서대로 열린다(SPEC F5) (EM: `ExperienceSystem` / PM: 연속 3레벨 시나리오에서 회차 ID 1·2·3, 스텝 수 불변).
- TAC-F5-5 (2회차 재작성, A5): 선택 입력(키 1/2/3, 버튼 클릭, 포커스 버튼의 Enter)은 모두 `LevelUpSession.TrySubmit(roundId, index, frame)`(R29)을 부른다. 회차당 **처음 수락된 한 번만** 유효하고, 같은 회차의 두 번째 입력과 지난 회차 ID를 가진 입력은 거짓을 받는다. 같은 프레임에 키와 클릭이 함께 오면 먼저 처리된 하나만 유효하다(처리 순서는 보장되지 않음, U42). 새 회차는 **열린 프레임에는 입력을 받지 않는다**(`frame > OpenedFrame`) — 한 번의 키 입력이 연속 회차 두 개를 고르지 않게 하기 위해서다 (EM + PM).
- TAC-F5-6: `Rules.LevelUpEnabled == false`이면 경험치만 쌓이고 레벨·선택지는 생기지 않는다 [기본값, D19].
- TAC-F5-7 (2회차 신규, SPEC F5·F8 4회차): 레벨업 화면에서 ESC → `FlowState.Paused`(복귀 상태 = LevelUp). 일시정지 중 선택 입력은 무시한다(`TrySubmit`을 부르지 않는다). 재개하면 같은 `RoundId`와 같은 3개 선택지로 레벨업 화면이 돌아오고 추첨은 다시 하지 않는다. 일시정지에서 "타이틀로 나가기"는 판을 리셋하고 기록을 남기지 않는다 (PM).
- TAC-F5-8 (2회차 신규, 사망 > 클리어 > 레벨업): 같은 스텝에 사망 또는 보스 처치가 있으면 `TryBeginLevelUp`을 호출하지 않는다(쌓인 경험치는 판과 함께 버린다) (EM).

### F6. 무기 시스템

- TAC-F6-1 (2회차 수정): 무기 인스턴스는 `behavior` 값으로 `WeaponFactory`가 만든다. `projectile`(W1, E1)·`homing`(W4) → `ProjectileWeapon`(homing이면 투사체가 재조준 모드), `orbit`(W2, E2) → `OrbitWeapon`, `zone`(W3) → `ZoneWeapon`. W4를 나타내는 방법은 `behavior: "homing"` 하나뿐이다(불리언 플래그 없음).
- TAC-F6-2: W1 방향 = `MoveMath.DirectionOrDefault(target − playerPos)`(영벡터면 `Vector2.right`). 발사 후 방향은 바뀌지 않는다. `projectileCount > 1`이면 `spreadDegrees`를 균등 분할해 같은 스텝에 명령을 여러 개 넣는다 (EM).
- TAC-F6-3: W4 투사체는 `ProjectileTick`에서 매 스텝 `Targets.TryFindNearest(자기 위치)`로 방향을 다시 잡는다. 대상이 없으면 마지막 방향으로 직진하고 수명이 끝나면 소멸한다 [Q9]. W1·W4 모두 사거리 없이 `lifetimeSteps`로 소멸한다 [Q9].
- TAC-F6-4: W2 궤도 원 위치 = `playerPos + R·(cos θ_i, sin θ_i)`, `θ_i = θ0 + ω·step + 2π·i/orbCount`. `OrbitWeapon`이 WeaponTick에서 자기 궤도 원들의 위치를 스텝 번호로 바로 계산해 쓴다(누적 오차 없음) (EM). (5회차) 궤도 원은 무기를 보유하는 동안 상시 존재하며 수명·지속시간·재생성 쿨다운이 없다: `SpawnPlayerOrb` 명령을 처리하는 `PlayerSpawnCommandHandler`는 `EntityLifecycle.Spawn<T>`에 `lifetimeSteps = 0`(수명 없음)을 넘겨 `ExpireStep = long.MaxValue`가 되고(5.10), `LifetimeSystem`은 이를 회수하지 않는다. 회수 경로는 `orbCount` 감소 초과분(TAC-F6-8), 진화 교체(TAC-F7-2), 런 종료·재시작(`DespawnAll(RunReset)`) 셋뿐이다. 핫 리로드는 보유 인스턴스에 적용되지 않아(5.11.1) 해당하지 않는다 (SPEC F6 6회차 신규) (EM).
- TAC-F6-5 (2회차 수정, Q46②): W3는 쿨다운이 됐을 때 `Targets.TryFindNearest(playerPos)`가 있으면 그 위치로 `SpawnPlayerZone` 명령을 넣고 쿨다운을 소비한다. 없으면 명령도 쿨다운 소비도 없고 다음 스텝에 다시 시도한다. 장판은 생성 스텝부터 `durationSteps` 뒤 Lifecycle의 수명 만료(order 10)에서 회수한다.
- TAC-F6-6: W2·W3 지속 피해는 무기 인스턴스가 소유한 `ContactDamageTracker`(5.12)가 처리한다. Enter 시 허용 스텝이면 즉시 1회, 겹침이 이어지면 `hitIntervalSteps`마다 1회, Exit 뒤에도 허용 스텝까지 기록을 유지한다. 키는 대상 `EntityId`(세대 포함)다. 같은 무기의 장판 여러 개가 같은 적에 겹쳐도 그 무기의 기록 하나로 센다 (EM).
- TAC-F6-7: 강화 값은 `levels[level-1]` 전체 파라미터 집합을 그대로 쓴다(절대값). 최대 단계 = `levels.Count`(기본 무기 5, 진화 무기 1).
- TAC-F6-8 (2회차 신규, A7): 무기 발사 결과물(W1·W4·E1 투사체, W3 장판, W2·E2 궤도 원)은 전부 WeaponTick에서 **명령으로** 적재되고 같은 스텝 Lifecycle(order 0)에서 생성된다. 생성 위치는 발사 스텝의 값이다. 그래서 W3의 첫 피해 스텝은 "발사 스텝 + 1"로 두 충돌 전략·두 틱 전략에서 같다. W2 궤도 원 수가 `orbCount`와 다르면(획득·강화·진화 직후) `OrbitWeapon`이 WeaponTick에서 모자란 만큼 `SpawnPlayerOrb`, 남는 만큼(인덱스 큰 것부터) `Despawn(Replaced)`를 넣는다 (EM + PM).
- TAC-F6-9 (2회차 신규, 3.0 I): W1·W4 투사체는 채널 0 Enter 1회에 피해를 주고 `Consumed` 표시 후 `Despawn` 명령을 넣는다. 같은 스텝에 같은 투사체의 다른 Enter는 `Consumed`라서 무시한다. 관통(`pierceCount > 0`)이면 "이미 맞힌 ID 목록"(세대 포함 `EntityId`)으로 재피해를 막고 `pierceCount + 1`회째 명중에서 소모된다 (EM).

### F7. 조합(진화)

- TAC-F7-1: 자격 조건 = `weapon.Level == weapon.Definition.Levels.Count && passives.Owns(rule.passiveId) && !weapon.Definition.EvolvedOnly`. `TryFindFirstEligible()`은 무기 슬롯 순서로 훑어 첫 자격 규칙을 반환한다 (EM).
- TAC-F7-2 (2회차 수정): 진화는 `PlayerProgressionApplier.ApplyEvolution`이 같은 슬롯 인덱스에서 원본 인스턴스를 결과 무기 1단계 인스턴스로 교체한다. 원본의 활성 자식 엔티티(궤도 원·장판)는 `Despawn(Replaced)` 명령으로 같은 스텝 Lifecycle에서 회수하고, 결과 무기의 자식은 다음 스텝 WeaponTick부터 생성된다(TAC-F6-8). 슬롯 수는 바뀌지 않는다 (EM).
- TAC-F7-3: 조합 규칙 파일이 없으면 `GameData.Combinations`가 빈 목록이 되어 `TryFindFirstEligible()`은 항상 없음을 반환한다.

### F8. 스테이지와 게임 종료 흐름

- TAC-F8-1: 씬은 `Main.unity` 하나다. 타이틀·플레이·일시정지·레벨업·결과·데이터 오류는 `GameFlowController`의 상태(`FlowState`)이고 UI 패널 전환으로 표현한다. 판 초기화는 `RunSession.Reset()` 한 곳에서 한다.
- TAC-F8-2 (2회차 수정, 3회차 수정): `RunSession.Reset()`은 다음 순서로 실행한다. ① `EntityLifecycle.DespawnAll(RunReset)`(현재 전략으로 회수) → ② `IPoolStrategy.ReleaseAll()` · `MonsterSpatialIndex.Clear()`(공간 해시) · `ICollisionStrategy.Clear()` · 틱 등록 해제(`ITickStrategy.Clear()`) → ③ 다음 판 전략 선택 적용(App이 새 풀·틱·충돌 전략을 만들어 `EntityLifecycle.ReplaceStrategies`, `CollisionEventRouter.ReplaceStrategy`, `PhysicsBodySynchronizer.ApplyRequirements`) → ④ 나머지 초기화: `ContactDamageTracker`·`PlayerDamageReceiver`·`LevelUpSession`·`SimulationCommandBuffer`·`PendingCommandQueue` 비우기 → `EntityIdAllocator.Reset()` → `RngStreams` 재시드 → 입력 소스·스폰 정책 바인딩(5.15 `IDevModeHost`) → `SimulationClock.Reset()`(게이트 닫힘) → 플레이어 재생성(1:1, 자석 2:1). 전략 교체가 회수보다 앞서면 옛 전략에 묶인 객체가 새 전략으로 반납되므로 이 순서를 바꾸지 않는다(5.10 `ReplaceStrategies` 전제). 재시작과 "타이틀로 나가기" 모두 이 함수를 쓴다 (PM).
- TAC-F8-3 (2회차 수정): 생존 시간은 스텝 수(`long`)다. 사망하면 사망 스텝까지의 완료 스텝 수, 클리어하면 보스 처치 스텝까지의 완료 스텝 수다(Q12). 판 종료(사망·클리어) 시 `Rules.RecordBestTime && survival > best`이면 `PlayerPrefs.SetInt("BestSurvivalSteps", ...)` 직후 **`PlayerPrefs.Save()`** 를 호출한다. 개발자 모드가 붙으면 `RecordBestTime = false`라 저장하지 않는다(SPEC F8 4회차). "타이틀로 나가기"는 판 종료가 아니라 저장 경로를 타지 않는다. 기록이 없으면 결과 화면에 "-"를 표시한다 (PM: `--dev` 실행 후 키 값 불변).
- TAC-F8-4: 데이터 검증 결과가 `IsPlayable == false`이면 `FlowState.DataError`가 되어 시작 버튼이 비활성화되고, 화면에 `DataValidationReport.BlockingIssues`를 파일명·정의 ID·필드와 함께 표시한다.
- TAC-F8-5 (2회차 재작성, A13): 시작 시 `DisplaySetup.Apply()`(IMP-FLOW, `Screen.SetResolution`의 유일한 호출자)가 `WindowSizeSelector.Choose(workArea)`(5.18)로 1920x1080 → 1600x900 → 1280x720 중 작업 영역에 들어가는 가장 큰 후보를 고르고, 없으면 1280x720을 고른다(잘림 허용). `Screen.SetResolution(w, h, FullScreenMode.Windowed)`. 실행 중 다시 바꾸지 않는다 (EM: 선택 표 / PM, 실측 U35·U24).
- TAC-F8-6 (2회차 신규): Alt+Enter로 전체화면이 되지 않는다(Player Settings "Allow Fullscreen Switch" 끔, 5b) [실측 U36]. 설정 UI가 없다.
- TAC-F8-7 (2회차 신규): UI는 uGUI `Canvas`(Screen Space - Overlay) + `CanvasScaler`(Scale With Screen Size, 기준 1920x1080, Match 0.5)다(D25). 세 해상도에서 패널이 잘리거나 겹치지 않는다 (PM: 세 해상도 스크린샷).
- TAC-F8-8 (2회차 신규): 같은 스텝의 종료/전환 우선순위는 `StepOutcomeGate`가 **사망 > 클리어 > 레벨업** 순서로 한 번만 판정한다. 보스 처치와 플레이어 사망이 같은 스텝이면 게임 오버다 (EM).
- TAC-F8-9 (2회차 신규): `GameFlowController.OnApplicationFocus(false)`이고 `FlowState == Playing`이며 `Rules.AutoPauseOnFocusLoss`이면 일시정지 메뉴를 연다. 포커스가 돌아와도 자동 재개하지 않는다 [기본값]. 개발자 모드 측정 중에는 `MeasurementSession`이 `AutoPauseOnFocusLoss = false`, `Application.runInBackground = true`로 바꾸고 끝나면 되돌린다 [실측 U37].
- TAC-F8-10 (2회차 신규, D6): 스폰 테이블의 `bossMonsterId`가 무효면 Warning만 남기고 시작을 허용한다. 이때 36000 스텝에 일반 스폰은 끝나지만 보스가 나오지 않아 **클리어 경로가 없다**(소프트락). 판은 사망이나 "타이틀로 나가기"로만 끝난다. 데이터 오류 화면은 뜨지 않고, 경고 로그에 "보스 정의가 없어 클리어할 수 없다"를 남긴다 [기본값].
- TAC-F8-11 (2회차, F1에서 이동): 반복 배경 `BackgroundTiler`(IMP-FLOW)는 `LateUpdate`(실행 순서 100, `CameraFollow` 뒤)에서 카메라 위치를 셀 크기 배수로 스냅해 따라가는 Tiled `SpriteRenderer`다 [실측 U29].
- TAC-F8-12 (2회차 신규): 타이틀·일시정지·결과 메뉴는 uGUI `Button`과 `EventSystem` 탐색(방향키, Enter = Submit)으로 키보드·마우스 모두 같은 콜백을 부른다. ESC는 `UiInputHandler.Update`가 처리한다 [실측 U42] (PM).
- TAC-F8-13 (2회차 신규): 보스전에 시간 제한 코드가 없다.

### F9. 오브젝트 풀링

- TAC-F9-1: 게임 코드는 `IPoolStrategy`만 참조하고, 그것도 `EntityLifecycle` 한 곳에서만 호출한다.
- TAC-F9-2: 구현 A(`CustomPoolStrategy`)는 템플릿별 비활성 스택(LIFO)이다. 비면 `Instantiate`로 확장하고 상한은 없다 [Q13]. 사전 생성 개수는 `PoolPrewarmCounts` 코드 상수다 [Q13, 측정 후 조정] (EM: 반환 후 재요청 시 같은 인스턴스).
- TAC-F9-3: 구현 B는 `Acquire`마다 `Instantiate`, `Release`마다 `SetActive(false)` 후 `Destroy`다 [실측 U22] (PM).
- TAC-F9-4: 구현 C는 템플릿별 `UnityEngine.Pool.ObjectPool<GameObject>`이고 `collectionCheck: true`, `maxSize: int.MaxValue`다 [실측 U21].
- TAC-F9-5: 중복 반환이면 세 구현 모두 `InvalidOperationException`을 던지고, 그 전에 `Debug.LogError`로 객체 이름과 ID를 남긴다. 정상 경로에서는 `EntityLifecycle`이 `EntityIdAllocator.IsLive`로 미리 막는다 (EM).
- TAC-F9-6: 풀은 상태를 초기화하지 않는다. 초기화는 `EntityLifecycle`이 엔티티의 초기화 콜백을 호출해서 한다(풀 전략과 무관하게 같은 초기화가 보장된다).
- TAC-F9-7 (2회차 신규): 템플릿에는 전략과 무관하게 같은 컴포넌트 묶음이 붙는다(풀·충돌·셀프 틱 데코레이터가 전부 적용됨, 5.6). 전략에 따라 켜고 끄기만 다르다. 풀 전략 토글은 다음 판 `Reset`에서 적용한다.

### F10. 성능 측정 (측정 훅)

- TAC-F10-1 (2회차 수정, B): 시스템 ms는 `ProfilerMarkers`(5a R16)의 마커를 `ProfilerRecorder`로 읽은 **프레임 합계를 세션 동안 누적**해 구한다. 스텝당 ms = 세션 합계 / 세션의 전체 스텝 수, 프레임당 ms = 세션 합계 / 세션의 전체 프레임 수(R27). 프레임별 비율을 평균하지 않는다 [실측 U14] (EM: R27 표 테스트).
- TAC-F10-2: 틱 마커는 두 모드 모두 드라이버 경계에서 연다(`Early` 끝에서 Begin, `Mid` 시작에서 End). 그래서 F17 A의 Unity 호출 오버헤드까지 들어간다 [실측 U14].
- TAC-F10-3 (2회차 수정, A1): 측정 파일의 헤더·본문 항목은 SPEC F10·F19 4회차 목록과 1:1이다(5.15.3 대응표). 본문에는 6개 항목(항목 4는 A0 외 "해당 없음", 항목 5는 스텝당·프레임당, 항목 6은 평균·최댓값)과 활성 젬 수가 있다 (PM: 열 이름·개수 검사).
- TAC-F10-4 (2회차 신규, 3회차 수정): `MeasurementPresets`(Dev)가 A 6회(전부 끔/단독 4/전부 켬), B 8회(N 50·100·200·400 × 전수/공간 해시), C 2회(엔진 대안 하나 + 나머지 기준선)의 `StrategySelection`을 상수로 제공한다. 각 프리셋은 고정 무기 구성(W1~W4 단계)과 `ChestDropChance = 0`을 함께 가진다(3회차). D는 R26으로 자동 설정된다 (EM).

### F11. 데이터 기반 설계

- TAC-F11-1: 로더는 `Path.Combine(Application.streamingAssetsPath, "Data", <파일명>)`을 읽는다 [실측 U13]. 에디터 전용 API(`AssetDatabase`)를 쓰지 않는다.
- TAC-F11-2: 검증 결과는 `DataValidationReport` 하나에 모이며, 항목마다 `Severity`, `FileName`, `DefinitionId`, `FieldPath`, `Message`, (파싱 오류면) `Line`·`Column`을 갖는다. 모든 항목은 `Debug.LogWarning`/`LogError`로 한 줄씩 남는다 (EM: 파일 종류마다 실패 픽스처).
- TAC-F11-3 (2회차 수정): 초 단위 필드는 로드 시 R1의 적용 대상 표대로 환산해 정의 객체에 스텝 값으로 저장한다. 소비자는 스텝 값만 읽는다. 원본 초 값도 덤프용으로 함께 보존한다.
- TAC-F11-4: 터널링 불변식 검증 `TunnelingInvariantValidator`는 1단에서 이동 객체마다 `v/60 ≤ r`를 검사하고, 실패한 객체만 2단에서 실제 상대 레이어(5a R5 채널 표 기준)와 `(v_a+v_b)/60 ≤ r_a+r_b`를 검사해 쌍을 지목하는 경고를 남긴다. 차단하지 않는다 (EM, E3).
- TAC-F11-5 (2회차 수정, B): 핫 리로드의 결과가 플레이 불가면 기존 데이터를 유지하고 오류만 표시한다. 가능하면 `SwapData` 대기 명령으로 다음 스텝 `Begin`에서 `GameDataRegistry.TrySwap`이 일어나고, 파일 종류별 적용 시점은 5.11.1 표를 따른다(활성 객체는 기존 정의 유지, 현재 체력·최대 체력 유지) [Q43 확장].
- TAC-F11-6 (2회차 신규, SPEC F11 4회차): 파싱은 Newtonsoft `JToken.Parse`가 맡고 바인딩·검증은 직접 쓴 `Binders/*`가 맡는다. 파일은 `File.ReadAllText(path, new UTF8Encoding(false, false))`로 읽고, 결과 텍스트에 U+FFFD가 있으면 `Warning "인코딩 오류 가능(UTF-8이 아닌 파일일 수 있습니다)"`를 파일명과 함께 남긴 뒤 계속 진행한다 [실측 U41] (EM: CP949 바이트 픽스처).
- TAC-F11-7 (2회차 신규): 게임이 쓰는 CSV(측정 파일, `summary.csv`, 충돌 로그)는 전부 `CsvWriter`(R28)로 쓴다. UTF-8 BOM, 숫자는 `CultureInfo.InvariantCulture`, 쉼표·따옴표·줄바꿈이 든 값은 RFC 4180 따옴표 처리 (EM: `tr-TR`·`de-DE` 현재 문화권에서도 소수점이 점).
- TAC-F11-8 (2회차 신규): `playerStats.invulnerabilitySeconds`와 무기 `hitIntervalSeconds`는 같은 R1 함수 계열로 환산된다(무적은 0 허용 `SecondsToSteps`, 틱 간격은 최소 1 `SecondsToIntervalSteps`). 무적 필드 누락·오류는 플레이어 기본 스탯 오류(Blocking)다 (EM).

### F12·F14·F15 (문서/사용자 수행)

- TAC-F12-1 (2회차 수정): README의 클래스 다이어그램은 **5.0.1 인터페이스 전체 목록과 그 구현 클래스**를 기준으로 그린다(SPEC F12 4회차 일치 판정 기준). 흐름도는 2.2(c)의 스텝 단계 순서를 기준으로 그린다.
- TAC-F14-1 (2회차 신규, 3회차 수정): SPEC F14 기능별 필수 기록 중 구현자가 초안을 내야 하는 것은 5c.2a 표에 있다. 초안은 각 구현자의 완료 보고에 담기고, 소유자(doc-writer·사용자)가 **해당 기능 브랜치 머지 전에** README/devlog에 반영해 **같은 머지 커밋에 포함**한다(SPEC F14 "기능 브랜치 머지 커밋에 해당 devlog/README 변경이 포함된다").
- TAC-F15-1: 폴더 구조는 5c.1을 따른다. `docs/devlog.md` 틀은 doc-writer가 만든다.

### F13. 빌드 (구현 부분)

- TAC-F13-1 (2회차 수정): 개발자 모드 코드는 Development Build·릴리스 빌드 모두에 포함된다(Q40). 두 빌드의 차이는 Unity 빌드 옵션뿐이다. 시스템 ms·GC·메모리 항목은 Development Build에서만 값이 나오고, 릴리스 빌드에서는 `NA`로 기록된다 [실측 U14·U15·U38]. F19 비교용 "코드 제거 빌드"는 스크립팅 심볼 `SURVIVOR_NO_DEV`를 넣은 빌드다(5c.1) [실측 U40].
- TAC-F13-2: `StreamingAssets/Data/*.json` 7개가 빌드 폴더에 원본 그대로 복사된다 [실측 U13]. 에셋 키 레지스트리는 `Assets/Resources/AssetKeyRegistry.asset`(빌드에 포함)이다.

### F16. 타깃 탐색 전략

- TAC-F16-1: 세 구현 모두 `BuildIndex` 단계에서 받은 `MonsterSnapshotBuffer`의 위치로 거리를 계산하고, `TargetOrdering.IsBetter`(5a R6)로 고른다. 그래서 반환이 비트 단위로 같다 (EM: 무작위 배치 10,000건 + 동거리 배치 + 큰 몬스터 포함 배치에서 A=B 비교 / PM: A=B=C).
- TAC-F16-2: 공간 해시 셀 키 = `SpatialHash.CellOf(pos, cellSize)`(5a R14). 고리(ring) 탐색 종료 조건은 "고리 k를 끝냈고 `bestD2 ≤ (k·cellSize)²`" 또는 "점유 셀 범위를 벗어남"이다. 그 뒤 큰 몬스터 리스트를 전수 검사한다 (EM).
- TAC-F16-3 (2회차 재작성, A6): `Physics2DTargetProvider`가 켜져 있으면 BuildIndex order 5에서 `PhysicsBodySynchronizer.SyncMonstersForQuery()`가 모든 몬스터 바디의 `Rigidbody2D.position`을 스냅샷 위치로 직접 대입하고 `Physics2D.SyncTransforms()`를 1회 호출한다. 그래서 질의 시점의 물리 위치 = 스냅샷 위치다. 질의는 `Physics2D.OverlapCircle(point, R, filter{Monster 레이어, useTriggers}, results)`를 반경 4부터 두 배씩 넓히되 상한은 "질의점에서 스냅샷 AABB의 가장 먼 꼭짓점까지 거리"다. 후보가 생기면 `R = √bestD2 + 1e-3`으로 한 번 더 질의하고, 최종 선택은 스냅샷 위치와 R6으로 한다(콜라이더 겹침 판정은 중심 거리 ≤ R인 몬스터를 모두 포함하는 상위 집합이다) [실측 U11·U32].
- TAC-F16-4: 공간 해시 셀 크기는 `SpatialHashSettings.CellSize`(기본 [기본값] 2.0) 코드 상수이며, 개발자 모드에서 다음 판부터 바꿀 수 있고 측정 헤더 `cellSize`에 기록된다.
- TAC-F16-5 (2회차 수정): 타깃 공급자는 판 진행 중에 `SwitchTargetProvider` 대기 명령으로 바꾼다. 다음 스텝 `Begin`에서 `TargetProviderSlot`이 교체되고 `MonsterSpatialIndex.Required`와 `PhysicsBodySynchronizer.ApplyRequirements(R19)`가 다시 계산된다(Physics2D로 바꾸면 활성 몬스터 콜라이더를 ID 순으로 켠다). 나머지 3개 전략은 다음 판 `Reset`에서 적용한다(SPEC 3.0 B).
- TAC-F16-6 (2회차 신규, B, 3회차 수정): 공간 해시에서 빠지는 "큰 몬스터"는 `MonsterSizeRules.IsLarge(radius, isBoss)`(R25, `radius > GameRules.LargeMonsterRadiusThreshold ∥ behavior == boss`)로 정한다. 보스는 반지름과 무관하게 항상 큰 몬스터다(SPEC F16 "보스는 공간 해시에 들어가지 않는다"). JSON으로 반지름이 큰 일반 몬스터를 추가해도 같은 경로를 탄다 (EM).

### F17. Update 집중화

- TAC-F17-1: `CentralTickStrategy`는 그룹별 `List<IUpdatable>`을 등록 순서로 순회한다. 순회 중 해제되면 슬롯을 null로 표시하고 순회가 끝난 뒤 압축한다. 순회 중 등록은 대기 목록에 넣었다가 순회가 끝난 뒤 붙인다 (EM). (2회차) 생성이 Lifecycle로 옮겨져 순회 중 등록은 정상 경로에서는 일어나지 않는다. 방어 코드로 남긴다.
- TAC-F17-2 (2회차 수정): `SelfTickStrategy.Register`는 호스트의 `MonsterSelfTicker`/`ProjectileSelfTicker`에 `IUpdatable`과 `IStepClockReader`를 연결하고 `enabled = true`로, `Unregister`는 `false`로 바꾼다. 셀프 티커는 `clock.StepActive`가 거짓이면 아무것도 하지 않는다 [실측 U31] (PM).
- TAC-F17-3: 플레이어·무기·궤도 원·장판·젬·UI·매니저는 `ITickStrategy`에 등록하지 않는다. 각자 해당 단계 핸들러가 직접 갱신한다.

### F18. 충돌 판정

- TAC-F18-1: 채널 9개(5a R5)만 판정한다. 엔진판은 레이어 매트릭스(R30)로, 자체판은 채널 표로 같은 쌍만 본다 [실측 U12].
- TAC-F18-2: 자체판은 채널마다 이번 스텝 겹침 쌍 집합을 계산해 이전 스텝 집합과 비교하고, 새로 겹친 쌍은 Enter, 사라진 쌍은 Exit으로 낸다. 겹침이 지속되는 스텝에는 이벤트가 없다. 겹침 판정은 `CircleMath.Overlaps`(`≤`)다 (EM).
- TAC-F18-3: 자체판의 후보 추림: `PlayerProjectile`·`PlayerOrb`·`PlayerZone` → Monster는 `MonsterSpatialIndex.QueryCircle` + 큰 몬스터 리스트, `PlayerMagnet`·`PlayerBody` → Gem·Chest는 자체 소유 `SpatialHash` 인스턴스(픽업 전용), `EnemyProjectile`·`EnemyZone` → PlayerBody와 `PlayerBody` → Monster는 플레이어 1개 기준 선형 또는 해시 질의다 (D15).
- TAC-F18-4 (2회차 재작성, A6): 엔진판 `CollectEvents(step)`는 ① `PhysicsBodySynchronizer.SyncAllForSimulation()`(등록 바디 전부를 ID 순으로 `MovePosition(body.Position)`, 반지름이 바뀐 바디는 `CircleCollider2D.radius` 갱신) ② `Physics2D.Simulate(StepMath.StepSeconds)` ③ 콜백 큐를 ID로 바꾸고 정렬해 반환한다. 새로 등록된 바디는 `Register` 때 `Rigidbody2D.position`과 `transform.position`을 바로 대입한다(풀에서 나온 객체가 옛 위치에서 스윕되지 않게) [실측 U5·U7·U10].
- TAC-F18-5: 등록 해제(회수) 시 두 전략 모두 Exit을 내지 않는다. 자체판은 그 바디의 쌍을 조용히 지우고, 엔진판은 `Physics2D.callbacksOnDisable = false`에 더해 "등록되지 않은 바디의 콜백은 버림" 필터를 둔다 [실측 U8]. 이 때문에 겹침 기록 소비자는 TAC-0-10대로 `Despawned`에서 기록을 지운다.
- TAC-F18-6 (2회차 수정, A7): 생성은 Lifecycle에서만 일어나므로 스텝 s에 생성된 객체는 s+1의 `Collision`부터 판정에 들어간다. 두 전략 모두 `Register` 직후의 첫 `CollectEvents`에서 이미 겹쳐 있으면 Enter를 낸다 (EM + PM).
- TAC-F18-7: E1·E2·E3의 실행 방법은 5c.5에 있다. (2회차) E1·E2는 타깃 공급자를 `Physics2DQuery`가 아닌 것으로 둔다(D30).
- TAC-F18-8 (2회차 신규, 4회차 수정): 이벤트 정렬·비교 키는 `(Step, Channel, A, B, Kind)` 하나다(`CollisionEvent.CompareTo`, SPEC 5회차 F18 "(채널, ID) 순 정렬"). `CollisionEvent.ToString()`과 충돌 로그 CSV 열(`step,channel,a,b,kind`)도 이 순서다. A·B는 `EntityId.ToString()`(`"인덱스:세대"`)이다.

### F19. 개발자 모드

- TAC-F19-1 (2회차 재작성): `DevModeEntry`(Dev)의 `[RuntimeInitializeOnLoadMethod(AfterSceneLoad)]` 메서드가 `DevModeActivation.IsRequested(Environment.GetCommandLineArgs())`를 정확히 1회 호출한다. 거짓이면 즉시 반환하고 `Survivor.Dev`의 어떤 `MonoBehaviour`도 만들지 않는다. 참이면 `GameBootstrap.Current`(App)의 `IDevModeHost`에 붙는다. App을 포함한 어떤 게임 어셈블리도 Dev를 참조하지 않는다 [실측 U16·U40] (EM: asmdef 참조 표 테스트 / PM: 인자 없는 실행에서 Dev 타입 인스턴스 0개).
- TAC-F19-2: 개발자 모드는 값과 객체를 주입하는 방식으로만 개입한다: `SimulationRules` 필드, `IInputSource` 교체(`AutoPathInputSource`), `ISpawnPolicy` 교체(`DevSpawnPolicy`), 다음 판 충돌 전략 데코레이터(`LoggingCollisionStrategy`), `StrategySelection`, `PendingCommandQueue` 명령, `Time.timeScale`.
- TAC-F19-3 (3회차 수정): 자동 종료는 `SimulationRules.StopAtStep`(기본 `long.MaxValue`)으로 한다. 시계는 End에서 `Step >= StopAtStep`이 되면 게이트를 닫는다(5.3과 같은 `>=` 판정). 개발자 모드의 `Update`가 이를 감지해 결과를 출력하고 `Application.Quit()`(에디터에서는 플레이 중지)를 호출한다 (PM: 정확히 N스텝).
- TAC-F19-4 (2회차 수정, B): 시각화는 `DevVisualizer`가 `RenderPipelineManager.endCameraRendering`에 등록한 콜백의 GL 선으로 그린다(URP라 `OnRenderObject` 대신) [실측 U33]. 시각화가 전부 꺼지면 콜백을 **해제**하고 컴포넌트를 `enabled = false`로 둔다(호출 비용 0).
- TAC-F19-5 (2회차 재작성, A9, 3회차 수정): `MeasurementSession.TryBegin(preset)`은 다음 중 하나라도 해당하면 시작하지 않고 원인을 오버레이에 표시한다: 시각화가 하나라도 켜짐, 충돌 이벤트 로그 옵션이 켜짐, `Time.timeScale != 1`, 한 스텝 진행 모드, (3회차) 프리셋에 고정 무기 구성이 없거나 `ChestDropChance != 0`. 통과하면 ① 오버레이를 숨긴다(`DevOverlay.enabled = false` — `OnGUI` 호출 0) ② 측정 환경을 적용·기록한다(TAC-F19-9) ③ **로그 데코레이터 없이** 프리셋으로 새 판을 시작한다(충돌 전략은 판 시작 때 정해지므로 데코레이터가 설치될 수 없다). 측정 중 오버레이를 다시 열거나(F1) 시각화·배속·로그를 바꾸거나 (3회차) 무기 구성이 바뀌면(개발자 `SetLoadout`·상자 효과·레벨업 적용) 세션은 "무효"로 끝나고 CSV에 쓰지 않는다 (PM).
- TAC-F19-6 (2회차 수정, A2): 배속은 `Time.timeScale` ∈ [0.25, 3](R20)이다. 오버레이에 "실제 배속" = 최근 1초(비스케일 시간) 동안 실행된 스텝 수 / 60을 표시한다. catch-up 상한 3은 그대로다 [실측 U23].
- TAC-F19-7 (2회차 신규, SPEC F10·F19 4회차): 워밍업 — `StatsCollector`는 활성 몬스터 수가 처음 N 이상이 된 스텝 `s_N`을 기록하고, `clock.Step ≥ s_N + GameRules.MeasurementWarmupSteps(300)`인 프레임부터 집계한다. 측정 길이(`durationSteps`)는 그 시점부터 센다. `StopAtStep = s_N + 300 + durationSteps`로 그때 설정한다 (PM).
- TAC-F19-8 (2회차 신규): 통계는 F10 6개 항목 + 활성 젬 수(평균·최댓값)를 모두 제공한다(5.15.3).
- TAC-F19-9 (2회차 신규): 측정 시작 시 `QualitySettings.vSyncCount = 0`, `Application.targetFrameRate = -1`을 설정하고, 스크립팅 백엔드(컴파일 심볼 `ENABLE_MONO`/`ENABLE_IL2CPP`)와 `GarbageCollector.isIncremental`을 읽어 헤더에 쓴다. 끝나면 이전 값으로 되돌린다 [실측 U39].
- TAC-F19-10 (2회차 신규): F1 키 감시는 `DevModeRoot.Update`에만 있고, `DevModeRoot`는 활성일 때만 존재한다.
- TAC-F19-11 (2회차 신규, SPEC F19 4회차): 개발자 모드 끔(인자 없음) 빌드와 `SURVIVOR_NO_DEV` 빌드의 F10 수치 비교 1회가 가능하다. 두 빌드 모두 같은 일반 플레이 경로이므로 비교는 외부 Profiler로 사용자가 한다(측정 세션은 Dev 기능이라 코드 제거 빌드에 없다) (사용자 수행).

---

