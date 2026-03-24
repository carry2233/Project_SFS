using UnityEngine;

[AddComponentMenu("Combat/Equipment Activation Controller (숫자키 활성 전환)")]
public class EquipmentActivationController : MonoBehaviour
{
    [Header("참조")]
    public EquipInventory equipInventory;            // 장착 인벤토리
    public EquipInventoryUI equipInventoryUI;        // 장착 인벤토리 UI(딜레이 토글 담당)

    [Header("입력")]
    public KeyCode baseKey = KeyCode.Alpha1;         // 시작 키(Alpha1 → 1번 슬롯)

    private void Update()                            // 숫자키 입력 체크
    {
        if (!equipInventory || !equipInventoryUI) return;

        int cap = equipInventory.capacity;
        for (int i = 0; i < cap; i++)
        {
            var key = baseKey + i;                   // Alpha1 + i → Alpha2,3...
            if (Input.GetKeyDown((KeyCode)key))
            {
                equipInventoryUI.RequestActivateIndex(i); // i번째 활성 요청
                break;
            }
        }
    }
}
