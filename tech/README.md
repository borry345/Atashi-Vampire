# tech/ 색인 — TECH_SPEC 뱀서라이크 1차 MVP (14회차)

> 2026-10-27 단일 `TECH_SPEC.md`를 제목 단위로 분할했다(RUN_LOG 2026-10-27 D1). 내용은 바이트 그대로 옮겼고 ID(TAC·R·D·U·A 번호, 5.x 절 번호)는 변하지 않았다. 소유자는 tech-spec-writer이며, 개정 회차와 변경 요약은 이 파일이 관리한다.

## 개정 이력

| 회차 | 요지 | 전문 위치 |
|---|---|---|
| 1 | 최초 작성. 00-header.md에 요약 없음 (RUN_LOG 참조) | RUN_LOG |
| 2 | 독립 검토 "수정 필요" 13건(A1~A13: 측정 CSV 열, 배속 3x, 스텝 안 게이트 닫기, 레벨업 즉시 적용, 물리 바디 동기화 단일 소유, 생성 경로 단일화, asmdef 참조 위반 해소, 공유 규칙 R22~R30, 창·빌드 설정 등)과 PM 채택 확인 권장분, SPEC 4회차 정합(C 목록 18건) 반영. D25~D33·U30~U42 신규 | `00-header.md` 0절 A~D |
| 3 | 2차 독립 검토·PM 결정 반영 소폭 개정 7건: 기록 초안을 같은 머지 커밋에 포함(웨이브 6 축소), `RunSession.Reset` 순서 정정, 오버라이드 판 시작 1회 바인딩, `IsLarge` 판정, 측정 중 상자 드랍 0%·무기 구성 고정, D26 해소, `EndStep` 직접 호출·`StopAtStep` `>=` | `00-header.md` "3회차 변경 요약" |
| 4 | doc-writer 보고 내부 불일치 3건: 충돌 이벤트 비교 키를 `CollisionEvent.CompareTo`로 통일, `TargetProviderSlot`은 단계 핸들러가 아님, 부팅 시 최초 JSON 로드 경로(5.11.0) 추가 | `00-header.md` "4회차 변경 요약" |
| 5 | SPEC 6회차(W2 궤도 원 생애) 반영: 궤도 원 수명 없음 규약(`ExpireStep = long.MaxValue`), 회수 경로 셋, `LifetimeSystem`이 수명 없는 엔티티를 건너뜀 | `00-header.md` "5회차 변경 요약" |
| 6 | 단일 파일을 제목 단위로 분할(내용 불변), 5c.2b 구현자별 읽을 파일 표 신설, 머리말의 SPEC 경로를 spec/로 수정, 옛 경로 정정, 수용 기준 개수 329 → 286 정정 | 이 README |
| 7 | SPEC 7회차(2026-11-05, 수용 기준 288개) 반영 소폭 개정, 설계 변경 없음: 첫 보유 `_nextFireStep` 규약(TAC-F2-2·5.14), 진화 결과 무기 첫 동작·궤도 원 1스텝 공백(TAC-F7-2), `placement: player` 타깃 없음 재시도(TAC-F6-5), W4 첫 발사 방향(TAC-F6-3), 보유 항목 "새 획득" 제외(TAC-F5-3), D14·D25 사용자 확정 해소, D29·D31 PM 확인 완료, IMP-MONSTER 소유 클래스 설계 5.12.1 신설, 5.13 W2~W4 5단계·E2 예시값, U32 담당 통일(5c.4·5c.2b·6.3) | 이 README |
| 8 | 2026-11-05 독립 검토 2차와 SPEC 8회차(수용 기준 정규식 295, 보정 포함 306) 반영: 참조 방향 위반 해소 — `UpgradeKind`·`UpgradeOption`을 Core로(5.14·5.00·R12·5c.2), `RunOutcome`·`RunOutcomeKind`를 Simulation으로(5.17·5.14·5.00·5c.2), `RunSession.Reset` 뒤 `MonsterSpatialIndex.Clear`·`ApplyRequirements`는 App이 조율(TAC-F8-2·5.07). 5c.2b 읽을 파일 보강(CORE·LOOP·PLAYER·MONSTER·FLOW). SPEC 8회차 규정: "타깃 있음"은 발사·생성형만(TAC-F6-5), 측정 무효(게이트 Paused·RunReset·Error)와 결과 파일 무효 사유(TAC-F19-5·5.15.2·5.15.3), 상한 400 = 보스 제외·소환 쿨다운 소비(TAC-F3-3·F3-9·R20·5.12.1), 보스 판당 1기·`>=`·조기 처치 클리어(TAC-F3-6·F8-8·5.12.1·5.14), 시간 점프 앞으로만(TAC-F19-3·5.03), 핫 리로드 인스턴스 정의 유지(TAC-F11-5·5.11.1·5.14), 공격 끔 범위(TAC-F2-5·5.12), P1 다음 산정부터(TAC-F2-3·5.12), 상자 2개 순차(TAC-F4-4·5.12.1·5c.5), 강제 종료·오류 미저장(TAC-F8-3·5.17). 정합: TAC-F4-1 보스 문구, 5.13 궤도 원 불변식 D2 식 재계산, 5c.5 측정 가드 6조건, 01 2.0 경로, D5·D27·D29 해소·D26 반영 확인(6.3: 해소 18·미해소 15) | 이 README |
| 9 | 2026-11-05. SPEC 9회차 반영: `placement: player` 장판은 타깃 조건 없음(TAC-F2-2·F6-5, 사용자 결정 — 7회차 "타깃 없으면 재시도" 번복), 설계 변경 없음 | 이 README |
| 10 | 2026-11-05. 웨이브 0 실측 반영(Unity 6000.3.28f1 에디터, `docs/spike/results/2026-11-05-editor-run2.txt`): U 10건 확인 + U23 틀림(해소) = 11건((11회차 정정) 그중 U11은 부분 확인으로 재분류 → 확인 9 + U23 = 10건), U23 → D9 변경(`maximumDeltaTime = 0.05/timeScale`, 5.3·5.15.1·TAC-F19-6), 경계 판정 접촉 포함(U9, R5), 상한 테스트 방법(실제 긴 프레임, TAC-0-3·0-4·5c.5), Kinematic 구성 유지·sleepMode 불변(U5, 5.9), `SyncMonstersForQuery`의 `SyncTransforms` 불필요(U32, 5.9·TAC-F16-3), 6.2·2.3 표 상태 표기, 6.3 U 미확정 41 → 30, 5c.4 웨이브 0 완료(U35·U36 빌드 대기) | 이 README |
| 11 | 2026-11-05. 2차 독립 검토 반영(PM 기본값, SPEC 10회차 295/306): 참조 방향 — 타깃 공급자 교체를 App이 만든 공급자 + 요구사항 콜백 명령 `SwitchTargetProviderCommand`로(5.7·5.15 `IDevModeHost.RequestTargetProviderSwitch`·TAC-F16-5·5c.3 Begin·5.9), `RunSession.Reset(in RunStrategies)`로 전략 전달 명시·판 시작 요구사항 콜백 통일(5.14·TAC-F8-2). `SpawnBossNow` 판당 1기 거부(5.15·5.12.1·TAC-F19-2), 상태 해시에 장판·궤도 원·`_nextFireStep`(5.16·TAC-0-4·5c.5), U11 부분 확인으로 재분류(6.3 미확정 30 → 31), 측정 무효 즉시 종료(5.15.2·TAC-F19-5), `summary.csv` 마지막 유효 행, 판 시작 배속 초기화(5.03·5.15.1·TAC-F19-6), 허용 오차 비교(TAC-0-3·F19-6), 결과 무기 삭제 시 진화 불가(5.14), `SyncTransforms` 비호출 정리(5.9·01 2.2), 스파이크 메모 주석(5c.4), 공격 끔 중 궤도 원 공백 예외(TAC-F2-5), 00-header 범례 (9회차)·(11회차)·★ | 이 README |
| 12 | 2026-11-06. U35·U36 빌드 실측 상태 반영(Windows 플레이어, `docs/spike/results/2026-11-06-build-run1.txt`): 둘 다 확인됨(01 2.3 표·06 6.2·5.18·5c.2b FLOW 행·5c.4 웨이브 0·4 행·스파이크 메모), 6.3 U 미확정 31 → 29, 00-header 범례 (12회차). 설계 변경 없음. **PM이 직접 한 사실 수정**(tech-spec-writer 미디스패치, RUN_LOG 2026-11-06) | 이 README |
| 13 | 2026-11-06. 웨이브 1(IMP-CORE, main 524b32c, EditMode 205건) 구현 확정 사실 반영, 설계 변경 없음: 객체 ID 타입·할당기 이름을 `EntityHandle`·`EntityHandleAllocator`로 전면 치환(Unity 6 이름 충돌, 사용자 결정 DECISIONS 1절, 옛 이름은 5.01 각주에만), readonly struct 5개 생성자(`SimulationCommand`+`WithSequence` 5.4, `TargetHit` 5.7, `PhysicsBodyRequirements` 5.9.1, `StrategySelection` 5.15, `UpgradeOption` 5.14), 공개 멤버 추가(`CollisionChannelTable.GetLayers`·`ChannelCount`·`CollisionLayerMapping.FirstUnityLayer` 5.9·R5·R30, `StepMath.StepsPerSecond` R1, `MoveMath.Diagonal` R17), 셀 크기 2.0 단일 소유 `StrategySelection.DefaultSpatialHashCellSize`(5.7·5.15·TAC-F16-4, public 공개는 웨이브 2 전 IMP-CORE), 구현이 정한 의미 명문화(5.2 PCG stream id·SplitMix64, 5.9·5.15·TAC-F18-8 충돌 이벤트 열거형 이름 표기, R1·R4·R5 A측 = 채널 이름 앞 레이어·R7·R9·R13·R17·R26·R30), 5c.2 IMP-CORE 파일 3개(`RngStream.cs`·`SimulationCommandKind.cs`·`TickGroup.cs`)·`Survivor.Tests.EditMode.asmdef` 소유, 5c.4 웨이브 1 완료, 00-header 범례 (13회차) | 이 README |
| 14 | 2026-11-06. 웨이브 2a(IMP-DATA·IMP-POOL·IMP-SPATIAL A·B, main d06b0bb) 구현 확정 사실 반영, 설계 변경 없음: 구현자 테스트로 U20(기본 원 bounds 1×1)·U21(`ObjectPool` 중복 반환 `InvalidOperationException`, 남의 객체는 못 잡음 → 자체 Owner 검사)·U22(`Destroy` 1프레임 뒤, 비활성화 먼저 유효)·U25(Newtonsoft 3.2.2, 테스트 asmdef에서는 안 보임) 확인됨(01 2.3·06 6.2, 6.3 U 확인 15·미확정 25), 풀 구현 확정 5.6.1 신설(`PoolInstanceTracker` 합성, 템플릿·반환 검증 예외 규칙, C의 `Prewarm`·감싼 예외, `ReleaseAll` 획득 순, TAC-F9-5 ID = `GetInstanceID()`), 공간 해시 구현 의미 5.7.1 신설(`QueryCircle`·`ForEachRing` 비우고 채움·상위 집합, `SpatialHash.CellSize`·`Count`, `SpatialHashSettings.IsValidCellSize`, B의 다른 버퍼 `ArgumentException` [기본값], `Required` 켜기 전제를 App/LOOP 계약으로 5.7·5c.3 #9), TAC-F16-2 종료 조건 여유 각주(구현자 반박 수용), R14 확인, 5.9 `QueryCircle` 소비자 재판정 의무, 데이터 구현 확정(5.11 공개 멤버 ★·`JsonTextReader` 파서 교체·`IsPlayable` = Blocking 0건·엄격성 규칙 [기본값]·5.11.0 순서 확인, 정의 객체 표 5.11.2 신설, 5.13 P2·P3 예시·정의 수, TAC-F3-7 등 정의 멤버 코드 이름 괄호), 공급자 C·`Tests/PlayMode/Targeting/*` 웨이브 3 이월(의존 누락 정정, 5c.2·5c.2b·5c.4), 5c.2 파일 목록(DATA 44 .cs·POOL `PoolInstanceTracker`·테스트 파일·asmdef 참조), 5c.3 #8 영역별 로그 도우미, 5c.5 테스트 관례(`LoadFromTexts`·문자열 치환 픽스처·`ignoreFailingMessages` 본문에서), 2a 기록 초안 별도 커밋 보충 기록(5c.4), 풀 전략 토글 잔존 인스턴스를 미결 설계 항목으로(06 6.1 끝·5.6.1, PM 결정 대기, D·U 번호 없음), 00-header 범례 (14회차) | 이 README |

분할 뒤 개정 규칙: 설계 개정은 해당 `5.xx`·`02`·`5a`·`5c`·`06` 파일에서 하고 `(N회차 …)` 표기는 지금처럼 둔다. 회차 번호와 변경 요약은 이 README에만 추가한다. `00-header.md`의 0절은 2회차 기록으로 동결한다.

## 파일 목록

| 파일 | 내용 | 행 수 |
|---|---|---|
| [`00-header.md`](./00-header.md) | 제목·머리말 · 0 변경 요약 (회차 스탬프의 원본) | 70 |
| [`01-overview.md`](./01-overview.md) | 1 개요 · 2 기존 코드 구조 분석 · Unity 동작 전제(U 표) | 137 |
| [`02-feature-tac.md`](./02-feature-tac.md) | 3 기능 목록 · 4 기능별 기술 수용 기준 TAC (전원 읽음) | 223 |
| [`5.00-assemblies-interfaces.md`](./5.00-assemblies-interfaces.md) | 5 머리말 · 5.0 네임스페이스와 어셈블리 · 5.0.1 인터페이스 전체 목록 | 36 |
| [`5.01-entity-id.md`](./5.01-entity-id.md) | 5.1 객체 ID와 할당기 (Core) | 41 |
| [`5.02-rng.md`](./5.02-rng.md) | 5.2 결정론 RNG (Core) | 32 |
| [`5.03-step-clock.md`](./5.03-step-clock.md) | 5.3 고정 스텝 시계·게이트 (Core 계약, Simulation 구현, IMP-LOOP) (2회차 재작성) | 95 |
| [`5.04-commands-spawn.md`](./5.04-commands-spawn.md) | 5.4 지연 명령과 생성 경로 (Core 계약, Simulation 구현) (2회차 재작성, A7) | 67 |
| [`5.05-input.md`](./5.05-input.md) | 5.5 입력 소스 (Core 계약, IMP-LOOP 키보드·IMP-DEV 자동 경로) | 21 |
| [`5.06-pool.md`](./5.06-pool.md) | 5.6 풀 전략과 템플릿 데코레이터 (Core 계약, IMP-POOL·IMP-COLLISION·IMP-LOOP 구현) (2회차 수정, A8) · (14회차) 5.6.1 구현 확정 사실 | 88 |
| [`5.07-targeting.md`](./5.07-targeting.md) | 5.7 타깃 공급자 (Core 계약, IMP-SPATIAL 구현) · (11회차) `SwitchTargetProviderCommand` · (14회차) 5.7.1 구현 확정 의미 | 124 |
| [`5.08-tick.md`](./5.08-tick.md) | 5.8 중앙 틱 (Core 계약, IMP-LOOP 구현) (2회차 수정, A8) | 30 |
| [`5.09-collision.md`](./5.09-collision.md) | 5.9 충돌 전략 (Core 계약, IMP-COLLISION 구현) | 158 |
| [`5.10-lifecycle.md`](./5.10-lifecycle.md) | 5.10 엔티티 생명주기 (Simulation, IMP-LOOP) — 생성·회수의 유일한 경로 | 46 |
| [`5.11-data-loader.md`](./5.11-data-loader.md) | 5.11 데이터 로더·레지스트리·검증 (Data, IMP-DATA) · (14회차) 5.11.2 정의 객체 | 189 |
| [`5.12-damage-rules.md`](./5.12-damage-rules.md) | 5.12 피해·획득 규칙 구성요소 (Simulation, IMP-PLAYER/IMP-MONSTER) · 5.12.1 IMP-MONSTER 소유 클래스 설계 (7회차 신설) | 89 |
| [`5.13-json-schemas.md`](./5.13-json-schemas.md) | 5.13 JSON 스키마 7종 | 261 |
| [`5.14-progression.md`](./5.14-progression.md) | 5.14 레벨업·진화·상자·진행 적용 (Simulation) (2회차 재작성, A4·A5·A11) · (11회차) `RunSession.Reset(in RunStrategies)` | 123 |
| [`5.15-dev-mode.md`](./5.15-dev-mode.md) | 5.15 개발자 모드 주입 경계와 측정 (Dev, IMP-DEV) | 164 |
| [`5.16-state-hash.md`](./5.16-state-hash.md) | 5.16 상태 해시 (Simulation, IMP-LOOP) | 13 |
| [`5.17-flow-state.md`](./5.17-flow-state.md) | 5.17 흐름 상태 (App, IMP-FLOW) (2회차 신규) | 26 |
| [`5.18-screen.md`](./5.18-screen.md) | 5.18 화면 설정 (App, IMP-FLOW) (2회차 신규, A13) | 23 |
| [`5a-shared-rules.md`](./5a-shared-rules.md) | 5a 공유 규칙 표 R1~R30 (전원 읽음) | 39 |
| [`5b-external-settings.md`](./5b-external-settings.md) | 5b 외부 서비스·프로젝트 설정 점검표 | 24 |
| [`5c-ownership-contracts.md`](./5c-ownership-contracts.md) | 5c 폴더 구조·소유권 맵·계층 계약·디스패치 순서·테스트 전략·구현자별 읽을 파일 5c.2b (전원 읽음) | 201 |
| [`06-open-questions.md`](./06-open-questions.md) | 6 미확정 사항 D·U | 97 |

## ID → 파일

| ID 형태 | 파일 |
|---|---|
| `TAC-F<n>-<k>` | `02-feature-tac.md` |
| `5.<n>` 절, 함수·인터페이스 | `5.<nn>-*.md` |
| `R<n>` 공유 규칙 | `5a-shared-rules.md` |
| `D<n>` 문서 간 불일치·기본값, `U<n>` 실측 필요 | `06-open-questions.md` (U 표 원문은 `01-overview.md` 2.3) |
| `IMP-*` 소유권, 웨이브 | `5c-ownership-contracts.md` |

## 구현자별 읽을 파일

단일 소유: [`5c-ownership-contracts.md`](./5c-ownership-contracts.md) 5c.2b "구현자별 읽을 파일" 표를 본다(여기에 복제하지 않는다).
