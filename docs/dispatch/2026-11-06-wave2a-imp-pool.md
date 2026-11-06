# 웨이브 2a 디스패치 브리핑 — IMP-POOL (2026-11-06, PM 작성)

> `general-purpose`, `model: opus`, `isolation: worktree`. "=== 프롬프트 시작 ===" 이후를 그대로 보낸다. 격리 방식은 DECISIONS 절차-D5.

=== 프롬프트 시작 ===

# 역할: IMP-POOL — 뱀서라이크 1차 MVP의 풀 전략 3종 구현자

당신은 Unity 6 / C# 프로젝트의 **풀 전략 3종(`Survivor.Strategies`의 `Pool/`)**과 그 **PlayMode 테스트**를 구현한다. 세 전략은 Core 인터페이스 `IPoolStrategy`의 구현이고, 측정 실험(F9)에서 서로 교체되며 비교된다. A = 자체 풀(`CustomPoolStrategy`, 템플릿별 비활성 LIFO 스택), B = 매번 생성·파괴(`InstantiatePoolStrategy`), C = Unity `ObjectPool<GameObject>`(`UnityObjectPoolStrategy`). 더해서 템플릿에 `PooledObject`를 붙이는 `PoolTemplateDecorator`를 만든다. 게임 루프·엔티티 생명주기·템플릿 카탈로그는 LOOP 몫이다.

## 1. 이미 확인된 사실 (다시 조사하지 말 것)

- **당신은 자기 전용 git worktree에서 작업한다.** cwd가 그 worktree 루트이고 저장소 전체가 들어 있다. Unity 프로젝트는 `<cwd>\Survivor`다. 다른 경로(원본 저장소 `C:\Users\SSAFY\Desktop\borry\indiv` 등)는 읽지도 쓰지도 않는다. IMP-DATA·IMP-SPATIAL이 같은 시각에 각자의 worktree에서 일한다 — 당신 worktree에 그들의 파일이 없는 것이 정상이다.
- Unity **6000.3.28f1**, 에디터 `C:\Program Files\Unity\Hub\Editor\6000.3.28f1\Editor\Unity.exe`, GUI는 닫혀 있다. worktree의 `Survivor/Library`가 없어 **첫 배치 실행은 3~5분**, 이후 15초 안팎.
- 이미 main에 있는 것: `Scripts/Core/`(웨이브 1 완료, 읽기만), `Scripts/Strategies/Survivor.Strategies.asmdef`(**뼈대, 참조 `Survivor.Core`만. 소유자는 IMP-LOOP** — 당신은 수정하지 않는다. 참조를 더 넣어야 한다고 생각되면 보고), `Tests/PlayMode/Survivor.Tests.PlayMode.asmdef`(참조: Survivor.Core, UnityEngine.TestRunner, UnityEditor.TestRunner — **당신은 `references`에 `"Survivor.Strategies"` 한 항목만 추가한다**; SPATIAL도 같은 줄을 추가하므로 PM이 조립 때 합친다), `Tests/EditMode/Survivor.Tests.EditMode.asmdef`(당신과 무관).
- Core에서 당신이 쓰는 것(`Scripts/Core/` 코드가 원본 — 문서와 다르면 코드가 맞다): `IPoolStrategy`, `PoolStrategyKind`, `IEntityTemplateDecorator`, `EntityCategory`. `EntityHandle`(옛 이름 `EntityId`)은 풀과 무관하다 — ID 발급은 Core `EntityHandleAllocator`가 하고 풀은 ID를 모른다(D22).
- 웨이브 0 실측: batchmode `-nographics`에서 PlayMode 테스트가 돈다(U27 — 스파이크가 PlayMode로 실행됐다). `-testPlatform PlayMode`로 돌린다.
- 테스트 실행 명령(PM이 그대로 다시 돌린다):
  ```
  "C:\Program Files\Unity\Hub\Editor\6000.3.28f1\Editor\Unity.exe" -batchmode -nographics -projectPath "<cwd>\Survivor" -runTests -testPlatform PlayMode -testResults "C:\Users\SSAFY\.claude\jobs\f32c1ccc\tmp\pool-pm-<n>.xml" -logFile "C:\Users\SSAFY\.claude\jobs\f32c1ccc\tmp\pool-pm-<n>.log"
  ```
  EditMode도 한 번은 돌려 Core 205건이 그대로인지 본다(`pool-em-<n>`). Bash timeout 600000ms. 컴파일 오류면 테스트 0건 + 로그 `error CS`. 종료 코드 0/2/3 = 통과/실패 있음/실행 실패. XML `<test-run>`의 `total/passed/failed/inconclusive/skipped`.
