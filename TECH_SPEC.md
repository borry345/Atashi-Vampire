# TECH_SPEC: 뱀서라이크 1차 MVP

> 입력: `SPEC.md`(3회차, F1~F19), `CODING_GUIDE.md`, `RUN_LOG.md` 2026-10-12·2026-10-16 항목(Q40·Q41 확정, 피해 틱 설계, 객체 ID 세대 번호, 구현 단계 조정 항목의 PM 기본값). RUN_LOG의 확정 결정은 SPEC.md보다 우선한다.
> 이 문서는 `SPEC.md`를 수정하지 않는다. 기능 범위는 SPEC.md 그대로이며, 범위에 영향을 줄 수 있는 판단은 6절에 적었다.
> 표기: **[기본값]** = PM이 정한 구현 단계 기본값이며 구현·밸런싱 때 바꿀 수 있다. **[실측]** = Unity 동작 전제이며 실행해 보기 전에는 확정할 수 없다(6.2절에 번호를 붙였다).

---

## 1. 개요 (Overview)

Unity 6000.3.26f1(2D, C#)로 만드는 뱀서라이크 1차 MVP의 기술 설계다. 이 프로젝트의 핵심은 기능의 양이 아니라 **측정 가능한 구조**다. 풀링·타깃 탐색·Update 집중화·충돌 판정 네 가지를 전략 인터페이스 뒤로 분리하고, 1/60초 고정 스텝 위에서 **어느 전략 조합을 골라도 스텝 단위 게임 결과가 똑같이** 나오게 만든 다음, 개발자 모드에서 비용 차이만 잰다. 따라서 이 문서의 대부분은 기능 로직보다 **결정론 계약**에 할애한다. 스텝 안의 단계 순서, 객체 ID 형식, 정렬·타이브레이크 규칙, 난수 스트림 분리, 지연 명령 큐가 그 내용이다. 하나라도 계층마다 다르게 구현되면 E1·E2 동등성 테스트와 F10 측정이 무의미해진다. 저장소에는 아직 코드도 Unity 프로젝트도 없다. 그래서 "기존 구조"를 읽는 대신 SPEC이 요구하는 구조의 의존 관계와, 설계가 기대는 Unity 동작 전제를 2절에 정리했다. 병렬 구현자가 바로 착수할 수 있도록 C# 시그니처 수준의 계약, JSON 스키마 7종, 공유 규칙 표, 파일 소유권 맵, 디스패치 순서는 5절에 확정했다.

---

## 2. 기존 코드 구조 분석 (Existing Code Structure Analysis)

### 2.0 읽을 코드가 없다는 사실

- 프로젝트 루트(`C:\Users\SSAFY\Desktop\borry\indiv`)에는 하네스 문서(`CLAUDE.md`, `HANDOFF.md`, `ORCHESTRATION.md`, `CODING_GUIDE.md`, `RUN_LOG.md`, `DESIGN_NOTES.md`)와 기획 문서(`PRD.md`, `SPEC.md`, 계획 메모)만 있다. `.cs` 파일도, Unity 프로젝트 폴더(`Assets/`, `ProjectSettings/`, `Packages/`)도 없다(F15 사용자 수행 전).
- 그래서 이 절의 "함수/인터페이스 참조 표"는 기존 코드가 아니라 **SPEC.md가 이름까지 지정한 계약**을 출처(SPEC 절)와 함께 나열한 것이다. 시그니처는 5절에서 새로 정한다.

### 2.1 SPEC이 이름을 지정한 계약 (출처 추적표)

| 계약 | SPEC 출처 | SPEC이 정한 것 | 정하지 않은 것 (5절에서 정함) |
|---|---|---|---|
| `IInputSource` | F1 마지막 기준, F19 설계 제약 | 이동 로직은 키보드를 직접 읽지 않고 입력 소스를 통한다. 자동 경로를 주입한다 | 반환 형식, 속도 지정 방법 |
| 풀 인터페이스(획득/반환) | F9 | 구현 A 자체 풀 / B `Instantiate`·`Destroy` / C `UnityEngine.Pool.ObjectPool<T>` | 시그니처, 사전 생성, 판 리셋 |
| `ITargetProvider` | F2, F16 | 전수 순회 / 공간 해시 / `Physics2D` 쿼리. 제곱거리 비교, ID 타이브레이크, 보스는 별도 리스트 | 질의 시점, 반환 형식, 공간 해시 공유 방식 |
| `IUpdatable` + 중앙 틱 | F17 | 몬스터·투사체만 등록. A는 객체별 `FixedUpdate`, B는 중앙 순회 | 순회 순서 독립성 보장 방법 |
| `ICollisionStrategy` (폴링, "이번 스텝 이벤트 목록") | F18 + RUN_LOG Q41 | Enter·Exit 두 종류, ID 순 정렬, 엔진판은 콜백을 큐에 모은다 | 이벤트 구조체, 채널, 등록 해제 시 Exit 처리 |
| 고정 스텝 | 3.0 F | 1/60초, catch-up 상한 3(`Time.maximumDeltaTime = 0.05`), 게임 시간 = 스텝 수 × 1/60 | 스텝 내부 단계 순서 |
| 결정론 RNG | 3.0 F, F16 | 고정 시드 가능 | 생성기 종류, 스트림 분리 |
| JSON 7종 + 로더·레지스트리·검증 | F11, F18 터널링 | 필수 4/선택 3, 플레이 불가 4기준, 정의 단위 건너뛰기, `v_x/60 ≤ r_x` 2단 검증 | 필드명·타입·단위 |
| 개발자 모드 | F19 + RUN_LOG Q40 | `--dev` 인자, F1 오버레이, `if (devMode)` 금지, 값 주입 | 주입 지점 목록, 통계·CSV 형식 |

### 2.2 SPEC 요구 구조의 의존 관계 분석

**(a) 계층 의존 방향** (화살표 = "의존한다")

```
Dev(F19) ──▶ App(조립) ──▶ Flow/UI(F5,F8)
   │            │              │
   │            ▼              ▼
   │      Strategies(F9,F16,F17,F18) ◀── Gameplay(F1,F2,F3,F4,F6,F7)
   │            │                           │
   └──────────▶ Core(계약·공유 규칙) ◀──────┘
                ▲
             Data(F11) ──▶ Core
```

- Core는 아무것에도 의존하지 않는다(UnityEngine 수학 타입은 사용).
- Gameplay는 Core 인터페이스와 Data 정의 객체에만 의존하고, Strategies 구현 클래스를 참조하지 않는다(F9·F16·F17·F18 "인터페이스에만 의존").
- **어떤 게임 어셈블리도 Dev를 참조하지 않는다.** 이렇게 하면 F19의 "`if (devMode)` 분기 금지"가 컴파일러 수준에서 강제된다(5c.1의 asmdef 구조).

**(b) 전략 4건의 실제 결합 관계** (SPEC 3.0 B는 "서로 독립적으로 전환 가능"을 요구한다)

| 결합 | 원인 (SPEC 근거) | 설계 대응 |
|---|---|---|
| F16 공간 해시 ↔ F18 자체 판정 | F16 마지막 기준 "F18이 후보 추림에 재사용", F10 "두 전략은 독립이 아니다" | 공간 해시를 **하나의 공유 인스턴스**(`MonsterSpatialIndex`)로 둔다. 타깃=공간 해시 **또는** 충돌=자체 중 하나라도 켜져 있으면 스텝마다 한 번 재구축한다. "전부 켬"에서 유지 비용이 한 번만 드는 것이 측정할 상호작용이다 |
| F16 C(`Physics2D` 쿼리) ↔ F18 A(`Collider2D`) | F18 "몬스터 `Collider2D`는 구현 A와 F16 C일 때만 켠다" | `ColliderActivationPolicy.MonsterCollidersEnabled(selection)` 한 곳에서 판정한다(5a) |
| F9 풀 ↔ 객체 ID | RUN_LOG "ID에 세대 포함, 풀 반납마다 증가" | **ID 할당을 풀에서 분리한다.** ID를 풀 슬롯에서 파생하면 구현 B(풀 없음)에는 슬롯이 없어 전략마다 ID가 달라지고, 타이브레이크·이벤트 로그가 갈린다. 시뮬레이션 소유의 `EntityIdAllocator`가 ID를 발급하고, 디스폰(=반납) 시 세대를 올린다 |
| F17 A(객체별 `FixedUpdate`) ↔ 결정론 | Unity는 같은 클래스 인스턴스끼리의 호출 순서를 보장하지 않는다 [실측 U1] | `Tick`은 **자기 상태만 쓰고**, 다른 객체에 영향을 주는 일(투사체 생성, 소환, 사망)은 지연 명령 큐로 미룬다. 큐는 ID 순으로 적용한다(5a R10) |
| F17 A ↔ 측정 공정성 | 객체마다 `FixedUpdate` 메서드가 있으면 Unity는 B 모드에서도 그것을 호출한다 | 엔티티 클래스에는 `FixedUpdate`를 두지 않는다. A 모드일 때만 켜지는 별도 컴포넌트(`MonsterSelfTicker`/`ProjectileSelfTicker`)가 `FixedUpdate`를 가진다 |
| F18 엔진판 ↔ 스텝 경계 | Unity의 물리 시뮬레이션과 `OnTrigger*` 콜백은 모든 `FixedUpdate` **뒤**에 돈다 [실측 U7] | `Physics2DSettings.simulationMode = Script`로 두고, 엔진 전략의 `CollectEvents()` 안에서 `Physics2D.Simulate(1/60)`을 직접 호출한다. 그래야 같은 스텝 번호 안에서 이벤트가 나와 자체판과 스텝 번호가 일치한다 |

**(c) 스텝 내부 단계 순서** (결정론의 뼈대, 5절 §5.4에서 시그니처 확정)

```
FixedUpdate 1회 = 스텝 1회 (게이트가 열려 있을 때만)
 [Begin]          대기 명령 적용(레벨업 선택, 개발자 조작) → 입력 소비
 [PlayerMove]     플레이어 이동
 [MonsterTick]    몬스터 AI·이동      ← F17 A: MonsterSelfTicker(실행 순서 -500) / B: 중앙 틱
 [BuildIndex]     몬스터 스냅샷 확정 → 공간 해시 재구축(필요 시) → 타깃 공급자 갱신
 [WeaponTick]     무기 쿨다운·발사·궤도 위치·장판 생성 (타깃 질의는 스냅샷 기준)
 [ProjectileTick] 투사체 이동·수명    ← F17 A: ProjectileSelfTicker(실행 순서 +500) / B: 중앙 틱
 [Collision]      ICollisionStrategy.CollectEvents(step)
 [Resolve]        이벤트 라우팅 → 피해/획득/무적/쌍별 쿨다운 → 사망 처리(드랍)
 [Lifecycle]      지연 명령 적용 → 수명 만료·디스폰 → 스폰 → 레벨업 판정
 [End]            step++ , 상태 해시(테스트/개발자 모드에서만 조회)
```

### 2.3 설계가 기대는 Unity 6 API·동작 전제 (전부 실측 필요)

아래는 이 설계가 **사실이라고 가정하고** 기대는 Unity 동작이다. 코드를 실행해 확인한 것이 아니며 하나도 확정되지 않았다. 번호는 6.2절과 같다.

| # | 전제 | 이 전제에 기대는 설계 |
|---|---|---|
| U1 | `[DefaultExecutionOrder]`가 `FixedUpdate`에도 적용되고, 같은 클래스 인스턴스 간 호출 순서는 보장되지 않는다 | 드라이버 3개 + 셀프 티커 2개의 인터리브(2.2 c) |
| U2 | `Time.fixedDeltaTime = 1/60`, `Time.maximumDeltaTime = 0.05`이면 한 프레임의 `FixedUpdate`가 최대 3회다(부동소수 반올림으로 2회나 4회가 되지 않는다) | catch-up 상한 3 |
| U3 | `Time.captureDeltaTime = 1/30, 1/60, 1/144`로 프레임레이트를 흉내 내면 `FixedUpdate` 누적이 그에 맞게 동작한다 | 프레임레이트 독립 PlayMode 테스트 |
| U4 | `Rigidbody2D.collisionDetectionMode` 기본값은 `Discrete`다 (SPEC Q39) | E1 조건 |
| U5 | 키네마틱 ↔ 키네마틱 트리거 콜백이 발생한다(필요하면 `useFullKinematicContacts`로) | 엔진 충돌 전략의 바디 구성 |
| U6 | `Continuous` 모드가 트리거·키네마틱 바디의 판정 결과를 바꿀 수 있다 | E2가 의미 있는 검증인지 여부 |
| U7 | `simulationMode = Script`에서 `Physics2D.Simulate()` 호출 중(또는 직후)에 `OnTriggerEnter2D/Exit2D`가 동기적으로 호출된다 | 엔진판 이벤트가 같은 스텝 번호에 나온다 |
| U8 | `Physics2D.callbacksOnDisable = false`이면 콜라이더 비활성화 시 Exit 콜백이 나오지 않는다 | "등록 해제 시 Exit 없음" 규칙의 엔진판 일치 |
| U9 | Box2D 원-원 겹침의 경계(거리 = 반지름 합)가 자체 판정 `≤`와 같은 쪽으로 판정된다 | E1 경계 사례 |
| U10 | `Rigidbody2D.MovePosition`은 스윕 이동이고, `transform.position`을 직접 쓰면 다음 시뮬레이션에서 텔레포트로 처리된다 | 엔진 모드에서 표시 동기화를 생략한다 |
| U11 | `Rigidbody2D.position`을 바꾸거나 콜라이더를 켠 직후 `Physics2D.OverlapCircle`에 반영된다(`autoSyncTransforms`·`SyncTransforms` 영향) | `Physics2D` 타깃 공급자의 여유 반경 |
| U12 | 이름 없는 레이어 인덱스(8~17)에 `Physics2D.IgnoreLayerCollision`을 코드로 설정할 수 있다 | 레이어 충돌 매트릭스를 코드로 설정 |
| U13 | Windows 플레이어에서 `Application.streamingAssetsPath` + `File.ReadAllText`가 동기로 동작하고, 빌드 폴더 `<Name>_Data/StreamingAssets/`를 편집할 수 있다 | F11 로더, F13 |
| U14 | `ProfilerMarker`는 비개발 빌드에서 호출째 제거되고(`ENABLE_PROFILER`), `ProfilerRecorder`는 Development Build에서 마커의 프레임 합계를 읽으며, `Begin`/`End`를 다른 스크립트에서 짝지어도 된다 | 시스템 ms 수집(릴리스 빌드에서 감시 비용 0) |
| U15 | `ProfilerRecorder`로 "GC Allocated In Frame" 카운터를 읽을 수 있다 | F10 항목 3 |
| U16 | Windows 플레이어에서 `Environment.GetCommandLineArgs()`에 `--dev`가 그대로 들어온다 | Q40 활성화 |
| U17 | Unity 6 2D 템플릿에 Input System 패키지가 기본 활성이고 업데이트 모드가 Dynamic이라 `wasPressedThisFrame`이 `Update`에서 유효하다 | 입력 수집 |
| U18 | 한글 글꼴 에셋을 넣으면 UI Toolkit·IMGUI가 한글을 렌더링한다 | 모든 UI |
| U19 | UI Toolkit `UIDocument`가 Windows 플레이어에서 동작하고 `PanelSettings`를 런타임에 만들 수 있다(안 되면 에디터에서 만든 에셋 1개) | UI 구성 |
| U20 | 런타임에 만든 `Texture2D` 원 스프라이트의 월드 크기를 `sprite.bounds`로 알 수 있다 | 반지름 → 표시 배율 |
| U21 | `ObjectPool<T>`의 `maxSize`를 넘겨 반환하면 객체가 파괴되고, `collectionCheck`가 중복 반환을 잡는다 | 풀 구현 C |
| U22 | `Destroy`는 프레임 끝까지 지연되므로 `SetActive(false)`를 먼저 해야 같은 프레임의 다음 스텝에 끼어들지 않는다 | 풀 구현 B |
| U23 | `Time.maximumDeltaTime`은 스케일된 `deltaTime`을 제한한다(그러면 `timeScale = 4`도 60FPS에서 최대 3스텝) | 개발자 모드 배속 |
| U24 | 1920x1080 창 모드가 1080p 모니터에서 크기 조정 없이 16:9를 유지한다 | Q44 화면 밖 계산 |
| U25 | `com.unity.nuget.newtonsoft-json` 패키지를 6000.3에서 쓸 수 있고 Mono 백엔드에서 동작한다 | JSON 파서(6.1 D17) |
| U26 | 같은 빌드·같은 PC에서 float 연산은 실행마다 비트 단위로 같다(Mono JIT) | 프레임레이트 간 결정론 |
| U27 | `Unity.exe -batchmode -runTests -testPlatform PlayMode`로 물리를 포함한 PlayMode 테스트가 돈다 | E1·E2 자동 실행 |
| U28 | 6.3의 새 저수준 2D 물리 API와 무관하게 기존 `Physics2D`·`Rigidbody2D` API가 그대로 동작한다 | F18 A, F16 C |
| U29 | `SpriteRenderer.drawMode = Tiled`로 반복 배경을 그릴 수 있다 | F1·F8 반복 배경 |

### 2.4 Q44 카메라 크기 검증 (계산이며, 실측 아님)

- orthographic size 9 → 화면 세로 절반 = 9유닛, 16:9이면 가로 절반 = 9 × 16/9 = 16유닛.
- 화면 반대각 = √(16² + 9²) = √337 ≈ **18.358**유닛. 스폰 반경 20보다 작으므로 **스폰 지점(중심)은 항상 화면 밖**이다(SPEC F3 기준 충족).
- 여유는 20 − 18.358 ≈ **1.64유닛**이고, 이 값은 모서리 방향에서만 적용된다(가로 방향 여유 4, 세로 방향 여유 11). **반지름이 1.64 이상인 몬스터가 모서리 방향으로 스폰되면 몸체 일부가 스폰 순간 화면에 보인다.** SPEC 기준은 "스폰 지점"이라 위반은 아니지만, 보스처럼 큰 객체는 등장 순간이 보일 수 있다(6.1 D18).
- 창 비율이 16:9에서 벗어나면 가로 절반 = 9 × aspect로 바뀐다. aspect가 2.0이면 √(18² + 9²) ≈ 20.12 > 20이 되어 깨진다. 창 크기 고정 전제는 U24.
- 디스폰 거리 45는 반대각 18.36의 약 2.45배라 화면 안에서 디스폰되는 일은 없다.

---

## 3. 기능 목록 (Feature List)

SPEC.md의 ID를 그대로 쓴다. 추가·삭제는 없다.

| ID | 이름 | 구분 | 담당 구현자(5c) |
|---|---|---|---|
| F1 | 플레이어 이동 | 구현 | IMP-PLAYER (입력 소스: IMP-LOOP) |
| F2 | 자동 공격 | 구현 | IMP-PLAYER |
| F3 | 몬스터 | 구현 | IMP-MONSTER |
| F4 | 아이템 드랍과 획득 | 구현 | IMP-MONSTER (상자 효과 결정: IMP-PLAYER) |
| F5 | 레벨업과 선택지 | 구현 | IMP-FLOW |
| F6 | 무기 시스템 | 구현 | IMP-PLAYER |
| F7 | 조합(진화) | 구현 | IMP-PLAYER |
| F8 | 스테이지와 게임 종료 흐름 | 구현 | IMP-FLOW |
| F9 | 오브젝트 풀링 | 구현 + 전략 | IMP-POOL |
| F10 | 성능 측정과 비교 기록 | 측정 훅 구현 + 사용자 수행 | IMP-DEV (훅) / 사용자 |
| F11 | 데이터 기반 설계 | 구현 | IMP-DATA |
| F12 | README | 문서 | doc-writer (구현 완료 후) |
| F13 | 빌드와 시연 영상 | 사용자 수행 (빌드 설정 일부 구현) | 사용자 / IMP-LOOP(설정 적용 코드) |
| F14 | 품질 관리 기록 | 사용자 수행 | 사용자 (`docs/devlog.md` 틀: doc-writer) |
| F15 | 개발 환경과 저장소 | 사용자 수행 | 사용자 (폴더 구조는 5c.1) |
| F16 | 타깃 탐색 전략 | 구현 + 측정 | IMP-SPATIAL |
| F17 | Update 집중화 전략 | 구현 + 측정 | IMP-LOOP |
| F18 | 충돌 판정 전략 | 구현 + 측정 | IMP-COLLISION |
| F19 | 개발자 모드 | 구현 | IMP-DEV |

공통 기반(3.0 F 고정 스텝, 3.0 G 무한 평면, ID·RNG·공유 규칙)은 기능 ID가 따로 없으므로 IMP-CORE와 IMP-LOOP가 맡는다.

---

## 4. 기능별 요구사항 및 수용 기준 (Functional Requirements & Acceptance Criteria)

SPEC.md 3절의 수용 기준 202개는 그대로 유효하며 여기서 반복하지 않는다. 아래는 그 기준을 이 설계로 구현할 때 추가로 지켜야 하는 **기술 수용 기준(TAC)** 이다. 각 항목은 5절의 계약과 5a의 공유 규칙을 근거로 하며, 테스트 위치를 함께 적었다(EM = EditMode, PM = PlayMode).

### 3.0 F (공통) 고정 스텝·결정론

- TAC-0-1: 게임 상태를 바꾸는 코드는 `SimulationPhase` 핸들러나 `IUpdatable.Tick` 안에서만 실행된다. 그 밖의 경로에서 상태를 바꾸는 것은 `Begin` 단계에 적용되는 대기 명령(`PendingCommandQueue`)뿐이다. 레벨업 선택, 개발자 조작이 여기에 해당한다. (코드 리뷰 + PM)
- TAC-0-2: 로직 코드는 `Time.deltaTime`, `Time.time`, `Time.fixedDeltaTime`, `UnityEngine.Random`, `System.Random`, `DateTime`을 읽지 않는다. 시간은 `StepContext.Step`, 난수는 `RngStreams`만 쓴다. 예외는 일반 플레이의 시드 생성 1회뿐이다. (정적 검사: grep 테스트를 EM에 둔다)
- TAC-0-3: `Time.fixedDeltaTime == 1f/60f`, `Time.maximumDeltaTime == 0.05f`를 부트스트랩에서 코드로 설정하고 PM 테스트로 확인한다. 한 프레임의 스텝 수가 3을 넘지 않는다 [실측 U2].
- TAC-0-4: 같은 시드와 같은 입력 시퀀스로 `Time.captureDeltaTime`을 1/30, 1/60, 1/144로 바꿔 같은 스텝 수만큼 돌리면 `SimulationStateHash`가 같다 (PM, U3).
- TAC-0-5: 일시정지와 레벨업 중에는 `SimulationClock.IsRunning == false`라 어떤 단계도 실행되지 않고, F17 A의 셀프 티커도 `Tick`을 호출하지 않는다 (PM).
- TAC-0-6: `IUpdatable.Tick`은 자기 상태와 `StepContext`가 주는 읽기 전용 값만 읽고 쓴다. 다른 엔티티의 생성·소멸·피해는 `ISimulationCommands`로 미룬다 (코드 리뷰 + F17 A/B 동등성 PM).

### F1. 플레이어 이동

- TAC-F1-1: `PlayerMovement`는 `IInputSource.ReadMove(step, position)`만 호출한다. `Keyboard.current`를 참조하는 코드는 `KeyboardInputSource` 한 곳에만 있다.
- TAC-F1-2: 대각 방향은 `MoveMath.ToUnitDirection(x, y)`의 8방향 상수 표를 쓴다. 단일 축과 대각의 스텝당 이동 크기 차이가 1e-6 이하다 (EM).
- TAC-F1-3: 속도 = `PlayerStats.moveSpeed × (1 + P2 보너스)` / 60(스텝당). `MoveCommand.SpeedOverride ≥ 0`이면 그 값을 쓴다(자동 경로 전용) (EM).
- TAC-F1-4: 위치를 제한하거나 보정하는 코드가 없다. 반복 배경 `BackgroundTiler`는 `LateUpdate`에서 카메라 위치를 셀 크기 배수로 스냅해 따라간다 [실측 U29].
- TAC-F1-5: 게임패드 입력이 아무 효과도 내지 않는다. `KeyboardInputSource`는 `Keyboard.current`만 읽는다 (PM).

### F2. 자동 공격

- TAC-F2-1: 시작 시 `PlayerStats.startingWeaponId`(기본 "W1") 무기를 1단계로 보유한다.
- TAC-F2-2: 무기 쿨다운은 "다음 발사 허용 스텝"(`_nextFireStep`)으로 관리한다. 발사 조건은 `step ≥ _nextFireStep && Rules.WeaponsEnabled && 타깃 있음`이고, 발사하면 `_nextFireStep = step + 유효 쿨다운 스텝`이다. 타깃이 없으면 발사하지 않고 `_nextFireStep`도 그대로 두어 다음 스텝에 다시 시도한다 (EM).
- TAC-F2-3: 유효 쿨다운 = `StatMath.ApplyAttackSpeed(baseCooldownSteps, P1 보너스)` (5a R13) (EM).
- TAC-F2-4: 무기 코드는 `ITargetProvider`(`StepContext.Targets`)만 호출한다. `Strategies` 어셈블리 참조가 asmdef에 없으므로 구현 클래스를 직접 참조하면 컴파일되지 않는다.
- TAC-F2-5: 공격 on/off는 `SimulationRules.WeaponsEnabled` 값으로만 제어한다.

### F3. 몬스터

- TAC-F3-1: 행동 타입은 `chaser`(M1), `charger`(M2), `ranged`(M3), `boss`(M4) 4종이고 `Monster`가 `MonsterBehavior` 값으로 분기한다. 새 행동 타입은 코드 추가 대상이다(F11 경계).
- TAC-F3-2: 스폰 수 = `SpawnScheduler.SpawnsDueAtStep(rate, k)` = ⌊(k+1)·rate/60⌋ − ⌊k·rate/60⌋ (k = 구간 시작부터 센 스텝). 정수 초당 속도에서는 1분 스폰 수가 정확히 rate × 60이다(±10% 기준의 상위 보장) (EM).
- TAC-F3-3: 상한(`Rules.MonsterCap`, 기본 400)에 도달하면 그 스텝의 남은 스폰은 버린다. 누적 변수가 없으므로 몰아서 스폰할 수 없다. 상한 판정은 스폰 1기마다 `live < cap`이다 (EM).
- TAC-F3-4: 스폰 위치 = `SpawnGeometry.PointOnCircle(playerPos, GameRules.SpawnRadius, rng.NextFloat01())` 1회 (5a R7). 재추첨 코드가 없다 (EM).
- TAC-F3-5: 디스폰은 `Lifecycle` 단계에서 보스를 제외한 몬스터의 `(pos − playerPos).sqrMagnitude > D²`를 검사하고, `EntityLifecycle.Despawn(id, DespawnReason.OutOfRange)`로 회수한다. 이 경로는 `KillPipeline`을 거치지 않으므로 드랍·처치 수가 없다 (EM).
- TAC-F3-6: 보스 스폰 스텝 = `GameRules.BossSpawnStep`(36000). 그 스텝의 `Lifecycle`에서 일반 스폰을 끝내고 보스 1기를 상한과 무관하게 스폰한다. 위치는 일반 스폰과 같은 원주다 [기본값, 6.1 D4].
- TAC-F3-7: M2 상태 기계는 `Approach → Charge(chargeDurationSteps 동안 고정 방향, chargeSpeed) → Cooldown(chargeCooldownSteps, 이 동안 moveSpeed로 추적) → Approach`다. 돌진 트리거는 `dist² ≤ chargeTriggerDistance²`이고 Approach 상태에서만 판정한다 (EM).
- TAC-F3-8: M3는 `dist² ≤ attackRange²`이면 이동을 멈추고, `fireCooldownSteps`마다 `ISimulationCommands`로 적 투사체 생성을 요청한다. 직접 생성하지 않는다 (EM).
- TAC-F3-9: M4는 `moveSpeed`로 추적하며, 패턴 ①(`hazardZone`: 플레이어 위치에 적 장판)과 ②(`summon`: 보스 주변 `summonRadius` 원주에 소형 몬스터 `count`기)를 각각 독립 쿨다운으로 쓴다. 소환은 상한 검사를 받는다 [기본값, D4] (EM).
- TAC-F3-10: 몬스터 생성·회수는 `EntityLifecycle`만 거친다(풀 → ID → 초기화 → 틱 등록 → 충돌 등록 → 몬스터 레지스트리 등록, 회수는 역순, 5a R11).

### F4. 아이템 드랍과 획득

- TAC-F4-1: 처치(`KillPipeline.OnMonsterKilled`)마다 처치 위치에 젬 1개를 만든다. 젬 경험치 = `PlayerStats.pickups.gemXpValue`. 보스 처치는 드랍 없이 F8 클리어 이벤트만 낸다 [기본값].
- TAC-F4-2: 상자 판정 `rng(Drop).Chance(GameRules.ChestDropChance = 0.02f)`를 처치마다 정확히 1회 뽑는다. 처치 처리 순서는 `Resolve` 단계의 피해 적용 순서(이벤트 정렬 순서)를 따른다 (EM: 시드 고정 10,000회, 허용 오차 [기본값] ±0.4%p).
- TAC-F4-3: 젬은 `PlayerMagnetVsGem` Enter에서 끌림 상태가 되어 매 스텝 `gemAttractSpeed`로 플레이어를 향해 이동하고, `PlayerBodyVsGem` Enter에서 획득된다(채널 2개, 6.1 D16). 끌림은 되돌리지 않는다(Exit 무시).
- TAC-F4-4: 상자 효과는 `ChestEffectResolver.Resolve(rng(Chest))`가 결정한다. ① `EvolutionService.FindFirstEligible()`(무기 슬롯 순서, Q42 기본값)가 있으면 진화하고 RNG를 뽑지 않는다. ② 없으면 `Chance(0.70f)`가 참이면 강화, 거짓이면 회복이다. 강화 대상이 없으면 회복으로 바꾼다. 강화 대상은 "5단계 미만이고 진화 무기가 아닌 보유 무기"를 슬롯 순서로 나열한 목록에서 `NextInt(count)`로 고른다 [기본값] (EM).
- TAC-F4-5: 회복량 = `maxHealth × GameRules.HealRatio(0.30)`이고 결과는 `min(maxHealth, hp + 회복량)`이다 (EM).

### F5. 레벨업과 선택지

- TAC-F5-1: 후보 생성 `LevelUpCandidateBuilder.Build(state)`는 후보를 **정규 순서**(5a R12)로 반환한다. 비복원 추출은 `WeightedSampler.SampleWithoutReplacement`(5a R9)이고 RNG는 `LevelUp` 스트림이다 (EM).
- TAC-F5-2: 회복 후보 가중치 = `HealWeight.Compute(wBase, hp, maxHp)`(5a R8). 0이면 후보에서 뺀다. 추출 뒤 3칸에 못 미치면 남은 칸을 `Heal` 채움 항목으로 채운다(만체력이어도) (EM).
- TAC-F5-3: 제외 규칙은 다음과 같다. 새 무기는 무기 슬롯 4개가 차면 제외, 새 패시브는 패시브 3개가 차면 제외, 5단계(`Levels.Count`)인 항목의 강화는 제외, `evolvedOnly` 무기는 항상 제외, 진화로 소모된 원본 무기의 "새 획득"도 제외한다 [기본값, D5] (EM).
- TAC-F5-4: 경험치 임계값은 `PlayerStats.xpCurve`(5.7 스키마)를 따른다. 젬 획득으로 여러 레벨이 한 번에 오르면 `PendingLevelUps`가 그만큼 늘고, `GameFlowController`가 하나씩 처리한다 (EM + PM).
- TAC-F5-5: 선택(키 1/2/3 또는 클릭)은 `PendingCommandQueue`에 `ApplyLevelUpChoice(index)`로 들어가 다음 스텝의 `Begin`에서 적용된다. 키 입력과 클릭이 같은 명령을 만든다 (PM).
- TAC-F5-6: `Rules.LevelUpEnabled == false`이면 경험치만 쌓이고 레벨·선택지는 생기지 않는다 [기본값, D19].

### F6. 무기 시스템

- TAC-F6-1: 무기 인스턴스 4종은 `ProjectileWeapon`(W1, E1, 그리고 `homing=true`이면 W4)·`OrbitWeapon`(W2, E2)·`ZoneWeapon`(W3)이고, 행동 타입 문자열로 생성한다(`WeaponFactory`).
- TAC-F6-2: W1 방향 = `MoveMath.DirectionOrDefault(target − playerPos)`(영벡터면 `Vector2.right`). 발사 후 방향은 바뀌지 않는다. `projectileCount > 1`이면 `spreadDegrees`를 균등 분할해 같은 스텝에 쏜다 (EM).
- TAC-F6-3: W4 투사체는 `ProjectileTick`에서 매 스텝 `Targets.TryFindNearest(자기 위치)`로 방향을 다시 잡는다. 대상이 없으면 마지막 방향으로 직진하고 수명이 끝나면 소멸한다 [기본값 Q9]. W1·W4 모두 사거리 없이 `lifetimeSteps`로 소멸한다 [기본값 Q9].
- TAC-F6-4: W2 궤도 원 위치 = `playerPos + R·(cos θ_i, sin θ_i)`, `θ_i = θ0 + ω·step + 2π·i/orbCount`. 스텝 번호에서 바로 계산하므로 누적 오차가 없다 (EM).
- TAC-F6-5: W3 장판은 쿨다운마다 `Targets.TryFindNearest(playerPos)`의 위치에 생성한다 [기본값 Q8]. 대상이 없으면 생성하지 않고 다음 스텝에 다시 시도한다 [기본값, D24]. 장판은 `durationSteps` 뒤 회수한다.
- TAC-F6-6: W2·W3 지속 피해는 무기 인스턴스가 소유한 `ContactDamageTracker`(5.12)가 처리한다. Enter 시 허용 스텝이면 즉시 1회, 겹침이 이어지면 `hitIntervalSteps`마다 1회, Exit 뒤에도 허용 스텝까지 기록을 유지한다 (EM).
- TAC-F6-7: 강화 값은 `levels[level-1]` 전체 파라미터 집합을 그대로 쓴다(차분이 아닌 절대값). 최대 단계 = `levels.Count`(기본 무기 5, 진화 무기 1).

### F7. 조합(진화)

- TAC-F7-1: 자격 조건 = `weapon.Level == weapon.Definition.MaxLevel && passives.Owns(rule.passiveId) && !evolvedAlready(rule)`. `FindFirstEligible()`은 무기 슬롯 순서로 훑어 첫 자격 규칙을 반환한다 (EM).
- TAC-F7-2: 진화는 같은 슬롯 인덱스에서 원본 인스턴스를 결과 무기 1단계 인스턴스로 교체한다. 원본의 궤도 원·장판 같은 활성 자식 엔티티는 교체 스텝의 `Lifecycle`에서 회수한다. 슬롯 수는 바뀌지 않는다 (EM).
- TAC-F7-3: 조합 규칙 파일이 없으면 `GameData.Combinations`가 빈 목록이 되어 `FindFirstEligible()`은 항상 없음을 반환한다.

### F8. 스테이지와 게임 종료 흐름

- TAC-F8-1: 씬은 `Main.unity` 하나다. 타이틀·플레이·일시정지·레벨업·결과·데이터 오류는 `GameFlowController`의 상태(`FlowState`)이고 UI 패널 전환으로 표현한다. 씬 로드가 없으므로 판 초기화는 `RunSession.Reset()` 한 곳에서 한다.
- TAC-F8-2: `RunSession.Reset()`은 다음 순서로 실행한다. `IPoolStrategy.ReleaseAll()` → `ICollisionStrategy.Clear()` → 틱 등록 해제 → `EntityIdAllocator.Reset()` → `RngStreams` 재시드 → `SimulationClock.Reset()` → 플레이어 재생성. 재시작과 "타이틀로 나가기" 모두 이 함수를 쓴다 (PM).
- TAC-F8-3: 생존 시간은 스텝 수(`long`)로 기록한다. 사망하면 사망 스텝, 클리어하면 보스 처치 스텝이다 [기본값 Q12]. 최고 기록은 `PlayerPrefs` 키 `"BestSurvivalSteps"`(int)에 저장하며, 사망·클리어와 무관하게 더 클 때만 갱신한다 [기본값 Q12]. 기록이 없으면 결과 화면에 "-"를 표시한다 [기본값 Q12].
- TAC-F8-4: 데이터 검증 결과가 `IsPlayable == false`이면 `FlowState.DataError`가 되어 시작 버튼이 비활성화되고, 화면에 `DataValidationReport.BlockingIssues`를 파일명·정의 ID·필드와 함께 표시한다.
- TAC-F8-5: 화면은 `Screen.SetResolution(1920, 1080, FullScreenMode.Windowed)`이고, Player Settings의 크기 조절을 끈다(사용자 설정, 5b) [실측 U24].

### F9. 오브젝트 풀링

- TAC-F9-1: 게임 코드는 `IPoolStrategy`만 참조하고, 그것도 `EntityLifecycle` 한 곳에서만 호출한다.
- TAC-F9-2: 구현 A(`CustomPoolStrategy`)는 템플릿별 비활성 스택(LIFO)이다. 비면 `Instantiate`로 확장하고 상한은 없다 [기본값 Q13]. 사전 생성 개수는 `PoolPrewarmCounts` 코드 상수다 [기본값 Q13, 측정 후 조정] (EM: 반환 후 재요청 시 같은 인스턴스).
- TAC-F9-3: 구현 B는 `Acquire`마다 `Instantiate`, `Release`마다 `SetActive(false)` 후 `Destroy`다 [실측 U22] (PM).
- TAC-F9-4: 구현 C는 템플릿별 `UnityEngine.Pool.ObjectPool<GameObject>`이고 `collectionCheck: true`, `maxSize: int.MaxValue`다 [실측 U21].
- TAC-F9-5: 중복 반환이면 세 구현 모두 `InvalidOperationException`을 던지고, 그 전에 `Debug.LogError`로 객체 이름과 ID를 남긴다. 정상 경로에서는 `EntityLifecycle`이 `EntityIdAllocator.IsLive`로 미리 막는다 (EM).
- TAC-F9-6: 풀은 상태를 초기화하지 않는다. 초기화는 `EntityLifecycle`이 엔티티의 `Initialize(...)`를 호출해서 한다(풀 전략과 무관하게 같은 초기화가 보장된다).

### F10. 성능 측정 (측정 훅)

- TAC-F10-1: 시스템 ms는 `ProfilerMarkers`(5a R16)의 마커를 `ProfilerRecorder`(`SumAllSamplesInFrame`)로 읽은 프레임 합계이고, 스텝당 값 = 프레임 합계 / `SimulationClock.StepsThisFrame`이다 [실측 U14].
- TAC-F10-2: 틱 마커는 두 모드 모두 드라이버 경계에서 연다(`Early` 끝에서 Begin, `Mid` 시작에서 End). 그래서 F17 A의 Unity 호출 오버헤드까지 들어간다 [실측 U14].
- TAC-F10-3: CSV 요약 행에 PC 사양(`SystemInfo.processorType`, `graphicsDeviceName`, `systemMemorySize`, `operatingSystem`), 빌드 종류(`Debug.isDebugBuild`), 전략 조합, 적 수, D, 시드, 스텝 수, 스텝 간격이 자동으로 기록된다(5.15).

### F11. 데이터 기반 설계

- TAC-F11-1: 로더는 `Path.Combine(Application.streamingAssetsPath, "Data", <파일명>)`을 `File.ReadAllText`(UTF-8)로 읽는다 [실측 U13]. 에디터 전용 API(`AssetDatabase`)를 쓰지 않는다.
- TAC-F11-2: 검증 결과는 `DataValidationReport` 하나에 모이며, 항목마다 `Severity`, `FileName`, `DefinitionId`, `FieldPath`, `Message`, (파싱 오류면) `Line`·`Column`을 갖는다. 모든 항목은 `Debug.LogWarning`/`LogError`로 한 줄씩 남는다 (EM: 파일 종류마다 실패 픽스처).
- TAC-F11-3: 초 단위 필드는 로드 시 `StepMath.SecondsToIntervalSteps`로, 초당 속도는 `StepMath.PerSecondToPerStep`으로 환산해 정의 객체에 저장한다. 소비자는 스텝 값만 읽는다. 원본 초 값도 덤프용으로 함께 보존한다.
- TAC-F11-4: 터널링 불변식 검증 `TunnelingInvariantValidator`는 1단에서 이동 객체마다 `v/60 ≤ r`를 검사하고, 실패한 객체만 2단에서 실제 상대 레이어(5a R5 채널 표 기준)와 `(v_a+v_b)/60 ≤ r_a+r_b`를 검사해 쌍을 지목하는 경고를 남긴다. 차단하지 않는다 (EM, E3).
- TAC-F11-5: 핫 리로드(`GameDataLoader.LoadAll()` 재실행)의 결과가 플레이 불가면 기존 데이터를 유지하고 오류만 표시한다. 가능하면 `GameDataRegistry.Swap(newData)`로 바꾸며, 다음 스폰·다음 선택지 생성부터 적용되고 활성 객체는 기존 정의를 유지한다 [기본값 Q43].

### F12·F14·F15 (문서/사용자 수행)

- TAC-F12-1: README의 클래스 다이어그램은 5.0의 인터페이스 9개(`IPoolStrategy`, `ITargetProvider`, `ITickStrategy`, `IUpdatable`, `ICollisionStrategy`, `IInputSource`, `ISpawnPolicy`, `ISimulationCommands`, `IStepPhaseHandler`)와 그 구현을 기준으로 그린다. 흐름도는 2.2(c)의 스텝 단계 순서를 기준으로 그린다.
- TAC-F15-1: 폴더 구조는 5c.1을 따른다. `docs/devlog.md` 틀은 doc-writer가 만든다.

### F13. 빌드 (구현 부분)

- TAC-F13-1: 개발자 모드 코드는 릴리스 빌드에도 포함된다(Q40). Development Build와 릴리스 빌드의 차이는 Unity 빌드 옵션뿐이다. 시스템 ms·GC 항목은 Development Build에서만 값이 나오고, 릴리스 빌드에서는 "N/A"로 표시된다 [실측 U14·U15].
- TAC-F13-2: `StreamingAssets/Data/*.json` 7개가 빌드 폴더에 원본 그대로 복사된다 [실측 U13]. 에셋 키 레지스트리는 `Assets/Resources/AssetKeyRegistry.asset`(빌드에 포함)이다.

### F16. 타깃 탐색 전략

- TAC-F16-1: 세 구현 모두 `BuildIndex` 단계에서 받은 `MonsterSnapshotBuffer`의 위치로 거리를 계산하고, `TargetOrdering.IsBetter`(5a R6)로 고른다. 그래서 반환이 비트 단위로 같다 (EM: 무작위 배치 10,000건 + 동거리 배치 + 보스 포함 배치에서 A=B 비교 / PM: A=B=C).
- TAC-F16-2: 공간 해시 셀 키 = `SpatialHash.CellOf(pos, cellSize)`(5a R14). 고리(ring) 탐색 종료 조건은 "고리 k를 끝냈고 `bestD2 ≤ (k·cellSize)²`" 또는 "점유 셀 범위를 벗어남"이다. 그 뒤 보스 리스트를 전수 검사한다 (EM).
- TAC-F16-3: `Physics2D` 공급자는 `OverlapCircle`(레이어 = Monster)을 반경 4에서 시작해 두 배씩 넓히며 후보를 찾는다. 찾으면 `R = √bestD2 + maxMonsterStepDisplacement + 1e-3`으로 한 번 더 질의하고, 최종 선택은 스냅샷 위치로 한다. 물리 위치가 최대 1스텝 늦어도 결과가 같게 하려는 여유다 [실측 U11].
- TAC-F16-4: 공간 해시 셀 크기는 `SpatialHashSettings.CellSize`(기본 [기본값] 2.0) 코드 상수이며, 개발자 모드에서 바꿀 수 있고 CSV에 기록된다.
- TAC-F16-5: 타깃 공급자는 판 진행 중에 바꿀 수 있다(상태가 스냅샷뿐이라 다음 스텝부터 적용). 나머지 3개 전략은 다음 판 시작 시 적용한다(6.1 D10).

### F17. Update 집중화

- TAC-F17-1: `CentralTickStrategy`는 그룹별 `List<IUpdatable>`을 등록 순서로 순회한다. 순회 중 해제되면 슬롯을 null로 표시하고 순회가 끝난 뒤 압축한다. 순회 중 등록은 대기 목록에 넣었다가 순회가 끝난 뒤 붙인다 (EM).
- TAC-F17-2: `SelfTickStrategy.Register`는 호스트의 `MonsterSelfTicker`/`ProjectileSelfTicker`를 `enabled = true`로, `Unregister`는 `false`로 바꾼다. 셀프 티커는 `SimulationClock.StepActive`가 거짓이면 아무것도 하지 않는다 (PM).
- TAC-F17-3: 플레이어·무기·궤도 원·장판·젬·UI·매니저는 `ITickStrategy`에 등록하지 않는다. 각자 해당 단계 핸들러가 직접 갱신한다.

### F18. 충돌 판정

- TAC-F18-1: 채널 9개(5a R5)만 판정한다. 엔진판은 레이어 매트릭스로, 자체판은 채널 표로 같은 쌍만 본다 [실측 U12].
- TAC-F18-2: 자체판은 채널마다 이번 스텝 겹침 쌍 집합을 계산해 이전 스텝 집합과 비교하고, 새로 겹친 쌍은 Enter, 사라진 쌍은 Exit으로 낸다. 겹침 판정은 `CircleMath.Overlaps`(`≤`)다 (EM).
- TAC-F18-3: 자체판의 후보 추림: `PlayerProjectile`·`PlayerOrb`·`PlayerZone` → Monster는 `MonsterSpatialIndex.QueryCircle` + 보스 리스트, `PlayerMagnet`·`PlayerBody` → Gem·Chest는 자체 소유 `SpatialHash` 인스턴스(픽업 전용), `EnemyProjectile`·`EnemyZone` → PlayerBody와 `PlayerBody` → Monster는 플레이어 1개 기준 선형 또는 해시 질의다 (6.1 D15).
- TAC-F18-4: 엔진판은 `Collision` 단계에서 모든 등록 바디에 `Rigidbody2D.MovePosition(body.Position)`을 하고, 반지름이 바뀌었으면 `CircleCollider2D.radius`를 갱신한 뒤 `Physics2D.Simulate(StepMath.StepSeconds)`를 호출한다. 콜백 큐를 비워 ID로 바꾸고 정렬해 반환한다 [실측 U5·U7·U10].
- TAC-F18-5: 등록 해제(회수) 시 두 전략 모두 Exit을 내지 않는다. 자체판은 그 바디의 쌍을 조용히 지우고, 엔진판은 `Physics2D.callbacksOnDisable = false`에 더해 "등록되지 않은 바디의 콜백은 버림" 필터를 둔다 [실측 U8].
- TAC-F18-6: 같은 스텝에 생성된 객체는 다음 스텝의 `Collision`부터 판정에 들어간다(생성은 `Collision` 뒤의 `Lifecycle`에서 일어나므로 두 전략 모두 자연히 그렇게 된다).
- TAC-F18-7: E1·E2·E3의 실행 방법은 5c.5에 있다.

### F19. 개발자 모드

- TAC-F19-1: `GameBootstrap.Awake`가 `DevModeActivation.IsRequested(Environment.GetCommandLineArgs())`를 정확히 1회 호출한다. 거짓이면 `Survivor.Dev`의 어떤 `MonoBehaviour`도 만들지 않는다. 그 밖에 게임 코드 어디에도 개발자 모드 분기가 없다 [실측 U16].
- TAC-F19-2: 개발자 모드는 값과 객체를 주입하는 방식으로만 개입한다: `SimulationRules` 필드, `IInputSource` 교체(`AutoPathInputSource`), `ISpawnPolicy` 교체(`DevSpawnPolicy`), `ICollisionStrategy` 데코레이터(`LoggingCollisionStrategy`), `StrategySelection`, `PendingCommandQueue` 명령, `Time.timeScale`.
- TAC-F19-3: 자동 종료는 `SimulationRules.StopAtStep`(기본 `long.MaxValue`)으로 한다. 시계는 `Step == StopAtStep`이 되면 게이트를 닫는다. 개발자 모드의 `Update`가 이를 감지해 결과를 출력하고 `Application.Quit()`(에디터에서는 플레이 중지)를 호출한다 (PM: 정확히 N스텝).
- TAC-F19-4: 시각화는 개발자 전용 `DevVisualizer`의 `OnRenderObject`(GL 선)에서 그린다. 꺼져 있으면 컴포넌트 `enabled = false`라 호출 비용이 없다.
- TAC-F19-5: 측정 시작(`MeasurementSession.Begin`)은 시각화가 하나라도 켜져 있거나 `Time.timeScale != 1`이면 거부하고 화면에 이유를 표시한다(F10 오염 방지).

---

## 5. 신규 구현 필요 함수 명세 (New Function/Interface Specs)

모든 코드는 신규다. 아래 시그니처는 병렬 구현자 사이의 계약이다. 5c와 어긋나면 5c가 우선한다. 공개 멤버에는 영어 한 줄 요약 주석을 단다(CODING_GUIDE §3). 사용자에게 보이는 문자열은 한국어다.

### 5.0 네임스페이스와 인터페이스 목록

| 네임스페이스(=asmdef) | 내용 |
|---|---|
| `Survivor.Core` | 공유 규칙 함수, `EntityId`, RNG, 계약 인터페이스, 이벤트·명령 구조체, `SimulationRules`, `GameRules` |
| `Survivor.Data` | 정의 객체, 로더, 검증, 에셋 키 레지스트리 |
| `Survivor.Simulation` | 루프 드라이버, 시계, 엔티티 생명주기, 플레이어·무기·몬스터·드랍·진행 |
| `Survivor.Strategies` | 풀 3, 타깃 3, 틱 2, 충돌 2 구현과 공간 해시 |
| `Survivor.UI` | UI Toolkit 뷰 |
| `Survivor.App` | 조립 루트(`GameBootstrap`), 흐름 제어 |
| `Survivor.Dev` | 개발자 모드 |

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

- 플레이어는 판마다 가장 먼저 할당되므로 항상 `1:1`이다. 플레이어 자석(PlayerMagnet) 바디는 별도 ID(`2:1`)를 받는다(6.1 D8).
- 세대는 **할당 해제(디스폰·사망·수명 만료)** 때 올라간다. RUN_LOG의 "풀 반납 때마다 증가"와 같은 시점이지만, 세대를 소유하는 쪽은 풀이 아니라 할당기다(2.2 b).

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

### 5.3 고정 스텝 시계·게이트 (Simulation, IMP-LOOP)

```csharp
namespace Survivor.Simulation
{
    /// Owns the step counter and the run gate; the only source of game time.
    public sealed class SimulationClock
    {
        public long Step { get; }               // completed steps in the current run
        public bool IsRunning { get; }          // gate for the next FixedUpdate
        public bool StepActive { get; }         // true between Begin and End of an executing step
        public int StepsThisFrame { get; }      // reset by SimulationFrameCounter.Update (order -10000)
        public double GameSeconds { get; }      // StepMath.StepsToSeconds(Step)
        public void SetRunning(bool running);   // flow: Playing=true, Paused/LevelUp/Result=false
        public void RequestSingleStep();        // dev step-through: runs exactly one step then closes gate
        public void JumpTo(long step);          // dev: only while !StepActive
        public void Reset();
        internal bool TryBeginStep(long stopAtStep); // called by SimulationDriverEarly
        internal void EndStep();                     // called by SimulationDriverLate (Step++)
    }

    public enum SimulationPhase
    { Begin = 0, PlayerMove = 1, MonsterTick = 2, BuildIndex = 3, WeaponTick = 4,
      ProjectileTick = 5, Collision = 6, Resolve = 7, Lifecycle = 8, End = 9 }

    public interface IStepPhaseHandler { void OnPhase(SimulationPhase phase, in StepContext context); }

    /// Ordered registry of phase handlers; same order for every strategy combination.
    public sealed class SimulationPipeline
    {
        public void AddHandler(SimulationPhase phase, int order, IStepPhaseHandler handler); // order ties -> throw
        public void RunPhase(SimulationPhase phase, in StepContext context);
    }

    // Drivers (MonoBehaviour, one instance each, created by GameBootstrap):
    // [DefaultExecutionOrder(-1000)] SimulationDriverEarly : Begin, PlayerMove, (central) MonsterTick, opens Tick.Monsters marker
    // [DefaultExecutionOrder(-500)]  MonsterSelfTicker      (per monster, enabled only in per-object mode)
    // [DefaultExecutionOrder(0)]     SimulationDriverMid   : closes Tick.Monsters, BuildIndex, WeaponTick, (central) ProjectileTick, opens Tick.Projectiles
    // [DefaultExecutionOrder(500)]   ProjectileSelfTicker  (per projectile, enabled only in per-object mode)
    // [DefaultExecutionOrder(1000)]  SimulationDriverLate  : closes Tick.Projectiles, Collision, Resolve, Lifecycle, End
}
```

- 중앙 틱 모드에서는 `MonsterTick`을 `Early`에서, `ProjectileTick`을 `Mid`에서 `ITickStrategy.RunGroup`으로 실행한다. 객체별 모드에서는 `RunGroup`이 아무것도 하지 않고, 같은 위치에 Unity가 셀프 티커를 끼워 넣는다 [실측 U1].
- `TryBeginStep`은 `IsRunning && Step < stopAtStep`이면 참을 반환하고 `StepActive = true`, `StepsThisFrame++`을 한다. 이 함수가 거짓이면 같은 `FixedUpdate`의 `Mid`·`Late`·셀프 티커는 `StepActive == false`를 보고 아무것도 하지 않는다.
- 설정 적용: `SimulationSettingsApplier.Apply()`(IMP-LOOP)가 부트스트랩에서 `Time.fixedDeltaTime = StepMath.StepSeconds`, `Time.maximumDeltaTime = StepMath.MaxCatchUpSteps * StepMath.StepSeconds`(= 0.05), `Physics2D.simulationMode = SimulationMode2D.Script`, `Physics2D.callbacksOnDisable = false`, 레이어 충돌 매트릭스를 설정한다 [실측 U2·U7·U8·U12].

```csharp
namespace Survivor.Core
{
    /// Read-only per-step view handed to every phase handler and IUpdatable.
    public readonly struct StepContext
    {
        public readonly long Step;
        public readonly Vector2 PlayerPosition;     // after PlayerMove of this step
        public readonly SimulationRules Rules;
        public readonly ITargetProvider Targets;    // valid from BuildIndex onward
        public readonly ISimulationCommands Commands;
        public StepContext(long step, Vector2 playerPosition, SimulationRules rules,
                           ITargetProvider targets, ISimulationCommands commands);
    }
}
```

### 5.4 지연 명령 (Core 계약, Simulation 구현)

```csharp
namespace Survivor.Core
{
    public enum SimulationCommandKind : byte
    { Despawn = 0, SpawnEnemyProjectile = 1, SpawnEnemyZone = 2, Summon = 3, SpawnPlayerProjectile = 4, SpawnPlayerZone = 5 }

    public enum DespawnReason : byte { Killed = 0, OutOfRange = 1, LifetimeExpired = 2, Consumed = 3, Replaced = 4, RunReset = 5 }

    public readonly struct SimulationCommand
    {
        public readonly SimulationCommandKind Kind;
        public readonly EntityId Source;       // issuing entity (sort key 1)
        public readonly int Sequence;          // per-source order within the step (sort key 2), assigned by the buffer
        public readonly EntityId Target;       // Despawn target
        public readonly DespawnReason Reason;
        public readonly Vector2 Position;
        public readonly Vector2 Direction;
        public readonly string DefinitionId;   // monster id for Summon, weapon id for player spawns
        public readonly int Count;
        public readonly int LevelIndex;        // weapon level (0-based) for player spawns
    }

    /// Deferred, order-independent side effects. Applied in Lifecycle sorted by (Source, Sequence).
    public interface ISimulationCommands { void Enqueue(in SimulationCommand command); }
}
```

- `SimulationCommandBuffer`(IMP-LOOP)는 `Lifecycle` 단계 맨 앞에서 정렬하고 종류별 `ISimulationCommandHandler`(몬스터·무기 구현자가 등록)에게 넘긴다. 같은 대상의 `Despawn`이 두 번 들어오면 두 번째는 무시한다(사망과 수명 만료가 겹치는 경우).
- 무기 발사(W1·W4 투사체, W3 장판)는 `WeaponTick`에서 일어난다. 이 시점은 단계 핸들러 안이고 순서가 결정적이므로 즉시 생성해도 된다. 다만 같은 스텝의 `ProjectileTick`에서 새 투사체가 움직이느냐 마느냐가 F17 A/B에서 달라지지 않도록 **즉시 생성하되 "생성 스텝에는 Tick 건너뜀"** 규칙(`_spawnStep == ctx.Step`이면 `Tick` 반환)을 두 모드 공통으로 적용한다 (EM/PM).

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

- `KeyboardInputSource : MonoBehaviour, IInputSource`(Simulation): `Update`에서 W/A/S/D·방향키의 눌림 상태를 래치하고, `ReadMove`는 래치 값을 `MoveMath.ToUnitDirection`으로 바꿔 반환한다. ESC·1/2/3·F1은 이 클래스가 다루지 않는다(UI가 `Update`에서 처리한다, TAC-F5-5) [실측 U17].
- `AutoPathInputSource : IInputSource`(Dev): 생성자 `(Vector2 center, float radius, float speedUnitsPerSecond, float startAngleRad)`. 스텝 s의 목표점 = `center + radius·(cos φ, sin φ)`, `φ = start + (speed/radius)·(s+1)/60`. 반환 방향 = `(목표점 − 현재 위치)` 정규화, `SpeedOverride` = min(speed, 거리 × 60). 사람 입력이 없으므로 결정적이다.

### 5.6 풀 전략 (Core 계약, IMP-POOL 구현)

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
}
```

- 템플릿은 `PrefabCatalog`(Simulation, IMP-LOOP)가 부트스트랩에서 코드로 만든 비활성 `GameObject`다(카테고리별: Monster, PlayerProjectile, EnemyProjectile, PlayerOrb, PlayerZone, EnemyZone, Gem, Chest). 각 템플릿에는 `SpriteRenderer`, `PooledObject`(템플릿 참조와 활성 플래그), 비활성 `CircleCollider2D`(isTrigger) + `Rigidbody2D`(Kinematic, `simulated = false`), `CollisionBodyLink`가 붙어 있다. 레지스트리 항목에 프리팹 덮어쓰기가 있으면 그 프리팹이 템플릿이 된다(F11).
- `PoolPrewarmCounts`(Simulation 상수) [기본값 Q13]: Monster 450, PlayerProjectile 300, EnemyProjectile 100, PlayerOrb 16, PlayerZone 16, EnemyZone 8, Gem 600, Chest 16. 측정 후 조정한다.

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
        public bool IsLarge;          // true for behavior "boss": kept out of the spatial hash
    }

    /// Frozen monster positions for this step, filled in BuildIndex in ascending Id order.
    public sealed class MonsterSnapshotBuffer
    {
        public int Count { get; }
        public ref readonly MonsterSnapshot this[int index] { get; }
        public float MaxHashedRadius { get; }          // max Radius over !IsLarge entries
        public float MaxStepDisplacement { get; }      // max monster speed per step (from definitions, incl. charge)
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

    /// Nearest-monster query; all implementations must return identical results (TargetOrdering).
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
    public sealed class MonsterSpatialIndex
    {
        public MonsterSpatialIndex(float cellSize);
        public bool Required { get; set; }                          // set by composition root
        public float CellSize { get; }
        public void Rebuild(MonsterSnapshotBuffer snapshot);        // no-op when !Required
        public void QueryCircle(Vector2 center, float radius, List<int> snapshotIndicesOut); // includes all IsLarge entries
        public void ForEachRing(Vector2 point, int ring, List<int> snapshotIndicesOut);       // for nearest search
        public IReadOnlyDictionary<long, int> CellCounts { get; }   // dev visualization
    }

    public sealed class SpatialHash   // generic center-point hash, also used for gems/chests
    {
        public SpatialHash(float cellSize);
        public void Clear();
        public void Insert(int payload, Vector2 position);
        public void QueryCells(Vector2 center, float radius, List<int> payloadsOut);
    }
}
```

