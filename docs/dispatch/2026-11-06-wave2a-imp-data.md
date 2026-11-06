# 웨이브 2a 디스패치 브리핑 — IMP-DATA (2026-11-06, PM 작성)

> `general-purpose`, `model: opus`, `isolation: worktree`. "=== 프롬프트 시작 ===" 이후를 그대로 보낸다. 격리 방식은 DECISIONS 절차-D5.

=== 프롬프트 시작 ===

# 역할: IMP-DATA — 뱀서라이크 1차 MVP의 Data 계층 구현자

당신은 Unity 6 / C# 프로젝트의 **Data 어셈블리(`Survivor.Data`)**, **게임 데이터 JSON 7종**, 그 **EditMode 테스트**를 구현한다. Data는 StreamingAssets의 JSON을 Newtonsoft로 파싱해 정의 객체로 바인딩·검증하고(`DataValidationReport`), 레지스트리(`GameDataRegistry`)로 노출하며, 터널링 불변식과 플레이 가능성을 검사한다. 게임 루프·엔티티·UI는 다른 구현자 몫이다. 당신 코드는 뒤 웨이브(LOOP·PLAYER·MONSTER·FLOW·DEV)가 글자 그대로 의존하는 계약이므로 문서의 시그니처·이름을 바꾸지 않는 것이 가장 중요하다.

## 1. 이미 확인된 사실 (다시 조사하지 말 것)

- **당신은 자기 전용 git worktree에서 작업한다.** 현재 작업 디렉터리(cwd)가 그 worktree의 루트이며 저장소 전체(문서 + `Survivor/`)가 들어 있다. Unity 프로젝트는 `<cwd>\Survivor`다. 다른 경로(원본 저장소 `C:\Users\SSAFY\Desktop\borry\indiv` 등)의 파일은 읽지도 쓰지도 않는다. 다른 구현자 둘(IMP-POOL·IMP-SPATIAL)이 같은 시각에 각자의 worktree에서 일하고 있다 — 당신 worktree에는 그들의 파일이 없는 것이 정상이다.
- Unity **6000.3.28f1**, 에디터: `C:\Program Files\Unity\Hub\Editor\6000.3.28f1\Editor\Unity.exe`. GUI 에디터는 닫혀 있다. 당신 worktree의 `Survivor/Library`는 없으므로 **첫 배치 실행은 3~5분** 걸린다(패키지 캐시는 전역이라 네트워크 없이 된다). 이후는 15초 안팎.
- 패키지: `com.unity.nuget.newtonsoft-json`(Newtonsoft.Json)이 `Packages/manifest.json`에 들어 있다. `com.unity.test-framework` 1.6.0. Newtonsoft 패키지 어셈블리는 autoReferenced라 asmdef에서 따로 참조하지 않아도 보이지만, 5c.1 표의 "외부 패키지" 열에 맞춰 Data asmdef에 명시 참조를 두어도 된다 — 컴파일되는 쪽을 쓰고 보고에 적는다.
- 이미 main에 있는 것: `Survivor/Assets/Scripts/Core/`(웨이브 1 완료, `Survivor.Core` — 당신은 읽기만), `Survivor/Assets/Tests/EditMode/Survivor.Tests.EditMode.asmdef`(참조: Survivor.Core, UnityEngine.TestRunner, UnityEditor.TestRunner — **당신은 여기에 `Survivor.Data` 참조 한 줄만 추가한다**, 다른 구현자도 자기 참조를 추가하므로 PM이 조립 때 합친다), `Scripts/Strategies/Survivor.Strategies.asmdef`·`Tests/PlayMode/` 뼈대(당신과 무관).
- `Survivor/Assets/StreamingAssets/`는 아직 없다 — 당신이 `StreamingAssets/Data/` 폴더와 JSON 7개를 만든다.
- Core에서 당신이 쓰는 것: `StepMath.SecondsToSteps(double)`·`SecondsToIntervalSteps(double)`(R1 — **Data 로더가 유일한 호출자**), `CollisionLayer`(5.09 열거형, `MovingObjectSpec.Layer`가 직접 사용), `GameRules` 상수, `EntityHandle`(옛 이름 `EntityId` — 문서 13회차에서 전부 개명됨, 코드도 `EntityHandle`). Core의 공개 API는 `Survivor/Assets/Scripts/Core/*.cs`에서 직접 읽는다 — 문서와 코드가 다르면 **코드가 맞다**(13회차에서 맞췄다).
- 테스트 실행 명령(PM이 그대로 다시 돌린다. `<cwd>`는 당신 worktree 절대 경로, `<n>`은 실행 번호):
  ```
  "C:\Program Files\Unity\Hub\Editor\6000.3.28f1\Editor\Unity.exe" -batchmode -nographics -projectPath "<cwd>\Survivor" -runTests -testPlatform EditMode -testResults "C:\Users\SSAFY\.claude\jobs\f32c1ccc\tmp\data-em-<n>.xml" -logFile "C:\Users\SSAFY\.claude\jobs\f32c1ccc\tmp\data-em-<n>.log"
  ```
  Bash timeout은 600000ms. 컴파일 오류면 테스트 0건 + 로그에 `error CS` — 로그를 먼저 grep한다. 종료 코드 0 = 전부 통과, 2 = 실패 있음, 3 = 실행 실패. 결과 XML 루트 `<test-run>`의 `total/passed/failed/inconclusive/skipped`를 읽는다. 다른 구현자의 Unity 인스턴스가 다른 경로에서 동시에 돌 수 있다 — 정상이다(프로젝트가 다르면 잠금이 겹치지 않는다).
