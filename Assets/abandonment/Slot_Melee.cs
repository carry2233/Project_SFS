/*using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;



public class Slot_Melee : MonoBehaviour, IPointerClickHandler, IInventorySlotRightClickable
{
    [SerializeField] private Image iconImage;   // 아이콘
    [SerializeField] private Text nameText;     // 이름

        // ▼ 우클릭 처리를 위해 InventoryUI와 원본 참조를 보관
private Inventory sourceInventory;                 // 원본 인벤토리 참조
private ItemType itemType = ItemType.Melee;        // 이 슬롯의 타입 고정(근접)
private int sourceIndex = -1;                      // 원본 리스트 인덱스
private string objectId = null;                    // objectId (장착 이동에 사용할 키)
private InventoryUI ownerUI;                       // 호출자(UI) — 패널 호출용

    public void Bind(ItemViewData v)            // 바인딩
    {
        if (iconImage) iconImage.sprite = v.icon;
        if (nameText)  nameText.text  = string.IsNullOrEmpty(v.name) ? "Melee" : v.name;
    }

    // 📌 InventoryUI에서 세팅해 주는 참조 정보
    public void SetupRightClickContext(Inventory inv, ItemType type, int index, string objId, InventoryUI ui) // 우클릭 컨텍스트 세팅
    {
        sourceInventory = inv;
        itemType = type;
        sourceIndex = index;
        objectId = objId;
        ownerUI = ui;
    }

    // 📌 우클릭 감지 → EquipActionPanel 호출 훅으로 전달
    public void OnPointerClick(PointerEventData eventData) // 포인터 클릭 처리
    {
        if (eventData.button != PointerEventData.InputButton.Right) return; // 우클릭만
        if (ownerUI == null) return;

        ownerUI.ShowEquipActionForInventorySlot(itemType, sourceIndex, objectId, eventData.position);
    }
}*/