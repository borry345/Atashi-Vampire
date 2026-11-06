> 분할 파일 — 원본 `TECH_SPEC.md`(5회차)를 2026-10-27에 제목 단위로 나눈 것이다. 내용은 원본 그대로이며 색인·개정 이력은 `tech/README.md`에 있다. 수정은 tech-spec-writer만 한다.

## 6. 미확정 사항 (Open Questions)

### 6.1 코드에서 확인됨 → 이 프로젝트에서는 "문서 간 불일치 / SPEC 미규정" (코드가 없으므로 SPEC·RUN_LOG를 읽고 확인한 것)

각 항목에 [기본값]을 적었다. blocking 여부도 표시했다. 해소된 항목은 번호를 유지하고 "해소(2회차, 근거)"로 표시했다.

**C 목록 — SPEC 4회차 정합 점검에서 찾아 고친 것 (2회차)**: ① SPEC F8 "Canvas Scaler"인데 1회차는 UI Toolkit → uGUI로 변경(D25) ② SPEC F12 4회차 "TECH_SPEC 5.0의 인터페이스 전부"가 판정 기준인데 1회차 5.0에 인터페이스 목록이 없었다 → 5.0.1 ③ SPEC F19 4회차 "코드 제거 빌드와 1회 비교"가 1회차 App → Dev 참조로는 불가 → 의존 방향 반전(D31) ④ 1회차는 등록 해제가 Exit을 내지 않는데 겹침 집합·쿨다운 기록을 지우는 경로가 없어 **디스폰된 몬스터가 플레이어 겹침 집합에 남아 계속 피해를 주는 버그**가 있었다 → TAC-0-10 ⑤ TAC-F4-2 드랍 처리 순서가 "피해 적용 순서"로 모호 → `KillPipeline` ID 순 ⑥ SPEC F4 4회차 70:30 ±3%p(1,000회) 기준이 TAC·테스트에 없었다 → TAC-F4-7 ⑦ SPEC 3.0 I 몸통 접촉 최대 피해·작은 ID → TAC-F3-12(D11·D12 해소) ⑧ SPEC F3 4회차 몬스터는 플레이어 위치 직접 참조 → TAC-F3-11(D20 해소) ⑨ SPEC F6 4회차 W3 대상 없음 = 쿨다운 미소비 → TAC-F6-5(D24 해소, (9회차) `nearestEnemy`에만 해당) ⑩ SPEC F8 4회차 개발자 모드 판 기록 미저장·`PlayerPrefs.Save`·타이틀로 나가기 미갱신·메뉴 키보드+마우스·보스전 시간 제한 없음 → TAC-F8-3·F8-12·F8-13 ⑪ SPEC F10 4회차 C 묶음 "나머지 기준선" → `MeasurementPresets` ⑫ SPEC F10·F19 4회차 메모리·젬 수·워밍업·환경 4값·배제 조건 → 5.15 ⑬ SPEC F11 4회차 U+FFFD·CSV BOM → TAC-F11-6·F11-7 ⑭ SPEC F16 "반지름이 큰 객체" → R25 ⑮ SPEC F1 4회차 카메라 즉시 스냅 → TAC-F1-6 ⑯ SPEC F15 4회차 URP 템플릿 → 5b·U33·U34 ⑰ SPEC F12·F14 4회차 기능별 기록 → 5c.2a ⑱ 1회차 `MovingObjectSpec.CollisionLayerTag` 별칭은 C#으로 표현할 수 없다 → `CollisionLayer` 직접 사용.

