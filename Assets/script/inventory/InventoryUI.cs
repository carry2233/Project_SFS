using System;                                      // Serializable 등
using System.Collections;
using System.Collections.Generic;
using UnityEngine;                                  // MonoBehaviour 등
using UnityEngine.UI;                               // Button, Image
using TMPro;                                        // TextMeshProUGUI

[AddComponentMenu("Inventory/UI/Inventory UI (Pooling + Pagination + Equip/Apparel Panel Hook)")]
[DisallowMultipleComponent]
public class InventoryUI : MonoBehaviour           // 인벤토리 UI 컨트롤러
{
    [Serializable]
    public class TypeOrder                        // 종류 정렬 규칙 항목
    {
        public int typeId;                        // 종류 id
        public int order;                         // 나열 우선순위
    }

    [Header("참조")]
    public Inventory inventory;                   // 인벤토리 참조
    public Transform slotContainer;               // 슬롯 부모(그리드)
    public GameObject slotPrefab;                 // ItemSlot 프리팹

    [Header("정렬 규칙(종류별 순서)")]
    public List<TypeOrder> typeOrders = new();    // 종류 정렬 규칙 리스트

    [Header("페이지네이션")]
    [Min(1)] public int itemsPerPage = 20;        // 페이지당 슬롯 수
    [SerializeField] private int currentPage = 0; // 현재 페이지(0-base)

    public Button prevButton;                     // 이전 페이지 버튼
    public Button nextButton;                     // 다음 페이지 버튼
    public TextMeshProUGUI pageLabel;             // 페이지 라벨

    [Header("장착 관련 참조")]
    public EquipActionPanel actionPanel;                // 우클릭 패널
    public WeaponAndToolTypeRegistry weaponRegistry;    // 무기/도구 레지스트리
    public ApparelTypeRegistry apparelRegistry;         // 의류 레지스트리

    [Header("소비 아이템 효과 레지스트리")]
    public ConsumeItemEffectRegistry consumeRegistry;   // 소비 아이템 효과 DB


    [Header("표시 토글(CANVAS GROUP)")]
    public CanvasGroup canvasGroup;               // 표시/상호작용 토글
    public KeyCode toggleKey = KeyCode.I;         // 인벤토리 토글 키

    [Header("내구도 색상 설정")]
    public Color fullDurabilityColor = Color.green; // 내구도 100% 색상
    public Color zeroDurabilityColor = Color.red;   // 내구도 0% 색상

    [Header("소비 효과 적용 대상(ObjectInfo)")]
    public ObjectInfo targetObjectInfo;   // 플레이어 또는 적용할 개체

        [Header("아이템 설명 패널")]
    public ItemDescriptionPanelController descriptionPanel; // 아이템 설명창 컨트롤러



    private bool _visible = true;                 // 현재 표시 상태

    // 런타임 캐시/풀
    private readonly Dictionary<int, int> _typeOrderMap = new(); // typeId → order
    private readonly List<GameObject> _activeSlots = new();      // 활성 슬롯 리스트
    private readonly Stack<GameObject> _slotPool = new();        // 비활성 슬롯 풀
    private readonly List<int> _pageItemIndices = new();         // 현재 페이지에 표시할 인벤토리 인덱스들

    private bool _dirty;                          // 리프레시 필요 플래그
    private int _totalItems;                      // 전체 아이템 수
    private int _totalPages;                      // 전체 페이지 수

private void Awake()                          // 초기 설정
{
    RebuildTypeOrderMap();                   // 정렬 규칙 캐시

    if (prevButton)
        prevButton.onClick.AddListener(OnClickPrev);
    if (nextButton)
        nextButton.onClick.AddListener(OnClickNext);

    // EquipActionPanel과 Inventory 인스턴스 동기화
    if (actionPanel != null)
    {
        if (actionPanel.inventory == null)
        {
            actionPanel.inventory = inventory;
        }
        else if (actionPanel.inventory != inventory)
        {
            Debug.LogWarning(
                "[InventoryUI] EquipActionPanel.inventory가 " +
                "InventoryUI.inventory와 달라서 강제로 동일하게 맞춥니다."
            );
            actionPanel.inventory = inventory;
        }

        // ✅ 디버그: Awake 시점 인벤토리 참조 상태
        Debug.Log($"[InventoryUI] Awake - inventory={inventory?.name}, invID={inventory?.GetInstanceID()}, actionPanel.inventory={actionPanel.inventory?.name}, apInvID={actionPanel.inventory?.GetInstanceID()}");
    }

    if (canvasGroup)
    {
        canvasGroup.alpha = _visible ? 1f : 0f;
        canvasGroup.interactable = _visible;
        canvasGroup.blocksRaycasts = _visible;
    }
}


