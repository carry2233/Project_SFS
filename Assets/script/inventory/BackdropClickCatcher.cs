using UnityEngine;
using UnityEngine.EventSystems;

[AddComponentMenu("Inventory/UI/Backdrop Click Catcher (패널 바깥 클릭 닫기)")]
public class BackdropClickCatcher : MonoBehaviour, IPointerClickHandler
{
    public EquipActionPanel panel; // 패널 참조(클릭 시 Hide() 호출)

        public void OnPointerClick(PointerEventData eventData) // 어느 버튼이든 클릭 시
    {
        if (panel != null) panel.Hide(); // 패널 닫기
    }
}