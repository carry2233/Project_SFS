using UnityEngine;

[DisallowMultipleComponent]                                   // 중복 부착 방지
[AddComponentMenu("Item Runtime/Resource Item Runtime")]      // 메뉴 경로
public class ResourceItemRuntime : MonoBehaviour
{
    [Header("공통 데이터(인라인)")]
    public ItemCommonData common;                             // 공통 메타 데이터

    [Header("무게(인라인)")]
    public ItemWeightData weightData;                         // 아이템 무게정보

    // 👇 얇은 공개 프로퍼티
    public string fallbackDisplayName => common.fallbackDisplayName; // 표시 이름 폴백
    public Sprite fallbackIcon => common.fallbackIcon;               // 아이콘 폴백
}
