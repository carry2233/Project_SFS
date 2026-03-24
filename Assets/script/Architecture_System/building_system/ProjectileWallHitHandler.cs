using UnityEngine;                           // Unity 기본
using UnityEngine.Tilemaps;                  // Tilemap

public class ProjectileWallHitHandler : MonoBehaviour
{
    [Header("참조")]
    [SerializeField] private CombatPayload2D payload;         // 공격 데이터

    [Header("설정")]
    [SerializeField] private LayerMask wallLayer;             // 벽 레이어
    [SerializeField] private bool debugLog = false;           // 디버그 로그

    private Collider2D _collider;                             // 투사체 콜라이더 캐시

private void Awake()                                           // ✅ 초기화
{
    _collider = GetComponent<Collider2D>();                     // 콜라이더 캐시
    if (!payload) payload = GetComponent<CombatPayload2D>();     // payload 캐시
}


    private void Start()                                      // 시작 시 타일맵 자동 참조
    {

    }

private void OnTriggerEnter2D(Collider2D other)                 // ✅ 충돌 처리(벽 프리팹 방식)
{
    if (((1 << other.gameObject.layer) & wallLayer) == 0)       // 벽 레이어 아니면 무시
        return;

    if (payload == null)                                        // payload 없으면 종료
        return;

    WallPrefabController wall = other.GetComponentInParent<WallPrefabController>(); // ✅ 벽 컨트롤러 찾기(부모 포함)
    if (wall == null) wall = other.GetComponent<WallPrefabController>();            // ✅ 혹시 루트면 직접

    if (wall == null)                                           // 벽 컨트롤러 없으면 종료
        return;

    if (debugLog)
        Debug.Log($"[ProjectileWallHit] Hit wall prefab: {wall.name}");

    wall.ApplyHit(payload);                                     // ✅ 피격 처리(벽 프리팹이 체력/파괴 담당)

    if (payload.currentPenetration <= 0)                         // ✅ 관통력 소진 시 투사체 제거(기존 유지)
    {
        if (debugLog)
            Debug.Log("[ProjectileWallHit] Projectile stopped (penetration <= 0)");
        gameObject.SetActive(false);
    }
}

}
