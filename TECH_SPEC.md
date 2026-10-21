# TECH_SPEC: 뱀서라이크 1차 MVP (5회차)

> 입력: `SPEC.md`(6회차, F1~F19, 수용 기준 329개), `CODING_GUIDE.md`, `RUN_LOG.md` 2026-10-12·2026-10-16 항목(Q40·Q41·D17·D3·Q24 수정 확정, 피해 세 갈래, 세대 ID, 교차 검토 결과와 PM 방향). RUN_LOG의 확정 결정은 SPEC.md보다 우선한다.
> 이 문서는 `SPEC.md`를 수정하지 않는다. 기능 범위는 SPEC.md 그대로이며, 범위에 영향을 줄 수 있는 판단은 6절에 적었다.
> 표기: **[기본값]** = PM이 정한 구현 단계 기본값이며 구현·밸런싱 때 바꿀 수 있다. **[실측]** = Unity 동작 전제이며 실행해 보기 전에는 확정할 수 없다(6.2절 번호). **(2회차)** = 2회차 개정에서 바뀌었거나 새로 생긴 곳. **(3회차)** = 3회차 개정에서 바뀐 곳. **(4회차)** = 4회차 개정에서 바뀐 곳. **(5회차)** = 5회차 개정에서 바뀐 곳.

---

## 0. 2회차 변경 요약 (2회차)

독립 검토자의 교차 점검 결과("수정 필요" 13건, "확인 권장" 중 PM 채택분)와 SPEC 4회차를 반영했다. 코드는 여전히 없고, Unity 동작 전제는 전부 [실측]이다.

**A. 수정 필요 13건**

| # | 내용 | 반영 위치 |
|---|---|---|
| A1 | 측정 CSV 열(활성 젬 수, `cellSize`, 무기 구성, 무적, 메모리 평균·최댓값, D, 측정 환경 4값, PC 사양은 헤더) | 5.15.3 (측정 파일 구조·열 대응표), TAC-F10-3, TAC-F19-8 |
| A2 | 배속 상한 3x, catch-up 3 유지, 실제 배속 표시 | 5.3 메모, 5.15.1, R20, TAC-F19-6, D9 해소 |
| A3 | 게이트는 스텝 안 Lifecycle 핸들러가 닫는다 | 2.2(c), 5.3(게이트 규칙), 5c.3 #9·#11, TAC-0-5·0-7 |
| A4 | 레벨업 선택은 게이트 밖 즉시 적용 → 연속 레벨업 재평가 → 적용 후 상태로 후보 생성 | 5.14, TAC-F5-4·F5-7, TAC-0-1 |
| A5 | 회차 ID당 첫 입력만 | 5.14 `LevelUpSession`, R29, TAC-F5-5 |
| A6 | 물리 바디 동기화 소유자 1개 | 2.2(b), 5.9.1 `PhysicsBodySynchronizer`, R19, TAC-F16-3·F18-4 |
| A7 | 생성 경로 Lifecycle 하나, 생성 스텝은 판정 불참 | 5.4, 5.10, TAC-0-8·F6-8·F18-6 |
| A8 | asmdef 참조 위반 해소(Core 추상 훅 + 조립 루트 주입) | 2.2(a), 5c.1 참조 방향 표·전수 점검표, 5.6·5.8 |
| A9 | 측정 중 배제(오버레이 숨김, 로그 데코레이터 미설치, 시각화 끔, 1x) + 워밍업 300스텝 | 5.15.2, TAC-F19-5·F19-7 |
| A10 | 공유 규칙 추가(강화 가능 판정, 회복 적용, 처치 수, 초→스텝 적용 대상), 5a 머리말 정정 | 5a R1(확장)·R22~R30, 5a 머리말 |
| A11 | 레벨업 선택 적용·개발자 무기 구성 처리 소유자 | 5.14 `PlayerProgressionApplier`(IMP-PLAYER), 5c.2 |
| A12 | 레이어 매트릭스 이중 소유 해소 | 5.3 메모(LOOP는 시간 설정만), 5.9.1 `PhysicsSettingsApplier`(COLLISION) |
| A13 | 창·빌드 설정(자동 해상도, Alt+Enter, Canvas Scaler, 포커스) | 5.18, 5b, TAC-F8-5~F8-9 |

**B. 확인 권장 중 PM 채택분**

| 항목 | 반영 위치 |
|---|---|
| 스텝당 ms = 세션 합 / 전체 스텝 수 | R27, TAC-F10-1 |
| `Physics2D.defaultContactOffset` U 항목 + 0에 가깝게 | U30, 5.9.1 `PhysicsSettingsApplier` |
| D6 보스 정의 무효 → 소프트락 결과 | TAC-F8-10, D6 |
| W4 표현 `behavior: "homing"` 하나로 | 5.13.1 예시, TAC-F6-1 |
| `IsLarge` = 반지름 임계값 | R25, 5.7, TAC-F16-6 |
| `BackgroundTiler` TAC 위치(F8로 이동) | TAC-F8-11, 3절 표 |
| 카메라 즉시 스냅 | TAC-F1-6 |
| 개발자 모드 판 최고 기록 미저장 + `PlayerPrefs.Save()` | TAC-F8-3 |
| 같은 스텝 상자 회복이 피해보다 먼저 | 5c.3 #9 Resolve 순서, TAC-F4-6 |
| JSON UTF-8·U+FFFD 경고 | TAC-F11-6, U41 |
| CSV UTF-8 BOM·InvariantCulture | R28, TAC-F11-7 |
| Universal 2D(URP) 템플릿과 시각화 영향 | 5b, U33·U34, TAC-F19-4 |
| 핫 리로드 시 `playerStats`·스폰 테이블 처리 | 5.11.1 표, TAC-F11-5 |

**C. SPEC 4회차 정합에서 찾아 고친 것**: 6.1 머리의 "C 목록"에 정리했다(UI 프레임워크, 인터페이스 전체 목록, 개발자 모드 의존 방향, 조용한 등록 해제 뒤 겹침 기록 잔존 버그 등 18건).

**D. 미확정 갱신**: 해소 12건(D3·D8·D9·D10·D11·D12·D13·D17·D18·D20·D22·D24), 신규 D25~D33(9건), 신규 U30~U42(13건), U19는 대체(U42). 집계는 6.3.

**3회차 변경 요약 (3회차)**: 2차 독립 검토 결과와 PM 결정을 반영한 소폭 개정이다. 다른 내용은 바꾸지 않았다.

| # | 내용 | 반영 위치 |
|---|---|---|
| 1 | SPEC F14 "기능 브랜치 머지 커밋에 devlog/README 변경 포함"에 맞춰 기록 초안을 머지 전에 소유자가 반영해 같은 머지 커밋에 넣는다. 웨이브 6은 최종 정리·다이어그램 검증으로 한정 | 3절 F12·F14 행, TAC-F14-1, 5c.2a, 5c.4 웨이브 6 |
| 2 | `RunSession.Reset` 순서를 `DespawnAll` → 풀·공간 해시·충돌 정리 → 전략 교체 → 나머지 초기화로 정정(5.10 `ReplaceStrategies` 전제와 일치) | TAC-F8-2, 5.7 `MonsterSpatialIndex.Clear` |
| 3 | `InputOverride`·`SpawnPolicyOverride`는 판 시작(`Reset`)에서 1회 바인딩(매 스텝 null 검사 없음) | 5.15 `IDevModeHost`, 5.15.2 |
| 4 | 큰 객체 판정 `IsLarge = radius > 1.0 ∥ behavior == boss` | R25, TAC-F16-6, 5.7·5.9 주석, 5.13.2 |
| 5 | 측정 중 상자 드랍 0%, 프리셋 무기 구성 측정 내내 고정 | 5.15 `SimulationRules.ChestDropChance`, 5.15.2, 5.15.3 헤더, TAC-F4-2, TAC-F10-4, TAC-F19-5 |
| 6 | D26 해소(PM 승인) | D26, 6.3 |
| 7 | End 단계의 `EndStep`은 드라이버 직접 호출로 표기, `StopAtStep` 판정을 `>=`로 통일 | 5c.3 #9 표, TAC-F19-3 |

**4회차 변경 요약 (4회차)**: doc-writer가 보고한 내부 불일치 3건만 고쳤다 — ① 충돌 이벤트 비교 키·정렬·로그 열 순서를 `CollisionEvent.CompareTo`의 `(Step, Channel, A, B, Kind)`로 통일(SPEC 5회차 F18 "(채널, ID) 순 정렬", 2.1·5.9·TAC-F18-8·5.15 충돌 로그·5c.5 E1·D8) ② `TargetProviderSlot`은 5.7 정의대로 `ITargetProvider`만 구현하며 단계 핸들러가 아니다 — BuildIndex 끝에 `SimulationDriverMid`가 직접 호출하도록 5c.3 #9를 고쳤다(5.7 메모) ③ 부팅 시 최초 JSON 로드 경로와 검증 호출 순서를 5.11.0에 추가했다(호출 주체 `GameBootstrap`은 IMP-FLOW, 로더는 IMP-DATA, `GameDataRegistry.Initialize` 추가, 5c.2 표기).

**5회차 변경 요약 (5회차)**: SPEC 6회차(W2 궤도 원 생애) 반영, TAC-F6-4 보강 — 궤도 원은 수명 없음(`lifetimeSteps = 0` → `ExpireStep = long.MaxValue` 규약을 5.10 `Spawn<T>` 주석에 확정), 회수 경로 셋(TAC-F6-8·TAC-F7-2·RunReset), `LifetimeSystem`은 수명 없는 엔티티를 건너뜀(5.4 메모·5c.3 표).

---

## 1. 개요 (Overview)

