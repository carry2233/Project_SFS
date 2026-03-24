using System;
using System.Collections.Generic;
using UnityEngine;

[AddComponentMenu("Inventory/Equip Inventory (장착 인벤토리)")]
[DisallowMultipleComponent]
public class EquipInventory : MonoBehaviour
{
    [Serializable]
    public class EquippedEntry
    {
        public int typeId;                 // 종류 id
        public int itemId;                 // 아이템 id
        public string displayName;         // 표시 이름
        public Sprite icon;                // 아이콘
        public int durability;             // 내구도
        public int maxDurability;      // 최대 내구도
        public float weight;               // 무게
    }

    [Header("설정")]
    [Min(1)] public int capacity = 4;     // 최대 저장 수(슬롯 수)



    public event Action OnChanged;        // 변경 이벤트
    
    [System.NonSerialized] private List<EquippedEntry> _slots = new(); // 런타임 전용 슬롯 리스트(인스펙터 숨김)
    public IReadOnlyList<EquippedEntry> Slots => _slots;              // 읽기 전용 뷰(외부는 조회만)


private void Awake()                  // 초기 용량 보정(런타임에서만 슬롯 준비)
{
    EnsureCapacity();                 // capacity 만큼 null 슬롯 확보 (인스펙터에서 못 만듦)
}


public void EnsureCapacity()          // 용량만큼 슬롯 확보/정렬(런타임 전용 리스트 사용)
{
    if (_slots == null) _slots = new List<EquippedEntry>(capacity); // 내부 리스트 보장
    // 부족분 null 채우기
    while (_slots.Count < capacity) _slots.Add(null);
    // 초과분 제거
    while (_slots.Count > capacity) _slots.RemoveAt(_slots.Count - 1);
    OnChanged?.Invoke();
}


// 새 시그니처 - 최대 내구도까지 함께 저장
public bool EquipFirstEmpty(      // 첫 빈칸 장착(최대 내구도 포함)
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
        if (_slots[i] == null)    // 빈칸이면 새 엔트리 생성
        {
            _slots[i] = new EquippedEntry
            {
                typeId        = typeId,
                itemId        = itemId,
                displayName   = displayName,
                icon          = icon,
                durability    = durability,
                maxDurability = maxDurability, // ✅ 여기에 저장
                weight        = weight
            };
            OnChanged?.Invoke();  // UI 리프레시
            return true;
        }
    }
    return false;                 // 빈칸이 없음
}


// 새 시그니처 - 최대 내구도까지 함께 저장
public bool EquipAt(              // 특정 칸 장착(최대 내구도 포함)
    int index,                    // 장착 슬롯 인덱스
    int typeId,                   // 아이템 종류 id
    int itemId,                   // 아이템 id
    string displayName,           // 표시 이름
    Sprite icon,                  // 아이콘
    int durability,               // 현재 내구도
    int maxDurability,            // 최대 내구도
    float weight                  // 무게
)
{
    if (index < 0 || index >= _slots.Count) return false;

    _slots[index] = new EquippedEntry
    {
        typeId        = typeId,
        itemId        = itemId,
        displayName   = displayName,
        icon          = icon,
        durability    = durability,
        maxDurability = maxDurability, // ✅ 여기서도 설정
        weight        = weight
    };

    OnChanged?.Invoke();          // UI 리프레시
    return true;
}


public void UnequipAt(int index)      // 특정 칸 해제(null로 비움)
{
    if (index < 0 || index >= _slots.Count) return;
    _slots[index] = null;
    OnChanged?.Invoke();
}


public EquippedEntry GetAt(int index) // 슬롯 조회(읽기 전용)
{
    if (index < 0 || index >= _slots.Count) return null;
    return _slots[index];
}

// ─────────────────────────────────────────────────────────────
// ✅ 신규 메서드 추가: 외부에서 UI 리프레시를 요청할 수 있게 함
// ─────────────────────────────────────────────────────────────
public void NotifyChanged() // 장착 인벤토리 변경 알림 이벤트 호출
{
    OnChanged?.Invoke();
}


}
