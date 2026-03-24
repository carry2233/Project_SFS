// ApparelWearStateRegistry.cs — (변경) 방어 스냅샷 내보내기 유틸 추가
using System;                     // 직렬화, Action
using System.Collections.Generic; // 리스트/딕셔너리
using UnityEngine;                // 유니티 엔진

[AddComponentMenu("Apparel/Apparel Wear State Registry (화이트리스트 + 메타 참조 저장 + LinkedObject 토글)")]
[DisallowMultipleComponent]
public class ApparelWearStateRegistry : MonoBehaviour
{
    // ========== 내부 데이터 구조 ==========

    [Serializable]
    public class MirrorEntry // (tier,slot) 화이트리스트 + 현재/이전 메타 참조
    {
        [Header("키(화이트리스트)")]
        public int wearSlotTier;                 // 착용부위단계(정수)  // 화이트리스트 키
        public int wearSlot;                     // 착용부위(정수)      // 화이트리스트 키

        [Header("현재/이전 의류 메타 참조")]
        public ApparelTypeRegistry.ApparelData current; // 현재 착용 메타 참조
        public ApparelTypeRegistry.ApparelData prev;    // 직전 착용 메타 참조(복원용)
        public bool hasPrev;                            // 이전 메타 보유 여부 플래그
    }

    // ========== 인스펙터 설정/참조 ==========

    [Header("화이트리스트 슬롯(등록된 (단계,부위)만 착용 허용)")]
    public List<MirrorEntry> entries = new();           // Inventory 스타일 리스트 // 화이트리스트

    [Header("의류 메타 레지스트리(필수: 메타 조회/부위 판정)")]
    public ApparelTypeRegistry apparelRegistry;         // 의류 메타 레지스트리   // 메타 조회

    [Header("정책/디버그 옵션")]
    public bool enforceWhitelist = true;                // true면 미등록 부위 거부  // 거부 정책
    public bool autoToggleLinkedObjects = true;         // 메타 변경 시 linkedObject를 자동 ON/OFF할지
    public bool logToggles = false;                     // ON/OFF 토글 로그 출력 여부

    public event Action OnChanged;                      // 변경 이벤트(UI/동기화 리프레시)

    // ========== 런타임 캐시 ==========

    private readonly Dictionary<string, int> _indexMap = new(); // "tier:slot"→entries 인덱스 // 인덱스 맵

    // ========== 생명주기 ==========

    private void Awake()                 // 초기 인덱스 구성
    {
        RebuildIndex();                  // 인덱스 재구축
    }

    private void OnValidate()            // 에디터 값 변경 시 인덱스 재구성
    {
        RebuildIndex();                  // 인덱스 재구축
    }

    // ========== 인덱스/키 유틸 ==========

    public void RebuildIndex()           // 리스트→딕셔너리 재구축
    {
        _indexMap.Clear(); // 기존 맵 초기화
        for (int i = 0; i < entries.Count; i++)
        {
            var e = entries[i];
            if (e == null) continue;
            _indexMap[MakeKey(e.wearSlotTier, e.wearSlot)] = i;
        }
    }

    public static string MakeKey(int tier, int slot) // "tier:slot" 키
    {
        return $"{tier}:{slot}";
    }

    // ========== 화이트리스트 API ==========

    public bool HasRegisteredSlot(int tier, int slot) // (tier,slot) 등록 여부
    {
        return _indexMap.ContainsKey(MakeKey(tier, slot));
    }

    public int RegisterSlotIfMissing(int tier, int slot) // 없으면 빈 항목 등록 후 인덱스 반환
    {
        var key = MakeKey(tier, slot);
        if (_indexMap.TryGetValue(key, out int idx))
            return idx;

        var entry = new MirrorEntry
        {
            wearSlotTier = tier,
            wearSlot = slot,
            current = null,
            prev = null,
            hasPrev = false
        };
        entries.Add(entry);
        idx = entries.Count - 1;
        _indexMap[key] = idx;
        OnChanged?.Invoke();
        return idx;
    }

    // ========== 조회 헬퍼 ==========

    public bool TryGetBySlot(int tier, int slot, out MirrorEntry entry) // (tier,slot)로 엔트리 조회
    {
        if (_indexMap.TryGetValue(MakeKey(tier, slot), out int idx))
        {
            entry = (idx >= 0 && idx < entries.Count) ? entries[idx] : null;
            return entry != null;
        }
        entry = null;
        return false;
    }

