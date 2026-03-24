using UnityEngine;

[DisallowMultipleComponent]                                  // 중복 부착 방지
[AddComponentMenu("Item Runtime/Tool Item Runtime")]         // 메뉴 경로
public class ToolItemRuntime : MonoBehaviour
{
    [Header("공통/장비 데이터(인라인)")]
    public ItemCommonData common;                            // 공통 메타 데이터
    public EquipmentStats equip;                             // 전투 수치(선택)

    [Header("무게(인라인)")]
    public ItemWeightData weightData;                        // 아이템 무게정보

    // 👇 얇은 공개 프로퍼티
    public string fallbackDisplayName => common.fallbackDisplayName; // 표시 이름 폴백
    public Sprite fallbackIcon => common.fallbackIcon;               // 아이콘 폴백

    [Header("런타임 상태(개체 고유)")]
    public string objectId;                                  // 고유 ID(기존 유지)
    [Tooltip("내구도")]
    public int durability;                                   // 내구도
    [Tooltip("자원 드랍률")]
    public float resourceDropRate;                           // 자원 드랍률
}
