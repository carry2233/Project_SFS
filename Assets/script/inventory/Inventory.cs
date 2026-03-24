using System;                                       // Action 이벤트용
using System.Collections.Generic;                   // List<T>
using UnityEngine;                                  // MonoBehaviour 등

[AddComponentMenu("Inventory/Inventory (Stack Merge + Name/Icon/Durability/Inventory")]
[DisallowMultipleComponent]
public class Inventory : MonoBehaviour              // 인벤토리 본체
{
    [Serializable]
    public class ItemInstance                       // 인벤토리 한 스택 정보
    {
        [Header("식별/키")]
        public string stackId;                      // 스택 고유 id
        public int typeId;                          // 종류 id
        public int itemId;                          // 아이템 id

        [Header("표시")]
        public string displayName;                  // 표시 이름
        public Sprite icon;                         // 아이콘

        [Header("수량/상태")]
        public int count;                           // 수량
        public int durability;                      // 내구도
        public int maxDurability;                   // 최대 내구도
        public float weight;                        // 무게

        public string Key => StackRuleRegistry.MakeKey(typeId, itemId); // 스택 키
    }

    [Header("보관 데이터")]
    public List<ItemInstance> items = new();        // 인벤토리 스택 리스트

    public event Action<List<int>> OnInventoryChanged; // 변경된 인덱스 리스트(null=전체)

    // ─────────────────────────────────────────────
    // 내부: count<=0 인 스택 제거
    // ─────────────────────────────────────────────
    private bool RemoveEmptyStacks()                // 0개 스택 제거 후 true/false 반환
    {
        bool removed = false;
        for (int i = items.Count - 1; i >= 0; i--)
        {
            if (items[i] == null || items[i].count <= 0)
            {
                items.RemoveAt(i);
                removed = true;
            }
        }
        return removed;
    }

    public bool AddItem(                            // 아이템 추가(스택 병합)
        int typeId, int itemId, int addCount,
        string displayName, Sprite icon,
        int durability, int maxDurability, float weight)
    {
        if (addCount <= 0) return false;

        // 스택 규칙 조회
        StackRuleRegistry.Instance.GetRuleOrDefault(
            typeId, itemId, out bool canStack, out int maxStack);

        string key = StackRuleRegistry.MakeKey(typeId, itemId); // 동일 스택 키
        var changed = new List<int>();                          // 변경된 인덱스 목록

        // 1) 스택 가능하면 기존 스택에 먼저 채움
        if (canStack)
        {
            for (int i = 0; i < items.Count && addCount > 0; i++)
            {
                var inst = items[i];
                if (inst.Key != key) continue;

                int remain = maxStack - inst.count;
                if (remain <= 0) continue;

                int put = Mathf.Min(remain, addCount);
                inst.count += put;
                addCount   -= put;

                // 기존 스택에 maxDur 정보가 없으면 새 maxDur 적용
                if (inst.maxDurability <= 0 && maxDurability > 0)
                    inst.maxDurability = maxDurability;

                changed.Add(i);
            }
        }

        // 2) 남은 개수는 새 스택 생성
        while (addCount > 0)
        {
            int put = canStack ? Mathf.Min(addCount, maxStack) : 1;
            addCount -= put;

            var newInst = new ItemInstance
            {
                typeId        = typeId,
                itemId        = itemId,
                displayName   = displayName,
                icon          = icon,
                count         = put,
                durability    = durability,
                maxDurability = maxDurability,
                weight        = weight
            };

            items.Add(newInst);
            changed.Add(items.Count - 1);
        }

        OnInventoryChanged?.Invoke(changed);        // 부분 갱신 이벤트
        return true;
    }

public int ConsumeItem(int typeId, int itemId, int needCount) // (기존) 키 기반 차감
{
    if (needCount <= 0) return 0;
    string key = StackRuleRegistry.MakeKey(typeId, itemId);

    Debug.Log($"[Inventory] ConsumeItem 호출 key={key}, needCount={needCount}"); // ✅ 디버그

    var changed = new List<int>();
    int removed = 0;

    for (int i = 0; i < items.Count && needCount > 0; i++)
    {
        var it = items[i];
        if (it.Key != key) continue;

        int take = Mathf.Min(it.count, needCount);
        it.count   -= take;
        needCount  -= take;
        removed    += take;
        changed.Add(i);
    }

    bool cleaned = RemoveEmptyStacks();         // 0개 스택 제거
    Debug.Log($"[Inventory] ConsumeItem 완료 removed={removed}, cleaned={cleaned}, items.Count={items.Count}"); // ✅ 디버그

    if (cleaned)
    {
        OnInventoryChanged?.Invoke(null);       // 전체 리프레시
    }
    else
    {
        OnInventoryChanged?.Invoke(changed);    // 부분 리프레시
    }

    return removed;
}


    // ─────────────────────────────────────────────
    // [신규] 인덱스 기반 차감 (방법 B 핵심)
    // ─────────────────────────────────────────────
public int ConsumeItemAtIndex(                  // 인벤토리 인덱스로 정확히 차감
    int index,                                  // 차감할 인덱스
    int needCount)                              // 차감 수량
{
    Debug.Log($"[Inventory] ConsumeItemAtIndex 호출 index={index}, needCount={needCount}, items.Count={items.Count}"); // ✅ 디버그

    if (needCount <= 0) return 0;
    if (index < 0 || index >= items.Count)
    {
        Debug.LogWarning($"[Inventory] ConsumeItemAtIndex 무시 - index 범위 밖 (index={index}, items.Count={items.Count})");
        return 0;
    }

    var inst = items[index];
    Debug.Log($"[Inventory] Before index={index}, typeId={inst.typeId}, itemId={inst.itemId}, count={inst.count}"); // ✅ 디버그

    int take = Mathf.Min(inst.count, needCount);
    inst.count -= take;
    int removed = take;

    bool removedStack = false;

    if (inst.count <= 0)
    {
        items.RemoveAt(index);                  // 스택 자체 삭제
        removedStack = true;
        Debug.Log($"[Inventory] Stack removed at index={index}"); // ✅ 디버그
    }
    else
    {
        Debug.Log($"[Inventory] After index={index}, count={inst.count}, removed={removed}"); // ✅ 디버그
    }

    if (removedStack)
    {
        OnInventoryChanged?.Invoke(null);       // 인덱스가 모두 바뀌었으므로 전체 리프레시
    }
    else
    {
        OnInventoryChanged?.Invoke(
            new List<int> { index });           // 해당 인덱스만 부분 갱신
    }

    return removed;
}


    public void ForceRefresh()                      // 강제 전체 리프레시
    {
        bool cleaned = RemoveEmptyStacks();
        OnInventoryChanged?.Invoke(null);
    }

    public IReadOnlyList<ItemInstance> GetItems() => items; // 읽기 전용 뷰
}