    public bool TryGetCurrentData(int tier, int slot, out ApparelTypeRegistry.ApparelData data) // 현재 메타 조회
    {
        data = null;
        if (!TryGetBySlot(tier, slot, out var e)) return false;
        if (e.current == null) return false;
        data = e.current;
        return true;
    }

    public bool TryGetRequiredSlot(int typeId, int itemId, out int tier, out int slot) // 의류 메타에서 (tier,slot) 가져오기
    {
        tier = 0; slot = 0;
        if (!apparelRegistry) return false;
        if (apparelRegistry.TryGetData(typeId, itemId, out var meta))
        {
            tier = meta.wearSlotTier;
            slot = meta.wearSlot;
            return true;
        }
        return false;
    }

    private void ToggleLinkedObject(GameObject obj, bool on) // 연결오브젝트 활성/비활성 토글
    {
        if (!obj) return;
        if (obj.activeSelf == on) return;
        obj.SetActive(on);
        if (logToggles) Debug.Log($"[WearState] LinkedObject {(on ? "ON" : "OFF")} : {obj.name}");
    }

    // ========== 핵심: 착용/복원 (메타 참조 저장 + Renderer 토글) ==========

    public bool ApplyWearByIds(int typeId, int itemId) // (편의) (typeId,itemId)만으로 착용 적용
    {
        // 1) 메타 조회
        if (!apparelRegistry || !apparelRegistry.TryGetData(typeId, itemId, out var meta))
        {
            Debug.LogWarning($"[WearState] 메타 조회 실패: ({typeId},{itemId})");
            return false;
        }

        // 2) 착용 부위 결정
        int tier = meta.wearSlotTier;
        int slot = meta.wearSlot;

        // 3) 화이트리스트 검사
        if (enforceWhitelist && !HasRegisteredSlot(tier, slot))
        {
            Debug.Log($"[WearState] 미등록 착용부위 → 거부 (tier,slot={tier},{slot})");
            return false;
        }

        // 4) 실제 적용
        return ApplyWearData(meta, tier, slot);
    }

    // ✅ 핵심: 착용 적용(메타 참조 저장 + Renderer 토글)
    public bool ApplyWearData(ApparelTypeRegistry.ApparelData meta, int wearSlotTier, int wearSlot)
    {
        if (meta == null)
        {
            Debug.LogWarning("[WearState] null 메타는 적용할 수 없습니다.");
            return false;
        }

        // 화이트리스트 강제 시 체크
        if (enforceWhitelist && !HasRegisteredSlot(wearSlotTier, wearSlot))
            return false;

        // 1) 대상 엔트리 확보
        int idx = RegisterSlotIfMissing(wearSlotTier, wearSlot);
        var e = entries[idx];

        // 2) 이전값 백업
        var prevCurrent = e.current;          // ← 이전 current 보관
        e.prev = e.current;
        e.hasPrev = (e.prev != null);

        // 3) 현재값 갱신(메타 참조 저장)
        e.current = meta;

        // 4) Renderer 토글 (이전 OFF → 현재 ON)
        if (autoToggleLinkedObjects)
        {
            if (TryResolveRenderer(prevCurrent, out var prevSr)) ToggleRenderer(prevSr, false); // 이전 OFF
            if (TryResolveRenderer(meta, out var curSr)) ToggleRenderer(curSr, true);   // 현재 ON
        }

        OnChanged?.Invoke();
        return true;
    }

    // ✅ 복원(이전 메타로 롤백, 없으면 빈 상태) + Renderer 토글
    public void RevertWear(int wearSlotTier, int wearSlot)
    {
        if (!TryGetBySlot(wearSlotTier, wearSlot, out var e))
            return;

        // 이전/현재 참조 저장(토글 대비)
        var before = e.current; // 바꾸기 전
        ApparelTypeRegistry.ApparelData after; // 바꾼 후

        if (e.hasPrev && e.prev != null)
        {
            e.current = e.prev;                       // prev → current 복원
            after = e.current;
        }
        else
        {
            e.current = null;
            e.prev = null;
            e.hasPrev = false;
            after = null;
        }

        // Renderer 토글 (이전 OFF → 이후 ON)
        if (autoToggleLinkedObjects)
        {
            if (TryResolveRenderer(before, out var beforeSr)) ToggleRenderer(beforeSr, false);
            if (TryResolveRenderer(after,  out var afterSr))  ToggleRenderer(afterSr,  true);
        }

        OnChanged?.Invoke();
    }

