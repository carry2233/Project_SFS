using System;
using System.Collections.Generic;
using System.Reflection;               // 리플렉션
using UnityEngine;

[AddComponentMenu("Build/Build Requirement Service (Type→Item Priority)")]
public class BuildRequirementService : MonoBehaviour
{
    [Header("디버그")]
    [SerializeField] private bool verbose = false;               // ✅ 디버그 로그 On/Off

    public event Action OnAssignChanged; // ✅ 자원 할당 변화 이벤트(UI 갱신 트리거)

    [Serializable]
    public struct RequirementSnapshot // ✅ UI에 뿌릴 스냅샷 단위 (미사용 시 그대로 유지)
    {
        public int typeId;             // 타입 id
        public int itemId;             // 아이템 id
        public int requiredCount;      // 필요 개수
        public int assignedCount;      // 현재 할당된 개수
        public string displayName;     // 표시 이름
        public Sprite icon;            // 아이콘(인벤/카탈로그에서 공급)
    }

    // ───────────── [수정] 셀 단위 할당 누적 테이블 ─────────────
    // 키 포맷: "C:x,y:typeId:itemId"  (타입 기반은 itemId=0, 아이템ID 기반은 typeId=0 사용)
    private readonly Dictionary<string, int> cellAssignedMap = new();   // ✅ 셀별 할당 수

    // ───────────────────── 공용 진입점(보유/소비) ─────────────────────
public bool HasResources(Inventory inv, BuildItemData item)          // 보유 검증(통합 구조)
{
    if (!inv || item == null || item.requirements == null || item.requirements.Length == 0)
        return false;

    var items = GetItemsList(inv);                                   // 인벤토리 아이템 리스트
    if (items == null) return false;

    foreach (var need in item.requirements)                          // 각 요구 재료에 대해
    {
        int have = CountAvailable(items, need.typeId, need.itemId);  // (typeId,itemId) 조합 보유 수
        if (have < need.count) return false;                         // 하나라도 부족하면 실패
    }
    return true;
}


public bool TryConsume(Inventory inv, BuildItemData item)            // (설치 시) 소모 시도
{
    if (!inv || item == null || item.requirements == null || item.requirements.Length == 0)
        return false;

    var list = GetItemsList(inv);                                    // 인벤토리 아이템 리스트
    if (list == null) return false;

    foreach (var need in item.requirements)                          // 각 요구 재료별로
    {
        if (!TryConsumeOneGroupByTypeAndItem(list, need.typeId, need.itemId, need.count))
        {
            if (verbose)
                Debug.LogWarning($"[BuildReq] 소비 실패(typeId:{need.typeId}, itemId:{need.itemId})");
            return false;                                            // 중간에 부족하면 실패
        }
    }

    RaiseInventoryChanged(inv);                                      // 인벤토리 UI 갱신
    return true;
}


    // ───────────────────── [추가] 좌클릭 1회 = 1개 할당 ─────────────────────
public bool TryAssignOne(Inventory inv, BuildItemData item, Vector3Int cell) // 좌클릭 한 번으로 1개 할당
{
    if (inv == null || item == null || item.requirements == null)
        return false;

    foreach (var need in item.requirements)
    {
        int assigned = GetAssignedCount(cell, need.typeId, need.itemId); // 현재 셀에 할당된 수
        if (assigned < need.count)                                       // 아직 부족한 요구 재료이면
        {
            if (TryConsumeOneByTypeAndItem(inv, need.typeId, need.itemId)) // 인벤에서 1개 차감
            {
                IncreaseAssigned(cell, need.typeId, need.itemId, +1);      // 셀 할당 +1
                RaiseInventoryChanged(inv);                                 // 인벤 UI 갱신
                RaiseAssignChanged();                                       // 할당 UI 갱신
                return true;
            }
            else
            {
                if (verbose)
                    Debug.Log($"[BuildReq] 자원 부족 (typeId:{need.typeId}, itemId:{need.itemId})");
                return false;
            }
        }
    }

    // 모든 요구가 이미 채워져 있으면 추가 할당 불필요
    return false;
}