- 구현 클래스: `BruteForceTargetProvider`, `SpatialHashTargetProvider(MonsterSpatialIndex)`, `Physics2DTargetProvider(int monsterLayerMask)`.
- 보스는 `IsLarge`라 해시에 들어가지 않고, `QueryCircle`·고리 탐색 뒤에 항상 별도로 전수 검사한다(SPEC F16).
- 몬스터가 플레이어를 찾는 질의는 없다. 플레이어가 1명뿐이라 `StepContext.PlayerPosition`을 직접 쓴다(6.1 D20).

### 5.8 중앙 틱 (Core 계약, IMP-LOOP 구현)

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
        void Register(IUpdatable updatable, GameObject host);   // per-object: enables host's self ticker
        void Unregister(IUpdatable updatable);                  // safe during RunGroup
        void RunGroup(TickGroup group, in StepContext context); // per-object: no-op
        int RegisteredCount(TickGroup group);
        void Clear();
    }
}
```

- 셀프 티커(`MonsterSelfTicker`, `ProjectileSelfTicker`)는 `FixedUpdate`에서 `if (!clock.StepActive) return; _owner.Tick(SimulationContextHub.Current);`만 한다. `SimulationContextHub.Current`는 `Early` 드라이버가 스텝마다 채우는 정적 `StepContext`다(IMP-LOOP).

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
        public int CompareTo(CollisionEvent other);   // Step, Channel, A, B, Kind
        public override string ToString();            // CSV row: "step,kind,channel,a,b"
    }

    /// Mutable circle owned by an entity; strategies read Position/Radius during CollectEvents.
    public sealed class CollisionBody
    {
        public EntityId Id;
        public CollisionLayer Layer;
        public Vector2 Position;
        public float Radius;
        public bool IsLarge;          // boss
        public GameObject Host;       // engine strategy enables CircleCollider2D/Rigidbody2D on it
        public int StrategySlot = -1; // private to the active strategy
        public bool DrivesTransform;  // set by engine strategy: presentation must not write transform
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
}
```

