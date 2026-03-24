using System;
using System.Collections.Generic;
using UnityEngine;

[AddComponentMenu("Apparel/Apparel Inventory (슬롯 기반 의류 저장소)")]
[DisallowMultipleComponent]
public class ApparelInventory : MonoBehaviour
{
    [Serializable]
    public class ApparelEntry // 의류 슬롯 엔트리(슬롯당 1개)
    {
        public int typeId;           // 종류 id
        public int itemId;           // 아이템 id
        public string displayName;   // 표시 이름
        public Sprite icon;          // 아이콘
        public int durability;       // 내구도
        public int maxDurability;      // 최대 내구도
        public float weight;         // 무게
        // (옵션) UI/툴팁용 메타 캐시 필드가 필요하면 추가 가능
    }

    [Header("설정")]
    [Min(1)] public int capacity = 4;     // 슬롯 수 제한(고정)

    public event Action OnChanged;        // 변경 이벤트(UI 리프레시 트리거)

    [System.NonSerialized] private List<ApparelEntry> _slots = new(); // 런타임 슬롯(인스펙터 숨김)
    public IReadOnlyList<ApparelEntry> Slots => _slots;               // 읽기 전용 뷰

       [Header("의류 착용 상태 레지스트리")]
    [SerializeField] private ApparelWearStateRegistry wearStateRegistry; // ✅ 현재 착용 상태/렌더러를 관리하는 레지스트리 참조

    private void Awake() // 초기 용량 보정
    {
        EnsureCapacity(); // capacity 만큼 null 슬롯 확보
    }

    public void EnsureCapacity() // 슬롯 리스트를 capacity에 맞춰 정렬/보정
    {
        if (_slots == null) _slots = new List<ApparelEntry>(capacity);
        while (_slots.Count < capacity) _slots.Add(null);
        while (_slots.Count > capacity) _slots.RemoveAt(_slots.Count - 1);
        OnChanged?.Invoke();
    }

// 새 시그니처 - 최대 내구도까지 함께 저장
public bool WearFirstEmpty(       // 첫 빈칸에 입기(최대 내구도 포함)
    int typeId,                   // 아이템 종류 id
    int itemId,                   // 아이템 id
    string displayName,           // 표시 이름
    Sprite icon,                  // 아이콘
    int durability,               // 현재 내구도
    int maxDurability,            // 최대 내구도
    float weight                  // 무게
)
{
    for (int i = 0; i < _slots.Count; i++)
    {
        if (_slots[i] == null)    // 빈칸 찾기
        {
            _slots[i] = new ApparelEntry
            {
                typeId        = typeId,
                itemId        = itemId,
                displayName   = displayName,
                icon          = icon,
                durability    = durability,
                maxDurability = maxDurability, // ✅ 여기 저장
                weight        = weight
            };
            OnChanged?.Invoke();  // UI 리프레시
            return true;
        }
    }
    return false;                 // 빈칸 없음
}

// == 착용(입기) 시 최대 내구도 저장 ==
public bool WearAt(int index, int typeId, int itemId, string displayName, Sprite icon,
                   int durability, int maxDurability, float weight)
{
    if (index < 0 || index >= _slots.Count) return false;

    _slots[index] = new ApparelEntry
    {
        typeId = typeId,
        itemId = itemId,
        displayName = displayName,
        icon = icon,
        durability = durability,
        maxDurability = maxDurability, // ★ 수정됨
        weight = weight
    };

    OnChanged?.Invoke();   // ★ DisplaySlot들이 이 이벤트를 구독함
    return true;
}


    public void TakeOffAt(int index) // 특정 슬롯 비우기(벗기)
    {
        if (index < 0 || index >= _slots.Count) return;
        _slots[index] = null;
        OnChanged?.Invoke();
    }

    public ApparelEntry GetAt(int index) // 슬롯 조회
    {
        if (index < 0 || index >= _slots.Count) return null;
        return _slots[index];
    }

// == 내구도 감소 시 슬롯 제거 + 이벤트 전파 ==
public bool TryApplyDurabilityByMeta(ApparelTypeRegistry.ApparelData meta, int amount)
{
    for (int i = 0; i < _slots.Count; i++)
    {
        var entry = _slots[i];
        if (entry == null) continue;

        if (entry.typeId == meta.typeId && entry.itemId == meta.itemId)
        {
            entry.durability = Mathf.Max(0, entry.durability - amount);

            if (entry.durability <= 0)
            {
                _slots[i] = null;

                if (wearStateRegistry != null)
                {
                    int tier, slot;
                    if (wearStateRegistry.TryGetRequiredSlot(meta.typeId, meta.itemId, out tier, out slot))
                        wearStateRegistry.RevertWear(tier, slot);
                }
            }

            OnChanged?.Invoke(); // ★ DisplaySlot 갱신 트리거
            return true;
        }
    }
    return false;
}




}