- **D1.** F18 불변식 검사 대상 목록(W1·W4, 몬스터, M3 투사체, W2, W3)에 **플레이어**와 **끌리는 젬**이 빠져 있다(SPEC 4회차에서도 그대로). [기본값] 둘 다 검사 대상에 넣는다. 플레이어 속도는 P2 최대 단계를 적용한 값. 비차단.
- **D2.** W2 궤도의 "선속도 = 각속도 × 궤도 반지름"은 플레이어 이동을 뺀 값이다. 궤도 원의 실제 월드 이동량은 ωR + 플레이어 속도다. [기본값] 검사에는 ωR + 플레이어 최대 속도를 쓴다. 비차단.
- **D3.** 해소(2회차, RUN_LOG 2026-10-16 D3 확정 + SPEC F4 4회차): 젬·상자 디스폰 규칙은 넣지 않고 활성 젬 수를 통계·CSV에 기록한다(TAC-F4-8, 5.15.3).
- **D4.** SPEC의 "스폰 위치는 r=20 원주"가 보스 등장과 보스 소환에도 적용되는지 정해져 있지 않다. [기본값] 보스는 같은 원주, 소환은 보스 주변 `summonRadius` 원주다(소환체는 화면 안에 생길 수 있다). 비차단.
- **D5.** 해소(8회차, SPEC F7 8회차 "진화로 교체된 원본 무기는 이후 레벨업의 '새 획득' 후보에 나타나지 않아야 한다"로 [기본값]이 SPEC 기준으로 승격, TAC-F5-3). 원래 기록: 진화로 소모된 원본 무기를 레벨업에서 다시 "새 무기"로 얻을 수 있는지 SPEC에 없다. [기본값] 다시 얻을 수 없다(후보에서 제외). 비차단.
- **D6.** 보스(M4) 정의가 무효면 10분 뒤 클리어가 불가능하지만, SPEC의 "플레이 불가 4기준"에는 들어 있지 않다. [기본값] Warning만 남기고 시작은 허용한다. (2회차) 결과는 **소프트락**이다: 36000 스텝 이후 일반 스폰이 끝나고 보스도 나오지 않아 사망·타이틀로 나가기 외에 판이 끝나지 않는다(TAC-F8-10). 5번째 기준으로 추가하려면 PM이 결정한다. 비차단.
- **D7.** `startingWeaponId`가 무효일 때의 처리가 없다. [기본값] 플레이어 기본 스탯 오류(Blocking)로 본다. 비차단.
- **D8.** 해소(2회차, SPEC F18 4회차): 비교 키에 채널이 들어갔다. (4회차) 키 순서는 SPEC 5회차 F18 "(채널, ID) 순 정렬"에 맞춰 `(스텝, 채널, 객체 ID 쌍, 종류)` = `CollisionEvent.CompareTo`로 통일했다(TAC-F18-8). 자석 별도 ID는 로그 가독성 때문에 유지한다.
- **D9.** 해소(2회차, SPEC F19 4회차 + PM 방향): 배속 상한을 3x로 낮췄다. catch-up 상한 3은 유지하고 오버레이에 실제 배속을 표시한다(5.15.1). U23은 실측 항목으로 남는다. (10회차 변경, 사용자 확정 2026-11-05) U23 실측(`maximumDeltaTime`은 스케일 전 시간에만 걸려 timeScale 3에서 프레임당 최대 7스텝)으로 전제를 고쳐, 배속을 바꿀 때 `Time.maximumDeltaTime = 0.05f / timeScale`로 동기 갱신한다(1x면 0.05). 스케일 후 프레임 시간 상한 0.05초 = 3스텝이 유지되고, 느린 프레임에서는 게임 시간이 덜 흐르며 기존 "실제 배속" 표시가 그 차이를 보여 준다(5.3, 5.15.1, TAC-F19-6). 버린 대안: `SimulationClock`이 프레임당 스텝 호출을 세어 3을 넘는 호출을 무시하는 방식(사용자 결정으로 채택하지 않음).
- **D10.** 해소(2회차, SPEC 3.0 B 4회차): 타깃 탐색만 즉시, 풀링·틱·충돌은 다음 판 시작 시 적용.
- **D11.** 해소(2회차, SPEC 3.0 I·F3 4회차): 몸통 접촉 다수 = 최대 피해 1회, 같으면 작은 ID.
- **D12.** 해소(2회차, SPEC 3.0 I·F3 4회차): 무적 중 M3 투사체는 피해 없이 소모.
- **D13.** 해소(2회차, SPEC 3.0 I·F6 4회차): 쿨다운 기록은 무기 인스턴스가 대상별로 갖는다.
- **D14.** 해소(7회차, 사용자 확정 2026-11-05, SPEC F2 7회차): P1은 모든 무기의 쿨다운(발사·생성 간격) — W1·W3·W4·E1 쿨다운, W2·E2 궤도 원 피해 틱 간격 — 에 적용하고 W3 장판 내부 틱 간격에는 적용하지 않는다(TAC-F2-3, R13, 5.12). 원래 기록: P1 "모든 무기의 공격 간격"이 W2 틱 간격과 W3 장판의 틱 간격에도 적용되는지 정해져 있지 않다(SPEC 4회차에서도 그대로). [기본값] W1·W3·W4의 쿨다운과 W2 틱 간격에 적용하고, W3 장판 내부 틱 간격에는 적용하지 않는다. 비차단.
- **D15.** SPEC F18의 "구현 B 후보는 공간 해시로 추리고 전수 비교하지 않는다"를 플레이어 1명이 한쪽인 채널에서 어떻게 해석할지가 애매하다. [기본값] 몬스터 채널은 공유 몬스터 인덱스, 젬·상자는 픽업 전용 해시, 적 투사체·적 장판은 선형 순회. 비차단.
- **D16.** SPEC의 "젬 ↔ 플레이어" 판정 하나가 이 설계에서는 채널 두 개(자석 Enter = 끌림 시작, 몸통 Enter = 획득)로 나뉜다. 비차단(정보 공유).
- **D17.** 해소(2회차, RUN_LOG 2026-10-16 D17 확정 + SPEC F11 4회차): Newtonsoft 패키지. 6000.3 가용성은 U25.
- **D18.** 해소(2회차, SPEC F3 4회차): SPEC이 화면 기준을 월드 단위(orthographic size 9, 반대각 18.36, 여유 1.64)로 고쳤다. 반지름 1.64 이상 몬스터의 모서리 노출은 2.4에 정보로 남긴다.
- **D19.** 개발자 모드 "레벨업 비활성" 상태에서 경험치를 어떻게 처리할지 정해져 있지 않다. [기본값] 경험치는 쌓이고 레벨은 오르지 않는다. 비차단.
- **D20.** 해소(2회차, SPEC F3·F16 4회차): 몬스터는 플레이어 위치를 직접 참조하고 `ITargetProvider`는 무기 전용.
- **D21.** 개발자 오버레이의 문자열 언어: CODING_GUIDE §3은 "사용자에게 보이는 문자열은 한국어"라고 한다. [기본값] 한국어로 쓰고 같은 한글 글꼴을 쓴다. 비차단.
- **D22.** 해소(2회차, SPEC 3.0 H 4회차): "ID 발급과 세대 증가 규칙은 풀 구현 A/B/C 어느 것을 쓰든 같아야 한다" — 풀과 독립된 `EntityHandleAllocator`가 그 요구를 충족한다.
- **D23.** 상자 효과 ②의 강화 대상 선택 방식이 "랜덤"으로만 적혀 있다. [기본값] 슬롯 순서 목록에서 균등. 비차단.
- **D24.** 해소(2회차, SPEC F6 4회차·Q46②): W3 대상 없음 = 생성 안 함, 쿨다운 미소비, 다음 스텝 재시도. (9회차) `nearestEnemy`에만 해당, `placement: player`는 TAC-F6-5.
- **D25.** 해소(7회차, 사용자 확정 2026-11-05): **uGUI + CanvasScaler + TextMeshPro**로 확정하고 개발자 오버레이는 IMGUI(5.15) 그대로 둔다. "웨이브 4 전" 재검토 조건과 UI Toolkit 복귀 여지는 없어졌다. 원래 기록: (2회차 신규) SPEC F8 4회차는 "UI는 Canvas Scaler로 스케일"이라고 적었는데 Canvas Scaler는 uGUI 컴포넌트다. 1회차 TECH_SPEC은 UI Toolkit을 골랐다. [기본값] SPEC 문구대로 **uGUI + CanvasScaler + TextMeshPro**로 바꾸고 UI를 코드(`UiFactory`)로 구성한다. 영향: IMP-FLOW 파일 목록, 5b(TMP Essentials·폰트 에셋), U18·U42. UI Toolkit의 PanelSettings "Scale With Screen Size"로도 같은 효과를 낼 수 있으므로, SPEC 문구를 "해상도 독립 스케일"로 넓히는 쪽을 PM이 고르면 UI Toolkit으로 되돌릴 수 있다. 비차단.
- **D26.** 해소(3회차, PM 승인: 세대 유일성은 한 판 안에서 보장하고, 판 리셋 때 할당기를 초기화해 세대가 1로 돌아간다. SPEC 3.0 H에 이 문구를 추가할 예정 → (8회차 확인) 반영됨: `spec/00-common.md` 3.0 H 본문 "세대 유일성은 한 판 안에서만 보장된다 … (TECH_SPEC D26)"와 5회차 수용 기준). 근거는 같은 시드 재실행의 ID 동일성(결정론 비교 전제)이며, ID로 키를 잡는 기록은 리셋에서 모두 지워지므로 판을 넘는 오적용은 없다(5.1).
- **D27.** 해소(8회차 확인, SPEC F18 5회차가 이미 반영: `spec/F18-collision-strategy.md` "(5회차 수정) … 타깃 탐색 구현 C를 함께 쓰는 조합에서는 몬스터 `Collider2D`가 질의 전용으로 켜지며 … 젬은 항상 없거나 비활성 (TECH D27)"). 원래 기록: (2회차 신규) SPEC F18 수용 기준 "구현 B에서 경험치 젬과 몬스터에는 `Collider2D`가 없거나 비활성"은 F16 C(`Physics2D` 쿼리)를 함께 켠 조합에서 몬스터 콜라이더를 켜야 한다는 F18 요구사항 본문과 부딪힌다. [기본값] 요구사항 본문을 따른다: 자체 충돌 + Physics2D 타깃 조합에서만 몬스터 콜라이더가 켜지고, 젬은 항상 꺼진다(R19). 비차단.
- **D28.** (2회차 신규) SPEC F10 항목 4 "동시 몬스터 수 한계"는 A0 스윕 전체의 결과인데 측정 파일은 1회 단위다. [기본값] A0 행마다 `avgFps < 55`면 그 N, 아니면 `미도달`을 쓰고, N\*(가장 작은 도달 N)는 사용자가 devlog에 적는다. A·B·C는 `해당 없음`. 비차단.
- **D29.** 해소(8회차 확인, SPEC F10·F19 5회차가 이미 반영: `spec/F10-measurement.md`·`spec/F19-dev-mode.md` "(5회차 수정) … 측정 시작을 누르면 나머지 조건을 검사해 … 통과하면 오버레이를 자동으로 숨기고 시작 … 다시 열면 그 세션은 무효"). 원래 기록: (2회차 신규) SPEC F10·F19 4회차 "오버레이가 켜져 있으면 측정을 시작하지 않는다"와 "측정 시작은 오버레이에서 조작"이 맞물린다. [기본값] "측정 시작" 버튼은 나머지 조건(시각화·로그·배속·한 스텝 모드)을 검사해 거부하거나, 통과하면 **오버레이를 스스로 숨기고** 시작한다. 측정 중 오버레이를 다시 열면 세션은 무효가 된다(5.15.2). 비차단. (7회차) PM 확인 완료(2026-11-05), 기본값 유지.
- **D30.** (2회차 신규) "엔진 충돌 + Physics2D 타깃" 조합에서는 BuildIndex 동기화 때문에 몬스터가 텔레포트로 움직여 Continuous 스윕이 몬스터에 적용되지 않는다. [기본값] E1·E2는 타깃을 공간 해시로 두고 돌린다. 측정 묶음 C의 Physics2D 회차는 충돌이 기준선(엔진)이라 이 조합에 해당하지만, C는 CCD 끔(Discrete)이라 판정에는 영향이 없다고 본다(U6과 함께 확인). 비차단.
- **D31.** (2회차 신규) SPEC F19 4회차 "개발자 모드 코드를 제거한 빌드와 1회 비교"를 위해 의존 방향을 Dev → App으로 뒤집고, Dev 어셈블리에 `defineConstraints: ["!SURVIVOR_NO_DEV"]`를 둔다. 그 결과 인자 확인 1회가 App이 아니라 Dev의 `RuntimeInitializeOnLoadMethod`에서 일어난다(SPEC "시작 시 인자 확인 1회"는 그대로 충족). 비차단(U40 실측 필요). (7회차) PM 확인 완료(2026-11-05), 기본값 유지.
- **D32.** (2회차 신규) SPEC 3.0 I는 "몸통 접촉 적이 여럿이면 최대 피해 1회"만 정했다. 같은 스텝에 몸통 접촉·M3 투사체·M4 장판이 섞이면? [기본값] 세 종류 후보 전체에서 최대 피해 1회, 같으면 작은 ID(무적 시간이 0이어도 스텝당 최대 1회). 비차단.
- **D33.** (2회차 신규) SPEC F8 "작업 영역에 들어가는 가장 큰 16:9 해상도"가 창 테두리·제목 표시줄을 포함한 바깥 크기 기준인지 정해져 있지 않다. [기본값] 바깥 크기 기준(클라이언트 크기 + 16×39 픽셀 여유). 1080p 모니터에서는 1600x900이 고르게 된다. 비차단.