    private void OnEnable()                       // 이벤트 구독
    {
        if (inventory != null)
            inventory.OnInventoryChanged += OnInventoryChanged;

        MarkDirty();                              // 처음 한 번 그리기 예약
    }

    private void OnDisable()                      // 이벤트 해제
    {
        if (inventory != null)
            inventory.OnInventoryChanged -= OnInventoryChanged;
    }

    private void Update()                         // 리프레시 + 토글
    {
        if (_dirty)
        {
            _dirty = false;
            RefreshPage();
        }

        if (Input.GetKeyDown(toggleKey))
        {
            SetVisible(!_visible);
        }
    }

    public void SetVisible(bool visible) // 인벤토리 UI 표시/숨김
    {
        _visible = visible;

        if (canvasGroup)
        {
            canvasGroup.alpha = _visible ? 1f : 0f;             // 알파 값 조정
            canvasGroup.interactable = _visible;                // 상호작용 가능 여부
            canvasGroup.blocksRaycasts = _visible;              // 레이캐스트 차단 여부
        }

        if (!_visible && actionPanel)                           // 인벤토리 닫힐 때
        {
            actionPanel.Hide();                                 // 기존 행동 패널 숨기기
        }

        if (!_visible && descriptionPanel != null)              // 인벤토리 닫힐 때
        {
            descriptionPanel.HidePanel();                       // 아이템 설명 패널도 함께 숨기기
        }
    }


    private void OnValidate()                     // 에디터 값 변경 시
    {
        RebuildTypeOrderMap();
        itemsPerPage = Mathf.Max(1, itemsPerPage);
    }

private void OnInventoryChanged(List<int> changedIndices) // 인벤토리 변경 이벤트
{
    Debug.Log($"[InventoryUI] OnInventoryChanged 호출 - changedIndices={(changedIndices == null ? "null(전체)" : string.Join(",", changedIndices))}");
    MarkDirty();
}


    private void MarkDirty()                      // 다음 프레임에 리프레시
    {
        _dirty = true;
    }

    private void RebuildTypeOrderMap()            // 정렬 규칙 캐시 재구축
    {
        _typeOrderMap.Clear();
        foreach (var to in typeOrders)
        {
            if (to == null) continue;
            _typeOrderMap[to.typeId] = to.order;
        }
    }

    private int GetTypeOrder(int typeId)          // typeId → 순서
    {
        return _typeOrderMap.TryGetValue(typeId, out var ord) ? ord : int.MaxValue;
    }

    private List<int> BuildSortedIndexList()      // 정렬된 인벤토리 인덱스 리스트 생성
    {
        var sorted = new List<int>();
        var items = inventory ? inventory.GetItems() : null;
        if (items == null) return sorted;

        for (int i = 0; i < items.Count; i++)
            sorted.Add(i);

        sorted.Sort((a, b) =>
        {
            var ia = items[a];
            var ib = items[b];
            int oa = GetTypeOrder(ia.typeId);
            int ob = GetTypeOrder(ib.typeId);
            int c = oa.CompareTo(ob); if (c != 0) return c;
            c = ia.itemId.CompareTo(ib.itemId); if (c != 0) return c;
            return string.CompareOrdinal(ia.stackId, ib.stackId);
        });

        return sorted;
    }

    private void CalcPagination(List<int> sorted) // 페이지 계산
    {
        _totalItems = sorted.Count;
        _totalPages = Mathf.Max(1, Mathf.CeilToInt(_totalItems / (float)itemsPerPage));
        currentPage = Mathf.Clamp(currentPage, 0, _totalPages - 1);

        int start = currentPage * itemsPerPage;
        int end = Mathf.Min(start + itemsPerPage, _totalItems);

        _pageItemIndices.Clear();
        for (int i = start; i < end; i++)
            _pageItemIndices.Add(sorted[i]);

        UpdatePagerUI();
    }

