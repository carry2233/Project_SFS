/*using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;



public class Slot_Apparel : MonoBehaviour, IPointerClickHandler, IInventorySlotRightClickable
{
    [SerializeField] private Image iconImage;  // 아이콘
    [SerializeField] private Text nameText;    // 이름

        private Inventory sourceInventory;                 // 원본 인벤토리 참조
private ItemType itemType = ItemType.Apparel;      // 타입 고정(의류)
private int sourceIndex = -1;                      // 원본 인덱스
private string objectId = null;                    // objectId
private InventoryUI ownerUI;                       // 호출자(UI)

    public void Bind(ItemViewData v)           // 바인딩
    {
        if (iconImage) iconImage.sprite = v.icon;
        if (nameText)  nameText.text  = string.IsNullOrEmpty(v.name) ? "Apparel" : v.name;
    }

    public void SetupRightClickContext(Inventory inv, ItemType type, int index, string objId, InventoryUI ui) // 우클릭 컨텍스트 세팅
    {
        sourceInventory = inv;
        itemType = type;
        sourceIndex = index;
        objectId = objId;
        ownerUI = ui;
    }

    public void OnPointerClick(PointerEventData eventData) // 포인터 클릭 처리
    {
        if (eventData.button != PointerEventData.InputButton.Right) return;
        if (ownerUI == null) return;

        ownerUI.ShowEquipActionForInventorySlot(itemType, sourceIndex, objectId, eventData.position);
    }
}*/