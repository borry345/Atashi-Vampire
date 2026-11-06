# 웨이브 1 디스패치 브리핑 — IMP-CORE (2026-11-06, PM 작성)

> 이 파일은 PM이 구현 에이전트에게 보내는 프롬프트의 원본이다. `dispatch-implementer` 스킬의 8블록을 따른다.
> 보내는 방법: `general-purpose` 에이전트, `model: opus`. 아래 "=== 프롬프트 시작 ===" 이후를 그대로 보낸다.
> 사용자 승인 전 상태. 승인·비용·결과는 `RUN_LOG.md`에 남긴다.

## PM 기본값 (이 디스패치에서 PM이 정한 것 — RUN_LOG에 근거 기록)

1. **스크래치 격리 미적용(웨이브 1 한정)**: 웨이브 1은 IMP-CORE 단독이라 쓰기 경합이 없고, Unity는 `Assets/` 밖의 C#을 컴파일하지 않아 스크래치에서는 EditMode 테스트를 돌릴 수 없다. 최종 경로(`Survivor/Assets/Scripts/Core/`, `Survivor/Assets/Tests/EditMode/`)에서 직접 작업한다. 웨이브 2(4명 병렬)의 격리 방식은 별도 결정.
2. **`Tests/EditMode/Survivor.Tests.EditMode.asmdef`의 첫 작성자 = IMP-CORE**: 5c.2 소유권 맵에 이 asmdef의 소유자가 없다. 웨이브 1에서 처음 필요하므로 IMP-CORE가 만들고(참조: `Survivor.Core`, nunit, UnityEngine.TestRunner, UnityEditor.TestRunner), 뒤 웨이브의 구현자는 참조만 추가한다.
3. **브랜치**: Q20(main + feature 브랜치)에 따라 PM이 `feature/wave1-core`를 만들어 디스패치한다. 에이전트는 커밋하지 않는다. 검증 뒤 PM이 체크포인트 커밋, 사용자 승인으로 main 머지·푸시.
4. **모델 opus**: 결정론·RNG·계약 인터페이스 50여 파일 — "복잡한 로직·상태 관리" 자리.
5. **`.meta` 파일**: 에이전트가 손으로 만들지 않는다. Unity 배치 실행이 생성한 것을 그대로 둔다(커밋 대상).

## 비용 추정

읽기 약 1,300행(tech 5.00~5.09·5.14·5.15 일부·5a·5c·02·spec/00-common) + 파일 약 50개 + 테스트 작성 + Unity 배치 실행(1회 1~3분) 반복. **40만~80만 토큰, 1~2시간.** 에이전트가 PM 전제를 반박하면 PM이 실측(추가 수만 토큰).

## 사전 조건 (사용자)

- **Unity 에디터를 닫아 둔다.** 배치 모드는 같은 프로젝트를 연 에디터가 있으면 실행되지 않는다. 구현이 끝나 PM이 "열어도 된다"고 할 때까지 닫은 상태를 유지한다.

---

=== 프롬프트 시작 ===

# 역할: IMP-CORE — 뱀서라이크 1차 MVP의 Core 계층 구현자

당신은 Unity 6 / C# 프로젝트의 **Core 어셈블리(`Survivor.Core`)와 그 EditMode 테스트**를 구현한다. Core는 순수 C# 계산 계층이다: 공유 규칙 함수, `EntityId`, 결정론 RNG, 계약 인터페이스, 단계 열거형, 이벤트·명령 구조체, 규칙 상수. MonoBehaviour·씬·프리팹·JSON 로더·게임 루프는 전부 다른 구현자의 몫이고 이번 웨이브에는 없다. 당신 코드는 뒤 웨이브 9명의 구현자가 **글자 그대로** 의존하는 계약이므로, 문서의 시그니처·이름을 바꾸지 않는 것이 가장 중요하다.

## 1. 이미 확인된 사실 (다시 조사하지 말 것)