- 구현: `SelfCollisionStrategy(MonsterSpatialIndex index, MonsterSnapshotBuffer snapshot)`, `EngineCollisionStrategy(CollisionDetectionMode2D mode)`. 엔진판은 `CollisionBodyLink : MonoBehaviour`(Strategies)의 `OnTriggerEnter2D/Exit2D`로 큐를 채운다. 링크는 자기 `CollisionBody`를 알고, 콜백의 상대 콜라이더에서 상대 링크를 찾아 레이어 쌍으로 채널을 정하고 A/B를 정렬한다.
- 엔진판 바디 구성: `Rigidbody2D.bodyType = Kinematic`, `useFullKinematicContacts = true`, `collisionDetectionMode = mode`(E1은 Discrete, E2는 Discrete와 Continuous), `CircleCollider2D.isTrigger = true` [실측 U5·U6].
- 이벤트를 소비하는 쪽은 `CollisionEventRouter`(IMP-LOOP) 하나다. 채널별로 `ICollisionEventHandler`를 하나씩 등록받는다: 0~2는 무기·투사체(IMP-PLAYER), 3~5는 `PlayerDamageReceiver`(IMP-PLAYER), 6~8은 `PickupSystem`(IMP-MONSTER). 피해 적용 로직이 전략별로 중복되지 않는다(SPEC F18).

