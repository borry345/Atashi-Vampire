# 웨이브 4d 디스패치 브리핑 — IMP-VISUAL (2026-11-09, PM 작성, 승인됨 "추천대로")

> `general-purpose`, `model: opus`, `isolation: worktree`(origin/main a806cb5에서 분기). "=== 프롬프트 시작 ===" 이후를 worktree 안의 이 파일로 읽게 한다.
> 배경: 4b 머지 뒤 첫 실기 확인에서 타이틀·흐름·레벨업·체력·플레이어 원은 정상이나 몬스터·투사체·젬·상자가 전혀 보이지 않았다. 원인은 결함이 아니라 미배정 작업 — 5.11 145행 "생성 시점에 `SpriteRenderer`에 적용"의 소유자가 5c.2 어느 행에도 없었고, 사용자 생성 레지스트리 에셋도 없다.

=== 프롬프트 시작 ===

# 역할: IMP-VISUAL — 엔티티 기본 도형 시각 + 에셋 키 레지스트리 생성기

당신은 뱀서라이크 1차 MVP(Unity 6000.3.28f1, C#, URP 2D)에서 풀 엔티티 8종(몬스터·적 투사체·적 장판·플레이어 투사체·궤도 원·플레이어 장판·젬·상자)이 화면에 보이게 만든다. 1차 MVP의 필수 에셋은 "Unity 기본 Sprite + 색상 도형"(spec 00-common 66행)이다. 두 산출물: ① App의 `EntityPresenter`(생성 시점에 에셋 키를 해석해 색·크기를 입힘), ② 에디터 스크립트로 생성하는 `Resources/AssetKeyRegistry.asset`(키 14개, 색은 키 이름 끝에서). 시뮬레이션 상태(위치·해시·충돌)는 1비트도 바뀌면 안 된다.

## 1. 이미 확인된 사실 (다시 조사하지 말 것)

- 자기 전용 worktree. cwd가 worktree 루트, Unity 프로젝트 `<cwd>/Survivor`. 동시에 도는 구현자 없음. 테스트 실행(worktree 루트, Unity 인스턴스 한 번에 하나, Bash `timeout` 600000, 한 실행 3~6분):
  ```
  bash "C:/Users/SSAFY/.claude/jobs/f32c1ccc/tmp/flow-run.sh" EditMode vis-em1
  bash "C:/Users/SSAFY/.claude/jobs/f32c1ccc/tmp/flow-run.sh" PlayMode vis-pm1
  ```
  태그 `vis-` 접두사, 매 실행 바꾼다. 결과 `C:/Users/SSAFY/.claude/jobs/f32c1ccc/tmp/flow-<tag>.xml/.log`. 임시 파일도 그 폴더에만.
- **기준선(main a806cb5)**: EditMode **703/703**, PlayMode **257/257**. `Tests/PlayMode/Flow/FlowStrategyCombinationTests` 36조합 해시 0x7AE2FE65ACEFF8F4 — 당신 작업 뒤에도 **같은 값**이어야 한다(시각은 상태에 영향 없음).
- **Data에 이미 있는 것**(`Scripts/Data/`, 수정 금지): `AssetKeyRegistry : ScriptableObject`(`[CreateAssetMenu(menuName = "Survivor/Asset Key Registry")]`, `ResourcePath = "AssetKeyRegistry"`, `List<Entry> Entries`, `Entry { string Key; Sprite Sprite; Color Color = white; GameObject PrefabOverride; }`, `static LoadDefault()` → `Resources/AssetKeyRegistry.asset` 없으면 null), `AssetKeyResolver(AssetKeyRegistry registryOrNull, DataValidationReport reportOrNull)` + `VisualAsset Resolve(string key)`(null 아님; 미등록이면 `DefaultShapeFactory.Circle` + 마젠타, 키당 경고 1회) + `bool Contains(key)` + `ValidateKeys(GameData)`(로드 시점 검증), `VisualAsset { Sprite Sprite; Color Color; GameObject PrefabOverride; }`, `DefaultShapeFactory.Circle`(64×64 흰 원, pixelsPerUnit 64 → `bounds` 1×1 유닛, 파괴되면 재생성), `FallbackColor = magenta`. **`AssetKeyResolver`는 아직 아무도 만들지 않는다** — 당신이 `GameBootstrap`에서 만든다.
- **JSON 에셋 키 14개**(`StreamingAssets/Data`): 몬스터 `shape.monster.green`(M1)·`shape.monster.orange`(M2)·`shape.monster.purple`(M3)·`shape.boss.red`(M4), 적탄 `shape.enemyshot.red`, 보스 장판 `shape.hazard.red`, 무기 `shape.projectile.white`·`shape.projectile.gold`·`shape.projectile.pink`·`shape.orb.cyan`·`shape.orb.gold`·`shape.zone.green`, playerStats `pickups.gemAssetKey = "shape.gem.blue"`·`pickups.chestAssetKey = "shape.chest.yellow"`. 정의 객체의 키 필드: `MonsterDefinition.AssetKey`, `EnemyProjectileDefinition.AssetKey`, `BossPatternDefinition.AssetKey`, `WeaponDefinition.AssetKey`(+ `IconKey`), `PickupsDefinition.GemAssetKey`·`ChestAssetKey`(`PlayerStatsDefinition` 아래 — 경로는 코드 확인).
- **엔티티 → 키 경로**(전부 공개 속성, Simulation 수정 불필요): `Monster.Definition.AssetKey`, `EnemyProjectile.Definition.AssetKey`, `EnemyHazardZone.Pattern.AssetKey`, `PlayerProjectile.WeaponId`·`OrbitOrb.WeaponId`·`DamageZone.WeaponId` → `GameData`에서 무기 정의 조회(`GameData`의 무기 사전/조회 API는 코드 확인) → `WeaponDefinition.AssetKey`, `ExperienceGem`·`Chest` → 현재 `GameData`의 `PlayerStats` 픽업 정의 키. 모든 `SimEntity`는 `Body.Radius`(유닛)·`Category`(`EntityCategory` 8종)·`Position`을 갖는다.
- **생성 훅**: `EntityLifecycle.Spawned`(`Action<SimEntity>`, 등록·초기화 뒤 마지막에 발생 — 풀 재사용 때도 매번), `Despawning`/`Despawned`, `ActiveEntities`(`IReadOnlyList<SimEntity>`), `ActiveCount`. 템플릿은 `PrefabCatalog`가 만든 비활성 GameObject로 **`SpriteRenderer`(스프라이트 없음)와 카테고리의 `SimEntity` 타입만** 붙어 있다(5.06). 풀 전략에 따라 같은 호스트가 재사용된다(풀 Prewarm/`ReleaseAll`은 조립 루트 몫, 이미 됨).
- **호스트 transform 스케일은 반드시 1로 유지**한다. 엔진 충돌 모드에서 `PhysicsBodySynchronizer`가 호스트에 `CircleCollider2D.radius = Body.Radius`를 두므로 호스트를 스케일하면 콜라이더가 함께 커진다. 또 `Body.DrivesTransform == true`(엔진 모드)면 물리가 호스트 transform을 옮기고 `PresentationSync`(실행 순서 −100, `LateUpdate`)는 쓰지 않는다 — 당신은 **호스트 위치를 절대 쓰지 않는다**. `PlayerPresenter`(`Scripts/App/PlayerPresenter.cs`)가 같은 이유로 자식 `Player.Visual`에 원을 그리고 지름을 `localScale`로 맞춘다 — **그 패턴을 그대로 따른다**(읽고 따라 하되 수정하지 않는다).
- 부팅·조립은 `Scripts/App/GameBootstrap.cs`(조립 루트, 5c.3a + 4a FLOW). 데이터는 `GameDataLoader.LoadAll(Application.streamingAssetsPath)` → `GameDataRegistry.Initialize(data, report)`; 현재 데이터는 `Func<GameData> current`(코드 확인). `DataValidationReport`에 경고를 넣으면 타이틀의 데이터 오류 표시와 섞일 수 있으므로 — **PM [기본값]: 리졸버는 `new AssetKeyResolver(AssetKeyRegistry.LoadDefault(), null)`로 만들어 경고를 로그로만 낸다**(report null 허용, 5.11 rev 14). 부팅 때 `resolver.ValidateKeys(data)`를 한 번 부른다(레지스트리가 없으면 "registry missing" 경고 1회 + 키별 경고 — 정상).
- URP 2D: 씬 `Main.unity`에 `Global Light 2D`가 있어 기본 스프라이트 머티리얼(Sprite-Lit-Default)이 정상 밝기다(U34). `PlayerPresenter`와 같은 `SpriteRenderer` 기본 머티리얼을 쓴다.
- asmdef 참조 테스트 `Tests/EditMode/Architecture/AsmdefReferenceTests.cs`는 `Assets/Scripts`·`Assets/Tests`·**`Assets/Editor`** 아래 모든 `.asmdef`를 읽어 표(`Table`)에 있는지, 참조가 허용인지 검사한다. 상수에 `Dev`·`Urp2D`·`RenderCore` 등이 이미 있다. 당신이 `Assets/Editor/Survivor.Editor.asmdef`를 만들면 **그 표에 `Survivor.Editor` 행 1개를 추가**해야 한다(허용 참조: `Survivor.Data`, `Survivor.Core`; 외부 없음). 다른 행은 손대지 않는다.
- 파일 LF·UTF-8 BOM 없음. 주석 영어 산문, 공개 멤버 한 줄 요약(`CODING_GUIDE.md` §3). 파일당 공개 타입 1개, 파일명 = 주 클래스명.

## 2. 설계 — PM [기본값] (따르지 않으면 실측 근거와 함께 보고)

### ① `Scripts/App/EntityPresenter.cs` (MonoBehaviour, `[DefaultExecutionOrder(-50)]` — PresentationSync −100 뒤, CameraFollow 0 앞)
- `Initialize(EntityLifecycle lifecycle, AssetKeyResolver resolver, Func<GameData> currentData)`; `lifecycle.Spawned` 구독.
- `OnSpawned(SimEntity e)`: 카테고리별로 키를 고른다(위 "엔티티 → 키 경로"; 키가 null/빈 문자열이면 `""`로 해석해 폴백). `resolver.Resolve(key)` → 호스트의 자식 `"Visual"`(없으면 생성: `SpriteRenderer` 1개, `SetParent(host, false)`, localPosition 0)에 `sprite`(null이면 `DefaultShapeFactory.Circle`)·`color`·`sortingOrder`를 적용하고 `localScale = (2r, 2r, 1)`. 호스트의 기존 빈 `SpriteRenderer`는 **건드리지 않는다**(`enabled`도 — Simulation 소유 템플릿). 풀 재사용 시 자식이 이미 있으면 재사용. `VisualAsset.PrefabOverride`는 **무시**(프리팹 교체는 스폰 측 `Spawn(category, prefabOverride, …)` 몫 — 보고에 적는다).
- `LateUpdate`: `ActiveEntities`를 돌며 `Body.Radius * 2 != visual.localScale.x`이면 스케일 갱신(궤도 원·장판 반지름이 레벨업으로 커진다). 위치는 쓰지 않는다(호스트가 옮겨지고 자식은 따라간다).
- 정렬·알파 PM [기본값]: `sortingOrder` — zone/hazard(`PlayerZone`·`EnemyZone`) −10, `Gem`·`Chest` 0, `Monster` 5, `PlayerProjectile`·`PlayerOrb`·`EnemyProjectile` 8(플레이어 원은 10). zone/hazard는 레지스트리 색의 알파를 0.35로(생성기에서 — 아래 ②), 나머지 1.0.
- 로그: `AppLog`(App, `[Flow]`/`[App]` 영역 — 기존 도우미 확인) 최소. 프레임 로그 금지.

### ② `Assets/Editor/` (신규 폴더) — `Survivor.Editor.asmdef`(`"includePlatforms": ["Editor"]`, references `Survivor.Data`, `Survivor.Core`) + `AssetKeyRegistryGenerator.cs`
- `[MenuItem("Survivor/Create Default Asset Key Registry")]` + 배치용 `public static void Generate()`(`-executeMethod Survivor.Editor.AssetKeyRegistryGenerator.Generate`로 PM이 돌릴 수 있게; 네임스페이스·이름 정확히 보고).
- 동작: `Assets/Resources/`가 없으면 만들고 `AssetKeyRegistry` 인스턴스를 만들어 **14개 키**를 `Entries`에 넣는다(`Sprite = null`(기본 원), `PrefabOverride = null`, `Color`는 키의 마지막 토큰으로: green `#3DBE5A`, orange `#F28C28`, purple `#9B59D0`, red `#E04040`, white `#F2F2F2`, gold `#E6B422`, pink `#F06292`, cyan `#30C8D8`, blue `#3F7FE0`, yellow `#F2D43D`; zone/hazard는 알파 0.35). 이미 에셋이 있으면 **덮어쓰지 않고** 없는 키만 추가(기존 항목 보존) — 사용자가 손으로 고친 값을 지키기 위해. `AssetDatabase.CreateAsset`/`SaveAssets`.
- 키 목록은 하드코딩하지 말고 **StreamingAssets JSON에서 읽어 모은다**(`GameDataLoader.LoadAll`로 로드한 정의들의 키 필드 전부: 몬스터·적탄·보스 패턴·무기 `AssetKey`+`IconKey`·픽업 2개) — 그래야 JSON에 키를 추가하면 메뉴 한 번으로 따라온다. 색 토큰이 위 표에 없으면 `Color.white`.
- 당신은 worktree에서 `Generate()`를 배치로 1회 실행해(`Unity.exe -batchmode -nographics -quit -projectPath … -executeMethod …`, 로그 파일 tmp) `Survivor/Assets/Resources/AssetKeyRegistry.asset`(+`.meta`)을 **실제로 만들어 둔다**. 이 에셋은 사용자 소유지만 PM이 이번 웨이브에 한해 생성을 허가했다 — 머지 대상이다. 생성된 YAML에 항목 14개와 색이 들어갔는지 `grep`으로 확인해 보고한다.

### ③ `GameBootstrap` 배선(App 소유, 최소 변경)
- 데이터 로드 뒤 `_resolver = new AssetKeyResolver(AssetKeyRegistry.LoadDefault(), null)` → `_resolver.ValidateKeys(data)`; `EntityPresenter`를 `Simulation` 쪽 드라이버들과 같은 방식으로 `AddComponent` + `Initialize(_lifecycle, _resolver, current)`. ★ `internal AssetKeyResolver Resolver`·`internal EntityPresenter Presenter` 읽기 속성 추가 가능(테스트용, ★ 표시).

## 3. 배타적 파일 소유권 (이 밖의 파일 수정 금지)

1. `Survivor/Assets/Scripts/App/EntityPresenter.cs`(신규), `GameBootstrap.cs`(③ 배선 줄만 — 다른 줄 금지).
2. `Survivor/Assets/Editor/**`(신규: asmdef + 생성기), `Survivor/Assets/Resources/AssetKeyRegistry.asset`(+`.meta`, 생성기로만 만든다 — 손으로 YAML을 쓰지 않는다).
3. `Survivor/Assets/Tests/EditMode/Architecture/AsmdefReferenceTests.cs` — `Survivor.Editor` 행 1개(+ 필요하면 상수 1개) 추가만.
4. `Survivor/Assets/Tests/EditMode/App/EntityVisualTests.cs`(신규 — 순수 함수: 카테고리별 정렬 순서 표, 색 토큰 → 색, 키 선택 규칙 등 테스트 가능한 정적 도우미를 `EntityPresenter`의 `internal static` 메서드로 두고 검사), `Survivor/Assets/Tests/PlayMode/Flow/FlowVisualTests.cs`(신규 — `FlowPmFixture` 사용, 아래 4절).
5. Unity 자동 생성 `.meta`.

건드리지 않는 것: `Scripts/Core|Data|Simulation|Strategies|UI/**` 전부, `PlayerPresenter.cs`·`PresentationSync.cs`·`PrefabCatalog.cs`(읽기만), 다른 테스트 파일(특히 `FlowStrategyCombinationTests.cs`·`FlowPmFixture.cs`·`FlowPmDoubles.cs`), `Scenes/**`, `StreamingAssets/**`(JSON에 키를 추가하지 않는다), `ProjectSettings/**`, `Packages/**`, `TextMesh Pro/**`. 원본 저장소 루트 문서(`RUN_LOG.md`·`DECISIONS.md`·`docs/`·`spec/`·`tech/`)는 읽기만.

## 4. 검증 재현성·결정성

- `Library/`·`Temp/`·`Logs/` 삭제 금지. 패키지 수정 = BLOCKED. `git commit`/`push`/`reset`/`stash`/`rm -rf` 금지. 같은 도구 호출이 같은 이유로 2회 연속 실패하면 세 번째를 시도하지 말고 보고. 보고 수치는 PM이 같은 명령으로 재현 가능해야 한다.
- **PM 테스트**(`FlowVisualTests`, `FlowPmFixture` 방식 — `LoadMain`, `UseScript`, `Host.StartNewRun`, `WaitFor`): ① 판을 시작해 몬스터가 1마리 이상 스폰된 뒤(`FlowPmCrowdPolicy` 사용) 모든 `ActiveEntities`의 자식 `Visual`에 `SpriteRenderer.sprite != null`, 색이 레지스트리 항목(`Resolver.Resolve(key).Color`)과 같고, `localScale.x == Body.Radius * 2`, **호스트 `localScale == 1`**. ② 레지스트리 없는 리졸버(`new AssetKeyResolver(null, null)`)를 쓰는 `EntityPresenter`를 따로 만들어(또는 ★ 테스트 훅) 폴백 색이 마젠타임. ③ 엔진 충돌 조합(`StrategySelection.Baseline`)으로도 ①이 성립하고 호스트 스케일이 1(콜라이더 반지름 = `Body.Radius` 그대로 — `CircleCollider2D.radius` 단언). ④ 같은 시드·같은 스텝 수에서 `SimulationStateHash.Compute`가 프레젠터 유무와 무관하게 같음(가장 간단히: 36조합 테스트가 기준 해시 그대로임을 보고로 대신한다).
- **합격 기준: EditMode 전체 3회 전회 동일 + PlayMode 전체 3회 전회 동일(실패 0), 36조합 해시 0x7AE2FE65ACEFF8F4 유지.** 여섯 줄 그대로 보고.
- 테스트에 `UnityEngine.Random`·시드 없는 `System.Random`·실시간 대기 금지. 배치 PlayMode는 그래픽이 없으므로 "보인다"는 렌더 결과가 아니라 컴포넌트 상태(sprite·color·scale·sortingOrder)로 검증한다.

## 5. 기준선 — 정상 동작인데 결함으로 오판하기 쉬운 것

1. 레지스트리가 없을 때 `AssetKeyResolver`가 경고 1회 + 키별 경고를 내는 것은 정상(spec F11 34행). PM 테스트 ②가 그 경로다 — `LogAssert.ignoreFailingMessages`가 아니라 `LogAssert.Expect`류로 경고를 소비하거나, 경고가 Warning 레벨이라 테스트를 깨지 않으면 그대로 둔다(확인).
2. `DefaultShapeFactory.Circle`은 흰색이고 **틴트가 색을 결정**한다. 마젠타는 폴백 틴트다.
3. 풀 `InstantiateDestroy`는 엔티티마다 새 GameObject라 `Visual` 자식이 매번 새로 생긴다 — 정상. `CustomPool`/`UnityObjectPool`은 재사용 — `Spawned`가 매번 오므로 색·크기를 매번 다시 적용한다(다른 정의의 몬스터가 같은 호스트를 받을 수 있다).
4. 엔진 모드 바디는 `DrivesTransform == true`이고 `PresentationSync`가 쓰지 않는다 — 자식 `Visual`은 호스트를 따라가므로 당신 코드에 분기가 필요 없다.
5. `EntityLifecycle.Spawned`는 판 리셋(`DespawnAll`) 뒤 재스폰에서도 온다. `Despawned`를 구독할 필요는 없다(비활성 호스트는 보이지 않는다).
6. `Monster.Definition`은 `Initialize` 뒤에만 유효 — `Spawned`는 초기화 뒤 발생하므로 안전.
7. 36조합 테스트는 매 조합 `StartNewRun`을 하고 수백 엔티티를 만든다 — `EntityPresenter.LateUpdate`가 `ActiveEntities`를 매 프레임 도는 비용은 허용(수백 비교). 총 소요가 기준(30~34초)에서 크게 늘면 보고.
8. `AsmdefReferenceTests`는 `Assets/Editor`를 **이미** 스캔 대상에 넣어 두었다 — 폴더가 없을 때는 건너뛴다. 당신이 만들면 표에 없어서 빨강 → 행 추가로 초록. 이것이 그 테스트의 유일한 변경이다.

## 6. 금지

- 시크릿 파일 열람 금지. git 변경 명령·패키지 설치·`rm -rf` 금지.
- 단언 완화·조합 제외·허용 오차로 초록 만들기 금지. 문서 공개 시그니처·이름 변경 금지(반박으로).
- 호스트 transform의 위치·스케일 쓰기 금지. Simulation 상태를 읽기만 한다.
- UI가 App을 참조하는 우회 금지(이번엔 UI 변경 없음).

## 7. 보고 형식 (압축)

1. 만든/고친 파일 목록(타입, 절). 2. EM 3회 + PM 3회 원문 여섯 줄 + XML 경로 + 36조합 해시·총 소요. 3. `Generate()` 정확한 `-executeMethod` 이름, 생성된 `AssetKeyRegistry.asset`의 항목 수·키 목록·색(grep 발췌). 4. ★ 추가 공개 멤버 전부. 5. PM 기본값을 따르지 않은 것과 실측 근거. 6. 소유 밖에서 고쳐야 한다고 본 것(위치·이유 — 고치지 않음). 7. 읽을 목록 밖 파일. 8. **사용자 실기 확인 안내 1단락**(무엇이 어떤 색으로 보여야 하는지). 9. **확신하지 못하는 것 — 추측하지 말고 미확정으로**(항상 마지막). 마지막에 `git status --short`.

## 8. 읽을 문서 (worktree 안)

- `spec/00-common.md`(에셋 절), `spec/F11-data-driven.md`(에셋 키 레지스트리 절), `tech/5.11-data-loader.md`(`AssetKeyRegistry`·`AssetKeyResolver`·`VisualAsset`·`DefaultShapeFactory` 블록, 145행), `tech/5.06-pool.md`(템플릿 문단), `tech/5c-ownership-contracts.md` 5c.1 표·5c.3 #6, `tech/01-overview.md` U20·U34 행, `CODING_GUIDE.md`.
- 코드: `Scripts/Data/AssetKeyRegistry.cs`·`AssetKeyResolver.cs`·`DefaultShapeFactory.cs`·`VisualAsset.cs`·`GameData.cs`·`Definitions/{Monster,EnemyProjectile,BossPattern,Weapon,Pickups,PlayerStats}Definition.cs`·`GameDataLoader.cs`(공개 API), `Scripts/Simulation/Entities/{EntityLifecycle,SimEntity,PrefabCatalog,PresentationSync}.cs`, 엔티티 8종(`Monsters/{Monster,EnemyProjectile,EnemyHazardZone}.cs`, `Weapons/{PlayerProjectile,OrbitOrb,DamageZone}.cs`, `Pickups/{ExperienceGem,Chest}.cs` — 공개 속성만), `Scripts/App/{GameBootstrap,PlayerPresenter,AppLog}.cs`, `Scripts/Core/EntityCategory`(위치 확인)·`CollisionBody.cs`, 테스트 `Tests/EditMode/Architecture/AsmdefReferenceTests.cs`, `Tests/PlayMode/Flow/{FlowPmFixture,FlowPmDoubles,FlowAssemblyTests,FlowPlayerTransformTests}.cs`(읽기만).
- 주지 않는 것: `RUN_LOG.md`, `DECISIONS.md`, 다른 `spec/F*.md`, 다른 `tech/5.x`.

## 작업 순서 제안

1. 문서·코드 읽기 → `Assets/Editor` asmdef + 생성기 → `AsmdefReferenceTests` 행 추가 → EM 1회(컴파일·asmdef 표 확인).
2. 배치 `-executeMethod`로 레지스트리 생성 → YAML grep 확인.
3. `EntityPresenter` + `GameBootstrap` 배선 + EM 순수 함수 테스트.
4. `FlowVisualTests` ①~③ → PM 1회(36조합 해시 확인).
5. EM 3회 + PM 3회 → 보고.

=== 프롬프트 끝 ===
