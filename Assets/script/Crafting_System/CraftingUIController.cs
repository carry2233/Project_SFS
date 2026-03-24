using System;                                   // Action 등 델리게이트용
using System.Collections.Generic;               // List<T> 사용
using System.Linq;                              // OrderBy 사용
using TMPro;                                    // TMP 텍스트용
using UnityEngine;                              // 유니티 기본 네임스페이스
using UnityEngine.UI;                           // Image, Slider, Button 등 UI용

[AddComponentMenu("Crafting/Crafting UI Controller")]
public class CraftingUIController : MonoBehaviour // 제작 UI 전체를 제어하는 컴포넌트
{
    [Header("필수 참조")]
    public Inventory inventory;                         // 플레이어 인벤토리 참조

    [Header("루트/패널")]
    public GameObject panelRoot;                        // 제작 UI 전체를 켜고 끄는 패널 루트

    [Header("제작 아이템 슬롯 영역")]
    public Transform craftItemParent;                   // 제작 아이템 슬롯들이 들어갈 부모(Grid Layout Group 적용)
    public GameObject craftItemSlotPrefab;              // 제작 아이템 슬롯 프리팹(CraftItemSlotUI 포함)

     [Header("제작 아이템 자동 높이 조절")]
    public bool autoResizeCraftParentHeight = true;     // 제작 아이템 Content 높이를 자동 조절할지 여부
    private RectTransform _craftParentRect;             // 제작 아이템 Content의 RectTransform 캐시
    private GridLayoutGroup _craftParentGrid;           // 제작 아이템 Content의 GridLayoutGroup 캐시

    [Header("필요 아이템 슬롯 영역")]
    public Transform requiredItemParent;                // 필요 아이템 슬롯들이 들어갈 부모(Grid Layout Group 적용)
    public GameObject requiredItemSlotPrefab;           // 필요 아이템 슬롯 프리팹(RequiredItemSlotUI 포함)

     [Header("필요 아이템 자동 높이 조절")]
    public bool autoResizeRequiredParentHeight = true;  // 필요 아이템 Content 높이를 자동 조절할지 여부
    private RectTransform _requiredParentRect;          // 필요 아이템 Content의 RectTransform 캐시
    private GridLayoutGroup _requiredParentGrid;        // 필요 아이템 Content의 GridLayoutGroup 캐시

    

    [Header("메인 표시 영역")]
    public Image resultIconImage;                       // 선택된 제작 결과 아이콘 이미지
    public Transform descriptionRoot;                   // 선택된 제작 아이템 설명 TMP 프리팹이 들어갈 부모

    [Header("제작 진행/버튼")]
    public Slider progressSlider;                       // 제작 진행 상황을 표시하는 슬라이더
    public GameObject progressRoot;                     // 슬라이더를 감싸는 오브젝트(평소에는 비활성)
    public Button craftButton;                          // 제작 시작 버튼
    public Button cancelButton;                         // 제작 취소 버튼(제작 중에만 사용)

    private CraftingStationController _currentStation;  // 현재 UI를 연 제작대 참조
    private CraftingRecipeList _currentRecipeList;      // 현재 사용 중인 레시피 리스트
    private List<CraftingRecipeEntry> _currentRecipes;  // 정렬된 현재 레시피 목록
    private CraftingRecipeEntry _currentRecipe;         // 현재 선택된 레시피(없으면 null)

    private readonly List<CraftItemSlotUI> _craftSlots = new();       // 생성된 제작 아이템 슬롯 리스트
    private readonly List<RequiredItemSlotUI> _requiredSlots = new(); // 생성된 필요 아이템 슬롯 리스트

    private bool _isOpen;                               // UI가 열려 있는지 여부
    private bool _isCrafting;                           // 제작이 진행 중인지 여부
    private float _craftTimer;                          // 현재 제작 경과 시간
    private float _craftDuration;                       // 현재 제작 총 소요 시간

