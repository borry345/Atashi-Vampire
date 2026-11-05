# 웨이브 0 실측 스파이크 안내서 (Wave0Spike)

Unity 엔진이 우리 설계가 가정한 대로 동작하는지 **14가지**(U1·U2·U2b·U23·U5·U7·U8·U9·U30·U11·U31·U32·U35·U36)를 확인하는 측정 도구입니다.
스크립트 1개를 빈 프로젝트에 넣고 Play 하면 콘솔에 답이 찍힙니다. 결과는 콘솔 로그를
복사해 `RESULT_TEMPLATE.md`에 붙여 PM에게 주면 됩니다. 이 스크립트는 **버리는 측정
도구**라 게임 프로젝트에는 넣지 않습니다.

소요 시간: 에디터 측정 약 10분, 빌드 측정(U35·U36) 약 15분.

## 0. 준비물

- Unity Hub + Unity **6000.3.28f1** (6000.3.x면 됩니다)
- 템플릿: **Universal 2D** (URP 2D)
- 추가 패키지 설치 필요 없음 (Input System 등 없어도 동작)

## 1. 프로젝트 만들기

1. Unity Hub → New project → 템플릿 **Universal 2D** 선택.
2. Project name: `Survivor`, Location: `...\borry\indiv` (결과 경로 `indiv\Survivor\`).
3. Create project. 첫 열기는 몇 분 걸립니다.

이미 `indiv\Survivor\`가 있다면 그 프로젝트를 그대로 씁니다.

## 2. 스크립트 넣기

1. 탐색기에서 `indiv\docs\spike\Wave0Spike.cs`를 복사합니다.
2. `indiv\Survivor\Assets\` 안에 `Spike` 폴더를 만들고 그 안에 붙여 넣습니다.
   (최종 경로: `Survivor\Assets\Spike\Wave0Spike.cs`)
   - PM이 배치 실행(8절)까지 쓸 거라면 `Wave0Spike.asmdef`와 `Tests` 폴더도 함께 복사합니다
     (8-1 표). 에디터 Play만 할 거라면 `.cs` 하나로 충분합니다.
3. Unity로 돌아오면 컴파일이 돕니다. **Console 창(Window → General → Console)에 빨간
   오류가 있으면 Play 하지 말고** 오류 줄 전체를 복사해 PM에게 보내 주세요(3절의 "컴파일
   오류" 항목).

## 3. 씬에 붙이고 Play

1. Hierarchy 창에서 우클릭 → Create Empty. 이름은 아무거나(예: `Spike`).
2. 그 오브젝트를 선택한 채 Inspector → Add Component → `Wave0Spike` 검색 → 추가.
   (`SpikeClock` 등 다른 Spike 컴포넌트는 직접 붙이지 않습니다 — 스크립트가 알아서 만듭니다.)
3. Console 창에서 **Clear** 를 누르고, 상단 ▶ Play.
4. 약 3~5초 뒤 콘솔에 `[SPIKE][DONE]` 줄이 나오면 끝입니다. Play를 끕니다(▶ 다시 클릭).
   - Game 뷰에 검은 상자에 글자가 겹쳐 보이는 것은 정상입니다(빌드에서 읽으라고 띄운 것).

## 4. 결과 복사

1. Console 창 오른쪽 위 검색칸에 `[SPIKE]` 를 입력합니다 → 요약 줄만 남습니다
   (`[SPIKE-TRACE]`로 시작하는 상세 줄은 빠집니다. 보고에는 요약 줄이면 충분합니다).
2. 보이는 줄들을 전부 선택(한 줄 클릭 후 Ctrl+A) → Ctrl+C.
   - Console은 한 번에 한 줄만 복사될 수 있습니다. 그 경우 `[SPIKE][SUMMARY]` 줄 하나를
     클릭하면 아래 상세 창에 RESULT 줄 12개가 한 덩어리로 보입니다. 거기서 복사하세요.
   - 또는 `[SPIKE][FILE]` 줄에 적힌 경로의 `Wave0Spike_result.txt`를 열어 전체를 복사해도
     됩니다(보통 `C:\Users\<이름>\AppData\LocalLow\DefaultCompany\Survivor\`).
3. `RESULT_TEMPLATE.md`를 복사해 새 파일(예: `RESULT_2026-11-05.md`)을 만들고, 맨 아래
   "로그 붙여넣기" 칸에 붙입니다. 그 다음 각 U번호 줄의 판정을 로그의 `RESULT=` 값대로
   체크합니다.

## 5. 각 줄이 뜻하는 것 (판정은 로그의 RESULT= 값을 그대로 옮기면 됩니다)

| 번호 | 무엇을 재는가 | YES의 뜻 |
|---|---|---|
| U1 | `DefaultExecutionOrder`가 다른 두 스크립트의 `FixedUpdate` 호출 순서 | 속성값 순서대로 불림. `observed_sequences`는 같은 클래스 안의 인스턴스 순서(관찰값일 뿐) |
| U2 | `captureDeltaTime=0.1`로 프레임을 길게 만들었을 때 한 프레임의 `FixedUpdate` 횟수 | 최대 정확히 3번. 실측 결과 captureDeltaTime은 `maximumDeltaTime` 클램프를 우회해 6이 나옴 — 진짜 답은 U2b |
| U2b | `Thread.Sleep(100)`으로 **실제** 0.1초 정지 프레임을 5번 만든 뒤 프레임당 `FixedUpdate` 횟수와 `deltaTime` | 정지 뒤 프레임마다 최대 3번이고 `deltaTime ≤ 0.05`. `per_frame`·`deltaTime` 배열 그대로 보고 |
| U23 | `timeScale=3`, `targetFrameRate=60`에서 60프레임 동안 프레임당 `FixedUpdate` 횟수 | 최대 3 이하, 평균 2.5~3.0. 에디터 Game 뷰·vsync 영향이 있는 관찰값 |
| U5 | Kinematic-Kinematic 트리거 콜라이더가 Enter/Exit 콜백을 주는가 | 둘 다 옴. `NOTE: NeverSleep에서만 동작`이 있으면 그것도 그대로 보고 |
| U7 | 콜백이 `Physics2D.Simulate()` 호출 **안에서** 바로 오는가 | 같은 스텝, Simulate 안에서 옴 |
| U8 | 겹친 상태에서 콜라이더를 끄면 Exit이 **안** 오는가 | 안 옴(설계가 기대하는 결과. 안 오는 게 정상) |
| U9 | 반지름 합과 거리가 거의 같은 경계 배치에서 Enter가 오는가 | 0.9999는 옴, 1.0001은 안 옴. `d=1`(정확히 경계)은 관찰값만 |
| U30 | `defaultContactOffset`을 0.0001로 낮출 수 있는가 | 읽어보니 그 값 |
| U11 | `MovePosition` 뒤 `OverlapCircle`이 새 위치를 언제 보는가 | Simulate 뒤에 보임. `before_simulate` 값도 중요하니 그대로 |
| U31 | `FixedUpdate` 도중 만든/켠 컴포넌트가 같은 프레임의 다음 스텝부터 불리는가 | 같은 프레임 2번째 스텝부터 불림 |
| U32 | `rb.position` 대입 직후 Simulate 없이 `OverlapCircle`이 찾는가 | 아무 것도 안 해도 찾음. `after_SyncTransforms`·`after_Simulate` 값도 그대로 |
| U35 | 작업 영역 크기와 `SetResolution(1280,720)` 뒤 창 크기 | **빌드에서만 의미 있음**(6절). 에디터에서는 UNKNOWN이 정상 |
| U36 | Allow Fullscreen Switch 끔이 Alt+Enter를 막는가 | **빌드에서 손으로 확인**(6절). 스크립트는 UNKNOWN을 찍고 변화만 감시 |

에디터 Play만으로 U1~U32(U2b·U23 포함 12건)가 끝납니다. U35·U36은 아래 빌드 절차가 필요합니다.
U2b 구간에서 화면이 0.5초쯤 멈추는 것은 의도한 정지 프레임입니다.

## 6. 빌드에서 확인하기 (U35·U36)

에디터의 Game 뷰는 진짜 창이 아니라 U35·U36 값이 의미가 없습니다. Windows 플레이어로
빌드해서 봅니다.

### 6-1. Player Settings 바꾸기

Edit → Project Settings → Player → (Windows 탭) **Resolution and Presentation**:

| 항목 | 값 |
|---|---|
| Fullscreen Mode | **Windowed** |
| Default Screen Width / Height | 1280 / 720 |
| Default Is Native Resolution | 끔 |
| Resizable Window | **끔** |
| **Allow Fullscreen Switch** | **끔** (U36의 핵심) |
| Run In Background | 끔 |

### 6-2. 빌드

1. File → Build Profiles(또는 Build Settings) → Windows 선택 → Scenes 목록에 현재 씬이
   들어 있는지 확인(없으면 Add Open Scenes).
2. Build → 폴더는 `indiv\Survivor\Build\Spike\` 같은 **프로젝트 밖이 아닌, Assets 밖** 위치.
3. 빌드가 끝나면 그 폴더의 `Survivor.exe`를 실행합니다.

### 6-3. 실행 중 할 일

1. 창이 뜨면 화면에 검은 상자 안에 `[SPIKE]` 줄들이 보입니다. `[SPIKE][DONE]`까지 기다립니다
   (수 초).
2. **Alt+Enter**를 한 번 누릅니다. 1~2초 기다립니다.
   - 전체화면으로 바뀌고 `[SPIKE][U36] changed: ...` 줄이 추가되면 → U36은 **NO**(막히지 않음).
   - 아무 변화가 없고 추가 줄이 없으면 → U36은 **YES**(막힘).
   - 바뀌었다면 Alt+Enter를 한 번 더 눌러 되돌린 뒤 종료합니다.
3. Alt+F4로 종료합니다.
4. 로그 파일을 엽니다. 두 곳 중 하나:
   - `C:\Users\<이름>\AppData\LocalLow\DefaultCompany\Survivor\Wave0Spike_result.txt`
     (`[SPIKE]` 줄만 모아 둔 파일. **이걸 복사하는 게 가장 쉽습니다**)
   - 같은 폴더의 `Player.log` (전체 로그. `[SPIKE]`로 검색)
   - 탐색기 주소창에 `%USERPROFILE%\AppData\LocalLow\DefaultCompany\Survivor` 를 붙여 넣으면
     바로 열립니다. Company/Product 이름을 바꿨다면 그 이름의 폴더입니다.
5. 빌드 로그의 `[SPIKE][U35] ...`, `[SPIKE][U35-editor-ref] ...`, `[SPIKE][U36] ...` 줄을
   결과 양식의 U35·U36 칸에 붙입니다. 모니터가 여러 대이거나 Windows 배율이 100%가 아니면
   그 사실(예: "모니터 2대, 배율 150%")도 적어 주세요 — `workArea` 숫자 해석에 필요합니다.

## 7. 문제가 생기면

| 증상 | 할 일 |
|---|---|
| Console에 빨간 컴파일 오류 | Play 하지 말고 오류 줄(파일명·줄 번호 포함)을 통째로 복사해 PM에게. 스크립트는 컴파일 없이 작성됐으므로 API 이름 오류가 있을 수 있습니다 |
| `[SPIKE][DONE]`이 10초 넘게 안 나옴 | Play를 끄고, 그때까지 나온 `[SPIKE]` 줄과 마지막 `[SPIKE-TRACE]` 줄 5개를 복사해 PM에게 |
| `Physics2D.Simulate` 관련 경고 | 정상일 수 있습니다. 경고 줄도 함께 복사 |
| 빌드 창이 안 뜨거나 바로 꺼짐 | `Player.log`의 마지막 30줄을 복사해 PM에게 |
| 결과 파일(`Wave0Spike_result.txt`)이 없음 | `[SPIKE][FILE]` 줄에 적힌 경로를 확인. 없으면 Player.log를 대신 사용 |

## 8. 배치 실행 (PM이 수행 — 에디터를 열지 않고 10건 측정)

에디터 Play 대신 명령줄에서 PlayMode 테스트로 스파이크를 돌릴 수 있습니다. U1~U32 12건이
대상이고, U35·U36은 창이 없어 UNKNOWN으로 나옵니다(6절 빌드 절차는 그대로 필요). U23은
배치 모드에서 `targetFrameRate=60`이 지켜지는지에 따라 값이 달라질 수 있으니 에디터 Play 값과
함께 봅니다.

### 8-1. 파일 배치 (`docs/spike` → `Survivor/Assets/Spike`)

| 출발 | 도착 |
|---|---|
| `docs/spike/Wave0Spike.cs` | `Survivor/Assets/Spike/Wave0Spike.cs` |
| `docs/spike/Wave0Spike.asmdef` | `Survivor/Assets/Spike/Wave0Spike.asmdef` |
| `docs/spike/Tests/Wave0SpikeTests.cs` | `Survivor/Assets/Spike/Tests/Wave0SpikeTests.cs` |
| `docs/spike/Tests/Wave0Spike.Tests.asmdef` | `Survivor/Assets/Spike/Tests/Wave0Spike.Tests.asmdef` |

`Wave0Spike.asmdef`가 Spike 폴더를 `Wave0Spike` 어셈블리로 만들고, 테스트 어셈블리가 그것을
참조합니다(기본 Assembly-CSharp는 테스트 asmdef에서 참조할 수 없기 때문). 에디터 Play(3절)
절차는 asmdef가 있어도 그대로 동작합니다.

### 8-2. 명령

**먼저 Unity 에디터에서 Survivor 프로젝트를 닫습니다.** 열려 있으면 프로젝트 잠금(`Temp/UnityLockfile`)
때문에 "Multiple Unity instances cannot open the same project" 오류로 바로 종료됩니다.

```
"C:\Program Files\Unity\Hub\Editor\6000.3.28f1\Editor\Unity.exe" -batchmode -nographics -projectPath "C:\Users\SSAFY\Desktop\borry\indiv\Survivor" -runTests -testPlatform PlayMode -testFilter Wave0SpikeTests -testResults "C:\Users\SSAFY\Desktop\borry\indiv\Survivor\spike-results.xml" -logFile "C:\Users\SSAFY\Desktop\borry\indiv\Survivor\spike-editor.log"
```

- 한 줄로 입력합니다(PowerShell이면 맨 앞에 `&` 를 붙입니다).
- 첫 실행은 라이브러리 임포트 때문에 수 분 걸립니다. 종료 코드 0이면 테스트 통과(= DONE까지 도달),
  2면 테스트 실패(타임아웃), 3이면 컴파일 오류 등 실행 자체 실패입니다.
- 결과 보는 곳(둘 다 `[SPIKE]`로 검색):
  - `spike-editor.log` — 모든 `Debug.Log`가 그대로 남습니다. `[SPIKE][SUMMARY]` 덩어리가 가장 읽기 쉽습니다.
  - `spike-results.xml` — `<test-case name="Wave0Spike_RunsToDone" result="Passed">` 안의 `<output>`에
    같은 로그가 들어 있습니다.
  - `%USERPROFILE%\AppData\LocalLow\DefaultCompany\Survivor\Wave0Spike_result.txt` 도 같이 생성됩니다.
- 테스트는 판정을 하지 않습니다. 20,000프레임 안에 `[SPIKE][DONE]`이 안 나오면 `timeout`으로
  실패하며 메시지에 마지막 단계 이름(`last phase=...`)이 들어 있습니다.

## 9. 측정이 끝난 뒤

- `Assets\Spike\` 폴더는 삭제해도 됩니다(실제 구현 전에 반드시 지웁니다).
- Player Settings의 변경(6-1)은 실제 프로젝트 설정과 같으니 그대로 둬도 됩니다.
- 결과 파일은 PM이 `RUN_LOG.md`에 기록하고 이 스파이크는 버립니다.
