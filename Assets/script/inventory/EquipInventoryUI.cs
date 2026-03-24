using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;                     // GraphicRaycaster 사용
using UnityEngine.EventSystems;          // UI 레이캐스트용


[AddComponentMenu("Inventory/UI/Equip Inventory UI (슬롯 생성 + 활성 딜레이 + 우클릭 해제 패널 호출)")]
[DisallowMultipleComponent]
public class EquipInventoryUI : MonoBehaviour
{
    // ─────────────────────────────────────────────────────────────────────────────
    // 변수 (Fields)
    // ─────────────────────────────────────────────────────────────────────────────

    [Header("참조")]
    public EquipInventory equipInventory;                // 장착 인벤토리 참조
    public WeaponAndToolTypeRegistry registry;           // 무기/도구 분류 레지스트리
    public EquipActionPanel actionPanel;                 // 장착 해제/교체 패널

    [Header("패널 / 프리팹")]
    public Transform equipSlotPanel;                     // 장착 슬롯 부모(그리드)
    public Transform displaySlotPanel;                   // 상단 표시 슬롯 부모(그리드)
    public GameObject equipSlotPrefab;                   // EquipSlot 프리팹
    public GameObject displaySlotPrefab;                 // EquippedDisplaySlot 프리팹

    [Header("토글 딜레이")]
    public float toggleDelay = 0.5f;                     // (옵션) 장비 토글 딜레이

    [Header("내구도 색상")]
    public Color fullDurabilityColor = Color.green;      // 내구도 100% 색상
    public Color zeroDurabilityColor = Color.red;        // 내구도 0% 색상

        [Header("아이템 설명 패널")]
    public ItemDescriptionPanelController descriptionPanel; // 장비 설명창 컨트롤러

    [Header("UI 클릭 레이캐스트")]
    public GraphicRaycaster raycaster;                      // 이 UI가 소속된 캔버스의 Raycaster
    public EventSystem eventSystem;                         // EventSystem (보통 씬에 1개)


    private readonly List<EquipSlot> _equipSlots = new List<EquipSlot>();                 // 장착 슬롯 캐시
    private readonly List<EquippedDisplaySlot> _displaySlots = new List<EquippedDisplaySlot>(); // 상단 표시 슬롯 캐시

    private Coroutine _toggleCo;                         // 토글 코루틴 핸들
    private int _activeIndex = -1;                       // 현재 활성 장비 인덱스(0-base)

    // ─────────────────────────────────────────────────────────────────────────────
    // Unity 라이프사이클
    // ─────────────────────────────────────────────────────────────────────────────

    private void Awake()                                 // 초기 슬롯 생성 및 1회 리프레시
    {
        GenerateSlots();
        RefreshAll();
    }

        private void Update() // 매 프레임 입력 체크(좌클릭 → 설명창)
    {
        if (descriptionPanel == null) return;           // 설명 패널 없으면 처리 안 함
        if (Input.GetMouseButtonDown(0))                // 좌클릭 눌렀을 때 한 번만
        {
            HandleLeftClickForEquipSlots();             // 장착 슬롯 클릭 검사
        }
    }

    private void HandleLeftClickForEquipSlots() // 장착 슬롯 위 좌클릭 시 설명창 표시
    {
        if (!equipInventory) return;                    // 장착 인벤토리 없으면 종료

        if (raycaster == null)                          // Raycaster 캐시 없으면 부모에서 찾기
            raycaster = GetComponentInParent<GraphicRaycaster>();
        if (eventSystem == null)                        // EventSystem 캐시 없으면 현재 사용
            eventSystem = EventSystem.current;

        if (raycaster == null || eventSystem == null)   // 둘 중 하나라도 없으면 처리 불가
            return;

        var pointer = new PointerEventData(eventSystem) // 마우스 위치 기준 포인터 데이터 생성
        {
            position = Input.mousePosition
        };

        var results = new List<RaycastResult>();        // 레이캐스트 결과 리스트
        raycaster.Raycast(pointer, results);            // UI 레이캐스트 실행

        for (int i = 0; i < results.Count; i++)         // 결과들 순회
        {
            var go = results[i].gameObject;             // 맞은 게임오브젝트
            var slot = go.GetComponentInParent<EquipSlot>(); // 상위에서 EquipSlot 찾기
            if (!slot) continue;                        // EquipSlot 아니면 건너뜀

            int index = _equipSlots.IndexOf(slot);      // 이 슬롯이 몇 번째 장착 슬롯인지
            if (index < 0) return;                      // 리스트에 없으면 종료

            var slotsData = equipInventory.Slots;       // 실제 장착 데이터 리스트
            if (index >= slotsData.Count) return;       // 범위 체크

            var entry = slotsData[index];               // 해당 인덱스의 장착 데이터
            if (entry == null) return;                  // 빈 장비면 종료

            descriptionPanel.ShowDescription(           // 타입/아이템ID + 아이콘으로 설명창 열기
                entry.typeId,
                entry.itemId,
                entry.icon                              // ★ 장착 장비 아이콘 전달
            );
            break;                                      // 첫 번째 슬롯만 처리하고 종료
        }
    }