    private void Awake() // 초기 설정(패널 비활성 등)
    {
        if (panelRoot != null)
        {
            panelRoot.SetActive(false);                 // 시작 시 UI는 닫힌 상태로 둠
        }

        if (progressRoot != null)
        {
            progressRoot.SetActive(false);              // 제작 진행 슬라이더 영역도 초기에는 비활성
        }

        if (progressSlider != null)
        {
            progressSlider.value = 0f;                  // 슬라이더 값 초기화
        }

        if (craftButton != null)
        {
            craftButton.onClick.AddListener(OnClickCraftButton);   // 제작 버튼 클릭 이벤트 연결
        }

        if (cancelButton != null)
        {
            cancelButton.onClick.AddListener(OnClickCancelButton); // 취소 버튼 클릭 이벤트 연결
            cancelButton.interactable = false;                     // 초기에는 비활성
        }

        // ★ 여기부터 추가
        if (craftItemParent != null)
        {
            _craftParentRect = craftItemParent.GetComponent<RectTransform>();   // 제작 Content RectTransform 캐시
            _craftParentGrid = craftItemParent.GetComponent<GridLayoutGroup>(); // 제작 Content GridLayoutGroup 캐시
        }

        if (requiredItemParent != null)
        {
            _requiredParentRect = requiredItemParent.GetComponent<RectTransform>();   // 필요 Content RectTransform 캐시
            _requiredParentGrid = requiredItemParent.GetComponent<GridLayoutGroup>(); // 필요 Content GridLayoutGroup 캐시
        }
    }


    private void Update() // 제작 진행(슬라이더) 업데이트
    {
        if (!_isCrafting) return;                       // 제작 중이 아니면 무시

        _craftTimer += Time.deltaTime;                  // 경과 시간 누적
        float t = Mathf.Clamp01(_craftTimer / Mathf.Max(_craftDuration, 0.0001f)); // 진행 비율 계산

        if (progressSlider != null)
        {
            progressSlider.value = t;                   // 슬라이더에 반영
        }

        if (_craftTimer >= _craftDuration)             // 제작 시간이 모두 지났다면
        {
            CompleteCraft();                           // 제작 완료 처리
        }
    }

public void Open(CraftingStationController station, CraftingRecipeList recipeList) // 제작 UI 열기 메서드
{
    if (station == null) return;                   // 제작대가 없으면 무시
    if (recipeList == null) return;                // 레시피 리스트가 없으면 무시
    if (inventory == null)                         // 인벤토리 참조가 없으면 경고만 출력
    {
        Debug.LogWarning("[CraftingUI] Inventory 참조가 없습니다.");
    }

    _currentStation = station;                     // 현재 제작대 저장
    _currentRecipeList = recipeList;               // 현재 레시피 리스트 저장

    BuildRecipeList();                             // 제작 아이템 슬롯 리스트 생성
    ClearCurrentRecipeDisplay();                   // 현재 선택 레시피 표시 초기화(아이콘/설명/필요 재료)

    if (panelRoot != null)
    {
        panelRoot.SetActive(true);                 // UI 패널 활성화
    }

    if (descriptionRoot != null)
    {
        descriptionRoot.gameObject.SetActive(true); // 패널이 켜질 때 설명 영역 부모는 항상 활성화
    }

    _isOpen = true;                                // 열림 상태 true

    SubscribeInventoryEvents();                    // 인벤토리 변경 이벤트 구독(있을 경우)
}

private void ResizeParentHeight(               // Content 높이를 자동 조절하는 공용 메서드
    RectTransform parentRect,                  // 높이를 바꿀 RectTransform
    GridLayoutGroup grid,                      // Y축 셀 크기/간격 정보를 가져올 GridLayoutGroup
    int visibleCount,                          // 실제로 표시되는 슬롯(버튼) 개수
    bool apply)                                // 이 기능을 적용할지 여부
{
    if (!apply) return;                        
    if (parentRect == null) return;            
    if (grid == null) return;                  
    if (visibleCount <= 0) return;             

    float cellH = grid.cellSize.y;             
    float spacingH = grid.spacing.y;           

    // ★ 수정된 부분 — 원하는 수식대로 적용
    // newHeight = (visibleCount * cellSize) + (spacing * (visibleCount - 1))
    float newHeight = (visibleCount * cellH) 
                    + (spacingH * Mathf.Max(visibleCount - 1, 0));

    Vector2 size = parentRect.sizeDelta;       
    size.y = newHeight;                        
    parentRect.sizeDelta = size;               
}