**(14회차) 미결 설계 항목 — PM 결정 대기 (D 번호 없음, 결정은 PM·사용자가 DECISIONS에서)** → (15회차) 해소(아래 마지막 항목)

- **풀 전략 토글 시 옛 전략의 비활성 인스턴스 잔존.** 코드에서 확인됨(웨이브 2a, `Strategies/Pool/*`): 풀 전략 토글(TAC-F9-7, 다음 판 `Reset`에서 적용) 때 옛 A(`CustomPoolStrategy`, 템플릿별 `Stack`)·C(`UnityObjectPoolStrategy`, 템플릿별 `ObjectPool`) 객체가 보유한 비활성 인스턴스가 씬에 그대로 남는다. `ReleaseAll`은 활성 인스턴스만 반환하며, `IPoolStrategy`(5.6)에는 비활성 인스턴스를 정리하는 수단이 없다. 후보: (a) `IPoolStrategy`에 `Dispose`/`Clear` 추가(Core 인터페이스 변경), (b) App이 전략 교체 때 옛 전략의 비활성 인스턴스를 씬에서 파괴, (c) 판마다 누적 허용(측정 오염 가능). 14회차는 결정하지 않는다 — PM이 사용자와 결정한 뒤 15회차에 반영한다. 차단 여부: LOOP·FLOW의 `RunSession.Reset` 전략 교체 구현(웨이브 2b·4) 전에 정해지는 것이 바람직하다(5.6 각주).
- **(15회차) 해소 — DECISIONS 2절 "풀 전략 토글 시 옛 풀의 비활성 인스턴스 정리"(2026-11-06, 사용자 확정, 후보 (a)).** `IPoolStrategy`에 `void Clear();`를 추가했다 = "이 전략이 보유한 비활성 인스턴스를 전부 파괴"(A: 템플릿별 스택, C: `ObjectPool.Clear()`, B: 할 일 없음), 활성 인스턴스가 남아 있으면 `InvalidOperationException`. 호출자는 `EntityLifecycle.ReplaceStrategies`이며 풀 객체가 바뀔 때만 옛 풀을 `Clear()`한 뒤 교체하고, 새 풀 `Prewarm`은 App이 한다. 반영 위치: 5.6(코드 블록)·5.6.1(`Clear` 구현)·5.10(`ReplaceStrategies`)·TAC-F8-2 ③·TAC-F9-7 각주·5c.2 IMP-CORE·IMP-POOL 행. 토글 전후 비활성 객체 수·메모리 실측은 App 조립 뒤 할 일로 남는다(TAC-F9-7).

