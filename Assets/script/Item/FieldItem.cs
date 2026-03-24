using UnityEngine;

[AddComponentMenu("Item Runtime/Field Item (키/수량/내구/무게/이름/아이콘)")]
[DisallowMultipleComponent]
public class FieldItem : MonoBehaviour
{
    [Header("식별 키")]
    public int typeId;                   // 종류 id
    public int itemId;                   // 아이템 id

    [Header("표시 데이터")]
    public string displayName;           // 표시 이름
    public Sprite icon;                  // 아이콘

    [Header("수량/스택")]
    public int count = 1;                // 수량
    public string stackId;               // 스택 고유 id

    [Header("상태/속성")]
    public int durability = 100;         // 내구도(정수)
    public int maxDurability;          // 최대 내구도

    public float weight = 1f;            // 무게(실수)

    private void Reset()                 // 기본값 보정
    {
        if (count < 1) count = 1;
        if (string.IsNullOrEmpty(stackId))
            stackId = System.Guid.NewGuid().ToString();
    }
}
