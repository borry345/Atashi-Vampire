# tech/ 색인 — TECH_SPEC 뱀서라이크 1차 MVP (6회차)

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

분할 뒤 개정 규칙: 설계 개정은 해당 `5.xx`·`02`·`5a`·`5c`·`06` 파일에서 하고 `(N회차 …)` 표기는 지금처럼 둔다. 회차 번호와 변경 요약은 이 README에만 추가한다. `00-header.md`의 0절은 2회차 기록으로 동결한다.

## 파일 목록

| 파일 | 내용 | 행 수 |
|---|---|---|
| [`00-header.md`](./00-header.md) | 제목·머리말 · 0 변경 요약 (회차 스탬프의 원본) | 70 |
| [`01-overview.md`](./01-overview.md) | 1 개요 · 2 기존 코드 구조 분석 · Unity 동작 전제(U 표) | 137 |
| [`02-feature-tac.md`](./02-feature-tac.md) | 3 기능 목록 · 4 기능별 기술 수용 기준 TAC (전원 읽음) | 223 |
| [`5.00-assemblies-interfaces.md`](./5.00-assemblies-interfaces.md) | 5 머리말 · 5.0 네임스페이스와 어셈블리 · 5.0.1 인터페이스 전체 목록 | 36 |
| [`5.01-entity-id.md`](./5.01-entity-id.md) | 5.1 객체 ID와 할당기 (Core) | 39 |
| [`5.02-rng.md`](./5.02-rng.md) | 5.2 결정론 RNG (Core) | 31 |
| [`5.03-step-clock.md`](./5.03-step-clock.md) | 5.3 고정 스텝 시계·게이트 (Core 계약, Simulation 구현, IMP-LOOP) (2회차 재작성) | 94 |
| [`5.04-commands-spawn.md`](./5.04-commands-spawn.md) | 5.4 지연 명령과 생성 경로 (Core 계약, Simulation 구현) (2회차 재작성, A7) | 49 |
| [`5.05-input.md`](./5.05-input.md) | 5.5 입력 소스 (Core 계약, IMP-LOOP 키보드·IMP-DEV 자동 경로) | 21 |
| [`5.06-pool.md`](./5.06-pool.md) | 5.6 풀 전략과 템플릿 데코레이터 (Core 계약, IMP-POOL·IMP-COLLISION·IMP-LOOP 구현) (2회차 수정, A8) | 34 |
| [`5.07-targeting.md`](./5.07-targeting.md) | 5.7 타깃 공급자 (Core 계약, IMP-SPATIAL 구현) | 75 |
| [`5.08-tick.md`](./5.08-tick.md) | 5.8 중앙 틱 (Core 계약, IMP-LOOP 구현) (2회차 수정, A8) | 30 |
| [`5.09-collision.md`](./5.09-collision.md) | 5.9 충돌 전략 (Core 계약, IMP-COLLISION 구현) | 137 |
| [`5.10-lifecycle.md`](./5.10-lifecycle.md) | 5.10 엔티티 생명주기 (Simulation, IMP-LOOP) — 생성·회수의 유일한 경로 | 46 |
| [`5.11-data-loader.md`](./5.11-data-loader.md) | 5.11 데이터 로더·레지스트리·검증 (Data, IMP-DATA) | 111 |
| [`5.12-damage-rules.md`](./5.12-damage-rules.md) | 5.12 피해·획득 규칙 구성요소 (Simulation, IMP-PLAYER/IMP-MONSTER) | 39 |
| [`5.13-json-schemas.md`](./5.13-json-schemas.md) | 5.13 JSON 스키마 7종 | 237 |
| [`5.14-progression.md`](./5.14-progression.md) | 5.14 레벨업·진화·상자·진행 적용 (Simulation) (2회차 재작성, A4·A5·A11) | 82 |
| [`5.15-dev-mode.md`](./5.15-dev-mode.md) | 5.15 개발자 모드 주입 경계와 측정 (Dev, IMP-DEV) | 152 |
| [`5.16-state-hash.md`](./5.16-state-hash.md) | 5.16 상태 해시 (Simulation, IMP-LOOP) | 12 |
| [`5.17-flow-state.md`](./5.17-flow-state.md) | 5.17 흐름 상태 (App, IMP-FLOW) (2회차 신규) | 20 |
| [`5.18-screen.md`](./5.18-screen.md) | 5.18 화면 설정 (App, IMP-FLOW) (2회차 신규, A13) | 23 |
| [`5a-shared-rules.md`](./5a-shared-rules.md) | 5a 공유 규칙 표 R1~R30 (전원 읽음) | 39 |
| [`5b-external-settings.md`](./5b-external-settings.md) | 5b 외부 서비스·프로젝트 설정 점검표 | 24 |
| [`5c-ownership-contracts.md`](./5c-ownership-contracts.md) | 5c 폴더 구조·소유권 맵·계층 계약·디스패치 순서·테스트 전략·구현자별 읽을 파일 5c.2b (전원 읽음) | 198 |
| [`06-open-questions.md`](./06-open-questions.md) | 6 미확정 사항 D·U | 93 |

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