    private void UpdatePagerUI()                  // 페이지 UI 갱신
    {
        if (pageLabel)
            pageLabel.text = $"{currentPage + 1} / {_totalPages}";

        if (prevButton) prevButton.interactable = (currentPage > 0);
        if (nextButton) nextButton.interactable = (currentPage < _totalPages - 1);
    }

    private void RefreshPage()                    // 실제 슬롯 그리기
    {
        if (!inventory || !slotContainer || !slotPrefab)
            return;

        var sorted = BuildSortedIndexList();
        CalcPagination(sorted);
        EnsureActiveSlotCount(_pageItemIndices.Count);

        for (int i = 0; i < _pageItemIndices.Count; i++)
        {
            int itemIndex = _pageItemIndices[i]; // 인벤토리 인덱스
            var slotGO = _activeSlots[i];
            BindSlot(slotGO, itemIndex);
            slotGO.transform.SetSiblingIndex(i);
        }
    }

    private void EnsureActiveSlotCount(int needed) // 활성 슬롯 수 조정(풀링)
    {
        while (_activeSlots.Count < needed)
        {
            GameObject slotGO = _slotPool.Count > 0 ? _slotPool.Pop()
                                                   : Instantiate(slotPrefab);
            slotGO.transform.SetParent(slotContainer, false);
            var slot = slotGO.GetComponent<ItemSlot>();
            if (!slot)
            {
                Debug.LogError("[InventoryUI] ItemSlot 컴포넌트가 슬롯 프리팹에 없습니다.");
                Destroy(slotGO);
                continue;
            }
            slot.Show(null, 0, false);
            _activeSlots.Add(slotGO);
        }

        while (_activeSlots.Count > needed)
        {
            int last = _activeSlots.Count - 1;
            var slotGO = _activeSlots[last];
            var slot = slotGO.GetComponent<ItemSlot>();
            if (slot) slot.Hide();
            slotGO.transform.SetParent(slotContainer, false);
            _activeSlots.RemoveAt(last);
            _slotPool.Push(slotGO);
        }
    }

    private void BindSlot(GameObject slotGO, int itemIndex) // 슬롯 하나 바인딩
    {
        if (!inventory || slotGO == null) return; // 인벤토리/슬롯 유효성 검사

        var items = inventory.GetItems();         // 인벤토리 아이템 리스트 가져오기
        if (items == null || itemIndex < 0 || itemIndex >= items.Count)
        {
            var emptySlot = slotGO.GetComponent<ItemSlot>(); // 빈 슬롯 처리용 컴포넌트 획득
            if (emptySlot)
            {
                emptySlot.Clear();                           // 아이콘/텍스트 초기화
                emptySlot.SetDurability(0, 0, Color.white);  // 내구도 초기화
                emptySlot.SetInteractable(false);            // 상호작용 비활성
                emptySlot.onRightClick = null;               // 우클릭 이벤트 제거
            }
            return;
        }

        var inst = items[itemIndex];                         // 실제 아이템 인스턴스
        var slot = slotGO.GetComponent<ItemSlot>();          // 슬롯 컴포넌트 획득
        if (!slot)
        {
            Debug.LogError("[InventoryUI] ItemSlot 컴포넌트가 없습니다.");
            return;
        }

        // 1) 기본 아이콘/수량 표시
        StackRuleRegistry.Instance.GetRuleOrDefault(
            inst.typeId, inst.itemId,
            out bool canStack, out _);                       // 스택 가능 여부 조회

        bool showCount = canStack;                           // 개수 표시 여부
        slot.Show(inst.icon, inst.count, showCount);         // 아이콘 + 개수 표시
        slot.SetInteractable(true);                          // 클릭 가능하게 설정

        slot.slotIndex = itemIndex;                          // 이 슬롯이 가리키는 인벤토리 인덱스
        slot.SetData(inst.typeId, inst.itemId,               // 타입/아이템ID/이름/아이콘 설정
                     inst.displayName, inst.icon);

        // 2) 내구도 슬라이더 색상
        bool isWeaponOrTool = false;                         // 무기/도구 여부 플래그
        bool isApparel = false;                              // 의류 여부 플래그

        if (weaponRegistry)
        {
            var kind = weaponRegistry.GetKind(inst.typeId, inst.itemId); // 무기/도구 분류 가져오기
            isWeaponOrTool = (kind != WeaponAndToolTypeRegistry.ItemKind.None);
        }

        if (apparelRegistry)
        {
            isApparel = apparelRegistry.IsApparel(inst.typeId, inst.itemId); // 의류 여부 판정
        }

        if ((isWeaponOrTool || isApparel) && inst.maxDurability > 0)
        {
            float ratio = Mathf.Clamp01((float)inst.durability / inst.maxDurability); // 내구도 비율 계산
            Color durColor = Color.Lerp(
                zeroDurabilityColor,
                fullDurabilityColor,
                ratio
            );                                                   // 비율에 따른 색상 보간

            slot.SetDurability(inst.durability, inst.maxDurability, durColor); // 내구도 UI 반영
        }
        else
        {
            slot.SetDurability(0, 0, Color.white);               // 내구도 없으면 초기화
        }

        // 3) 우클릭 이벤트 연결
        slot.SetActionPanel(actionPanel);                        // 행동 패널 참조 설정
        slot.onRightClick = OnSlotRightClick;                    // 우클릭 시 패널 호출

        // 4) 좌클릭 → 아이템 설명창 열기
        if (slot.button != null)                                 // 슬롯에 Button이 연결되어 있을 때만
        {
            int capturedIndex = itemIndex;                       // 인벤토리 인덱스를 캡처
            slot.button.onClick.RemoveAllListeners();            // 기존 onClick 리스너 초기화
            slot.button.onClick.AddListener(                     // 새 onClick 리스너 등록
                () => OnSlotLeftClick(capturedIndex)             // 좌클릭 시 설명창 호출
            );
        }
    }

