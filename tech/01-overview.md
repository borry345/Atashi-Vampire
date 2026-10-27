> 분할 파일 — 원본 `TECH_SPEC.md`(5회차)를 2026-10-27에 제목 단위로 나눈 것이다. 내용은 원본 그대로이며 색인·개정 이력은 `tech/README.md`에 있다. 수정은 tech-spec-writer만 한다.

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

