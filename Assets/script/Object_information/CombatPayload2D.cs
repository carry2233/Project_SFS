using UnityEngine;                                          // 유니티 기본
using System.Collections.Generic;                           // 리스트/해시셋

[AddComponentMenu("Combat/Combat Payload 2D")]              // 인스펙터 메뉴 경로
public class CombatPayload2D : MonoBehaviour
{
    // ─────────────────────────────────────────────────────────────────────────────
    // 식별/라벨 (레거시 호환)
    // ─────────────────────────────────────────────────────────────────────────────
    [Header("식별(레거시 호환)")]
    public int typeId;                                       // 장비 타입 ID             // 레거시 호환
    public int itemId;                                       // 아이템 ID                // 레거시 호환

    [Header("라벨(레거시 호환)")]
    public string ownerName;                                 // 소유자 라벨              // 레거시 호환
    public string sourceName;                                // 히트 소스 라벨           // 레거시 호환

    [Header("출처/레이블(새)")]
    [SerializeField] private string sourceLabel = "";        // 표시용 라벨(없으면 owner/source 조합) // 새

    // ─────────────────────────────────────────────────────────────────────────────
    // 전투 수치 (기존 유지)
    // ─────────────────────────────────────────────────────────────────────────────
    [Header("공격 수치")]
    public int attackPower = 10;                             // 공격력                    // 방어율 적용 대상
    public int absolutePower = 0;                            // 절대 위력                 // 절대방어와 비교

    [Header("출혈 부여")]
    public int bleedRate = 0;                                // 초당 출혈 수치

    // ─────────────────────────────────────────────────────────────────────────────
    // 관통력 (기존 유지)
    // ─────────────────────────────────────────────────────────────────────────────
    [Header("관통력(저지력과 상호작용)")]
    public bool canReducePenetration = true;                 // 관통력 감소 허용 여부
    public int maxPenetration = 0;                           // 최대 관통력(초기치/상한)
    public int currentPenetration = 0;                       // 현재 관통력(0 이하면 비활성화)

    // ─────────────────────────────────────────────────────────────────────────────
    // ⭐ 무시(상호작용 제외) 규칙
    // ─────────────────────────────────────────────────────────────────────────────
    [Header("상호작용 무시 규칙")]
    [SerializeField] private LayerMask ignoreLayers;         // 무시할 레이어 마스크              // 레이어 기반
    [SerializeField] private List<ObjectInfo> ignoreObjectInfos = new List<ObjectInfo>(); // 무시할 ObjectInfo 목록

    private HashSet<ObjectInfo> ignoreSet = new HashSet<ObjectInfo>();   // 빠른 포함 체크용 캐시

    // ⭐ 공격자(발사자/소유자) – AI 추적용 핵심 정보
    [SerializeField] private Transform attacker;   // 실제 공격 주체(투사체 X, 발사자 O)

    public Transform Attacker => attacker;          // 읽기 전용 접근자


    // ─────────────────────────────────────────────────────────────────────────────
    // 라이프사이클
    // ─────────────────────────────────────────────────────────────────────────────
    private void Awake()                                     // 초기화(보정만)
    {
        Reset();                                             // 수치 보정
        // 캐시 재구축은 Start에서(직렬화 복원 이후 보장)
    }

    private void Start()                                     // 직렬화 복원 후 캐시 구성
    {
        RebuildIgnoreCache();                                // 리스트 → 해시셋
    }

    public void SetAttacker(Transform attackerTransform) // 공격자 주입
{
    attacker = attackerTransform;
}

private void OnEnable()
{
    attacker = null; // 풀 재사용 시 이전 공격자 정보 제거
}


    private void OnValidate()                                // 인스펙터 변경 시 재구축
    {
        RebuildIgnoreCache();
    }