```csharp
namespace Survivor.Simulation
{
    public interface ICollisionEventHandler { void Handle(in CollisionEvent e, in StepContext context); }
    public sealed class CollisionEventRouter : IStepPhaseHandler   // phase Collision collects, Resolve dispatches in sorted order
    {
        public CollisionEventRouter(ICollisionStrategy strategy);
        public void SetHandler(CollisionChannel channel, ICollisionEventHandler handler); // one per channel, else throw
        public void ReplaceStrategy(ICollisionStrategy strategy);                         // run boundary only
    }
}
```

### 5.10 엔티티 생명주기 (Simulation, IMP-LOOP) — 생성·회수의 유일한 경로

```csharp
namespace Survivor.Simulation
{
    public enum EntityCategory { Monster, PlayerProjectile, EnemyProjectile, PlayerOrb, PlayerZone, EnemyZone, Gem, Chest }

    /// Base component for pooled simulation entities (no FixedUpdate here).
    public abstract class SimEntity : MonoBehaviour
    {
        public EntityId Id { get; }
        public EntityCategory Category { get; }
        public CollisionBody Body { get; }
        public Vector2 Position { get; protected set; }  // authoritative; mirrors into Body.Position
        public long SpawnStep { get; }
        internal void Bind(EntityId id, long spawnStep);
        protected abstract void OnReleased();             // clear references; called before pool release
    }

    /// Single owner of acquire/id/init/register and the exact reverse on despawn.
    public sealed class EntityLifecycle
    {
        public EntityLifecycle(IPoolStrategy pool, EntityIdAllocator ids, ITickStrategy ticks,
                               ICollisionStrategy collisions, PrefabCatalog prefabs, SimulationClock clock);
        public T Spawn<T>(EntityCategory category, Vector2 position, Action<T> initialize) where T : SimEntity;
        //   order: pool.Acquire -> ids.Allocate -> Bind -> initialize(entity) -> ticks.Register (if IUpdatable)
        //          -> collisions.Register(Body) -> registry add (MonsterRegistry / PickupRegistry ...)
        public bool Despawn(EntityId id, DespawnReason reason);
        //   returns false if !ids.IsLive(id) (already gone; no exception)
        //   order: registry remove -> collisions.Unregister -> ticks.Unregister -> OnReleased -> ids.Free -> pool.Release
        public bool TryGet(EntityId id, out SimEntity entity);
        public void DespawnAll();   // run reset, ascending Id order
        public event Action<SimEntity, DespawnReason> Despawned;   // KillPipeline etc. subscribe (no dev subscribers)
    }
}
```

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

    /// Holds the current GameData; hot reload swaps it atomically at a step boundary.
    public sealed class GameDataRegistry
    {
        public GameData Current { get; }
        public DataValidationReport LastReport { get; }
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
    { public readonly string Source; public readonly CollisionLayerTag Layer; public readonly float SpeedPerSecond; public readonly float Radius; }

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

- `CollisionLayerTag`는 Data 어셈블리가 Core의 `CollisionLayer`를 그대로 쓰는 별칭이다(Data → Core 참조. 별도 enum을 만들지 않는다).
- 파서: [기본값] Newtonsoft `JToken.Parse`(`JsonLoadSettings { LineInfoHandling = Load, DuplicatePropertyNameHandling = Error }`)로 트리만 만들고, 바인딩은 리플렉션 없이 손으로 쓴다(필수·알 수 없는 키·타입을 직접 판정하기 위해서이며, `JsonUtility`로는 누락 필드와 알 수 없는 키를 판정할 수 없다). 패키지 추가는 승인 대상이라 6.1 D17에서 PM이 결정한다. 대안은 자체 최소 파서다.
- 파일 단위 정책: 필수 4종(weapons, monsters, spawnTable, playerStats)이 없거나 전체 파싱에 실패하면 Blocking이다. 선택 3종은 Warning이고 폴백한다. 정의 단위 오류는 그 정의만 건너뛰고 Warning이다. 플레이어 기본 스탯은 정의 단위 오류도 Blocking이다(SPEC F11).
- 플레이 불가 4기준: 사용 가능한 무기 0개, 스폰 가능한 몬스터 0종(스폰 테이블이 참조하는 유효 몬스터 기준), 플레이어 기본 스탯 없음, 스폰 테이블 없음. `startingWeaponId`가 유효하지 않으면 플레이어 기본 스탯 오류(Blocking)로 본다 [기본값, D7].
- 에셋 키 해석은 로드 시점에 키를 검증(경고)할 때와 생성 시점에 `SpriteRenderer`에 적용할 때 쓰인다. 기본 도형은 `DefaultShapeFactory.Circle`(런타임에 64×64 `Texture2D`로 만든 원, 마젠타 틴트)이다 [실측 U20].

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
        public void Forget(EntityId target);               // called when target despawned
        public void SetInterval(int hitIntervalSteps);     // level-up / P1 change
        public void Clear();
    }

    /// Victim-side shared invulnerability for the player (body contact, M3 projectiles, M4 zones).
    public sealed class PlayerDamageReceiver : ICollisionEventHandler, IStepPhaseHandler
    {
        // Enter/Exit for channels 3 and 5 maintain an overlap set; channel 4 Enter is a one-shot hit (projectile consumed).
        // Resolve: if Rules.PlayerTakesDamage && step >= _invulnerableUntilStep && (pending one-shot || overlap set non-empty):
        //   damage = max over candidates, tie -> smaller EntityId; _invulnerableUntilStep = step + invulnerabilitySteps.
        public long InvulnerableUntilStep { get; }
    }
}
```

- 일회성 접촉(W1·W4 투사체 → 몬스터, 젬, 상자): Enter 1회를 처리하고 `Despawn(Consumed)`를 요청한다. 관통(`pierceCount > 0`)이면 투사체가 "이미 맞힌 ID 목록"을 갖고 같은 ID의 재진입을 무시하며, 맞힌 수가 `pierceCount + 1`이 되면 회수한다.
- 한 투사체가 같은 스텝에 몬스터 둘과 Enter하면 정렬 순서상 앞(작은 몬스터 ID)만 맞히고, 나머지 이벤트는 투사체가 이미 회수 요청된 상태라 무시한다(관통 없음 = 정확히 1회 피해, SPEC F18).
- 무적 중에 M3 투사체가 Enter하면 피해 없이 회수한다 [기본값, D12].

### 5.13 JSON 스키마 7종

공통 규칙:
- 인코딩은 UTF-8, 최상위는 객체, 첫 필드는 `"schemaVersion": 1`(필수, 정수)이다.
- 단위: **시간은 초(`...Seconds`)로 적고 로드 시 스텝으로 환산**(`SecondsToIntervalSteps`, 5a R1)한다. **속도는 초당 유닛(`...Speed`)으로 적고 로드 시 /60**(5a R3)한다. 각속도는 **초당 도(`angularSpeedDegPerSec`)로 적고 로드 시 스텝당 라디안**으로 바꾼다. 거리·반지름은 월드 유닛이다(5a R4).
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
| `behavior` | string enum | - | 필수 | `projectile` / `homing` / `orbit` / `zone` |
| `assetKey` | string | - | 필수 | 투사체·궤도 원·장판의 시각 키 |
| `iconKey` | string | - | 선택 | 선택지 UI 아이콘 키(없으면 `assetKey`) |
| `evolvedOnly` | bool | - | 선택(기본 false) | true면 진화로만 얻는다. 선택지·상자 강화 제외, `levels` 길이는 정확히 1 |
| `levels` | WeaponLevel[] | - | 필수 | 기본 무기는 정확히 5개, 진화 무기는 정확히 1개. i번째 = (i+1)단계의 **절대값** |

WeaponLevel (행동 타입별 필수 필드)

| 필드 | 타입 | 단위 → 로드 후 | projectile | homing | orbit | zone |
|---|---|---|---|---|---|---|
| `damage` | number > 0 | 피해량 | 필수 | 필수 | 필수 | 필수 |
| `cooldownSeconds` | number > 0 | 초 → 스텝 | 필수 | 필수 | - | 필수(생성 주기) |
| `projectileCount` | int ≥ 1 | 개 | 선택(1) | 선택(1) | - | - |
| `spreadDegrees` | number ≥ 0 | 도 | 선택(0) | 선택(0) | - | - |
| `projectileSpeed` | number > 0 | 유닛/초 → 유닛/스텝 | 필수 | 필수 | - | - |
| `projectileRadius` | number > 0 | 유닛 | 필수 | 필수 | - | - |
| `lifetimeSeconds` | number > 0 | 초 → 스텝 | 필수 | 필수 | - | - |
| `pierceCount` | int ≥ 0 | 회 | 선택(0) | 선택(0) | - | - |
| `orbCount` | int ≥ 1 | 개 | - | - | 필수 | - |
| `orbRadius` | number > 0 | 유닛 | - | - | 필수 | - |
| `orbitRadius` | number > 0 | 유닛 | - | - | 필수 | - |
| `angularSpeedDegPerSec` | number | 도/초 → 라디안/스텝 | - | - | 필수 | - |
| `hitIntervalSeconds` | number > 0 | 초 → 스텝 (W2·W3 틱 간격) | - | - | 필수 | 필수 |
| `zoneRadius` | number > 0 | 유닛 | - | - | - | 필수 |
| `durationSeconds` | number > 0 | 초 → 스텝 | - | - | - | 필수 |
| `placement` | string enum | `nearestEnemy` / `player` | - | - | - | 선택(`nearestEnemy`) [기본값 Q8] |

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
      "id": "E1", "displayName": "연사 투사체", "behavior": "projectile", "assetKey": "shape.projectile.gold", "evolvedOnly": true,
      "levels": [
        { "damage": 22, "cooldownSeconds": 0.2, "projectileSpeed": 14, "projectileRadius": 0.25, "lifetimeSeconds": 2.0, "projectileCount": 3, "spreadDegrees": 12 }
      ]
    }
  ]
}
```
(W2 예시는 지면상 1단계만 보였다. 실제 파일에서는 기본 무기 `levels`가 반드시 5개이고, 아니면 그 정의를 건너뛴다. 수치는 Q1 밸런싱 대상이다. 불변식 확인: W1 12/60 = 0.2 ≤ 0.25 통과, W2 선속도 = π·2.0 ≈ 6.28/초 → 0.105/스텝 ≤ 0.4 통과.)