    public int GetAssignedCount(Vector3Int cell, int typeId, int itemId) // ✅ 특정 셀·자원의 현재 할당 수
    {
        string key = Key(cell, typeId, itemId);
        return cellAssignedMap.TryGetValue(key, out var cnt) ? cnt : 0;
    }

public bool IsCellFullyAssigned(Vector3Int cell, BuildItemData item) // 해당 셀의 요구 전부 충족 여부
{
    if (item == null || item.requirements == null || item.requirements.Length == 0)
        return false;

    foreach (var need in item.requirements)
    {
        if (GetAssignedCount(cell, need.typeId, need.itemId) < need.count) // 하나라도 부족하면
            return false;
    }
    return true;
}


    // ───────────────────── 타입 기준 (기존 유지) ─────────────────────
public bool HasByTypes(Inventory inv, BuildItemData item)    // (구조 변경 후) 통합 보유 검증 래퍼
{
    return HasResources(inv, item);
}

public bool TryConsumeByTypes(Inventory inv, BuildItemData item) // (구조 변경 후) 통합 소비 래퍼
{
    return TryConsume(inv, item);
}

    // ───────────────────── 아이템ID 기준 (기존 유지) ─────────────────────
public bool HasByItems(Inventory inv, BuildItemData item)    // (구조 변경 후) 통합 보유 검증 래퍼
{
    return HasResources(inv, item);
}

public bool TryConsumeByItems(Inventory inv, BuildItemData item) // (구조 변경 후) 통합 소비 래퍼
{
    return TryConsume(inv, item);
}

    // ───────────────────── [추가] 1개만 차감 유틸 ─────────────────────
    private bool TryConsumeOneByType(Inventory inv, int typeId)  // ✅ 타입 일치 1개 차감
    {
        var list = GetItemsList(inv);
        if (list == null) return false;
        return TryConsumeOne(list, key: "typeId", keyValue: typeId);
    }

    private bool TryConsumeOneByItemId(Inventory inv, int itemId) // ✅ 아이템ID 일치 1개 차감
    {
        var list = GetItemsList(inv);
        if (list == null) return false;
        return TryConsumeOne(list, key: "itemId", keyValue: itemId);
    }

    private bool TryConsumeOne(List<object> items, string key, int keyValue) // ✅ 리스트에서 1개 차감
    {
        for (int i = 0; i < items.Count; i++)
        {
            var it = items[i];
            if (ReadInt(it, key) != keyValue) continue;

            int cnt = ReadInt(it, "count");
            if (cnt <= 0) continue;

            WriteInt(it, "count", cnt - 1);                 // 1개 차감
            if (ReadInt(it, "count") <= 0) items.RemoveAt(i); // 0 스택 제거(선택)
            return true;
        }
        return false;
    }

    // ───────────────────── 내부 유틸(리플렉션 기반) ─────────────────────

