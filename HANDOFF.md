# 세션 인수인계 (Handoff)

새 세션을 열었다면 **이 문서를 먼저 읽는다.** 읽는 순서: §1(이 프로젝트의 현재 위치) → `RUN_LOG.md` 최신 "세션 인수인계" 항목(상세) → `DECISIONS.md` 색인(결정의 현재 상태·이유) → `ORCHESTRATION.md`·`CODING_GUIDE.md`(팀 구조·정책·코딩 규칙, 항상 읽음). 이 문서는 그 문서들에 없는 것만 담는다 — §1은 **이 설치본 전용**(PM 세션을 마칠 때마다 갱신), §2·§3은 하네스 수준(범용, 하네스 저장소와 공유).

---

## 1. 이 프로젝트의 세션 인수인계 (최종 갱신: 2026-11-27)

**프로젝트**: 뱀서라이크 1차 MVP — Unity 6000.3.28f1 / C#, 프로젝트 `Survivor/`, 원격 `github.com/borry345/Atashi-Vampire` main. 목적은 게임이 아니라 **아키텍처 학습 + 측정 기록 포트폴리오**(`진로-및-뱀서라이크-계획.md` 4절). 기획은 `spec/`(SPEC 14회차)·`tech/`(TECH 26회차), 결정은 `DECISIONS.md`, 결과 기록은 `README.md`·`docs/devlog.md`·`docs/measurements/`.

**현재 위치**: main = origin/main **fe2b09e**(2026-11-25). 코드는 15ecaf9(웨이브 5b 머지본)과 같고 그 뒤는 전부 문서·데이터 커밋. 테스트 기준선 EditMode **823** / PlayMode **360**(skipped 0). 상태 해시 4값(36조합 `0x9CCD040879EF6EE7`, 게이트 `0x6B8AA2178AB9C02D`, Dev 결정론 `0xB63948329659FF2E`, 프레임레이트 `0x5928B7C20FEF59D6`) 불변. 작업트리 미커밋: 이 문서·`RUN_LOG.md`·`CLAUDE.md`(인수인계 재작성).

**끝난 것**: 구현 전부 머지(웨이브 1~7·5b), 측정 1차(16회 + A0 스윕 + E1·E2 2,499건 일치 — `docs/measurements/`, devlog 3절), README·devlog 작성, 빌드 3종(`C:/Users/SSAFY/Desktop/borry/builds/`), **GitHub Release v0.1.0 공개**(태그 = f60ba0c, zip 43.5MB). 결정론 검증은 충분하다고 사용자가 판단 — 더 하지 않는다.

**진행 중 — 측정 2차**(DECISIONS 2절 "측정 2차 범위 (2026-11-24)"; 1차 결론이 약한 세 원인 = 마커 밖 비용 5.44ms 미확정·1회 측정·Hold N이 풀을 혹사하지 않음). 사용자 결정 4건 확정: N 1000·3회 반복(S1 풀 3조합·S2 충돌 2조합·A1/A6@1200 재측정)·S1 젬 즉시 회수 + 미회수 1판·에디터. 기획 문서는 끝났다: SPEC 14회차(F10 묶음 S·변형 세트·기준 334/345), TECH 26회차(프리셋 6개 `S1-A1`·`S1-A2`·`S1-C1`·`S1-gems`·`S2-A1`·`S2-A5`, 마커 `Survivor.Separation`, CSV 보조 열 3 + 프레임 열 2, `IDevModeHost.CollisionEventTotal`, `CollisionEventRouter.DispatchedEventTotal`, schema `-2`, buildType `Editor`). 변형 JSON은 `docs/measurements/stress/`(README에 적용·원복 명령).

