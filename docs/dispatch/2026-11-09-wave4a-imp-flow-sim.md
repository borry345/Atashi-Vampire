# 웨이브 4a 디스패치 브리핑 — IMP-FLOW-SIM (2026-11-09, PM 작성, **초안 — 사용자 승인 뒤 디스패치**)

> `general-purpose`, `model: opus`, `isolation: worktree`. "=== 프롬프트 시작 ===" 이후를 그대로 보낸다(또는 worktree 안의 이 파일을 읽게 한다).
> **PM 분할 결정(5c.4 웨이브 4 = IMP-FLOW 1명을 두 디스패치로)**: 4a = Simulation 쪽(`Progression/` 9파일 + `SimulationStateHash`, EditMode 테스트), 4b = App·UI·씬(`GameBootstrap`·`GameFlowController`·`StrategyFactory`·화면·uGUI 뷰·`Main.unity`·asmdef 참조 테스트·PlayMode 흐름 테스트). 4b는 4a 머지 뒤 순차. 이유: 한 에이전트에 40파일 + 씬 + UI는 PLAYER(32파일, 35만 토큰)보다 크고, 4b는 TMP Essentials·한글 폰트(5b, 사용자 몫)가 선행이라 지금 시작할 수 없다. 소유권 맵(5c.2 IMP-FLOW 행)은 바뀌지 않는다 — 같은 구현자 역할을 두 번 부르는 것이다. `SimulationStateHash.cs`는 IMP-LOOP 소유(5.16 "IMP-FLOW와 함께 또는 LOOP 재개")이며 이번에 4a가 작성한다.

=== 프롬프트 시작 ===

# 역할: IMP-FLOW-SIM — 뱀서라이크 1차 MVP의 진행(레벨업·판 세션·결과·상태 해시) 구현자

당신은 Unity 6 / C# 프로젝트의 **`Survivor.Simulation` 어셈블리 `Progression/` 폴더**(경험치, 레벨업 후보·추첨·회차, 스텝 결과 게이트, 판 세션 리셋, 전략 묶음, 최고 기록, 판 결과)와 **`Loop/SimulationStateHash.cs`**(결정론 검증용 상태 해시), 그 **EditMode 테스트**를 구현한다. 당신의 `RunSession.Reset`은 웨이브 1~3이 만든 모든 객체를 판 경계에서 한 번에 초기화하는 **유일한 경로**이고, `StepOutcomeGate`는 매 스텝 사망 > 클리어 > 레벨업을 한 번 판정해 게이트를 닫는다. App(조립 루트·UI·흐름 컨트롤러)은 다음 디스패치(4b)가 만든다 — 당신은 그들이 **부를 메서드와 읽을 속성**을 만들고, 등록·배선 코드는 갖지 않는다(5c.3 #9 메모).

## 1. 이미 확인된 사실 (다시 조사하지 말 것)

