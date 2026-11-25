# 스트레스 데이터 변형 세트 (SPEC F10 14회차 "스트레스 데이터 변형 세트", DECISIONS 2절 2026-11-24)

측정 2차 S 묶음용. 원본 7종 중 **바뀐 파일만** 두었고, 값은 SPEC F10의 [기본값, PM] 표와 같다. 게임 밸런스가 아니며 배포 데이터에 넣지 않는다.

| 폴더 | 파일 | 용도 |
|---|---|---|
| `S1/` | `monsters.json`(M1 체력 1), `weapons.json`(W3 전 범위 장판, 나머지 피해 0.001), `playerStats.json`(획득 반경 200·젬 흡인 속도 10000 = 젬 즉시 회수) | S1 풀링 과부하 본 측정 |
| `S1/` 중 `playerStats.json` 제외 | 위 두 파일만 복사하고 `playerStats.json`은 원본 유지 | S1 젬 미회수 판(`S1-gems`) |
| `S2/` | `monsters.json`(M1 체력 10억), `weapons.json`(W1·W4 50발·0.05초, W2 궤도 30개, W3 전 범위 장판, 피해 전부 0.001) | S2 충돌 과부하 |

## 적용·원복 (에디터 Play 전에, 핫 리로드 금지)
```powershell
cd C:\Users\SSAFY\Desktop\borry\indiv
# 적용 (S1 예)
Copy-Item docs\measurements\stress\S1\*.json Survivor\Assets\StreamingAssets\Data\ -Force
# 원복 (git이 원본을 쥐고 있다)
git checkout -- Survivor/Assets/StreamingAssets/Data/
```
적용 뒤 `git status`에 `StreamingAssets/Data`가 수정으로 보이면 정상이고, 측정이 끝나면 원복해 깨끗해야 한다. **변형 상태로 커밋하지 않는다.**

파일은 2026-11-25에 PM이 원본에서 스크립트로 생성했다(JSON 들여쓰기만 다르고 값은 표와 같다).