    public void CancelAndClose() // 진행 중 제작 취소 + UI 닫기
    {
        CancelCraft();                                 // 진행 중 제작 취소 및 초기화

        UnsubscribeInventoryEvents();                  // 인벤토리 이벤트 구독 해제

        if (panelRoot != null)
        {
            panelRoot.SetActive(false);                // UI 패널 비활성화
        }

        _currentStation = null;                        // 현재 제작대 참조 초기화
        _currentRecipeList = null;                     // 레시피 리스트 참조 초기화
        _currentRecipes = null;                        // 정렬된 레시피 목록 초기화
        _currentRecipe = null;                         // 선택된 레시피 초기화

        ClearAllSlots();                               // 제작 아이템 슬롯/필요 재료 슬롯 모두 제거
        ClearCurrentRecipeDisplay();                   // 메인 아이콘/설명/슬라이더 초기화

        _isOpen = false;                               // 열림 상태 false
    }

    public void CancelOnly() // 진행 중 제작만 취소(UI는 유지)
    {
        CancelCraft();                                 // 제작 관련 상태만 초기화
        RefreshCurrentRecipeRequirements();            // 현재 레시피가 있다면 재료 상태/버튼 상태 갱신
    }

    private void BuildRecipeList() // 제작 아이템 슬롯 전체를 다시 생성
    {
        ClearCraftItemSlots();                         // 기존 제작 아이템 슬롯 제거

        if (_currentRecipeList == null || _currentRecipeList.recipes == null)
        {
            _currentRecipes = null;                    // 레시피가 없으면 리스트 null 설정
            return;
        }

        // displayOrder 기준으로 정렬된 레시피 목록 구성
        _currentRecipes = _currentRecipeList.recipes
            .Where(r => r != null)
            .OrderBy(r => r.displayOrder)
            .ToList();

        if (craftItemParent == null || craftItemSlotPrefab == null) return;

        foreach (var recipe in _currentRecipes)        // 정렬된 레시피들을 순회하며 슬롯 생성
        {
            GameObject go = Instantiate(craftItemSlotPrefab, craftItemParent); // 슬롯 프리팹 인스턴스
            var slot = go.GetComponent<CraftItemSlotUI>();                     // 슬롯 전용 스크립트 가져오기
            if (slot == null)
            {
                Debug.LogWarning("[CraftingUI] CraftItemSlotPrefab에 CraftItemSlotUI가 없습니다.");
                continue;
            }

            slot.SetData(recipe, OnSelectRecipeSlot);  // 레시피와 클릭 콜백 주입
            _craftSlots.Add(slot);                     // 리스트에 추가
        }

        // 선택 안 된 상태에서는 제작 아이템 슬롯만 채워져 있고, 필요 아이템 슬롯은 비어 있음
        ClearRequiredItemSlots();                      // 필요 재료 슬롯은 비워 둔다
        ClearCurrentRecipeDisplayMainOnly();           // 메인 영역의 아이콘/설명은 비움
        UpdateCraftButtonInteractable(false);          // 아무 레시피도 선택되지 않았으므로 제작 버튼 비활성

        // ★ 여기 추가: 제작 아이템 Content 높이 자동 조절
        ResizeParentHeight(
            _craftParentRect,                          // 제작 Content RectTransform
            _craftParentGrid,                          // 제작 Content GridLayoutGroup
            _craftSlots.Count,                         // 표시되는 슬롯 개수
            autoResizeCraftParentHeight                // 자동 조절 여부
        );
    }


