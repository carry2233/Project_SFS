using UnityEngine;

[DisallowMultipleComponent]                                          // 중복 부착 방지
[AddComponentMenu("Item Runtime/Apparel Item Runtime")]              // 메뉴 경로
public class ApparelItemRuntime : MonoBehaviour
{
    [Header("공통/의류 데이터(인라인)")]
    public ItemCommonData common;                                    // 공통 메타 데이터
    public ApparelDefinition apparel;                                 // 의류 방어 스탯

    [Header("무게(인라인)")]
    public ItemWeightData weightData;                                 // 아이템 무게정보

    // 👇 얇은 공개 프로퍼티(호버/일반화 처리용)
    public string fallbackDisplayName => common.fallbackDisplayName;  // 표시 이름 폴백
    public Sprite fallbackIcon => common.fallbackIcon;                // 아이콘 폴백

    [Header("런타임 상태(개체 고유)")]
    public string objectId;                                           // 고유 ID(string)
    public int durability;                                            // 내구도(정수)
}