    private List<object> GetItemsList(Inventory inv)                     // 인벤토리 아이템 리스트 취득
    {
        var t = inv.GetType();

        var f = t.GetField("Items", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (f != null)
        {
            var val = f.GetValue(inv) as System.Collections.IEnumerable;
            return ToObjList(val);
        }

        var p = t.GetProperty("Items", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (p != null)
        {
            var val = p.GetValue(inv, null) as System.Collections.IEnumerable;
            return ToObjList(val);
        }

        var m = t.GetMethod("GetItems", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (m != null)
        {
            var val = m.Invoke(inv, null) as System.Collections.IEnumerable;
            return ToObjList(val);
        }

        Debug.LogError("[BuildReq] Inventory에서 아이템 리스트를 찾지 못했습니다(Items/GetItems 미존재).");
        return null;
    }

    private List<object> ToObjList(System.Collections.IEnumerable enumerable) // IEnumerable→List<object>
    {
        var list = new List<object>();
        if (enumerable == null) return list;
        foreach (var it in enumerable) list.Add(it);
        return list;
    }

    private Dictionary<string, int> BuildCountMap(List<object> items, string key) // 보유 합계 맵
    {
        var map = new Dictionary<string, int>();
        foreach (var it in items)
        {
            int id = ReadInt(it, key);
            int cnt = ReadInt(it, "count");
            if (!map.ContainsKey($"{id}")) map[$"{id}"] = 0;
            map[$"{id}"] += Mathf.Max(0, cnt);
        }
        return map;
    }

    private bool TryConsumeOneGroup(List<object> items, string key, int keyValue, int needCount) // 그룹 차감
    {
        int remain = needCount;
        for (int i = 0; i < items.Count && remain > 0; i++)
        {
            var it = items[i];
            if (ReadInt(it, key) != keyValue) continue;

            int cnt = ReadInt(it, "count");
            if (cnt <= 0) continue;

            int take = Mathf.Min(cnt, remain);
            WriteInt(it, "count", cnt - take);
            remain -= take;
        }

        items.RemoveAll(o => ReadInt(o, "count") <= 0);
        return remain <= 0;
    }

    private int ReadInt(object obj, string name)                         // int 필드/프로퍼티 읽기
    {
        if (obj == null) return 0;
        var t = obj.GetType();
        var f = t.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (f != null) return Convert.ToInt32(f.GetValue(obj));

        var p = t.GetProperty(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (p != null) return Convert.ToInt32(p.GetValue(obj, null));

        if (verbose) Debug.LogWarning($"[BuildReq] {t.Name}.{name} 필드를 찾지 못함(int)");
        return 0;
    }

    private void WriteInt(object obj, string name, int value)            // int 필드/프로퍼티 쓰기
    {
        if (obj == null) return;
        var t = obj.GetType();
        var f = t.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (f != null) { f.SetValue(obj, value); return; }

        var p = t.GetProperty(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (p != null && p.CanWrite) { p.SetValue(obj, value, null); return; }

        if (verbose) Debug.LogWarning($"[BuildReq] {t.Name}.{name} 쓰기 실패(int)");
    }

    private void RaiseInventoryChanged(Inventory inv)                    // 인벤토리 변경 알림 시도
    {
        // [수정] Inventory는 ForceRefresh()를 제공 → 이를 우선 호출 시도
        var t = inv.GetType();

        // 우선순위 1: ForceRefresh()
        var mForce = t.GetMethod("ForceRefresh", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (mForce != null) { mForce.Invoke(inv, null); return; }

        // 우선순위 2~4: 기존 호환 메서드들
        var m1 = t.GetMethod("NotifyChanged", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        var m2 = t.GetMethod("RaiseOnInventoryChanged", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        var m3 = t.GetMethod("RefreshUI", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (m1 != null) { m1.Invoke(inv, null); return; }
        if (m2 != null) { m2.Invoke(inv, null); return; }
        if (m3 != null) { m3.Invoke(inv, null); return; }

        // 그래도 실패하면 경고(디버깅 도움)
        if (verbose) Debug.LogWarning("[BuildReq] 인벤토리 변경 알림을 호출할 수 없습니다.");
    }

    internal void RaiseAssignChanged() // ✅ 내부 로직에서 호출: 할당 변화 알림
    {
        OnAssignChanged?.Invoke();     // UI 즉시 갱신
    }

    // ───────────── [추가] 셀-자원 키/누적 유틸 ─────────────
    private static string Key(Vector3Int cell, int typeId, int itemId)  // ✅ 셀+자원 키 생성
    {
        return $"C:{cell.x},{cell.y}:{typeId}:{itemId}";
    }

    private void IncreaseAssigned(Vector3Int cell, int typeId, int itemId, int delta) // ✅ 셀 할당 증감
    {
        string key = Key(cell, typeId, itemId);
        if (!cellAssignedMap.ContainsKey(key)) cellAssignedMap[key] = 0;
        cellAssignedMap[key] = Mathf.Max(0, cellAssignedMap[key] + delta);
    }

    private int CountAvailable(List<object> items, int typeId, int itemId) // (typeId,itemId) 조합 보유 합계
{
    int sum = 0;
    for (int i = 0; i < items.Count; i++)
    {
        var it = items[i];
        if (ReadInt(it, "typeId") != typeId) continue;
        if (ReadInt(it, "itemId") != itemId) continue;

        int cnt = ReadInt(it, "count");
        if (cnt > 0) sum += cnt;
    }
    return sum;
}

private bool TryConsumeOneByTypeAndItem(Inventory inv, int typeId, int itemId) // (typeId,itemId) 1개 차감
{
    var list = GetItemsList(inv);                            // 인벤토리 리스트
    if (list == null) return false;
    return TryConsumeOneByTypeAndItem(list, typeId, itemId);
}

private bool TryConsumeOneByTypeAndItem(List<object> items, int typeId, int itemId) // 리스트에서 1개 차감
{
    for (int i = 0; i < items.Count; i++)
    {
        var it = items[i];
        if (ReadInt(it, "typeId") != typeId) continue;
        if (ReadInt(it, "itemId") != itemId) continue;

        int cnt = ReadInt(it, "count");
        if (cnt <= 0) continue;

        WriteInt(it, "count", cnt - 1);                      // 1개 차감
        if (ReadInt(it, "count") <= 0)
            items.RemoveAt(i);                               // 0 스택 제거(옵션)
        return true;
    }
    return false;
}

private bool TryConsumeOneGroupByTypeAndItem(List<object> items, int typeId, int itemId, int needCount)
// (typeId,itemId) 조합으로 여러 개 차감
{
    int remain = needCount;
    for (int i = 0; i < items.Count && remain > 0; i++)
    {
        var it = items[i];
        if (ReadInt(it, "typeId") != typeId) continue;
        if (ReadInt(it, "itemId") != itemId) continue;

        int cnt = ReadInt(it, "count");
        if (cnt <= 0) continue;

        int take = Mathf.Min(cnt, remain);
        WriteInt(it, "count", cnt - take);
        remain -= take;
    }

    items.RemoveAll(o => ReadInt(o, "count") <= 0);          // 0 스택 제거
    return remain <= 0;
}

// ───────────── [추가] 인벤토리 보유 개수 조회 ─────────────
public int GetOwnedCount(Inventory inv, int typeId, int itemId) // 인벤토리에서 (typeId,itemId) 보유 개수 조회
{
    if (!inv) return 0;
    var items = GetItemsList(inv);                // 인벤토리 아이템 리스트 가져오기
    if (items == null) return 0;

    return CountAvailable(items, typeId, itemId); // (typeId,itemId) 조합으로 보유 수 계산
}



    // ▼ 아래 4개 메서드는 예시 그대로 유지 (스냅샷/해석) ─ 필요 시 나중에 확장
    public IReadOnlyList<RequirementSnapshot> GetCurrentRequirements() { var list = new List<RequirementSnapshot>(); foreach (var req in GetInternalRequirementTable()) { int typeId = req.typeId; int itemId = req.itemId; int need = req.requiredCount; int have = 0; list.Add(new RequirementSnapshot{ typeId=typeId, itemId=itemId, requiredCount=need, assignedCount=have, displayName=ResolveDisplayName(typeId,itemId), icon=ResolveIcon(typeId,itemId)}); } return list; }
    private IEnumerable<(int typeId, int itemId, int requiredCount)> GetInternalRequirementTable() { yield break; }
    private string ResolveDisplayName(int typeId, int itemId) { return $"Item({typeId}:{itemId})"; }
    private Sprite ResolveIcon(int typeId, int itemId) { return null; }
}