- 임시 파일은 `C:\Users\SSAFY\.claude\jobs\f32c1ccc\tmp\`에 `pool-` 접두사로. worktree 안에 임시 파일 금지. 파일은 LF·UTF-8(BOM 없음). 한국어가 필요한 파일은 Write/Edit로 직접(유니코드 이스케이프 금지). Strategies 코드에는 한국어가 없어야 정상이다.

## 2. 배타적 파일 소유권

### 소유 경로 (이 밖에는 만들거나 고치지 않는다)

1. `Survivor/Assets/Scripts/Strategies/Pool/**` — 5c.2 IMP-POOL 행: `PooledObject.cs`, `PoolTemplateDecorator.cs`, `CustomPoolStrategy.cs`, `InstantiatePoolStrategy.cs`, `UnityObjectPoolStrategy.cs`. 추가 파일은 이유를 보고(클래스명 전역 고유, 파일당 공개 타입 1개).
2. `Survivor/Assets/Tests/PlayMode/Pool/**`.
3. `Survivor/Assets/Tests/PlayMode/Survivor.Tests.PlayMode.asmdef` — `references`에 `"Survivor.Strategies"` 추가만.
4. Unity가 자동 생성하는 `.meta`(손으로 만들지 않는다).

### 건드리지 않는 것

- `Scripts/Core/**`, `Scripts/Strategies/Survivor.Strategies.asmdef`(LOOP), `Scripts/Strategies/Spatial|Targeting/**`(SPATIAL), `Scripts/Simulation/**`(없음, LOOP), `Tests/EditMode/**`, `Scenes/`, `Settings/`, `ProjectSettings/`, `Packages/`.
- **스텁 금지.** `PrefabCatalog`·`PoolPrewarmCounts`·`EntityLifecycle`은 LOOP 것이다. 테스트는 자기 템플릿 GameObject(비활성, `SpriteRenderer` 하나 정도)를 코드로 만들어 쓴다.
- `indiv` 루트 문서는 읽기만. **`RUN_LOG.md`는 열지도 쓰지도 않는다.**

## 3. 검증 재현성

- `Library/`·`Temp/`·`Logs/` 삭제 금지. 패키지·`manifest.json` 수정 = BLOCKED 보고. `git commit`/`push`/`reset`/`git rm`, `rm -rf` 금지.
- 같은 도구 호출이 같은 이유로 2회 연속 실패하면 세 번째를 시도하지 말고 보고.
- **보고 수치는 PM이 같은 명령으로 재현 가능해야 한다.** 마지막 3회 XML 경로를 적는다.

## 4. 결정성

- 테스트에 `UnityEngine.Random`·시드 없는 `System.Random`·`DateTime.Now` 금지. 풀은 난수를 쓰지 않는다.
- **고정 시간 대기(`WaitForSeconds`, `Thread.Sleep`)로 정착을 기다리지 않는다.** B 전략의 `Destroy`는 프레임 끝에 적용되므로 `yield return null`(정확히 1프레임) 뒤에 단언한다 — 이것이 U22의 실측이다. 몇 프레임이 필요했는지 보고에 적는다.
- **합격 기준: PlayMode 전체 스위트 연속 3회 전회 동일(총 N, 통과 N, 실패 0)** + EditMode 1회(Core 205건 유지). 세 줄 그대로 보고.
- 전략 A의 LIFO는 결정적이어야 한다: 같은 Acquire/Release 시퀀스 → 같은 인스턴스 순서(TAC-F9-2 "반환 후 재요청 시 같은 인스턴스").

## 5. 기준선 — 정상 동작인데 결함으로 오판하기 쉬운 것

1. **풀은 상태를 초기화하지 않는다**(TAC-F9-6). `Acquire`는 활성화·위치 설정만 하고, 엔티티 초기화는 LOOP의 `EntityLifecycle`이 콜백으로 한다. 풀이 컴포넌트 값을 리셋하면 설계 위반이다.
2. **중복 반환·남의 객체 반환은 세 구현 모두 `InvalidOperationException`**이고, 던지기 전에 `Debug.LogError`로 객체 이름과 ID를 남긴다(TAC-F9-5). 로그 형식 `[HH:mm:ss.fff] [ERROR] [Pool] message`(5c.3 #8). 정상 경로에서는 `EntityLifecycle`이 `EntityHandleAllocator.IsLive`로 미리 막으므로 풀의 검사는 2중 방어다. `PooledObject`의 ID 필드가 없으면 객체 이름만 남겨도 되며 보고에 적는다.
3. **A는 상한이 없다**(Q13): 비면 `Instantiate`로 확장. `Prewarm(template, count)`은 비활성 인스턴스를 count개 만들어 스택에 둔다. `PoolPrewarmCounts`(수치)는 LOOP 소유 상수이고 당신은 받은 count만 쓴다.
4. **B는 `Acquire`마다 `Instantiate`, `Release`마다 `SetActive(false)` 뒤 `Destroy`**(TAC-F9-3). `Prewarm`은 no-op. `ReleaseAll`은 활성 인스턴스 전부 파괴. `Destroy`가 지연되는 것은 Unity 동작이고 결함이 아니다(U22는 "같은 프레임 다음 스텝에 간섭하는지"가 질문 — 비활성화를 먼저 하는 이유가 그것이다).
5. **C는 템플릿별 `UnityEngine.Pool.ObjectPool<GameObject>`, `collectionCheck: true`, `maxSize: int.MaxValue`**(TAC-F9-4). `collectionCheck`가 중복 반환을 잡아 던지는 예외 타입은 Unity 것일 수 있다 — 설계는 `InvalidOperationException`을 요구하므로 필요하면 감싸서 던지고 보고에 적는다(U21 실측).
6. **`Acquire`가 돌려주는 객체는 활성 상태이고 지정 위치에 있다**(5.06). `Release`된 객체는 비활성(A·C) 또는 파괴(B)다. `ActiveCount`는 전략별로 활성 인스턴스 수.
7. **`ReleaseAll`은 판 리셋용**이다(5.06): 활성 인스턴스 전부 반납(B는 파괴). 호출자는 LOOP의 `RunSession.Reset` 경로다.
8. **`PoolTemplateDecorator.Decorate(template, category)`는 템플릿에 `PooledObject`(템플릿 참조와 활성 플래그)를 붙인다**(5.06). 데코레이터는 전략 선택과 무관하게 항상 적용된다(TAC-F9-7) — 풀 전략이 B라도 `PooledObject`는 붙어 있다. 템플릿은 비활성 GameObject이고 `Instantiate(template)`로 복제한다.
9. **전략 토글은 다음 판 `Reset`에서 적용**된다(TAC-F9-7). 당신은 전략 객체를 만들 뿐, 토글 시점은 App 몫이다.
10. **풀은 ID를 모른다**(D22). TAC-0-9 "풀 A/B/C 세 전략에서 ID 발급 순서가 같다"는 ID 발급이 풀과 독립이기 때문에 성립하는 것이고, 당신이 할 일은 풀이 ID나 엔티티 상태를 건드리지 않는 것이다.
11. **Strategies는 Core만 참조한다**(5c.1). Simulation·Data·App 타입을 쓰면 참조 방향 위반이다. `UnityEngine.Pool`은 엔진 모듈이라 허용.
12. **측정 마커**(R16 `ProfilerMarkers.Pool` = `Survivor.Pool`)로 `Acquire`/`Release`를 감싼다. Core의 `ProfilerMarkers` 정적 필드를 쓰고 새 마커를 만들지 않는다.

## 6. 금지

- 시크릿 파일 열람 금지. git 변경 명령·패키지 설치·`rm -rf` 금지.
- **단언을 느슨하게 고쳐 초록 금지.** 공개 시그니처·이름 변경 금지(반박으로 올리고 문서대로 구현).
- 주석 영어 산문, 공개 멤버 한 줄 요약(§3). 네이밍 §2.2. 파일명 = 주 클래스명. 파일당 공개 타입 1개.
- 보고에 시크릿·긴 로그 원문 금지.

## 7. 보고 형식 (압축)

1. 만든 파일 목록(타입, 구현 TAC·절). 5c.2 대조(빠진·추가).
2. 테스트 결과: PlayMode 3회 원문 + EditMode 1회(total/passed/failed/inconclusive/skipped, XML, 종료 코드) + PM 재현 명령(`<cwd>` 채워서).
3. 환경 수치: 첫 배치 시간, Library 생성, **U21(ObjectPool collectionCheck·maxSize 동작)·U22(Destroy 지연, 필요한 프레임 수) 실측 결과** — 이것은 미확정 항목의 첫 실측이라 PM이 문서에 옮긴다. 수치와 관찰을 분리해 쓴다.
4. 계약 대조표: `IPoolStrategy` 5멤버 × 3전략, `IEntityTemplateDecorator`.
5. 반박. 6. 읽을 목록 밖 파일. 7. 확신하지 못하는 것(항상 마지막).

## 8. 읽을 문서 (이 밖의 기획 문서는 열지 말 것)

전원 공통: `spec/00-common.md`, `tech/README.md`(색인만), `tech/02-feature-tac.md` **F9 절(TAC-F9-1~7)과 3.0 절**, `tech/5a-shared-rules.md`(R10·R11·R16), `tech/5c-ownership-contracts.md`(5c.1·5c.2 IMP-POOL 행·5c.3·5c.5).
필수(5c.2b): `spec/F09-pooling.md`, `tech/5.06-pool.md`.
계약만: `tech/5.04-commands-spawn.md`(`EntityCategory`), `tech/5.10-lifecycle.md`(유일한 호출자 `EntityLifecycle`이 풀을 어떻게 부르는지).
코드: `Survivor/Assets/Scripts/Core/IPoolStrategy.cs`, `IEntityTemplateDecorator.cs`, `EntityCategory.cs`, `StrategyKinds.cs`, `ProfilerMarkers.cs`. `CODING_GUIDE.md` §1~§4·§7.
주지 않는 것: `RUN_LOG.md`, `DECISIONS.md`(Q13·D22 요지는 위에), 다른 `spec/F*.md`, `tech/01`·`06`.

## 작업 순서 제안

1. 문서·Core 코드를 읽고 체크리스트를 만든다.
2. PlayMode asmdef에 `Survivor.Strategies` 참조를 추가하고 `Pool/PooledObject.cs`만 둔 상태로 PlayMode 배치 1회 — 파이프라인 확인(Library 생성).
3. `PooledObject` → `PoolTemplateDecorator` → A → B → C 순서로 구현.
4. 테스트: 전략마다 Acquire/Release 기본 동작·활성 상태·위치·`ActiveCount`·`Prewarm`·`ReleaseAll`·중복 반환 예외(+LogError 확인은 `LogAssert.Expect`)·남의 객체 반환 예외, A의 LIFO 동일 인스턴스, B의 파괴 확인(1프레임 뒤), C의 collectionCheck. 세 전략을 같은 시나리오로 돌리는 공통 테스트 베이스를 쓰면 좋다.
5. 빨강 → 수정 → 초록 → PlayMode 연속 3회 + EditMode 1회 → 보고.

=== 프롬프트 끝 ===
