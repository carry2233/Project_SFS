using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems; // 우클릭 이벤트
using System;

[AddComponentMenu("Inventory/UI/Equip Slot (장착 슬롯)")]
public class EquipSlot : MonoBehaviour, IPointerClickHandler // ★ 우클릭 처리를 위해 인터페이스 구현
{
    [Header("순서/식별")]
    public int orderIndex;                       // 슬롯 순서(표시용)

    [Header("UI")]
    public Image icon;                           // 아이콘 이미지
    public TextMeshProUGUI label;                // 이름 라벨(옵션)
    public GameObject highlight;                 // 활성 하이라이트(옵션)

    [Header("이벤트")]
    public Action<int> onRightClick;             // ★ 우클릭 델리게이트(0-base 인덱스 전달)

    [Header("내구도 UI")]
public Slider durabilitySlider;          // 내구도 슬라이더
public Image durabilityFill;             // 내구도 Fill 이미지

[Header("내구도 값 저장")]
public int currentDurability;            // 현재 내구도
public int maxDurability;                // 최대 내구도


public void Bind(Sprite sprite, string name, bool active) // ✅ 슬롯의 아이콘/이름/하이라이트 설정
{
    if (icon)
    {
        icon.sprite = sprite;
        icon.enabled = (sprite != null);
    }

    if (label)
        label.text = string.IsNullOrEmpty(name) ? string.Empty : name;

    if (highlight)
        highlight.SetActive(active);
}


public void OnPointerClick(PointerEventData eventData) // ✅ 슬롯 위에서 마우스 클릭 감지
{
    if (eventData != null && eventData.button == PointerEventData.InputButton.Right)
    {
        // orderIndex는 1-base이므로, 0-base 인덱스로 맞춰서 전달
        int zeroBasedIndex = Mathf.Max(0, orderIndex - 1); 
        onRightClick?.Invoke(zeroBasedIndex);                     // ✅ 정수 인덱스 전달
    }
}


public void SetDurability(int current, int max, Color fillColor) // ✅ 내구도 슬라이더 값/색 반영
{
    currentDurability = current;
    maxDurability = max;

    bool hasDur = (max > 0);

    if (durabilitySlider)
        durabilitySlider.gameObject.SetActive(hasDur);           // ✅ 내구도 있을 때만 보이기

    if (!hasDur) return;

    durabilitySlider.maxValue = max;
    durabilitySlider.value = Mathf.Clamp(current, 0, max);

    if (durabilityFill)
        durabilityFill.color = fillColor;
}


}