- **당신은 자기 전용 git worktree에서 작업한다.** cwd가 worktree 루트, Unity 프로젝트는 `<cwd>\Survivor`. 다른 경로(원본 저장소 `C:\Users\SSAFY\Desktop\borry\indiv` 등)는 읽지도 쓰지도 않는다. 동시에 IMP-SPATIAL C가 자기 worktree에서 `Strategies/Targeting/Physics2DTargetProvider.cs`·`Tests/PlayMode/Targeting/`을 만들고 있을 수 있다 — 당신 worktree에 없는 것이 정상이고 당신과 겹치는 파일은 없다.
- Unity **6000.3.28f1**, 에디터 `C:\Program Files\Unity\Hub\Editor\6000.3.28f1\Editor\Unity.exe`, GUI는 닫혀 있다. worktree의 `Survivor/Library`가 없어 **첫 배치 실행은 3~5분**, 이후 EditMode 15~30초.
- 기준선(main, 2026-11-09): **EditMode 615 / PlayMode 185**, 전부 통과. 당신은 EditMode만 추가한다(PlayMode는 확인용 1회, 185 유지).
- 이미 main에 있고 당신이 **읽기만** 하는 것(**문서와 코드가 다르면 코드가 맞다**):
  - Core: `UpgradeKind`·`UpgradeOption(kind, targetId, isFiller)`, `UpgradeOrdering.Compare`(R12), `ProgressionRules.IsWeaponUpgradable(level, levelCount, evolvedOnly)`·`IsPassiveUpgradable(level, levelCount)`·`ApplyHeal`(R22·R23), `HealWeight.Compute(wBase, hp, maxHp)`(R8), `WeightedSampler.SampleWithoutReplacement`(R9), `KillRules.CountsAsKill`(R24), `GameRules`(무기 슬롯 4·패시브 슬롯 3·`HealRatio` 등 상수 — 이름은 `Core/GameRules.cs`), `SimulationRules`(`LevelUpEnabled`, `RecordBestTime`, `StopAtStep`, `AutoPauseOnFocusLoss`, …), `RngStreams(masterSeed)`·`Get(RngStream)`·`Reseed(ulong)`·`MasterSeed`, `RngStream` 열거형(`LevelUp` 스트림 포함), `EntityHandleAllocator`, `StepContext`, `IStepPhaseHandler`, `SimulationPhase`, `DespawnReason`.
  - Data: `GameDataRegistry`(인스턴스: `Current`, `LastReport`, `Initialize(data, report)`, `TrySwap`, `event Swapped`), `GameData`(`Weapons`·`Passives`·`Monsters`·`Combinations`·`SpawnTable`·`LevelUpPool`·`PlayerStats`, `TryGetWeapon/Passive/Monster`), `PlayerStatsDefinition.XpCurve`(`XpCurveDefinition`: `Thresholds`·`GrowthAfterTable`)·`MaxHealth`·`StartingWeaponId`, `XpCurve.RequiredForNext(int level, XpCurveDefinition def)`(레벨 L→L+1 필요량 = `thresholds[L-1]`, 표를 넘으면 마지막 값 + `growthAfterTable × 넘긴 수`), `LevelUpPoolDefinition.ResolveWeight(UpgradeKind, targetId)`(구체 ID 규칙이 `"*"`보다 우선, 없으면 `DefaultWeight`)·`HealBaseWeight`·`UniformFallback`, `WeaponDefinition`(단계 목록·`EvolvedOnly` — 멤버 이름은 `Data/Definitions/`에서), `PassiveDefinition`, `CombinationRule`. 정의 객체의 setter는 internal이라 **테스트는 `GameDataLoader.LoadFromTexts`로만 데이터를 만든다**(5c.5 14회차 관례; 실제 JSON은 `Survivor/Assets/StreamingAssets/Data/*.json`, 변형은 문자열 치환).
  - LOOP(`Scripts/Simulation/Loop|Entities`): `SimulationClock`(`Step`·`IsRunning`·`StepActive`·`CloseReason`·`CloseSequence`·`Open()`·`Close(reason)`·`CloseAfterCurrentStep(reason)`·`Reset()`, internal `TryBeginStep(long stopAtStep)`·`EndStep(long stopAtStep)` — 리그는 `long.MaxValue`를 넘긴다), `SimulationPipeline.AddHandler(phase, order, handler)`, `EntityLifecycle(pool, ids, ticks, collisions, physicsBodies, catalog, …)`(생성자 인자는 코드에서)·`DespawnAll(reason)`·`ReplaceStrategies(pool, ticks, collisions)`·`ActiveEntities`(**순서 없음**)·`ActiveCount`·`TryGet`·이벤트 `Spawned/Despawning/Despawned`, `SimEntity`(`Id`·`Category`·`Position` — 멤버는 코드에서), `MonsterRegistry(lifecycle, snapshot)`, `CollisionEventRouter(strategy)`·`ReplaceStrategy`, `TargetProviderSlot(clock)`·`Replace`, `SimulationCommandBuffer`(`RegisterHandler`·`Clear`), `PendingCommandQueue(targets, data, rules)`(`RegisterHandler(kind, Action<PendingCommand, StepContext>)`·`Clear`·`BossSpawnedThisRun`), `PendingCommand`(`Kind`·`Count`·`Value`…), `PrefabCatalog(entityComponentTypes, decorators)`, `LifetimeSystem(lifecycle)`, `KeyboardInputSource`. `SimulationAssemblyInfo`의 `InternalsVisibleTo`로 **EditMode 테스트가 Simulation internal을 본다**.
  - PLAYER·MONSTER(웨이브 3, 5c.3a 조립 표가 생성자·등록·리셋 메서드를 전부 적어 두었다 — **그 표가 당신 리그의 설계도다**): `PlayerEntity(ids, physicsBodies, currentData, passives, host)`·`RegisterForRun(collisions, position)`·`UnregisterFromRun()`·`Position`, `PlayerMovement(player, input, passives, currentData)`·`SetInputSource`, `PlayerHealth`(`Hp`·`MaxHp`·`IsDead`), `PassiveInventory`(`Slots`·`Count`·`Owns`·`Level(id)`·`Clear`), `WeaponInventory(player, passives, lifecycle)`(`Slots`·`Count`·`Owns`·`ConsumedByEvolution`·`TryGetSlot`·`Clear`), `WeaponInstance`(`Definition`·`Id`·`Level`·`SlotIndex`·`NextFireStep`), `PlayerProgressionApplier(clock, weapons, passives, health, player, currentData)`(`ResetForRun()`·`ApplyLevelUpOption(in UpgradeOption)`(조건 `CloseReason == LevelUp && !StepActive && !IsRunning`, 부적용은 `InvalidOperationException`)·`HandleSetLoadout`·`HandleSetHealth`·`ChangeCount`), `PlayerDamageReceiver(health, currentData, contactSources, lifecycle)`·`Clear()`·`OverlapCount`, `PlayerHitHandler(lifecycle, weapons, damageables)`, `WeaponDamageResolver(weapons, damageables)`, `PlayerSpawnCommandHandler(lifecycle, weapons)`, `EvolutionService(weapons, passives, currentData)`, `ChestEffectResolver(evolution, applier, weapons)`; `MonsterSpawner(lifecycle, data, rng)`(`Bind(ISpawnPolicy)`·`BossSpawnedThisRun`·`LiveNonBossCount`), `TableSpawnPolicy(data)`, `MonsterCommandHandler(lifecycle, data, rng, spawner)`, `DropCommandHandler(lifecycle, data)`, `DespawnSystem(lifecycle, registry, data)`, `KillPipeline(registry, data, rng)`(`KillCount`·`BossKilledThisStep`·`Reset()` — **`BossKilledThisStep`이 언제 세워지고 지워지는지 `KillPipeline.cs`에서 확인**: Resolve order 30에서 세우고 같은 스텝 Lifecycle 90의 당신이 읽어야 한다), `PickupSystem(lifecycle, data, rng, addXp, openChest)`·`Clear()`, `PickupRegistry(lifecycle)`, `Monster`(`Hp`·`IsBoss`·`Definition`), 엔티티 타입 8종(`PrefabCatalog` 사전: `PlayerProjectile → PlayerProjectile`, `PlayerOrb → OrbitOrb`, `PlayerZone → DamageZone`, `Monster → Monster`, `EnemyProjectile → EnemyProjectile`, `EnemyZone → EnemyHazardZone`, `Gem → ExperienceGem`, `Chest → Chest`).
  - 단계 핸들러 순서(5c.3 #9·5c.3a): Begin 0 `PendingCommandQueue`·10 `PlayerMovement`; PlayerMove 0 `PlayerMovement`; BuildIndex 0 `MonsterRegistry`(·5 동기화기·10 공간 인덱스 — Strategies, 당신 리그에는 없어도 됨); WeaponTick 0 `WeaponInventory`·10 `PickupSystem`; Collision 0·Resolve 0 `CollisionEventRouter`; Resolve 10 `WeaponDamageResolver`·20 `PlayerDamageReceiver`·30 `KillPipeline`; Lifecycle 0 `SimulationCommandBuffer`·10 `LifetimeSystem`·20 `DespawnSystem`·30 `MonsterSpawner`·**90 `StepOutcomeGate`(당신)**. 라우터 채널 0·1·2 `PlayerHitHandler`, 3·4·5 `PlayerDamageReceiver`, 6·7·8 `PickupSystem`. 명령 버퍼: `Despawn` → `EntityDespawnCommandHandler(lifecycle)`, 플레이어 3종 → `PlayerSpawnCommandHandler`, `SpawnEnemyProjectile`·`SpawnEnemyZone`·`SpawnMonsters` → `MonsterCommandHandler`, `SpawnGem`·`SpawnChest` → `DropCommandHandler`. 드라이버 3개(`SimulationDriverEarly/Mid/Late`)는 MonoBehaviour라 EditMode 리그는 쓰지 않고 `Clock.TryBeginStep(long.MaxValue)` → 단계별 `Pipeline` 호출 → `Clock.EndStep(long.MaxValue)`을 직접 돌린다(`PlrRig.Step()`·`StepAndCloseForLevelUp()`, `MonEmRig.StepOnce()`가 그 코드) — **`Tests/EditMode/Monsters/MonEmRig.cs`와 `Tests/EditMode/Player/PlrRig.cs`가 그 방식의 조립 예시**(같은 어셈블리, internal 도우미 `MonEmData.RealTexts()/Load(...)`, `MonEmPool`, `MonEmPhysics`, `LoopEmCollisions`(`Tests/EditMode/Loop/LoopEmDoubles.cs`), `PlrPool`·`PlrTicks`·`PlrPhysics`·`PlrCollisions`·`PlrTargets`·`PlrInput`을 **읽고 호출해도 되지만 수정하지 않는다**). 두 리그는 각각 한쪽만 조립한다 — 당신은 **둘을 합친 전체 조립 리그**를 `Tests/EditMode/Progression/` 안에 만든다(5c.3a 표 그대로).
- EditMode 제약(5c.5 15회차 기준선): EditMode asmdef 안에 정의한 `SimEntity` 파생 더블은 `AddComponent`할 수 없지만, **런타임 어셈블리의 실물 엔티티 타입 8종은 EditMode에서 `AddComponent`·`Instantiate`된다**(MONSTER·PLAYER 테스트가 그렇게 했다). 실물 풀의 `Clear()`는 `Object.Destroy`를 쓰므로 EditMode에 맞지 않는다 — 리그는 테스트 풀(`MonEmPool` 패턴)을 쓰고, `Reset`에서 **같은 풀 객체를 다시 넘겨** `Clear()`가 불리지 않게 한다(5.10 `ReplaceStrategies`: 풀 참조가 바뀔 때만 옛 풀 `Clear()`).
- 테스트 실행 명령(PM이 그대로 다시 돌린다):
  ```
  "C:\Program Files\Unity\Hub\Editor\6000.3.28f1\Editor\Unity.exe" -batchmode -nographics -projectPath "<cwd>\Survivor" -runTests -testPlatform EditMode -testResults "C:\Users\SSAFY\.claude\jobs\f32c1ccc\tmp\flow-em-<n>.xml" -logFile "C:\Users\SSAFY\.claude\jobs\f32c1ccc\tmp\flow-em-<n>.log"
  ```
  PlayMode 확인은 `-testPlatform PlayMode`, 결과 `flow-pm-<n>.xml`. Bash timeout 600000ms. 컴파일 오류면 테스트 0건 + 로그 `error CS`. 종료 코드 0/2/3. XML `<test-run>`의 `total/passed/failed/inconclusive/skipped`. 한 프로젝트에 Unity 인스턴스는 하나만.
- 임시 파일은 `C:\Users\SSAFY\.claude\jobs\f32c1ccc\tmp\`에 `flow-` 접두사로. worktree 안에 임시 파일 금지. 파일은 LF·UTF-8(BOM 없음). Simulation 코드에 한국어는 없어야 정상이다. 개행 확인은 `.claude/skills/eol-audit/SKILL.md`의 방법만 쓴다.

## 2. 배타적 파일 소유권

### 소유 경로 (이 밖에는 만들거나 고치지 않는다)

1. `Survivor/Assets/Scripts/Simulation/Progression/**` — 5c.2 IMP-FLOW 행: `ExperienceSystem.cs`, `LevelUpCandidateBuilder.cs`, `LevelUpSelector.cs`, `LevelUpSession.cs`, `StepOutcomeGate.cs`, `RunSession.cs`, `RunStrategies.cs`, `BestRecordStore.cs`, `RunOutcome.cs`(+`RunOutcomeKind`, 같은 파일 — 5.17이 함께 적음). 행에 없는 파일을 추가해야 했다면 보고에 이유(파일당 공개 타입 1개 규칙).
2. `Survivor/Assets/Scripts/Simulation/Loop/SimulationStateHash.cs` — 신규 1개(IMP-LOOP 소유 파일을 이번에 당신이 작성, 5.16).
3. `Survivor/Assets/Tests/EditMode/Progression/**` — 신규 폴더(전체 조립 리그 포함).
4. Unity가 자동 생성하는 `.meta`.

### 건드리지 않는 것

- `Scripts/Core/**`, `Scripts/Data/**`, `Scripts/Strategies/**`, `Scripts/Simulation/Loop/**`(`SimulationStateHash.cs` 제외)·`Entities/**`·`Player/**`·`Weapons/**`·`Monsters/**`·`Pickups/**`, `Scripts/App|UI|Dev/**`(없음, 4b), `Tests/EditMode/Core|Data|Spatial|Loop|Collision|Player|Monsters/**`, `Tests/PlayMode/**`, asmdef 전부(Simulation asmdef는 이미 Core·Data·InputSystem을 참조하고 테스트 asmdef는 Simulation을 참조한다 — **수정 불필요**), `Scenes/`, `Settings/`, `ProjectSettings/`, `Packages/`, `StreamingAssets/`.
- **스텁 금지.** `GameBootstrap`·`GameFlowController`·`StrategyFactory`·UI 뷰·`IDevModeHost` 구현을 만들지 않는다(4b). `RunSession`이 Strategies 타입(`MonsterSpatialIndex`·`PhysicsBodySynchronizer`·풀/충돌 구체 클래스)을 참조하면 위반이다(5c.1: Simulation → Core·Data만).
- 소유 밖 결함(예: PLAYER·MONSTER 리셋 메서드가 문서와 다름)은 **고치지 말고 보고**한다.
- `indiv` 루트 문서는 읽기만. **`RUN_LOG.md`는 열지도 쓰지도 않는다.**

## 3. 검증 재현성

- `Library/`·`Temp/`·`Logs/` 삭제 금지. 패키지 수정 = BLOCKED 보고. `git commit`/`push`/`reset`/`git rm`, `rm -rf` 금지.
- 같은 도구 호출이 같은 이유로 2회 연속 실패하면 세 번째를 시도하지 말고 보고.
- **보고 수치는 PM이 같은 명령으로 재현 가능해야 한다.** 마지막 3회 XML 경로를 적는다. 래퍼는 `C:\Users\SSAFY\.claude\jobs\f32c1ccc\tmp\flow-run.sh <EditMode|PlayMode> <tag>`.

## 4. 결정성

- 테스트에 `UnityEngine.Random`·시드 없는 `System.Random`·`DateTime.Now`·`Time.*` 금지. 난수는 `RngStreams`(고정 시드)뿐.
- **레벨업 추첨은 `LevelUp` 스트림만 소비한다**(TAC-F5-1·F5-4 ②). 다른 스트림(Spawn·Drop·Chest…)을 건드리면 몬스터 스폰이 어긋나 전략 조합 결정론이 깨진다 — 테스트: 추첨 전후 다른 스트림의 다음 값이 같다.
- **상태 해시는 입력 순서가 고정**이어야 한다. `ActiveEntities`는 순서가 없으므로 **`EntityHandle` 오름차순으로 정렬한 사본**을 돌린다(5.16). 해시 테스트: 같은 시드·같은 스텝 수 → 같은 값(2회 실행), 시드가 다르면 다른 값, 무기 구성이 다르면 다른 값(`NextFireStep` 포함 확인), 엔티티 등록 순서를 바꿔도(같은 상태) 같은 값.
- **합격 기준: EditMode 전체 스위트 연속 3회 전회 동일(총 N, 통과 N, 실패 0) + PlayMode 확인 1회 185 유지.** 네 줄 그대로 보고.

## 5. 기준선 — 정상 동작인데 결함으로 오판하기 쉬운 것, 그리고 PM 기본값

1. **`StepOutcomeGate`(Lifecycle order 90) 판정은 스텝당 1회, 사망 > 클리어 > 레벨업**(TAC-F8-8·F5-8). 사망 = `health.IsDead`, 클리어 = `killPipeline.BossKilledThisStep`(스텝 번호 무관, 5.14 rev 8), 레벨업 = 앞 둘이 아니고 `context.Rules.LevelUpEnabled`일 때 `experience.TryBeginLevelUp(curve)` **1회**(참이면 `clock.CloseAfterCurrentStep(LevelUp)`). 사망·클리어는 `CloseAfterCurrentStep(Death|Clear)`와 `PendingOutcome`(`SurvivalSteps = context.Step + 1`, `Kills = killPipeline.KillCount`, `Level = experience.Level`). 같은 스텝에 사망·클리어가 있으면 `TryBeginLevelUp`을 부르지 않는다(경험치는 버려진다). `curve`는 `currentData().PlayerStats.XpCurve`(생성자로 `Func<GameData>` 주입, 5c.3 #10 — 플레이어 기본 스탯은 참조할 때마다 `Current`).
2. **`ExperienceSystem`**: `Level`은 1에서 시작, `AddXp(amount)`는 쌓기만 하고 레벨을 올리지 않는다(레벨업 판정은 게이트가 한다). `TryBeginLevelUp(curve)`는 `Xp >= RequiredForNext(Level, curve)`면 빼고 +1, **호출당 최대 1레벨**(여러 레벨분이 쌓여도 회차 하나씩, TAC-F5-4). `Reset()`은 레벨 1·경험치 0. **PM [기본값] 대기 명령 핸들러**: PLAYER의 `HandleSetLoadout` 패턴대로 ★ `HandleAddXp(PendingCommand, StepContext)`(= `AddXp(command.Count)`)·★ `HandleLevelUpNow(PendingCommand, StepContext)`(= `AddXp(RequiredForNext(Level, curve) − Xp)`, 5.15 16회차 [기본값]; 이미 충분하면 0 추가)를 **메서드 그룹으로 등록 가능하게**(`in` 없이) 공개한다. 등록은 App(4b) 몫이고 당신 리그가 테스트에서 등록한다. `curve`는 생성자 `Func<GameData>`로.
3. **`LevelUpCandidateBuilder.Build(data, weapons, passives, health, maxHealth, output)`**: 출력은 R12 정규 순서(`UpgradeKind` 오름차순 → `TargetId` 서수). 가중치는 `data.LevelUpPool.ResolveWeight(kind, targetId)`; 회복은 `HealWeight.Compute(data.LevelUpPool.HealBaseWeight, health, maxHealth)`이고 **0이면 후보에서 뺀다**(만체력). 제외(TAC-F5-3): 새 무기는 슬롯 4개가 차면, 새 패시브는 3개가 차면, 강화는 R22 거짓이면, `evolvedOnly` 무기는 새 획득·강화 모두 항상, `weapons.ConsumedByEvolution`에 든 ID의 새 획득은 제외. **강화 후보의 `levelCount`·`evolvedOnly`는 보유 인스턴스의 정의**(`WeaponInstance.Definition`, `PassiveInstance.Definition`)에서, **새 획득 후보만 `data`(= `Current`)에서**(TAC-F11-5·5.14 8회차: 핫 리로드로 삭제된 무기의 인스턴스도 강화 후보에 남는다). `Heal`의 `TargetId`는 **PM [기본값] 빈 문자열 `""`**(`UpgradeOrdering.Compare`가 null을 다루는지 코드에서 확인하고, null이 안전하면 null도 허용 — 보고에 적는다). `IsFiller`는 여기서 전부 false.
4. **`LevelUpSelector.Draw(candidates, rng, output3)`**: `WeightedSampler.SampleWithoutReplacement`(R9)로 **정확히 3개**를 비복원 추출하고, 후보가 3 미만이면 남은 칸을 `UpgradeOption(Heal, "", isFiller: true)`로 채운다(**만체력이어도**, TAC-F5-2). 후보 0개면 Heal 채움 3개. `output3`은 비운 뒤 채운다.
5. **`LevelUpSession`**(R29): `Open(options3, frame)`은 `RoundId`를 1 올리고(판 안에서 증가, `Reset`에서 0), 이미 열려 있으면 `InvalidOperationException`. `TrySubmit(roundId, optionIndex, frame, out chosen)`은 `IsOpen && roundId == RoundId && 미제출 && frame > OpenedFrame && 0 ≤ index < 3`일 때만 참이고 그 뒤 같은 회차는 전부 거짓. `Close()`는 닫기만(옵션은 다음 `Open`까지 유지돼도 된다 — TAC-F5-7 일시정지 중 같은 3개 유지는 "닫지 않는 것"으로 구현된다: ESC는 `clock.Close(Paused)`만 하고 세션은 열린 채 둔다, 4b 몫).
6. **`RunSession`**: 공개 시그니처는 `Reset(in RunStrategies next)` 하나가 문서에 있고(5.14), **생성자와 읽기 속성은 당신이 정한다(★)** — 5.14 끝의 (17회차) 리셋 순서 0~9를 **그대로** 실행하는 데 필요한 참여자를 생성자로 받는다(`PlayerEntity`, `PlayerProgressionApplier`, `PlayerMovement`, `PlayerDamageReceiver`, `PickupSystem`, `KillPipeline`, `MonsterSpawner`, `LevelUpSession`, `ExperienceSystem`, `SimulationCommandBuffer`, `PendingCommandQueue`, `EntityLifecycle`, `CollisionEventRouter`, `TargetProviderSlot`, `EntityHandleAllocator`, `RngStreams`, `SimulationClock`, 시드 공급 `Func<ulong>`, 스폰 정책·입력 공급 `Func<ISpawnPolicy>`·`Func<IInputSource>`(App이 `devHost.SpawnPolicyOverride ?? new TableSpawnPolicy(data)`·`devHost.InputOverride ?? keyboard`를 닫아 넘긴다 — **`RunSession`은 `IDevModeHost`를 모른다**, App 타입이다). 인자가 많으면 **읽기 전용 묶음 구조체/클래스 하나**(예: `RunSessionParts`, 파일당 공개 타입 1개 규칙 — 새 파일이면 보고)로 받아도 된다. **순서 0~9를 바꾸지 않는다**(근거는 5.14 각주). `Reset`은 게이트가 닫힌 상태(`!StepActive`)에서만 — `StepActive`면 `InvalidOperationException`. `Reset` 뒤 `clock.Open()`·`MonsterSpatialIndex.Clear()`·요구사항 콜백은 **App이 부른다**(TAC-F8-2 8회차·11회차) — `RunSession`은 부르지 않는다. 상태 해시가 읽을 수 있게 ★ 읽기 속성(`Clock`, `Player`, `Health`, `Experience`, `Weapons`, `Lifecycle`, `KillPipeline` 등)을 연다.
7. **`RunStrategies(pool, tick, collision, target)`** readonly struct, 5.14 그대로. `RunOutcome(Kind, SurvivalSteps, Kills, Level)`·`RunOutcomeKind { None, Death, Clear }` 5.17 그대로(생성자 ★).
8. **`BestRecordStore`**(TAC-F8-3): `PlayerPrefs` 정수 키 `"BestSurvivalSteps"`(`long` 생존 스텝은 `int`로 저장 — 36,000 상한이라 안전; 넘치면 `int.MaxValue`로 자름 [기본값]). 저장은 `rules.RecordBestTime && survival > best`일 때만, `SetInt` 직후 **`PlayerPrefs.Save()`**. 없으면 "기록 없음"(`TryGetBest(out int)` false). **PM [기본값] 테스트 격리**: 생성자에 키를 받는 오버로드(기본값 `"BestSurvivalSteps"`)를 두고 테스트는 `"BestSurvivalSteps.Test"` 같은 키를 쓴 뒤 `DeleteKey`로 지운다 — 실제 키를 테스트가 건드리지 않는다.
9. **`SimulationStateHash.Compute(RunSession session)`**(5.16): FNV-1a 64, float은 비트 패턴(`BitConverter.SingleToInt32Bits`), long/int는 그대로 바이트로. 입력 순서(고정): ① `Step` ② 플레이어 `Position.x/y`·`Hp`·`Xp`·`Level` ③ **PM [기본값]** 활성 엔티티 전부를 `EntityHandle` 오름차순으로 (`Index`, `Generation`, `Category`, `Position.x/y`), 몬스터면 `Hp` 추가 — 문서의 "몬스터·투사체·젬·장판·궤도 원" 목록의 상위 집합이고 카테고리 태그로 구분된다(18회차에 명문화 예정) ④ `KillCount`(R24) ⑤ 무기 `NextFireStep`: **PM [기본값]** 점유 슬롯만 슬롯 순서로 (`SlotIndex`, `NextFireStep`)(5.16 "빈 슬롯 처리는 LOOP가 정함"의 답). 2b의 테스트 도우미 `LoopPmStateHash`(`Tests/PlayMode/Loop/LoopPmRig.cs` internal)와 같은 FNV 상수·규칙을 쓴다 — 읽고 참고하되 **그 도우미를 실물로 교체하는 것은 4b**(PlayMode 소유)다.
10. **`PlayerProgressionApplier.ApplyLevelUpOption` 조건에 `!clock.IsRunning`이 있다**(17회차) — 레벨업 적용 테스트는 게이트가 `LevelUp`으로 닫힌 뒤, `Open()` 전에 호출해야 한다. `Open()` 뒤 호출은 예외가 **정상**이다. 적용 불가 옵션(이미 보유 등)도 예외가 정상 — 후보 생성이 올바르면 일어나지 않는다.
11. **`PendingCommandQueue`는 소유자 종류 핸들러가 미등록이면 `Enqueue`에서 던진다**(15회차). 리그는 `SetLoadout`·`SetHealth`(PLAYER 메서드)와 `AddXp`·`LevelUpNow`(당신 메서드) 4개를 등록한다 — 실제 App과 같은 배선.
12. **`BossKilledThisStep`의 수명**: `KillPipeline.cs`에서 확인한다. Resolve 30에서 세워지고 Lifecycle 90에서 읽혀야 한다. 다음 스텝 Resolve에서 지워지는 구조면 그대로 쓰고, 아니면 보고(소유 밖 결함).
13. **몬스터를 "죽이는" 테스트 방법**: `Monster.ApplyDamage(amount, step)`(Core `IDamageable`)를 Resolve 전에 호출하거나 무기를 실제로 쏘게 한다. 보스 클리어 테스트는 `MonsterSpawner`가 보스를 생성하게 하거나(`TableSpawnPolicy`는 36,000스텝 — 테스트용 `ISpawnPolicy`(`Scripts/Core/ISpawnPolicy.cs`) 더블로 `BossSpawnDue(step)` 참) 실제 보스 정의 ID로 `SpawnMonsters(ignoreCap)` 명령을 넣는다. 보스 여부는 `Monster.IsBoss`.
14. **전체 조립 리그 뒤 `Reset` 단언**(5c.4 웨이브 4, 17회차 후보): 리셋 뒤 `ActiveCount == 0`, 플레이어 몸·자석 ID 1:1·2:1(`PlayerEntity.Body.Id`·`MagnetBody.Id` — 이름은 코드에서), `clock.Step == 0`·`!IsRunning`, `weapons.Count == 1`이고 그 `NextFireStep == 0`, `passives.Count == 0`, `health.Hp == MaxHp`, `receiver.OverlapCount == 0`, `pickupSystem.AttractedGemCount == 0`, `killPipeline.KillCount == 0`, `spawner.BossSpawnedThisRun == false`, `levelUpSession.RoundId == 0`, `experience.Level == 1`, 대기 명령 큐 비어 있음. **두 번째 판 결정론**: 판 1을 N스텝 돌린 뒤 `Reset`(같은 시드) → 판 2를 N스텝 → 두 판의 상태 해시가 같다(리셋이 완전하다는 증거, "흐름" 행의 EM 부분).
15. **R24 처치 수**는 `KillPipeline.KillCount`를 읽기만 한다(증가는 MONSTER 한 곳). 결과 화면의 `Kills`도 그 값이다.
16. **TAC-0-1**: 당신 코드 중 스텝 밖에서 상태를 바꾸는 것은 `RunSession.Reset`뿐이어야 한다(`LevelUpSession`은 UI 상태라 예외 — 시뮬레이션 상태가 아님). `ApplyLevelUpOption` 호출은 App(4b)이 한다. 당신은 EM 테스트에서 TAC-F5-4 ②~④ 순서를 **테스트 코드로** 재현해(게이트 닫힘 → `Build`+`Draw` → `Open` → `TrySubmit` → `ApplyLevelUpOption` → `TryBeginLevelUp` 재평가 → 참이면 두 번째 `Open`, 거짓이면 `clock.Open()`) 연속 3레벨 시나리오에서 회차 ID 1·2·3이 **스텝 수 불변**으로 열리는 것을 확인한다(TAC-F5-4 EM 부분).
17. **데이터**: 실제 `StreamingAssets/Data/*.json`(W1~W4·E1·E2, P1~P3, 몬스터, 레벨업 풀)을 `LoadFromTexts`로 읽는다(`MonEmData.RealTexts()` 패턴). 제외 규칙 테스트에 필요한 변형(슬롯 가득, `evolvedOnly`, 삭제된 무기)은 문자열 치환 또는 `SetLoadout`으로 만든다.

## 6. 금지

- 시크릿 파일 열람 금지. git 변경 명령·패키지 설치·`rm -rf` 금지.
- **단언을 느슨하게 고쳐 초록 금지**(해시 비교에 허용 오차·부분 비교 도입, 리셋 단언 생략 등). 문서에 있는 공개 시그니처·이름 변경 금지(반박으로).
- 주석 영어 산문, 공개 멤버 한 줄 요약(`CODING_GUIDE.md` §3). 네이밍 §2.2. 파일명 = 주 클래스명. 파일당 공개 타입 1개(`RunOutcome`+`RunOutcomeKind`는 문서가 함께 적은 예외).
- 로그는 Simulation internal `LoopLog`를 써도 된다(PLAYER가 그렇게 했다). 스텝마다 남기는 로그 금지. 판 리셋 1줄·판 종료 1줄 정도.
- 보고에 시크릿·긴 로그 원문 금지.

## 7. 보고 형식 (압축)

1. 만든 파일 목록(타입, 구현 TAC·절·R). 5c.2 IMP-FLOW 행 대조(이번 범위 밖 = "4b").
2. 테스트 결과: EditMode 3회 + PlayMode 1회 원문(total/passed/failed/inconclusive/skipped, XML, 종료 코드) + PM 재현 명령. Progression 테스트만의 건수도 따로.
3. 계약 대조표: 문서 시그니처(5.14·5.16·5.17) × 구현 — 같음/다름(이유). **★ 추가 공개 멤버 전부**(특히 `RunSession` 생성자·읽기 속성, `ExperienceSystem.Handle*`, `BestRecordStore` 키 오버로드).
4. **조립 루트(4b)가 꽂아야 할 것**: 당신 객체의 생성자·등록 위치(단계 순서·대기 명령 종류)·`Reset` 뒤 App이 할 일 — 5c.3a와 같은 표 형식.
5. PM 기본값(5절 1~17) 중 따르지 않은 것과 이유. 반박은 **먼저 실측**하고 수치와 함께.
6. 읽을 목록 밖 파일. 7. **F5·F8 기록 초안 각 1단락**(5c.2a: 게이트 우선순위와 회차 설계 이유 — devlog 소재. 창 자동 선택은 4b). 8. 확신하지 못하는 것(항상 마지막).

## 8. 읽을 문서 (이 밖의 기획 문서는 열지 말 것. 열어야 했다면 보고에 적는다)

전원 공통: `spec/00-common.md`, `tech/README.md`(색인만), `tech/02-feature-tac.md` **3.0 절(TAC-0-1~0-10)·F5 절·F8 절(TAC-F8-1~F8-3·F8-8)·TAC-F2-1·F2-2·TAC-F11-5**, `tech/5a-shared-rules.md`(R8·R9·R12·R22·R23·R24·R29), `tech/5c-ownership-contracts.md`(5c.1·5c.2 IMP-FLOW 행·5c.2a·5c.3 #1·#7·#8·#9·#10·#11·**5c.3a 전체**·5c.4 웨이브 4 행·5c.5).
필수(5c.2b): `spec/F05-level-up.md`, `spec/F08-stage-flow.md`, `tech/5.14-progression.md`(전부), `tech/5.16-state-hash.md`, `tech/5.17-flow-state.md`, `tech/5.03-step-clock.md`(게이트 규칙), `tech/5.15-dev-mode.md`(`PendingCommandQueue` 소유자 종류 문단과 `LevelUpNow` [기본값]만).
계약만: `tech/5.12-damage-rules.md`(`PlayerProgressionApplier`·`PlayerEntity` 블록, 5.12.1 `KillPipeline`·`MonsterSpawner`·`PickupSystem` 입력·출력), `tech/5.10-lifecycle.md`(`EntityLifecycle` 블록·리셋 플래그), `tech/5.11-data-loader.md`(5.11.1 표), `tech/5.13-json-schemas.md`(5.13.6·5.13.7), `tech/5.02-rng.md`(`Reseed`).
코드: `Scripts/Core/`의 `UpgradeOption.cs`, `UpgradeOrdering.cs`, `ProgressionRules.cs`, `HealWeight.cs`, `WeightedSampler.cs`, `KillRules.cs`, `GameRules.cs`, `SimulationRules.cs`, `RngStreams.cs`, `RngStream.cs`, `DespawnReason.cs`, `EntityHandle.cs`; `Scripts/Data/`의 `GameData.cs`, `GameDataRegistry.cs`, `XpCurve.cs`, `GameDataLoader.cs`(`LoadFromTexts` 시그니처), `Definitions/*`(멤버 이름); `Scripts/Simulation/Loop|Entities/*`(공개 API·주석), `Player/*`·`Weapons/*`·`Monsters/*`·`Pickups/*`(공개 API·생성자·리셋 메서드·`KillPipeline.BossKilledThisStep` 수명); 테스트 리그 `Tests/EditMode/Monsters/MonEmRig.cs`, `Tests/EditMode/Player/PlrRig.cs`·`PlrDoubles.cs`, `Tests/EditMode/Loop/LoopEmDoubles.cs`, `Tests/PlayMode/Loop/LoopPmRig.cs`(`LoopPmStateHash`·조립 순서). `CODING_GUIDE.md` §1~§4·§7.
주지 않는 것: `RUN_LOG.md`, `DECISIONS.md`, 다른 `spec/F*.md`, `tech/01`·`06`·`5.18`·`5.09`·`5.07`.

## 작업 순서 제안

1. 문서·Core·Data·LOOP·PLAYER·MONSTER 공개 API를 읽고 5c.3a 표를 체크리스트로 만든다.
2. `RunStrategies`·`RunOutcome`·`ExperienceSystem`·`LevelUpSession`·`BestRecordStore`(순수 C#) + 단위 테스트 → 배치 1회(Library 생성, 615 + α).
3. 전체 조립 리그(`Tests/EditMode/Progression/FlowEmRig.cs` 등) — MonEmRig + PlrRig 합본, 5c.3a 순서 그대로 → `RunSession` → 리셋 단언(기준선 14).
4. `LevelUpCandidateBuilder`·`LevelUpSelector`·`StepOutcomeGate` → 우선순위·제외·연속 3레벨·`LevelUpEnabled` 테스트.
5. `SimulationStateHash` → 결정론 테스트(기준선 4·14 두 번째 판).
6. 빨강 → 수정 → 초록 → EM 연속 3회 + PM 1회 → 보고.

=== 프롬프트 끝 ===