#### 5.13.2 몬스터 (`monsters.json`)

최상위: `{ "schemaVersion": 1, "monsters": [MonsterDef, ...] }`

| 필드 | 타입 | 단위 | 필수 | 해당 행동 |
|---|---|---|---|---|
| `id`, `displayName`, `assetKey` | string | - | 필수 | 전부 |
| `behavior` | enum | `chaser`/`charger`/`ranged`/`boss` | 필수 | 전부 |
| `maxHealth` | number > 0 | HP | 필수 | 전부 |
| `moveSpeed` | number ≥ 0 | 유닛/초 → /스텝 | 필수 | 전부 |
| `radius` | number > 0 | 유닛 | 필수 | 전부 |
| `contactDamage` | number ≥ 0 | 피해량 | 필수 | 전부 |
| `charge` | object | - | charger만 필수 | `{ triggerDistance(유닛), chargeSpeed(유닛/초), durationSeconds, cooldownSeconds }` |
| `ranged` | object | - | ranged만 필수 | `{ attackRange(유닛), fireCooldownSeconds, projectile: { assetKey, speed(유닛/초), radius, damage, lifetimeSeconds } }` |
| `bossPatterns` | object[] | - | boss만 필수(1개 이상) | 아래 |

보스 패턴 (`bossPatterns[]`, `type`으로 구분)

| type | 필드 | 의미 |
|---|---|---|
| `hazardZone` (패턴 ①) | `cooldownSeconds`, `zoneRadius`, `durationSeconds`, `damage`, `assetKey`, `placement`(`player` 기본) | 쿨다운마다 플레이어 위치에 적 장판(EnemyZone) 생성. 피해는 플레이어 무적 규칙을 따른다 |
| `summon` (패턴 ②) | `cooldownSeconds`, `monsterId`, `count`, `summonRadius` | 쿨다운마다 보스 주변 원주(각도는 Summon 스트림)에 `monsterId`를 `count`기 소환. `monsterId`는 boss가 아닌 몬스터여야 한다(참조 오류 검사) |

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
| `despawnDistance` | number > spawnRadius(20) | 유닛 | 필수 | 기본 45. 개발자 모드가 덮어쓸 수 있다 |
| `segments` | Segment[] | - | 필수(1개 이상) | 시간 오름차순, 겹치지 않음 |
| `segments[].startSeconds` | number ≥ 0 | 초 → 스텝 | 필수 | 포함 |
| `segments[].endSeconds` | number | 초 → 스텝 | 필수 | 미포함. 600을 넘으면 600으로 잘라내고 Warning |
| `segments[].spawnsPerSecond` | number > 0 | 기/초 | 필수 | |
| `segments[].entries` | `{ monsterId, weight }`[] | weight > 0 | 필수 | boss 행동 몬스터는 금지(참조 오류) |
| `bossMonsterId` | string | - | 필수 | boss 행동 몬스터. 무효면 Warning [기본값, D6] |

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

파일이 없거나 깨지면 `UniformFallback`(`defaultWeight = 1`, `healBaseWeight = 1`, 규칙 없음)이 된다. 회복량 30%는 규칙이라 `GameRules.HealRatio`다.
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
| `invulnerabilitySeconds` | number ≥ 0 | 초 → 스텝 (0이면 0스텝 허용) | 필수 |
| `startingWeaponId` | string | 무기 ID (`evolvedOnly`는 금지) | 필수 |
| `xpCurve` | `{ "thresholds": int[], "growthAfterTable": int }` | 레벨 L→L+1에 필요한 경험치 = `thresholds[L-1]`, 표를 넘어가면 마지막 값 + `growthAfterTable × 넘어간 레벨 수` | 필수 |
| `pickups` | `{ gemAssetKey, gemRadius, gemXpValue(int), gemAttractSpeed(유닛/초), chestAssetKey, chestRadius }` | - | 필수(전부) |

어떤 오류든 Blocking이다. 무기·패시브 슬롯 수(4/3), 상자 확률 2%, 강화 70%, 회복 30%는 규칙이라 `GameRules`다.
```json
{ "schemaVersion": 1, "maxHealth": 100, "moveSpeed": 5, "radius": 0.5, "pickupRadius": 2.0,
  "invulnerabilitySeconds": 0.5, "startingWeaponId": "W1",
  "xpCurve": { "thresholds": [5, 8, 12, 16, 20, 25, 30], "growthAfterTable": 5 },
  "pickups": { "gemAssetKey": "shape.gem.blue", "gemRadius": 0.2, "gemXpValue": 1, "gemAttractSpeed": 10,
               "chestAssetKey": "shape.chest.yellow", "chestRadius": 0.5 } }
```
(불변식 확인: 플레이어 5/60 ≈ 0.083 ≤ 0.5, 젬 끌림 10/60 ≈ 0.167 ≤ 0.2 통과. 플레이어와 젬은 SPEC의 검사 목록에 없지만 포함한다, 6.1 D1.)

### 5.14 레벨업·진화·상자 (Simulation)

```csharp
namespace Survivor.Simulation
{
    public enum UpgradeKind { WeaponUpgrade = 0, WeaponNew = 1, PassiveUpgrade = 2, PassiveNew = 3, Heal = 4 }
    public readonly struct UpgradeOption { public readonly UpgradeKind Kind; public readonly string TargetId; public readonly bool IsFiller; }

    public static class LevelUpCandidateBuilder   // IMP-FLOW
    {
        /// Returns candidates in canonical order (R12) with weights; heal included only when weight > 0.
        public static void Build(GameData data, WeaponInventory weapons, PassiveInventory passives,
                                 float health, float maxHealth, List<(UpgradeOption option, float weight)> output);
    }
    public static class LevelUpSelector           // IMP-FLOW
    {
        /// Draws exactly 3 options: weighted without replacement, then heal fillers.
        public static void Draw(List<(UpgradeOption option, float weight)> candidates, DeterministicRng rng, List<UpgradeOption> output3);
    }
    public sealed class EvolutionService          // IMP-PLAYER
    {
        public bool TryFindFirstEligible(out int slotIndex, out CombinationRule rule); // weapon slot order (Q42 default)
        public void Evolve(int slotIndex, CombinationRule rule, in StepContext context);
    }
    public sealed class ChestEffectResolver       // IMP-PLAYER
    {
        public ChestEffect Resolve(DeterministicRng chestRng, in StepContext context); // priority ① evolve ② 70/30
    }
    public enum ChestEffect { Evolved, WeaponUpgraded, Healed }
}
```

### 5.15 개발자 모드 주입 경계와 내보내기 (Dev, IMP-DEV)

게임 쪽에 미리 있어야 하는 주입 지점(이 지점들은 개발자 모드가 꺼져 있어도 존재하는 "값"이다):

```csharp
namespace Survivor.Core
{
    /// Runtime-tunable values read by gameplay; defaults equal normal play. Dev mode only mutates these.
    public sealed class SimulationRules
    {
        public int MonsterCap = GameRules.MonsterCapDefault;          // 400; dev may set int.MaxValue
        public float DespawnDistance;                                 // from spawnTable.json
        public bool PlayerTakesDamage = true;
        public bool WeaponsEnabled = true;
        public bool LevelUpEnabled = true;
        public long StopAtStep = long.MaxValue;
    }

    public interface ISpawnPolicy   // normal: TableSpawnPolicy (IMP-MONSTER); dev: DevSpawnPolicy
    {
        /// Fills spawn requests for this step; Spawner applies cap and placement.
        void CollectSpawns(in StepContext context, int liveMonsterCount, DeterministicRng spawnRng, List<string> monsterIdsOut);
        bool BossSpawnDue(long step);
    }

    public readonly struct StrategySelection
    {
        public readonly PoolStrategyKind Pool; public readonly TargetProviderKind Target;
        public readonly TickStrategyKind Tick; public readonly CollisionStrategyKind Collision;
        public readonly CollisionDetectionMode2D EngineDetectionMode;   // Discrete by default
        public static StrategySelection ShippingDefault { get; }        // CustomPool, SpatialHash, CentralTick, SelfSquaredDistance
        public static StrategySelection Baseline { get; }               // InstantiateDestroy, BruteForce, PerObjectFixedUpdate, EngineCollider2D
    }
}
```

개발자 쪽 구성요소:

| 클래스 | 역할 |
|---|---|
| `DevModeActivation` (static) | `IsRequested(string[] args)` → `args`에 `--dev`가 있는지(대소문자 무시). 에디터에서는 `EditorPrefs` `Survivor.DevModeOnPlay`도 본다(`#if UNITY_EDITOR`, `Assets/Editor/DevModeMenu.cs`) |
| `DevModeRoot : MonoBehaviour` | 활성일 때만 생성. `Update`에서 F1로 오버레이를 토글하고 `StopAtStep` 도달을 감지 |
| `DevOverlay` | IMGUI 패널(한국어 레이블, 같은 한글 글꼴). 전략 토글 4개 + CCD 모드, 시드, 레벨업 비활성, 자동 경로(반지름·속도), 무적, 무기 구성, 공격 on/off, 스폰 제어, D, 상한 해제, 시각화 4종, 배속 0.25~4, 시간 점프, 한 스텝 진행, 체력·경험치·레벨·보스·상자, 핫 리로드, 덤프 |
| `DevController` | 오버레이 조작을 `SimulationRules`, `PendingCommandQueue`, `StrategySelection`(다음 판 적용), `Time.timeScale`로 바꾼다 |
| `DevSpawnPolicy : ISpawnPolicy` | 스폰 중단 / 특정 종만 / N 유지(죽거나 디스폰되면 같은 스텝에 보충) / 즉시 N기 |
| `AutoPathInputSource : IInputSource` | 5.5 |
| `LoggingCollisionStrategy : ICollisionStrategy` | 실제 전략을 감싸 `CollectEvents` 결과를 `CollisionEventLog`에 복사(데코레이터) |
| `CollisionEventLog` | 메모리 버퍼 → `collision_<seed>_<strategy>_<mode>.csv`. 헤더: `step,kind,channel,a,b` (`a`,`b`는 `EntityId.ToString()`) |
| `StatsCollector` | `ProfilerRecorder`: `ProfilerMarkers` 전부(SumAllSamplesInFrame), `"GC Allocated In Frame"`. `Time.unscaledDeltaTime`으로 프레임 시간을 링 버퍼에 쌓는다 [실측 U14·U15] |
| `MeasurementSession` | 시작·종료, 1% low = 가장 느린 1% 프레임 시간 평균의 역수, 최악 프레임 ms |
| `MeasurementCsvExporter` | `Application.persistentDataPath/measurements/summary.csv`(행 추가)와 `frames_<timestamp>.csv`. 저장 경로를 화면에 표시 |
| `DevVisualizer` | GL 선: 해시 셀 + 셀별 수(IMGUI 레이블), 충돌 반경, 무기 → 타깃 선(`WeaponInstance.LastTargetId` 읽기 전용), 스폰·디스폰 원 |
| `HotReloadService` | `GameDataLoader.LoadAll` → `GameDataRegistry.TrySwap`. 다음 `Begin`에서 적용 |
| `DataDumpWriter` | 로드된 정의(초·스텝 값 모두)와 `DataValidationReport`를 `persistentDataPath/dump_<timestamp>.txt`로 |

