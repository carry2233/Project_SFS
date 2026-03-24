using System;                             // Action 델리게이트용
using TMPro;                              // TMP 텍스트용
using UnityEngine;                        // 유니티 기본 네임스페이스
using UnityEngine.UI;                     // Image, Button 등 UI용

[AddComponentMenu("Crafting/Craft Item Slot UI")]
public class CraftItemSlotUI : MonoBehaviour // 제작 아이템(결과 아이템) 슬롯 하나를 표시하는 UI 컴포넌트
{
    [Header("UI 참조")]
    public Image iconImage;                       // 제작 아이템 아이콘 이미지
    public TextMeshProUGUI nameText;             // 제작 아이템 이름/라벨 텍스트
    public Button button;                        // 슬롯을 클릭하기 위한 버튼 컴포넌트
    public GameObject selectedHighlight;         // 선택 상태를 표시할 하이라이트 오브젝트(옵션)

    private CraftingRecipeEntry _recipe;         // 이 슬롯이 표현하는 레시피 데이터
    private Action<CraftingRecipeEntry, CraftItemSlotUI> _onClick; // 클릭 시 호출할 콜백(레시피+슬롯 전달용)

    public void SetData( // 슬롯 초기화: 레시피와 클릭 콜백을 주입
        CraftingRecipeEntry recipe,
        Action<CraftingRecipeEntry, CraftItemSlotUI> onClick)
    {
        _recipe = recipe;                        // 레시피 데이터 저장
        _onClick = onClick;                      // 클릭 콜백 저장

        RefreshUI();                             // 아이콘/텍스트 갱신

        if (button != null)
        {
            button.onClick.RemoveAllListeners(); // 기존 리스너 제거
            button.onClick.AddListener(OnClick); // 버튼 클릭 시 OnClick 호출
        }
    }

    private void RefreshUI() // 레시피 데이터 기반으로 슬롯 UI를 갱신
    {
        if (_recipe == null)
        {
            if (iconImage != null)
            {
                iconImage.sprite = null;         // 아이콘 초기화
                iconImage.enabled = false;       // 이미지 숨김
            }
            if (nameText != null)
            {
                nameText.text = string.Empty;    // 텍스트 비우기
            }
            return;
        }

        // 결과 프리팹의 FieldItem에서 아이콘/이름 가져오기 (표시용)
        Sprite icon = null;
        string displayName = string.Empty;

        if (_recipe.resultPrefab != null)
        {
            var fieldItem = _recipe.resultPrefab.GetComponent<FieldItem>(); // FieldItem 가져오기
            if (fieldItem != null)
            {
                icon = fieldItem.icon;           // 아이콘
                displayName = fieldItem.displayName; // 이름
            }
        }

        if (iconImage != null)
        {
            iconImage.sprite = icon;            // 아이콘 설정
            iconImage.enabled = (icon != null); // 아이콘 유무에 따라 표시/숨김
        }

        if (nameText != null)
        {
            nameText.text = string.IsNullOrWhiteSpace(displayName)
                ? "Unknown"                     // 이름이 비어 있으면 기본값 사용
                : displayName;                  // 이름 표시
        }
    }

    public void SetSelected(bool selected) // 이 슬롯이 선택/비선택 상태인지 표시
    {
        if (selectedHighlight != null)
        {
            selectedHighlight.SetActive(selected); // 선택 시 하이라이트 활성, 아니면 비활성
        }
    }

    private void OnClick() // 버튼 클릭 시 호출되는 내부 메서드
    {
        if (_recipe == null) return;            // 레시피가 없으면 무시
        _onClick?.Invoke(_recipe, this);        // 콜백이 있으면 레시피+슬롯을 전달하여 호출
    }
}