- 프로젝트 루트: `C:\Users\SSAFY\Desktop\borry\indiv` (문서), Unity 프로젝트: `C:\Users\SSAFY\Desktop\borry\indiv\Survivor` (URP 2D 템플릿, Unity **6000.3.28f1**).
- 설치된 에디터: `C:\Program Files\Unity\Hub\Editor\6000.3.28f1\Editor\Unity.exe`. Unity CLI(`unity` 명령)는 없다. 에디터 GUI는 닫혀 있다(열려 있으면 배치 실행이 "다른 인스턴스가 열었다"며 실패한다 — 그러면 BLOCKED로 보고하고 기다리지 말 것).
- 패키지: `com.unity.test-framework` 1.6.0, `com.unity.inputsystem` 1.20.1(Core는 쓰지 않음), `com.unity.render-pipelines.universal` 17.3.0(Core는 쓰지 않음). Newtonsoft는 없다(Core에 불필요).
- `Survivor/Assets/` 현재 내용: `Scenes/SampleScene.unity`, `Settings/*`뿐이다. `Scripts/`·`Tests/`는 아직 없다 — 당신이 처음 만든다.
- 웨이브 0 실측 완료(Unity 동작 전제 중 Core와 관련된 것): 원-원 겹침은 거리 = 반지름 합(정확히 접촉)에서도 겹침이다(U9 실측, R5의 `≤`가 엔진판과 같은 쪽). 그 밖의 실측 항목은 Core와 무관하다.
- 테스트 실행 명령(PM이 그대로 다시 돌린다. 결과 XML 경로와 로그 경로는 매번 다르게 줘도 된다):
  ```
  "C:\Program Files\Unity\Hub\Editor\6000.3.28f1\Editor\Unity.exe" -batchmode -nographics -projectPath "C:\Users\SSAFY\Desktop\borry\indiv\Survivor" -runTests -testPlatform EditMode -testResults "C:\Users\SSAFY\.claude\jobs\f32c1ccc\tmp\em-<n>.xml" -logFile "C:\Users\SSAFY\.claude\jobs\f32c1ccc\tmp\em-<n>.log"
  ```
  첫 실행은 Library 생성 때문에 수 분 걸린다. 컴파일 오류가 있으면 테스트가 0건으로 끝나고 로그에 `error CS`가 찍힌다 — 로그를 `error CS`로 grep해서 먼저 본다. 종료 코드 0 = 전부 통과, 2 = 실패 있음, 3 = 실행 자체 실패. 결과 XML의 `total`·`passed`·`failed` 속성을 읽는다.