`summary.csv` 열(순서 고정): `timestampUtc, buildType, cpu, gpu, ramMB, os, seed, pool, target, tick, collision, ccdMode, monsterTarget, despawnDistance, monsterCapDisabled, stepSeconds, durationSteps, frames, avgFps, onePercentLowFps, worstFrameMs, gcAllocPerFrameBytesAvg, monsterCountAvg, monsterCountMax, slowmoFrames, pool_msPerStep, pool_msPerFrame, targeting_msPerStep, targeting_msPerFrame, spatialIndex_msPerStep, spatialIndex_msPerFrame, tickMonsters_msPerStep, tickMonsters_msPerFrame, tickProjectiles_msPerStep, tickProjectiles_msPerFrame, collision_msPerStep, collision_msPerFrame, step_msPerStep, step_msPerFrame`. 값이 없으면 `NA`. `slowmoFrames` = 스텝 3회를 돌린 프레임 수(catch-up 상한에 걸렸을 가능성).

### 5.16 상태 해시 (Simulation, IMP-LOOP)

```csharp
namespace Survivor.Simulation
{
    /// FNV-1a 64 over: step, player pos/hp/xp/level, monsters (ascending Id: id, pos, hp),
    /// projectiles (ascending Id: id, pos), gem count, kill count. Floats hashed by bit pattern.
    public static class SimulationStateHash { public static ulong Compute(RunSession session); }
}
```
결정론 테스트(프레임레이트 3종, 전략 조합)와 개발자 모드 N스텝 결과 출력에 이 해시를 쓴다.

---

## 5a. 공유 규칙 표 (Shared Rules)

각 규칙에는 소유 함수가 하나씩 있고, 다른 계층은 그 함수를 호출만 한다. 다시 구현하면 리뷰에서 반려한다. 소유 파일은 전부 `Assets/Scripts/Core/`(IMP-CORE)이며, R14만 예외로 `Strategies/Spatial`(IMP-SPATIAL)에 있다.

| # | 규칙 | 단위·의미 (정확히) | 소유 함수 | 사용자 |
|---|---|---|---|---|
| R1 | 초 → 스텝 환산 | `steps = (int)Math.Round(seconds × 60, MidpointRounding.AwayFromZero)`. 간격·수명·쿨다운처럼 0보다 커야 하는 값은 최소 1(`SecondsToIntervalSteps`). 무적처럼 0 허용 값은 `SecondsToSteps`. 음수·NaN·∞는 `ArgumentOutOfRangeException` | `StepMath.SecondsToSteps`, `StepMath.SecondsToIntervalSteps` | Data 로더(유일한 호출자. 런타임 코드는 이미 환산된 값만 쓴다) |
| R2 | 게임 시간 | 게임 시간(초) = 완료 스텝 수 / 60. 표시는 `mm:ss`(초는 내림). 분이 99를 넘으면 그대로 증가 | `StepMath.StepsToSeconds`, `StepMath.FormatGameTime(long steps)` | HUD, 결과, 최고 기록, CSV |
| R3 | 속도 단위 | JSON = 유닛/초. 런타임 = 유닛/스텝 = 값 / 60f. 각속도는 도/초 → 라디안/스텝 = deg × π/180 / 60 | `StepMath.PerSecondToPerStep`, `StepMath.DegPerSecondToRadPerStep` | Data 로더, `AutoPathInputSource`, 불변식 검증 |
| R4 | 반지름·거리 | 월드 유닛. 1유닛 = 카메라 orthographic size 9 기준 세로 화면의 1/18. 시각 크기 = 지름 2r에 맞춘 배율 | `CircleMath.VisualScaleFor(Sprite, float radius)` | 모든 엔티티 초기화 |
| R5 | 원-원 겹침 | `(b − a).sqrMagnitude ≤ (ra + rb)²`, 경계 포함, 제곱근 없음. 채널 9개와 A측 레이어는 `CollisionChannelTable` | `CircleMath.Overlaps(Vector2 a, float ra, Vector2 b, float rb)`, `CollisionChannelTable.TryGetChannel(CollisionLayer x, CollisionLayer y, out CollisionChannel ch, out bool xIsA)` | 자체 충돌 전략, 불변식 2단 대상 쌍, 엔진 링크(채널 판정), 레이어 매트릭스 설정 |
| R6 | 최근접 타이브레이크 | 제곱거리(float, `sqrMagnitude`) 작은 쪽 → 같으면 `EntityId` 작은 쪽(Index, 그다음 Generation) | `TargetOrdering.IsBetter(float d2, EntityId id, float bestD2, EntityId bestId)` | 타깃 공급자 3종, W3 위치 선정 |
| R7 | 화면 밖 스폰 위치 | `θ = u × 2π`(u = `NextFloat01()`, float), `pos = center + radius × (Mathf.Cos θ, Mathf.Sin θ)`. 1회 추첨, 재시도 없음 | `SpawnGeometry.PointOnCircle(Vector2 center, float radius, float u01)` | 일반 스폰(r=20), 보스 스폰, 보스 소환(summonRadius) |
| R8 | 회복 가중치 | `r = clamp01(hp / maxHp)`, `w = wBase × (1 − r)²`. `w ≤ 0`이면 후보 제외 | `HealWeight.Compute(float wBase, float hp, float maxHp)` | 레벨업 후보, 개발자 오버레이(곡선 표시) |
| R9 | 가중 추출 | 정규 순서의 후보에 누적 합. `u = NextFloat01() × total`, `cum > u`인 첫 항목. 가중치 0 이하는 건너뜀. 비복원이면 뽑힌 항목을 지우고 반복 | `WeightedSampler.PickIndex`, `WeightedSampler.SampleWithoutReplacement` | 레벨업, 스폰 종 선택 |
| R10 | 결정적 순회 순서 | 엔티티 집합 순회·피해 적용·처치·지연 명령 적용은 모두 `EntityId` 오름차순. 이벤트는 `CollisionEvent.CompareTo` 순서. 명령은 (Source, Sequence) 순서 | `EntityId.CompareTo`, `CollisionEvent.CompareTo`, `SimulationCommandBuffer`(정렬 키 정의는 Core `SimulationCommand.Compare`) | 모든 구현자 |
| R11 | 엔티티 생성·회수 순서 | 5.10의 순서 그대로. 다른 경로로 `Acquire`/`Release`/`Allocate`/`Free`를 호출하지 않는다 | `EntityLifecycle.Spawn`, `EntityLifecycle.Despawn` (Simulation, IMP-LOOP 소유. 5a에서 유일한 비 Core 소유 중 하나) | 몬스터·무기·드랍 구현자 |
| R12 | 레벨업 후보 정규 순서 | `UpgradeKind` 값 오름차순(WeaponUpgrade, WeaponNew, PassiveUpgrade, PassiveNew, Heal) → `TargetId` 서수(Ordinal) 문자열 비교 | `UpgradeOrdering.Compare` (Core) | 후보 생성, 테스트 |
| R13 | 패시브 보정 | 공격 속도: `max(1, round(baseSteps / (1 + bonus)))`. 이동 속도: `base × (1 + bonus)`. 획득 범위: `base × (1 + bonus)`. bonus = 보유 단계의 `levels[level-1].bonus`, 미보유는 0 | `StatMath.ApplyAttackSpeed(int baseSteps, float bonus)`, `StatMath.ApplyMultiplier(float baseValue, float bonus)` | 무기(W1·W3·W4 쿨다운, W2 틱 간격 [기본값 D14]), 플레이어 이동, 자석 반경 |
| R14 | 공간 해시 셀 키 | `cx = FloorToInt(x / cellSize)`, `cy = FloorToInt(y / cellSize)`, `key = ((long)cx << 32) ∣ (uint)cy`. 음수 좌표 포함 | `SpatialHash.CellOf(Vector2 p, float cellSize)`, `SpatialHash.KeyOf(int cx, int cy)` | 몬스터 인덱스, 픽업 해시, 개발자 시각화 |
| R15 | 객체 ID 형식 | 5.1 `EntityId`. 문자열은 `"{Index}:{Generation}"`. CSV·로그·쿨다운 기록·타깃 결과 모두 이 형식 | `EntityId.ToString`, `EntityId.CompareTo` | 전부 |
| R16 | 측정 마커 이름 | `Survivor.Step`, `Survivor.Pool`, `Survivor.Targeting`, `Survivor.SpatialIndex`, `Survivor.Tick.Monsters`, `Survivor.Tick.Projectiles`, `Survivor.Collision` | `ProfilerMarkers` (static readonly `ProfilerMarker` 필드) | 루프, 전략 구현, `StatsCollector` |
| R17 | 대각 이동 | 입력 (x, y) ∈ {−1, 0, 1}² → 8방향 단위 벡터 상수 표(대각 = 0.70710677f). 방향이 영벡터면 `DirectionOrDefault`가 `Vector2.right` | `MoveMath.ToUnitDirection(int x, int y)`, `MoveMath.DirectionOrDefault(Vector2 v)` | 키보드 입력, W1 발사 |
| R18 | 디스폰 판정 | `(pos − playerPos).sqrMagnitude > D²`(엄격 초과). 보스는 제외 | `SpawnGeometry.IsBeyondDespawn(Vector2 pos, Vector2 player, float d)` | 디스폰 시스템, 개발자 시각화 |
| R19 | 몬스터 콜라이더 켜짐 조건 | `Collision == EngineCollider2D ∥ Target == Physics2DQuery` | `ColliderActivationPolicy.MonsterCollidersEnabled(StrategySelection s)` | 조립 루트, 엔진 전략, Physics2D 공급자 |
| R20 | 규칙 상수 | `SpawnRadius = 20f`, `DefaultDespawnDistance = 45f`, `MonsterCapDefault = 400`, `BossSpawnStep = 36000`, `ChestDropChance = 0.02f`, `ChestUpgradeChance = 0.70f`, `HealRatio = 0.30f`, `MaxWeaponSlots = 4`, `MaxPassiveSlots = 3`, `CameraOrthographicSize = 9f` | `GameRules` (static class, `const`) | 전부 |
| R21 | 경험치 곡선 | 5.13.7 `xpCurve` 정의 | `XpCurve.RequiredForNext(int level, XpCurveDefinition def)` (Data) | 경험치 시스템, 개발자 "레벨 즉시 상승" |

---

## 5b. 외부 서비스 설정 점검표 (External Service Checklist)

인증·스토리지 같은 외부 서비스 연동은 **없다**. 대신 사용자만 할 수 있는 에디터·빌드 설정과 GitHub Releases 업로드가 F13·F15에 걸려 있어 짧게 정리한다.

| 설정 (사용자 수행) | 없으면 생기는 일 | 증상 | 확인 상태 |
|---|---|---|---|
| Unity 6000.3.26f1 + Windows Build Support 모듈 (F15) | 빌드 불가 | Build Profiles에 Windows가 비활성 | 실측 필요 |
| 2D 템플릿 프로젝트를 `indiv/` 아래 `Survivor/`로 생성 (5c.1) | 구현자가 파일을 둘 경로가 없다 | - | 사용자 결정 필요(폴더명) |
| Player Settings: 1920x1080, Windowed, Resizable Window 끔, Default Is Native Resolution 끔 | 16:9가 깨지고 Q44 계산이 무효 | 창 크기 변화, 모서리 스폰이 보임 | 실측 U24 |
| Active Input Handling = Input System Package | `Keyboard.current`가 null | NullReferenceException | 실측 U17 |
| 한글 글꼴(TTF, 예: Noto Sans KR, 라이선스 기록) 임포트 + UI Toolkit Font Asset 생성 | 한글이 네모로 표시 | 타이틀·결과 문자 깨짐 | 실측 U18 |
| `Assets/Resources/AssetKeyRegistry.asset` 생성(메뉴 Survivor/Asset Key Registry) | 모든 키가 기본 도형으로 대체 + 경고 | 마젠타 원만 보임 | 코드는 폴백 처리 |
| Newtonsoft JSON 패키지 추가(D17에서 채택 시) | Data 어셈블리 컴파일 실패 | `JToken` 미정의 | 실측 U25, 승인 게이트 |
| 빌드 2종: Development Build(Autoconnect Profiler 체크) / 릴리스 | F10 공식 측정 불가 | Profiler 미연결, 시스템 ms가 `NA` | 실측 U14 |
| GitHub Releases: 빌드 폴더 전체를 zip(`StreamingAssets` 포함) | 다른 PC에서 데이터 로드 실패 | 시작 차단 화면 "weapons.json 없음" | F13 테스터 확인 |
| 저장소 공개 전 점검: `persistentDataPath` 덤프·CSV는 저장소 밖이라 커밋되지 않음. 개인 경로가 들어간 로그 파일이 저장소에 있는지 확인 | 개인 정보 노출 | - | 사용자 확인 |

순서: 프로젝트 생성 → 입력·Player 설정 → 글꼴 → (구현 1차 디스패치) → 레지스트리 에셋 → 빌드 2종 → Releases 업로드.

---

## 5c. 소유권 맵·인터페이스 계약 초안 (Ownership Map & Contract Draft)

**이 절이 5절과 어긋나면 이 절이 우선한다.** 구현자는 각자 스크래치 디렉터리(`<프로젝트>/.scratch/<IMP 이름>/`)에서 아래 최종 경로와 같은 상대 경로로 작업한다(ORCHESTRATION.md "구현 에이전트 디스패치 원칙"). 남의 파일이 필요하면 자기 스크래치 안에 스텁을 두고, 조립 시 소유자 버전만 채택된다.

### 5c.1 폴더 구조와 어셈블리

Unity 프로젝트 루트 = `indiv/Survivor/` [제안, 사용자 결정]. 하네스 문서는 `indiv/` 루트에 그대로 둔다.

```
Survivor/
  Assets/
    Scripts/
      Core/            Survivor.Core.asmdef          (refs: none)
      Data/            Survivor.Data.asmdef          (refs: Core, Newtonsoft*)
      Simulation/      Survivor.Simulation.asmdef    (refs: Core, Data)
        Loop/ Entities/ Player/ Weapons/ Monsters/ Pickups/ Progression/
      Strategies/      Survivor.Strategies.asmdef    (refs: Core)
        Pool/ Spatial/ Targeting/ Tick/ Collision/
      UI/              Survivor.UI.asmdef            (refs: Core, Data, Simulation)
      App/             Survivor.App.asmdef           (refs: Core, Data, Simulation, Strategies, UI, Dev)
      Dev/             Survivor.Dev.asmdef           (refs: Core, Data, Simulation, Strategies)
    Editor/            Survivor.Editor.asmdef        (Editor only; refs: Dev, Data)
    UI/                *.uxml, *.uss, PanelSettings.asset, Fonts/
    Resources/         AssetKeyRegistry.asset        (사용자 생성)
    StreamingAssets/Data/  weapons.json monsters.json combinations.json spawnTable.json passives.json levelUpPool.json playerStats.json
    Scenes/            Main.unity                    (Bootstrap 오브젝트 + Camera + 빈 UIDocument)
    Tests/
      EditMode/        Survivor.Tests.EditMode.asmdef (refs: 전부 + nunit; Editor platform)
      PlayMode/        Survivor.Tests.PlayMode.asmdef (refs: 전부 + nunit)
  docs/devlog.md
  README.md
```
- `Simulation`은 `Strategies`를 참조하지 않는다. 구현 클래스는 `App`(조립 루트)만 안다. `Strategies`도 `Simulation`을 참조하지 않는다(필요한 타입은 전부 Core에 있다: `CollisionBody`, `MonsterSnapshotBuffer`, `IUpdatable`).
- `Dev`를 참조하는 것은 `App`과 `Editor`뿐이다.
- 이름 접두사는 쓰지 않는다. 네임스페이스와 asmdef로 충돌을 막는다. 다만 **클래스명은 프로젝트 전체에서 고유**해야 한다(Unity가 MonoBehaviour를 파일명으로 찾으므로, CODING_GUIDE §2.2).
- 프리팹 파일(`.prefab`)은 만들지 않는다. 템플릿은 `PrefabCatalog`가 코드로 만든다. 에이전트가 에디터 없이 작업할 수 있게 하고, 병렬 작업에서 YAML 충돌을 피하기 위해서다.