### 6.2 실측/실행 필요 — 미확정 (Needs measurement/execution — unconfirmed)

아래는 모두 정적으로 읽어서는 확정할 수 없는 Unity 동작 전제다(2.3 표와 번호가 같다). **사실로 취급하지 않는다.** 웨이브 0 스파이크나 해당 구현자의 PlayMode 테스트로 확인한다. (10회차) "확인됨"·"틀림"·"부분 확인" 표기 항목은 웨이브 0 실측(Unity 6000.3.28f1 에디터 PlayMode, 2026-11-05, `docs/spike/results/2026-11-05-editor-run2.txt`)의 결과다. (12회차) U35·U36은 빌드 실측(Windows 플레이어, 2026-11-06, `docs/spike/results/2026-11-06-build-run1.txt`)의 결과다. (14회차) U20·U21·U22·U25는 웨이브 2a 구현자 테스트(Unity 6000.3.28f1, 2026-11-06 — IMP-DATA EditMode, IMP-POOL batchmode `-nographics` PlayMode)의 결과다.

- **U1** 확인됨(10회차 실측 2026-11-05) — `DefaultExecutionOrder`가 `FixedUpdate`에 적용됨(-100 두 인스턴스 → +100 두 인스턴스, 매 스텝). 같은 클래스 인스턴스 간 순서는 생성 순으로 관찰됐을 뿐 보장 아님(설계대로 의존하지 않음). 원래 질문: `DefaultExecutionOrder`가 `FixedUpdate`에 적용되는지, 인스턴스 간 순서가 보장되지 않는지. 틀리면 F17 A 모드의 단계 순서를 다시 설계해야 한다. **IMP-LOOP 착수 전 확인 권장 → 확인 완료(10회차).**
- **U2** 확인됨(10회차 실측 2026-11-05) — 실제 정지 프레임(`Thread.Sleep(100)` ×5) 뒤 프레임당 `FixedUpdate` 최대 3회, `Time.deltaTime`은 0.05로 클램프. **주의**: `Time.captureDeltaTime = 0.1`로 흉내 낸 긴 프레임은 `maximumDeltaTime` 클램프를 우회해 6스텝이 돌았다 → 상한 검증 테스트는 실제 정지 프레임으로 한다(TAC-0-3). 원래 질문: `maximumDeltaTime = 0.05`에서 프레임당 정확히 최대 3스텝인지. 틀리면 SPEC의 "0.05로 설정"을 고쳐야 하므로 PM에게 보고한다.
- **U3** **(15회차) 확인됨(IMP-LOOP PlayMode 배치 2026-11-06)** — `captureDeltaTime` 1/30·1/60·1/144 각 600스텝에서 상태 해시 일치(CentralTick·PerObject 7회 모두 `0x5928B7C20FEF59D6`, 5.16), 아래 "1/30·1/144는 아직 보지 않았다"가 해소됐다. catch-up 상한 검증에 못 쓴다는 주의(U2)는 그대로다. 이전 기록: 부분 확인(10회차 실측 2026-11-05, 미확정에 남김) — `captureDeltaTime`으로 프레임 길이를 고정하면 `FixedUpdate` 누적이 그에 맞게 동작(1/60에서 1스텝, 0.1에서 6스텝 관찰). 프레임레이트 결정론 테스트(TAC-0-4)에는 쓸 수 있으나 catch-up 상한 검증에는 못 쓴다(U2 주의). 1/30·1/144는 아직 보지 않았다. 원래 질문: `Time.captureDeltaTime`으로 프레임레이트 결정론 테스트를 할 수 있는지.
- **U4** `Rigidbody2D` Collision Detection 기본값이 `Discrete`인지(SPEC Q39).
- **U5** 확인됨(10회차 실측 2026-11-05) — Kinematic–Kinematic(`useFullKinematicContacts`) 트리거 Enter·Exit 모두 수신. 기본 sleepMode(StartAwake)와 NeverSleep에서 같다 → sleepMode 변경 불필요, Dynamic 대안 불필요. 원래 질문: 키네마틱-키네마틱 트리거 콜백 발생 조건. 안 되면 Dynamic + gravityScale 0 바디로 바꾼다. **IMP-COLLISION 착수 전 필요 → 확인 완료(10회차).**
- **U6** `Continuous`가 트리거·키네마틱 바디에 영향을 주는지. **E2가 "자명하게 동일"로 끝나 SPEC이 의도한 검증이 되지 않을 가능성이 있다.** 그렇다면 그 사실 자체를 devlog 소재로 기록하고, E2의 의미를 PM이 다시 정해야 한다.
- **U7** 확인됨(10회차 실측 2026-11-05) — 트리거 콜백이 `Physics2D.Simulate()` 호출 안에서 같은 스텝에 동기 발생(callback_step == position_set_step). 원래 질문: `simulationMode = Script`에서 `Physics2D.Simulate()` 호출 중 트리거 콜백이 동기적으로 오는지. 틀리면 엔진판 이벤트가 한 스텝 늦어 E1이 스텝 번호에서 갈린다. **IMP-COLLISION 착수 전 필요 → 확인 완료(10회차).**
- **U8** 확인됨(10회차 실측 2026-11-05) — `callbacksOnDisable = false`에서 겹친 콜라이더를 `enabled = false`로 끄면 Exit 없음. 원래 질문: `callbacksOnDisable = false`이면 비활성화 시 Exit이 나오지 않는지.
- **U9** 확인됨(10회차 실측 2026-11-05, 규칙 확정) — r = 0.5 + 0.5에서 d = 0.9999 Enter, d = 1.0001 Enter 없음, **d = 1.0(정확히 접촉) Enter 발생**(이때 `Physics2D.Distance`는 distance = 0, isOverlapped = false). Box2D 트리거는 "거리 ≤ 반지름 합"(접촉 포함)으로 판정하므로 자체 판정 R5 `≤`와 같은 쪽이다(5.9). ±ε 별도 집계 정책은 불필요. 원래 질문: Box2D 원-원 경계 판정과 float 차이 때문에 E1 경계 사례에서 불일치가 생길 수 있는지. 생기면 허용 정책(경계 ±ε 사례는 별도 집계)을 PM이 정한다. U30과 함께 본다.
- **U10** `MovePosition`이 스윕 이동인지, `transform` 직접 쓰기가 텔레포트로 처리되는지.
- **U11** 부분 확인(10회차 실측 2026-11-05; 11회차 정정, 미확정에 남김 — 10회차에는 "확인됨"으로 적었다. `MovePosition` 반영 시점만 확인. 콜라이더 활성화 직후 `OverlapCircle` 반영은 미확인 → 웨이브 3 IMP-COLLISION EM 테스트로 확인) — `MovePosition`은 `autoSyncTransforms` 값과 무관하게 `Simulate` 뒤에만 `OverlapCircle`에 반영된다(Simulate 전 쿼리는 옛 위치). 원래 질문: `Rigidbody2D.position`·콜라이더 활성화가 `OverlapCircle`에 언제 반영되는지(`autoSyncTransforms` 기본값 포함).
- **U12** 이름 없는 레이어 인덱스에 `IgnoreLayerCollision`을 코드로 설정할 수 있는지.
- **U13** Windows 플레이어에서 `StreamingAssets` 동기 읽기와 빌드 후 편집이 되는지.
- **U14** `ProfilerMarker`가 비개발 빌드에서 제거되는지, `ProfilerRecorder`가 Development Build에서 마커 프레임 합계를 주는지, 스크립트를 넘나드는 `Begin`/`End`가 허용되는지. 안 되면 틱 ms 수집을 Stopwatch 주입 방식으로 바꾼다(Q40의 "비활성 시 감시 없음"과 충돌하는지 PM 확인 필요). (15회차) 부분 — 구현은 콜백을 가로지르는 `Begin`/`End`를 쓰며(`Survivor.Step` Early → Late, `Survivor.Tick.Monsters` Early → Mid, `Survivor.Tick.Projectiles` Mid → Late, 5.3) batchmode 배치 테스트에서 오류가 없었다. 프로파일러·`ProfilerRecorder`를 붙였을 때 "Non matching Begin/EndSample" 경고 여부와 계층 표시는 측정 전 Dev·사용자가 확인한다(미확정 유지).
- **U15** "GC Allocated In Frame" 카운터 이름과 가용성.
- **U16** `Environment.GetCommandLineArgs()`에 `--dev`가 들어오는지.
- **U17** Input System 활성 여부, 업데이트 모드, `wasPressedThisFrame` 동작. (15회차) 부분 — `KeyboardInputSource`의 방향 계산·반환 규칙은 EM 테스트로 확인했고, 래치(`wasPressedThisFrame`)·실제 키 입력·게임패드 무반응은 App 조립 뒤 수동 확인(미확정 유지).
- **U18** (2회차 수정) TMP Dynamic Font Asset(uGUI)과 IMGUI `Font`의 한글 렌더링.
- **U19** 대체됨(2회차) — UI Toolkit 미사용(D25). U42로 대체. 집계에서 뺀다.
- **U20** 확인됨(14회차, IMP-DATA EditMode 배치 2026-11-06) — `DefaultShapeFactory.Circle`이 EditMode 배치에서 생성된다. 64×64 `Texture2D`, 스프라이트 `bounds` 1×1 유닛(픽셀/유닛 64), 중심 불투명·모서리 투명. 반지름 → 표시 배율은 `bounds.size`(1×1) 기준으로 계산할 수 있다. 원래 질문: 런타임 생성 원 스프라이트의 월드 크기 계산.
- **U21** 확인됨(14회차, IMP-POOL PlayMode 배치, Unity 6000.3.28f1) — `ObjectPool<GameObject>` `collectionCheck: true`에서 중복 반환은 `System.InvalidOperationException`("Trying to release an object that has already been released to the pool.")이다. 설계가 요구한 타입과 같아 감쌀 필요가 없고 `CountInactive`도 유지된다. `collectionCheck: false`면 예외 없이 `CountInactive=2 CountAll=1 CountActive=-1`로 깨지고 이후 `Get` 2회가 같은 객체를 준다. **풀에서 나오지 않은(남의) 객체 반환은 `collectionCheck: true`여도 잡지 못한다**(`CountInactive=1 CountAll=0 CountActive=-1`) → 자체 Owner 검사가 필수이며 구현은 `PoolInstanceTracker`(5.6)다. `maxSize: int.MaxValue`에서 1000개 반환 시 `CountAll=1000 CountInactive=1000`, `actionOnDestroy` 0회. 대조군 `maxSize: 2`에 3개 반환 시 `CountInactive=2`, `actionOnDestroy` 1회(초과분 파괴 확인). 재사용 순서는 LIFO도 FIFO도 아니지만 결정적이다(관찰 패턴 `0,1,2,1,0,3,2,3`; A 자체 풀은 LIFO `0,1,2,0,1,3,3,1`). 이 순서가 swap-remove 모델과 일치한다는 것은 구현자 추론이다(Unity 소스 미확인). (남은 미확인: `collectionCheck`의 비용 — 반환마다 O(n) 탐색인지 미측정. F10 측정에서 C에 불리하게 작용할 수 있다.) 원래 질문: `ObjectPool<T>`의 `maxSize`·`collectionCheck` 동작.
- **U22** 확인됨(14회차, IMP-POOL PlayMode 배치) — B(`InstantiatePoolStrategy`)의 `Release`는 `SetActive(false)` 뒤 `Destroy`다. 파괴는 **정확히 1프레임 뒤**(`yield return null` 1회) 적용되며 3회 시행이 같았다. 같은 프레임에는 객체가 남아 있지만 이미 `activeSelf=false`라 `FindObjectsByType<PooledObject>(FindObjectsInactive.Exclude)`에 잡히지 않는다 → 같은 프레임 다음 스텝에 간섭하지 않으며 "비활성화 먼저" 설계가 유효하다. `ReleaseAll`도 1프레임 뒤 전부 파괴된다. 측정 조건은 batchmode `-nographics` PlayMode 테스트 코루틴이다(플레이어 빌드에서는 미확인). 원래 질문: `Destroy` 지연과 같은 프레임 다음 스텝 간섭 여부.
- **U23** **틀림(10회차 실측 2026-11-05) → D9 변경으로 반영, 해소** — `maximumDeltaTime`은 **스케일 전** 시간에만 걸린다. timeScale = 3·targetFrameRate 60·vSync 0에서 60프레임 평균 3.02스텝이지만 최대 7스텝, 스케일된 deltaTime 최대 0.116(비스케일 0.039). → 배속 시 `maximumDeltaTime = 0.05 / timeScale` 동기 갱신(D9, 5.3·5.15.1·TAC-F19-6). 원래 질문: `maximumDeltaTime`이 스케일된 시간을 제한하는지(60FPS·3x = 프레임당 3스텝).
- **U24** (2회차 수정) 자동 선택한 16:9 창이 작업 표시줄·DPI 배율로 강제 축소되지 않는지.
- **U25** 확인됨(14회차, IMP-DATA) — `com.unity.nuget.newtonsoft-json` 3.2.2가 6000.3.28f1 에디터(Mono)에서 동작한다. `Packages/manifest.json`에 직접 의존으로 추가됐다(2026-11-06, 사용자 승인, DECISIONS D17 이력). 패키지가 autoReferenced라 `Survivor.Data.asmdef`에 명시 참조 없이 컴파일된다. **단 테스트 asmdef(`overrideReferences: true`)는 Newtonsoft를 보지 못한다** — Data 테스트는 JSON 픽스처를 문자열 치환으로 변형해 우회한다(5c.1·5c.5). 원래 질문: Newtonsoft 패키지의 6000.3 가용성과 Mono 동작.
- **U26** 같은 빌드·같은 PC에서 float 결과가 실행마다 같은지(결정론 전제 전체).
- **U27** batchmode에서 물리를 포함한 PlayMode 테스트가 도는지.
- **U28** 6.3에서 기존 `Physics2D` API가 그대로 동작하는지.
- **U29** `SpriteRenderer` Tiled 모드로 반복 배경을 그릴 수 있는지(URP 2D 포함).
- **U30** 확인됨(10회차 실측 2026-11-05) — `defaultContactOffset = 0.0001` 설정·읽기 일치. 경계 효과는 U9에서 함께 확인(접촉 포함 판정). 원래 질문: (2회차 신규) `Physics2D.defaultContactOffset`을 0.0001 근처로 낮출 수 있는지, 그 값이 트리거 겹침 경계에 주는 영향. 낮출 수 없으면 E1 경계 사례 허용 정책(U9)이 필요하다. **IMP-COLLISION 착수 전 확인 권장 → 확인 완료(10회차).**
- **U31** 확인됨(10회차 실측 2026-11-05) — `FixedUpdate`(order 1000) 안에서 `AddComponent`한 컴포넌트와 `enabled = true`로 켠 기존 컴포넌트 모두 **같은 프레임의 다음 스텝**부터 `FixedUpdate`가 호출됨 → F17 A 셀프 티커 설계 유지, 대안(항상 켜 두기) 불필요. 원래 질문: (2회차 신규) `FixedUpdate` 도중 켠 셀프 티커가 같은 프레임의 다음 `FixedUpdate`부터 호출되는지. **틀리면 F17 A 모드에서 catch-up 프레임마다 새 엔티티의 첫 Tick이 밀려 프레임레이트 결정론이 깨진다.** 그 경우 대안은 셀프 티커를 생성 시점과 무관하게 항상 켜 두고 `IUpdatable` 연결 여부로만 거르는 것이다(측정 비용 특성이 바뀌므로 PM 판단). **IMP-LOOP 착수 전 확인 권장 → 확인 완료(10회차).**
- **U32** 확인됨(10회차 실측 2026-11-05) — Script 모드에서 `rb.position` 직접 대입은 `Simulate`·`SyncTransforms` 없이 즉시 `OverlapCircle`에 반영(`queriesHitTriggers = true`로 트리거 검출). `transform.position` 대입은 `SyncTransforms`가 필요하다(설계는 `rb.position`을 쓴다). 원래 질문: (2회차 신규) Script 모드에서 `rb.position` 직접 대입이 `Simulate` 없이 쿼리에 반영되는지, `queriesHitTriggers`로 트리거를 찾는지. **IMP-COLLISION·IMP-SPATIAL(C 구현) 착수 전 확인 권장 → 확인 완료(10회차).**
- **U33** (2회차 신규) URP 2D에서 `endCameraRendering` + GL 그리기가 보이는지. 안 되면 `LineRenderer` 풀 또는 `OnRenderObject` 재확인.
- **U34** (2회차 신규) URP 2D 기본 스프라이트 머티리얼의 Global Light 2D 의존과 코드 생성 `Light2D`. 안 되면 레지스트리에 Unlit 머티리얼 참조를 두는 방식으로 바꾼다.
- **U35** (2회차 신규) `Screen.mainWindowDisplayInfo.workArea`의 가용성·단위와 `SetResolution` 크기 의미. (10회차: 빌드 측정 대기 — 에디터 값은 Game 뷰 크기라 무의미) **(12회차) 확인됨** — `workArea`는 물리 픽셀(배율 100% 모니터에서 2560x1392 = 1440 − 작업 표시줄 48), `SetResolution(1280,720,Windowed)` 뒤 클라이언트 1280x720. 배율 125%·150% 환경은 미측정(5.18의 물리 픽셀 판정 전제는 유지).
- **U36** (2회차 신규) "Allow Fullscreen Switch" 끔이 Alt+Enter를 막는지. (10회차: 빌드 측정 대기) **(12회차) 확인됨** — 빌드에서 Alt+Enter 뒤 창·`fullScreenMode` 변화 없음.
- **U37** (2회차 신규) 창 모드 포커스 이벤트와 런타임 `runInBackground` 전환.
- **U38** (2회차 신규) 메모리 카운터 이름·가용성(Development/릴리스).
- **U39** (2회차 신규) vSync·targetFrameRate 런타임 설정이 창 모드에서 지켜지는지, `GarbageCollector.isIncremental` 읽기.
- **U40** (2회차 신규) `RuntimeInitializeOnLoadMethod(AfterSceneLoad)` 시점과 `defineConstraints`로 Dev 어셈블리(와 Dev 테스트 어셈블리)가 깨끗하게 빠지는지. **IMP-DEV 착수 전 확인 권장.**
- **U41** (2회차 신규) Mono의 UTF-8 디코딩이 잘못된 바이트를 U+FFFD로 바꾸는지(CP949 감지 전제).
- **U42** (2회차 신규) `InputSystemUIInputModule` 메뉴 탐색과, UI 클릭 콜백·`Update` 사이 순서(보장 안 된다고 가정했으므로 틀려도 설계는 안전).

