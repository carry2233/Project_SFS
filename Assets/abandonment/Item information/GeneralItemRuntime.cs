using UnityEngine;

[DisallowMultipleComponent]                                     // 중복 부착 방지
[AddComponentMenu("Item Runtime/General Item Runtime")]         // 메뉴 경로
public class GeneralItemRuntime : MonoBehaviour
{
    [Header("공통 데이터(인라인)")]
    public ItemCommonData common;                               // 공통 메타(아이콘/이름/ID 등)

    [Header("갯수(인라인)")]
    public ItemCountData countData;                             // 스택/갯수 정보

    [Header("무게(인라인)")]
    public ItemWeightData weightData;                           // ✅ (추가) 일반 아이템 무게 데이터

    // 👇 FieldItemHoverUI 리플렉션용 '얇은' 공개 프로퍼티
    public string fallbackDisplayName => common.fallbackDisplayName; // 표시 이름 폴백
    public Sprite fallbackIcon => common.fallbackIcon;               // 아이콘 폴백
}
