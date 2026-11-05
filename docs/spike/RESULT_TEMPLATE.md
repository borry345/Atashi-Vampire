# 웨이브 0 실측 결과

- 측정일: 2026-__-__
- Unity 버전: (로그의 `[SPIKE][ENV] unity=` 값)
- 측정 환경: [ ] 에디터 Play   [ ] Windows 빌드 (U35·U36은 빌드 필수)
- 모니터: __대, Windows 배율 ___% , 해상도 ____x____

## 판정 (로그의 `RESULT=` 값을 그대로 옮기고, 한 줄 메모가 있으면 적습니다)

| 번호 | 판정 | 로그의 핵심 값 (detail에서 옮겨 적기) |
|---|---|---|
| U1 | [ ] YES [ ] NO [ ] UNKNOWN | observed_sequences= |
| U2 | [ ] YES [ ] NO [ ] UNKNOWN | actual_max= per_frame= (captureDeltaTime 우회 — 참고값) |
| U2b | [ ] YES [ ] NO [ ] UNKNOWN | actual_max= per_frame= deltaTime= |
| U23 | [ ] YES [ ] NO [ ] UNKNOWN | max= min= avg= max_deltaTime(scaled)= |
| U5 | [ ] YES [ ] NO [ ] UNKNOWN | round0= / round1= (NeverSleep에서만 동작 메모 여부: ) |
| U7 | [ ] YES [ ] NO [ ] UNKNOWN | insideSimulate= callback_step= position_set_step= |
| U8 | [ ] YES [ ] NO [ ] UNKNOWN | exit_total_after_disable= |
| U9 | [ ] YES [ ] NO [ ] UNKNOWN | d=1: enterA= / d=1.0001: enterA= / d=0.9999: enterA= |
| U30 | [ ] YES [ ] NO [ ] UNKNOWN | readback= |
| U11 | [ ] YES [ ] NO [ ] UNKNOWN | autoSync=false: before= after= / autoSync=true: before= after= |
| U31 | [ ] YES [ ] NO [ ] UNKNOWN | steps_in_trigger_frame= new first= pre first= |
| U32 | [ ] YES [ ] NO [ ] UNKNOWN | no_sync= after_SyncTransforms= after_Simulate= |
| U35 | [ ] YES [ ] NO [ ] UNKNOWN [ ] 빌드 미실시 | workArea w= h= / SetResolution 뒤 Screen= x |
| U36 | [ ] YES(막힘) [ ] NO(전환됨) [ ] 빌드 미실시 | Alt+Enter 뒤 `changed` 줄: [ ] 있음 [ ] 없음 |

판정 기준(YES의 뜻)은 `README.md` 5절 표에 있습니다. 모르겠으면 비워 두고 로그만 붙여도
됩니다 — 판정은 PM이 로그로 다시 합니다.

## 컴파일 오류 / 이상 (있을 때만)

```
(Console의 빨간 줄, 경고, DONE이 안 나온 경우의 마지막 TRACE 줄 등을 여기에)
```

## 로그 붙여넣기 — 에디터 Play (`[SPIKE]` 필터 결과 전체)

```
(여기에 붙여넣기)
```

## 로그 붙여넣기 — Windows 빌드 (`Wave0Spike_result.txt` 전체 또는 Player.log의 `[SPIKE]` 줄)

```
(여기에 붙여넣기)
```
