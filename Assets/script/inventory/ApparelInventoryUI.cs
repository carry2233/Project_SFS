using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;                    // GraphicRaycaster 사용
using UnityEngine.EventSystems;          // UI 레이캐스트용


[AddComponentMenu("Apparel/UI/Apparel Inventory UI (슬롯 생성 + 우클릭 벗기 패널 호출)")]
[DisallowMultipleComponent]
public class ApparelInventoryUI : MonoBehaviour
{
    [Header("참조")]
    public ApparelInventory apparelInventory;      // 의류 인벤토리 참조
    public ApparelTypeRegistry apparelRegistry;    // 의류 레지스트리(옵션: 정책/판정용)
    public EquipActionPanel actionPanel;           // 우클릭 시 패널 호출(벗기)

    [Header("UI 프리팹/패널")]
    public Transform slotPanel;                    // 슬롯 부모(그리드)
    public GameObject apparelSlotPrefab;           // ApparelSlot 프리팹

    [Header("슬롯 생성")]
    public bool generateOnAwake = true;            // 시작 시 자동 생성
    

    [SerializeField] private List<ApparelSlot> manualSlots = new(); // 수동 배치한 슬롯 리스트(씬에서 직접 드래그)

    [Header("내구도 색상 설정")]
public Color fullDurabilityColor = Color.green;   // 내구도 100%일 때 색
public Color zeroDurabilityColor = Color.red;     // 내구도 0%일 때 색

[Header("이벤트")]
public System.Action<ApparelSlot> onRightClick;   // ✅ 자신의 슬롯을 인자로 받는 델리게이트

// === DisplaySlot 관련 신규 변수 ===
[Header("DisplaySlot 수동 배치")]
public Transform displaySlotPanel;                         // DisplaySlot 부모 오브젝트
public List<ApparelDisplaySlot> displaySlots = new();      // 수동으로 배치한 DisplaySlot 리스트

    [Header("아이템 설명 패널")]
    public ItemDescriptionPanelController descriptionPanel; // 의류 설명창 컨트롤러

    [Header("UI 클릭 레이캐스트")]
    public GraphicRaycaster raycaster;                      // 이 UI가 소속된 캔버스의 Raycaster
    public EventSystem eventSystem;                         // EventSystem (보통 씬에 1개)






    private readonly List<ApparelSlot> _slots = new(); // 슬롯 컴포넌트 캐시

private void Awake()
{
    // 기존 ApparelSlot 초기화 로직 유지
    if (generateOnAwake)
    {
        GenerateSlots();
    }
    else
    {
        _slots.Clear();
        if (manualSlots != null && manualSlots.Count > 0)
        {
            for (int i = 0; i < manualSlots.Count; i++)
            {
                var slot = manualSlots[i];
                if (!slot) continue;

                slot.orderIndex = i + 1;
                slot.onRightClick = OnApparelSlotRightClick;
                _slots.Add(slot);
            }
        }
        RefreshAll();
    }

    // === 신규 추가: DisplaySlot 초기화 ===
    if (displaySlots != null && displaySlots.Count > 0)
    {
        for (int i = 0; i < displaySlots.Count; i++)
        {
            var ds = displaySlots[i];
            if (!ds) continue;

            ds.orderIndex = i + 1;    // 보기 순서 설정
        }
    }

    // 인벤토리 변경 이벤트 구독
    if (apparelInventory)
        apparelInventory.OnChanged += RefreshAll;
}

    private void Update() // 매 프레임 입력 체크(좌클릭 → 설명창)
    {
        if (descriptionPanel == null) return;           // 설명 패널 없으면 처리 안 함
        if (Input.GetMouseButtonDown(0))                // 좌클릭 눌렀을 때 한 번만
        {
            HandleLeftClickForApparelSlots();           // 의류 슬롯 클릭 검사
        }
    }