### 5c.2 파일 소유권 맵

| 소유자 | 파일 (경로는 `Assets/` 기준) |
|---|---|
| **IMP-CORE** | `Scripts/Core/`: `Survivor.Core.asmdef`, `EntityId.cs`, `EntityIdAllocator.cs`, `DeterministicRng.cs`, `RngStreams.cs`, `StepMath.cs`, `CircleMath.cs`, `CollisionChannelTable.cs`, `TargetOrdering.cs`, `SpawnGeometry.cs`, `HealWeight.cs`, `WeightedSampler.cs`, `UpgradeOrdering.cs`, `StatMath.cs`, `MoveMath.cs`, `GameRules.cs`, `ProfilerMarkers.cs`, `ColliderActivationPolicy.cs`, `SimulationRules.cs`, `StrategySelection.cs`, `StepContext.cs`, `SimulationCommand.cs`, `ISimulationCommands.cs`, `IInputSource.cs`, `MoveCommand.cs`, `IPoolStrategy.cs`, `ITargetProvider.cs`, `MonsterSnapshot.cs`, `MonsterSnapshotBuffer.cs`, `TargetHit.cs`, `IUpdatable.cs`, `ITickStrategy.cs`, `ICollisionStrategy.cs`, `CollisionBody.cs`, `CollisionEvent.cs`, `CollisionEnums.cs`(Layer·Channel·EventKind), `StrategyKinds.cs`(4개 Kind enum), `ISpawnPolicy.cs`, `DespawnReason.cs`; `Tests/EditMode/Core/*` |
| **IMP-DATA** | `Scripts/Data/` 전부: `Survivor.Data.asmdef`, `Definitions/*.cs`(Weapon·WeaponLevel·Monster·ChargeParams·RangedParams·BossPattern·CombinationRule·SpawnTable·SpawnSegment·Passive·LevelUpPool·PlayerStats·Pickups·XpCurveDefinition), `GameData.cs`, `GameDataLoader.cs`, `GameDataRegistry.cs`, `JsonTreeReader.cs`, `Binders/*Binder.cs`(7개), `DataValidationReport.cs`, `ValidationIssue.cs`, `TunnelingInvariantValidator.cs`, `MovingObjectSpec.cs`, `PlayabilityChecker.cs`, `XpCurve.cs`, `AssetKeyRegistry.cs`, `AssetKeyResolver.cs`, `VisualAsset.cs`, `DefaultShapeFactory.cs`; `StreamingAssets/Data/*.json` 7개; `Tests/EditMode/Data/*`(픽스처 포함, E3) |
| **IMP-LOOP** | `Scripts/Simulation/Loop/`: `SimulationClock.cs`, `SimulationFrameCounter.cs`, `SimulationPhase.cs`, `IStepPhaseHandler.cs`, `SimulationPipeline.cs`, `SimulationDriverEarly.cs`, `SimulationDriverMid.cs`, `SimulationDriverLate.cs`, `SimulationContextHub.cs`, `SimulationCommandBuffer.cs`, `ISimulationCommandHandler.cs`, `PendingCommandQueue.cs`, `CollisionEventRouter.cs`, `ICollisionEventHandler.cs`, `SimulationSettingsApplier.cs`, `SimulationStateHash.cs`, `KeyboardInputSource.cs`; `Scripts/Simulation/Entities/`: `SimEntity.cs`, `EntityCategory.cs`, `EntityLifecycle.cs`, `PrefabCatalog.cs`, `PoolPrewarmCounts.cs`, `PresentationSync.cs`, `MonsterRegistry.cs`(스냅샷 채우기 포함); `Scripts/Strategies/Tick/`: `CentralTickStrategy.cs`, `SelfTickStrategy.cs`, `MonsterSelfTicker.cs`, `ProjectileSelfTicker.cs`; `Survivor.Simulation.asmdef`, `Survivor.Strategies.asmdef`; `Tests/EditMode/Loop/*`, `Tests/PlayMode/Loop/*`(고정 스텝·프레임레이트 결정론) |
| **IMP-POOL** | `Scripts/Strategies/Pool/`: `PooledObject.cs`, `CustomPoolStrategy.cs`, `InstantiatePoolStrategy.cs`, `UnityObjectPoolStrategy.cs`; `Tests/PlayMode/Pool/*` |
| **IMP-SPATIAL** | `Scripts/Strategies/Spatial/`: `SpatialHash.cs`, `MonsterSpatialIndex.cs`, `SpatialHashSettings.cs`; `Scripts/Strategies/Targeting/`: `BruteForceTargetProvider.cs`, `SpatialHashTargetProvider.cs`, `Physics2DTargetProvider.cs`; `Tests/EditMode/Spatial/*`, `Tests/PlayMode/Targeting/*` |
| **IMP-COLLISION** | `Scripts/Strategies/Collision/`: `SelfCollisionStrategy.cs`, `EngineCollisionStrategy.cs`, `CollisionBodyLink.cs`, `PhysicsLayerSetup.cs`, `PairSet.cs`; `Tests/EditMode/Collision/*`, `Tests/PlayMode/Collision/*`(E1·E2 하네스) |
| **IMP-PLAYER** | `Scripts/Simulation/Player/`: `PlayerEntity.cs`, `PlayerMovement.cs`, `PlayerHealth.cs`, `PlayerDamageReceiver.cs`, `PassiveInventory.cs`; `Scripts/Simulation/Weapons/`: `WeaponInventory.cs`, `WeaponInstance.cs`, `WeaponFactory.cs`, `ProjectileWeapon.cs`, `OrbitWeapon.cs`, `ZoneWeapon.cs`, `PlayerProjectile.cs`, `OrbitOrb.cs`, `DamageZone.cs`, `ContactDamageTracker.cs`, `EvolutionService.cs`, `ChestEffectResolver.cs`, `ChestEffect.cs`; `Tests/EditMode/Player/*` |
| **IMP-MONSTER** | `Scripts/Simulation/Monsters/`: `Monster.cs`, `MonsterBehaviorState.cs`, `EnemyProjectile.cs`, `EnemyHazardZone.cs`, `MonsterSpawner.cs`, `SpawnScheduler.cs`, `TableSpawnPolicy.cs`, `DespawnSystem.cs`, `KillPipeline.cs`, `MonsterCommandHandler.cs`; `Scripts/Simulation/Pickups/`: `ExperienceGem.cs`, `Chest.cs`, `PickupSystem.cs`, `DropService.cs`; `Tests/EditMode/Monsters/*` |
| **IMP-FLOW** | `Scripts/Simulation/Progression/`: `ExperienceSystem.cs`, `UpgradeOption.cs`, `LevelUpCandidateBuilder.cs`, `LevelUpSelector.cs`, `RunSession.cs`, `BestRecordStore.cs`; `Scripts/UI/` 전부(`Survivor.UI.asmdef`, `TitleView.cs`, `HudView.cs`, `LevelUpView.cs`, `PauseMenuView.cs`, `ResultView.cs`, `DataErrorView.cs`, `UiInputHandler.cs`); `Assets/UI/*.uxml`, `*.uss`; `Scripts/App/`: `Survivor.App.asmdef`, `GameBootstrap.cs`, `GameFlowController.cs`, `FlowState.cs`, `StrategyFactory.cs`, `CameraFollow.cs`, `BackgroundTiler.cs`; `Scenes/Main.unity`; `Tests/EditMode/Progression/*`, `Tests/PlayMode/Flow/*` |
| **IMP-DEV** | `Scripts/Dev/` 전부(5.15 표의 클래스 + `Survivor.Dev.asmdef`); `Editor/DevModeMenu.cs`, `Editor/Survivor.Editor.asmdef`; `Tests/PlayMode/Dev/*` |
| **사용자** | `ProjectSettings/*`, `Packages/manifest.json`(패키지 추가), `Resources/AssetKeyRegistry.asset`, `UI/Fonts/*`, `UI/PanelSettings.asset`(U19에 따라) |
| **doc-writer** | `docs/devlog.md`(틀), `README.md`(구현 후) |

메모: `Survivor.Strategies.asmdef`는 IMP-LOOP가 소유한다(먼저 디스패치되는 쪽). 다른 Strategies 구현자는 스크래치에 스텁 asmdef를 둔다.

### 5c.3 계층 간 핵심 계약 요약 (구현자가 반드시 지킬 것)

1. 엔티티 생성·회수는 `EntityLifecycle.Spawn/Despawn`만 쓴다(R11). 풀·ID 할당기를 직접 부르지 않는다.
2. `IUpdatable.Tick`은 자기 상태만 쓰고, 부작용은 `context.Commands.Enqueue`로 넘긴다. 생성된 그 스텝에는 `Tick`이 바로 반환한다(5.4).
3. 집합 순회·피해 적용은 `EntityId` 오름차순이다(R10). `Dictionary`/`HashSet`를 순서가 중요한 곳에서 순회하지 않는다.
4. 시간은 스텝 정수, 속도는 스텝당 값으로만 계산한다. 초 단위 상수가 코드에 나타나면 안 된다(R1·R3).
5. 난수는 `RngStreams.Get(stream)`만 쓴다. 스트림 배정: 스폰 종·각도 = Spawn, 소환 각도 = Summon, 상자 드랍 = Drop, 상자 효과 = Chest, 레벨업 = LevelUp.
6. 위치의 원본은 `SimEntity.Position`(시뮬레이션 데이터)이다. `transform.position`은 `PresentationSync.LateUpdate`가 프레임당 1회 쓴다. 단 `Body.DrivesTransform == true`인 엔진 모드 바디는 쓰지 않는다 [실측 U10].
7. 오류 규약: 계약 위반(중복 반환, 같은 채널에 핸들러 두 번 등록, 같은 순서값의 단계 핸들러 등)은 `InvalidOperationException`을 던진다. 데이터 오류는 던지지 않고 `DataValidationReport`에 넣는다. 런타임의 예상 가능한 실패(타깃 없음 등)는 `bool Try...` 패턴으로 처리한다. 예외를 잡는 곳은 드라이버 3개 각각 한 곳이고, 잡으면 `[시각] [ERROR] phase=<단계> step=<n> <메시지>`를 남긴 뒤 게이트를 닫고 결과 화면이 아니라 오류 표시로 간다(CODING_GUIDE §4·§5).
8. 로그 형식: `[HH:mm:ss.fff] [LEVEL] [Area] message` (Area = Data/Loop/Pool/Collision/Flow/Dev). 판 시작·종료·레벨업·진화·핫 리로드·전략 변경은 성공해도 한 줄을 남긴다(§5). 스텝마다 남기는 로그는 금지한다(측정 오염).
9. 단계 핸들러 등록 순서(`order`)는 아래로 고정한다. 바꾸려면 PM 승인이 필요하다.

| 단계 | order: 핸들러 (소유자) |
|---|---|
| Begin | 0 `PendingCommandQueue`(LOOP), 10 입력 소비(PLAYER `PlayerMovement`가 `ReadMove` 캐시) |
| PlayerMove | 0 `PlayerMovement`(PLAYER) |
| MonsterTick | (전략) |
| BuildIndex | 0 `MonsterRegistry.FillSnapshot`(LOOP), 10 `MonsterSpatialIndex.Rebuild`(SPATIAL, 조립 시 연결), 20 `ITargetProvider.OnSnapshotBuilt`(SPATIAL) |
| WeaponTick | 0 `WeaponInventory`(PLAYER, 슬롯 순서), 10 젬 끌림 이동 `PickupSystem`(MONSTER) |
| ProjectileTick | (전략) |
| Collision | 0 `CollisionEventRouter` 수집(LOOP) |
| Resolve | 0 라우터 디스패치(LOOP), 10 무기 `ContactDamageTracker` 피해(PLAYER), 20 `PlayerDamageReceiver`(PLAYER), 30 `KillPipeline`(MONSTER: hp ≤ 0 확정 → 드랍 → 회수 명령) |
| Lifecycle | 0 `SimulationCommandBuffer`(LOOP), 10 수명 만료(LOOP가 `SimEntity` 만료 스텝 기준으로 처리), 20 `DespawnSystem`(MONSTER), 30 `MonsterSpawner`(MONSTER), 40 `ExperienceSystem` 레벨업 판정(FLOW), 50 종료 판정(FLOW: 사망/클리어) |
| End | 0 `SimulationClock.EndStep`(LOOP) |

10. 데이터 접근: 소비자는 `GameDataRegistry.Current`를 생성 시점에 한 번 읽고 정의 참조를 보관한다(Q43 기본값: 활성 객체는 기존 정의 유지).

### 5c.4 권장 디스패치 순서

| 웨이브 | 구현자 (병렬) | 선행 조건 | 산출물 검증 |
|---|---|---|---|
| 0 | 사용자: Unity 프로젝트 생성·설정(5b 앞 3줄), **Unity 동작 실측 스파이크**(U1·U2·U5·U7·U8·U10·U11 — 아래 메모) | 없음 | 실측 결과를 RUN_LOG에 기록 |
| 1 | **IMP-CORE** (단독) | 웨이브 0의 프로젝트 폴더 | EditMode Core 테스트 통과 |
| 2 | **IMP-DATA**, **IMP-LOOP**, **IMP-POOL**, **IMP-SPATIAL** | Core 확정, D17(파서) 결정 | 각자 EM 테스트 |
| 3 | **IMP-COLLISION**, **IMP-PLAYER**, **IMP-MONSTER** | 웨이브 2 조립 완료(Spatial 인덱스, 생명주기, 정의 객체). COLLISION은 U5·U7 실측 결과 필요 | EM + PM(E1·E2 하네스 골격) |
| 4 | **IMP-FLOW** (조립 루트·UI 포함) | 웨이브 3 | 타이틀 → 플레이 → 결과 PM 테스트, 전략 조합 결정론 PM 테스트 |
| 5 | **IMP-DEV** | 웨이브 4 | N스텝 자동 종료, CSV, 충돌 로그, E1·E2 실행 |
| 6 | doc-writer(README), 사용자(F10 측정·F13·F14) | 웨이브 5 | - |

웨이브 0 실측 스파이크 메모: 빈 2D 프로젝트에 스크립트 1개를 넣어 (a) 서로 다른 `DefaultExecutionOrder`의 `FixedUpdate` 순서, (b) `Time.captureDeltaTime = 0.1`에서 프레임당 `FixedUpdate` 횟수(3이어야 함), (c) `simulationMode = Script` + `Simulate()` 호출 중 키네마틱-키네마틱 트리거 Enter/Exit 발생 여부와 시점, (d) `callbacksOnDisable = false`에서 비활성화 시 Exit 유무, (e) `MovePosition` 뒤 `OverlapCircle` 반영 시점을 로그로 확인한다. 구현 코드가 아니라 실측용이므로 결과만 RUN_LOG에 남기고 버린다. 이 결과에 따라 5.9 엔진판 바디 구성(Kinematic인지 Dynamic + gravityScale 0인지)이 바뀔 수 있다.

### 5c.5 테스트 전략

