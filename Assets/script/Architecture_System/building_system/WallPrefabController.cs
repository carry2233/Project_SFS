using UnityEngine;                                               // ✅ Unity 기본
using UnityEngine.Tilemaps;                                      // ✅ Tilemap
using Unity.AI.Navigation;                                       // ✅ NavMeshSurface(선택)

[AddComponentMenu("Build/Walls/Wall Prefab Controller")]
public class WallPrefabController : MonoBehaviour
{
    [Header("식별/상태(디버그)")]
    [SerializeField] private Vector3Int cell;                     // ✅ 벽이 대응하는 타일 셀
    [SerializeField] private int typeId;                          // ✅ 벽 타입ID
    [SerializeField] private int itemId;                          // ✅ 벽 아이템ID
    [SerializeField] private int currentHealth;                   // ✅ 현재 체력
    [SerializeField] private int maxHealth;                       // ✅ 최대 체력

    [Header("방어 스탯")]
    [SerializeField] private int stoppingPower;                   // ✅ 저지력(관통력 감소)
    [SerializeField] private int defenseRate;                     // ✅ 방어율(%)
    [SerializeField] private int absoluteDefense;                 // ✅ 절대 방어치

    [Header("런타임 참조(초기화로 주입)")]
    [SerializeField] private Tilemap mainWallTilemap;             // ✅ Final 타일맵(타일 삭제용)
    [SerializeField] private Tilemap sharedWallTilemap;           // ✅ 공유 타일맵(선택)
    [SerializeField] private NavMeshSurface navSurface;           // ✅ NavMesh 리베이크(선택)

    [Header("옵션")]
    [SerializeField] private bool logDebug = false;               // ✅ 디버그 로그

    private IWallAutoTiler wallAutoTiler;                         // ✅ 오토타일러(선택)
    private BuildProgressAndPlacer owner;                         // ✅ 점유 해제 콜백 대상(선택)
    private bool initialized;                                     // ✅ 초기화 완료 여부
    private bool destroyed;                                       // ✅ 중복 파괴 방지

    public void Initialize(                                       // ✅ 외부(빌더)에서 벽 정보/참조를 주입
        BuildProgressAndPlacer owner,
        Vector3Int cell,
        Tilemap mainWallTilemap,
        Tilemap sharedWallTilemap,
        IWallAutoTiler wallAutoTiler,
        NavMeshSurface navSurface,
        int typeId,
        int itemId,
        int maxHealth,
        int stoppingPower,
        int defenseRate,
        int absoluteDefense)
    {
        this.owner = owner;                                       // ✅ 콜백 대상 저장
        this.cell = cell;                                         // ✅ 셀 저장
        this.mainWallTilemap = mainWallTilemap;                   // ✅ 타일 삭제용
        this.sharedWallTilemap = sharedWallTilemap;               // ✅ 공유 타일 삭제용
        this.wallAutoTiler = wallAutoTiler;                       // ✅ 오토타일러 저장
        this.navSurface = navSurface;                             // ✅ 네비 저장

        this.typeId = typeId;                                     // ✅ 식별 저장
        this.itemId = itemId;                                     // ✅ 식별 저장

        this.maxHealth = Mathf.Max(1, maxHealth);                 // ✅ 최대체력 보정
        this.currentHealth = this.maxHealth;                      // ✅ 현재체력 초기화

        this.stoppingPower = Mathf.Max(0, stoppingPower);         // ✅ 보정
        this.defenseRate = Mathf.Clamp(defenseRate, 0, 100);       // ✅ 보정
        this.absoluteDefense = Mathf.Max(0, absoluteDefense);      // ✅ 보정

        initialized = true;                                       // ✅ 초기화 완료

        if (logDebug)
            Debug.Log($"[WallPrefab] Init cell={cell} typeId={typeId} itemId={itemId} hp={this.maxHealth}");
    }

    public void ApplyHit(CombatPayload2D payload)                 // ✅ 투사체/피격에서 호출되는 진입점
    {
        if (!initialized) return;                                 // 가드
        if (destroyed) return;                                    // 가드
        if (payload == null) return;                              // 가드

        if (payload.IsIgnored(gameObject))                        // ✅ 무시 규칙(같은 편/특정 태그 등)
            return;

        int absPow = Mathf.Max(0, payload.absolutePower);         // ✅ 절대 위력
        int absDef = Mathf.Max(0, absoluteDefense);               // ✅ 절대 방어

        if (absPow <= absDef)                                     // ✅ 절대 방어에 막힘(피해/관통 감소 없음: 기존 로직 유지)
        {
            if (logDebug)
                Debug.Log($"[WallPrefab] Blocked (AbsPow={absPow} <= AbsDef={absDef}) at {cell}");
            return;
        }

        int rawDamage = Mathf.Max(0, payload.attackPower);        // ✅ 기본 공격력
        int rate = Mathf.Clamp(defenseRate, 0, 100);              // ✅ 방어율
        float reduced = rawDamage * (1f - rate / 100f);           // ✅ 방어율 적용
        int finalDamage = Mathf.Max(0, Mathf.RoundToInt(reduced)); // ✅ 최종 피해

        if (payload.canReducePenetration && stoppingPower > 0)     // ✅ 저지력으로 관통력 감소
        {
            payload.ReducePenetration(stoppingPower);             // ✅ 관통력 감소
        }

        int before = currentHealth;                               // ✅ 로그용
        currentHealth = Mathf.Max(0, currentHealth - finalDamage); // ✅ 체력 감소

        if (logDebug)
            Debug.Log($"[WallPrefab] cell={cell} HP {before}->{currentHealth} (dmg={finalDamage}, rate={rate}%)");

        if (currentHealth <= 0)                                   // ✅ 체력 0이면 파괴
        {
            DestroyWall();                                        // ✅ 파괴 처리
        }
    }

    private void DestroyWall()                                    // ✅ 벽 파괴 처리(타일 삭제 + 오토타일 + 네비 + 점유 해제)
    {
        if (destroyed) return;                                    // 중복 방지
        destroyed = true;                                         // 플래그

        if (mainWallTilemap != null)                              // ✅ Final 타일 삭제
        {
            mainWallTilemap.SetTile(cell, null);                  // 타일 제거
            mainWallTilemap.SetTransformMatrix(cell, Matrix4x4.identity); // 변형 초기화(안 쓰면 영향 적음)
            mainWallTilemap.RefreshTile(cell);                    // 리프레시
        }

        if (sharedWallTilemap != null)                            // ✅ 공유 타일 삭제(선택)
        {
            sharedWallTilemap.SetTile(cell, null);                // 타일 제거
            sharedWallTilemap.SetTransformMatrix(cell, Matrix4x4.identity); // 변형 초기화
            sharedWallTilemap.RefreshTile(cell);                  // 리프레시
        }

        wallAutoTiler?.RefreshAround(cell);                       // ✅ 주변 오토타일 갱신

        if (navSurface != null)                                   // ✅ NavMesh 리베이크(선택)
        {
            navSurface.BuildNavMesh();                             // 리베이크
        }

        owner?.NotifyWallPrefabDestroyed(cell);                    // ✅ 점유 해제(빌더에 알림)

        if (logDebug)
            Debug.Log($"[WallPrefab] Destroy wall at {cell}");

        Destroy(gameObject);                                       // ✅ 프리팹 오브젝트 삭제
    }
}
