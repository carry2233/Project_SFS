using System.Collections.Generic;           // Dictionary
using UnityEngine;                          // Unity 기본
using UnityEngine.Tilemaps;                 // Tilemap
using Unity.AI.Navigation;                  // NavMeshSurface

[AddComponentMenu("Build/Walls/Wall Health Manager")]
public class WallHealthManager : MonoBehaviour
{
    [System.Serializable]
    public class WallCellState                               // 한 칸의 벽 상태
    {
        public int typeId;                                   // 벽 타입ID
        public int itemId;                                   // 벽 아이템ID

        public int currentHealth;                            // 현재 체력
        public int maxHealth;                                // 최대 체력

        public int stoppingPower;                            // 저지력
        public int defenseRate;                              // 방어율(%)
        public int absoluteDefense;                          // 절대 방어치
    }

    [Header("참조")]
    [SerializeField] private Tilemap wallTilemap;            // 벽 타일맵
    [SerializeField] private MonoBehaviour autoTiler;        // IWallAutoTiler 구현체
    [SerializeField] private NavMeshSurface navSurface;      // NavMesh 리베이크(선택)

    

    [Header("옵션")]
    [SerializeField] private bool logDebug = false;          // 디버그 로그

    private IWallAutoTiler _autoTiler;                       // 자동 타일러 캐시
    private readonly Dictionary<Vector3Int, WallCellState> _cells = new(); // 셀→상태

    public System.Action<Vector3Int> OnWallDestroyed;         // 벽 파괴 알림
    public static WallHealthManager Instance { get; private set; } // 싱글톤

    public Tilemap WallTilemap => wallTilemap;               // ✅ [추가] 외부에서 동일 타일맵 참조용

    private void Awake()                                     // 초기화
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        _autoTiler = autoTiler as IWallAutoTiler;            // 인터페이스 캐시

        if (!wallTilemap) wallTilemap = GetComponent<Tilemap>(); // 타일맵 자동 캐시(선택)
    }

    private void Start()                                     // 시작 시 기존 벽 등록(선택)
    {
        Debug.Log("[WallHealth] Initial scan started");
        RegisterExistingWallsOnTilemap();
    }

    private void RegisterExistingWallsOnTilemap()            // 기존 벽 자동 등록(기본값으로)
    {
        if (wallTilemap == null)
        {
            Debug.LogWarning("[WallHealth] wallTilemap is NULL");
            return;
        }

        BoundsInt bounds = wallTilemap.cellBounds;
        Debug.Log($"[WallHealth] Scan bounds = {bounds}");

        foreach (Vector3Int cell in bounds.allPositionsWithin)
        {
            bool hasTile = wallTilemap.HasTile(cell);
            Debug.Log($"[WallHealth][Scan] cell={cell}, hasTile={hasTile}");

            if (!hasTile) continue;
            if (_cells.ContainsKey(cell))
            {
                Debug.Log($"[WallHealth][Skip] already registered cell={cell}");
                continue;
            }

            Debug.Log($"[WallHealth][Register] cell={cell}");

            RegisterWallCell(
                cell,
                typeId: 0,
                itemId: 0,
                maxHealth: 100,
                stoppingPower: 5,
                defenseRate: 0,
                absoluteDefense: 0
            );
        }
    }

    public void RegisterWallCell(                            // 벽 셀 등록
        Vector3Int cell,                                     // 셀 좌표
        int typeId, int itemId,                              // 식별 정보
        int maxHealth,                                       // 최대 체력
        int stoppingPower,                                   // 저지력
        int defenseRate,                                     // 방어율
        int absoluteDefense)                                 // 절대 방어치
    {
        if (!_cells.TryGetValue(cell, out var state))
        {
            state = new WallCellState();
            _cells[cell] = state;
        }

        state.typeId = typeId;
        state.itemId = itemId;
        state.maxHealth = Mathf.Max(1, maxHealth);
        state.currentHealth = state.maxHealth;
        state.stoppingPower = Mathf.Max(0, stoppingPower);
        state.defenseRate = Mathf.Clamp(defenseRate, 0, 100);
        state.absoluteDefense = Mathf.Max(0, absoluteDefense);

        if (logDebug)
            Debug.Log($"[WallHealth] Register cell={cell} typeId={typeId} itemId={itemId} hp={state.maxHealth}");
    }

    public void RemoveWallCell(Vector3Int cell)              // 상태만 제거(선택)
    {
        if (_cells.Remove(cell) && logDebug)
            Debug.Log($"[WallHealth] Remove cell state={cell}");
    }

    public void ApplyHitToCell(Vector3Int cell, CombatPayload2D payload) // 피격 적용
    {
        if (payload == null) return;

        if (!_cells.TryGetValue(cell, out var state))
        {
            if (logDebug)
                Debug.Log($"[WallHealth] No registered wall at {cell}, ignore.");
            return;
        }

        int absPow = Mathf.Max(0, payload.absolutePower);     // 절대 위력
        int absDef = Mathf.Max(0, state.absoluteDefense);     // 절대 방어

        if (absPow <= absDef)                                 // 절대 방어에 막힘
        {
            if (logDebug)
                Debug.Log($"[WallHealth] Hit blocked at {cell} (AbsPow={absPow} <= AbsDef={absDef}).");
            return;
        }

        int rawDamage = Mathf.Max(0, payload.attackPower);    // 기본 공격력
        int rate = Mathf.Clamp(state.defenseRate, 0, 100);    // 방어율

        float reduced = rawDamage * (1f - rate / 100f);       // 방어율 적용
        int finalDamage = Mathf.Max(0, Mathf.RoundToInt(reduced)); // 최종 피해

        if (payload.canReducePenetration && state.stoppingPower > 0) // 저지력 적용
        {
            payload.ReducePenetration(state.stoppingPower);   // 관통력 감소
        }

        int before = state.currentHealth;
        state.currentHealth = Mathf.Max(0, state.currentHealth - finalDamage);

        if (logDebug)
            Debug.Log($"[WallHealth] cell={cell} HP {before}->{state.currentHealth} (damage={finalDamage}, rate={rate}%)");

        if (state.currentHealth <= 0)
        {
            DestroyWallCell(cell);
        }
    }

    private void DestroyWallCell(Vector3Int cell)            // 벽 파괴 처리
    {
        _cells.Remove(cell);                                  // 상태 제거

        if (wallTilemap != null)                              // 타일 삭제
        {
            wallTilemap.SetTile(cell, null);
            wallTilemap.SetTransformMatrix(cell, Matrix4x4.identity);
            wallTilemap.RefreshTile(cell);
        }

        _autoTiler?.RefreshAround(cell);                      // 주변 자동 타일 갱신

        if (navSurface != null)                               // NavMesh 리베이크
        {
            navSurface.BuildNavMesh();
        }

        OnWallDestroyed?.Invoke(cell);                        // 외부 알림

        if (logDebug)
            Debug.Log($"[WallHealth] Destroy wall at {cell}");
    }

    public bool HasWallAt(Vector3Int cell)                    // 등록 여부 확인
    {
        return _cells.ContainsKey(cell);
    }
}