**다음 할 일 (순서)**:
1. 사용자 승인 → **웨이브 9 디스패치**: 브리핑 `docs/dispatch/2026-11-25-wave-9.md`(IMP-DEV 1명, 줄 단위 편집 5파일, 8~15만). 워크트리 `git worktree add -b wave-9/dev C:/Users/SSAFY/Desktop/borry/wt-9-dev origin/main`. 검증 스크립트 `flow-run.sh`는 세션 임시 폴더에 있어 새 세션이면 다시 만든다(내용은 브리핑 0.2·RUN_LOG 2026-11-12 5b 항목).
2. PM 재검증(2회 연속 실행·해시 4값·본문 33열·끝 열) → feat 커밋 → `--no-ff` 머지 → 푸시(승인).
3. 사용자 측정 22판(기존 `summary.csv` 이동 → 세트 복사 → 에디터 Play → `git checkout -- Survivor/Assets/StreamingAssets/Data/` 원복). PM이 절차서를 `docs/measurements/stress/README.md`에 보강.
4. 결과 집계 → devlog 3절 2차 절·06 [실측] 갱신(문서 작성자 + tech-spec-writer, 웨이브 8b 패턴) → 체크포인트.
5. 3차 보류 항목(분리 패스 공간 해시·N 2000~4000 묶음·Rigidbody 비교)은 2차 결과를 본 뒤 결정.

**사용자 수행 남은 것**: SmartScreen 문구(README 16행), GIF·영상(README 5·7행), F11 시연 결과(devlog 4절), F19 오버헤드 비교(선택), 테스터 피드백(devlog 5절). 자리표시 README 4·devlog 15.

**주의**: 사용자 Unity 에디터가 `indiv/Survivor`를 열고 있다 — 배치 테스트는 워크트리에서만. `gh`는 `/c/Program Files/GitHub CLI/gh.exe` 전체 경로(PATH 미반영), 로그인 상태. `.scratch/` 없음. 1차 CSV의 `buildType Development`는 실제로는 에디터(TECH 26 주석). 사용자 소유 파일(`ProjectSettings/*`·`Packages/manifest.json`·`Resources/AssetKeyRegistry.asset`·`StreamingAssets/Data/*.json`)은 PM·에이전트가 고치지 않는다. 비용 누계(2026-11-12~27 디스패치) 약 132만 토큰.

---

## 2. 하네스 수준 — 세션이 바뀌면 끊기는 것 (범용)

- **역할 분담**: 이 하네스는 프로젝트 콘텍츠(코드·SPEC·RUN_LOG)를 담지 않는다(2026-09-14). 결정의 현재 상태·이유는 `DECISIONS.md`, 시간순 이력은 `RUN_LOG.md`(append-only)가 소유한다(2026-10-27 분리 — 이유: RUN_LOG 하나에 결정 46건의 재수정이 흩어져 현재 상태를 알 수 없게 됐기 때문). 이 문서는 결정을 요약하지 않는다. 프로젝트 진행 상황의 인수인계는 §1(요약)과 RUN_LOG 최신 "세션 인수인계" 항목(상세)이 맡는다.
- **하네스 상태**: 최신 태그 `v0.2.3`(2026-11-02). 설치본의 중첩 저장소 `borry-claude-harness/`(gitignore)는 master d161675 = v0.2.3 + 2커밋(기획 문서 분할 규칙 포함). 이력은 `CHANGELOG.md`가 소유한다. 다른 프로젝트에 쓰려면 `sh borry-claude-harness/scripts/install-into-project.sh <프로젝트 루트>`(기존 파일은 덮어쓰지 않음).
- **2026-11-05~27 운행 교훈 — 아직 `[Unreleased]`·에이전트 정의·스킬에 미반영(반영하면 이 줄을 지운다)**:
  ① 짧은 질문에 `/fork`를 쓰지 않는다 — 포크가 전체 컨텍스트와 같은 체크아웃의 승인·디스패치를 함께 들고 가서 두 세션의 역할이 뒤바뀌었다(2026-11-16). 같은 세션에서 묻거나 `claude -p`.
  ② spec-writer·doc-writer 정의에는 셸·파이썬이 없어 `eol-audit`를 스스로 못 돌린다 — "CR 검사 미수행" 보고가 정상이고 PM이 검사한다.
  ③ 더운 서브에이전트 재개(`SendMessage`)는 싸고 정확하다 — 2줄 수정 0.4만, 5건 후속 1.8만. 압축 뒤에도 같은 세션 안에서는 ID가 유지됐다.
  ④ PM 브리핑의 "확인한 사실"은 디스패치마다 1~2건이 틀렸고 전부 작성자의 코드 확인이 잡았다(누계 9/9 에이전트 정답) — "PM 전제를 반박하면 먼저 실측" 유지.
  ⑤ `RUN_LOG` append는 반드시 파이썬 heredoc으로 — `python -c "…"` 안의 백틱이 셸 명령 치환으로 실행되어 줄이 깨진 일 2회. `runlog-append` 스킬에 적을 것.
  ⑥ 외부 CLI 설치·로그인은 세션 밖에서 — `winget install`은 현재 셸 PATH를 갱신하지 않고, `gh auth login` 브라우저 인증은 세션 120초 제한에 걸린다. 1회성 배포는 웹이 더 짧다.
  ⑦ PowerShell `Compress-Archive`는 zip 안 경로를 역슬래시로 쓴다 — 배포 zip은 파이썬 `zipfile`로.
  ⑧ Unity define 심볼 변경은 `ProjectSettings.asset`에 메모리로만 반영된다 — 사용자가 Save Project를 눌러야 디스크에 써진다. 콘솔 "Deleting invalid font reference" 다수는 도메인 재로드 잡음.
