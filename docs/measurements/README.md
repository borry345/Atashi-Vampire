# 측정 데이터 (2026-11-16 ~ 2026-11-18, 사용자 수행)

SPEC F10의 측정 16회 + A0 스윕과 TECH 5c.5의 E1·E2 본 실행 결과다. 측정은 에디터(Development, Dev Mode On Play)에서 개발자 모드 측정 세션(README "측정 절차")으로 수행했고, 파일은 `Survivor.Dev`의 `MeasurementCsvExporter`·`CollisionEventLog`가 쓴 원본을 그대로 복사했다(CRLF·UTF-8 BOM, R28). 프레임 원자료(`frames_*.csv`, 회당 ~450KB)는 저장소에 넣지 않았다.

| 파일 | 내용 |
|---|---|
| `summary.csv` | 유효 측정 47행 전부(시간순). 같은 라벨이 여러 행이면 devlog 집계는 **마지막 유효 행**을 쓴다 |
| `final-rows.csv` | devlog 표에 쓴 행만 추린 것: A0 스윕(N별 1행) + A1~A6·C1~C2(N 1200) + B1~B8 |
| `runs/measurement_<UTC>.csv` | 측정 1회 원본 50개(무효 3건 포함 — 헤더 `valid,false`) |
| `collision/collision_4242_*.csv` | E1·E2 본 실행 충돌 이벤트 로그 3개(시드 4242, Hold 200, ~3,600스텝) |

열 뜻은 `README.md` "측정 절차"의 CSV 열 요약과 `tech/5.15-dev-mode.md` 5.15.3을 본다.

## 요약 표 (PM이 summary.csv에서 뽑음, 2026-11-18)

### 표 1. A0 스윕 (기준 조합, 3600스텝; 200·400 첫 2건은 600스텝)
| N | avgFps | 1% low | worst ms | step ms/스텝 | collision | GC B/프레임 | 젬 평균 | 한계 판정 |
|---|---|---|---|---|---|---|---|---|
| 200 | 614.6 | 276.3 | 4.9 | 0.69 | 0.24 | 10,112 | 136 | 미도달 |
| 400 | 361.7 | 132.9 | 24.7 | 2.08 | 0.86 | 10,661 | 689 | 미도달 |
| 800 | 171.1 | 63.5 | 27.2 | 5.27 | 1.88 | 12,876 | 1,128 | 미도달 |
| 1200 | 66.7 | 28.9 | 43.7 | 8.77 | 2.66 | 62,344 | 1,415 | 미도달 |
| 1400 | 37.1 | 17.5 | 66.1 | 11.07 | 3.07 | 73,950 | 1,617 | 1400 |
| 1800 | 17.0 | 13.3 | 77.7 | 16.03 | 3.86 | 89,642 | 2,374 | 1800 |

N* = 1400 (avgFps < 55가 처음 나온 N; 1200은 66.7로 미도달).

### 표 2. A·C 묶음 @ N 1200 (각 라벨의 N 1200 유효 행)
| 라벨 | 조합 pool/target/tick/collision | avgFps | 1% low | step | pool | targeting | spatialIndex | tickMonsters | collision | GC B/프레임 |
|---|---|---|---|---|---|---|---|---|---|---|
| A1 | InstantiateDestroy/BruteForce/PerObjectFixedUpdate/EngineCollider2D | 60.3 | 25.4 | 9.00 | 0.26 | 0.003 | 0.00 | 0.52 | 2.78 | 63,418 |
| A2 | CustomPool/BruteForce/PerObjectFixedUpdate/EngineCollider2D | 81.4 | 32.3 | 8.54 | 0.11 | 0.003 | 0.00 | 0.53 | 2.64 | 52,655 |
| A3 | InstantiateDestroy/SpatialHash/PerObjectFixedUpdate/EngineCollider2D | 66.8 | 30.4 | 8.81 | 0.25 | 0.001 | 0.14 | 0.51 | 2.63 | 61,687 |
| A4 | InstantiateDestroy/BruteForce/CentralTick/EngineCollider2D | 70.8 | 30.6 | 8.47 | 0.26 | 0.003 | 0.00 | 0.24 | 2.65 | 60,695 |
| A5 | InstantiateDestroy/BruteForce/PerObjectFixedUpdate/SelfSquaredDistance | 73.3 | 29.8 | 6.66 | 0.25 | 0.003 | 0.14 | 0.51 | 0.46 | 60,895 |
| A6 | CustomPool/SpatialHash/CentralTick/SelfSquaredDistance | 89.5 | 43.8 | 6.17 | 0.11 | 0.001 | 0.14 | 0.23 | 0.46 | 52,371 |
| C1 | UnityObjectPool/BruteForce/PerObjectFixedUpdate/EngineCollider2D | 77.2 | 31.1 | 8.65 | 0.11 | 0.003 | 0.00 | 0.53 | 2.67 | 53,266 |
| C2 | InstantiateDestroy/Physics2DQuery/PerObjectFixedUpdate/EngineCollider2D | 54.9 | 26.3 | 9.55 | 0.26 | 0.485 | 0.00 | 0.51 | 2.94 | 65,983 |

