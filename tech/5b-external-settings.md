> 분할 파일 — 원본 `TECH_SPEC.md`(5회차)를 2026-10-27에 제목 단위로 나눈 것이다. 내용은 원본 그대로이며 색인·개정 이력은 `tech/README.md`에 있다. 수정은 tech-spec-writer만 한다.

## 5b. 외부 서비스 설정 점검표 (External Service Checklist)

인증·스토리지 같은 외부 서비스 연동은 **없다**. 대신 사용자만 할 수 있는 에디터·빌드 설정과 GitHub Releases 업로드가 F13·F15에 걸려 있어 정리한다. (2회차) URP 템플릿, 창 설정, 측정 환경, 비교 빌드를 추가했다.

| 설정 (사용자 수행) | 없으면 생기는 일 | 증상 | 확인 상태 |
|---|---|---|---|
| Unity 6000.3.26f1 + Windows Build Support 모듈 (F15) | 빌드 불가 | Build Profiles에 Windows가 비활성 | 실측 필요 |
| **Universal 2D(URP) 템플릿**으로 `indiv/Survivor/` 생성 (F15 4회차, 5c.1) | 구현자가 파일을 둘 경로가 없다. Built-in이면 U33·U34 전제가 달라진다 | - | 사용자 결정 필요(폴더명) |
| Player Settings > Resolution: Fullscreen Mode = **Windowed**, Default Screen 1280x720(스크립트 적용 전 초기값), **Resizable Window 끔**, **Allow Fullscreen Switch 끔**(Alt+Enter), Default Is Native Resolution 끔, **Run In Background 끔**(측정 중에만 코드로 켬) | 16:9가 깨지고 Q44 계산이 무효 / Alt+Enter로 전체화면 | 창 크기 변화, 모서리 스폰이 보임 | 실측 U24·U35·U36·U37 |
| Active Input Handling = Input System Package | `Keyboard.current`가 null, uGUI 입력 모듈 오류 | NullReferenceException, 메뉴 키보드 탐색 불가 | 실측 U17·U42 |
| Scripting Backend = **Mono**, 점진적 GC = Unity 기본값 유지 (F10 측정 환경) | 측정 환경이 SPEC과 다름 | 헤더 `scriptingBackend`가 `IL2CPP` | 측정 헤더로 확인 |
| TextMeshPro Essential Resources 가져오기 + 한글 TTF(예: Noto Sans KR, 라이선스 기록)로 TMP Dynamic Font Asset 생성 + IMGUI용 `Font` | 한글이 네모로 표시 | 타이틀·결과·오버레이 문자 깨짐 | 실측 U18 |
| `Assets/Resources/AssetKeyRegistry.asset` 생성(메뉴 Survivor/Asset Key Registry) | 모든 키가 기본 도형으로 대체 + 경고 | 마젠타 원만 보임 | 코드는 폴백 처리 |
| Newtonsoft JSON 패키지 추가(D17 확정) | Data 어셈블리 컴파일 실패 | `JToken` 미정의 | 실측 U25, 승인 게이트 |
| 빌드 3종: Development Build(Autoconnect Profiler) / 릴리스 / **비교용 `SURVIVOR_NO_DEV` 심볼 빌드**(F19 4회차 1회 비교) | F10 공식 측정 불가 / 코드 제거 비교 불가 | Profiler 미연결, 시스템 ms가 `NA` | 실측 U14·U40 |
| GitHub Releases: 빌드 폴더 전체를 zip(`StreamingAssets` 포함). 배포 설명에 zip 해제 후 실행, Windows 10/11 64비트, SmartScreen 안내, "1280x720 이상 작업 영역"(F13 4회차) | 다른 PC에서 데이터 로드 실패 | 시작 차단 화면 "weapons.json 없음" | F13 테스터 확인 |
| 저장소 공개 전 점검: `persistentDataPath` 덤프·CSV는 저장소 밖이라 커밋되지 않음. 개인 경로가 들어간 로그 파일이 저장소에 있는지 확인 | 개인 정보 노출 | - | 사용자 확인 |

순서: 프로젝트 생성(URP 2D) → Player·입력·백엔드 설정 → Newtonsoft 추가 → TMP Essentials·글꼴 → 웨이브 0 실측 스파이크 → (구현 디스패치) → 레지스트리 에셋 → 빌드 3종 → Releases 업로드.

URP 관련 코드 쪽 처리: `GameBootstrap`이 씬에 Global Light 2D가 없으면 코드로 하나 만든다(강도 1) [실측 U34]. 시각화는 `endCameraRendering` 경로다 [실측 U33].

---