- **세션 시작 점검 3개**: ⓐ Agent 도구에 `run_in_background`가 있는지(없으면 fork mode — 포그라운드 요청 불가). ⓑ `/agents`에 커스텀 타입(`tester`·`spec-writer` 등)이 네이티브로 뜨는지 — 안 뜨면 `general-purpose` + "정의 파일을 따르라" 우회(그때 `model:`·`tools:`는 적용되지 않으므로 모델은 인자로, 도구 제한은 소프트 제약). ⓒ 릴리스 교차검증 검토자는 PM이 `claude -p`로 열어도 된다(사용자 결정 2026-10-28, 조건은 `ORCHESTRATION.md` "검토자 세션의 개설").

## 3. 잊으면 안 되는 것 (ORCHESTRATION.md에 있지만 특히 놓치기 쉬운 것만 재강조)

- **git commit/tag/push는 항상 사용자와 상의**(체크포인트 커밋 스크립트도 승인 게이트를 지난다). 한 번의 승인은 그때 말한 묶음에만 유효하다. 무응답은 동의가 아니다.
- **사용자 결정은 한 번에 하나씩** — 배경 설명과 남은 결정 표를 먼저, 그다음 질문 하나와 추천안(`ORCHESTRATION.md` "PM 행동 규칙"). 결정이 확정되면 **같은 턴에** 소유 문서(`DECISIONS.md`)를 고친다.
- **비싼 디스패치(검토자·구현자류, 수만~수십만 토큰)는 비용 추정과 함께 "진행할까요"를 먼저 묻는다.** 소폭 문서 수정은 묻지 않고 한다.
- **에이전트 보고는 그대로 믿지 않는다** — 수치는 PM이 다시 돌리고, PM 전제를 반박하면 먼저 실측한다.
- **하네스는 프로젝트 루트에 "설치"해야 한다**(`scripts/install-into-project.sh <프로젝트 루트>`). 옆 폴더에 두면 `CLAUDE.md` 자동 로드·`settings.json` 승인 게이트·`checkpoint-commit.sh`가 전부 꺼진다(PJT4에서 실제로 발생).
- **프로젝트 폴더를 하네스 안에 다시 만들지 않는다**(2026-09-14에 한 번 발생). 코드·프로젝트별 스펙은 항상 하네스 폴더 바깥.
- **개행**: 하네스 문서(이 문서·`CLAUDE.md`·`ORCHESTRATION.md`·`CODING_GUIDE.md`)는 CRLF, 프로젝트 문서·소스는 LF. 확인은 `eol-audit` 스킬의 파이썬 바이트 방법으로(셸 `grep -c $'\r'`는 이 환경에서 틀린다).