    private void HandleLeftClickForApparelSlots() // 의류 슬롯 위 좌클릭 시 설명창 표시
    {
        if (!apparelInventory) return;                  // 의류 인벤토리 없으면 종료

        if (raycaster == null)                          // Raycaster 캐시 없으면 부모에서 찾기
            raycaster = GetComponentInParent<GraphicRaycaster>();
        if (eventSystem == null)                        // EventSystem 캐시 없으면 현재 사용
            eventSystem = EventSystem.current;

        if (raycaster == null || eventSystem == null)   // 둘 중 하나라도 없으면 처리 불가
            return;

        var pointer = new PointerEventData(eventSystem) // 마우스 위치 기준 포인터 데이터
        {
            position = Input.mousePosition
        };

        var results = new List<RaycastResult>();        // 레이캐스트 결과 리스트
        raycaster.Raycast(pointer, results);            // UI 레이캐스트 실행

        for (int i = 0; i < results.Count; i++)         // 결과들 순회
        {
            var go = results[i].gameObject;             // 맞은 게임오브젝트
            var slot = go.GetComponentInParent<ApparelSlot>(); // 상위에서 ApparelSlot 찾기
            if (!slot) continue;                        // 의류 슬롯 아니면 건너뜀

            // 이 슬롯이 담당하는 착용부위(티어/슬롯)를 가져옴
            int targetTier = slot.wearSlotTier;         // 클릭된 슬롯의 착용부위 티어
            int targetSlot = slot.wearSlot;             // 클릭된 슬롯의 착용부위 슬롯

            var slotsData = apparelInventory.Slots;     // 실제 의류 데이터 리스트

            ApparelInventory.ApparelEntry match = null; // 클릭된 부위에 매칭되는 의류 데이터
            ApparelTypeRegistry.ApparelData meta = null;// 매칭된 메타 데이터(착용부위/내구도 등)

            // RefreshAll에서와 동일한 방식으로 첫 번째 매칭 항목을 찾음
            for (int s = 0; s < slotsData.Count; s++)
            {
                var e = slotsData[s];
                if (e == null) continue;

                if (apparelRegistry != null &&
                    apparelRegistry.TryGetData(e.typeId, e.itemId, out var m))
                {
                    if (m.wearSlotTier == targetTier && m.wearSlot == targetSlot)
                    {
                        match = e;
                        meta = m;
                        break;
                    }
                }
            }

            if (match != null)                          // 실제로 장착된 아이템이 있을 때만
            {
                descriptionPanel.ShowDescription(       // 타입/아이템ID + 아이콘으로 설명창 열기
                    match.typeId,
                    match.itemId,
                    match.icon                          // ★ 의류 아이콘 전달
                );
            }

            break;                                      // 첫 번째 ApparelSlot만 처리하고 종료
        }
    }





    private void OnDestroy() // 이벤트 해제
    {
        if (apparelInventory) apparelInventory.OnChanged -= RefreshAll;
    }

public void GenerateSlots() // 슬롯 생성(자동 모드에서만 동작)
{
    // 수동 세팅 모드라면 생성 로직을 건너뜀
    if (!generateOnAwake)
    {
        // 수동 모드에서는 manualSlots를 Awake에서 이미 셋업했으므로 여기선 아무 것도 안 함
        return;
    }

    // ===== 아래는 기존 자동 생성 로직 유지 =====
    ClearChildren(slotPanel);
    _slots.Clear();

    int cap = Mathf.Max(1, apparelInventory ? apparelInventory.capacity : 1);
    for (int i = 0; i < cap; i++)
    {
        var go = Instantiate(apparelSlotPrefab, slotPanel);
        var slot = go.GetComponent<ApparelSlot>();
        slot.orderIndex = i + 1;                       // 1-base 표기
        slot.onRightClick = OnApparelSlotRightClick;   // 우클릭 콜백 바인딩
        _slots.Add(slot);
    }

    RefreshAll();
}