### 6.3 집계 (8회차 갱신, 10회차 U 갱신, 11회차 U 재조정, 14회차 U 갱신, 15회차 U 갱신)

- 문서 간 불일치/SPEC 미규정: 번호 **33개**(D1~D33). **해소 18건**(D3·D8·D9·D10·D11·D12·D13·D17·D18·D20·D22·D24, (3회차) D26, (7회차) D14·D25, (8회차) D5·D27·D29), **미해소 15건**. **blocking 0건** — 모두 [기본값]이 있다. PM 확인 권장: (7회차) 없음 — D25는 사용자 확정으로 해소, D29·D31은 PM 확인 완료(2026-11-05, 기본값 유지). (4회차 변화 없음)
- 실측/실행 필요 (10회차 갱신, 11회차 재조정): 번호 **42개**(U1~U42) 중 U19는 대체, (10회차 실측 2026-11-05) **확인·해소 10건**(확인됨 9건 U1·U2·U5·U7·U8·U9·U30·U31·U32 + 틀림 → D9 변경으로 반영 U23), **부분 확인 2건**(U3, (11회차 정정) U11 — 미확정에 남김), 남은 **미확정 31건**(부분 확인 2건과 U35·U36 빌드 측정 대기 포함). (12회차) 빌드 실측 2026-11-06으로 U35·U36 확인됨 → 확인됨 11건, 남은 **미확정 29건**(부분 확인 2건 포함). (14회차) 웨이브 2a 구현자 테스트 2026-11-06으로 U20·U21·U22·U25 확인됨 → **확인됨 15건**, 남은 **미확정 25건**(부분 확인 2건 포함). (15회차) IMP-LOOP PlayMode 배치 2026-11-06으로 U3 확인됨(부분 확인 → 확인) → **확인됨 16건**, 남은 **미확정 24건**(부분 확인은 U11 1건만 남음. U14·U17은 (15회차) 부분 괄호를 달았으나 미확정 유지). 6.1 끝의 (14회차) 미결 설계 항목(풀 전략 토글 잔존)은 U·D 집계에 넣지 않는다 — (15회차) 해소됨. (3회차·7회차·8회차까지는 미확정 41건, 10회차 표기는 30건이었으나 U11을 확인으로 잘못 셌다.) 형식상 blocking은 없다. **착수 전 확인 권장**: IMP-LOOP 전 U1·U2·U31 → 확인 완료, IMP-COLLISION 전 U5·U7·U8·U30·U32 → 확인 완료, (7회차) IMP-SPATIAL(C 구현) 전 U32 → 확인 완료, IMP-DEV 전 U40(미확인, 남음)(웨이브 0 스파이크 (a)~(j) 중 (i) U35·(j) U36은 빌드 측정 대기 → (12회차) 확인 완료, 웨이브 0 전부 종료). U6은 E2의 의미에, U31은 F17 A의 결정론 성립 자체에 영향을 준다.