| 대상 | 위치 | 방법 |
|---|---|---|
| 공유 규칙 R1~R21 | EM | 경계값 표 테스트(0.5스텝 반올림, 음수 좌표 셀 키, `(1−r)²` 끝점, 동거리 타이브레이크) |
| RNG 분포 | EM | 시드 고정: 상자 10,000회 ≈ 2%, 상자 효과 70:30, 레벨업 1,000회 빈도 순서, 회복 빈도의 체력 단조성 |
| 스폰 스케줄 | EM | 구간별 60초 스폰 수 = rate × 60 정확히, 상한 시 버림, 구간 밖 종 0 |
| JSON 로더·검증 | EM | 7종 × (정상 / 깨진 JSON / 필수 누락 / 타입 오류 / ID 중복 / 참조 오류 / 알 수 없는 키 / 에셋 키 없음) 픽스처. 필수 4종 차단, 선택 3종 폴백, 플레이 불가 4기준 |
| **E3** | EM | `GameDataLoader.LoadAll(<프로젝트>/Assets/StreamingAssets)` → `TunnelingInvariantValidator.EnumerateMovingObjects` 전부가 `v/60 ≤ r`인지 단언. 게임을 돌리지 않는다 |
| 타깃 공급자 동등성 | EM(A·B) / PM(C) | 무작위 배치 10,000건 + 격자점 동거리 배치 + 보스 포함. 반환 `TargetHit` 비트 일치 |
| 자체 충돌 Enter/Exit | EM | 스크립트 배치 시퀀스로 기대 이벤트 목록과 비교. 등록 해제 시 Exit 없음 |
| 피해 틱 | EM | `ContactDamageTracker`: Enter 즉시 1회, 간격, Exit 후 재진입 악용 불가. `PlayerDamageReceiver`: 400기 겹침에도 무적 스텝마다 1회 |
| 고정 스텝 | PM | `Time.captureDeltaTime`을 1/30, 1/60, 1/144로 바꿔 각각 600스텝 → `SimulationStateHash` 일치. 1/10에서 프레임당 스텝 ≤ 3 [실측 U3] |
| 전략 조합 결정론 | PM | 시드 고정, 레벨업 비활성, 자동 경로, 무적, 무기 W1~W4 5단계, N=200 유지, 1,800스텝. 조합 3×3×2×2 = 36개 전부의 상태 해시 일치(시간이 오래 걸리면 기준선 대비 단일 변경 7개 + 전부 켬 1개로 줄인다) |
| **E1** | PM | 위 시나리오를 `Collision = Self`와 `Engine/Discrete`로 각각 3,600스텝 실행하고 `LoggingCollisionStrategy` 로그를 (스텝, 종류, 채널, A, B) 집합으로 비교. 다르면 첫 불일치 스텝과 두 바디의 거리·반지름 합을 출력 |
| **E2** | PM | 같은 시나리오를 `Engine/Discrete`와 `Engine/Continuous`로 실행해 같은 방식으로 비교. U6 실측 결과에 따라 "트리거에는 CCD가 적용되지 않아 자명하게 같음"이 확인되면 그 사실을 devlog에 기록한다(6.2 U6) |
| 흐름 | PM | 타이틀 → 시작 → 사망 → 결과 → 재시작 → 상태 초기화(ID 1:1부터, 스텝 0, 풀 활성 0) |
| 개발자 모드 | PM | N스텝 자동 종료 정확성, CSV 헤더·열 수, 시각화 끔 시 컴포넌트 비활성, `--dev` 없는 실행에서 `Survivor.Dev` 타입 인스턴스 0개 |

실행: 에디터 Test Runner 또는 `Unity.exe -batchmode -projectPath Survivor -runTests -testPlatform EditMode -testResults em.xml`(PlayMode도 같은 방식) [실측 U27]. 개발자 모드의 Development Build 측정(F10)은 사용자가 수행한다.

---

## 6. 미확정 사항 (Open Questions)

### 6.1 코드에서 확인됨 → 이 프로젝트에서는 "문서 간 불일치 / SPEC 미규정" (코드가 없으므로 SPEC·RUN_LOG를 읽고 확인한 것)

각 항목에 [기본값]을 적었다. blocking 여부도 표시했다.

- **D1.** F18 불변식 검사 대상 목록(W1·W4, 몬스터, M3 투사체, W2, W3)에 **플레이어**(PlayerBody ↔ 몬스터 쌍의 한쪽이고 움직인다)와 **끌리는 젬**이 빠져 있다. [기본값] 둘 다 검사 대상에 넣는다. 플레이어 속도는 P2 최대 단계를 적용한 값. 비차단.
- **D2.** W2 궤도의 "선속도 = 각속도 × 궤도 반지름"(RUN_LOG)은 플레이어 이동을 뺀 값이다. 궤도 원의 실제 월드 이동량은 ωR + 플레이어 속도다. [기본값] 검사에는 ωR + 플레이어 최대 속도를 쓴다. 비차단.
- **D3.** 젬·상자에는 디스폰 규칙이 없다. 무한 평면에서 플레이어가 떠나면 회수되지 않은 젬이 계속 쌓여 풀·충돌 비용이 커진다. 범위 결정이 필요하다(PM). [기본값] 규칙을 추가하지 않고, F10 측정 때 젬 수를 함께 기록해 문제인지 본다. 비차단.
- **D4.** SPEC의 "스폰 위치는 r=20 원주"가 보스 등장과 보스 소환에도 적용되는지 정해져 있지 않다. [기본값] 보스는 같은 원주, 소환은 보스 주변 `summonRadius` 원주다(소환체는 화면 안에 생길 수 있다). 비차단.
- **D5.** 진화로 소모된 원본 무기(예: W1 → E1 뒤의 W1)를 레벨업에서 다시 "새 무기"로 얻을 수 있는지 SPEC에 없다. [기본값] 다시 얻을 수 없다(후보에서 제외). 비차단.
- **D6.** 보스(M4) 정의가 무효면 10분 뒤 클리어가 불가능하지만, SPEC의 "플레이 불가 4기준"에는 들어 있지 않다. [기본값] Warning만 남기고 시작은 허용한다. 5번째 기준으로 추가하려면 PM이 결정한다. 비차단.
- **D7.** `startingWeaponId`가 무효일 때의 처리가 없다. [기본값] 플레이어 기본 스탯 오류(Blocking)로 본다. 무효인 ID로 시작하면 무기가 없어 첫 레벨업이 불가능하므로(F2 근거). 비차단.
- **D8.** RUN_LOG는 E1·E2 비교 키를 (스텝, 종류, 객체 ID 쌍)으로 정했다. 그런데 플레이어 몸통과 자석이 같은 ID를 쓰면 같은 젬에 대한 두 채널 이벤트가 집합에서 하나로 합쳐진다. [기본값] 자석에 별도 ID를 주고, 비교 키에 채널도 넣는다. 비차단(PM 확인 권장).
- **D9.** 개발자 모드 배속 4x와 catch-up 상한 3은 산술적으로 충돌한다. 60FPS에서 4x는 프레임당 4스텝이 필요하지만 상한이 3이라 실제로는 최대 3x다(U23이 맞다면). [기본값] 상한을 유지하고 오버레이에 "실제 배속"을 표시한다. 대안은 배속 > 1일 때만 `maximumDeltaTime`을 올리는 것이며 PM이 결정한다. 비차단.
- **D10.** SPEC은 전략을 "런타임 토글 또는 빌드 설정"으로 전환하라고 하고, F16에는 "재시작 없이"가 있다. 풀·틱·충돌 전략을 판 진행 중에 바꾸면 활성 객체가 옛 전략에 묶여 있다. [기본값] 타깃 공급자만 즉시 바꾸고, 나머지 셋은 "다음 판 시작"(앱 재시작은 아님)에 적용한다. 비차단(PM 확인 권장).
- **D11.** 몸통 접촉으로 적 여럿이 동시에 겹칠 때 어떤 피해량을 적용하는지 정해져 있지 않다. [기본값] 겹친 후보 중 최대 피해, 같으면 작은 ID. 비차단.
- **D12.** 무적 중 M3 투사체에 맞으면 투사체가 소모되는지 관통하는지 정해져 있지 않다. [기본값] 피해 없이 소모. 비차단.
- **D13.** W2 쿨다운 기록의 단위(무기 인스턴스 / 궤도 원 하나하나)가 정해져 있지 않다. 궤도 원 단위면 같은 적이 원 개수만큼 맞는다. [기본값] 무기 인스턴스 단위(RUN_LOG의 "공격자 측 쌍별"을 "무기-대상 쌍"으로 해석). 밸런싱 시 변경 가능. 비차단.
- **D14.** P1 "모든 무기의 공격 간격"이 W2 틱 간격과 W3 장판의 틱 간격에도 적용되는지 정해져 있지 않다. [기본값] W1·W3·W4의 쿨다운과 W2 틱 간격에 적용하고, W3 장판 내부 틱 간격에는 적용하지 않는다. 비차단.
- **D15.** SPEC F18의 "구현 B 후보는 공간 해시로 추리고 전수 비교하지 않는다"를 플레이어 1명이 한쪽인 채널(적 투사체·적 장판 → 플레이어)에서 어떻게 해석할지가 애매하다. 상대가 1개이면 O(N) 선형 순회이며 쌍 전수는 아니다. [기본값] 몬스터 채널은 공유 몬스터 인덱스, 젬·상자는 픽업 전용 해시, 적 투사체·적 장판은 선형 순회. 비차단.
- **D16.** SPEC의 "젬 ↔ 플레이어" 판정 하나가 이 설계에서는 채널 두 개(자석 반경 Enter = 끌림 시작, 몸통 Enter = 획득)로 나뉜다. 획득도 `ICollisionStrategy`를 거치라는 SPEC 요구를 지키기 위해서다. 비차단(정보 공유).
- **D17.** JSON 파서를 정해야 한다. `JsonUtility`로는 필수 누락·알 수 없는 키를 판정할 수 없어 SPEC F11 기준을 못 맞춘다. 선택지는 (a) Newtonsoft 패키지 추가(승인 게이트 대상, U25) 또는 (b) 자체 최소 파서(약 300줄, 의존성 0). [기본값] (a). **IMP-DATA 착수 전 PM 결정 필요 — 기본값을 받아들이면 비차단.**
- **D18.** SPEC F3은 "r=20이 1920x1080 화면 대각 반절보다 커야 한다"고 적었다. 그런데 화면 크기는 픽셀이고 r은 월드 유닛이라 단위가 섞였다. 이 문서는 orthographic size 9로 환산해 검증했다(2.4: 반대각 18.36, 여유 1.64). 반지름이 1.64 이상인 몬스터(기본값 보스 1.5는 통과, 키우면 위반)는 모서리 방향에서 스폰 순간 일부가 보인다. 비차단.
- **D19.** 개발자 모드 "레벨업 비활성" 상태에서 경험치를 어떻게 처리할지 정해져 있지 않다. [기본값] 경험치는 쌓이고 레벨은 오르지 않는다. 비차단.
- **D20.** SPEC F16은 "몬스터의 플레이어 탐색"도 `ITargetProvider`를 쓴다고 적었다. 하지만 플레이어가 1명뿐이라 탐색할 것이 없다. [기본값] 몬스터는 `StepContext.PlayerPosition`을 직접 쓴다. `ITargetProvider`는 "가장 가까운 몬스터" 전용. 비차단.
- **D21.** 개발자 오버레이의 문자열 언어: CODING_GUIDE §3은 "사용자에게 보이는 문자열은 한국어"라고 한다. 오버레이는 개발자용이다. [기본값] 한국어로 쓰고 같은 한글 글꼴을 쓴다. 비차단.
- **D22.** RUN_LOG의 "풀 반납 때마다 세대 증가"를 문자 그대로 구현하면(ID가 풀 슬롯에 붙으면) F9 구현 B에 슬롯이 없어 전략마다 ID가 달라지고, F9 수용 기준 "스텝 단위 결과가 같다"와 E1 로그 비교가 깨진다. [기본값] 세대를 가진 ID를 풀과 독립된 `EntityIdAllocator`가 발급한다(시점은 반납과 같음). 비차단(PM 확인 권장).
- **D23.** 상자 효과 ②의 강화 대상 선택 방식(균등/가중)이 정해져 있지 않다. [기본값] 슬롯 순서 목록에서 균등. 비차단.
- **D24.** W3 장판을 생성할 때 적이 없으면 어떻게 할지 정해져 있지 않다(Q8 기본값은 "가장 가까운 적 위치"). [기본값] 생성하지 않고 다음 스텝에 다시 시도(W1과 같은 규칙). 비차단.

### 6.2 실측/실행 필요 — 미확정 (Needs measurement/execution — unconfirmed)

아래는 모두 정적으로 읽어서는 확정할 수 없는 Unity 동작 전제다(2.3 표와 번호가 같다). **사실로 취급하지 않는다.** 웨이브 0 스파이크나 해당 구현자의 PlayMode 테스트로 확인한다.

- **U1** `DefaultExecutionOrder`가 `FixedUpdate`에 적용되는지, 인스턴스 간 순서가 보장되지 않는지 — 드라이버 인터리브의 전제. 틀리면 F17 A 모드의 단계 순서를 다시 설계해야 한다. **IMP-LOOP 착수 전 확인 권장.**
- **U2** `maximumDeltaTime = 0.05`에서 프레임당 정확히 최대 3스텝인지(부동소수 오차로 2나 4가 되지 않는지). 틀리면 SPEC의 "0.05로 설정"을 고쳐야 하므로 PM에게 보고한다.
- **U3** `Time.captureDeltaTime`으로 프레임레이트 결정론 테스트를 할 수 있는지.
- **U4** `Rigidbody2D` Collision Detection 기본값이 `Discrete`인지(SPEC Q39).
- **U5** 키네마틱-키네마틱 트리거 콜백 발생 조건(`useFullKinematicContacts` 필요 여부). 안 되면 Dynamic + gravityScale 0 바디로 바꾼다. **IMP-COLLISION 착수 전 필요.**
- **U6** `Continuous`가 트리거·키네마틱 바디에 영향을 주는지. Box2D 계열 CCD는 보통 동적 바디 ↔ 정적 바디의 비센서 접촉에만 적용되는 것으로 알려져 있어(확인 안 됨), **E2가 "자명하게 동일"로 끝나 SPEC이 의도한 검증이 되지 않을 가능성이 있다.** 그렇다면 그 사실 자체를 devlog 소재로 기록하고, E2의 의미를 PM이 다시 정해야 한다.
- **U7** `simulationMode = Script`에서 `Physics2D.Simulate()` 호출 중 트리거 콜백이 동기적으로 오는지. 틀리면 엔진판 이벤트가 한 스텝 늦어 E1이 스텝 번호에서 갈린다. **IMP-COLLISION 착수 전 필요.**
- **U8** `callbacksOnDisable = false`이면 비활성화 시 Exit이 나오지 않는지.
- **U9** Box2D 원-원 경계 판정과 float 차이 때문에 E1 경계 사례에서 불일치가 생길 수 있는지. 생기면 허용 정책(경계 ±ε 사례는 별도 집계)을 PM이 정한다.
- **U10** `MovePosition`이 스윕 이동인지, `transform` 직접 쓰기가 텔레포트로 처리되는지.
- **U11** `Rigidbody2D.position`·콜라이더 활성화가 `OverlapCircle`에 언제 반영되는지(`autoSyncTransforms` 기본값 포함). `Physics2DTargetProvider`의 여유 반경이 충분한지.
- **U12** 이름 없는 레이어 인덱스에 `IgnoreLayerCollision`을 코드로 설정할 수 있는지.
- **U13** Windows 플레이어에서 `StreamingAssets` 동기 읽기와 빌드 후 편집이 되는지.
- **U14** `ProfilerMarker`가 비개발 빌드에서 제거되는지, `ProfilerRecorder`가 Development Build에서 마커 프레임 합계를 주는지, 스크립트를 넘나드는 `Begin`/`End`가 허용되는지. 안 되면 틱 ms 수집을 Stopwatch 주입 방식으로 바꾼다(Q40의 "비활성 시 감시 없음"과 충돌하는지 PM 확인 필요).
- **U15** "GC Allocated In Frame" 카운터 이름과 가용성.
- **U16** `Environment.GetCommandLineArgs()`에 `--dev`가 들어오는지.
- **U17** Input System 기본 활성 여부, 업데이트 모드, `wasPressedThisFrame` 동작.
- **U18** 한글 글꼴 렌더링(UI Toolkit, IMGUI).
- **U19** UI Toolkit 런타임 `PanelSettings` 생성 가능 여부.
- **U20** 런타임 생성 원 스프라이트의 월드 크기 계산.
- **U21** `ObjectPool<T>`의 `maxSize`·`collectionCheck` 동작.
- **U22** `Destroy` 지연과 같은 프레임 다음 스텝 간섭 여부.
- **U23** `maximumDeltaTime`이 스케일된 시간을 제한하는지(D9 근거).
- **U24** 1920x1080 창 모드가 1080p 모니터에서 16:9를 유지하는지(작업 표시줄·제목 표시줄 때문에 창이 줄어드는지).
- **U25** Newtonsoft 패키지의 6000.3 가용성과 Mono 동작(D17).
- **U26** 같은 빌드·같은 PC에서 float 결과가 실행마다 같은지(결정론 전제 전체).
- **U27** batchmode에서 물리를 포함한 PlayMode 테스트가 도는지.
- **U28** 6.3에서 기존 `Physics2D` API가 그대로 동작하는지(새 저수준 2D 물리 API와 공존).
- **U29** `SpriteRenderer` Tiled 모드로 반복 배경을 그릴 수 있는지.

### 6.3 집계

- 문서 간 불일치/SPEC 미규정: **24건**(D1~D24). blocking은 **D17 1건뿐**이고 그것도 IMP-DATA 착수 조건일 뿐이며, 기본값을 받아들이면 해소된다. PM 확인 권장: D8, D10, D22, D3(범위).
- 실측/실행 필요: **29건**(U1~U29). 착수 전 확인 권장: IMP-LOOP 전 U1·U2, IMP-COLLISION 전 U5·U7(웨이브 0 스파이크로 한 번에 확인). U6은 E2의 의미에 영향을 준다.