    private void ClearChildren(Transform t) // 자식 정리 유틸
    {
        if (!t) return;
        for (int i = t.childCount - 1; i >= 0; i--)
            DestroyImmediate(t.GetChild(i).gameObject);
    }

public void RefreshAll()
{
    if (!apparelInventory) return;

    var slots = apparelInventory.Slots;

    // 기존 ApparelSlot 갱신 루프
    for (int i = 0; i < _slots.Count; i++)
    {
        var ui = _slots[i];
        if (!ui) continue;

        ui.Bind(null, string.Empty, false);
        ui.SetDurability(0, 0, Color.white);

        int targetTier = ui.wearSlotTier;
        int targetSlot = ui.wearSlot;

        ApparelInventory.ApparelEntry match = null;
        ApparelTypeRegistry.ApparelData matchMeta = null;

        for (int s = 0; s < slots.Count; s++)
        {
            var e = slots[s];
            if (e == null) continue;

            if (apparelRegistry.TryGetData(e.typeId, e.itemId, out var meta))
            {
                if (meta.wearSlotTier == targetTier && meta.wearSlot == targetSlot)
                {
                    match = e;
                    matchMeta = meta;
                    break;
                }
            }
        }

        if (match != null)
        {
            ui.Bind(match.icon, match.displayName, false);

            int cur = match.durability;
            int max = match.maxDurability;
            if (max <= 0 && matchMeta != null) max = matchMeta.maxDurability;

            if (max > 0)
            {
                float ratio = (float)cur / max;
                var durColor = Color.Lerp(zeroDurabilityColor, fullDurabilityColor, ratio);
                ui.SetDurability(cur, max, durColor);
            }
            else
            {
                ui.SetDurability(0, 0, Color.white);
            }
        }
    }

    // ========================
    // === DisplaySlot 갱신 ===
    // ========================
    if (displaySlots != null && displaySlots.Count > 0)
    {
        foreach (var ds in displaySlots)
        {
            if (!ds) continue;

            ds.Bind(null, string.Empty, false);
            ds.SetDurability(0, 0, Color.white);

            int targetTier = ds.wearSlotTier;
            int targetSlot = ds.wearSlot;

            ApparelInventory.ApparelEntry match = null;
            ApparelTypeRegistry.ApparelData matchMeta = null;

            // 실제 착용된 의류 탐색
            for (int s = 0; s < slots.Count; s++)
            {
                var e = slots[s];
                if (e == null) continue;

                if (apparelRegistry.TryGetData(e.typeId, e.itemId, out var meta))
                {
                    if (meta.wearSlotTier == targetTier &&
                        meta.wearSlot == targetSlot)
                    {
                        match = e;
                        matchMeta = meta;
                        break;
                    }
                }
            }

            // === DisplaySlot UI 반영 ===
            if (match != null)
            {
                ds.Bind(match.icon, match.displayName, false);

                int cur = match.durability;
                int max = match.maxDurability;
                if (max <= 0 && matchMeta != null) max = matchMeta.maxDurability;

                if (max > 0)
                {
                    float ratio = (float)cur / max;
                    var durColor = Color.Lerp(zeroDurabilityColor, fullDurabilityColor, ratio);
                    ds.SetDurability(cur, max, durColor);
                }
                else
                {
                    ds.SetDurability(0, 0, Color.white);
                }
            }
        }
    }
}






private void OnApparelSlotRightClick(ApparelSlot uiSlot) // 의류 슬롯 우클릭 시 패널 호출용 콜백 메서드
{
    if (!apparelInventory || !apparelRegistry || !actionPanel)
        return;

    int targetTier = uiSlot.wearSlotTier;              // 이 UI 슬롯의 착용 tier
    int targetSlot = uiSlot.wearSlot;                  // 이 UI 슬롯의 착용 slot

    var slots = apparelInventory.Slots;                // 실제 인벤토리 슬롯 리스트
    int matchIndex = -1;                               // 일치하는 인벤토리 인덱스
    ApparelInventory.ApparelEntry entry = null;        // 일치 항목 캐시

    for (int i = 0; i < slots.Count; i++)
    {
        var e = slots[i];
        if (e == null) continue;

        if (apparelRegistry && apparelRegistry.TryGetData(e.typeId, e.itemId, out var meta))
        {
            if (meta.wearSlotTier == targetTier && meta.wearSlot == targetSlot)
            {
                matchIndex = i;
                entry = e;
                break;
            }
        }
    }

    if (matchIndex < 0 || entry == null) return;

    Vector2 screenPos = Input.mousePosition;           // 우클릭 위치(스크린 좌표)

    actionPanel.ShowForWornApparel(
        matchIndex,
        entry.typeId,
        entry.itemId,
        entry.displayName,
        entry.icon,
        entry.durability,          // 현재 내구도
        entry.maxDurability,       // ✅ 최대 내구도 추가 전달
        entry.weight,              // 무게
        screenPos
    );
}




}
