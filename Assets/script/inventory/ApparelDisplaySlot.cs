using TMPro;                                 // TMP 사용
using UnityEngine;                           // 유니티 엔진
using UnityEngine.UI;                        // UI 이미지

[AddComponentMenu("Apparel/UI/Apparel Display Slot (착용 의류 표시 전용)")]
[DisallowMultipleComponent]
public class ApparelDisplaySlot : MonoBehaviour
{
    [Header("식별/표시")]
    public int orderIndex;                   // 표시용 순서(1-base)

    [Header("UI")]
    public Image icon;                       // 의류 아이콘 이미지
    public Image activeHighlight;            // 활성 하이라이트(테두리/글로우 등)
    public TextMeshProUGUI label;            // 의류 이름 라벨(옵션)
    public CanvasGroup canvasGroup;          // 표시/상호작용 제어(옵션)

    [Header("표시 옵션")]
    public bool showLabel = false;           // 이름 라벨 표시 여부
    public bool hideWhenEmpty = false;       // 빈 슬롯일 때 자체 오브젝트 숨김

    [Header("담당 착용부위")]
public int wearSlotTier;   // 이 디스플레이 슬롯이 담당할 착용부위단계
public int wearSlot;       // 이 디스플레이 슬롯이 담당할 착용부위

[Header("내구도 UI")]
public Slider durabilitySlider;             // 내구도 슬라이더 (옵션)
public Image durabilityFill;                // 내구도 Fill 이미지 (옵션)

[Header("내구도 값 저장")]
public int currentDurability;               // 현재 내구도
public int maxDurability;                   // 최대 내구도



    // ===== 메서드 =====

    public void Bind(Sprite sprite, string displayName, bool active) // 슬롯 전체 바인딩(아이콘/이름/활성)
    {
        SetItemIcon(sprite);                 // 아이콘 반영
        SetLabel(displayName);               // 이름 반영
        SetActiveVisual(active);             // 활성 하이라이트 반영
        ApplyEmptyVisibility(sprite);        // 빈 슬롯 표시 정책 반영
    }

    public void SetItemIcon(Sprite sprite)   // 의류 아이콘 설정
    {
        if (!icon) return;                   // 아이콘 참조 없으면 무시
        icon.sprite = sprite;                // 스프라이트 지정
        icon.enabled = (sprite != null);     // 스프라이트 유무에 따른 표시
        icon.raycastTarget = false;          // 표시 전용(클릭 불필요)
    }

    public void SetLabel(string displayName) // 이름 라벨 설정(옵션)
    {
        if (!label) return;                  // 라벨 참조 없으면 무시
        label.text = string.IsNullOrEmpty(displayName) ? "" : displayName; // 이름 지정
        label.gameObject.SetActive(showLabel && !string.IsNullOrEmpty(label.text)); // 옵션에 따른 표시
    }

    public void SetActiveVisual(bool on)     // 활성 하이라이트 토글
    {
        if (activeHighlight)                 // 하이라이트 이미지 존재 시
        {
            activeHighlight.enabled = on;    // 이미지 On/Off
            if (activeHighlight.gameObject != gameObject)
                activeHighlight.gameObject.SetActive(on); // 별도 오브젝트면 활성 토글
        }
    }

    public void Clear()                      // 슬롯 비우기(초기화)
    {
        if (icon) { icon.sprite = null; icon.enabled = false; } // 아이콘 제거
        if (label) { label.text = ""; label.gameObject.SetActive(false); } // 라벨 숨김
        SetActiveVisual(false);              // 하이라이트 끄기
        ApplyEmptyVisibility(null);          // 빈 슬롯 표시 정책 반영
    }

    public void SetInteractable(bool on)     // 표시 전용이지만 필요 시 인터랙션 제어
    {
        if (!canvasGroup) return;            // CanvasGroup 없으면 무시
        canvasGroup.interactable = on;       // 상호작용 가능 여부
        canvasGroup.blocksRaycasts = on;     // 레이캐스트 차단 여부
        canvasGroup.alpha = on ? 1f : 0.9f;  // 약한 시각 피드백(옵션)
    }

    private void ApplyEmptyVisibility(Sprite sprite) // 빈 슬롯 시 표시 정책 적용
    {
        if (!hideWhenEmpty) return;          // 빈 슬롯 숨김 옵션이 아니면 무시
        bool hasItem = (sprite != null);     // 아이템 존재 여부
        gameObject.SetActive(hasItem);       // 아이템 없으면 슬롯 자체 숨김
    }

// == 부위 일치 시만 UI 반영 ==
public void BindIfMatch(int itemTier, int itemSlot, Sprite sprite, string displayName, bool active)
{
    if (itemTier == wearSlotTier && itemSlot == wearSlot)
        Bind(sprite, displayName, active); // 기존 UI 바인딩
    else
        Clear(); // 다른 부위면 표시 제거
}

// == 내구도 UI 반영 ==
public void SetDurability(int current, int max, Color fillColor)
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
