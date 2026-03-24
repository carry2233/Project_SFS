using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems; // 우클릭 이벤트용
using System;

[AddComponentMenu("Inventory/UI/Item Slot (풀링 친화 뷰)")]
public class ItemSlot : MonoBehaviour, IPointerClickHandler // ★ 우클릭 이벤트 수신을 위해 인터페이스 구현
{
    [Header("UI 참조")]
    public Image icon;                               // 아이콘 이미지
    public TextMeshProUGUI countText;                // 수량 표기(TMP)
    public Button button;                            // (옵션) 좌클릭용

    [Header("우클릭 패널/데이터")]
    public EquipActionPanel actionPanel;             // 우클릭 시 호출할 패널 참조(InventoryUI가 주입)

    [Header("상태")]
    public int slotIndex;                            // 이 슬롯이 UI상 몇 번째인지 (InventoryUI가 할당)
    public Action<int> onRightClick;                 // 우클릭 델리게이트(InventoryUI가 구독)

    [Header("내구도 UI")]
public Slider durabilitySlider;            // 내구도 슬라이더
public Image durabilityFill;               // 슬라이더 Fill 이미지 (색상 변경용)

[Header("내구도 값 저장")]
public int currentDurability;              // 현재 내구도
public int maxDurability;                  // 최대 내구도



    // ★ 슬롯이 표시 중인 아이템 데이터(우클릭 시 전달 용도)
    public int typeId;                               // 종류 id
    public int itemId;                               // 아이템 id
    public string displayName;                       // 표시 이름
    public Sprite iconSprite;                        // 아이콘 스프라이트

    private void Awake()  { Clear(); }               // 초기화: 안전 클리어
    private void OnEnable(){ /* 풀링 복귀 시 상태 보존 가능 */ }

    public void Clear() // 슬롯 초기화(아이콘/텍스트 Off)
    {
        if (icon) { icon.sprite = null; icon.enabled = false; }
        if (countText) { countText.text = string.Empty; countText.gameObject.SetActive(false); }
    }

    public void Show(Sprite sprite, int count, bool showCount) // 슬롯 표시(아이콘/수량)
    {
        if (icon)
        {
            icon.sprite = sprite;                    // 아이콘 스프라이트 지정
            icon.enabled = (sprite != null);         // 아이콘 표시 On/Off
            // ★ 클릭을 받으려면 Raycast Target이 켜져 있어야 함
            icon.raycastTarget = true;
        }

        if (countText)
        {
            if (showCount && count > 1)              // 스택 가능 & 2개 이상일 때만 수량 출력
            {
                countText.text = count.ToString();   // 수량 텍스트
                countText.gameObject.SetActive(true);
            }
            else
            {
                countText.text = string.Empty;
                countText.gameObject.SetActive(false);
            }
            // 텍스트도 우클릭을 가로채지 않도록 할 땐 필요 시 raycastTarget = false 옵션 검토
        }

        gameObject.SetActive(true);                  // 오브젝트 활성
    }

    public void Hide() // 슬롯 숨김(풀로 반환 전 호출 추천)
    {
        Clear();                                     // 내용 초기화
        gameObject.SetActive(false);                 // 오브젝트 비활성
    }

    public void SetInteractable(bool interactable) // 상호작용 가능 여부(OnClick 등) 제어
    {
        if (button) button.interactable = interactable;
    }

    public void SetActionPanel(EquipActionPanel panel) // 우클릭 패널 참조 주입
    {
        actionPanel = panel;
    }

    public void SetData(int typeId, int itemId, string displayName, Sprite sprite) // 슬롯 데이터 주입
    {
        this.typeId = typeId;              // 종류 id 저장
        this.itemId = itemId;              // 아이템 id 저장
        this.displayName = displayName;    // 표시 이름 저장
        this.iconSprite = sprite;          // 아이콘 저장
    }

public void OnPointerClick(PointerEventData eventData) // ★ 우클릭 이벤트 수신(인터페이스 구현 필수)
{
    if (eventData == null) return;

    // 우클릭만 처리
    if (eventData.button == PointerEventData.InputButton.Right)
    {
        Debug.Log($"[ItemSlot] OnPointerClick RightClick - slotIndex={slotIndex}, name={displayName}"); // ✅ 디버그

        // InventoryUI로 우클릭 알림 (델리게이트 방식)
        if (onRightClick != null)
        {
            onRightClick.Invoke(slotIndex);
        }
        else
        {
            // 안전 로그: 델리게이트 미주입 시 디버깅 도움
            Debug.LogWarning($"[ItemSlot] onRightClick 미할당 (slotIndex={slotIndex})");
        }
    }
}


public void SetDurability(int current, int max, Color fillColor) // ✅ 내구도 슬라이더 값/색 반영
{
    currentDurability = current;
    maxDurability = max;

    bool hasDur = (max > 0);

    if (durabilitySlider)
        durabilitySlider.gameObject.SetActive(hasDur);            // ✅ 내구도 있으면 슬라이더 보이기

    if (!hasDur) return;                                          // ✅ 없으면 더 이상 처리 안 함

    durabilitySlider.maxValue = max;
    durabilitySlider.value = Mathf.Clamp(current, 0, max);        // ✅ 범위 클램프

    if (durabilityFill)
        durabilityFill.color = fillColor;                         // ✅ 전달받은 색 적용
}




}