    private void OnEnable()                              // 인벤토리 변경 이벤트 구독
    {
        if (equipInventory != null)
            equipInventory.OnChanged += RefreshAll;
    }

    private void OnDisable()                             // 인벤토리 변경 이벤트 해제
    {
        if (equipInventory != null)
            equipInventory.OnChanged -= RefreshAll;
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // 슬롯 생성
    // ─────────────────────────────────────────────────────────────────────────────

    private void GenerateSlots()                         // EquipSlot / EquippedDisplaySlot 생성 메서드
    {
        _equipSlots.Clear();
        _displaySlots.Clear();

        if (!equipInventory) return;

        int capacity = Mathf.Max(1, equipInventory.capacity);   // 장착 인벤토리 용량 기준

        // 기존 자식 정리 (원하면 유지하도록 바꿀 수 있음)
        if (equipSlotPanel)
        {
            for (int i = equipSlotPanel.childCount - 1; i >= 0; i--)
                Destroy(equipSlotPanel.GetChild(i).gameObject);
        }
        if (displaySlotPanel)
        {
            for (int i = displaySlotPanel.childCount - 1; i >= 0; i--)
                Destroy(displaySlotPanel.GetChild(i).gameObject);
        }

        for (int i = 0; i < capacity; i++)
        {
            // 1) 장착 슬롯 생성
            if (equipSlotPanel && equipSlotPrefab)
            {
                var go = Instantiate(equipSlotPrefab, equipSlotPanel);
                var es = go.GetComponent<EquipSlot>();
                if (es != null)
                {
                    es.orderIndex = i + 1;                      // 1-base 표시용
                    es.onRightClick = OnEquipSlotRightClick;    // 우클릭 델리게이트 연결
                    es.Bind(null, string.Empty, false);         // 아이콘/라벨 초기화
                    es.SetDurability(0, 0, Color.white);        // 내구도 슬라이더 비활성
                    _equipSlots.Add(es);
                }
            }

            // 2) 상단 표시 슬롯 생성
            if (displaySlotPanel && displaySlotPrefab)
            {
                var goDisp = Instantiate(displaySlotPrefab, displaySlotPanel);
                var ds = goDisp.GetComponent<EquippedDisplaySlot>();
                if (ds != null)
                {
                    ds.orderIndex = i + 1;                      // 1-base 표시용
                    ds.SetItemIcon(null);                       // 아이콘 비우기
                    ds.SetActiveVisual(false);                  // 하이라이트 꺼두기
                    ds.SetDurability(0, 0, Color.white);        // 내구도 슬라이더 비활성
                    _displaySlots.Add(ds);
                }
            }
        }
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // 전체 리프레시
    // ─────────────────────────────────────────────────────────────────────────────

public void RefreshAll()                            // 장착 인벤토리 전체를 UI에 반영하는 메서드
{
    if (!equipInventory) return;

    var slotsData = equipInventory.Slots;           // 실제 장착 데이터 리스트

    // ✅ 활성 인덱스 보정: 슬롯 범위 밖이거나 빈 칸이면 비활성 상태로 리셋
    if (_activeIndex < 0 || _activeIndex >= slotsData.Count || slotsData[_activeIndex] == null)
    {
        _activeIndex = -1;
    }

    // 1) 장착 슬롯(UI) 갱신
    for (int i = 0; i < _equipSlots.Count; i++)
    {
        var uiSlot = _equipSlots[i];
        if (!uiSlot) continue;

        EquipInventory.EquippedEntry entry =
            (i < slotsData.Count) ? slotsData[i] : null;

        if (entry == null)
        {
            uiSlot.Bind(null, string.Empty, false);
            uiSlot.SetDurability(0, 0, Color.white);
            continue;
        }

        // 아이콘/이름 + 활성 상태(선택 인덱스 기준)
        uiSlot.Bind(entry.icon, entry.displayName, (_activeIndex == i));

        // 무기/도구인지 판정
        bool isWeaponOrTool = false;
        if (registry != null)
        {
            var kind = registry.GetKind(entry.typeId, entry.itemId);
            isWeaponOrTool = (kind != WeaponAndToolTypeRegistry.ItemKind.None);
        }

        int cur = entry.durability;
        int max = entry.maxDurability;

        if (isWeaponOrTool && max > 0)
        {
            float ratio = Mathf.Clamp01((float)cur / max);
            Color durColor = Color.Lerp(zeroDurabilityColor, fullDurabilityColor, ratio);
            uiSlot.SetDurability(cur, max, durColor);
        }
        else
        {
            uiSlot.SetDurability(0, 0, Color.white);
        }
    }

    // 2) 상단 EquippedDisplaySlot 갱신
    RefreshDisplaySlots();
}



    private void RefreshDisplaySlots()                  // 상단 장비 표시 슬롯 갱신 메서드
    {
        if (!equipInventory) return;

        var slotsData = equipInventory.Slots;

        for (int i = 0; i < _displaySlots.Count; i++)
        {
            var ds = _displaySlots[i];
            if (!ds) continue;

            EquipInventory.EquippedEntry entry =
                (i < slotsData.Count) ? slotsData[i] : null;

            if (entry == null)
            {
                ds.SetItemIcon(null);
                ds.SetActiveVisual(false);
                ds.SetDurability(0, 0, Color.white);
                continue;
            }

            ds.SetItemIcon(entry.icon);
            ds.SetActiveVisual(_activeIndex == i);

            bool isWeaponOrTool = false;
            if (registry != null)
            {
                var kind = registry.GetKind(entry.typeId, entry.itemId);
                isWeaponOrTool = (kind != WeaponAndToolTypeRegistry.ItemKind.None);
            }

            int cur = entry.durability;
            int max = entry.maxDurability;

            if (isWeaponOrTool && max > 0)
            {
                float ratio = Mathf.Clamp01((float)cur / max);
                Color durColor = Color.Lerp(zeroDurabilityColor, fullDurabilityColor, ratio);
                ds.SetDurability(cur, max, durColor);
            }
            else
            {
                ds.SetDurability(0, 0, Color.white);
            }
        }
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // 외부에서 활성 인덱스 전달 (선택된 장비 표시용)
    // ─────────────────────────────────────────────────────────────────────────────

    public void SetActiveIndex(int index)              // 외부에서 현재 활성 슬롯 인덱스를 설정하는 메서드
    {
        _activeIndex = index;
        RefreshAll();
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // 우클릭 콜백
    // ─────────────────────────────────────────────────────────────────────────────

    private void OnEquipSlotRightClick(int zeroBasedIndex) // EquipSlot에서 우클릭 시 호출되는 콜백
    {
        if (!equipInventory || actionPanel == null) return;

        var slotsData = equipInventory.Slots;
        if (zeroBasedIndex < 0 || zeroBasedIndex >= slotsData.Count) return;

        var entry = slotsData[zeroBasedIndex];
        if (entry == null) return;

        // (원하면 여기서 registry로 무기/도구인지 검사해서 아닌 경우 return 가능)

        Vector2 screenPos = Input.mousePosition;            // 현재 마우스 위치

        actionPanel.ShowForEquipped(                        // 해제/교체 모드로 패널 열기
            zeroBasedIndex,
            entry.typeId,
            entry.itemId,
            entry.displayName,
            entry.icon,
            entry.durability,
            entry.maxDurability,                            // ✅ 최대 내구도까지 함께 전달
            entry.weight,
            screenPos
        );
    }

public void RequestActivateIndex(int index)        // 숫자키 등 외부에서 활성 슬롯 요청
{
    if (!equipInventory) return;

    // 슬롯 범위 검사
    var slotsData = equipInventory.Slots;
    if (index < 0 || index >= slotsData.Count)
        return;

    // 이미 돌고 있는 토글 코루틴이 있으면 중단
    if (_toggleCo != null)
    {
        StopCoroutine(_toggleCo);
        _toggleCo = null;
    }

    // ✅ 토글 딜레이를 포함한 코루틴 시작
    _toggleCo = StartCoroutine(ToggleRoutine(index));
}

private System.Collections.IEnumerator ToggleRoutine(int requestedIndex) // 토글 딜레이 처리용 코루틴
{
    // 딜레이가 있으면 기다렸다가 토글
    if (toggleDelay > 0f)
        yield return new WaitForSeconds(toggleDelay);

    if (!equipInventory)
    {
        _toggleCo = null;
        yield break;
    }

    var slotsData = equipInventory.Slots;
    if (requestedIndex < 0 || requestedIndex >= slotsData.Count)
    {
        _toggleCo = null;
        yield break;
    }

    var requestedEntry = slotsData[requestedIndex];
    if (requestedEntry == null)
    {
        // 요청한 슬롯에 아이템이 없으면 비활성 상태로만 갱신
        SetActiveIndex(-1);
        _toggleCo = null;
        yield break;
    }

    // 1) 이전 활성 무기 비활성화
    if (_activeIndex >= 0 &&
        _activeIndex < slotsData.Count)
    {
        var prevEntry = slotsData[_activeIndex];
        if (prevEntry != null && registry != null)
        {
            if (registry.TryResolveObject(prevEntry.typeId, prevEntry.itemId, out var prevObj) &&
                prevObj != null)
            {
                prevObj.SetActive(false);         // ✅ 이전 무기 오브젝트 비활성화
            }
        }
    }

    // 2) 같은 슬롯을 다시 눌렀다면 → 토글 OFF (무기 끄기 + 활성 인덱스 -1)
    if (requestedIndex == _activeIndex)
    {
        SetActiveIndex(-1);                      // UI에서 활성 표시 제거
        _toggleCo = null;
        yield break;
    }

    // 3) 새로 활성화할 무기 오브젝트 찾기
    if (registry != null &&
        registry.TryResolveObject(requestedEntry.typeId, requestedEntry.itemId, out var newObj) &&
        newObj != null)
    {
        newObj.SetActive(true);                  // ✅ 새 무기 오브젝트 활성화
    }

    // 4) 활성 인덱스 갱신 + UI 리프레시
    SetActiveIndex(requestedIndex);

    _toggleCo = null;
}


}
