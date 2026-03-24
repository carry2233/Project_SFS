using UnityEngine;

[AddComponentMenu("Item Runtime/Field Item Hover UI (간소 신호)")]
public class FieldItemHoverUI : MonoBehaviour
{
    // 간소화: 이 스크립트는 과거 구조와 달리 별도의 브로드캐스트 없이,
    // DisplayManager가 직접 Raycast로 찾도록 했습니다.
    // 필요 시 OnMouseEnter/Exit 또는 트리거 기반으로 확장 가능.
}