    private void OnSelectRecipeSlot(CraftingRecipeEntry recipe, CraftItemSlotUI slot) // 제작 아이템 슬롯 클릭 시 호출
    {
        _currentRecipe = recipe;                       // 현재 선택된 레시피 저장

        UpdateCraftSlotSelection(slot);                // 선택 슬롯 하이라이트 갱신
        UpdateCurrentRecipeDisplay(recipe);            // 메인 아이콘/설명 갱신
        BuildRequiredItemSlots(recipe);                // 필요 재료 슬롯 생성 및 상태 갱신
    }

    private void UpdateCraftSlotSelection(CraftItemSlotUI selectedSlot) // 제작 아이템 슬롯 선택 하이라이트 갱신
    {
        foreach (var slot in _craftSlots)
        {
            if (slot == null) continue;
            bool isSelected = (slot == selectedSlot);
            slot.SetSelected(isSelected);              // 슬롯에 선택 여부 전달
        }
    }

    private void UpdateCurrentRecipeDisplay(CraftingRecipeEntry recipe) // 메인 아이콘/설명 표시 갱신
    {
        ClearCurrentRecipeDisplayMainOnly();           // 기존 아이콘/설명 제거

        if (recipe == null) return;

        // 결과 아이콘 설정(결과 프리팹의 FieldItem에서 icon 가져오기)
        if (resultIconImage != null && recipe.resultPrefab != null)
        {
            var fieldItem = recipe.resultPrefab.GetComponent<FieldItem>(); // FieldItem 가져오기
            if (fieldItem != null)
            {
                resultIconImage.sprite = fieldItem.icon;   // 아이콘 스프라이트 설정
                resultIconImage.enabled = (fieldItem.icon != null); // 아이콘 유무에 따라 표시/숨김
            }
            else
            {
                resultIconImage.sprite = null;             // FieldItem이 없으면 스프라이트 초기화
                resultIconImage.enabled = false;           // 이미지 숨김
            }
        }

        // 설명 TMP 프리팹 인스턴스
        if (descriptionRoot != null && recipe.descriptionTextPrefab != null)
        {
            Instantiate(recipe.descriptionTextPrefab, descriptionRoot); // 설명 텍스트 프리팹을 자식으로 생성
        }
    }

    private void BuildRequiredItemSlots(CraftingRecipeEntry recipe) // 필요 재료 슬롯 전체 생성/갱신
    {
        ClearRequiredItemSlots();                      // 기존 필요 재료 슬롯 제거

        if (recipe == null || recipe.requiredItems == null) return;
        if (requiredItemParent == null || requiredItemSlotPrefab == null) return;
        if (inventory == null)
        {
            Debug.LogWarning("[CraftingUI] Inventory가 없어서 필요 재료 수량을 계산할 수 없습니다.");
        }

        bool allEnough = true;                         // 모든 재료가 충분한지 여부

        // displayOrder 기준으로 정렬
        var sortedRequired = recipe.requiredItems
            .Where(r => r != null)
            .OrderBy(r => r.displayOrder)
            .ToList();

        foreach (var req in sortedRequired)           // 각 필요 재료에 대해 슬롯 생성
        {
            int haveCount = GetInventoryCount(req.typeId, req.itemId); // 인벤토리에서 보유 수량 계산

            GameObject go = Instantiate(requiredItemSlotPrefab, requiredItemParent); // 슬롯 프리팹 인스턴스
            var slot = go.GetComponent<RequiredItemSlotUI>();                       // 전용 스크립트 가져오기
            if (slot == null)
            {
                Debug.LogWarning("[CraftingUI] RequiredItemSlotPrefab에 RequiredItemSlotUI가 없습니다.");
                continue;
            }

            slot.SetData(req, haveCount);             // 슬롯에 데이터/보유 수량 반영
            _requiredSlots.Add(slot);                 // 리스트에 추가

            if (haveCount < req.requiredCount)       // 하나라도 부족하면
            {
                allEnough = false;                   // 전체 충분 플래그 false
            }
        }

        UpdateCraftButtonInteractable(allEnough);     // 재료 충분 여부에 따라 제작 버튼 활성/비활성

        // ★ 여기 추가: 필요 아이템 Content 높이 자동 조절
        ResizeParentHeight(
            _requiredParentRect,                      // 필요 Content RectTransform
            _requiredParentGrid,                      // 필요 Content GridLayoutGroup
            _requiredSlots.Count,                     // 표시되는 슬롯 개수
            autoResizeRequiredParentHeight            // 자동 조절 여부
        );
    }