    private void Reset()                                     // 인스펙터 기본값 보정
    {
        maxPenetration = Mathf.Max(0, maxPenetration);
        currentPenetration = Mathf.Clamp(currentPenetration, 0, maxPenetration);
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // 라벨
    // ─────────────────────────────────────────────────────────────────────────────
    public string SourceLabel()                              // 표시용 라벨 반환
    {
        if (!string.IsNullOrEmpty(sourceLabel)) return sourceLabel;
        if (!string.IsNullOrEmpty(ownerName) && !string.IsNullOrEmpty(sourceName)) return $"{ownerName}/{sourceName}";
        if (!string.IsNullOrEmpty(ownerName)) return ownerName;
        if (!string.IsNullOrEmpty(sourceName)) return sourceName;
        return name;
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // 관통력 로직
    // ─────────────────────────────────────────────────────────────────────────────
    public void ReducePenetration(int amount)                // 관통력 감소 적용
    {
        if (!canReducePenetration || amount <= 0) return;
        currentPenetration = Mathf.Max(0, currentPenetration - amount);
        if (currentPenetration <= 0) gameObject.SetActive(false);
    }

    public void InjectIdentity(                              // 식별/라벨 일괄 주입(옵션)
        int newTypeId, int newItemId,
        string newOwnerName = null, string newSourceName = null,
        string newSourceLabel = null)
    {
        typeId = newTypeId;                                  // 타입 ID 주입
        itemId = newItemId;                                  // 아이템 ID 주입
        if (!string.IsNullOrEmpty(newOwnerName)) ownerName = newOwnerName;        // 소유자 라벨 주입
        if (!string.IsNullOrEmpty(newSourceName)) sourceName = newSourceName;     // 소스 라벨 주입
        if (!string.IsNullOrEmpty(newSourceLabel)) sourceLabel = newSourceLabel;  // 단일 라벨 주입
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // ⭐ 무시 규칙: 캐시/추가/제거
    // ─────────────────────────────────────────────────────────────────────────────
    public void RebuildIgnoreCache()                         // 리스트 → 해시셋 재구축
    {
        ignoreSet.Clear();
        if (ignoreObjectInfos == null) return;
        for (int i = 0; i < ignoreObjectInfos.Count; i++)
        {
            var oi = ignoreObjectInfos[i];
            if (oi != null) ignoreSet.Add(oi);
        }
    }

    public void AddIgnore(ObjectInfo oi)                     // 무시 목록 추가
    {
        if (oi == null) return;
        if (ignoreObjectInfos == null) ignoreObjectInfos = new List<ObjectInfo>();
        if (!ignoreObjectInfos.Contains(oi)) ignoreObjectInfos.Add(oi);
        ignoreSet.Add(oi);
    }

    public void RemoveIgnore(ObjectInfo oi)                  // 무시 목록 제거
    {
        if (oi == null) return;
        if (ignoreObjectInfos != null) ignoreObjectInfos.Remove(oi);
        ignoreSet.Remove(oi);
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // ⭐ 무시 규칙: 헬퍼(부모/자식 탐색 + 루트 레이어 확인)
    // ─────────────────────────────────────────────────────────────────────────────
    private static bool TryGetObjectInfoDeep(GameObject go, out ObjectInfo oi) // ObjectInfo 깊게 탐색
    {
        oi = null;
        if (!go) return false;
        oi = go.GetComponentInParent<ObjectInfo>();          // 1) 부모부터 탐색(실전에서 가장 흔함)
        if (oi) return true;
        oi = go.GetComponent<ObjectInfo>();                  // 2) 자기 자신
        if (oi) return true;
        oi = go.GetComponentInChildren<ObjectInfo>();        // 3) 자식
        return oi != null;
    }

    private bool IsLayerIgnoredByMask(GameObject go)         // 레이어 마스크 무시 여부(루트까지 확인)
    {
        if (!go) return true;
        int l = go.layer;
        if (((1 << l) & ignoreLayers.value) != 0) return true;                  // 자체 레이어 체크
        var root = go.transform.root != null ? go.transform.root.gameObject : null;
        if (root != null && root != go)
        {
            int rl = root.layer;
            if (((1 << rl) & ignoreLayers.value) != 0) return true;            // 루트 레이어 추가 체크
        }
        return false;
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // ⭐ 무시 규칙: 쿼리(오버로드)
    // ─────────────────────────────────────────────────────────────────────────────
    public bool IsIgnored(GameObject go)                      // GameObject 무시 여부
    {
        if (!go) return true;                                 // null 안전
        if (IsLayerIgnoredByMask(go)) return true;            // 레이어(자체/루트) 무시

        // ObjectInfo: 부모 → 자기자신 → 자식 순으로 탐색
        if (TryGetObjectInfoDeep(go, out var oi))
        {
            // 해시셋 또는 리스트에 포함되어 있으면 무시
            if ((ignoreSet != null && ignoreSet.Contains(oi)) ||
                (ignoreObjectInfos != null && ignoreObjectInfos.Contains(oi)))
                return true;
        }
        return false;
    }

    public bool IsIgnored(ObjectInfo oi)                      // ObjectInfo 무시 여부
    {
        if (!oi) return true;                                 // null 안전
        var go = oi.gameObject;
        if (IsLayerIgnoredByMask(go)) return true;            // 레이어(자체/루트) 무시
        if ((ignoreSet != null && ignoreSet.Contains(oi)) ||
            (ignoreObjectInfos != null && ignoreObjectInfos.Contains(oi)))
            return true;
        return false;
    }

    public bool IsIgnored(Collider2D col)                     // Collider2D 무시 여부
    {
        if (!col) return true;
        return IsIgnored(col.gameObject);
    }

    public bool ShouldInteract(GameObject go)                 // 상호작용 허용 여부(GameObject)
    {
        return !IsIgnored(go);
    }

    public bool ShouldInteract(ObjectInfo oi)                 // 상호작용 허용 여부(ObjectInfo)
    {
        return !IsIgnored(oi);
    }

    public bool ShouldInteract(Collider2D col)                // 상호작용 허용 여부(Collider2D)
    {
        return !IsIgnored(col);
    }
}