    // ========== 디버그/동기화 ==========

    [ContextMenu("Log All Entries")]
    public void LogAllEntries() // 전체 엔트리 로그(메타 참조 기반)
    {
        for (int i = 0; i < entries.Count; i++)
        {
            var e = entries[i];
            if (e == null) continue;
            string cur = e.current != null ? $"({e.current.typeId},{e.current.itemId})" : "(null)";
            string prv = e.prev != null ? $"({e.prev.typeId},{e.prev.itemId})" : "(null)";
            Debug.Log($"[WearState] [{i}] key=({e.wearSlotTier},{e.wearSlot}) current={cur} prev={prv} hasPrev={e.hasPrev}");
        }
    }

    [ContextMenu("Sync LinkedObject States")] // (이름은 호환을 위해 유지)
    public void SyncAllLinkedObjectStates()
    {
        if (!autoToggleLinkedObjects) return;

        // 1) 레지스트리에 등록된 모든 의류의 Renderer를 일괄 OFF
        if (apparelRegistry)
        {
            foreach (var data in apparelRegistry.apparelList)
            {
                if (data == null) continue;
                if (TryResolveRenderer(data, out var anySr)) ToggleRenderer(anySr, false);
            }
        }

        // 2) 현재 착용(current)만 ON
        foreach (var e in entries)
        {
            if (e == null || e.current == null) continue;
            if (TryResolveRenderer(e.current, out var curSr)) ToggleRenderer(curSr, true);
        }

        if (logToggles) Debug.Log("[WearState] SyncAllLinkedObjectStates(Renderer) 완료");
    }

    public IReadOnlyList<MirrorEntry> GetEntries() // 외부 읽기 전용 뷰
    {
        return entries;
    }

    // ✅ meta → SpriteRenderer 조회
    private bool TryResolveRenderer(ApparelTypeRegistry.ApparelData meta, out SpriteRenderer sr)
    {
        sr = null;                                        // 반환용
        if (meta == null || apparelRegistry == null)      // 방어코드
            return false;

        return apparelRegistry.TryResolveRenderer(meta.typeId, meta.itemId, out sr) && sr != null;
    }

    private void ToggleRenderer(SpriteRenderer sr, bool on) // 표시 On/Off
    {
        if (!sr) return;
        if (sr.enabled == on) return;
        sr.enabled = on;
        if (logToggles) Debug.Log($"[WearState] Renderer {(on ? "ENABLED" : "DISABLED")} : {sr.name}");
    }

    // ========== (추가) 방어 스냅샷 내보내기 유틸 ==========

    public struct DefenseSnapshot // PlayerInfo가 복사해서 ObjectInfo에 붙여넣을 구조
    {
        public int wearSlotTier;  // 착용 슬롯 티어
        public int wearSlot;      // 착용 슬롯 인덱스
        public int absoluteDefense; // 절대 방어
        public int defenseRate;     // 방어율(%)
    }

    public void ExportCurrentDefense(List<DefenseSnapshot> buffer) // 현재 착용 상태를 방어 스냅샷으로 내보내기
    {
        if (buffer == null) return;
        buffer.Clear();

        foreach (var e in entries)
        {
            if (e == null) continue;

            int tier = e.wearSlotTier;
            int slot = e.wearSlot;

            int abs = 0;
            int rate = 0;

            // 현재 메타에 방어치가 있다면 사용(없으면 0 처리)
            if (e.current != null)
            {
                // NOTE: ApparelData가 해당 필드를 가진다는 전제(없다면 0 유지)
                abs  = e.current.absoluteDefense;
                rate = Mathf.Clamp(e.current.defenseRate, 0, 100);
            }

            buffer.Add(new DefenseSnapshot
            {
                wearSlotTier   = tier,
                wearSlot       = slot,
                absoluteDefense= abs,
                defenseRate    = rate
            });
        }
    }

    // ▶ (추가) 현재 착용 중인 부위의 Renderer 조회
public bool TryGetCurrentRendererBySlot(int tier, int slot, out SpriteRenderer sr)
{
    sr = null;

    if (!TryGetBySlot(tier, slot, out var e)) 
        return false;

    if (e.current == null) 
        return false;

    return TryResolveRenderer(e.current, out sr);
}


    
}