Unity 6000.3.26f1(Universal 2D(URP) 템플릿, C#)로 만드는 뱀서라이크 1차 MVP의 기술 설계다. 이 프로젝트의 핵심은 기능의 양이 아니라 **측정 가능한 구조**다. 풀링·타깃 탐색·Update 집중화·충돌 판정 네 가지를 전략 인터페이스 뒤로 분리하고, 1/60초 고정 스텝 위에서 **어느 전략 조합을 골라도 스텝 단위 게임 결과가 똑같이** 나오게 만든 다음, 개발자 모드에서 비용 차이만 잰다. 따라서 이 문서의 대부분은 기능 로직보다 **결정론 계약**에 할애한다. 스텝 안의 단계 순서, 게이트를 닫는 주체와 시점, 객체 ID 형식, 정렬·타이브레이크 규칙, 난수 스트림 분리, 생성·회수 경로가 그 내용이다. 하나라도 계층마다 다르게 구현되면 E1·E2 동등성 테스트와 F10 측정이 무의미해진다. 저장소에는 아직 코드도 Unity 프로젝트도 없다. 그래서 "기존 구조"를 읽는 대신 SPEC이 요구하는 구조의 의존 관계와, 설계가 기대는 Unity 동작 전제를 2절에 정리했다. 병렬 구현자가 바로 착수할 수 있도록 C# 시그니처 수준의 계약, JSON 스키마 7종, 공유 규칙 표, 파일 소유권 맵, 디스패치 순서는 5절에 확정했다.

---

## 2. 기존 코드 구조 분석 (Existing Code Structure Analysis)

### 2.0 읽을 코드가 없다는 사실

- 프로젝트 루트(`C:\Users\SSAFY\Desktop\borry\indiv`)에는 하네스 문서(`CLAUDE.md`, `HANDOFF.md`, `ORCHESTRATION.md`, `CODING_GUIDE.md`, `RUN_LOG.md`, `DESIGN_NOTES.md`)와 기획 문서(`PRD.md`, `SPEC.md`, 계획 메모)만 있다. `.cs` 파일도, Unity 프로젝트 폴더(`Assets/`, `ProjectSettings/`, `Packages/`)도 없다(F15 사용자 수행 전).
- 그래서 이 절의 "함수/인터페이스 참조 표"는 기존 코드가 아니라 **SPEC.md가 이름까지 지정한 계약**을 출처(SPEC 절)와 함께 나열한 것이다. 시그니처는 5절에서 새로 정한다.

### 2.1 SPEC이 이름을 지정한 계약 (출처 추적표)

| 계약 | SPEC 출처 | SPEC이 정한 것 | 정하지 않은 것 (5절에서 정함) |
|---|---|---|---|
| `IInputSource` | F1 마지막 기준, F19 설계 제약 | 이동 로직은 키보드를 직접 읽지 않고 입력 소스를 통한다. 자동 경로를 주입한다 | 반환 형식, 속도 지정 방법 |
| 풀 인터페이스(획득/반환) | F9 | 구현 A 자체 풀 / B `Instantiate`·`Destroy` / C `UnityEngine.Pool.ObjectPool<T>`. 상한 초과 시 확장. 토글은 다음 판 적용 | 시그니처, 사전 생성, 판 리셋 |
| 객체 ID (2회차) | 3.0 H | "인덱스 + 세대", 풀 반납(디스폰)마다 세대 증가, 풀 구현과 무관하게 같은 규칙 | 구조체, 발급 주체, 판 간 처리 |
| 피해 세 갈래 (2회차) | 3.0 I, F3·F4·F6 | 일회성 접촉 / 공격자 측 쌍별 쿨다운 / 플레이어 공통 무적. 스텝 번호로 센다. 몸통 접촉 다수 = 최대 피해 1회, 같으면 작은 ID | 구성요소, 같은 스텝 내 적용 순서 |
| `ITargetProvider` | F2, F16 | 전수 / 공간 해시 / `Physics2D` 쿼리. 제곱거리 비교, ID 타이브레이크, 반지름 큰 객체는 별도 리스트. 무기 전용(몬스터는 플레이어 위치 직접 참조). 즉시 전환 | 질의 시점, 반환 형식, 공간 해시 공유 방식 |
| `IUpdatable` + 중앙 틱 | F17 | 몬스터·투사체만 등록. A는 객체별 `FixedUpdate`, B는 중앙 순회. 다음 판 적용 | 순회 순서 독립성 보장 방법 |
| `ICollisionStrategy` (폴링) | F18, 3.0 I | Enter·Exit 두 종류, (채널, ID) 순 정렬, 엔진판은 콜백을 큐에 모은다. 비교 키 (스텝, 채널, 객체 ID 쌍, 종류)(4회차: SPEC 5회차 F18 정렬 순서와 `CollisionEvent.CompareTo`에 통일). 다음 판 적용 | 이벤트 구조체, 채널, 등록 해제 시 Exit 처리 |
| 고정 스텝 | 3.0 F | 1/60초, catch-up 상한 3(`Time.maximumDeltaTime = 0.05`), 게임 시간 = 스텝 수 × 1/60, 일시정지·레벨업 중 스텝 없음 | 스텝 내부 단계 순서, 게이트 소유자 |
| 결정론 RNG | 3.0 F, F16 | 고정 시드 가능 | 생성기 종류, 스트림 분리 |
| JSON 7종 + 로더·레지스트리·검증 | F11, F18 터널링 | 필수 4/선택 3, 플레이 불가 4기준, 정의 단위 건너뛰기, Newtonsoft 파서, UTF-8(U+FFFD 경고), 틱 간격·무적 시간은 초 입력 → 스텝 환산 | 필드명·타입·단위 |
| 화면·흐름 (2회차) | F8 | 창 모드 16:9 자동 선택, Alt+Enter 끔, Canvas Scaler, 사망 > 클리어 > 레벨업, 포커스 상실 자동 일시정지, 개발자 모드 판 기록 미저장 | 소유자, 선택 알고리즘 |
| 개발자 모드 | F19, F10 | `--dev` 인자, F1 오버레이, `if (devMode)` 금지, 값 주입, 측정 배제 조건, 워밍업 300스텝, CSV 헤더·본문 항목, 배속 0.25~3x | 주입 지점 목록, 통계·CSV 형식 |

### 2.2 SPEC 요구 구조의 의존 관계 분석

**(a) 계층 의존 방향** (화살표 = "의존한다", (2회차) Dev → App으로 뒤집었다)

```
Dev(F19) ──▶ App(조립·흐름 F8) ──▶ UI(F5,F8 뷰)
   │            │                     │
   │            ▼                     ▼
   ├──────▶ Strategies(F9,F16,F17,F18)   Simulation(F1~F7, 루프, 진행) ──▶ Data(F11)
   │            │                     │                                  │
   └──────────▶ Core(계약·공유 규칙·추상 훅) ◀───────────────────────────────┘
```

- Core는 UnityEngine 외에 아무것에도 의존하지 않는다.
- Simulation은 Core·Data에만 의존하고 Strategies 구현 클래스를 참조하지 않는다(F9·F16·F17·F18 "인터페이스에만 의존"). Strategies도 Simulation을 참조하지 않는다. 둘 사이에 필요한 연결은 **Core의 추상 훅**(`IEntityTemplateDecorator`, `IStepClockReader`, `IPhysicsBodyRegistry`, `IStepPhaseHandler`)으로 하고, 조립 루트(App, IMP-FLOW)가 구현을 주입한다(2회차, A8).
- **어떤 게임 어셈블리(App 포함)도 Dev를 참조하지 않는다** (2회차). Dev가 App을 참조해 스스로 붙는다(`DevModeEntry`, 5.15). 그래서 F19의 "`if (devMode)` 분기 금지"가 컴파일러 수준에서 강제되고, Dev 어셈블리를 `defineConstraints`로 빼면 SPEC F19 4회차가 요구하는 "개발자 모드 코드를 제거한 빌드"가 별도 코드 수정 없이 나온다(D31).
- 참조 방향 표와 전수 점검은 5c.1에 있다.

**(b) 전략 4건의 실제 결합 관계** (SPEC 3.0 B는 "서로 독립적으로 전환 가능"을 요구한다)

| 결합 | 원인 (SPEC 근거) | 설계 대응 |
|---|---|---|
| F16 공간 해시 ↔ F18 자체 판정 | F16 마지막 기준 "F18이 후보 추림에 재사용", F10 "두 전략은 독립이 아니다" | 공간 해시를 **하나의 공유 인스턴스**(`MonsterSpatialIndex`)로 둔다. 타깃=공간 해시 **또는** 충돌=자체 중 하나라도 켜져 있으면 스텝마다 한 번 재구축한다. "전부 켬"에서 유지 비용이 한 번만 드는 것이 측정할 상호작용이다 |
| F16 C(`Physics2D` 쿼리) ↔ F18 A(`Collider2D`) ↔ 바디 동기화 (2회차) | F18 "몬스터 `Collider2D`는 구현 A와 F16 C일 때만 켠다" | 콜라이더 켜짐과 위치 동기화 필요 여부는 `ColliderActivationPolicy.Resolve(selection)`(5a R19) 한 곳에서 판정하고, 실제 `Rigidbody2D.position`·`MovePosition`·`Physics2D.SyncTransforms` 호출과 콜라이더 켜기/끄기는 **`PhysicsBodySynchronizer` 하나**(IMP-COLLISION)만 한다. `Physics2D.Simulate`는 `EngineCollisionStrategy`만 부른다. 1회차에는 "`Physics2D` 타깃 + 자체 충돌" 조합에서 아무도 몬스터 바디를 옮기지 않았다(5.9.1) |
| F9 풀 ↔ 객체 ID | 3.0 H "풀 구현과 무관하게 같은 규칙" | **ID 할당을 풀에서 분리한다.** 시뮬레이션 소유의 `EntityIdAllocator`가 ID를 발급하고, 디스폰(=반납) 시 세대를 올린다 |
| F17 A(객체별 `FixedUpdate`) ↔ 결정론 | Unity는 같은 클래스 인스턴스끼리의 호출 순서를 보장하지 않는다 [실측 U1] | `Tick`은 **자기 상태만 쓰고**, 다른 객체에 영향을 주는 일(투사체 생성, 소환, 사망)은 지연 명령으로 미룬다. 명령은 Lifecycle에서 (Source, Sequence) 순으로 적용한다(5a R10) |
| F17 A ↔ 측정 공정성 | 객체마다 `FixedUpdate` 메서드가 있으면 Unity는 B 모드에서도 그것을 호출한다 | 엔티티 클래스에는 `FixedUpdate`를 두지 않는다. A 모드일 때만 켜지는 별도 컴포넌트(`MonsterSelfTicker`/`ProjectileSelfTicker`)가 `FixedUpdate`를 가진다 |
| F18 엔진판 ↔ 스텝 경계 | Unity의 물리 시뮬레이션과 `OnTrigger*` 콜백은 모든 `FixedUpdate` **뒤**에 돈다 [실측 U7] | `Physics2D.simulationMode = Script`로 두고, 엔진 전략의 `CollectEvents()` 안에서 `Physics2D.Simulate(1/60)`을 직접 호출한다. 그래야 같은 스텝 번호 안에서 이벤트가 나와 자체판과 스텝 번호가 일치한다 |

**(c) 스텝 내부 단계 순서와 게이트** (결정론의 뼈대, (2회차) 게이트·바디 동기화·생성 경로 반영. 시그니처는 5.3·5.4)

```
FixedUpdate 1회 = 스텝 최대 1회 (SimulationClock.TryBeginStep이 참일 때만)
 [Begin]          (TryBeginStep 안) 예약된 시간 점프 적용 → 대기 명령 적용(개발자 조작, 핫 리로드 교체, 타깃 공급자 전환) → 입력 소비
 [PlayerMove]     플레이어 이동 → StepContext 확정(이 스텝의 플레이어 위치)
 [MonsterTick]    몬스터 AI·이동            ← F17 A: MonsterSelfTicker(실행 순서 -500) / B: 중앙 틱
 [BuildIndex]     몬스터 스냅샷 → (Physics2D 타깃일 때) 몬스터 바디 위치 동기화 → 공간 해시 재구축(필요 시) → 타깃 공급자 갱신
 [WeaponTick]     무기 쿨다운·발사 판단 → 생성 명령 적재(투사체·궤도 원·장판) → 궤도 원 위치 → 젬 끌림 이동
 [ProjectileTick] 투사체 이동·W4 재조준      ← F17 A: ProjectileSelfTicker(실행 순서 +500) / B: 중앙 틱
 [Collision]      ICollisionStrategy.CollectEvents(step)  (엔진판: 전 바디 MovePosition → Simulate → 콜백 큐)
 [Resolve]        이벤트 라우팅(일회성 접촉·젬·상자 효과·겹침 기록) → 무기 지속 피해 → 플레이어 피해 → 처치 확정(드랍·회수 명령)
 [Lifecycle]      명령 적용(생성·회수) → 수명 만료 → 디스폰 → 스폰 → 결과 게이트(사망 > 클리어 > 레벨업이면 게이트를 닫는다)
 [End]            Step++ → (StopAtStep 도달·한 스텝 진행이면 게이트를 닫는다)
```

- **게이트 규칙 (2회차, A3)**: 레벨업·사망·클리어로 인한 게이트 닫기는 **Lifecycle 단계 맨 끝 핸들러 `StepOutcomeGate`(order 90)** 가 `SimulationClock.CloseAfterCurrentStep(reason)`으로 한다. 이 호출은 `IsRunning`을 즉시 거짓으로 만들지만 **진행 중인 스텝은 Lifecycle의 남은 핸들러(없음)와 End까지 끝까지 돈다**(Step이 정상적으로 증가한다). **다음 `FixedUpdate`부터**(같은 프레임의 catch-up 호출 포함) `TryBeginStep`이 거짓이라 스텝이 돌지 않는다. `Update`에서 게이트를 닫는 코드는 없다. 그래서 30/60/144 FPS 어디서든 같은 스텝에서 멈춘다.
- **생성 규칙 (2회차, A7)**: 엔티티 생성·회수는 Lifecycle 단계(와 판 리셋)에서만 일어난다. Lifecycle은 Collision 뒤이므로 **스텝 s에 생성된 엔티티는 스텝 s의 판정에 참여하지 않고, 스텝 s+1의 Collision부터 참여한다.** 첫 `Tick`도 스텝 s+1이다.

### 2.3 설계가 기대는 Unity 6 API·동작 전제 (전부 실측 필요)

아래는 이 설계가 **사실이라고 가정하고** 기대는 Unity 동작이다. 코드를 실행해 확인한 것이 아니며 하나도 확정되지 않았다. 번호는 6.2절과 같다.

| # | 전제 | 이 전제에 기대는 설계 |
|---|---|---|
| U1 | `[DefaultExecutionOrder]`가 `FixedUpdate`에도 적용되고, 같은 클래스 인스턴스 간 호출 순서는 보장되지 않는다 | 드라이버 3개 + 셀프 티커 2개의 인터리브(2.2 c) |
| U2 | `Time.fixedDeltaTime = 1/60`, `Time.maximumDeltaTime = 0.05`이면 한 프레임의 `FixedUpdate`가 최대 3회다 | catch-up 상한 3 |
| U3 | `Time.captureDeltaTime = 1/30, 1/60, 1/144`로 프레임레이트를 흉내 내면 `FixedUpdate` 누적이 그에 맞게 동작한다 | 프레임레이트 독립 PlayMode 테스트 |
| U4 | `Rigidbody2D.collisionDetectionMode` 기본값은 `Discrete`다 (SPEC Q39) | E1 조건 |
| U5 | 키네마틱 ↔ 키네마틱 트리거 콜백이 발생한다(필요하면 `useFullKinematicContacts`로) | 엔진 충돌 전략의 바디 구성 |
| U6 | `Continuous` 모드가 트리거·키네마틱 바디의 판정 결과를 바꿀 수 있다 | E2가 의미 있는 검증인지 여부 |
| U7 | `simulationMode = Script`에서 `Physics2D.Simulate()` 호출 중(또는 직후)에 `OnTriggerEnter2D/Exit2D`가 동기적으로 호출된다 | 엔진판 이벤트가 같은 스텝 번호에 나온다 |
| U8 | `Physics2D.callbacksOnDisable = false`이면 콜라이더 비활성화 시 Exit 콜백이 나오지 않는다 | "등록 해제 시 Exit 없음" 규칙의 엔진판 일치 |
| U9 | Box2D 원-원 겹침의 경계(거리 = 반지름 합)가 자체 판정 `≤`와 같은 쪽으로 판정된다 | E1 경계 사례 |
| U10 | `Rigidbody2D.MovePosition`은 스윕 이동이고, `transform.position`을 직접 쓰면 다음 시뮬레이션에서 텔레포트로 처리된다 | 엔진 모드에서 표시 동기화를 생략한다 |
| U11 | `Rigidbody2D.position`을 바꾸거나 콜라이더를 켠 직후 `Physics2D.OverlapCircle`에 반영된다(`autoSyncTransforms`·`SyncTransforms` 영향) | `Physics2D` 타깃 공급자 |
| U12 | 이름 없는 레이어 인덱스(8~17)에 `Physics2D.IgnoreLayerCollision`을 코드로 설정할 수 있다 | 레이어 충돌 매트릭스를 코드로 설정 |
| U13 | Windows 플레이어에서 `Application.streamingAssetsPath` + `File.ReadAllText`가 동기로 동작하고, 빌드 폴더 `<Name>_Data/StreamingAssets/`를 편집할 수 있다 | F11 로더, F13 |
| U14 | `ProfilerMarker`는 비개발 빌드에서 호출째 제거되고, `ProfilerRecorder`는 Development Build에서 마커의 프레임 합계를 읽으며, `Begin`/`End`를 다른 스크립트에서 짝지어도 된다 | 시스템 ms 수집 |
| U15 | `ProfilerRecorder`로 "GC Allocated In Frame" 카운터를 읽을 수 있다 | F10 항목 3 |
| U16 | Windows 플레이어에서 `Environment.GetCommandLineArgs()`에 `--dev`가 그대로 들어온다 | Q40 활성화 |
| U17 | Input System 패키지가 활성이고 업데이트 모드가 Dynamic이라 `wasPressedThisFrame`이 `Update`에서 유효하다 | 입력 수집 |
| U18 | (2회차 수정) 한글 TTF로 만든 TextMeshPro Dynamic Font Asset이 uGUI에서, 같은 TTF의 `Font`가 IMGUI(`GUIStyle.font`)에서 한글을 렌더링한다 | 모든 UI |
| U19 | **대체됨(2회차)** — UI Toolkit을 쓰지 않으므로(D25) 무의미하다. U42로 대체 | - |
| U20 | 런타임에 만든 `Texture2D` 원 스프라이트의 월드 크기를 `sprite.bounds`로 알 수 있다 | 반지름 → 표시 배율 |
| U21 | `ObjectPool<T>`의 `maxSize`를 넘겨 반환하면 객체가 파괴되고, `collectionCheck`가 중복 반환을 잡는다 | 풀 구현 C |
| U22 | `Destroy`는 프레임 끝까지 지연되므로 `SetActive(false)`를 먼저 해야 같은 프레임의 다음 스텝에 끼어들지 않는다 | 풀 구현 B |
| U23 | `Time.maximumDeltaTime`은 스케일된 `deltaTime`을 제한한다(그러면 60FPS에서 `timeScale = 3`이 프레임당 정확히 3스텝) | 개발자 모드 배속 3x |
| U24 | (2회차 수정) 자동 선택한 16:9 창 크기가 작업 표시줄·DPI 배율 때문에 강제로 줄어들지 않고 그대로 유지된다(1280x720 미만 작업 영역에서는 잘린 채 유지) | Q44 화면 밖 계산(16:9 전제) |
| U25 | `com.unity.nuget.newtonsoft-json` 패키지를 6000.3에서 쓸 수 있고 Mono 백엔드에서 동작한다 | JSON 파서(D17 확정) |
| U26 | 같은 빌드·같은 PC에서 float 연산은 실행마다 비트 단위로 같다(Mono JIT) | 프레임레이트 간 결정론 |
| U27 | `Unity.exe -batchmode -runTests -testPlatform PlayMode`로 물리를 포함한 PlayMode 테스트가 돈다 | E1·E2 자동 실행 |
| U28 | 6.3의 새 저수준 2D 물리 API와 무관하게 기존 `Physics2D`·`Rigidbody2D` API가 그대로 동작한다 | F18 A, F16 C |
| U29 | `SpriteRenderer.drawMode = Tiled`로 반복 배경을 그릴 수 있다(URP 2D 포함) | F8 반복 배경 |
| U30 | (2회차) `Physics2D.defaultContactOffset`을 코드로 최솟값 근처(0.0001)까지 낮출 수 있고, 이 값이 트리거 겹침 경계를 반지름 합 바깥으로 넓히는 효과를 없앤다 | E1 경계 일치 |
| U31 | (2회차) `FixedUpdate` 도중(Late 드라이버 안에서) `enabled = true`가 된 셀프 티커는 **같은 프레임의 다음 `FixedUpdate` 호출부터** 호출된다(다음 프레임까지 밀리지 않는다) | F17 A 모드의 프레임레이트 결정론 |
| U32 | (2회차) `simulationMode = Script`에서도 `Rigidbody2D.position` 직접 대입이 `Simulate` 없이 즉시 쿼리에 반영되고, `Physics2D.queriesHitTriggers = true`이면 `OverlapCircle`이 트리거 콜라이더를 찾는다 | "`Physics2D` 타깃 + 자체 충돌" 조합 |
| U33 | (2회차) URP 2D 렌더러에서 `RenderPipelineManager.endCameraRendering` 콜백 안의 `GL` 즉시 그리기가 화면에 나온다(Built-in의 `OnRenderObject` 대체) | 개발자 시각화 |
| U34 | (2회차) URP 2D에서 기본 스프라이트 머티리얼(Sprite-Lit-Default)은 Global Light 2D가 있어야 정상 밝기로 보이며, `Light2D`를 코드로 만들어 Global로 둘 수 있다 | 모든 도형 표시 |
| U35 | (2회차) `Screen.mainWindowDisplayInfo.workArea`가 Windows 플레이어에서 작업 영역(작업 표시줄 제외)을 물리 픽셀로 돌려주고, `Screen.SetResolution(w, h, Windowed)`의 w·h는 창 테두리를 뺀 클라이언트 크기다 | 창 크기 자동 선택 |
| U36 | (2회차) Player Settings "Allow Fullscreen Switch" 끔이 Windows 플레이어의 Alt+Enter 전환을 막는다 | F8 Alt+Enter 끔 |
| U37 | (2회차) 창 모드 플레이어에서 포커스를 잃으면 `OnApplicationFocus(false)`가 오고, `Application.runInBackground`를 런타임에 켜면 포커스가 없어도 프레임이 계속 돈다 | 포커스 상실 자동 일시정지, 측정 중 백그라운드 실행 |
| U38 | (2회차) `ProfilerRecorder`로 Memory 카테고리의 "Total Used Memory"·"Total Reserved Memory"를 Development Build에서 읽을 수 있다(릴리스 빌드에서의 가용성 포함) | F10 항목 6 |
| U39 | (2회차) `QualitySettings.vSyncCount = 0`, `Application.targetFrameRate = -1`을 런타임에 설정하면 창 모드 플레이어가 그대로 따르고, `UnityEngine.Scripting.GarbageCollector.isIncremental`로 점진적 GC 여부를 읽을 수 있다 | 측정 환경 고정·기록 |
| U40 | (2회차) `[RuntimeInitializeOnLoadMethod(AfterSceneLoad)]`는 첫 씬 오브젝트의 `Awake` 뒤, `Start` 전에 호출되고, asmdef `defineConstraints`로 제외된 어셈블리는 빌드에서 통째로 빠진다 | 개발자 모드 자가 부착, 코드 제거 비교 빌드 |
| U41 | (2회차) Mono에서 `File.ReadAllText(path, new UTF8Encoding(false, false))`가 잘못된 바이트를 예외 없이 U+FFFD로 바꾸고 UTF-8 BOM은 제거한다 | JSON 인코딩 경고 |
| U42 | (2회차) Input System 패키지 사용 시 uGUI `EventSystem`에 `InputSystemUIInputModule`을 붙이면 방향키·Enter 메뉴 탐색과 클릭이 동작한다. 같은 프레임의 UI 클릭 콜백과 `MonoBehaviour.Update` 사이의 순서는 보장되지 않는다고 가정한다 | 메뉴 키보드·마우스 조작, 레벨업 중복 입력 처리 |

### 2.4 Q44 카메라 크기 검증 (계산이며, 실측 아님)

- orthographic size 9 → 화면 세로 절반 = 9유닛, 16:9이면 가로 절반 = 9 × 16/9 = 16유닛. (2회차) 1920x1080·1600x900·1280x720 세 후보 모두 16:9라 보이는 월드 범위가 같다(SPEC F3·F8 4회차).
- 화면 반대각 = √(16² + 9²) = √337 ≈ **18.358**유닛. 스폰 반경 20보다 작으므로 **스폰 지점(중심)은 항상 화면 밖**이다(SPEC F3 기준 충족). 카메라가 즉시 스냅하므로(TAC-F1-6) 이 여유가 보존된다.
- 여유는 20 − 18.358 ≈ **1.64유닛**이고, 모서리 방향에서만 적용된다(가로 방향 여유 4, 세로 방향 여유 11). **반지름이 1.64 이상인 몬스터가 모서리 방향으로 스폰되면 몸체 일부가 스폰 순간 화면에 보인다.** SPEC 기준은 "스폰 지점"이라 위반은 아니다. 기본 데이터의 보스 반지름 1.5는 통과한다.
- 창 비율이 16:9에서 벗어나면 가로 절반 = 9 × aspect로 바뀐다. aspect 2.0이면 √(18² + 9²) ≈ 20.12 > 20이 되어 깨진다. 16:9 유지 전제는 U24·U35.
- 디스폰 거리 45는 반대각 18.36의 약 2.45배라 화면 안에서 디스폰되는 일은 없다. N > 400 측정에서 D는 더 커진다(R26).

---

## 3. 기능 목록 (Feature List)

SPEC.md의 ID를 그대로 쓴다. 추가·삭제는 없다.

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

SPEC.md 3절의 수용 기준 329개는 그대로 유효하며 여기서 반복하지 않는다. 아래는 그 기준을 이 설계로 구현할 때 추가로 지켜야 하는 **기술 수용 기준(TAC)** 이다. 각 항목은 5절의 계약과 5a의 공유 규칙을 근거로 하며, 테스트 위치를 함께 적었다(EM = EditMode, PM = PlayMode).

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

## 5. 신규 구현 필요 함수 명세 (New Function/Interface Specs)

모든 코드는 신규다. 아래 시그니처는 병렬 구현자 사이의 계약이다. 5c와 어긋나면 5c가 우선한다. 공개 멤버에는 영어 한 줄 요약 주석을 단다(CODING_GUIDE §3). 사용자에게 보이는 문자열은 한국어다.

### 5.0 네임스페이스와 어셈블리

| 네임스페이스(=asmdef) | 내용 |
|---|---|
| `Survivor.Core` | 공유 규칙 함수, `EntityId`, RNG, 계약 인터페이스, 추상 훅(2회차), 단계 열거형, 이벤트·명령 구조체, `SimulationRules`, `GameRules` |
| `Survivor.Data` | 정의 객체, 로더, 검증, 에셋 키 레지스트리 |
| `Survivor.Simulation` | 루프 드라이버, 시계·게이트, 엔티티 생명주기, 플레이어·무기·몬스터·드랍·진행 |
| `Survivor.Strategies` | 풀 3, 타깃 3, 틱 2, 충돌 2 구현, 공간 해시, 물리 바디 동기화, 템플릿 데코레이터 |
| `Survivor.UI` | uGUI 뷰(2회차, 코드로 구성) |
| `Survivor.App` | 조립 루트(`GameBootstrap`), 흐름 제어, 화면 설정, 카메라·배경 |
| `Survivor.Dev` | 개발자 모드(App을 참조해 스스로 붙는다) |

#### 5.0.1 인터페이스 전체 목록과 구현 (2회차 신규 — SPEC F12 4회차 다이어그램 일치 기준)

| 인터페이스 | 어셈블리 | 구현 클래스 |
|---|---|---|
| `IPoolStrategy` | Core | `CustomPoolStrategy`, `InstantiatePoolStrategy`, `UnityObjectPoolStrategy` (Strategies) |
| `ITargetProvider` | Core | `BruteForceTargetProvider`, `SpatialHashTargetProvider`, `Physics2DTargetProvider` (Strategies), `TargetProviderSlot` (Simulation, 위임) |
| `ITickStrategy` | Core | `CentralTickStrategy`, `SelfTickStrategy` (Strategies) |
| `IUpdatable` | Core | `Monster`, `PlayerProjectile`, `EnemyProjectile` (Simulation) |
| `ICollisionStrategy` | Core | `SelfCollisionStrategy`, `EngineCollisionStrategy` (Strategies), `LoggingCollisionStrategy` (Dev, 데코레이터) |
| `IInputSource` | Core | `KeyboardInputSource` (Simulation), `AutoPathInputSource` (Dev) |
| `ISpawnPolicy` | Core | `TableSpawnPolicy` (Simulation), `DevSpawnPolicy` (Dev) |
| `ISimulationCommands` | Core | `SimulationCommandBuffer` (Simulation) |
| `IStepPhaseHandler` | Core (2회차 이동) | `PendingCommandQueue`, `PlayerMovement`, `MonsterRegistry`, `WeaponInventory`, `PickupSystem`, `CollisionEventRouter`, `WeaponDamageResolver`, `PlayerDamageReceiver`, `KillPipeline`, `SimulationCommandBuffer`, `LifetimeSystem`, `DespawnSystem`, `MonsterSpawner`, `StepOutcomeGate` (Simulation), `PhysicsBodySynchronizer`, `MonsterSpatialIndex` (Strategies) |
| `IStepClockReader` (2회차) | Core | `SimulationClock` (Simulation) |
| `IEntityTemplateDecorator` (2회차) | Core | `PoolTemplateDecorator`, `CollisionTemplateDecorator`, `SelfTickTemplateDecorator` (Strategies) |
| `IPhysicsBodyRegistry` (2회차) | Core | `PhysicsBodySynchronizer` (Strategies) |
| `ISimulationCommandHandler` | Simulation | `EntityDespawnCommandHandler`, `PlayerSpawnCommandHandler`, `MonsterCommandHandler`, `DropCommandHandler` (Simulation) |
| `ICollisionEventHandler` | Simulation | `PlayerHitHandler`(채널 0~2), `PlayerDamageReceiver`(3~5), `PickupSystem`(6~8) (Simulation) |
| `IDevModeHost` (2회차) | App | `GameBootstrap` (App) |

### 5.1 객체 ID와 할당기 (Core)

```csharp
namespace Survivor.Core
{
    /// Identity of a simulation entity; Index is reused, Generation increments on each free.
    public readonly struct EntityId : IEquatable<EntityId>, IComparable<EntityId>
    {
        public readonly int Index;       // 1-based. 0 means None.
        public readonly int Generation;  // starts at 1 for every index.
        public EntityId(int index, int generation);
        public static readonly EntityId None;          // default(EntityId)
        public bool IsValid { get; }                   // Index > 0
        public long Packed { get; }                    // ((long)Index << 32) | (uint)Generation
        public int CompareTo(EntityId other);          // Index, then Generation
        public bool Equals(EntityId other);
        public override string ToString();             // "17:3"
        public static bool operator <(EntityId a, EntityId b);
        public static bool operator >(EntityId a, EntityId b);
        public static bool operator ==(EntityId a, EntityId b);
        public static bool operator !=(EntityId a, EntityId b);
    }

    /// Issues EntityIds independently of any pool strategy so ids are identical across F9 strategies.
    public sealed class EntityIdAllocator
    {
        public EntityId Allocate();             // pops most recently freed index (LIFO), else next new index
        public void Free(EntityId id);          // throws InvalidOperationException if !IsLive(id); increments generation
        public bool IsLive(EntityId id);
        public int LiveCount { get; }
        public void Reset();                    // new run: next index = 1, all generations back to 1, free list empty
    }
}
```

- 플레이어는 판마다 가장 먼저 할당되므로 항상 `1:1`이다. 플레이어 자석(PlayerMagnet) 바디는 별도 ID(`2:1`)를 받는다(SPEC F18 4회차가 채널을 비교 키에 넣어 D8은 해소됐고, 별도 ID는 로그 가독성 때문에 유지한다).
- 세대는 **할당 해제(디스폰·사망·수명 만료·소모·교체)** 때 올라간다. 세대를 소유하는 쪽은 풀이 아니라 할당기다(2.2 b, SPEC 3.0 H "풀 구현과 무관").
- (2회차) 판 리셋 때 `Reset()`으로 세대가 1로 돌아간다. 같은 시드의 판을 다시 돌렸을 때 ID가 같아야 결정론 비교가 되기 때문이다. ID로 키를 잡는 모든 기록은 리셋에서 함께 지워지므로 판을 넘는 오적용은 없다(D26).

### 5.2 결정론 RNG (Core)

```csharp
namespace Survivor.Core
{
    /// PCG32 generator (O'Neill, XSH-RR). Never use UnityEngine.Random/System.Random in simulation code.
    public sealed class DeterministicRng
    {
        public DeterministicRng(ulong seed, ulong streamId);
        public uint NextUInt();
        public float NextFloat01();            // (NextUInt() >> 8) * (1f / 16777216f), range [0,1)
        public int NextInt(int maxExclusive);  // unbiased via rejection; throws if maxExclusive <= 0
        public bool Chance(float probability); // NextFloat01() < probability
    }

    public enum RngStream { Spawn = 1, Summon = 2, Drop = 3, Chest = 4, LevelUp = 5 }

    /// Independent per-purpose streams so disabling one system (e.g. level-up) never shifts another's sequence.
    public sealed class RngStreams
    {
        public RngStreams(ulong masterSeed);
        public ulong MasterSeed { get; }
        public DeterministicRng Get(RngStream stream);   // seed = SplitMix64(masterSeed ^ (0x9E3779B97F4A7C15UL * (ulong)stream))
        public void Reseed(ulong masterSeed);
    }
}
```

- 일반 플레이의 시드는 판 시작 시 `(ulong)DateTime.UtcNow.Ticks` 1회(`App`만 허용, TAC-0-2 예외)다. 개발자 모드는 고정 시드를 주입한다. 결과 화면에는 시드를 표시하지 않고, 개발자 오버레이와 CSV에는 기록한다.
- (2회차) LevelUp 스트림은 게이트가 닫힌 동안(스텝 밖) 후보 추첨에서만 쓰인다. 다른 스트림은 스텝 안에서만 쓰인다.

### 5.3 고정 스텝 시계·게이트 (Core 계약, Simulation 구현, IMP-LOOP) (2회차 재작성)

```csharp
namespace Survivor.Core
{
    public enum SimulationPhase
    { Begin = 0, PlayerMove = 1, MonsterTick = 2, BuildIndex = 3, WeaponTick = 4,
      ProjectileTick = 5, Collision = 6, Resolve = 7, Lifecycle = 8, End = 9 }

    /// Handler invoked by SimulationPipeline for one phase of one step. (moved to Core in rev 2 so Strategies can implement it)
    public interface IStepPhaseHandler { void OnPhase(SimulationPhase phase, in StepContext context); }

    /// Read-only view of the step gate for code outside Simulation (self tickers, dev tools).
    public interface IStepClockReader
    {
        bool StepActive { get; }          // true between Begin and End of an executing step
        long Step { get; }                // completed steps in the current run
        SimulationPhase CurrentPhase { get; }
        StepContext CurrentContext { get; }   // valid only while StepActive and phase >= MonsterTick
    }

    /// Read-only per-step view handed to every phase handler and IUpdatable.
    public readonly struct StepContext
    {
        public readonly long Step;
        public readonly Vector2 PlayerPosition;     // after PlayerMove of this step (Begin/PlayerMove see the previous position)
        public readonly SimulationRules Rules;
        public readonly ITargetProvider Targets;    // valid from BuildIndex onward
        public readonly ISimulationCommands Commands;
        public StepContext(long step, Vector2 playerPosition, SimulationRules rules,
                           ITargetProvider targets, ISimulationCommands commands);
    }
}

namespace Survivor.Simulation
{
    public enum GateCloseReason { None, Paused, LevelUp, Death, Clear, StopAtStep, SingleStepDone, RunReset, Error }

    /// Owns the step counter and the run gate; the only source of game time.
    public sealed class SimulationClock : IStepClockReader
    {
        public long Step { get; }
        public bool IsRunning { get; }                 // gate for the next FixedUpdate
        public bool StepActive { get; }
        public SimulationPhase CurrentPhase { get; }
        public StepContext CurrentContext { get; }
        public int StepsThisFrame { get; }             // reset by SimulationFrameCounter.Update (order -10000)
        public double GameSeconds { get; }             // StepMath.StepsToSeconds(Step)
        public GateCloseReason CloseReason { get; }    // reason of the latest close
        public int CloseSequence { get; }              // increments on every close; flow polls it in Update

        public void Open();                                      // flow only; throws if StepActive
        public void Close(GateCloseReason reason);               // outside a step only (pause, run reset); throws if StepActive
        public void CloseAfterCurrentStep(GateCloseReason reason); // inside a step only (Lifecycle handlers); throws if !StepActive
                                                                 // first close in a step wins; the step still runs to End
        public void RequestSingleStep();                         // dev: opens, runs exactly one step, End closes with SingleStepDone
        public void RequestJump(long targetStep);                // dev: applied inside the next TryBeginStep before Begin
        public void Reset();                                     // Step = 0, gate closed (RunReset)

        internal bool TryBeginStep(long stopAtStep);  // SimulationDriverEarly: IsRunning && Step < stopAtStep
        internal void SetPhase(SimulationPhase phase);
        internal void SetContext(in StepContext context);
        internal void EndStep(long stopAtStep);       // Step++; then close with StopAtStep (Step >= stopAtStep) / SingleStepDone if applicable and not closed yet
        internal void AbortStep();                    // driver exception handler only: StepActive = false, gate closed (Error), Step NOT incremented
    }

    /// Ordered registry of phase handlers; same order for every strategy combination.
    public sealed class SimulationPipeline
    {
        public void AddHandler(SimulationPhase phase, int order, IStepPhaseHandler handler); // order ties -> throw
        public void RunPhase(SimulationPhase phase, in StepContext context);
    }

    // Drivers (MonoBehaviour, one instance each, created by GameBootstrap):
    // [DefaultExecutionOrder(-1000)] SimulationDriverEarly : TryBeginStep, Begin, PlayerMove, (central) MonsterTick, opens Tick.Monsters marker
    // [DefaultExecutionOrder(-500)]  MonsterSelfTicker      (per monster, enabled only in per-object mode)
    // [DefaultExecutionOrder(0)]     SimulationDriverMid   : closes Tick.Monsters, BuildIndex, WeaponTick, (central) ProjectileTick, opens Tick.Projectiles
    // [DefaultExecutionOrder(500)]   ProjectileSelfTicker  (per projectile, enabled only in per-object mode)
    // [DefaultExecutionOrder(1000)]  SimulationDriverLate  : closes Tick.Projectiles, Collision, Resolve, Lifecycle, End
}
```

- 중앙 틱 모드에서는 `MonsterTick`을 `Early`에서, `ProjectileTick`을 `Mid`에서 `ITickStrategy.RunGroup`으로 실행한다. 객체별 모드에서는 `RunGroup`이 아무것도 하지 않고, 같은 위치에 Unity가 셀프 티커를 끼워 넣는다 [실측 U1].
- `TryBeginStep`이 거짓이면 같은 `FixedUpdate`의 `Mid`·`Late`·셀프 티커는 `StepActive == false`를 보고 아무것도 하지 않는다.
- **게이트 규칙 (2회차, A3)**:
  - 스텝 안에서 닫는 경로는 `CloseAfterCurrentStep` 하나이고, 호출자는 `StepOutcomeGate`(Lifecycle order 90: Death/Clear/LevelUp)와 `EndStep`(StopAtStep/SingleStepDone)뿐이다.
  - 호출 즉시 `IsRunning = false`. 진행 중인 스텝의 나머지 단계(Lifecycle의 남은 핸들러, End)는 끝까지 돌고 `Step`이 증가한다. 그 다음 `FixedUpdate`부터(같은 프레임의 catch-up 포함) 스텝이 돌지 않는다.
  - 한 스텝 안에서 먼저 닫은 이유가 남는다(`StepOutcomeGate`가 End보다 먼저이므로 Death/Clear/LevelUp이 StopAtStep보다 우선한다). 개발자 자동 종료는 이유가 아니라 `Step >= StopAtStep`으로 판정한다.
  - 예외는 별도 경로다. 드라이버가 단계 실행 중 예외를 잡으면 `AbortStep()`을 부른다. 그 스텝은 그 단계에서 중단되고 남은 단계와 End를 돌지 않으며 `Step`도 증가하지 않는다. 게이트는 `Error`로 닫히고 FLOW가 오류 표시로 간다(5c.3 #7).
  - 스텝 밖에서 닫는 경로는 `Close`(일시정지·포커스 상실·판 리셋)이고 `Update`에서 호출된다. 스텝이 돌지 않는 시점이라 결정론에 영향이 없다.
  - 여는 경로는 `Open` 하나이고 FLOW만 부른다(재개, 레벨업 종료, 판 시작).
- **설정 적용 (2회차, A12)**: `SimulationSettingsApplier.Apply()`(IMP-LOOP)는 **시간 설정만** 한다: `Time.fixedDeltaTime = StepMath.StepSeconds`, `Time.maximumDeltaTime = StepMath.MaxCatchUpSteps * StepMath.StepSeconds`(= 0.05). `Physics2D` 전역 설정과 레이어 매트릭스는 `PhysicsSettingsApplier`(IMP-COLLISION, 5.9.1)만 한다. 둘 다 `GameBootstrap.Awake`가 부른다 [실측 U2].
- 배속(A2): `Time.timeScale`은 `[GameRules.MinTimeScale, GameRules.MaxTimeScale] = [0.25, 3]`로 제한한다. `maximumDeltaTime`은 바꾸지 않는다(catch-up 3 유지). 60FPS에서 3x면 프레임당 3스텝, 그보다 낮은 FPS에서는 실제 배속이 3x보다 낮고 오버레이가 그 값을 보여 준다 [실측 U23].

### 5.4 지연 명령과 생성 경로 (Core 계약, Simulation 구현) (2회차 재작성, A7)

```csharp
namespace Survivor.Core
{
    public enum EntityCategory { Monster, PlayerProjectile, EnemyProjectile, PlayerOrb, PlayerZone, EnemyZone, Gem, Chest }  // moved to Core (rev 2)

    public enum SimulationCommandKind : byte
    { Despawn = 0, SpawnPlayerProjectile = 1, SpawnPlayerOrb = 2, SpawnPlayerZone = 3,
      SpawnEnemyProjectile = 4, SpawnEnemyZone = 5, SpawnMonsters = 6, SpawnGem = 7, SpawnChest = 8 }

    public enum DespawnReason : byte { Killed = 0, OutOfRange = 1, LifetimeExpired = 2, Consumed = 3, Replaced = 4, RunReset = 5 }

    public readonly struct SimulationCommand
    {
        public readonly SimulationCommandKind Kind;
        public readonly EntityId Source;       // issuing entity (sort key 1); EntityId.None for dev/weapon-less sources, PlayerId for weapons
        public readonly int Sequence;          // per-source order within the step (sort key 2), assigned by the buffer
        public readonly EntityId Target;       // Despawn target
        public readonly DespawnReason Reason;
        public readonly Vector2 Position;      // spawn point or summon center
        public readonly Vector2 Direction;
        public readonly float Radius;          // summon / dev spawn ring radius
        public readonly string DefinitionId;   // monster id / weapon id
        public readonly int Count;
        public readonly int LevelIndex;        // weapon level (0-based) for player spawns
        public readonly int SlotIndex;         // weapon slot for player spawns (orb/zone ownership)
        public readonly int OrbIndex;          // orb index for SpawnPlayerOrb
        public readonly bool IgnoreCap;        // boss / dev "spawn N now" with cap lifted
        public static int Compare(in SimulationCommand a, in SimulationCommand b);  // (Source, Sequence) — rule R10
    }

    /// Deferred, order-independent side effects. Applied in Lifecycle order 0, sorted by (Source, Sequence).
    public interface ISimulationCommands { void Enqueue(in SimulationCommand command); }
}

namespace Survivor.Simulation
{
    public interface ISimulationCommandHandler { void Handle(in SimulationCommand command, in StepContext context); }
}
```

- **생성 경로는 Lifecycle 하나다.** 엔티티를 만들거나 지우는 모든 요청은 명령으로 들어와 `SimulationCommandBuffer`(Lifecycle order 0)가 정렬 후 종류별 핸들러로 넘긴다. 핸들러: `Despawn` → `EntityDespawnCommandHandler`(LOOP), `SpawnPlayer*` → `PlayerSpawnCommandHandler`(PLAYER), `SpawnEnemy*`·`SpawnMonsters` → `MonsterCommandHandler`(MONSTER), `SpawnGem`·`SpawnChest` → `DropCommandHandler`(MONSTER). 같은 대상의 `Despawn`이 두 번 들어오면 두 번째는 무시한다.
- Lifecycle 안에서 직접 `EntityLifecycle`을 부르는 핸들러는 셋뿐이다: 수명 만료 `LifetimeSystem`(order 10, (5회차) 수명 없는 엔티티(궤도 원 등, `ExpireStep = long.MaxValue`)는 건너뛴다), `DespawnSystem`(order 20), `MonsterSpawner`(order 30, 일반 스폰·보스 등장).
- 명령을 넣는 곳: 무기(WeaponTick, Source = 플레이어 ID, 무기는 슬롯 순서로 돌므로 Sequence가 결정적), M3·M4(MonsterTick, Source = 몬스터 ID), 투사체·젬·상자 소모와 처치(Resolve, Source = 소모된 엔티티 또는 처치된 몬스터 ID), `PlayerProgressionApplier`의 진화·구성 변경(Resolve 또는 Begin), 개발자 "즉시 N기/보스/상자"(Begin에서 대기 명령이 Source = None으로 넣음 → 같은 스텝 Lifecycle에서 생성).
- **판정 참여 (한 문장)**: **스텝 s의 Lifecycle에서 생성된 엔티티는 스텝 s의 Collision에 참여하지 않으며, 스텝 s+1의 Collision부터 참여하고 첫 `Tick`도 s+1이다.** 5.10·TAC-0-8·TAC-F6-8·TAC-F18-6이 모두 이 문장을 따른다.
- 1회차의 "WeaponTick에서 즉시 생성 + 생성 스텝 Tick 건너뜀" 규칙은 없앴다. 생성이 Tick보다 뒤라 건너뛸 일이 생기지 않는다. 엔티티의 `Tick`에는 방어용 단언 `Debug.Assert(ctx.Step > SpawnStep)`만 둔다.
- ID 발급 순서도 이 정렬 순서를 따르므로 풀 전략·틱 전략과 무관하게 같다(TAC-0-9).

### 5.5 입력 소스 (Core 계약, IMP-LOOP 키보드·IMP-DEV 자동 경로)

```csharp
namespace Survivor.Core
{
    public readonly struct MoveCommand
    {
        public readonly Vector2 Direction;      // unit length or zero
        public readonly float SpeedOverride;    // units per second; < 0 means use player stats
        public MoveCommand(Vector2 direction, float speedOverride = -1f);
        public static readonly MoveCommand None;
    }

    /// Supplies movement intent for one step; implementations must not read Time.*.
    public interface IInputSource { MoveCommand ReadMove(long step, Vector2 currentPosition); }
}
```

- `KeyboardInputSource : MonoBehaviour, IInputSource`(Simulation): TAC-F1-7. ESC·1/2/3·F1은 이 클래스가 다루지 않는다(UI·Dev가 `Update`에서 처리한다) [실측 U17].
- `AutoPathInputSource : IInputSource`(Dev): 생성자 `(Vector2 center, float radius, float speedUnitsPerSecond, float startAngleRad)`. 스텝 s의 목표점 = `center + radius·(cos φ, sin φ)`, `φ = start + (speed/radius)·(s+1)/60`. 반환 방향 = `(목표점 − 현재 위치)` 정규화, `SpeedOverride` = min(speed, 거리 × 60). 사람 입력이 없으므로 결정적이다.

### 5.6 풀 전략과 템플릿 데코레이터 (Core 계약, IMP-POOL·IMP-COLLISION·IMP-LOOP 구현) (2회차 수정, A8)

```csharp
namespace Survivor.Core
{
    public enum PoolStrategyKind { InstantiateDestroy = 0, CustomPool = 1, UnityObjectPool = 2 }

    /// Supplies inactive-to-active GameObjects; never touches gameplay state.
    public interface IPoolStrategy
    {
        PoolStrategyKind Kind { get; }
        GameObject Acquire(GameObject template, Vector2 position);   // returned object is active at position
        void Release(GameObject instance);                           // throws InvalidOperationException on double release / foreign object
        void Prewarm(GameObject template, int count);                // no-op for InstantiateDestroy
        void ReleaseAll();                                           // run reset: every active instance released (B: destroyed)
        int ActiveCount { get; }
    }

    /// Hook that lets Strategies attach their components to entity templates without Simulation referencing Strategies.
    public interface IEntityTemplateDecorator
    {
        void Decorate(GameObject template, EntityCategory category);   // called once per template at bootstrap
    }
}
```

- 템플릿은 `PrefabCatalog`(Simulation, IMP-LOOP)가 부트스트랩에서 코드로 만든 비활성 `GameObject`다(카테고리별 8종). `PrefabCatalog`가 직접 붙이는 것은 `SpriteRenderer`와 카테고리별 `SimEntity` 파생 컴포넌트뿐이다. **(2회차) 나머지는 조립 루트가 주입한 데코레이터 목록이 붙인다**:
  - `PoolTemplateDecorator`(IMP-POOL): `PooledObject`(템플릿 참조와 활성 플래그).
  - `CollisionTemplateDecorator`(IMP-COLLISION): 비활성 `CircleCollider2D`(isTrigger) + `Rigidbody2D`(Kinematic, `simulated = false`) + `CollisionBodyLink`, 레이어 = R30.
  - `SelfTickTemplateDecorator`(IMP-LOOP): `Monster` → 비활성 `MonsterSelfTicker`, `PlayerProjectile`·`EnemyProjectile` → 비활성 `ProjectileSelfTicker`.
  데코레이터는 전략 선택과 무관하게 전부 적용된다(TAC-F9-7). 그래서 전략이 바뀌어도 템플릿을 다시 만들 필요가 없다.
- 레지스트리 항목에 프리팹 덮어쓰기가 있으면 그 프리팹을 복제한 뒤 같은 데코레이터를 적용한다(F11).
- `PoolPrewarmCounts`(Simulation 상수) [Q13]: Monster 450, PlayerProjectile 300, EnemyProjectile 100, PlayerOrb 16, PlayerZone 16, EnemyZone 8, Gem 600, Chest 16. 측정 후 조정한다.

### 5.7 타깃 공급자 (Core 계약, IMP-SPATIAL 구현)

```csharp
namespace Survivor.Core
{
    public enum TargetProviderKind { BruteForce = 0, SpatialHash = 1, Physics2DQuery = 2 }

    public struct MonsterSnapshot
    {
        public EntityId Id;
        public Vector2 Position;
        public float Radius;
        public bool IsLarge;          // (rev 3) MonsterSizeRules.IsLarge(Radius, isBoss): kept out of the spatial hash
    }

    /// Frozen monster positions for this step, filled in BuildIndex in ascending Id order.
    public sealed class MonsterSnapshotBuffer
    {
        public int Count { get; }
        public ref readonly MonsterSnapshot this[int index] { get; }
        public float MaxHashedRadius { get; }          // max Radius over !IsLarge entries
        public Rect Bounds { get; }                    // (rev 2) AABB of all positions; used by Physics2DTargetProvider
        public bool TryGetIndex(EntityId id, out int index);
        public void Clear();
        public void Add(in MonsterSnapshot snapshot);  // must be called in ascending Id order
    }

    public readonly struct TargetHit
    {
        public readonly EntityId Id;
        public readonly Vector2 Position;
        public readonly float DistanceSquared;
    }

    /// Nearest-monster query for weapons only (monsters use StepContext.PlayerPosition); all implementations must return identical results.
    public interface ITargetProvider
    {
        TargetProviderKind Kind { get; }
        void OnSnapshotBuilt(MonsterSnapshotBuffer snapshot);   // once per step in BuildIndex
        bool TryFindNearest(Vector2 point, out TargetHit hit);  // false when no monster exists; never throws
    }
}

namespace Survivor.Strategies
{
    /// Shared monster spatial hash; rebuilt at most once per step, reused by SpatialHashTargetProvider and SelfCollisionStrategy.
    public sealed class MonsterSpatialIndex : IStepPhaseHandler    // BuildIndex order 10
    {
        public MonsterSpatialIndex(float cellSize, MonsterSnapshotBuffer snapshot);
        public bool Required { get; set; }                          // set by composition root / target switch
        public float CellSize { get; }
        public void OnPhase(SimulationPhase phase, in StepContext context);   // Rebuild when Required
        public void QueryCircle(Vector2 center, float radius, List<int> snapshotIndicesOut); // includes all IsLarge entries
        public void ForEachRing(Vector2 point, int ring, List<int> snapshotIndicesOut);       // for nearest search
        public void Clear();                                        // (rev 3) run reset (TAC-F8-2 step ②)
        public IReadOnlyDictionary<long, int> CellCounts { get; }   // dev visualization
    }

    public sealed class SpatialHash   // generic center-point hash, also used for gems/chests
    {
        public SpatialHash(float cellSize);
        public static long CellOf(Vector2 p, float cellSize);       // R14
        public static long KeyOf(int cx, int cy);                   // R14
        public void Clear();
        public void Insert(int payload, Vector2 position);
        public void QueryCells(Vector2 center, float radius, List<int> payloadsOut);
    }
}
```

- 구현 클래스: `BruteForceTargetProvider`, `SpatialHashTargetProvider(MonsterSpatialIndex)`, `Physics2DTargetProvider(PhysicsBodySynchronizer bodies, int monsterLayerMask)`(TAC-F16-3).
- `TargetProviderSlot : ITargetProvider`(Simulation/Loop, IMP-LOOP)가 현재 공급자를 감싸고 `StepContext.Targets`로 나간다. 교체는 `Begin`의 `SwitchTargetProvider` 대기 명령에서만 한다(TAC-F16-5).
- (4회차) `TargetProviderSlot`은 `ITargetProvider`만 구현하며 `IStepPhaseHandler`가 아니다. BuildIndex 단계의 핸들러(order 0·5·10)가 모두 끝난 직후 `SimulationDriverMid`가 `TargetProviderSlot.OnSnapshotBuilt(snapshot)`을 **직접 호출**한다(5c.3 #9 BuildIndex 행). 그래서 공급자는 항상 확정된 스냅샷·재구축된 공간 해시를 받는다.
- 큰 몬스터는 해시에 들어가지 않고, `QueryCircle`·고리 탐색 뒤에 항상 별도로 전수 검사한다(SPEC F16, R25).

### 5.8 중앙 틱 (Core 계약, IMP-LOOP 구현) (2회차 수정, A8)

```csharp
namespace Survivor.Core
{
    public enum TickStrategyKind { PerObjectFixedUpdate = 0, CentralTick = 1 }
    public enum TickGroup { Monsters = 0, Projectiles = 1 }   // Projectiles = player W1/W4/E1 + enemy M3 projectiles

    /// Per-step logic for monsters and projectiles only (F17 scope).
    public interface IUpdatable
    {
        EntityId Id { get; }
        TickGroup Group { get; }
        void Tick(in StepContext context);   // writes own state only; side effects via context.Commands
    }

    public interface ITickStrategy
    {
        TickStrategyKind Kind { get; }
        void Register(IUpdatable updatable, GameObject host);   // per-object: binds and enables host's self ticker
        void Unregister(IUpdatable updatable);                  // safe during RunGroup
        void RunGroup(TickGroup group, in StepContext context); // per-object: no-op
        int RegisteredCount(TickGroup group);
        void Clear();
    }
}
```

- (2회차) 셀프 티커(`MonsterSelfTicker`, `ProjectileSelfTicker`, Strategies)는 `SelfTickStrategy(IStepClockReader clock)`가 `Register` 때 넣어 준 `IStepClockReader`와 `IUpdatable`만 안다. `FixedUpdate`에서 `if (!_clock.StepActive) return; _owner.Tick(_clock.CurrentContext);`만 한다. 1회차의 정적 `SimulationContextHub`와 `SimulationClock` 직접 참조는 없앴다(Strategies → Simulation 참조 위반이었다). `IStepClockReader`의 구현은 `SimulationClock`이며 조립 루트가 `SelfTickStrategy` 생성자에 넣는다.

### 5.9 충돌 전략 (Core 계약, IMP-COLLISION 구현)

```csharp
namespace Survivor.Core
{
    public enum CollisionStrategyKind { EngineCollider2D = 0, SelfSquaredDistance = 1 }

    public enum CollisionLayer : byte
    { PlayerBody = 0, PlayerMagnet = 1, Monster = 2, PlayerProjectile = 3, PlayerOrb = 4,
      PlayerZone = 5, EnemyProjectile = 6, EnemyZone = 7, Gem = 8, Chest = 9 }

    public enum CollisionChannel : byte
    { PlayerProjectileVsMonster = 0, PlayerOrbVsMonster = 1, PlayerZoneVsMonster = 2,
      PlayerBodyVsMonster = 3, EnemyProjectileVsPlayerBody = 4, EnemyZoneVsPlayerBody = 5,
      PlayerMagnetVsGem = 6, PlayerBodyVsGem = 7, PlayerBodyVsChest = 8 }

    public enum CollisionEventKind : byte { Enter = 0, Exit = 1 }

    /// One Enter/Exit transition of a pair. A is always the body on the channel's A-side layer.
    public readonly struct CollisionEvent : IComparable<CollisionEvent>
    {
        public readonly long Step;
        public readonly CollisionEventKind Kind;
        public readonly CollisionChannel Channel;
        public readonly EntityId A;
        public readonly EntityId B;
        public CollisionEvent(long step, CollisionEventKind kind, CollisionChannel channel, EntityId a, EntityId b);
        public int CompareTo(CollisionEvent other);   // (rev 4) Step, Channel, A, B, Kind — the single sort/dispatch/comparison key (TAC-F18-8)
        public override string ToString();            // (rev 4) CSV row: "step,channel,a,b,kind" (same order as CompareTo)
    }

    /// Mutable circle owned by an entity; strategies read Position/Radius during CollectEvents.
    public sealed class CollisionBody
    {
        public EntityId Id;
        public CollisionLayer Layer;
        public Vector2 Position;
        public float Radius;
        public bool IsLarge;          // (rev 3) MonsterSizeRules.IsLarge(Radius, isBoss) for monsters, false otherwise
        public GameObject Host;       // PhysicsBodySynchronizer toggles CircleCollider2D/Rigidbody2D on it
        public int StrategySlot = -1; // private to the active collision strategy
        public int PhysicsSlot = -1;  // (rev 2) private to PhysicsBodySynchronizer
        public bool DrivesTransform;  // set by PhysicsBodySynchronizer when engine simulation owns the transform
    }

    /// Polling collision detection; both strategies must emit the same sorted event set.
    public interface ICollisionStrategy
    {
        CollisionStrategyKind Kind { get; }
        void Register(CollisionBody body);      // participates from the next CollectEvents
        void Unregister(CollisionBody body);    // drops its overlaps silently (no Exit)
        void NotifyRadiusChanged(CollisionBody body);
        IReadOnlyList<CollisionEvent> CollectEvents(long step); // sorted ascending; list reused until next call
        void Clear();                           // run reset
    }

    /// (rev 2) Registration hook for Unity physics bodies; implemented by Strategies, called by EntityLifecycle.
    public interface IPhysicsBodyRegistry
    {
        void Register(CollisionBody body);      // teleports rb/transform to body.Position; enables collider if required (R19)
        void Unregister(CollisionBody body);    // disables collider (no Exit callbacks: callbacksOnDisable = false)
    }
}
```

- 구현: `SelfCollisionStrategy(MonsterSpatialIndex index, MonsterSnapshotBuffer snapshot)`, `EngineCollisionStrategy(PhysicsBodySynchronizer bodies, CollisionDetectionMode2D mode)`. 엔진판은 `CollisionBodyLink : MonoBehaviour`(Strategies)의 `OnTriggerEnter2D/Exit2D`로 큐를 채운다. 링크는 자기 `CollisionBody`를 알고(동기화기가 `Register` 때 연결), 콜백의 상대 콜라이더에서 상대 링크를 찾아 `CollisionChannelTable`로 채널을 정하고 A/B를 정렬한다.
- 엔진판 바디 구성: `Rigidbody2D.bodyType = Kinematic`, `useFullKinematicContacts = true`, `collisionDetectionMode = mode`(E1은 Discrete, E2는 Discrete와 Continuous), `CircleCollider2D.isTrigger = true` [실측 U5·U6].
- 이벤트를 소비하는 쪽은 `CollisionEventRouter`(IMP-LOOP) 하나다. 채널별로 `ICollisionEventHandler`를 하나씩 등록받는다: 0~2는 `PlayerHitHandler`(IMP-PLAYER), 3~5는 `PlayerDamageReceiver`(IMP-PLAYER), 6~8은 `PickupSystem`(IMP-MONSTER). 피해 적용 로직이 전략별로 중복되지 않는다(SPEC F18, 3.0 I).

```csharp
namespace Survivor.Simulation
{
    public interface ICollisionEventHandler { void Handle(in CollisionEvent e, in StepContext context); }
    public sealed class CollisionEventRouter : IStepPhaseHandler   // Collision order 0 collects, Resolve order 0 dispatches in sorted order
    {
        public CollisionEventRouter(ICollisionStrategy strategy);
        public void SetHandler(CollisionChannel channel, ICollisionEventHandler handler); // one per channel, else throw
        public void ReplaceStrategy(ICollisionStrategy strategy);                         // run boundary only
    }
}
```

#### 5.9.1 물리 바디 동기화와 물리 전역 설정 (Strategies, IMP-COLLISION) (2회차 신규, A6·A12)

```csharp
namespace Survivor.Core
{
    /// (rev 2) What the current strategy selection needs from Unity physics. Single owner: ColliderActivationPolicy (R19).
    public readonly struct PhysicsBodyRequirements
    {
        public readonly bool MonsterColliders;       // Collision == Engine || Target == Physics2DQuery
        public readonly bool AllColliders;           // Collision == Engine
        public readonly bool SyncMonstersAtBuildIndex; // Target == Physics2DQuery
        public readonly bool SimulateAtCollision;    // Collision == Engine
        public bool CollidersEnabledFor(CollisionLayer layer);  // AllColliders || (layer == Monster && MonsterColliders)
    }
    public static class ColliderActivationPolicy { public static PhysicsBodyRequirements Resolve(in StrategySelection s); }
}

namespace Survivor.Strategies
{
    /// The only code that writes Rigidbody2D.position / MovePosition, calls Physics2D.SyncTransforms,
    /// and enables/disables simulation colliders. EngineCollisionStrategy is the only caller of Physics2D.Simulate.
    public sealed class PhysicsBodySynchronizer : IPhysicsBodyRegistry, IStepPhaseHandler   // BuildIndex order 5
    {
        public PhysicsBodySynchronizer(PhysicsBodyRequirements initial);
        public void Register(CollisionBody body);
        public void Unregister(CollisionBody body);
        public void ApplyRequirements(PhysicsBodyRequirements next); // run start or target switch (Begin); toggles colliders in ascending Id
        public void OnPhase(SimulationPhase phase, in StepContext context); // BuildIndex: SyncMonstersForQuery() when SyncMonstersAtBuildIndex
        public void SyncMonstersForQuery();     // rb.position = body.Position for monsters (ascending Id), then Physics2D.SyncTransforms() once
        public void SyncAllForSimulation();     // engine only: MovePosition(body.Position) for every enabled body (ascending Id), radius updates
        public bool TryGetBody(Collider2D collider, out CollisionBody body);  // for Physics2DTargetProvider / CollisionBodyLink
    }

    /// Single owner of every Physics2D global setting (rev 2; replaces PhysicsLayerSetup and the physics half of SimulationSettingsApplier).
    public static class PhysicsSettingsApplier
    {
        // simulationMode = Script; callbacksOnDisable = false; queriesHitTriggers = true; autoSyncTransforms = false;
        // defaultContactOffset = 0.0001f [U30]; layer matrix from CollisionChannelTable via CollisionLayerMapping (R30): only the 9 channels collide
        public static void Apply();
    }
}
```

- **소유자 한 줄 정리**: 바디 위치·콜라이더 켜짐 = `PhysicsBodySynchronizer`, 시뮬레이션 실행 = `EngineCollisionStrategy`, 필요 여부 판정 = `ColliderActivationPolicy`(Core R19), 물리 전역 설정 = `PhysicsSettingsApplier`. 다른 코드는 `Rigidbody2D`·`Collider2D`·`Physics2D`를 건드리지 않는다(`Physics2DTargetProvider`의 `OverlapCircle` 질의는 읽기라 예외).
- 조합별 동작:

| 조합 | BuildIndex(order 5) | Collision |
|---|---|---|
| 자체 충돌 + 비 Physics2D 타깃 | 없음(콜라이더 전부 꺼짐) | 자체 폴링 |
| 자체 충돌 + Physics2D 타깃 | 몬스터 `rb.position` 대입 + `SyncTransforms` | 자체 폴링(Simulate 없음) |
| 엔진 충돌 + 비 Physics2D 타깃 | 없음 | 전 바디 `MovePosition` → `Simulate` |
| 엔진 충돌 + Physics2D 타깃 | 몬스터 `rb.position` 대입 + `SyncTransforms` | 전 바디 `MovePosition`(몬스터는 이미 같은 위치) → `Simulate` |

- **결정론 영향**: ① 순회는 전부 `EntityId` 오름차순이라 Box2D 내부 등록·갱신 순서가 전략 조합 안에서 실행마다 같다. ② `Physics2D` 타깃의 결과는 스냅샷 위치와 R6으로 고르므로 물리 위치가 정확히 같지 않아도(부동소수 동기화 오차) 후보 상위 집합만 맞으면 결과가 같다. 동기화로 1회차의 "1스텝 지연 여유 반경"이 필요 없어졌다. ③ 마지막 행 조합에서는 몬스터가 텔레포트로 움직이므로 Continuous 스윕이 몬스터에 적용되지 않는다. 그래서 E1·E2는 비 Physics2D 타깃으로 돌린다(D30). ④ 측정 비용: BuildIndex 동기화는 `Survivor.Targeting` 마커 안, `SyncAllForSimulation`·`Simulate`는 `Survivor.Collision` 마커 안에서 잰다(R16). 동기화 비용을 그것을 필요로 하는 전략에 귀속시키기 위해서다.

### 5.10 엔티티 생명주기 (Simulation, IMP-LOOP) — 생성·회수의 유일한 경로

```csharp
namespace Survivor.Simulation
{
    /// Base component for pooled simulation entities (no FixedUpdate here).
    public abstract class SimEntity : MonoBehaviour
    {
        public EntityId Id { get; }
        public EntityCategory Category { get; }
        public CollisionBody Body { get; }
        public Vector2 Position { get; protected set; }  // authoritative; mirrors into Body.Position
        public long SpawnStep { get; }
        public long ExpireStep { get; }                  // long.MaxValue when no lifetime; LifetimeSystem despawns when Step >= ExpireStep
        public bool IsConsumed { get; }                  // (rev 2) set in Resolve; later events for this entity are ignored
        internal void Bind(EntityId id, long spawnStep, long expireStep);
        internal void MarkConsumed();
        protected abstract void OnReleased();             // clear references; called before pool release
    }

    /// Single owner of acquire/id/init/register and the exact reverse on despawn. Lifecycle phase or run reset only.
    public sealed class EntityLifecycle
    {
        public EntityLifecycle(IPoolStrategy pool, EntityIdAllocator ids, ITickStrategy ticks,
                               ICollisionStrategy collisions, IPhysicsBodyRegistry physicsBodies,
                               PrefabCatalog prefabs, SimulationClock clock);
        public T Spawn<T>(EntityCategory category, Vector2 position, long lifetimeSteps, Action<T> initialize) where T : SimEntity;
        //   throws InvalidOperationException unless clock.CurrentPhase == Lifecycle or a run reset is in progress (TAC-0-8)
        //   (rev 5) lifetimeSteps == 0 -> no lifetime, ExpireStep = long.MaxValue (W2/E2 orbs); > 0 -> ExpireStep = SpawnStep + lifetimeSteps; < 0 -> ArgumentOutOfRangeException
        //   order: pool.Acquire -> ids.Allocate -> Bind -> initialize(entity) -> ticks.Register (if IUpdatable)
        //          -> physicsBodies.Register(Body) -> collisions.Register(Body) -> registry add (MonsterRegistry / PickupRegistry ...)
        public bool Despawn(EntityId id, DespawnReason reason);
        //   same phase restriction; returns false if !ids.IsLive(id) (already gone; no exception)
        //   order: registry remove -> collisions.Unregister -> physicsBodies.Unregister -> ticks.Unregister
        //          -> Despawned event -> OnReleased -> ids.Free -> pool.Release
        public bool TryGet(EntityId id, out SimEntity entity);
        public void DespawnAll(DespawnReason reason);   // run reset, ascending Id order
        public void ReplaceStrategies(IPoolStrategy pool, ITickStrategy ticks, ICollisionStrategy collisions); // run boundary only, after DespawnAll
        public event Action<SimEntity, DespawnReason> Despawned;   // KillPipeline, trackers, receiver subscribe (TAC-0-10); no dev subscribers
    }
}
```

- (2회차) 생성 위치·생성 스텝·판정 참여는 5.4의 한 문장을 따른다.
- `Despawned` 구독자는 그 대상 ID의 겹침·쿨다운 기록을 지운다(TAC-0-10). 이벤트는 `OnReleased`와 ID 해제 전에 나가므로 구독자가 `Id`를 읽을 수 있다.

### 5.11 데이터 로더·레지스트리·검증 (Data, IMP-DATA)

```csharp
namespace Survivor.Data
{
    public enum DataFileKind { Weapons, Monsters, Combinations, SpawnTable, Passives, LevelUpPool, PlayerStats }
    public enum IssueSeverity { Warning, Blocking }

    public sealed class ValidationIssue
    {
        public IssueSeverity Severity; public string FileName; public string DefinitionId;
        public string FieldPath; public string Message; public int Line; public int Column; // 0 when unknown
        public override string ToString();   // "[weapons.json:12:5] W3.levels[2].zoneRadius: 필수 필드가 없습니다"
    }

    public sealed class DataValidationReport
    {
        public IReadOnlyList<ValidationIssue> Issues { get; }
        public IEnumerable<ValidationIssue> BlockingIssues { get; }
        public bool IsPlayable { get; }   // no Blocking issue and all 4 playability criteria satisfied
        public void Add(ValidationIssue issue);  // also logs (Warning -> LogWarning, Blocking -> LogError)
    }

    /// Immutable loaded data set; consumers never parse JSON.
    public sealed class GameData
    {
        public IReadOnlyList<WeaponDefinition> Weapons { get; }          // file order
        public IReadOnlyList<MonsterDefinition> Monsters { get; }
        public IReadOnlyList<CombinationRule> Combinations { get; }      // empty when file missing/broken
        public SpawnTableDefinition SpawnTable { get; }
        public IReadOnlyList<PassiveDefinition> Passives { get; }        // empty when file missing/broken
        public LevelUpPoolDefinition LevelUpPool { get; }                // UniformFallback when missing/broken
        public PlayerStatsDefinition PlayerStats { get; }
        public bool TryGetWeapon(string id, out WeaponDefinition def);
        public bool TryGetMonster(string id, out MonsterDefinition def);
        public bool TryGetPassive(string id, out PassiveDefinition def);
    }

    public static class GameDataLoader
    {
        public const string DataFolder = "Data";
        /// Loads and validates all 7 files from StreamingAssets/Data; never throws for data errors.
        public static (GameData data, DataValidationReport report) LoadAll(string rootDirectory);
        /// Same pipeline over in-memory texts (EditMode tests).
        public static (GameData data, DataValidationReport report) LoadFromTexts(IReadOnlyDictionary<DataFileKind, string> texts);
        public static string FileNameOf(DataFileKind kind);  // see 5.13 table
    }

    /// Holds the current GameData; hot reload swaps it atomically at a step boundary (Begin).
    public sealed class GameDataRegistry
    {
        public GameData Current { get; }                 // null until the first playable load
        public DataValidationReport LastReport { get; }
        public void Initialize(GameData data, DataValidationReport report); // (rev 4) boot only, once; stores report always, sets Current only when report.IsPlayable; throws if called twice
        public bool TrySwap(GameData data, DataValidationReport report); // refuses when !report.IsPlayable
        public event Action<GameData> Swapped;
    }

    public static class TunnelingInvariantValidator
    {
        /// Stage 1 per-object v/60 <= r; stage 2 pairwise only for stage-1 failures; warnings only.
        public static void Validate(GameData data, DataValidationReport report);
        public static IReadOnlyList<MovingObjectSpec> EnumerateMovingObjects(GameData data); // E3 uses this
    }

    public readonly struct MovingObjectSpec
    { public readonly string Source; public readonly CollisionLayer Layer; public readonly float SpeedPerSecond; public readonly float Radius; }

    [CreateAssetMenu(menuName = "Survivor/Asset Key Registry")]
    public sealed class AssetKeyRegistry : ScriptableObject
    {
        [Serializable] public sealed class Entry { public string Key; public Sprite Sprite; public Color Color = Color.white; public GameObject PrefabOverride; }
        public List<Entry> Entries;
    }

    /// Resolves string keys; unknown keys fall back to the default circle and warn once per key.
    public sealed class AssetKeyResolver
    {
        public AssetKeyResolver(AssetKeyRegistry registryOrNull, DataValidationReport report);
        public VisualAsset Resolve(string key);   // never null
    }

    public readonly struct VisualAsset { public readonly Sprite Sprite; public readonly Color Color; public readonly GameObject PrefabOverride; }
}
```

- (2회차) `MovingObjectSpec.Layer`는 Core의 `CollisionLayer`를 그대로 쓴다. 1회차의 별칭 `CollisionLayerTag`는 C#에서 파일을 넘어 공유되는 형식 별칭을 만들 수 없어 없앴다.
- 파서(2회차, D17 확정): Newtonsoft `JToken.Parse`(`JsonLoadSettings { LineInfoHandling = Load, DuplicatePropertyNameHandling = Error }`)로 트리만 만들고, 바인딩은 리플렉션 없이 손으로 쓴다(필수·알 수 없는 키·타입을 직접 판정). 패키지는 웨이브 0에서 사용자가 추가한다(승인 게이트). 읽기는 TAC-F11-6.
- 파일 단위 정책: 필수 4종(weapons, monsters, spawnTable, playerStats)이 없거나 전체 파싱에 실패하면 Blocking이다. 선택 3종은 Warning이고 폴백한다. 정의 단위 오류는 그 정의만 건너뛰고 Warning이다. 플레이어 기본 스탯은 정의 단위 오류도 Blocking이다(SPEC F11).
- 플레이 불가 4기준: 사용 가능한 무기 0개, 스폰 가능한 몬스터 0종(스폰 테이블이 참조하는 유효 몬스터 기준), 플레이어 기본 스탯 없음, 스폰 테이블 없음. `startingWeaponId`가 유효하지 않으면 플레이어 기본 스탯 오류(Blocking)로 본다 [기본값, D7].
- 에셋 키 해석은 로드 시점에 키를 검증(경고)할 때와 생성 시점에 `SpriteRenderer`에 적용할 때 쓰인다. 기본 도형은 `DefaultShapeFactory.Circle`(런타임에 64×64 `Texture2D`로 만든 원, 마젠타 틴트)이다 [실측 U20].

#### 5.11.0 최초 로드 (4회차 신규)

- **호출 주체**: 부팅 시 최초 로드는 `GameBootstrap.Awake`(App, **IMP-FLOW 소유**, 5c.2)가 한 번 한다. 순서는 `SimulationSettingsApplier.Apply()` → `PhysicsSettingsApplier.Apply()` → `DisplaySetup.Apply()` → `GameDataLoader.LoadAll(Application.streamingAssetsPath)`(**IMP-DATA 소유**) → `GameDataRegistry.Initialize(data, report)` → `report.IsPlayable`이면 `FlowState.Title`, 아니면 `FlowState.DataError`로 F11 시작 차단 화면(TAC-F8-4, `LastReport.BlockingIssues` 표시)이다. `DataError` 상태에서는 `Current`가 null이고 판을 시작할 수 없다. 개발자 모드의 `DevModeEntry`(AfterSceneLoad)는 이 뒤에 붙는다(U40).
- **검증 묶음 호출 순서 (고정)**: `LoadAll`/`LoadFromTexts` 내부에서 항상 ① 파일별 읽기(UTF-8, U+FFFD 경고) → ② `JsonTreeReader`(Newtonsoft 파싱, 파일 단위 실패 판정) → ③ `Binders/*`(필드 바인딩·정의 단위 검증·ID 중복·초→스텝 환산, 7종 고정 순서: PlayerStats, Weapons, Passives, Monsters, SpawnTable, Combinations, LevelUpPool — 참조되는 쪽 먼저) → ④ 파일 간 참조 검증(조합·스폰 테이블·`startingWeaponId`·보스 소환 `monsterId`) → ⑤ `PlayabilityChecker`(플레이 불가 4기준) → ⑥ `TunnelingInvariantValidator.Validate`(경고만) 순이다. 핫 리로드도 같은 `LoadAll`을 쓰므로 같은 순서를 탄다.

#### 5.11.1 핫 리로드 적용 시점 (2회차 신규, Q43 확장)

교체는 `SwapData` 대기 명령으로 다음 스텝 `Begin`에서 일어난다. 그 뒤 각 데이터의 적용 시점:

| 데이터 | 새 정의가 적용되는 때 | 유지되는 것 |
|---|---|---|
| 무기 | 다음 레벨업 선택지 생성, 다음 새 무기 획득 | 보유 무기 인스턴스는 생성 때 받은 정의 유지 |
| 몬스터 | 다음 스폰 | 활성 몬스터는 기존 정의 유지 |
| 스폰 테이블 | 다음 스텝의 스폰 계산부터(`MonsterSpawner`가 매 스텝 `Current.SpawnTable`을 읽는다). `despawnDistance`도 다음 스텝부터(개발자가 D를 덮어쓴 경우는 덮어쓴 값 유지) | 이미 스폰된 몬스터 |
| 패시브 | 다음 선택지 생성 | 보유 패시브의 정의 |
| 조합 규칙 | 다음 진화 판정(상자 획득) | - |
| 레벨업 풀 | 다음 선택지 생성 | 열려 있는 회차의 선택지 3개 |
| 플레이어 기본 스탯 | 다음 참조부터: `moveSpeed`·`pickupRadius`(자석 반지름 갱신)·`invulnerabilitySeconds`·`radius`(바디 반지름 갱신, `NotifyRadiusChanged`)·`xpCurve`(다음 레벨업 판정)·`pickups`(다음 젬·상자 생성) | **현재 체력과 최대 체력**(PM 방향), 현재 레벨·경험치. `startingWeaponId`는 다음 판부터 |

### 5.12 피해·획득 규칙 구성요소 (Simulation, IMP-PLAYER/IMP-MONSTER)

```csharp
namespace Survivor.Simulation
{
    /// Attacker-side per-target cooldown for persistent hitters (W2/E2 orbs, W3 zones). One per weapon instance.
    public sealed class ContactDamageTracker
    {
        public ContactDamageTracker(int hitIntervalSteps);
        public void OnEnter(EntityId target, long step);   // overlapCount++ ; mark for immediate hit check
        public void OnExit(EntityId target);               // overlapCount-- ; keeps nextAllowed record
        /// Appends targets to hit this step in ascending Id order and advances their nextAllowed.
        public void CollectHits(long step, List<EntityId> hitsOut);
        public void Forget(EntityId target);               // Despawned subscriber (TAC-0-10)
        public void SetInterval(int hitIntervalSteps);     // level-up / P1 change
        public void Clear();
    }

    /// Victim-side shared invulnerability for the player (body contact, M3 projectiles, M4 zones).
    public sealed class PlayerDamageReceiver : ICollisionEventHandler, IStepPhaseHandler   // Resolve order 20
    {
        // Enter/Exit for channels 3 and 5 maintain an overlap set; channel 4 Enter is a one-shot candidate (projectile consumed).
        // Resolve: if Rules.PlayerTakesDamage && step >= _invulnerableUntilStep && (pending one-shot || overlap set non-empty):
        //   damage = max over candidates, tie -> smaller EntityId; _invulnerableUntilStep = step + invulnerabilitySteps.
        // While invulnerable, channel-4 Enter consumes the projectile without damage (SPEC 3.0 I).
        // Despawned subscriber removes ids from the overlap set (no Exit on unregister).
        public long InvulnerableUntilStep { get; }
        public void Clear();
    }

    /// Resolve order 10: persistent weapon damage in weapon slot order, each tracker's hits in ascending target Id.
    public sealed class WeaponDamageResolver : IStepPhaseHandler { }
}
```

- 일회성 접촉(W1·W4 투사체 → 몬스터, 젬, 상자): Enter 1회를 처리하고 `MarkConsumed` 후 `Despawn(Consumed)` 명령. 관통은 TAC-F6-9.
- 한 투사체가 같은 스텝에 몬스터 둘과 Enter하면 정렬 순서상 앞(작은 몬스터 ID)만 맞히고, 나머지 이벤트는 투사체가 `IsConsumed`라 무시한다(관통 없음 = 정확히 1회 피해, SPEC F18).
- 이미 hp ≤ 0인 몬스터에 대한 같은 스텝의 추가 피해는 무시한다(처치는 `KillPipeline`에서 1회).

### 5.13 JSON 스키마 7종

공통 규칙:
- 인코딩은 UTF-8(BOM 허용), 최상위는 객체, 첫 필드는 `"schemaVersion": 1`(필수, 정수)이다. (2회차) U+FFFD 감지 시 경고(TAC-F11-6).
- 단위: **시간은 초(`...Seconds`)로 적고 로드 시 스텝으로 환산**(5a R1의 적용 대상 표)한다. **속도는 초당 유닛(`...Speed`)으로 적고 로드 시 /60**(5a R3)한다. 각속도는 **초당 도(`angularSpeedDegPerSec`)로 적고 로드 시 스텝당 라디안**으로 바꾼다. 거리·반지름은 월드 유닛이다(5a R4).
- 확률·비율은 0~1 실수다. 정수 필드에 소수가 오면 타입 오류다.
- ID는 파일 안에서 고유한 문자열이고 대소문자를 구분한다(`^[A-Za-z0-9_]+$`). 다른 파일의 참조는 이 ID로만 한다.
- 알 수 없는 키는 Warning 후 무시한다. 필수 누락·타입 오류·범위 오류(음수 반지름, 0 이하 쿨다운 등)는 그 정의를 건너뛴다(플레이어 스탯은 Blocking).

| 종류 | 파일명 (`StreamingAssets/Data/`) | 필수 |
|---|---|---|
| 무기 | `weapons.json` | 필수 |
| 몬스터 | `monsters.json` | 필수 |
| 조합 규칙 | `combinations.json` | 선택 |
| 스폰 테이블 | `spawnTable.json` | 필수 |
| 패시브 | `passives.json` | 선택 |
| 레벨업 선택지 풀 | `levelUpPool.json` | 선택 |
| 플레이어 기본 스탯 | `playerStats.json` | 필수 |

#### 5.13.1 무기 (`weapons.json`)

최상위: `{ "schemaVersion": 1, "weapons": [WeaponDef, ...] }`

WeaponDef

| 필드 | 타입 | 단위 | 필수 | 설명 |
|---|---|---|---|---|
| `id` | string | - | 필수 | 고유 ID (`W1`, `E1` 등) |
| `displayName` | string | - | 필수 | 한국어 표시명 |
| `behavior` | string enum | - | 필수 | `projectile` / `homing` / `orbit` / `zone`. (2회차) W4는 `homing`으로만 표현한다 |
| `assetKey` | string | - | 필수 | 투사체·궤도 원·장판의 시각 키 |
| `iconKey` | string | - | 선택 | 선택지 UI 아이콘 키(없으면 `assetKey`) |
| `evolvedOnly` | bool | - | 선택(기본 false) | true면 진화로만 얻는다. 선택지·상자 강화 제외, `levels` 길이는 정확히 1 |
| `levels` | WeaponLevel[] | - | 필수 | 기본 무기는 정확히 5개, 진화 무기는 정확히 1개. i번째 = (i+1)단계의 **절대값** |

WeaponLevel (행동 타입별 필수 필드)

| 필드 | 타입 | 단위 → 로드 후 | projectile | homing | orbit | zone |
|---|---|---|---|---|---|---|
| `damage` | number > 0 | 피해량 | 필수 | 필수 | 필수 | 필수 |
| `cooldownSeconds` | number > 0 | 초 → 스텝(최소 1) | 필수 | 필수 | - | 필수(생성 주기) |
| `projectileCount` | int ≥ 1 | 개 | 선택(1) | 선택(1) | - | - |
| `spreadDegrees` | number ≥ 0 | 도 | 선택(0) | 선택(0) | - | - |
| `projectileSpeed` | number > 0 | 유닛/초 → 유닛/스텝 | 필수 | 필수 | - | - |
| `projectileRadius` | number > 0 | 유닛 | 필수 | 필수 | - | - |
| `lifetimeSeconds` | number > 0 | 초 → 스텝(최소 1) | 필수 | 필수 | - | - |
| `pierceCount` | int ≥ 0 | 회 | 선택(0) | 선택(0) | - | - |
| `orbCount` | int ≥ 1 | 개 | - | - | 필수 | - |
| `orbRadius` | number > 0 | 유닛 | - | - | 필수 | - |
| `orbitRadius` | number > 0 | 유닛 | - | - | 필수 | - |
| `angularSpeedDegPerSec` | number | 도/초 → 라디안/스텝 | - | - | 필수 | - |
| `hitIntervalSeconds` | number > 0 | 초 → 스텝(최소 1) (W2·W3 피해 틱 간격, SPEC F6·F11 4회차) | - | - | 필수 | 필수 |
| `zoneRadius` | number > 0 | 유닛 | - | - | - | 필수 |
| `durationSeconds` | number > 0 | 초 → 스텝(최소 1) | - | - | - | 필수 |
| `placement` | string enum | `nearestEnemy` / `player` | - | - | - | 선택(`nearestEnemy`) [Q8] |

행동 타입에 해당하지 않는 필드는 알 수 없는 키처럼 Warning 후 무시한다.

- **진화 무기 표현**: E1은 `behavior: "projectile"`, `evolvedOnly: true`이고 `levels` 1개(W1 5단계보다 작은 `cooldownSeconds`, 필요하면 `projectileCount` 증가)다. E2는 `behavior: "orbit"`, `evolvedOnly: true`이고 W2 5단계보다 큰 `orbitRadius`·`orbRadius`를 갖는다. 진화 관계 자체는 `combinations.json`에만 있다.

예시:
```json
{
  "schemaVersion": 1,
  "weapons": [
    {
      "id": "W1", "displayName": "투사체", "behavior": "projectile", "assetKey": "shape.projectile.white",
      "levels": [
        { "damage": 10, "cooldownSeconds": 1.0, "projectileSpeed": 12, "projectileRadius": 0.25, "lifetimeSeconds": 2.0 },
        { "damage": 12, "cooldownSeconds": 0.9, "projectileSpeed": 12, "projectileRadius": 0.25, "lifetimeSeconds": 2.0 },
        { "damage": 14, "cooldownSeconds": 0.8, "projectileSpeed": 12, "projectileRadius": 0.25, "lifetimeSeconds": 2.0 },
        { "damage": 16, "cooldownSeconds": 0.7, "projectileSpeed": 12, "projectileRadius": 0.25, "lifetimeSeconds": 2.0, "projectileCount": 2, "spreadDegrees": 10 },
        { "damage": 20, "cooldownSeconds": 0.6, "projectileSpeed": 12, "projectileRadius": 0.25, "lifetimeSeconds": 2.0, "projectileCount": 2, "spreadDegrees": 10 }
      ]
    },
    {
      "id": "W2", "displayName": "근접 회전", "behavior": "orbit", "assetKey": "shape.orb.cyan",
      "levels": [
        { "damage": 8, "orbCount": 2, "orbRadius": 0.4, "orbitRadius": 2.0, "angularSpeedDegPerSec": 180, "hitIntervalSeconds": 0.5 }
      ]
    },
    {
      "id": "W3", "displayName": "장판", "behavior": "zone", "assetKey": "shape.zone.green",
      "levels": [
        { "damage": 6, "cooldownSeconds": 3.0, "zoneRadius": 1.5, "durationSeconds": 2.0, "hitIntervalSeconds": 0.5 }
      ]
    },
    {
      "id": "W4", "displayName": "유도 투사체", "behavior": "homing", "assetKey": "shape.projectile.pink",
      "levels": [
        { "damage": 8, "cooldownSeconds": 1.5, "projectileSpeed": 9, "projectileRadius": 0.2, "lifetimeSeconds": 3.0 }
      ]
    },
    {
      "id": "E1", "displayName": "연사 투사체", "behavior": "projectile", "assetKey": "shape.projectile.gold", "evolvedOnly": true,
      "levels": [
        { "damage": 22, "cooldownSeconds": 0.2, "projectileSpeed": 14, "projectileRadius": 0.25, "lifetimeSeconds": 2.0, "projectileCount": 3, "spreadDegrees": 12 }
      ]
    }
  ]
}
```
(W2·W3·W4 예시는 지면상 1단계만 보였다. 실제 파일에서는 기본 무기 `levels`가 반드시 5개이고, 아니면 그 정의를 건너뛴다. 수치는 Q1 밸런싱 대상이다. 불변식 확인: W1 12/60 = 0.2 ≤ 0.25, W4 9/60 = 0.15 ≤ 0.2, W2 선속도 = π·2.0 ≈ 6.28/초 → 0.105/스텝 ≤ 0.4, W3 정지 0 ≤ 1.5 통과.)

#### 5.13.2 몬스터 (`monsters.json`)

최상위: `{ "schemaVersion": 1, "monsters": [MonsterDef, ...] }`

| 필드 | 타입 | 단위 | 필수 | 해당 행동 |
|---|---|---|---|---|
| `id`, `displayName`, `assetKey` | string | - | 필수 | 전부 |
| `behavior` | enum | `chaser`/`charger`/`ranged`/`boss` | 필수 | 전부 |
| `maxHealth` | number > 0 | HP | 필수 | 전부 |
| `moveSpeed` | number ≥ 0 | 유닛/초 → /스텝 | 필수 | 전부 |
| `radius` | number > 0 | 유닛. (3회차) `radius > 1.0`이거나 `behavior == boss`이면(R25) "큰 몬스터"로 공간 해시에서 빠진다 | 필수 | 전부 |
| `contactDamage` | number ≥ 0 | 피해량 | 필수 | 전부 |
| `charge` | object | - | charger만 필수 | `{ triggerDistance(유닛), chargeSpeed(유닛/초), durationSeconds, cooldownSeconds }` |
| `ranged` | object | - | ranged만 필수 | `{ attackRange(유닛), fireCooldownSeconds, projectile: { assetKey, speed(유닛/초), radius, damage, lifetimeSeconds } }` |
| `bossPatterns` | object[] | - | boss만 필수(1개 이상) | 아래 |

보스 패턴 (`bossPatterns[]`, `type`으로 구분)

| type | 필드 | 의미 |
|---|---|---|
| `hazardZone` (패턴 ①) | `cooldownSeconds`, `zoneRadius`, `durationSeconds`, `damage`, `assetKey`, `placement`(`player` 기본) | 쿨다운마다 플레이어 위치에 적 장판(EnemyZone) 생성 명령. 피해는 플레이어 무적 규칙을 따른다 |
| `summon` (패턴 ②) | `cooldownSeconds`, `monsterId`, `count`, `summonRadius` | 쿨다운마다 보스 주변 원주(각도는 Summon 스트림)에 `monsterId`를 `count`기 소환 명령. `monsterId`는 boss가 아닌 몬스터여야 한다(참조 오류 검사) |

예시:
```json
{
  "schemaVersion": 1,
  "monsters": [
    { "id": "M1", "displayName": "좀비", "behavior": "chaser", "assetKey": "shape.monster.green",
      "maxHealth": 10, "moveSpeed": 2.0, "radius": 0.45, "contactDamage": 5 },
    { "id": "M2", "displayName": "돌진체", "behavior": "charger", "assetKey": "shape.monster.orange",
      "maxHealth": 20, "moveSpeed": 1.5, "radius": 0.5, "contactDamage": 8,
      "charge": { "triggerDistance": 6, "chargeSpeed": 18, "durationSeconds": 0.5, "cooldownSeconds": 3 } },
    { "id": "M3", "displayName": "원거리", "behavior": "ranged", "assetKey": "shape.monster.purple",
      "maxHealth": 15, "moveSpeed": 1.8, "radius": 0.45, "contactDamage": 4,
      "ranged": { "attackRange": 8, "fireCooldownSeconds": 2,
                  "projectile": { "assetKey": "shape.enemyshot.red", "speed": 6, "radius": 0.2, "damage": 6, "lifetimeSeconds": 3 } } },
    { "id": "M4", "displayName": "보스", "behavior": "boss", "assetKey": "shape.boss.red",
      "maxHealth": 5000, "moveSpeed": 1.2, "radius": 1.5, "contactDamage": 20,
      "bossPatterns": [
        { "type": "hazardZone", "cooldownSeconds": 6, "zoneRadius": 3, "durationSeconds": 2, "damage": 15, "assetKey": "shape.hazard.red" },
        { "type": "summon", "cooldownSeconds": 8, "monsterId": "M1", "count": 6, "summonRadius": 3 }
      ] }
  ]
}
```
(불변식 확인: M2 돌진 18/60 = 0.3 ≤ 0.5 통과, M3 투사체 0.1 ≤ 0.2 통과.)

#### 5.13.3 조합 규칙 (`combinations.json`, 선택)

`{ "schemaVersion": 1, "combinations": [ { "id": string, "weaponId": string, "passiveId": string, "resultWeaponId": string } ] }` — 모든 필드 필수.
검증: `weaponId`는 `evolvedOnly == false`인 유효 무기, `passiveId`는 유효 패시브, `resultWeaponId`는 `evolvedOnly == true`인 유효 무기여야 한다. 같은 `weaponId`를 쓰는 규칙이 둘이면 둘째를 건너뛴다(Warning). 패시브 파일이 없으면 모든 규칙이 참조 오류로 건너뛰어진다.
```json
{ "schemaVersion": 1, "combinations": [
  { "id": "evolve_W1", "weaponId": "W1", "passiveId": "P1", "resultWeaponId": "E1" },
  { "id": "evolve_W2", "weaponId": "W2", "passiveId": "P3", "resultWeaponId": "E2" } ] }
```

#### 5.13.4 스폰 테이블 (`spawnTable.json`, 필수)

| 필드 | 타입 | 단위 | 필수 | 설명 |
|---|---|---|---|---|
| `despawnDistance` | number > spawnRadius(20) | 유닛 | 필수 | 기본 45. 개발자 모드가 덮어쓸 수 있다(R26) |
| `segments` | Segment[] | - | 필수(1개 이상) | 시간 오름차순, 겹치지 않음 |
| `segments[].startSeconds` | number ≥ 0 | 초 → 스텝(0 허용) | 필수 | 포함 |
| `segments[].endSeconds` | number | 초 → 스텝 | 필수 | 미포함. 600을 넘으면 600으로 잘라내고 Warning |
| `segments[].spawnsPerSecond` | number > 0 | 기/초 | 필수 | |
| `segments[].entries` | `{ monsterId, weight }`[] | weight > 0 | 필수 | boss 행동 몬스터는 금지(참조 오류) |
| `bossMonsterId` | string | - | 필수 | boss 행동 몬스터. 무효면 Warning [기본값, D6 — 소프트락, TAC-F8-10] |

스폰 반경 20, 보스 등장 600초, 상한 400은 SPEC의 규칙이라 `GameRules` 코드 상수로 둔다(JSON 아님).
```json
{ "schemaVersion": 1, "despawnDistance": 45, "bossMonsterId": "M4",
  "segments": [
    { "startSeconds": 0,   "endSeconds": 120, "spawnsPerSecond": 2,  "entries": [ { "monsterId": "M1", "weight": 1 } ] },
    { "startSeconds": 120, "endSeconds": 300, "spawnsPerSecond": 4,  "entries": [ { "monsterId": "M1", "weight": 3 }, { "monsterId": "M2", "weight": 1 } ] },
    { "startSeconds": 300, "endSeconds": 480, "spawnsPerSecond": 7,  "entries": [ { "monsterId": "M1", "weight": 4 }, { "monsterId": "M2", "weight": 2 }, { "monsterId": "M3", "weight": 1 } ] },
    { "startSeconds": 480, "endSeconds": 600, "spawnsPerSecond": 10, "entries": [ { "monsterId": "M1", "weight": 4 }, { "monsterId": "M2", "weight": 2 }, { "monsterId": "M3", "weight": 2 } ] } ] }
```

#### 5.13.5 패시브 (`passives.json`, 선택)

`{ "schemaVersion": 1, "passives": [ { "id", "displayName", "iconKey", "stat", "levels": [ { "bonus": number } ×5 ] } ] }`

| 필드 | 타입 | 필수 | 설명 |
|---|---|---|---|
| `stat` | enum `attackSpeed` / `moveSpeed` / `pickupRadius` | 필수 | P1 / P2 / P3에 대응 |
| `levels[].bonus` | number ≥ 0 | 필수, 정확히 5개 | 그 단계의 **누적 절대** 보너스 비율(0.10 = +10%) |

적용 식은 5a R13(`StatMath`) 하나뿐이다.
```json
{ "schemaVersion": 1, "passives": [
  { "id": "P1", "displayName": "공격 속도", "iconKey": "icon.p1", "stat": "attackSpeed",
    "levels": [ { "bonus": 0.10 }, { "bonus": 0.20 }, { "bonus": 0.30 }, { "bonus": 0.40 }, { "bonus": 0.50 } ] } ] }
```

#### 5.13.6 레벨업 선택지 풀 (`levelUpPool.json`, 선택)

| 필드 | 타입 | 필수 | 설명 |
|---|---|---|---|
| `defaultWeight` | number > 0 | 필수 | 규칙에 걸리지 않는 후보의 가중치 |
| `healBaseWeight` | number ≥ 0 | 필수 | `w_base` (Q38) |
| `rules` | `{ kind, targetId, weight }`[] | 선택 | `kind`: `weaponNew`/`weaponUpgrade`/`passiveNew`/`passiveUpgrade`. `targetId`: ID 또는 `"*"`. 구체 ID 규칙이 `"*"`보다 우선한다 |

파일이 없거나 깨지면 `UniformFallback`(`defaultWeight = 1`, `healBaseWeight = 1`, 규칙 없음)이 된다. 회복은 이때도 R8의 `(1 − r)²`을 유지한다(SPEC F5 4회차). 회복량 30%는 규칙이라 `GameRules.HealRatio`다.
```json
{ "schemaVersion": 1, "defaultWeight": 10, "healBaseWeight": 30,
  "rules": [ { "kind": "weaponNew", "targetId": "*", "weight": 8 }, { "kind": "weaponUpgrade", "targetId": "W1", "weight": 12 } ] }
```

#### 5.13.7 플레이어 기본 스탯 (`playerStats.json`, 필수·단일 정의)

| 필드 | 타입 | 단위 | 필수 |
|---|---|---|---|
| `maxHealth` | number > 0 | HP | 필수 |
| `moveSpeed` | number > 0 | 유닛/초 | 필수 |
| `radius` | number > 0 | 유닛 | 필수 |
| `pickupRadius` | number > 0 | 유닛 (자석 반경 기본값, P3 적용 전) | 필수 |
| `invulnerabilitySeconds` | number ≥ 0 | 초 → 스텝, R1 `SecondsToSteps`(0 허용). 공통 무적 시간(SPEC 3.0 I, F11 4회차) | 필수 |
| `startingWeaponId` | string | 무기 ID (`evolvedOnly`는 금지) | 필수 |
| `xpCurve` | `{ "thresholds": int[], "growthAfterTable": int }` | (2회차 명확화) 레벨 L→L+1에 필요한 경험치 = `thresholds[L-1]`, 표를 넘어가면 마지막 값 + `growthAfterTable × 넘어간 레벨 수`. 레벨업 때 필요량을 **빼고** 남은 경험치를 이월한다(R21) | 필수 |
| `pickups` | `{ gemAssetKey, gemRadius, gemXpValue(int), gemAttractSpeed(유닛/초), chestAssetKey, chestRadius }` | - | 필수(전부) |

어떤 오류든 Blocking이다. 무기·패시브 슬롯 수(4/3), 상자 확률 2%, 강화 70%, 회복 30%는 규칙이라 `GameRules`다.
```json
{ "schemaVersion": 1, "maxHealth": 100, "moveSpeed": 5, "radius": 0.5, "pickupRadius": 2.0,
  "invulnerabilitySeconds": 0.5, "startingWeaponId": "W1",
  "xpCurve": { "thresholds": [5, 8, 12, 16, 20, 25, 30], "growthAfterTable": 5 },
  "pickups": { "gemAssetKey": "shape.gem.blue", "gemRadius": 0.2, "gemXpValue": 1, "gemAttractSpeed": 10,
               "chestAssetKey": "shape.chest.yellow", "chestRadius": 0.5 } }
```
(불변식 확인: 플레이어 5/60 ≈ 0.083 ≤ 0.5, 젬 끌림 10/60 ≈ 0.167 ≤ 0.2 통과. 플레이어와 젬은 SPEC의 검사 목록에 없지만 포함한다, D1.)

### 5.14 레벨업·진화·상자·진행 적용 (Simulation) (2회차 재작성, A4·A5·A11)

```csharp
namespace Survivor.Simulation
{
    public enum UpgradeKind { WeaponUpgrade = 0, WeaponNew = 1, PassiveUpgrade = 2, PassiveNew = 3, Heal = 4 }
    public readonly struct UpgradeOption { public readonly UpgradeKind Kind; public readonly string TargetId; public readonly bool IsFiller; }
    public readonly struct WeaponLoadoutEntry { public readonly string WeaponId; public readonly int Level; }   // Level 1-based

    public sealed class ExperienceSystem   // IMP-FLOW
    {
        public int Level { get; }          // starts at 1
        public int Xp { get; }             // progress toward the next level
        public void AddXp(int amount);     // PickupSystem (Resolve), dev command (Begin)
        /// If Xp >= XpCurve.RequiredForNext(Level): Xp -= required; Level++; return true. At most one level per call.
        public bool TryBeginLevelUp(XpCurveDefinition curve);
        public void Reset();
    }

    /// Lifecycle order 90. Evaluates death > clear > level-up once per step and closes the gate (A3).
    public sealed class StepOutcomeGate : IStepPhaseHandler   // IMP-FLOW
    {
        public RunOutcome PendingOutcome { get; }   // Death/Clear with SurvivalSteps = context.Step + 1; None otherwise
    }

    /// One level-up round at a time; accepts only the first submission per round (R29).
    public sealed class LevelUpSession   // IMP-FLOW
    {
        public long RoundId { get; }                       // 0 = none; increments per round within a run
        public bool IsOpen { get; }
        public IReadOnlyList<UpgradeOption> Options { get; } // exactly 3, fixed for the round (kept across pause)
        public int OpenedFrame { get; }
        public void Open(IReadOnlyList<UpgradeOption> options3, int frame);
        public bool TrySubmit(long roundId, int optionIndex, int frame, out UpgradeOption chosen);
        //   false when !IsOpen, roundId != RoundId, already submitted, frame <= OpenedFrame, or index out of range
        public void Close();
        public void Reset();
    }

    public static class LevelUpCandidateBuilder   // IMP-FLOW
    {
        /// Returns candidates in canonical order (R12) with weights; heal included only when weight > 0. Uses R22 for exclusions.
        public static void Build(GameData data, WeaponInventory weapons, PassiveInventory passives,
                                 float health, float maxHealth, List<(UpgradeOption option, float weight)> output);
    }
    public static class LevelUpSelector           // IMP-FLOW
    {
        /// Draws exactly 3 options: weighted without replacement, then heal fillers.
        public static void Draw(List<(UpgradeOption option, float weight)> candidates, DeterministicRng rng, List<UpgradeOption> output3);
    }

    /// The only writer of WeaponInventory / PassiveInventory / heal (A11). FLOW and DEV only call it.
    public sealed class PlayerProgressionApplier   // IMP-PLAYER
    {
        public void ApplyLevelUpOption(in UpgradeOption option);
        //   only while clock.CloseReason == LevelUp && !clock.StepActive (TAC-0-1 ②); throws otherwise
        //   WeaponNew -> add level-1 instance to the first free slot; WeaponUpgrade/PassiveUpgrade -> level+1 (R22 checked);
        //   PassiveNew -> add level 1; Heal -> Heal()
        public void ApplyChestUpgrade(int slotIndex);                          // Resolve (ChestEffectResolver)
        public void ApplyEvolution(int slotIndex, CombinationRule rule, in StepContext context); // Resolve; enqueues Despawn(Replaced) for children
        public void Heal();                                                    // R23 — chest ② and level-up Heal
        public void SetLoadout(IReadOnlyList<WeaponLoadoutEntry> loadout, in StepContext context); // dev, Begin only; ≤ 4 entries, slot order = list order
        public void SetHealth(float hp);                                       // dev, Begin only; clamped to [0, maxHp]
    }

    public sealed class EvolutionService          // IMP-PLAYER
    {
        public bool TryFindFirstEligible(out int slotIndex, out CombinationRule rule); // weapon slot order (Q42)
    }
    public sealed class ChestEffectResolver       // IMP-PLAYER
    {
        public ChestEffect Resolve(DeterministicRng chestRng, in StepContext context); // ① evolve ② 70/30, applies via PlayerProgressionApplier
    }
    public enum ChestEffect { Evolved, WeaponUpgraded, Healed }
}
```

- **레벨업 흐름 (A4)**: TAC-F5-4의 1~4단계. FLOW(`GameFlowController.Update`)는 `clock.CloseSequence`가 바뀌었고 `CloseReason == LevelUp`이면 회차를 연다. 선택이 수락되면 같은 `Update` 안에서 `ApplyLevelUpOption` → `TryBeginLevelUp` 재평가 → (참) 새 회차 `Open`(후보는 적용 후 상태) / (거짓) `clock.Open()`.
- **입력 (A5)**: `UiInputHandler`(키 1/2/3)와 `LevelUpView`(버튼 클릭·Enter)는 둘 다 뷰가 들고 있는 `RoundId`로 `TrySubmit`을 부르는 이벤트만 낸다. 수락 여부 판단은 `LevelUpSession` 한 곳이다(R29).
- **개발자 무기 구성 (A11)**: `DevController` → `PendingCommandQueue.Enqueue(SetLoadout)` → 다음 스텝 Begin에서 `PlayerProgressionApplier.SetLoadout`. 빠진 무기의 자식 엔티티는 `Despawn(Replaced)` 명령, 새 무기의 자식은 그 스텝 WeaponTick에서 생성 명령(TAC-F6-8). 새 무기의 `_nextFireStep`은 현재 스텝이다.
- 진화로 교체된 원본 무기 ID는 `WeaponInventory.ConsumedByEvolution`에 남아 D5 제외 규칙에 쓰인다.

### 5.15 개발자 모드 주입 경계와 측정 (Dev, IMP-DEV)

게임 쪽에 미리 있어야 하는 주입 지점(이 지점들은 개발자 모드가 꺼져 있어도 존재하는 "값"이다):

```csharp
namespace Survivor.Core
{
    /// Runtime-tunable values read by gameplay; defaults equal normal play. Dev mode only mutates these.
    public sealed class SimulationRules
    {
        public int MonsterCap = GameRules.MonsterCapDefault;          // 400; dev may set int.MaxValue
        public float DespawnDistance;                                 // from spawnTable.json; dev overrides via R26
        public bool DespawnDistanceOverridden;                        // (rev 2) hot reload keeps the override
        public bool PlayerTakesDamage = true;
        public bool WeaponsEnabled = true;
        public bool LevelUpEnabled = true;
        public long StopAtStep = long.MaxValue;
        public bool RecordBestTime = true;                            // (rev 2) dev attach sets false (SPEC F8)
        public bool AutoPauseOnFocusLoss = true;                      // (rev 2) measurement sets false (SPEC F8·F19)
        public float ChestDropChance = GameRules.ChestDropChance;     // (rev 3) 0.02; measurement presets set 0 (SPEC F10)
    }

    public interface ISpawnPolicy   // normal: TableSpawnPolicy (IMP-MONSTER); dev: DevSpawnPolicy
    {
        /// Fills spawn requests for this step; MonsterSpawner applies cap and placement.
        void CollectSpawns(in StepContext context, int liveMonsterCount, DeterministicRng spawnRng, List<string> monsterIdsOut);
        bool BossSpawnDue(long step);
    }

    public readonly struct StrategySelection
    {
        public readonly PoolStrategyKind Pool; public readonly TargetProviderKind Target;
        public readonly TickStrategyKind Tick; public readonly CollisionStrategyKind Collision;
        public readonly CollisionDetectionMode2D EngineDetectionMode;   // Discrete by default
        public readonly float SpatialHashCellSize;                      // (rev 2) recorded as cellSize
        public static StrategySelection ShippingDefault { get; }        // CustomPool, SpatialHash, CentralTick, SelfSquaredDistance
        public static StrategySelection Baseline { get; }               // InstantiateDestroy, BruteForce, PerObjectFixedUpdate, EngineCollider2D
    }
}

namespace Survivor.App
{
    /// (rev 2) Everything dev mode may touch; implemented by GameBootstrap. App never references Survivor.Dev.
    public interface IDevModeHost
    {
        SimulationRules Rules { get; }
        SimulationClock Clock { get; }
        PendingCommandQueue Commands { get; }
        GameDataRegistry Data { get; }
        RunSession Session { get; }
        MonsterSpatialIndex SpatialIndex { get; }          // visualization (read only)
        StrategySelection NextRunSelection { get; set; }   // pool/tick/collision applied at next Reset
        Func<ICollisionStrategy, ICollisionStrategy> NextRunCollisionDecorator { get; set; } // null = none
        IInputSource InputOverride { get; set; }           // (rev 3) null = keyboard; bound once at Reset (run start) into PlayerMovement, never null-checked per step
        ISpawnPolicy SpawnPolicyOverride { get; set; }     // (rev 3) null = table; bound once at Reset (run start) into MonsterSpawner, never null-checked per step
        ulong? FixedSeed { get; set; }
        void StartNewRun();                                // Reset + Open, from Title/Result/Paused
        event Action<RunOutcome> RunEnded;
    }
}
```

- (3회차) `InputOverride`·`SpawnPolicyOverride`는 값을 바꿔도 진행 중인 판에는 적용되지 않고 다음 `Reset`(판 시작)에서 1회 바인딩된다(TAC-F8-2 ④). 그래서 개발자 모드가 꺼진 플레이에서는 스텝마다 오버라이드 null 검사가 돌지 않는다(SPEC F19 "비활성 상태에서는 분기 없음").

`PendingCommandQueue`(Simulation, Begin order 0) 명령 종류: `SetLoadout`, `SetHealth`, `AddXp`, `LevelUpNow`(다음 레벨까지 경험치 추가), `SpawnMonstersNow`(SimulationCommand로 변환), `SpawnBossNow`, `SpawnChestNow`, `SwapData`, `SwitchTargetProvider`, `SetDespawnDistance`.

개발자 쪽 구성요소:

| 클래스 | 역할 |
|---|---|
| `DevModeEntry` (static, 2회차) | `[RuntimeInitializeOnLoadMethod(AfterSceneLoad)]`. TAC-F19-1. 붙을 때 `Rules.RecordBestTime = false` |
| `DevModeActivation` (static) | `IsRequested(string[] args)` → `args`에 `--dev`가 있는지(대소문자 무시). 에디터에서는 `EditorPrefs` `Survivor.DevModeOnPlay`도 본다(`#if UNITY_EDITOR`) |
| `DevModeRoot : MonoBehaviour` | 활성일 때만 생성. `Update`에서 F1로 오버레이를 토글하고 `StopAtStep` 도달·측정 세션을 감시 |
| `DevOverlay : MonoBehaviour` | IMGUI 패널(한국어 레이블, 한글 `Font`). 숨기면 `enabled = false`(`OnGUI` 호출 0). 전략 토글 4개 + CCD 모드 + 셀 크기, 시드, 레벨업 비활성, 자동 경로(반지름·속도), 무적, 무기 구성, 공격 on/off, 스폰 제어, D, 상한 해제, 시각화 4종, 배속 0.25~3x와 실제 배속, 시간 점프, 한 스텝 진행, 체력·경험치·레벨·보스·상자, 핫 리로드, 덤프, 충돌 로그 on/off, 측정 프리셋·시작 |
| `DevController` | 오버레이 조작을 `IDevModeHost` 호출로 바꾼다. N을 바꾸면 D를 R26으로 자동 설정 |
| `DevSpawnPolicy : ISpawnPolicy` | 스폰 중단 / 특정 종만 / N 유지(죽거나 디스폰되면 같은 스텝 Lifecycle에 보충) / 즉시 N기. 보스 자동 등장 없음 |
| `AutoPathInputSource : IInputSource` | 5.5 |
| `LoggingCollisionStrategy : ICollisionStrategy` | 실제 전략을 감싸 `CollectEvents` 결과를 `CollisionEventLog`에 복사(데코레이터). 다음 판 시작 때만 설치된다 |
| `CollisionEventLog` | 메모리 버퍼 → `collision_<seed>_<strategy>_<mode>.csv`(R28). 헤더: `step,channel,a,b,kind` ((4회차) `CollisionEvent.CompareTo`·`ToString` 순서) |
| `StatsCollector` | `ProfilerRecorder`: 마커 전부, `"GC Allocated In Frame"`, `"Total Used Memory"`, `"Total Reserved Memory"`. 프레임 시간은 `Time.unscaledDeltaTime`. 워밍업(TAC-F19-7) [실측 U14·U15·U38] |
| `MeasurementSession` | 5.15.2 |
| `MeasurementGuard` (2회차) | 시작 거부 조건 판정과 원인 문자열 |
| `MeasurementPresets` (2회차, 3회차 수정) | A·B·C 16회 + A0 구성(TAC-F10-4). 프리셋마다 `StrategySelection`, 고정 무기 구성(W1~W4 단계), `ChestDropChance = 0`을 함께 가진다 |
| `MeasurementMath` (2회차) | R27, 1% low, 최악 프레임 |
| `MeasurementCsvExporter` | 5.15.3. 저장 경로를 오버레이에 표시 |
| `CsvWriter` (2회차) | R28 |
| `DevVisualizer` | URP `endCameraRendering` + GL(TAC-F19-4): 해시 셀 + 셀별 수(IMGUI 레이블, 오버레이가 열려 있을 때만), 충돌 반경, 무기 → 타깃 선(`WeaponInstance.LastTargetId` 읽기 전용), 스폰·디스폰 원 |
| `HotReloadService` | `GameDataLoader.LoadAll` → 플레이 가능하면 `SwapData` 대기 명령 |
| `DataDumpWriter` | 로드된 정의(초·스텝 값 모두)와 `DataValidationReport`를 `persistentDataPath/dump_<timestamp>.txt`로 |

#### 5.15.1 배속 (2회차, A2)

`DevController.SetTimeScale(x)`는 `Mathf.Clamp(x, 0.25f, 3f)`를 `Time.timeScale`에 쓴다. 실제 배속 = 최근 1초(비스케일) 동안의 `StepsThisFrame` 합 / 60. 측정 중에는 1로 고정되고 변경 시 세션이 무효가 된다.

#### 5.15.2 측정 세션 절차 (2회차 재작성, A9, 3회차 수정)

1. 사용자가 오버레이에서 프리셋(A0의 N, A1~A6, B1~B8, C1~C2)과 `durationSteps`를 고르고 "측정 시작"을 누른다.
2. `MeasurementGuard.Check`: 시각화 하나라도 켜짐 / 충돌 로그 옵션 켜짐 / `timeScale != 1` / 한 스텝 진행 모드 / (3회차) 프리셋에 고정 무기 구성이 없거나 프리셋의 `ChestDropChance != 0` → 거부, 원인을 오버레이에 표시(SPEC F10·F19 4회차 배제 조건).
3. 통과하면: 오버레이 숨김 → 환경 적용(vSync 0, targetFrameRate −1, runInBackground true, AutoPauseOnFocusLoss false) → `NextRunSelection = preset`, `NextRunCollisionDecorator = null` → 규칙 설정(고정 시드, LevelUpEnabled false, AutoPath, PlayerTakesDamage false, (3회차) `ChestDropChance = 0`, D = R26(N), N > 400이면 상한 해제) → (3회차) `InputOverride = AutoPathInputSource`, `SpawnPolicyOverride = DevSpawnPolicy`(스폰 중단 + 즉시 N기 + N 유지)를 **판 시작 전에** 설정 → `StartNewRun()`(이 `Reset`에서 두 오버라이드가 바인딩된다) → 0스텝 Begin에 `SetLoadout(프리셋 무기 구성)`.
4. 워밍업·종료: TAC-F19-7. (3회차) 측정 중 상자 드랍은 0%이고, 레벨업 비활성과 함께 무기 구성이 측정 내내 프리셋 그대로 고정된다. 세션 중 무기 구성이 바뀌는 경로(개발자 `SetLoadout`·`SpawnChestNow`)가 실행되면 무효로 처리한다.
5. 종료 시 CSV 기록(5.15.3) → 환경 복원 → 오버레이 다시 표시.
6. 무효 조건(세션 중 F1, 시각화·배속·로그 변경, (3회차) 무기 구성 변경): 기록하지 않고 오버레이에 "측정 무효: <원인>"을 표시한다.

#### 5.15.3 측정 파일과 열 (2회차 재작성, A1)

측정 1회 = `persistentDataPath/measurements/measurement_<yyyyMMddTHHmmssZ>.csv` 하나. 형식(R28):
- **헤더 블록**: 1열 `key`, 2열 `value`인 행들. 끝에 빈 행 1개.
- **본문 블록**: 열 이름 행 1개 + 값 행 1개.
- 별도로 `measurements/summary.csv`에 헤더 키와 본문 열을 한 행으로 이어 붙여 추가한다(16회 표 작업용, 파일이 없을 때만 BOM과 열 이름 행을 쓴다).
- 프레임 원자료는 `measurements/frames_<timestamp>.csv`(`frame,unscaledDtMs,steps,gcBytes,memUsedMB,liveMonsters,activeGems,<marker>_ms...`).

헤더 키(순서 고정)와 SPEC 대응:

| 키 | SPEC 근거 |
|---|---|
| `schema` (= `survivor-measurement-1`) | - |
| `timestampUtc`, `label`(프리셋 이름, 예 `A3`), `group`(`A0`/`A`/`B`/`C`) | F10 측정 설계 |
| `buildType`(`Development`/`Release`, `Debug.isDebugBuild`), `unityVersion` | F10 측정 조건(빌드 종류) |
| `cpu`, `gpu`, `ramMB`, `os` | F10·F19 PC 사양(헤더) |
| `vSyncCount`, `targetFrameRate`, `scriptingBackend`, `incrementalGC` | F10 측정 환경 고정 4값(헤더) |
| `pool`, `target`, `tick`, `collision`, `ccdMode` | 전략 조합 |
| `monsterCountN`, `monsterCapDisabled` | 적 수 N |
| `despawnDistance`, `despawnFormula`(`45` 또는 `45*sqrt(N/400)`) | 디스폰 거리 D(사용한 D) |
| `seed` | 시드 |
| `cellSize` | 공간 해시 셀 크기(F16) |
| `stepSeconds`(`0.016666667`) | 스텝 간격 |
| `durationSteps` | 측정 길이 |
| `warmupSteps`(`300`), `nReachedStep` | 제외한 워밍업 스텝 수 |
| `playerInvulnerable` | 무적 여부 |
| `weaponLoadout`(`W1:5;W2:5;W3:5;W4:5`) | 고정 무기 구성(W1~W4 단계) |
| `chestDropChance`(`0`) (3회차) | 측정 중 상자 드랍 0% |
| `spawnStopHoldN` | 스폰 중단 + N 유지 모드 여부 |
| `levelUpEnabled`, `autoPathRadius`, `autoPathSpeed` | 결정론 설정(F10 "기록에 이 설정이 남아야") |
| `timeScale`, `overlayHidden`, `visualizersOff`, `collisionLogOff` | 측정 중 배제 조건 |

본문 열(순서 고정)과 SPEC 대응:

| 열 | SPEC 항목 |
|---|---|
| `frames`, `steps`, `slowmoFrames`(스텝 3회 프레임 수) | 보조 |
| `avgFps` | 1 |
| `onePercentLowFps`, `worstFrameMs` | 2 |
| `gcAllocPerFrameBytes` | 3 |
| `monsterCountLimit` | 4 — `group != A0`이면 `해당 없음`, A0이면 `avgFps < 55`일 때 N, 아니면 `미도달`(D28) |
| `monsterCountAvg`, `monsterCountMax` | 4의 근거·보조 |
| `step_msPerStep`, `step_msPerFrame`, `pool_…`, `targeting_…`, `spatialIndex_…`, `tickMonsters_…`, `tickProjectiles_…`, `collision_…` (각 2열) | 5 (스텝당 = 주 지표, R27) |
| `memTotalUsedMB_avg`, `memTotalUsedMB_max` | 6 (평균·최댓값 두 열) |
| `memTotalReservedMB_avg`, `memTotalReservedMB_max` | 6 보조 |
| `activeGems_avg`, `activeGems_max` | 활성 젬 수(F4·F10·F19) |

값을 얻을 수 없으면 `NA`(예: 릴리스 빌드의 마커·GC·메모리). "해당 없음"은 정의상 측정하지 않는 항목에만 쓴다. FPS 평균 = 프레임 수 / 비스케일 시간 합, 1% low = 1000 / (가장 느린 1% 프레임 시간 평균 ms).

### 5.16 상태 해시 (Simulation, IMP-LOOP)

```csharp
namespace Survivor.Simulation
{
    /// FNV-1a 64 over: step, player pos/hp/xp/level, monsters (ascending Id: id, pos, hp),
    /// projectiles (ascending Id: id, pos), gems (ascending Id: id, pos), kill count (R24). Floats hashed by bit pattern.
    public static class SimulationStateHash { public static ulong Compute(RunSession session); }
}
```
결정론 테스트(프레임레이트 3종, 전략 조합)와 개발자 모드 N스텝 결과 출력에 이 해시를 쓴다.

### 5.17 흐름 상태 (App, IMP-FLOW) (2회차 신규)

```csharp
namespace Survivor.App
{
    public enum FlowState { Title, Playing, Paused, LevelUp, Result, DataError }

    public sealed class GameFlowController : MonoBehaviour
    {
        // Update: polls clock.CloseSequence -> LevelUp (open round) / Death, Clear (result + best record) / Error (error view) / StopAtStep (no-op; dev handles)
        // ESC: Playing -> Paused(return Playing); LevelUp -> Paused(return LevelUp); Paused -> return state
        // OnApplicationFocus(false): Playing && Rules.AutoPauseOnFocusLoss -> Paused (TAC-F8-9)
        // UI views raise events (UI never references App); this class subscribes.
    }

    public readonly struct RunOutcome { public readonly RunOutcomeKind Kind; public readonly long SurvivalSteps; public readonly int Kills; public readonly int Level; }
    public enum RunOutcomeKind { None, Death, Clear }
}
```

### 5.18 화면 설정 (App, IMP-FLOW) (2회차 신규, A13)

```csharp
namespace Survivor.App
{
    public static class WindowSizeSelector
    {
        public static readonly Vector2Int[] Candidates = { new(1920, 1080), new(1600, 900), new(1280, 720) };
        /// First candidate whose client size plus frame allowance fits the work area; otherwise 1280x720 (clipping allowed).
        public static Vector2Int Choose(int workAreaWidth, int workAreaHeight, int frameAllowanceW = 16, int frameAllowanceH = 39);
    }

    /// The only caller of Screen.SetResolution. Called once from GameBootstrap.Awake.
    public static class DisplaySetup { public static void Apply(); }   // workArea = Screen.mainWindowDisplayInfo.workArea [U35]
}
```

- 창 테두리 여유(16×39 픽셀)는 [기본값]이다(D33). 1080p 모니터에서 작업 표시줄 높이 40~48이면 작업 영역 높이가 1032~1040이라 1920x1080(+39)은 들어가지 않고 1600x900이 선택된다. 125% 배율 노트북(1536x816 논리)도 물리 픽셀 기준으로 판정한다 [실측 U35].
- `DisplaySetup`은 실행 중 다시 부르지 않는다(SPEC "설정 메뉴 없음").
- Player Settings 쪽(사용자): 5b.

---

## 5a. 공유 규칙 표 (Shared Rules)

각 규칙에는 소유 함수가 하나씩 있고, 다른 계층은 그 함수를 호출만 한다. 다시 구현하면 리뷰에서 반려한다. (2회차 정정) 소유 파일은 대부분 `Assets/Scripts/Core/`(IMP-CORE)이고, 예외는 **R11(Simulation, IMP-LOOP)**, **R14(Strategies/Spatial, IMP-SPATIAL)**, **R21(Data, IMP-DATA)**, **R27·R28(Dev, IMP-DEV)**, **R29(Simulation/Progression, IMP-FLOW)** 여섯 개다.

| # | 규칙 | 단위·의미 (정확히) | 소유 함수 | 사용자 |
|---|---|---|---|---|
| R1 | 초 → 스텝 환산 (2회차 확장) | `steps = (int)Math.Round(seconds × 60, MidpointRounding.AwayFromZero)`. 음수·NaN·∞는 `ArgumentOutOfRangeException`. **적용 대상**: 최소 1(`SecondsToIntervalSteps`) = 무기 `cooldownSeconds`·`lifetimeSeconds`·`hitIntervalSeconds`·`durationSeconds`, 몬스터 `charge.durationSeconds`·`charge.cooldownSeconds`·`ranged.fireCooldownSeconds`·투사체 `lifetimeSeconds`, 보스 패턴 `cooldownSeconds`·`durationSeconds`. 0 허용(`SecondsToSteps`) = `playerStats.invulnerabilitySeconds`, 스폰 `startSeconds`·`endSeconds` | `StepMath.SecondsToSteps`, `StepMath.SecondsToIntervalSteps` | Data 로더(유일한 호출자. 런타임 코드는 환산된 값만 쓴다) |
| R2 | 게임 시간 | 게임 시간(초) = 완료 스텝 수 / 60. 표시는 `mm:ss`(초는 내림). 분이 99를 넘으면 그대로 증가 | `StepMath.StepsToSeconds`, `StepMath.FormatGameTime(long steps)` | HUD, 결과, 최고 기록, CSV |
| R3 | 속도 단위 | JSON = 유닛/초. 런타임 = 유닛/스텝 = 값 / 60f. 각속도는 도/초 → 라디안/스텝 = deg × π/180 / 60 | `StepMath.PerSecondToPerStep`, `StepMath.DegPerSecondToRadPerStep` | Data 로더, `AutoPathInputSource`, 불변식 검증 |
| R4 | 반지름·거리 | 월드 유닛. 1유닛 = 카메라 orthographic size 9 기준 세로 화면의 1/18. 시각 크기 = 지름 2r에 맞춘 배율 | `CircleMath.VisualScaleFor(Sprite, float radius)` | 모든 엔티티 초기화 |
| R5 | 원-원 겹침 | `(b − a).sqrMagnitude ≤ (ra + rb)²`, 경계 포함, 제곱근 없음. 채널 9개와 A측 레이어는 `CollisionChannelTable` | `CircleMath.Overlaps(Vector2 a, float ra, Vector2 b, float rb)`, `CollisionChannelTable.TryGetChannel(CollisionLayer x, CollisionLayer y, out CollisionChannel ch, out bool xIsA)` | 자체 충돌 전략, 불변식 2단 대상 쌍, 엔진 링크(채널 판정), 레이어 매트릭스 설정 |
| R6 | 최근접 타이브레이크 | 제곱거리(float, `sqrMagnitude`) 작은 쪽 → 같으면 `EntityId` 작은 쪽(Index, 그다음 Generation) | `TargetOrdering.IsBetter(float d2, EntityId id, float bestD2, EntityId bestId)` | 타깃 공급자 3종, W3 위치 선정 |
| R7 | 화면 밖 스폰 위치 | `θ = u × 2π`(u = `NextFloat01()`, float), `pos = center + radius × (Mathf.Cos θ, Mathf.Sin θ)`. 1회 추첨, 재시도 없음 | `SpawnGeometry.PointOnCircle(Vector2 center, float radius, float u01)` | 일반 스폰(r=20), 보스 스폰, 보스 소환(summonRadius), 개발자 즉시 N기 |
| R8 | 회복 가중치 | `r = clamp01(hp / maxHp)`, `w = wBase × (1 − r)²`. `w ≤ 0`이면 후보 제외 | `HealWeight.Compute(float wBase, float hp, float maxHp)` | 레벨업 후보, 개발자 오버레이(곡선 표시) |
| R9 | 가중 추출 | 정규 순서의 후보에 누적 합. `u = NextFloat01() × total`, `cum > u`인 첫 항목. 가중치 0 이하는 건너뜀. 비복원이면 뽑힌 항목을 지우고 반복 | `WeightedSampler.PickIndex`, `WeightedSampler.SampleWithoutReplacement` | 레벨업, 스폰 종 선택 |
| R10 | 결정적 순회 순서 | 엔티티 집합 순회·피해 적용·처치·물리 바디 동기화는 모두 `EntityId` 오름차순. 이벤트는 `CollisionEvent.CompareTo` 순서. 명령은 (Source, Sequence) 순서 | `EntityId.CompareTo`, `CollisionEvent.CompareTo`, `SimulationCommand.Compare` | 모든 구현자 |
| R11 | 엔티티 생성·회수 (2회차 수정) | 5.10의 순서 그대로, **Lifecycle 단계와 판 리셋에서만**. 다른 경로로 `Acquire`/`Release`/`Allocate`/`Free`를 호출하지 않는다 | `EntityLifecycle.Spawn`, `EntityLifecycle.Despawn` (Simulation, IMP-LOOP) | 몬스터·무기·드랍 구현자(명령 핸들러) |
| R12 | 레벨업 후보 정규 순서 | `UpgradeKind` 값 오름차순(WeaponUpgrade, WeaponNew, PassiveUpgrade, PassiveNew, Heal) → `TargetId` 서수(Ordinal) 문자열 비교 | `UpgradeOrdering.Compare` | 후보 생성, 테스트 |
| R13 | 패시브 보정 | 공격 속도: `max(1, round(baseSteps / (1 + bonus)))`. 이동 속도: `base × (1 + bonus)`. 획득 범위: `base × (1 + bonus)`. bonus = 보유 단계의 `levels[level-1].bonus`, 미보유는 0 | `StatMath.ApplyAttackSpeed(int baseSteps, float bonus)`, `StatMath.ApplyMultiplier(float baseValue, float bonus)` | 무기(W1·W3·W4 쿨다운, W2 틱 간격 [기본값 D14]), 플레이어 이동, 자석 반경 |
| R14 | 공간 해시 셀 키 | `cx = FloorToInt(x / cellSize)`, `cy = FloorToInt(y / cellSize)`, `key = ((long)cx << 32) ∣ (uint)cy`. 음수 좌표 포함 | `SpatialHash.CellOf(Vector2 p, float cellSize)`, `SpatialHash.KeyOf(int cx, int cy)` (Strategies) | 몬스터 인덱스, 픽업 해시, 개발자 시각화 |
| R15 | 객체 ID 형식 | 5.1 `EntityId`. 문자열은 `"{Index}:{Generation}"`. CSV·로그·쿨다운 기록·타깃 결과 모두 이 형식 | `EntityId.ToString`, `EntityId.CompareTo` | 전부 |
| R16 | 측정 마커 이름 | `Survivor.Step`, `Survivor.Pool`, `Survivor.Targeting`(BuildIndex 몬스터 바디 동기화 포함), `Survivor.SpatialIndex`, `Survivor.Tick.Monsters`, `Survivor.Tick.Projectiles`, `Survivor.Collision`(엔진 동기화·Simulate 포함) | `ProfilerMarkers` (static readonly `ProfilerMarker` 필드) | 루프, 전략 구현, `StatsCollector` |
| R17 | 대각 이동 | 입력 (x, y) ∈ {−1, 0, 1}² → 8방향 단위 벡터 상수 표(대각 = 0.70710677f). 방향이 영벡터면 `DirectionOrDefault`가 `Vector2.right` | `MoveMath.ToUnitDirection(int x, int y)`, `MoveMath.DirectionOrDefault(Vector2 v)` | 키보드 입력, W1 발사 |
| R18 | 디스폰 판정 | `(pos − playerPos).sqrMagnitude > D²`(엄격 초과). 보스는 제외 | `SpawnGeometry.IsBeyondDespawn(Vector2 pos, Vector2 player, float d)` | 디스폰 시스템, 개발자 시각화 |
| R19 | 물리 바디 요구사항 (2회차 확장) | 몬스터 콜라이더 = `Collision == Engine ∥ Target == Physics2DQuery`, 전 콜라이더 = `Collision == Engine`, BuildIndex 몬스터 동기화 = `Target == Physics2DQuery`, Collision 단계 Simulate = `Collision == Engine` | `ColliderActivationPolicy.Resolve(in StrategySelection s)` → `PhysicsBodyRequirements` | 조립 루트, `PhysicsBodySynchronizer`, 엔진 전략, Physics2D 공급자, `MonsterSpatialIndex.Required` 계산 |
| R20 | 규칙 상수 (2회차 추가) | `SpawnRadius = 20f`, `DefaultDespawnDistance = 45f`, `MonsterCapDefault = 400`, `BossSpawnStep = 36000`, `ChestDropChance = 0.02f`, `ChestUpgradeChance = 0.70f`, `HealRatio = 0.30f`, `MaxWeaponSlots = 4`, `MaxPassiveSlots = 3`, `CameraOrthographicSize = 9f`, **`LargeMonsterRadiusThreshold = 1.0f`** [기본값], **`MinTimeScale = 0.25f`**, **`MaxTimeScale = 3f`**, **`MeasurementWarmupSteps = 300`**, **`MonsterLimitFpsThreshold = 55f`** | `GameRules` (static class, `const`) | 전부 |
| R21 | 경험치 곡선 (2회차 명확화) | 5.13.7 `xpCurve`. 레벨업마다 필요량을 빼고 이월 | `XpCurve.RequiredForNext(int level, XpCurveDefinition def)` (Data) | `ExperienceSystem`, 개발자 "레벨 즉시 상승" |
| R22 | 강화 가능 판정 (2회차 신규) | 무기: `level < levelCount && !evolvedOnly`. 패시브: `level < levelCount` | `ProgressionRules.IsWeaponUpgradable(int level, int levelCount, bool evolvedOnly)`, `ProgressionRules.IsPassiveUpgradable(int level, int levelCount)` | `ChestEffectResolver`(PLAYER), `LevelUpCandidateBuilder`(FLOW), `PlayerProgressionApplier`(검증), 개발자 오버레이 |
| R23 | 회복 적용 (2회차 신규) | `hp' = min(maxHp, hp + maxHp × HealRatio)`. 최대 체력에서 멈춘다. 최대 체력은 바꾸지 않는다 | `ProgressionRules.ApplyHeal(float hp, float maxHp)` | `PlayerProgressionApplier.Heal`(상자 ②·레벨업 회복 공용) |
| R24 | 처치 수 정의 (2회차 신규) | 처치 = `DespawnReason.Killed`인 몬스터 회수(보스·소환체 포함). `OutOfRange`·`RunReset`·`Replaced` 등은 처치가 아니다. 증가는 `KillPipeline` 한 곳 | `KillRules.CountsAsKill(DespawnReason r)` | `KillPipeline`(MONSTER, 증가), 결과 화면(FLOW), `SimulationStateHash`(LOOP), `DevSpawnPolicy`(N 유지 보충 판단) |
| R25 | 큰 몬스터 판정 (2회차 신규, 3회차 수정) | `radius > GameRules.LargeMonsterRadiusThreshold(1.0) ∥ behavior == boss`. 보스는 반지름과 무관하게 항상 큰 몬스터(SPEC F16 "보스는 공간 해시에 들어가지 않는다") | `MonsterSizeRules.IsLarge(float radius, bool isBoss)` | `MonsterRegistry`(스냅샷), 몬스터 `CollisionBody.IsLarge`, 공간 해시, 자체 충돌 |
| R26 | N에 따른 디스폰 거리 (2회차 신규) | `N ≤ 400 → 45`, `N > 400 → 45·√(N/400)` (float) | `SpawnGeometry.DespawnDistanceFor(int n)` | `DevController`, `MeasurementPresets`, 측정 헤더 |
| R27 | 측정 ms 정의 (2회차 신규) | 스텝당 = 세션 합(ms) / 세션 전체 스텝 수, 프레임당 = 세션 합 / 세션 전체 프레임 수. 분모가 0이면 `NA` | `MeasurementMath.PerStepMs(double totalMs, long steps)`, `MeasurementMath.PerFrameMs(double totalMs, long frames)` (Dev) | `StatsCollector`, `MeasurementCsvExporter`, 오버레이 실시간 표시 |
| R28 | CSV 형식 (2회차 신규) | UTF-8 BOM(새 파일만), 줄 끝 `\r\n`, 숫자 `ToString("R" 또는 지정 형식, CultureInfo.InvariantCulture)`, 불리언 `true`/`false`, 쉼표·따옴표·줄바꿈 포함 값은 큰따옴표로 감싸고 내부 `"`는 `""` | `CsvWriter` (Dev) | `MeasurementCsvExporter`, `CollisionEventLog` |
| R29 | 레벨업 입력 수락 (2회차 신규) | 회차 ID 일치 && 미제출 && `frame > OpenedFrame` && 인덱스 0~2 → 수락(이후 같은 회차 전부 거부) | `LevelUpSession.TrySubmit` (Simulation/Progression, IMP-FLOW) | `UiInputHandler`(키), `LevelUpView`(클릭·Enter) |
| R30 | Unity 레이어 대응 (2회차 신규) | Unity 레이어 인덱스 = `8 + (int)CollisionLayer`(8~17). 레이어 매트릭스는 R5 채널 9개만 켠다 | `CollisionLayerMapping.UnityLayerOf(CollisionLayer)`, `CollisionLayerMapping.MaskOf(CollisionLayer)` | `PhysicsSettingsApplier`, `CollisionTemplateDecorator`, `Physics2DTargetProvider` |

---

## 5b. 외부 서비스 설정 점검표 (External Service Checklist)

인증·스토리지 같은 외부 서비스 연동은 **없다**. 대신 사용자만 할 수 있는 에디터·빌드 설정과 GitHub Releases 업로드가 F13·F15에 걸려 있어 정리한다. (2회차) URP 템플릿, 창 설정, 측정 환경, 비교 빌드를 추가했다.

| 설정 (사용자 수행) | 없으면 생기는 일 | 증상 | 확인 상태 |
|---|---|---|---|
| Unity 6000.3.26f1 + Windows Build Support 모듈 (F15) | 빌드 불가 | Build Profiles에 Windows가 비활성 | 실측 필요 |
| **Universal 2D(URP) 템플릿**으로 `indiv/Survivor/` 생성 (F15 4회차, 5c.1) | 구현자가 파일을 둘 경로가 없다. Built-in이면 U33·U34 전제가 달라진다 | - | 사용자 결정 필요(폴더명) |
| Player Settings > Resolution: Fullscreen Mode = **Windowed**, Default Screen 1280x720(스크립트 적용 전 초기값), **Resizable Window 끔**, **Allow Fullscreen Switch 끔**(Alt+Enter), Default Is Native Resolution 끔, **Run In Background 끔**(측정 중에만 코드로 켬) | 16:9가 깨지고 Q44 계산이 무효 / Alt+Enter로 전체화면 | 창 크기 변화, 모서리 스폰이 보임 | 실측 U24·U35·U36·U37 |
| Active Input Handling = Input System Package | `Keyboard.current`가 null, uGUI 입력 모듈 오류 | NullReferenceException, 메뉴 키보드 탐색 불가 | 실측 U17·U42 |
| Scripting Backend = **Mono**, 점진적 GC = Unity 기본값 유지 (F10 측정 환경) | 측정 환경이 SPEC과 다름 | 헤더 `scriptingBackend`가 `IL2CPP` | 측정 헤더로 확인 |
| TextMeshPro Essential Resources 가져오기 + 한글 TTF(예: Noto Sans KR, 라이선스 기록)로 TMP Dynamic Font Asset 생성 + IMGUI용 `Font` | 한글이 네모로 표시 | 타이틀·결과·오버레이 문자 깨짐 | 실측 U18 |
| `Assets/Resources/AssetKeyRegistry.asset` 생성(메뉴 Survivor/Asset Key Registry) | 모든 키가 기본 도형으로 대체 + 경고 | 마젠타 원만 보임 | 코드는 폴백 처리 |
| Newtonsoft JSON 패키지 추가(D17 확정) | Data 어셈블리 컴파일 실패 | `JToken` 미정의 | 실측 U25, 승인 게이트 |
| 빌드 3종: Development Build(Autoconnect Profiler) / 릴리스 / **비교용 `SURVIVOR_NO_DEV` 심볼 빌드**(F19 4회차 1회 비교) | F10 공식 측정 불가 / 코드 제거 비교 불가 | Profiler 미연결, 시스템 ms가 `NA` | 실측 U14·U40 |
| GitHub Releases: 빌드 폴더 전체를 zip(`StreamingAssets` 포함). 배포 설명에 zip 해제 후 실행, Windows 10/11 64비트, SmartScreen 안내, "1280x720 이상 작업 영역"(F13 4회차) | 다른 PC에서 데이터 로드 실패 | 시작 차단 화면 "weapons.json 없음" | F13 테스터 확인 |
| 저장소 공개 전 점검: `persistentDataPath` 덤프·CSV는 저장소 밖이라 커밋되지 않음. 개인 경로가 들어간 로그 파일이 저장소에 있는지 확인 | 개인 정보 노출 | - | 사용자 확인 |

순서: 프로젝트 생성(URP 2D) → Player·입력·백엔드 설정 → Newtonsoft 추가 → TMP Essentials·글꼴 → 웨이브 0 실측 스파이크 → (구현 디스패치) → 레지스트리 에셋 → 빌드 3종 → Releases 업로드.

URP 관련 코드 쪽 처리: `GameBootstrap`이 씬에 Global Light 2D가 없으면 코드로 하나 만든다(강도 1) [실측 U34]. 시각화는 `endCameraRendering` 경로다 [실측 U33].

---

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

## 6. 미확정 사항 (Open Questions)

### 6.1 코드에서 확인됨 → 이 프로젝트에서는 "문서 간 불일치 / SPEC 미규정" (코드가 없으므로 SPEC·RUN_LOG를 읽고 확인한 것)

각 항목에 [기본값]을 적었다. blocking 여부도 표시했다. 해소된 항목은 번호를 유지하고 "해소(2회차, 근거)"로 표시했다.

**C 목록 — SPEC 4회차 정합 점검에서 찾아 고친 것 (2회차)**: ① SPEC F8 "Canvas Scaler"인데 1회차는 UI Toolkit → uGUI로 변경(D25) ② SPEC F12 4회차 "TECH_SPEC 5.0의 인터페이스 전부"가 판정 기준인데 1회차 5.0에 인터페이스 목록이 없었다 → 5.0.1 ③ SPEC F19 4회차 "코드 제거 빌드와 1회 비교"가 1회차 App → Dev 참조로는 불가 → 의존 방향 반전(D31) ④ 1회차는 등록 해제가 Exit을 내지 않는데 겹침 집합·쿨다운 기록을 지우는 경로가 없어 **디스폰된 몬스터가 플레이어 겹침 집합에 남아 계속 피해를 주는 버그**가 있었다 → TAC-0-10 ⑤ TAC-F4-2 드랍 처리 순서가 "피해 적용 순서"로 모호 → `KillPipeline` ID 순 ⑥ SPEC F4 4회차 70:30 ±3%p(1,000회) 기준이 TAC·테스트에 없었다 → TAC-F4-7 ⑦ SPEC 3.0 I 몸통 접촉 최대 피해·작은 ID → TAC-F3-12(D11·D12 해소) ⑧ SPEC F3 4회차 몬스터는 플레이어 위치 직접 참조 → TAC-F3-11(D20 해소) ⑨ SPEC F6 4회차 W3 대상 없음 = 쿨다운 미소비 → TAC-F6-5(D24 해소) ⑩ SPEC F8 4회차 개발자 모드 판 기록 미저장·`PlayerPrefs.Save`·타이틀로 나가기 미갱신·메뉴 키보드+마우스·보스전 시간 제한 없음 → TAC-F8-3·F8-12·F8-13 ⑪ SPEC F10 4회차 C 묶음 "나머지 기준선" → `MeasurementPresets` ⑫ SPEC F10·F19 4회차 메모리·젬 수·워밍업·환경 4값·배제 조건 → 5.15 ⑬ SPEC F11 4회차 U+FFFD·CSV BOM → TAC-F11-6·F11-7 ⑭ SPEC F16 "반지름이 큰 객체" → R25 ⑮ SPEC F1 4회차 카메라 즉시 스냅 → TAC-F1-6 ⑯ SPEC F15 4회차 URP 템플릿 → 5b·U33·U34 ⑰ SPEC F12·F14 4회차 기능별 기록 → 5c.2a ⑱ 1회차 `MovingObjectSpec.CollisionLayerTag` 별칭은 C#으로 표현할 수 없다 → `CollisionLayer` 직접 사용.

- **D1.** F18 불변식 검사 대상 목록(W1·W4, 몬스터, M3 투사체, W2, W3)에 **플레이어**와 **끌리는 젬**이 빠져 있다(SPEC 4회차에서도 그대로). [기본값] 둘 다 검사 대상에 넣는다. 플레이어 속도는 P2 최대 단계를 적용한 값. 비차단.
- **D2.** W2 궤도의 "선속도 = 각속도 × 궤도 반지름"은 플레이어 이동을 뺀 값이다. 궤도 원의 실제 월드 이동량은 ωR + 플레이어 속도다. [기본값] 검사에는 ωR + 플레이어 최대 속도를 쓴다. 비차단.
- **D3.** 해소(2회차, RUN_LOG 2026-10-16 D3 확정 + SPEC F4 4회차): 젬·상자 디스폰 규칙은 넣지 않고 활성 젬 수를 통계·CSV에 기록한다(TAC-F4-8, 5.15.3).
- **D4.** SPEC의 "스폰 위치는 r=20 원주"가 보스 등장과 보스 소환에도 적용되는지 정해져 있지 않다. [기본값] 보스는 같은 원주, 소환은 보스 주변 `summonRadius` 원주다(소환체는 화면 안에 생길 수 있다). 비차단.
- **D5.** 진화로 소모된 원본 무기를 레벨업에서 다시 "새 무기"로 얻을 수 있는지 SPEC에 없다. [기본값] 다시 얻을 수 없다(후보에서 제외). 비차단.
- **D6.** 보스(M4) 정의가 무효면 10분 뒤 클리어가 불가능하지만, SPEC의 "플레이 불가 4기준"에는 들어 있지 않다. [기본값] Warning만 남기고 시작은 허용한다. (2회차) 결과는 **소프트락**이다: 36000 스텝 이후 일반 스폰이 끝나고 보스도 나오지 않아 사망·타이틀로 나가기 외에 판이 끝나지 않는다(TAC-F8-10). 5번째 기준으로 추가하려면 PM이 결정한다. 비차단.
- **D7.** `startingWeaponId`가 무효일 때의 처리가 없다. [기본값] 플레이어 기본 스탯 오류(Blocking)로 본다. 비차단.
- **D8.** 해소(2회차, SPEC F18 4회차): 비교 키에 채널이 들어갔다. (4회차) 키 순서는 SPEC 5회차 F18 "(채널, ID) 순 정렬"에 맞춰 `(스텝, 채널, 객체 ID 쌍, 종류)` = `CollisionEvent.CompareTo`로 통일했다(TAC-F18-8). 자석 별도 ID는 로그 가독성 때문에 유지한다.
- **D9.** 해소(2회차, SPEC F19 4회차 + PM 방향): 배속 상한을 3x로 낮췄다. catch-up 상한 3은 유지하고 오버레이에 실제 배속을 표시한다(5.15.1). U23은 실측 항목으로 남는다.
- **D10.** 해소(2회차, SPEC 3.0 B 4회차): 타깃 탐색만 즉시, 풀링·틱·충돌은 다음 판 시작 시 적용.
- **D11.** 해소(2회차, SPEC 3.0 I·F3 4회차): 몸통 접촉 다수 = 최대 피해 1회, 같으면 작은 ID.
- **D12.** 해소(2회차, SPEC 3.0 I·F3 4회차): 무적 중 M3 투사체는 피해 없이 소모.
- **D13.** 해소(2회차, SPEC 3.0 I·F6 4회차): 쿨다운 기록은 무기 인스턴스가 대상별로 갖는다.
- **D14.** P1 "모든 무기의 공격 간격"이 W2 틱 간격과 W3 장판의 틱 간격에도 적용되는지 정해져 있지 않다(SPEC 4회차에서도 그대로). [기본값] W1·W3·W4의 쿨다운과 W2 틱 간격에 적용하고, W3 장판 내부 틱 간격에는 적용하지 않는다. 비차단.
- **D15.** SPEC F18의 "구현 B 후보는 공간 해시로 추리고 전수 비교하지 않는다"를 플레이어 1명이 한쪽인 채널에서 어떻게 해석할지가 애매하다. [기본값] 몬스터 채널은 공유 몬스터 인덱스, 젬·상자는 픽업 전용 해시, 적 투사체·적 장판은 선형 순회. 비차단.
- **D16.** SPEC의 "젬 ↔ 플레이어" 판정 하나가 이 설계에서는 채널 두 개(자석 Enter = 끌림 시작, 몸통 Enter = 획득)로 나뉜다. 비차단(정보 공유).
- **D17.** 해소(2회차, RUN_LOG 2026-10-16 D17 확정 + SPEC F11 4회차): Newtonsoft 패키지. 6000.3 가용성은 U25.
- **D18.** 해소(2회차, SPEC F3 4회차): SPEC이 화면 기준을 월드 단위(orthographic size 9, 반대각 18.36, 여유 1.64)로 고쳤다. 반지름 1.64 이상 몬스터의 모서리 노출은 2.4에 정보로 남긴다.
- **D19.** 개발자 모드 "레벨업 비활성" 상태에서 경험치를 어떻게 처리할지 정해져 있지 않다. [기본값] 경험치는 쌓이고 레벨은 오르지 않는다. 비차단.
- **D20.** 해소(2회차, SPEC F3·F16 4회차): 몬스터는 플레이어 위치를 직접 참조하고 `ITargetProvider`는 무기 전용.
- **D21.** 개발자 오버레이의 문자열 언어: CODING_GUIDE §3은 "사용자에게 보이는 문자열은 한국어"라고 한다. [기본값] 한국어로 쓰고 같은 한글 글꼴을 쓴다. 비차단.
- **D22.** 해소(2회차, SPEC 3.0 H 4회차): "ID 발급과 세대 증가 규칙은 풀 구현 A/B/C 어느 것을 쓰든 같아야 한다" — 풀과 독립된 `EntityIdAllocator`가 그 요구를 충족한다.
- **D23.** 상자 효과 ②의 강화 대상 선택 방식이 "랜덤"으로만 적혀 있다. [기본값] 슬롯 순서 목록에서 균등. 비차단.
- **D24.** 해소(2회차, SPEC F6 4회차·Q46②): W3 대상 없음 = 생성 안 함, 쿨다운 미소비, 다음 스텝 재시도.
- **D25.** (2회차 신규) SPEC F8 4회차는 "UI는 Canvas Scaler로 스케일"이라고 적었는데 Canvas Scaler는 uGUI 컴포넌트다. 1회차 TECH_SPEC은 UI Toolkit을 골랐다. [기본값] SPEC 문구대로 **uGUI + CanvasScaler + TextMeshPro**로 바꾸고 UI를 코드(`UiFactory`)로 구성한다. 영향: IMP-FLOW 파일 목록, 5b(TMP Essentials·폰트 에셋), U18·U42. UI Toolkit의 PanelSettings "Scale With Screen Size"로도 같은 효과를 낼 수 있으므로, SPEC 문구를 "해상도 독립 스케일"로 넓히는 쪽을 PM이 고르면 UI Toolkit으로 되돌릴 수 있다. 비차단(PM 확인 권장, 웨이브 4 전).
- **D26.** 해소(3회차, PM 승인: 세대 유일성은 한 판 안에서 보장하고, 판 리셋 때 할당기를 초기화해 세대가 1로 돌아간다. SPEC 3.0 H에 이 문구를 추가할 예정). 근거는 같은 시드 재실행의 ID 동일성(결정론 비교 전제)이며, ID로 키를 잡는 기록은 리셋에서 모두 지워지므로 판을 넘는 오적용은 없다(5.1).
- **D27.** (2회차 신규) SPEC F18 수용 기준 "구현 B에서 경험치 젬과 몬스터에는 `Collider2D`가 없거나 비활성"은 F16 C(`Physics2D` 쿼리)를 함께 켠 조합에서 몬스터 콜라이더를 켜야 한다는 F18 요구사항 본문과 부딪힌다. [기본값] 요구사항 본문을 따른다: 자체 충돌 + Physics2D 타깃 조합에서만 몬스터 콜라이더가 켜지고, 젬은 항상 꺼진다(R19). 비차단.
- **D28.** (2회차 신규) SPEC F10 항목 4 "동시 몬스터 수 한계"는 A0 스윕 전체의 결과인데 측정 파일은 1회 단위다. [기본값] A0 행마다 `avgFps < 55`면 그 N, 아니면 `미도달`을 쓰고, N\*(가장 작은 도달 N)는 사용자가 devlog에 적는다. A·B·C는 `해당 없음`. 비차단.
- **D29.** (2회차 신규) SPEC F10·F19 4회차 "오버레이가 켜져 있으면 측정을 시작하지 않는다"와 "측정 시작은 오버레이에서 조작"이 맞물린다. [기본값] "측정 시작" 버튼은 나머지 조건(시각화·로그·배속·한 스텝 모드)을 검사해 거부하거나, 통과하면 **오버레이를 스스로 숨기고** 시작한다. 측정 중 오버레이를 다시 열면 세션은 무효가 된다(5.15.2). 비차단(PM 확인 권장).
- **D30.** (2회차 신규) "엔진 충돌 + Physics2D 타깃" 조합에서는 BuildIndex 동기화 때문에 몬스터가 텔레포트로 움직여 Continuous 스윕이 몬스터에 적용되지 않는다. [기본값] E1·E2는 타깃을 공간 해시로 두고 돌린다. 측정 묶음 C의 Physics2D 회차는 충돌이 기준선(엔진)이라 이 조합에 해당하지만, C는 CCD 끔(Discrete)이라 판정에는 영향이 없다고 본다(U6과 함께 확인). 비차단.
- **D31.** (2회차 신규) SPEC F19 4회차 "개발자 모드 코드를 제거한 빌드와 1회 비교"를 위해 의존 방향을 Dev → App으로 뒤집고, Dev 어셈블리에 `defineConstraints: ["!SURVIVOR_NO_DEV"]`를 둔다. 그 결과 인자 확인 1회가 App이 아니라 Dev의 `RuntimeInitializeOnLoadMethod`에서 일어난다(SPEC "시작 시 인자 확인 1회"는 그대로 충족). 비차단(PM 확인 권장, U40 실측 필요).
- **D32.** (2회차 신규) SPEC 3.0 I는 "몸통 접촉 적이 여럿이면 최대 피해 1회"만 정했다. 같은 스텝에 몸통 접촉·M3 투사체·M4 장판이 섞이면? [기본값] 세 종류 후보 전체에서 최대 피해 1회, 같으면 작은 ID(무적 시간이 0이어도 스텝당 최대 1회). 비차단.
- **D33.** (2회차 신규) SPEC F8 "작업 영역에 들어가는 가장 큰 16:9 해상도"가 창 테두리·제목 표시줄을 포함한 바깥 크기 기준인지 정해져 있지 않다. [기본값] 바깥 크기 기준(클라이언트 크기 + 16×39 픽셀 여유). 1080p 모니터에서는 1600x900이 고르게 된다. 비차단.

### 6.2 실측/실행 필요 — 미확정 (Needs measurement/execution — unconfirmed)

아래는 모두 정적으로 읽어서는 확정할 수 없는 Unity 동작 전제다(2.3 표와 번호가 같다). **사실로 취급하지 않는다.** 웨이브 0 스파이크나 해당 구현자의 PlayMode 테스트로 확인한다.

- **U1** `DefaultExecutionOrder`가 `FixedUpdate`에 적용되는지, 인스턴스 간 순서가 보장되지 않는지. 틀리면 F17 A 모드의 단계 순서를 다시 설계해야 한다. **IMP-LOOP 착수 전 확인 권장.**
- **U2** `maximumDeltaTime = 0.05`에서 프레임당 정확히 최대 3스텝인지. 틀리면 SPEC의 "0.05로 설정"을 고쳐야 하므로 PM에게 보고한다.
- **U3** `Time.captureDeltaTime`으로 프레임레이트 결정론 테스트를 할 수 있는지.
- **U4** `Rigidbody2D` Collision Detection 기본값이 `Discrete`인지(SPEC Q39).
- **U5** 키네마틱-키네마틱 트리거 콜백 발생 조건. 안 되면 Dynamic + gravityScale 0 바디로 바꾼다. **IMP-COLLISION 착수 전 필요.**
- **U6** `Continuous`가 트리거·키네마틱 바디에 영향을 주는지. **E2가 "자명하게 동일"로 끝나 SPEC이 의도한 검증이 되지 않을 가능성이 있다.** 그렇다면 그 사실 자체를 devlog 소재로 기록하고, E2의 의미를 PM이 다시 정해야 한다.
- **U7** `simulationMode = Script`에서 `Physics2D.Simulate()` 호출 중 트리거 콜백이 동기적으로 오는지. 틀리면 엔진판 이벤트가 한 스텝 늦어 E1이 스텝 번호에서 갈린다. **IMP-COLLISION 착수 전 필요.**
- **U8** `callbacksOnDisable = false`이면 비활성화 시 Exit이 나오지 않는지.
- **U9** Box2D 원-원 경계 판정과 float 차이 때문에 E1 경계 사례에서 불일치가 생길 수 있는지. 생기면 허용 정책(경계 ±ε 사례는 별도 집계)을 PM이 정한다. U30과 함께 본다.
- **U10** `MovePosition`이 스윕 이동인지, `transform` 직접 쓰기가 텔레포트로 처리되는지.
- **U11** `Rigidbody2D.position`·콜라이더 활성화가 `OverlapCircle`에 언제 반영되는지(`autoSyncTransforms` 기본값 포함).
- **U12** 이름 없는 레이어 인덱스에 `IgnoreLayerCollision`을 코드로 설정할 수 있는지.
- **U13** Windows 플레이어에서 `StreamingAssets` 동기 읽기와 빌드 후 편집이 되는지.
- **U14** `ProfilerMarker`가 비개발 빌드에서 제거되는지, `ProfilerRecorder`가 Development Build에서 마커 프레임 합계를 주는지, 스크립트를 넘나드는 `Begin`/`End`가 허용되는지. 안 되면 틱 ms 수집을 Stopwatch 주입 방식으로 바꾼다(Q40의 "비활성 시 감시 없음"과 충돌하는지 PM 확인 필요).
- **U15** "GC Allocated In Frame" 카운터 이름과 가용성.
- **U16** `Environment.GetCommandLineArgs()`에 `--dev`가 들어오는지.
- **U17** Input System 활성 여부, 업데이트 모드, `wasPressedThisFrame` 동작.
- **U18** (2회차 수정) TMP Dynamic Font Asset(uGUI)과 IMGUI `Font`의 한글 렌더링.
- **U19** 대체됨(2회차) — UI Toolkit 미사용(D25). U42로 대체. 집계에서 뺀다.
- **U20** 런타임 생성 원 스프라이트의 월드 크기 계산.
- **U21** `ObjectPool<T>`의 `maxSize`·`collectionCheck` 동작.
- **U22** `Destroy` 지연과 같은 프레임 다음 스텝 간섭 여부.
- **U23** `maximumDeltaTime`이 스케일된 시간을 제한하는지(60FPS·3x = 프레임당 3스텝).
- **U24** (2회차 수정) 자동 선택한 16:9 창이 작업 표시줄·DPI 배율로 강제 축소되지 않는지.
- **U25** Newtonsoft 패키지의 6000.3 가용성과 Mono 동작.
- **U26** 같은 빌드·같은 PC에서 float 결과가 실행마다 같은지(결정론 전제 전체).
- **U27** batchmode에서 물리를 포함한 PlayMode 테스트가 도는지.
- **U28** 6.3에서 기존 `Physics2D` API가 그대로 동작하는지.
- **U29** `SpriteRenderer` Tiled 모드로 반복 배경을 그릴 수 있는지(URP 2D 포함).
- **U30** (2회차 신규) `Physics2D.defaultContactOffset`을 0.0001 근처로 낮출 수 있는지, 그 값이 트리거 겹침 경계에 주는 영향. 낮출 수 없으면 E1 경계 사례 허용 정책(U9)이 필요하다. **IMP-COLLISION 착수 전 확인 권장.**
- **U31** (2회차 신규) `FixedUpdate` 도중 켠 셀프 티커가 같은 프레임의 다음 `FixedUpdate`부터 호출되는지. **틀리면 F17 A 모드에서 catch-up 프레임마다 새 엔티티의 첫 Tick이 밀려 프레임레이트 결정론이 깨진다.** 그 경우 대안은 셀프 티커를 생성 시점과 무관하게 항상 켜 두고 `IUpdatable` 연결 여부로만 거르는 것이다(측정 비용 특성이 바뀌므로 PM 판단). **IMP-LOOP 착수 전 확인 권장.**
- **U32** (2회차 신규) Script 모드에서 `rb.position` 직접 대입이 `Simulate` 없이 쿼리에 반영되는지, `queriesHitTriggers`로 트리거를 찾는지. **IMP-COLLISION·IMP-SPATIAL(C 구현) 착수 전 확인 권장.**
- **U33** (2회차 신규) URP 2D에서 `endCameraRendering` + GL 그리기가 보이는지. 안 되면 `LineRenderer` 풀 또는 `OnRenderObject` 재확인.
- **U34** (2회차 신규) URP 2D 기본 스프라이트 머티리얼의 Global Light 2D 의존과 코드 생성 `Light2D`. 안 되면 레지스트리에 Unlit 머티리얼 참조를 두는 방식으로 바꾼다.
- **U35** (2회차 신규) `Screen.mainWindowDisplayInfo.workArea`의 가용성·단위와 `SetResolution` 크기 의미.
- **U36** (2회차 신규) "Allow Fullscreen Switch" 끔이 Alt+Enter를 막는지.
- **U37** (2회차 신규) 창 모드 포커스 이벤트와 런타임 `runInBackground` 전환.
- **U38** (2회차 신규) 메모리 카운터 이름·가용성(Development/릴리스).
- **U39** (2회차 신규) vSync·targetFrameRate 런타임 설정이 창 모드에서 지켜지는지, `GarbageCollector.isIncremental` 읽기.
- **U40** (2회차 신규) `RuntimeInitializeOnLoadMethod(AfterSceneLoad)` 시점과 `defineConstraints`로 Dev 어셈블리(와 Dev 테스트 어셈블리)가 깨끗하게 빠지는지. **IMP-DEV 착수 전 확인 권장.**
- **U41** (2회차 신규) Mono의 UTF-8 디코딩이 잘못된 바이트를 U+FFFD로 바꾸는지(CP949 감지 전제).
- **U42** (2회차 신규) `InputSystemUIInputModule` 메뉴 탐색과, UI 클릭 콜백·`Update` 사이 순서(보장 안 된다고 가정했으므로 틀려도 설계는 안전).

### 6.3 집계 (3회차 갱신)

- 문서 간 불일치/SPEC 미규정: 번호 **33개**(D1~D33). **해소 13건**(D3·D8·D9·D10·D11·D12·D13·D17·D18·D20·D22·D24, (3회차) D26), **미해소 20건**. **blocking 0건** — 모두 [기본값]이 있다. PM 확인 권장: D25(UI 프레임워크, 웨이브 4 전), D29(측정 시작 버튼), D31(개발자 모드 의존 방향). (4회차 변화 없음)
- 실측/실행 필요: 번호 **42개**(U1~U42) 중 U19는 대체되어 **미확정 41건**(3회차 변화 없음). 형식상 blocking은 없지만 **착수 전 확인 권장**: IMP-LOOP 전 U1·U2·U31, IMP-COLLISION 전 U5·U7·U8·U30·U32, IMP-DEV 전 U40(웨이브 0 스파이크 (a)~(j)로 한 번에 확인). U6은 E2의 의미에, U31은 F17 A의 결정론 성립 자체에 영향을 준다.