### 표 3. 조합별 N 스윕 (사용자가 추가로 돌린 것 — 55 FPS 경계 추정용)
| 라벨 | N → avgFps |
|---|---|
| A1 | 200→500.3, 800→150.9, 1000→104.1, 1100→84.8, 1200→60.3 |
| A2 | 800→164.6, 900→139.7, 1100→98.9, 1200→81.4, 1300→65.1 |
| A3 | 800→152.8, 1000→106.0, 1200→66.8 |
| A4 | 1000→110.0, 1100→89.3, 1200→70.8 |
| A5 | 1000→109.1, 1100→89.8, 1200→73.3 |
| A6 | 1100→102.9, 1200→89.5, 1300→74.0, 1400→59.3 |
| C1 | 200→506.9, 800→149.5, 1000→105.8, 1200→77.2, 1300→61.8 |
| C2 | 200→496.7, 1000→90.8, 1200→54.9, 1300→43.6 |

### 표 4. B 묶음 (전수 vs 공간 해시, 3600스텝)
| N | 전수 avgFps | 전수 step | 전수 targeting | 공간해시 avgFps | 공간해시 step | 공간해시 targeting+spatialIndex |
|---|---|---|---|---|---|---|
| 50 | 568.2 | 0.28 | 0.001 | 579.2 | 0.29 | 0.015 |
| 100 | 504.3 | 0.51 | 0.002 | 528.1 | 0.50 | 0.031 |
| 200 | 423.6 | 0.95 | 0.002 | 417.0 | 1.00 | 0.044 |
| 400 | 295.8 | 2.05 | 0.002 | 289.5 | 2.14 | 0.073 |

마커 밖 비용(A1@1200): step 9.00 − 마커 합 3.57 = **5.44 ms/스텝** (분리 패스 O(n²) 추정, Profiler 미확인).

### 표 5. E1·E2 본 실행 (시드 4242, Hold 200, 자동 경로·무적·레벨업 끔, 타깃 SpatialHash)
| 로그 | 총 이벤트 | step ≤ 3600 | 비교 |
|---|---|---|---|
| Self/Discrete | 2,523 | 2,499 | — |
| Engine/Discrete | 2,528 | 2,499 | E1: Self와 집합 완전 일치(차집합 0) |
| Engine/Continuous | 2,523 | 2,499 | E2: Engine/Discrete와 완전 일치(차집합 0) |

채널 분포(3개 공통): PlayerBodyVsMonster 2,290 · PlayerProjectileVsMonster 95 · PlayerBodyVsGem 57 · PlayerMagnetVsGem 57. 3,600 초과분은 ESC 시점 차이.

측정 환경(헤더): Development 빌드(에디터), Unity 6000.3.28f1, Mono, vSync 0, targetFrameRate -1, CPU Intel(R) Core(TM) i7-14700, GPU NVIDIA GeForce RTX 5060 Ti, RAM 32446 MB, 스텝 0.016666667s, 워밍업 300, 무기 W1:5;W2:5;W3:5;W4:5, 상자 0%, 시드 4242, 체력 배율 1.0;1.5;2.2;3.0.


## 해석의 한계

- 에디터 측정이라 절대값은 빌드보다 느릴 수 있다. 조합 간 **상대 비교**에만 쓴다.
- A·C 묶음은 N* 1400이 아니라 N 1200에서 비교했다(1400에서는 기준 조합이 37 FPS로 이미 한계 아래라 변별력이 낮다. 1200은 기준 60 FPS로 경계 직전).
- `targeting_msPerStep`이 전수·공간 해시에서 0.00x로 매우 작다. 타깃 탐색은 이 규모에서 병목이 아니다.