    private void OnSlotLeftClick(int itemIndex) // 좌클릭된 인벤토리 슬롯 설명창 표시
    {
        if (descriptionPanel == null) return;                   // 설명창 패널이 없으면 무시
        if (!inventory) return;                                 // 인벤토리 없으면 무시

        var items = inventory.GetItems();                       // 아이템 리스트 가져오기
        if (items == null) return;                              // 리스트 없으면 무시
        if (itemIndex < 0 || itemIndex >= items.Count) return;  // 인덱스 범위 체크

        var inst = items[itemIndex];                            // 해당 인덱스 아이템
        descriptionPanel.ShowDescription(                       // 설명창에 타입/아이템ID + 아이콘 전달
            inst.typeId,
            inst.itemId,
            inst.icon                                           // ★ 아이콘 스프라이트 추가 전달
        );
    }




    public void OnClickPrev()                      // 이전 페이지 버튼
    {
        if (currentPage <= 0) return;
        currentPage--;
        MarkDirty();
    }

    public void OnClickNext()                      // 다음 페이지 버튼
    {
        if (currentPage >= _totalPages - 1) return;
        currentPage++;
        MarkDirty();
    }

private void OnSlotRightClick(int slotIndex)   // 슬롯 우클릭 → 패널 호출
{
 var items = inventory.GetItems();
    if (slotIndex < 0 || slotIndex >= items.Count)
        return;

    var inst = items[slotIndex];
    Vector2 screenPos = Input.mousePosition;

    // 0) 소비 아이템인지 먼저 검사
    if (consumeRegistry != null &&
        consumeRegistry.TryGetEffect(inst.typeId, inst.itemId, out var effect))
    {
        // ⭐ 소비 아이템 전용 패널 열기 (ObjectInfo 전달)
        actionPanel.ShowForConsumable(
            slotIndex,
            effect,
            targetObjectInfo,   // ⭐ 어떤 개체에게 적용할지 전달
            screenPos
        );
        return;
    }

    // 1) 무기/도구 검사
    if (weaponRegistry != null)
    {
        var kind = weaponRegistry.GetKind(inst.typeId, inst.itemId);
        if (kind != WeaponAndToolTypeRegistry.ItemKind.None)
        {
            actionPanel.ShowForItem(
                slotIndex,
                inst.typeId, inst.itemId,
                inst.displayName,
                inst.icon,
                inst.durability,
                inst.maxDurability,
                inst.weight,
                screenPos
            );
            return;
        }
    }

    // 2) 의류 검사
    if (apparelRegistry != null &&
        apparelRegistry.IsApparel(inst.typeId, inst.itemId))
    {
        actionPanel.ShowForApparelItem(
            slotIndex,
            inst.typeId, inst.itemId,
            inst.displayName,
            inst.icon,
            inst.durability,
            inst.maxDurability,
            inst.weight,
            screenPos
        );
        return;
    }
}



    
}
