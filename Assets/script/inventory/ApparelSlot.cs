using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[AddComponentMenu("Apparel/UI/Apparel Slot (의류 슬롯 뷰)")]
public class ApparelSlot : MonoBehaviour, IPointerClickHandler
{
    [Header("식별/표시")]
    public int orderIndex;                 // 슬롯 순서(1-base 표기용)

    [Header("UI")]
    public Image icon;                     // 아이콘 이미지
    public TextMeshProUGUI label;          // 이름 라벨(옵션)
    public GameObject highlight;           // 활성 하이라이트(옵션)

    [Header("이벤트")]
    public Action<ApparelSlot> onRightClick;   // 의류 슬롯 자신을 전달하는 델리게이트

    [Header("담당 착용부위")]
public int wearSlotTier;   // 이 슬롯이 담당하는 착용부위단계(정수)
public int wearSlot;       // 이 슬롯이 담당하는 착용부위(정수)

[Header("내구도 UI")]
public Slider durabilitySlider;           // 내구도 슬라이더
public Image durabilityFill;              // 슬라이더 Fill 이미지

[Header("내구도 값 저장")]
public int currentDurability;             // 현재 내구도
public int maxDurability;                 // 최대 내구도



    public void Bind(Sprite sprite, string name, bool active) // 슬롯 표시 바인딩
    {
        if (icon) { icon.sprite = sprite; icon.enabled = (sprite != null); }
        if (label) label.text = string.IsNullOrEmpty(name) ? "" : name;
        if (highlight) highlight.SetActive(active);
    }

    public void OnPointerClick(PointerEventData eventData) // 우클릭 이벤트 감지
    {
        if (eventData != null && eventData.button == PointerEventData.InputButton.Right)
        {
            onRightClick?.Invoke(this);        // 이 슬롯 자신(ApparelSlot)을 전달
        }
    }

public void SetDurability(int current, int max, Color fillColor) // 내구도 슬라이더 값/색 반영 메서드
{
    currentDurability = current;
    maxDurability = max;

    bool hasDur = max > 0;

    if (durabilitySlider)
        durabilitySlider.gameObject.SetActive(hasDur);

    if (!hasDur) return;

    durabilitySlider.maxValue = max;
    durabilitySlider.value = current;

    if (durabilityFill)
        durabilityFill.color = fillColor;
}

}
