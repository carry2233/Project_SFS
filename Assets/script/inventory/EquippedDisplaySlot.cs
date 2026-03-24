using UnityEngine;
using UnityEngine.UI;

[AddComponentMenu("Inventory/UI/Equipped Display Slot (활성 장비 표시)")]
public class EquippedDisplaySlot : MonoBehaviour
{
    [Header("순서/식별")]
    public int orderIndex;                       // 슬롯 순서

    [Header("UI")]
    public Image activeImage;                    // 활성 표시 이미지 (하이라이트 유지)
    public Image itemIcon;                       // 장착 아이템 아이콘 이미지 (신규)

    [Header("내구도 UI")]
public Slider durabilitySlider;            // 내구도 슬라이더 (옵션)
public Image durabilityFill;               // 내구도 Fill 이미지 (옵션)

[Header("내구도 값 저장")]
public int currentDurability;              // 현재 내구도
public int maxDurability;                  // 최대 내구도


    public void SetItemIcon(Sprite sprite)       // 장착 아이템 아이콘 설정(알파 변화 없음)
    {
        if (!itemIcon) return;
        itemIcon.sprite = sprite;
        itemIcon.enabled = (sprite != null);     // 스프라이트 유/무만으로 표시/숨김
        // ※ 요구사항: 숫자키 토글에서 알파/색 변경 없음. 오직 표시 On/Off만 처리.
    }

    public void SetActiveVisual(bool on)         // 활성/비활성 하이라이트 (기존 activeImage만 제어)
    {
        if (!activeImage) return;
        activeImage.enabled = on;
        if (activeImage.gameObject != this.gameObject)
            activeImage.gameObject.SetActive(on);
        // ※ 요구사항: 아이콘 알파/색을 여기서 건드리지 않음. activeImage만 토글.
    }

public void SetDurability(int current, int max, Color fillColor) // ✅ 내구도 슬라이더 값/색 반영
{
    currentDurability = current;
    maxDurability = max;

    bool hasDur = (max > 0);

    if (durabilitySlider)
        durabilitySlider.gameObject.SetActive(hasDur);           // ✅ 무기인 경우에만 보이게

    if (!hasDur) return;

    durabilitySlider.maxValue = max;
    durabilitySlider.value = Mathf.Clamp(current, 0, max);

    if (durabilityFill)
        durabilityFill.color = fillColor;
}


}