> 분할 파일 — 원본 `TECH_SPEC.md`(5회차)를 2026-10-27에 제목 단위로 나눈 것이다. 내용은 원본 그대로이며 색인·개정 이력은 `tech/README.md`에 있다. 수정은 tech-spec-writer만 한다.

# TECH_SPEC: 뱀서라이크 1차 MVP (6회차)

> 입력: `spec/`(SPEC 6회차, F1~F19, 수용 기준 286개(2026-10-27 기계 집계, spec/README.md 방법 기준)), `CODING_GUIDE.md`, `RUN_LOG.md` 2026-10-12·2026-10-16 항목(Q40·Q41·D17·D3·Q24 수정 확정, 피해 세 갈래, 세대 ID, 교차 검토 결과와 PM 방향). 결정 기록(DECISIONS.md, 없으면 RUN_LOG)의 확정 결정은 spec/보다 우선한다.
> 이 문서는 `spec/`를 수정하지 않는다. 기능 범위는 spec/ 그대로이며, 범위에 영향을 줄 수 있는 판단은 6절에 적었다.
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