    private int GetInventoryCount(int typeId, int itemId) // 인벤토리에서 특정 아이템의 총 보유 수량을 계산
    {
        if (inventory == null) return 0;

        int total = 0;                                // 합계 수량

        // ★ 이 부분은 프로젝트의 Inventory 구현에 맞게 조정 필요
        // 예시: Inventory에 GetItems()가 있고, 각 항목에 typeId, itemId, count가 있다고 가정
        var items = inventory.GetItems();             // 인벤토리 내 모든 아이템 스냅샷 가져오기(프로젝트에 맞게 수정)
        if (items == null) return 0;

        foreach (var item in items)
        {
            if (item == null) continue;
            if (item.typeId == typeId && item.itemId == itemId) // 타입/아이디가 일치하면
            {
                total += item.count;                 // 수량 누적
            }
        }

        return total;                                // 합계 수량 반환
    }

    private void RefreshCurrentRecipeRequirements() // 현재 선택된 레시피의 필요 재료 상태를 다시 계산/표시
    {
        if (_currentRecipe == null)                 // 선택된 레시피가 없으면 무시
        {
            UpdateCraftButtonInteractable(false);   // 제작 버튼 비활성
            return;
        }

        if (inventory == null || _requiredSlots.Count == 0)
        {
            UpdateCraftButtonInteractable(false);   // 인벤토리나 슬롯이 없으면 제작 불가
            return;
        }

        bool allEnough = true;                     // 모든 재료 충분 여부

        foreach (var slot in _requiredSlots)       // 각 필요 재료 슬롯에 대해
        {
            if (slot == null || slot.Required == null) continue;

            var req = slot.Required;               // 슬롯이 기억하고 있는 필요 재료 정의
            int haveCount = GetInventoryCount(req.typeId, req.itemId); // 현재 보유 수량 재계산
            slot.RefreshAmount(haveCount);         // 슬롯 UI 업데이트

            if (haveCount < req.requiredCount)     // 부족하면
            {
                allEnough = false;                 // 전체 충분 플래그 false
            }
        }

        UpdateCraftButtonInteractable(allEnough);  // 제작 버튼 상태 갱신
    }

    private void ClearAllSlots() // 제작 아이템 슬롯과 필요 재료 슬롯을 모두 제거
    {
        ClearCraftItemSlots();                     // 제작 아이템 슬롯 제거
        ClearRequiredItemSlots();                  // 필요 재료 슬롯 제거
    }

    private void ClearCraftItemSlots() // 제작 아이템 슬롯만 제거
    {
        foreach (var slot in _craftSlots)          // 저장된 모든 슬롯에 대해
        {
            if (slot != null)
            {
                Destroy(slot.gameObject);          // 슬롯 오브젝트 제거
            }
        }
        _craftSlots.Clear();                      // 리스트 비우기

        // 부모에 직접 남아있는 자식이 있을 경우 안전하게 모두 제거
        if (craftItemParent != null)
        {
            for (int i = craftItemParent.childCount - 1; i >= 0; i--)
            {
                Destroy(craftItemParent.GetChild(i).gameObject); // 자식 오브젝트 제거
            }
        }
    }

    private void ClearRequiredItemSlots() // 필요 재료 슬롯만 제거
    {
        foreach (var slot in _requiredSlots)       // 저장된 모든 슬롯에 대해
        {
            if (slot != null)
            {
                Destroy(slot.gameObject);          // 슬롯 오브젝트 제거
            }
        }
        _requiredSlots.Clear();                   // 리스트 비우기

        if (requiredItemParent != null)
        {
            for (int i = requiredItemParent.childCount - 1; i >= 0; i--)
            {
                Destroy(requiredItemParent.GetChild(i).gameObject); // 자식 오브젝트 제거
            }
        }
    }