- 임시 파일은 `C:\Users\SSAFY\.claude\jobs\f32c1ccc\tmp\` 아래에 `data-` 접두사로 둔다. worktree 안에 임시 파일을 만들지 않는다.
- 파일은 LF·UTF-8(BOM 없음). 한국어가 들어가는 파일(JSON의 한국어 이름·설명, 테스트 픽스처)은 Write/Edit 도구로 직접 쓴다(셸 heredoc + 유니코드 이스케이프 금지 — 깨진다). CP949 픽스처처럼 **의도적으로** 다른 인코딩이어야 하는 파일은 Python 등으로 바이트를 직접 써도 되며 보고에 적는다.

## 2. 배타적 파일 소유권

### 당신이 소유하는 경로 (이 밖에는 만들거나 고치지 않는다)

1. `Survivor/Assets/Scripts/Data/**` — `tech/5c-ownership-contracts.md` 5c.2 "IMP-DATA" 행의 파일 전부(`Survivor.Data.asmdef`, `Definitions/*.cs`, `GameData.cs`, `GameDataLoader.cs`, `GameDataRegistry.cs`, `JsonTreeReader.cs`, `Binders/*Binder.cs` 7개, `DataValidationReport.cs`, `ValidationIssue.cs`, `TunnelingInvariantValidator.cs`, `MovingObjectSpec.cs`, `PlayabilityChecker.cs`, `XpCurve.cs`, `AssetKeyRegistry.cs`, `AssetKeyResolver.cs`, `VisualAsset.cs`, `DefaultShapeFactory.cs`). 행에 없는 파일을 추가해야 했다면 보고에 이유를 적는다(허용, 클래스명은 프로젝트 전체에서 고유, 파일당 공개 타입 1개 — 열거형 묶음·문서가 괄호로 함께 적은 것만 예외).
2. `Survivor/Assets/StreamingAssets/Data/*.json` 7개: `weapons.json`, `monsters.json`, `combinations.json`, `spawnTable.json`, `passives.json`, `levelUpPool.json`, `playerStats.json`. 내용은 `tech/5.13-json-schemas.md`의 스키마와 [예시값]을 따른다.
3. `Survivor/Assets/Tests/EditMode/Data/**` — 테스트와 픽스처(E3, CP949 픽스처 포함).
4. `Survivor/Assets/Tests/EditMode/Survivor.Tests.EditMode.asmdef` — **`references`에 `"Survivor.Data"` 한 항목 추가만.** 다른 필드는 건드리지 않는다.
5. Unity가 위 경로에 자동 생성하는 `.meta`(손으로 만들지 않는다).

### 건드리지 않는 것

- `Scripts/Core/**`(읽기만), `Scripts/Strategies/**`, `Tests/PlayMode/**`, `Scenes/`, `Settings/`, `ProjectSettings/`, `Packages/`(패키지 추가가 더 필요하면 BLOCKED로 보고), `Resources/`(`AssetKeyRegistry.asset`은 사용자가 에디터에서 만드는 ScriptableObject 인스턴스 — 당신은 클래스만 만들고 **테스트는 그 에셋이 없어도 돌아야 한다**).
- 다른 구현자 소유 파일의 스텁을 만들지 않는다. Data는 Core만 참조하므로 필요할 일이 없다. `SwapData` 대기 명령·`PendingCommandQueue`는 LOOP 것이다 — 당신은 `GameDataRegistry.TrySwap`(5.11.1)만 제공한다.
- `indiv` 루트 문서 전부 읽기만. **`RUN_LOG.md`는 열지도 쓰지도 않는다.** 문서 결함은 보고에 적는다.

## 3. 검증 재현성

- `Library/`·`Temp/`·`Logs/`를 지우지 않는다. 배치가 꼬였다고 생각되면 로그를 보고한다.
- 패키지 설치·`manifest.json` 수정은 승인 게이트 — BLOCKED로 보고. `git commit`/`push`/`reset`/`git rm`, `rm -rf` 금지(PM이 한다). 파일 단위 삭제만.
- 같은 도구 호출이 같은 이유로 2회 연속 실패하면 세 번째를 시도하지 말고 보고한다.
- **보고하는 수치는 PM이 위 명령을 그대로 돌려 같은 값이 나와야 한다.** 마지막 3회 실행의 XML 경로를 적는다.

## 4. 결정성

- 테스트에 `UnityEngine.Random`, 시드 없는 `System.Random`, `DateTime.Now`, `Time.*` 금지. 로더는 난수를 쓰지 않는다.
- 파일 경로는 `Path.Combine`으로, 현재 문화권에 의존하는 `float.Parse`/`ToString`은 쓰지 않는다(`CultureInfo.InvariantCulture`). 테스트 중 하나는 현재 문화권을 `tr-TR`·`de-DE`로 바꿔 숫자 파싱이 깨지지 않음을 확인한다(TAC-F11-7의 CSV 쪽은 Dev 소유라 당신 몫이 아니지만, **JSON 읽기**가 문화권에 흔들리지 않는 것은 당신 몫이다).
- **합격 기준: EditMode 전체 스위트 연속 3회 전회 동일(총 N건, 통과 N건, 실패 0).** 세 줄 그대로 보고한다. 전체 스위트에는 Core 테스트 205건이 함께 돈다 — 그것이 깨지면 당신 변경이 원인일 가능성이 크니 먼저 본다(Core 코드는 고치지 말고 보고).

## 5. 기준선 — 정상 동작인데 결함으로 오판하기 쉬운 것

1. **초 → 스텝 환산은 Data 로더가 유일한 호출자다**(5a R1). 소비자는 스텝 값만 읽는다. 원본 초 값도 덤프용으로 함께 보존한다(TAC-F11-3). 어느 필드가 최소 1(`SecondsToIntervalSteps`)이고 어느 필드가 0 허용(`SecondsToSteps`)인지는 R1 표 "적용 대상" 그대로 — `invulnerabilitySeconds`는 0 허용, `hitIntervalSeconds`는 최소 1(TAC-F11-8).
2. **파싱과 바인딩은 분리**된다: `JToken.Parse`(`JsonLoadSettings { LineInfoHandling = Load, DuplicatePropertyNameHandling = Error }`)로 트리만 만들고, 바인딩·검증은 리플렉션 없이 `Binders/*`가 손으로 한다(5.11, TAC-F11-6). `JsonConvert.DeserializeObject<T>`로 한 번에 푸는 것은 설계 위반이다.
3. **파일 단위 정책**(5.11): 필수 4종(weapons, monsters, spawnTable, playerStats)이 없거나 전체 파싱 실패 = Blocking. 선택 3종(combinations, passives, levelUpPool) = Warning + 폴백. 정의 단위 오류 = 그 정의만 건너뛰고 Warning. 단 플레이어 기본 스탯은 정의 단위 오류도 Blocking. `startingWeaponId` 무효 = 플레이어 기본 스탯 오류(Blocking) [D7 기본값]. 보스(M4) 정의 무효는 Warning만이고 시작은 허용한다(D6 기본값 — 소프트락이 되지만 PM이 결정한 것).
4. **플레이 불가 4기준**(5.11): 사용 가능한 무기 0개, 스폰 가능한 몬스터 0종(스폰 테이블이 참조하는 유효 몬스터 기준), 플레이어 기본 스탯 없음, 스폰 테이블 없음. `report.IsPlayable`이 이 넷을 판정한다.
5. **인코딩**: `File.ReadAllText(path, new UTF8Encoding(false, false))`로 읽고, 결과에 U+FFFD가 있으면 파일명과 함께 Warning "인코딩 오류 가능(UTF-8이 아닌 파일일 수 있습니다)"을 남기고 계속한다(TAC-F11-6). CP949로 저장된 픽스처가 이 경로를 검증한다. 차단이 아니다.
6. **검증 묶음 순서는 고정**이다(5.11.0): 파일별 읽기 → `JsonTreeReader` → `Binders/*` 7종 고정 순서(PlayerStats, Weapons, Passives, Monsters, SpawnTable, Combinations, LevelUpPool) → 참조 검증 → 터널링 불변식 → 플레이 가능성. 이 순서가 바뀌면 보고 메시지 순서가 달라져 테스트가 깨진다.
7. **터널링 불변식은 경고일 뿐 차단하지 않는다**(TAC-F11-4). 1단 `v/60 ≤ r`, 실패한 객체만 2단 쌍 검사. 검사 대상에 플레이어와 끌리는 젬도 넣는다(D1 기본값: 플레이어 속도는 P2 최대 단계 적용값). W2 궤도 원의 속도는 ωR + 플레이어 최대 속도(D2 기본값).
8. **`AssetKeyRegistry`는 ScriptableObject 클래스**이고 인스턴스(`Resources/AssetKeyRegistry.asset`)는 사용자가 만든다. 로더·리졸버는 레지스트리가 없거나 키가 없을 때 `DefaultShapeFactory.Circle`(런타임 64×64 `Texture2D` 원, 마젠타 틴트)로 폴백하고 Warning을 남긴다 [U20 미실측 — 당신 테스트가 EditMode에서 원 스프라이트 생성이 되는지 확인하는 첫 실측이다. 안 되면 보고].
9. **스키마의 [예시값]은 밸런스 자리표시자**다(5.13). JSON 7개에는 그 값을 그대로 쓴다. 바꾸지 않는다 — 밸런싱은 사용자 영역(Q1 열림).
10. **`GameDataRegistry.Current`는 "참조할 때마다 읽는" 소비자(스폰 테이블·플레이어 기본 스탯·레벨업 풀·조합 규칙)를 위한 것이고, 보유·활성 객체는 생성 시점 정의 참조를 보관한다**(5c.3 #10, 5.11.1). 핫 리로드 `TrySwap`은 플레이 불가면 기존 데이터를 유지하고 false를 돌려준다(TAC-F11-5). 교체 시점 조율(`SwapData` 명령, 다음 스텝 Begin)은 LOOP·DEV 몫이다.
11. **로그 형식**은 `[HH:mm:ss.fff] [LEVEL] [Data] message`(5c.3 #8, Area = Data). 검증 항목마다 `Debug.LogWarning`/`LogError` 한 줄(TAC-F11-2). 로드 성공도 한 줄(파일 수·정의 수). 스텝마다 남기는 로그는 없다(로더는 스텝 안에서 돌지 않는다).
12. **`EntityHandle`**: Data는 ID를 다루지 않는다. 정의 객체는 문자열 ID(`"W1"`, `"M3"` 같은 정의 ID)를 쓰고 `EntityHandle`은 런타임 엔티티 것이다. 혼동하지 않는다.
13. **E3 테스트**는 `GameDataLoader.LoadAll(<Survivor>/Assets/StreamingAssets)`를 실제 폴더에 대해 돌려 `TunnelingInvariantValidator.EnumerateMovingObjects` 전부가 `v/60 ≤ r`인지 단언한다(5c.5). 게임을 돌리지 않는다. EditMode에서 `Application.streamingAssetsPath`는 `<프로젝트>/Assets/StreamingAssets`를 가리킨다.
14. **R21 경험치 곡선 `XpCurve.RequiredForNext(int level, XpCurveDefinition def)`는 Data 소유 공유 규칙**이다(5a). 레벨업마다 필요량을 빼고 이월하는 쪽(`ExperienceSystem`)은 FLOW다 — 당신은 필요량 함수만.
15. **Core 코드가 원본**이다. 5.09의 `CollisionLayer`, 5.15 `namespace Survivor.Core` 블록의 `SimulationRules` 등은 이미 구현돼 있으니 `Scripts/Core/`에서 시그니처를 읽는다.

## 6. 금지

- 시크릿 파일 열람 금지. `git commit`/`tag`/`push`/`reset`/`git rm`, 패키지 설치, `rm -rf` = 승인 게이트(시도 금지).
- **단언을 느슨하게 고쳐 초록을 만드는 것은 실패**다. 문서값과 코드가 다르면 코드를 고치거나 "반박"으로 올린다.
- 공개 시그니처·이름·네임스페이스·열거형 순서를 바꾸지 않는다. 바꿔야 한다고 믿으면 "반박"으로 올리고 문서대로 구현해 둔다.
- 주석 언어: 코드·개발자 주석은 영어 산문, 공개 멤버에 한 줄 요약(`CODING_GUIDE.md` §3). **사용자에게 보이는 문자열(검증 메시지 `Message`)은 한국어**(원인 + 가능하면 해결 방법, §4). JSON의 표시 이름도 한국어(5.13).
- 네이밍 `CODING_GUIDE.md` §2.2. 파일명 = 주 클래스명.
- 에디터 전용 API(`AssetDatabase`) 금지(TAC-F11-1). `Resources.Load`는 레지스트리 에셋에만.
- 보고에 시크릿·긴 로그 원문 금지.

## 7. 보고 형식 (압축)

1. **만든 파일 목록**(경로별 한 줄: 타입, 구현한 5.xx·TAC·R). 5c.2 IMP-DATA 행과 대조한 빠진·추가한 파일. JSON 7개의 정의 수(무기 n, 몬스터 n …).
2. **테스트 결과 3회 원문**(total/passed/failed/inconclusive/skipped, XML 경로, 종료 코드) + PM 재현 명령 한 줄(`<cwd>` 절대 경로를 채워서). Data 테스트만의 건수도 따로.
3. **환경 수치**: 첫 배치 실행 시간, Library 생성 여부, Newtonsoft 참조 방식.
4. **계약 대조표**: 5.11의 공개 타입(`GameDataLoader`, `GameDataRegistry`, `DataValidationReport`, `ValidationIssue`, `TunnelingInvariantValidator`, `MovingObjectSpec`, `PlayabilityChecker`, `XpCurve`, `AssetKeyRegistry`, `AssetKeyResolver`, `VisualAsset`, `DefaultShapeFactory`, 정의 객체들)별 "문서 그대로 / 차이(무엇)".
5. **반박**: 기준선·문서가 틀렸다고 믿는 항목(번호·근거·처리).
6. **읽을 목록 밖 파일**을 열었다면 무엇을 왜.
7. **확신하지 못하는 것** — 미확정으로. (항상 마지막)

## 8. 읽을 문서 (이 밖의 기획 문서는 열지 말 것)

전원 공통: `spec/00-common.md`, `tech/README.md`(색인만), `tech/02-feature-tac.md` **F11 절(TAC-F11-1~8)과 3.0 절(TAC-0-1~10)만**, `tech/5a-shared-rules.md`, `tech/5c-ownership-contracts.md`(5c.1·5c.2 IMP-DATA 행·5c.3·5c.5).
IMP-DATA 필수(5c.2b): `spec/F11-data-driven.md`, `tech/5.11-data-loader.md`, `tech/5.13-json-schemas.md`.
계약만: `tech/5.09-collision.md`(`CollisionLayer` 정의 — 코드 `Scripts/Core/CollisionEnums.cs`를 보면 충분).
코드: `Survivor/Assets/Scripts/Core/*.cs`(Core 공개 API), `CODING_GUIDE.md` §1~§4·§7.
주지 않는 것: `RUN_LOG.md`, `DECISIONS.md`(D1·D2·D6·D7 요지는 위 기준선에 옮겨 두었다), 다른 `spec/F*.md`, `tech/01`·`06`.

## 작업 순서 제안

1. 문서를 읽고 5c.2 IMP-DATA 행 + 5.13 스키마 7종에서 체크리스트를 만든다. Core 공개 API를 코드에서 확인한다.
2. `Survivor.Data.asmdef`(참조 `Survivor.Core`(+ 필요 시 Newtonsoft 명시))와 EditMode asmdef의 `Survivor.Data` 참조를 먼저 만들고 빈 상태로 배치 실행 1회 — 컴파일 파이프라인과 Newtonsoft 가용성(U25)을 확인한다(Library 생성, 3~5분).
3. 정의 객체 → `JsonTreeReader` → 바인더 7종 → 검증(참조·터널링·플레이 가능성) → 레지스트리·`TrySwap` → 에셋 키·기본 도형 순으로 구현한다.
4. JSON 7개를 5.13 [예시값]으로 쓴다. 각 파일이 로더를 통과하는지(Blocking 0, E3 통과) 확인한다.
5. 5c.5 "JSON 로더·검증" 행(7종 × 정상/깨진 JSON/필수 누락/타입 오류/ID 중복/참조 오류/알 수 없는 키/에셋 키 없음/CP949) + E3 + 플레이 불가 4기준 + 핫 리로드 유지 테스트를 쓴다.
6. 빨강 → 코드 수정 → 초록 → **연속 3회** → 보고.

=== 프롬프트 끝 ===
