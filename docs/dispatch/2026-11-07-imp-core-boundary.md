# 소형 디스패치 브리핑 — IMP-CORE 경계 인터페이스 2개 (2026-11-07, PM 작성)

> `general-purpose`(sonnet), `isolation: worktree`. TECH 16회차·DECISIONS 4절 2026-11-07로 확정된 Core 인터페이스 2개를 추가하고 EditMode 테스트를 돌린다. 설계 판단 없음 — 시그니처는 아래 그대로다. 예상 1~2만 토큰(Unity 첫 실행 2~5분 포함 10~15분).

=== 프롬프트 시작 ===

# 역할: IMP-CORE 소형 추가 — `IDamageable`·`IContactDamageSource`

당신은 Unity 6(6000.3.28f1) / C# 프로젝트 `Survivor`의 Core 어셈블리에 인터페이스 파일 2개와 EditMode 테스트 1개를 추가한다. 설계는 끝났다 — 아래 코드를 그대로 쓰고, 컴파일과 전체 EditMode 스위트 통과를 확인해 보고한다.

## 1. 이미 확인된 사실

- 작업 디렉터리는 당신 전용 git worktree다(`pwd`로 확인, 아래 `<cwd>`). **cwd 밖은 읽지도 쓰지도 않는다.** Unity 프로젝트는 `<cwd>\Survivor`.
- `Survivor/Assets/Scripts/Core/`에는 `IPoolStrategy.cs`·`ITargetProvider.cs` 같은 인터페이스 파일이 있다(네임스페이스 `Survivor.Core`, 파일당 공개 타입 1개, 영문 XML 주석). 같은 관례를 따른다. Core는 아무것도 참조하지 않는다(UnityEngine 제외).
- 테스트 폴더 `Survivor/Assets/Tests/EditMode/Core/`(asmdef는 상위 `Tests/EditMode/Survivor.Tests.EditMode.asmdef`, 수정 불필요). 기존 EditMode 테스트는 473건 전부 통과한다.
- Unity 배치 실행(첫 실행 2~5분, Bash timeout 600000ms): `"C:\Program Files\Unity\Hub\Editor\6000.3.28f1\Editor\Unity.exe" -batchmode -nographics -projectPath "<cwd>\Survivor" -runTests -testPlatform EditMode -testResults "C:\Users\SSAFY\.claude\jobs\f32c1ccc\tmp\core2-em-<n>.xml" -logFile "C:\Users\SSAFY\.claude\jobs\f32c1ccc\tmp\core2-em-<n>.log"`. 종료 코드 0 = 통과. 결과는 XML `<test-run total= passed= failed=…>`. 컴파일 오류는 로그 `grep -c "error CS"`. 가드가 직접 실행을 막으면 같은 명령을 담은 래퍼 `C:\Users\SSAFY\.claude\jobs\f32c1ccc\tmp\core2-run.sh`로 돌린다.
- 임시 파일은 `C:\Users\SSAFY\.claude\jobs\f32c1ccc\tmp\`에 `core2-` 접두사로. 파일은 LF·UTF-8(BOM 없음). 개행 검사는 바이트로(`tr -cd '\r' | wc -c`).

## 2. 소유 파일 (이 3개 외 수정 금지)

1. `Survivor/Assets/Scripts/Core/IDamageable.cs`
2. `Survivor/Assets/Scripts/Core/IContactDamageSource.cs`
3. `Survivor/Assets/Tests/EditMode/Core/BoundaryInterfaceTests.cs`
`.meta`는 Unity가 만든다. 다른 파일·asmdef·`ProjectSettings`·`Packages`·`Library` 수정 금지.

## 3. 내용 (그대로)

```csharp
// IDamageable.cs
namespace Survivor.Core
{
    /// <summary>
    /// An entity that can take weapon damage. Implemented by Monster (Simulation); consumed by the player's
    /// hit handler (channel 0) and persistent-damage resolver through a Func&lt;EntityHandle, IDamageable&gt; lookup
    /// (TECH 5.12, rev 16).
    /// </summary>
    public interface IDamageable
    {
        /// <summary>False once health reached 0; stays false until the entity is despawned and reused.</summary>
        bool IsAlive { get; }

        /// <summary>
        /// Subtracts amount from health at step. A call while !IsAlive is ignored by the implementation
        /// (extra damage in the same step is dropped; the kill is confirmed once by KillPipeline).
        /// amount must be finite and >= 0, otherwise ArgumentOutOfRangeException.
        /// </summary>
        void ApplyDamage(float amount, long step);
    }
}

// IContactDamageSource.cs
namespace Survivor.Core
{
    /// <summary>
    /// An entity that damages the player on contact: Monster (definition ContactDamage), EnemyProjectile (Damage),
    /// EnemyHazardZone (zone damage). Consumed by PlayerDamageReceiver (channels 3, 4, 5) through a
    /// Func&lt;EntityHandle, IContactDamageSource&gt; lookup (TECH 5.12, rev 16).
    /// </summary>
    public interface IContactDamageSource
    {
        /// <summary>Damage dealt to the player by one hit (>= 0).</summary>
        float ContactDamage { get; }
    }
}
```

테스트 `BoundaryInterfaceTests`(네임스페이스는 기존 Core 테스트와 같게): 테스트 폴더 안에 두 인터페이스를 모두 구현하는 private 더블(`health`, `ContactDamage`)을 두고 ① `ApplyDamage`가 체력을 줄이고 0 이하에서 `IsAlive == false`가 된다, ② `!IsAlive` 뒤 호출은 체력을 더 줄이지 않는다, ③ 음수·NaN·무한대 `amount`는 `ArgumentOutOfRangeException`, ④ 인터페이스 참조로 `ContactDamage`를 읽을 수 있다 — 4건. 더블은 계약 주석의 동작을 그대로 구현한다(단언을 느슨하게 만들지 않는다).

## 4. 검증·금지

- EditMode 전체 스위트 **연속 2회** 실패 0, 기존 473 + 4 = 477건(다르면 그 수와 이유). 수치는 PM이 그대로 재현한다.
- git 상태 변경 명령·패키지 설치·`rm -rf` 금지. `RUN_LOG.md`·`DECISIONS.md`·`docs/**`·`spec/**`·`tech/**` 수정 금지. 기획 문서는 열지 않아도 된다(이 브리핑이 전부다).

## 5. 보고 (압축)

1. 만든 파일 3개 경로, 테스트 건수. 2. EM 1~2회의 total/passed/failed/종료 코드/XML 경로와 재현 명령. 3. 컴파일 경고가 있었다면 내용. 4. 확신하지 못하는 것(추측 금지).

=== 프롬프트 끝 ===