    private void ClearCurrentRecipeDisplay() // 메인 표시 + 슬라이더까지 모두 초기화
    {
        ClearCurrentRecipeDisplayMainOnly();       // 아이콘/설명만 초기화

        if (progressRoot != null)
        {
            progressRoot.SetActive(false);         // 진행 슬라이더 영역 비활성
        }

        if (progressSlider != null)
        {
            progressSlider.value = 0f;            // 슬라이더 값 초기화
        }
    }

    private void ClearCurrentRecipeDisplayMainOnly() // 메인 아이콘/설명 영역만 초기화
    {
        if (resultIconImage != null)
        {
            resultIconImage.sprite = null;        // 아이콘 초기화
            resultIconImage.enabled = false;      // 이미지 숨김
        }

        if (descriptionRoot != null)
        {
            for (int i = descriptionRoot.childCount - 1; i >= 0; i--)
            {
                Destroy(descriptionRoot.GetChild(i).gameObject); // 기존 설명 TMP 프리팹 제거
            }
        }
    }

    private void UpdateCraftButtonInteractable(bool canCraft) // 제작 버튼 활성/비활성 설정
    {
        if (craftButton != null)
        {
            craftButton.interactable = canCraft && !_isCrafting && _currentRecipe != null; // 제작 중이 아니고 레시피가 선택된 경우만
        }
    }

    private void OnClickCraftButton() // 제작 버튼 클릭 시 호출
    {
        if (_currentRecipe == null) return;       // 선택된 레시피가 없으면 무시
        if (_isCrafting) return;                  // 이미 제작 중이면 무시

        // 제작 시작 전에 재료 충분 여부 재확인
        if (!CanCraftCurrentRecipe())
        {
            Debug.Log("[CraftingUI] 재료가 부족하여 제작을 시작할 수 없습니다.");
            RefreshCurrentRecipeRequirements();   // UI 갱신
            return;
        }

        _isCrafting = true;                      // 제작 중 상태로 설정
        _craftTimer = 0f;                        // 경과 시간 초기화
        _craftDuration = Mathf.Max(_currentRecipe.craftTimeSeconds, 0.01f); // 0 방지용 최소값

        if (progressRoot != null)
        {
            progressRoot.SetActive(true);        // 진행 슬라이더 영역 활성화
        }
        if (progressSlider != null)
        {
            progressSlider.value = 0f;           // 슬라이더 초기화
        }

        UpdateCraftButtonInteractable(false);    // 제작 중에는 제작 버튼 비활성
        if (cancelButton != null)
        {
            cancelButton.interactable = true;    // 취소 버튼 활성
        }
    }

    private void OnClickCancelButton() // 제작 취소 버튼 클릭 시 호출
    {
        CancelOnly();                            // 제작만 취소(UI 유지)
    }

    private void CancelCraft() // 내부용: 제작 상태 초기화
    {
        _isCrafting = false;                     // 제작 중 플래그 해제
        _craftTimer = 0f;                        // 경과 시간 초기화
        _craftDuration = 0f;                     // 총 시간 초기화

        if (progressRoot != null)
        {
            progressRoot.SetActive(false);       // 진행 슬라이더 영역 비활성
        }
        if (progressSlider != null)
        {
            progressSlider.value = 0f;           // 슬라이더 값 초기화
        }

        if (cancelButton != null)
        {
            cancelButton.interactable = false;   // 취소 버튼 비활성
        }
    }

    private bool CanCraftCurrentRecipe() // 현재 선택된 레시피가 재료 충분한지 검사
    {
        if (_currentRecipe == null) return false;
        if (inventory == null) return false;
        if (_currentRecipe.requiredItems == null) return false;

        foreach (var req in _currentRecipe.requiredItems)
        {
            if (req == null) continue;
            int haveCount = GetInventoryCount(req.typeId, req.itemId); // 현재 보유 수량
            if (haveCount < req.requiredCount)                         // 필요 수량보다 적으면
            {
                return false;                                          // 제작 불가
            }
        }

        return true;                                                   // 모든 재료 충분
    }