- 임시 파일은 `C:\Users\SSAFY\.claude\jobs\f32c1ccc\tmp\` 아래에 둔다. 프로젝트 안에 임시 파일을 만들지 않는다.
- 이 프로젝트의 문서 파일은 LF 개행·UTF-8이다. 새 C# 파일도 LF·UTF-8(BOM 없음)로 만든다. asmdef는 JSON이다.

## 2. 배타적 파일 소유권

### 당신이 배타적으로 소유하는 경로 (이 밖에는 어떤 파일도 만들거나 고치지 않는다)

1. `Survivor/Assets/Scripts/Core/**` — `tech/5c-ownership-contracts.md` 5c.2 "IMP-CORE" 행의 파일 전부. 그 행이 파일 목록의 원본이다. 행에 있는 파일은 전부 만들고, 행에 없는 파일을 Core에 추가해야 했다면 보고에 이유를 적는다(추가 자체는 허용, 단 클래스명은 프로젝트 전체에서 고유).
2. `Survivor/Assets/Tests/EditMode/Core/**` — Core 테스트.
3. `Survivor/Assets/Tests/EditMode/Survivor.Tests.EditMode.asmdef` — 당신이 처음 만든다(PM 기본값 2). 참조: `Survivor.Core`, `UnityEngine.TestRunner`, `UnityEditor.TestRunner`; `includePlatforms: ["Editor"]`; `precompiledReferences: ["nunit.framework.dll"]`; `overrideReferences: true`; `defineConstraints: ["UNITY_INCLUDE_TESTS"]`.
4. Unity가 위 경로 아래에 자동 생성하는 `.meta` 파일(손으로 만들지 않는다).

### 건드리지 않는 것

- `Survivor/Assets/Scenes/`, `Survivor/Assets/Settings/`, `Survivor/ProjectSettings/`, `Survivor/Packages/` — 사용자 소유. 패키지 추가가 필요하면 BLOCKED로 보고한다(Core에는 필요 없어야 정상).
- `Survivor/Assets/Scripts/Core/` 밖의 `Scripts/*` — 다른 웨이브 구현자 소유. **스텁도 만들지 않는다.** Core는 아무것도 참조하지 않으므로 필요할 일이 없다.
- `indiv/` 루트의 문서 전부(`spec/`, `tech/`, `DECISIONS.md`, `RUN_LOG.md`, `CLAUDE.md` 등) — 읽기만. 문서 결함을 찾으면 고치지 말고 보고에 적는다.
- 동시에 도는 다른 에이전트는 없다.

## 3. 검증 재현성

- `Library/`·`Temp/`·`Logs/`를 지우거나 옮기지 않는다. 배치 실행이 꼬였다고 생각되면 지우지 말고 로그를 보고한다.
- 패키지 설치·`Packages/manifest.json` 수정은 승인 게이트라 할 수 없다. 필요하면 BLOCKED로 분류하고 이유만 쓴다.
- 디렉터리 삭제(`rm -rf`)는 승인 게이트다. 파일 단위로만 지운다. `git commit`·`push`·`reset`·`rm`도 하지 않는다(PM이 한다).
- **당신이 보고하는 수치는 PM이 위 명령을 그대로 돌려 같은 값이 나와야 한다.** 테스트 수·통과 수·실패 0을 보고할 때는 마지막 실행의 XML 경로와 함께 적는다.

## 4. 결정성

- 테스트에 `UnityEngine.Random`, `System.Random`(시드 없는), `DateTime.Now`, `Time.*`를 쓰지 않는다. 난수는 프로젝트의 `DeterministicRng`에 고정 시드를 준다.
- 고정 시간 대기(`Thread.Sleep`)를 테스트에 넣지 않는다. Core는 비동기가 없으므로 필요할 일도 없다.
- **합격 기준은 "EditMode 전체 스위트 연속 3회 전회 동일(총 N건, 통과 N건, 실패 0)"이다.** 3회 결과를 요약하지 말고 세 줄 그대로 보고한다.
- 분포 테스트(5c.5 "RNG 분포")는 시드를 고정하고 허용 오차를 문서값(예: ±0.4%p)으로 둔다. 시드가 같으면 결과가 같으므로 "가끔 실패"가 나오면 그것은 테스트 결함이지 운이 아니다.

## 5. 기준선 — 정상 동작인데 결함으로 오판하기 쉬운 것

번호마다 근거 문서 위치를 적었다. 반박할 것이 있으면 바꾸지 말고 보고에 "반박" 항목으로 올린다.

1. **R5 겹침은 경계 포함(`≤`)이다.** 접촉(거리 = 반지름 합)도 겹침. 근거: `tech/5a-shared-rules.md` R5, 웨이브 0 실측 U9(엔진 Box2D도 같은 판정). `<`로 "고치지" 말 것.
2. **R1은 두 함수로 갈라진다.** `SecondsToIntervalSteps`는 최소 1(쿨다운·수명·간격류), `SecondsToSteps`는 0 허용(무적 시간·스폰 시작/끝). 둘 다 `Math.Round(seconds × 60, MidpointRounding.AwayFromZero)`, 음수·NaN·∞는 `ArgumentOutOfRangeException`. 근거: 5a R1. 어느 쪽이 어느 값을 받는지는 R1 표의 "적용 대상" 그대로.
3. **R17 대각 성분은 리터럴 `0.70710677f`다.** `Vector2.Normalize`로 계산하지 않는다(float 결과가 플랫폼·최적화에 따라 비트가 다를 수 있어 결정론을 깨뜨린다). 영벡터 → `DirectionOrDefault`가 `Vector2.right`. 근거: 5a R17.
4. **R6 타이브레이크는 제곱거리 float 비교 → 같으면 `EntityId` 작은 쪽(Index, 그다음 Generation).** 제곱근을 쓰지 않는다. 근거: 5a R6, R10.
5. **R20 상수는 문서값 그대로이고 `const`다.** `LargeMonsterRadiusThreshold = 1.0f`·`MinTimeScale = 0.25f`·`MaxTimeScale = 3f`·`MeasurementWarmupSteps = 300`·`MonsterLimitFpsThreshold = 55f`는 [기본값]이지만 **이 웨이브에서는 바꾸지 않는다.** `MonsterCapDefault = 400`은 보스 제외 상한이라는 의미 주석을 단다. 근거: 5a R20.
6. **`EntityId` 세대 유일성은 한 판 안에서만 보장된다.** 판 리셋에서 할당기를 초기화하면 세대가 1로 돌아간다. 이것은 결정론(같은 시드 재실행의 ID 동일성)을 위한 의도이지 버그가 아니다. 근거: `tech/06-open-questions.md` D26, `spec/00-common.md` 3.0 H, `tech/5.01-entity-id.md`.
7. **`EntityId.ToString()`은 `"{Index}:{Generation}"`**이고 CSV·로그·기록 전부 이 형식이다. 근거: 5a R15.
8. **Core는 로그를 남기지 않는다.** 로그 형식(`[HH:mm:ss.fff] [LEVEL] [Area] message`)은 Simulation 이상의 계층 것이다. Core에 `Debug.Log`를 넣지 않는다. 예외 규약만 지킨다: 계약 위반은 `InvalidOperationException`, 잘못된 인자는 `ArgumentOutOfRangeException`/`ArgumentException`, 예상 가능한 실패는 `bool Try...`. 근거: `tech/5c-ownership-contracts.md` 5c.3 #7, `CODING_GUIDE.md` §4.
9. **Core는 다른 어셈블리를 참조하지 않는다.** `Survivor.Core.asmdef`의 `references`는 빈 배열이다. 허용되는 것은 UnityEngine 모듈(`Vector2`, `Mathf`, `Unity.Profiling.ProfilerMarker`)뿐. Input System·URP·Newtonsoft·`UnityEditor`를 쓰면 참조 방향 위반이다. 근거: 5c.1 참조 방향 표.
10. **시간은 스텝 정수(`long`), 속도는 스텝당 값.** Core 코드에 초 단위 상수·`Time.*`가 나타나면 안 된다(R1·R3의 환산 함수 자체는 예외 — 그것이 유일한 환산 지점이다). 근거: 5c.3 #4, `tech/02-feature-tac.md` TAC-0-2.
11. **`StepMath`에는 스텝 시계 상수가 있다**: 문서(5.03)의 `MaxCatchUpSteps = 3`, `StepSeconds = 1f/60f`(이름은 5.03의 것을 따른다). 배속에 따른 `Time.maximumDeltaTime` 갱신 규칙(D9)은 LOOP·DEV의 일이고 Core는 상수만 제공한다.
12. **R12 `UpgradeKind` 열거형 값 순서**는 WeaponUpgrade, WeaponNew, PassiveUpgrade, PassiveNew, Heal 이고 비교는 열거형 값 오름차순 → `TargetId` `string.CompareOrdinal`. `UpgradeKind`·`UpgradeOption`은 (8회차) Core로 옮겨졌다 — `tech/5.14-progression.md`의 `namespace Survivor.Core` 코드 블록이 정의 위치다.
13. **`SimulationRules`·`ISpawnPolicy`·`StrategySelection`의 정의 전체는 `tech/5.15-dev-mode.md` 첫 코드 블록(`namespace Survivor.Core`)에 있다.** 5.15의 나머지(개발자 모드 UI·측정)는 읽지 않아도 된다.
14. **★ 표시와 "(N회차)" 표기는 문서 이력 표기다.** 코드에 옮기지 않는다. 다만 ★가 붙은 이름·시그니처는 최근에 확정된 것이라 특히 글자 그대로 쓴다.
15. **테스트 수의 기준**: 5c.5 "공유 규칙 R1~R30 경계값 표"는 Core 소유 규칙(R1~R10, R12, R13, R15~R20, R22~R26, R30)에 대해서만 당신 몫이다. R11·R14·R21·R27~R29는 다른 구현자 소유라 테스트하지 않는다(인터페이스·타입이 Core에 있으면 컴파일만 되면 된다).

## 6. 금지

- 시크릿 파일(`.env*`, `env.js`) 열람 금지(permissions.deny로도 막혀 있다).
- `git commit`/`tag`/`push`/`reset`/`rm`, 패키지 설치, `rm -rf` = 승인 게이트. 시도하지 않는다.
- **단언을 느슨하게 고쳐 초록을 만드는 것은 실패로 간주한다.** 문서값과 코드가 다르면 코드를 고치거나 "반박"으로 올린다.
- 문서의 공개 시그니처·이름·네임스페이스·열거형 값 순서를 바꾸지 않는다. 바꿔야 한다고 믿으면 "반박"으로 올리고 문서대로 구현해 둔다.
- 주석 언어: 코드·개발자 주석은 **영어 산문**, 공개 멤버에는 한 줄 요약 주석(`CODING_GUIDE.md` §3). 코드에 실재하는 한국어 문자열의 인용은 원문 유지. Core에는 사용자에게 보이는 문자열이 없어야 정상이다(예외 메시지는 개발자용이므로 영어).
- 네이밍: `CODING_GUIDE.md` §2.2(PascalCase, 인터페이스 `I`, private 필드 `_camelCase`, `const`도 PascalCase, 파일명 = 주 클래스명).
- 파일 하나에 여러 public 타입을 넣지 않는다(5c.2의 파일 = 타입 대응을 지킨다. 예외는 5c.2가 괄호로 함께 적은 것 — `ColliderActivationPolicy.cs`(+`PhysicsBodyRequirements`), `UpgradeOption.cs`(`UpgradeKind` 포함), `CollisionEnums.cs`, `StrategyKinds.cs` 같은 열거형 묶음).
- 보고에 시크릿·긴 로그 원문을 넣지 않는다.

## 7. 보고 형식 (압축해서. PM은 원본 데이터가 아니라 결론을 받는다)

1. **만든 파일 목록**: 경로별 한 줄(무슨 타입, 어느 5.xx 절·R 번호를 구현). 5c.2 IMP-CORE 행과 대조해 **빠진 파일·추가한 파일**을 따로 적는다.
2. **테스트 결과 3회 원문**: 각 실행의 `total/passed/failed/inconclusive/skipped`와 XML 경로, 종료 코드. PM이 그대로 돌릴 명령 한 줄.
3. **컴파일·실행 환경 수치**: 첫 배치 실행 소요 시간, Library 생성 여부.
4. **계약 대조표**: 5.0.1 표의 Core 인터페이스 전부(`IPoolStrategy`, `ITargetProvider`, `ITickStrategy`, `IUpdatable`, `ICollisionStrategy`, `IInputSource`, `ISpawnPolicy`, `ISimulationCommands`, `IStepPhaseHandler`, `IStepClockReader`, `IEntityTemplateDecorator`, `IPhysicsBodyRegistry`)에 대해 "문서 시그니처 그대로 / 차이 있음(무엇)" 한 줄씩.
5. **반박**: PM 기준선이나 문서가 틀렸다고 믿는 항목. 번호·근거·당신이 한 처리(문서대로 구현했는지).
6. **읽을 목록 밖 파일을 열었다면** 어떤 파일을 왜 — 목록의 결함이다.
7. **확신하지 못하는 것** — 추측하지 말고 미확정으로 올린다. (마지막 항목은 항상 이것)

## 8. 읽을 문서 (이 목록 밖의 기획 문서는 열지 말 것. 열어야 했다면 7-6에 적을 것)

전원 공통:
- `spec/00-common.md` (3.0 공통 규칙 — 특히 F 고정 스텝, H 객체 ID, I 피해)
- `tech/README.md` (색인만)
- `tech/02-feature-tac.md` — **3.0 절(TAC-0-1~0-10)만**. 기능별 TAC는 뒤 웨이브 것.
- `tech/5a-shared-rules.md` (R1~R30 전부 — Core 소유 규칙의 정의)
- `tech/5c-ownership-contracts.md` (5c.1 폴더·참조 방향, 5c.2 IMP-CORE 행, 5c.3 계약 요약, 5c.5 테스트 전략)

IMP-CORE 필수(5c.2b IMP-CORE 행 그대로):
- `tech/5.00-assemblies-interfaces.md`
- `tech/5.01-entity-id.md`
- `tech/5.02-rng.md`
- `tech/5.03-step-clock.md` (Core 계약 부분. `SimulationClock` 구현은 LOOP)
- `tech/5.04-commands-spawn.md`
- `tech/5.05-input.md`
- `tech/5.06-pool.md`
- `tech/5.07-targeting.md`
- `tech/5.08-tick.md`
- `tech/5.09-collision.md` (Core 계약 부분: `ICollisionStrategy`, `CollisionBody`, `CollisionEvent`, 채널 표, `IPhysicsBodyRegistry`, `ColliderActivationPolicy`)
- `tech/5.15-dev-mode.md` — **첫 코드 블록(`namespace Survivor.Core`)만**
- `tech/5.14-progression.md` — **`namespace Survivor.Core` 코드 블록만**

코드 규칙: `CODING_GUIDE.md` §1~§4, §7.

주지 않는 것: `RUN_LOG.md`(실행 일지), `DECISIONS.md`(결정 배경이 꼭 필요하면 "객체 ID" 주제 절과 D26만), `spec/F*.md`(기능 명세는 뒤 웨이브 것), `tech/01-overview.md`, `tech/06-open-questions.md`(기준선 6번에 필요한 D26 문장은 위에 옮겨 두었다).

## 작업 순서 제안

1. 위 문서를 읽는다(필요한 절만). 5c.2 IMP-CORE 행에서 파일 목록을 뽑아 체크리스트로 만든다.
2. `Survivor.Core.asmdef`(참조 없음, `autoReferenced: true`, `noEngineReferences: false`)와 테스트 asmdef를 먼저 만들고 빈 상태로 배치 실행 1회 — 컴파일 파이프라인이 도는지 확인한다(이때 Library가 생성된다).
3. 타입을 구현한다. 문서의 코드 블록이 있으면 그 시그니처를 그대로 옮긴다.
4. 5c.5 기준으로 EditMode 테스트를 쓴다. 테스트 클래스는 규칙 번호를 이름에 넣는다(예: `R05_CircleOverlapTests`).
5. 배치 실행 → 빨강이면 코드를 고친다(단언을 고치지 않는다) → 초록이 되면 **연속 3회** 돌려 7-2 형식으로 기록한다.
6. 보고를 쓴다.

=== 프롬프트 끝 ===