    private void CompleteCraft() // 제작 완료 처리
    {
        _isCrafting = false;                     // 제작 중 상태 해제

        if (progressRoot != null)
        {
            progressRoot.SetActive(false);       // 슬라이더 영역 비활성
        }
        if (progressSlider != null)
        {
            progressSlider.value = 1f;           // 완료 상태로 설정(옵션)
        }
        if (cancelButton != null)
        {
            cancelButton.interactable = false;   // 취소 버튼 비활성
        }

        if (_currentRecipe == null || inventory == null)
        {
            UpdateCraftButtonInteractable(false); // 안전 차원에서 버튼 비활성
            return;
        }

        // 완료 시점에 재료 다시 확인(제작 도중 인벤토리 변화 대비)
        if (!CanCraftCurrentRecipe())
        {
            Debug.Log("[CraftingUI] 제작 완료 시점에 재료가 부족하여 제작이 취소되었습니다.");
            RefreshCurrentRecipeRequirements();  // UI 갱신
            return;
        }

        // 1) 재료 소모
        foreach (var req in _currentRecipe.requiredItems)
        {
            if (req == null) continue;
            inventory.ConsumeItem(req.typeId, req.itemId, req.requiredCount); // 타입/아이디/수량으로 소모(프로젝트에 맞게 구현되어 있다고 가정)
        }

        // 2) 결과 아이템 지급(결과 프리팹의 FieldItem 정보 사용)
        if (_currentRecipe.resultPrefab != null)
        {
            var fieldItem = _currentRecipe.resultPrefab.GetComponent<FieldItem>(); // FieldItem 가져오기
            if (fieldItem != null)
            {
                // FieldItem의 정보를 인벤토리 AddItem에 전달(프로젝트 Inventory 시그니처에 맞게 조정)
                inventory.AddItem(
                    fieldItem.typeId,           // 결과 타입 id
                    fieldItem.itemId,           // 결과 아이템 id
                    fieldItem.count,            // 결과 개수(필요 시 1로 고정하거나 SO에 별도 필드 추가)
                    fieldItem.displayName,      // 이름
                    fieldItem.icon,             // 아이콘
                    fieldItem.durability,       // 현재 내구도(초기값)
                    fieldItem.maxDurability,    // 최대 내구도
                    fieldItem.weight            // 무게
                );
            }
            else
            {
                Debug.LogWarning("[CraftingUI] 결과 프리팹에 FieldItem이 없어 인벤토리에 추가할 수 없습니다.");
            }
        }

        // 3) 인벤토리 변경에 따른 필요 재료 UI/버튼 상태 갱신
        RefreshCurrentRecipeRequirements();      // 현재 레시피 재료/버튼 상태 갱신
    }

    private void SubscribeInventoryEvents() // 인벤토리 이벤트 구독(있을 때만)
    {
        if (inventory == null) return;

        // ★ Inventory에 OnInventoryChanged 같은 이벤트가 있을 경우 여기에 구독
        // 예: inventory.OnInventoryChanged += HandleInventoryChanged;
        // 프로젝트 구조에 맞게 직접 연결해주면 된다.
    }

    private void UnsubscribeInventoryEvents() // 인벤토리 이벤트 구독 해제
    {
        if (inventory == null) return;

        // ★ SubscribeInventoryEvents에서 구독한 이벤트를 여기서 해제
        // 예: inventory.OnInventoryChanged -= HandleInventoryChanged;
    }

    public void HandleInventoryChanged() // 인벤토리 변경 시 호출될 콜백(프로젝트 이벤트에서 연결)
    {
        if (!_isOpen) return;            // UI가 닫혀 있으면 무시
        RefreshCurrentRecipeRequirements(); // 현재 선택 레시피 기준으로 필요 재료 상태 갱신
    }
}
