using TMPro;                                        // TMP
using UnityEngine;                                  // 유니티
using UnityEngine.UI;                               // UI

[AddComponentMenu("Inventory/UI/Equip Action Panel (우클릭 장착/해제/입기/벗기 패널)")]
public class EquipActionPanel : MonoBehaviour       // 장착/해제/입기/벗기 패널
{
    [Header("참조")]
    public EquipInventory equipInventory;          // 장착 인벤토리(무기/도구)
    public Inventory inventory;                    // 일반 인벤토리(이동/차감)
    public WeaponAndToolTypeRegistry registry;     // 무기/도구 레지스트리
    public ApparelInventory apparelInventory;      // 의류 인벤토리
    public ApparelTypeRegistry apparelRegistry;    // 의류 메타 레지스트리
    public ApparelWearStateRegistry wearStateRegistry; // 착용 상태 레지스트리

    [Header("UI")]
    public CanvasGroup canvasGroup;                // 패널 표시/숨김
    public TextMeshProUGUI actionLabel;            // 버튼 라벨
    public Button actionButton;                    // 실행 버튼

    [Header("문구 설정")]
    public string equipText = "장착";              // 무기/도구 장착 텍스트
    public string unequipText = "장착 해제";       // 무기/도구 해제 텍스트
    public string wearText = "입기";                // 의류 입기 텍스트
    public string takeOffText = "벗기";            // 의류 벗기 텍스트

    [Header("백드롭(패널 바깥 클릭 닫기)")]
    public GameObject backdrop;                    // 배경 클릭 닫기 용 오브젝트

    // 동작 모드
    private enum ActionMode { Equip, Unequip, Wear, TakeOff, ConsumeItem } // 동작 모드
    private ActionMode _mode;                      // 현재 모드

    // 공용 컨텍스트
    private int _ctxTypeId;                        // 대상 종류 id
    private int _ctxItemId;                        // 대상 아이템 id
    private string _ctxName;                       // 표시 이름
    private Sprite _ctxIcon;                       // 아이콘
    private int _ctxDurability;                    // 현재 내구도
    private int _ctxMaxDurability;                 // 최대 내구도
    private float _ctxWeight;                      // 무게

    // 추가 컨텍스트
    private int _ctxEquipIndex = -1;               // 장착 인벤토리 인덱스
    private int _ctxApparelIndex = -1;             // 의류 인벤토리 인덱스
    private int _ctxInventoryIndex = -1;           // ⭐ 인벤토리 리스트 인덱스(방법 B 핵심)
    
    // 소비 아이템 효과 저장용
    private ConsumeItemEffectRegistry.EffectData _consumeEffect;
    private ObjectInfo _targetInfo;   // 소비 효과 적용 대상




private void Awake()                           // 초기화
{
    Hide();
    if (actionButton)
    {
        actionButton.onClick.RemoveAllListeners(); // 실행 버튼 리스너 초기화
        actionButton.onClick.AddListener(OnClickAction); // 실행 버튼 클릭 연결
    }
    if (backdrop) backdrop.SetActive(false);       // 백드롭 비활성화

    // ✅ 디버그: 이 패널이 어떤 Inventory 인스턴스를 보고 있는지 확인
    string invName = (inventory != null) ? inventory.name : "null";           // inventory 이름
    int invId      = (inventory != null) ? inventory.GetInstanceID() : 0;     // inventory 인스턴스 ID

    Debug.Log($"[EquipActionPanel] Awake - inventory={invName}, instanceID={invId}");
}


    // =====================================================================
    // 인벤토리 아이템(무기/도구) → 장착 패널 호출
    // =====================================================================
public void ShowForItem(                       // 인벤토리 우클릭 → (무기/도구) 장착
    int inventoryIndex,                       // ⭐ 인벤토리 인덱스
    int typeId,                               // 아이템 종류 id
    int itemId,                               // 아이템 id
    string displayName,                       // 표시 이름
    Sprite icon,                              // 아이콘
    int durability,                           // 현재 내구도
    int maxDurability,                        // 최대 내구도
    float weight,                             // 무게
    Vector2 screenPos                         // 패널 표시 위치
)
{
    // ✅ 디버그: 패널 호출 시 어떤 값들이 들어오는지
    Debug.Log($"[EquipActionPanel] ShowForItem 호출 index={inventoryIndex}, typeId={typeId}, itemId={itemId}, name={displayName}");

    if (!registry || registry.GetKind(typeId, itemId) == WeaponAndToolTypeRegistry.ItemKind.None)
    {
        Debug.LogWarning($"[EquipActionPanel] ShowForItem 취소 - 무기/도구 아님 (typeId={typeId}, itemId={itemId})");
        Hide();
        return;
    }

    _mode = ActionMode.Equip;
    _ctxEquipIndex     = -1;
    _ctxApparelIndex   = -1;
    _ctxInventoryIndex = inventoryIndex;       // ⭐ 어떤 칸에서 빼야 하는지 저장

    _ctxTypeId        = typeId;
    _ctxItemId        = itemId;
    _ctxName          = displayName;
    _ctxIcon          = icon;
    _ctxDurability    = durability;
    _ctxMaxDurability = maxDurability;
    _ctxWeight        = weight;

    if (actionLabel)
        actionLabel.text = string.IsNullOrEmpty(equipText) ? "장착" : equipText;

    // ✅ 디버그: 컨텍스트 최종값 확인
    Debug.Log($"[EquipActionPanel] Mode=Equip, ctxIndex={_ctxInventoryIndex}, ctxTypeId={_ctxTypeId}, ctxItemId={_ctxItemId}");

    PlaceAndShow(screenPos);
}


    // =====================================================================
    // 장착 슬롯(무기/도구) → 해제 패널 호출
    // =====================================================================
    public void ShowForEquipped(                   // 장착 슬롯 우클릭 → 해제
        int equipIndex,                           // 장착 인덱스
        int typeId,                               // 아이템 종류 id
        int itemId,                               // 아이템 id
        string displayName,                       // 표시 이름
        Sprite icon,                              // 아이콘
        int durability,                           // 현재 내구도
        int maxDurability,                        // 최대 내구도
        float weight,                             // 무게
        Vector2 screenPos                         // 패널 표시 위치
    )
    {
        if (!equipInventory || equipIndex < 0 || equipIndex >= equipInventory.capacity)
        {
            Hide();
            return;
        }

        _mode = ActionMode.Unequip;
        _ctxEquipIndex     = equipIndex;
        _ctxApparelIndex   = -1;
        _ctxInventoryIndex = -1;                  // 인벤토리 인덱스 사용 안 함

        _ctxTypeId        = typeId;
        _ctxItemId        = itemId;
        _ctxName          = displayName;
        _ctxIcon          = icon;
        _ctxDurability    = durability;
        _ctxMaxDurability = maxDurability;
        _ctxWeight        = weight;

        if (actionLabel)
            actionLabel.text = string.IsNullOrEmpty(unequipText) ? "장착 해제" : unequipText;

        PlaceAndShow(screenPos);
    }

    // =====================================================================
    // 인벤토리 아이템(의류) → 입기 패널 호출
    // =====================================================================
public void ShowForApparelItem(               // 인벤토리 우클릭 → (의류) 입기
    int inventoryIndex,                       // ⭐ 인벤토리 인덱스
    int typeId,                               // 아이템 종류 id
    int itemId,                               // 아이템 id
    string displayName,                       // 표시 이름
    Sprite icon,                              // 아이콘
    int durability,                           // 현재 내구도
    int maxDurability,                        // 최대 내구도
    float weight,                             // 무게
    Vector2 screenPos                         // 패널 표시 위치
)
{
    Debug.Log($"[EquipActionPanel] ShowForApparelItem 호출 index={inventoryIndex}, typeId={typeId}, itemId={itemId}, name={displayName}");

    if (!apparelRegistry || !apparelRegistry.IsApparel(typeId, itemId))
    {
        Debug.LogWarning($"[EquipActionPanel] ShowForApparelItem 취소 - 의류 아님 (typeId={typeId}, itemId={itemId})");
        Hide();
        return;
    }

    _mode = ActionMode.Wear;
    _ctxEquipIndex     = -1;
    _ctxApparelIndex   = -1;
    _ctxInventoryIndex = inventoryIndex;       // ⭐ 어떤 칸에서 빼야 하는지 저장

    _ctxTypeId        = typeId;
    _ctxItemId        = itemId;
    _ctxName          = displayName;
    _ctxIcon          = icon;
    _ctxDurability    = durability;
    _ctxMaxDurability = maxDurability;
    _ctxWeight        = weight;

    if (actionLabel)
        actionLabel.text = string.IsNullOrEmpty(wearText) ? "입기" : wearText;

    Debug.Log($"[EquipActionPanel] Mode=Wear, ctxIndex={_ctxInventoryIndex}, ctxTypeId={_ctxTypeId}, ctxItemId={_ctxItemId}");

    PlaceAndShow(screenPos);
}


    // =====================================================================
    // 의류 슬롯 → 벗기 패널 호출
    // =====================================================================
    public void ShowForWornApparel(               // 의류 슬롯 우클릭 → (의류) 벗기
        int apparelIndex,                         // 의류 인벤토리 인덱스
        int typeId,                               // 아이템 종류 id
        int itemId,                               // 아이템 id
        string displayName,                       // 표시 이름
        Sprite icon,                              // 아이콘
        int durability,                           // 현재 내구도
        int maxDurability,                        // 최대 내구도
        float weight,                             // 무게
        Vector2 screenPos                         // 패널 표시 위치
    )
    {
        if (!apparelInventory || apparelIndex < 0 || apparelIndex >= apparelInventory.capacity)
        {
            Hide();
            return;
        }

        _mode = ActionMode.TakeOff;
        _ctxEquipIndex     = -1;
        _ctxApparelIndex   = apparelIndex;
        _ctxInventoryIndex = -1;                  // 인벤토리 인덱스 사용 안 함

        _ctxTypeId        = typeId;
        _ctxItemId        = itemId;
        _ctxName          = displayName;
        _ctxIcon          = icon;
        _ctxDurability    = durability;
        _ctxMaxDurability = maxDurability;
        _ctxWeight        = weight;

        if (actionLabel)
            actionLabel.text = string.IsNullOrEmpty(takeOffText) ? "벗기" : takeOffText;

        PlaceAndShow(screenPos);
    }

    

    // =====================================================================
    // 공통: 패널 배치/표시/숨김
    // =====================================================================
    private void PlaceAndShow(Vector2 screenPos)   // 패널 배치 + 표시
    {
        var rt = transform as RectTransform;
        var canvas = GetComponentInParent<Canvas>();
        if (rt && canvas)
        {
            if (canvas.renderMode == RenderMode.ScreenSpaceOverlay)
            {
                RectTransform parentRT = canvas.transform as RectTransform;
                if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                        parentRT, screenPos, null, out var local))
                    rt.anchoredPosition = local;
                else
                    rt.anchoredPosition = Vector2.zero;
            }
            else
            {
                Camera cam = canvas.worldCamera != null ? canvas.worldCamera : Camera.main;
                if (RectTransformUtility.ScreenPointToWorldPointInRectangle(
                        rt, screenPos, cam, out var world))
                    rt.position = world;
                else
                    rt.position = Vector3.zero;
            }
        }

        if (backdrop) backdrop.SetActive(true);

        if (canvasGroup)
        {
            canvasGroup.alpha = 1f;
            canvasGroup.interactable = true;
            canvasGroup.blocksRaycasts = true;
        }
        gameObject.SetActive(true);
    }

public void ShowForConsumable(
    int inventoryIndex,
    ConsumeItemEffectRegistry.EffectData effect,
    ObjectInfo target,
    Vector2 screenPos)
{
    _mode = ActionMode.ConsumeItem;
    _ctxInventoryIndex = inventoryIndex;

    _consumeEffect = effect;
    _targetInfo = target;   // ⭐ 대상 ObjectInfo 저장

    actionLabel.text = effect.actionLabel;
    PlaceAndShow(screenPos);
}




    public void Hide()                             // 패널 숨기기
    {
        if (backdrop) backdrop.SetActive(false);

        if (canvasGroup)
        {
            canvasGroup.alpha = 0f;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
        }
        gameObject.SetActive(false);
    }

    // =====================================================================
    // 실행 버튼 클릭
    // =====================================================================
private void OnClickAction()                   // 버튼 클릭 처리
{
    // ✅ 공통 디버그: 어떤 모드로 실행되는지, 어떤 인벤토리/인덱스인지 확인
    Debug.Log($"[EquipActionPanel] OnClickAction mode={_mode}, ctxInvIndex={_ctxInventoryIndex}, ctxEquipIndex={_ctxEquipIndex}, ctxTypeId={_ctxTypeId}, ctxItemId={_ctxItemId}, inventory={inventory?.name}");

    if (!inventory)
    {
        Debug.LogWarning("[EquipActionPanel] inventory 미할당 - 액션 취소");
        Hide();
        return;
    }

    switch (_mode)
    {
        case ActionMode.Equip:                // 무기/도구 장착
        {
            if (!equipInventory)
            {
                Debug.LogWarning("[EquipActionPanel] equipInventory 미할당 - 장착 취소");
                break;
            }

            bool ok = equipInventory.EquipFirstEmpty(
                _ctxTypeId,
                _ctxItemId,
                _ctxName,
                _ctxIcon,
                _ctxDurability,
                _ctxMaxDurability,
                _ctxWeight
            );

            Debug.Log($"[EquipActionPanel] EquipFirstEmpty 결과 ok={ok}");

            if (ok)
            {
                Debug.Log($"[EquipActionPanel] 장착 성공 - 인벤토리 차감 시도 (ctxIndex={_ctxInventoryIndex})");

                // ⭐ 인덱스 기반 차감 우선 사용
                if (_ctxInventoryIndex >= 0)
                {
                    Debug.Log("[EquipActionPanel] ConsumeItemAtIndex 호출");
                    inventory.ConsumeItemAtIndex(_ctxInventoryIndex, 1);
                }
                else
                {
                    Debug.Log("[EquipActionPanel] ctxIndex<0 → 키 기반 ConsumeItem 호출");
                    inventory.ConsumeItem(_ctxTypeId, _ctxItemId, 1);
                }
            }
            break;
        }

        case ActionMode.Unequip:              // 무기/도구 해제
        {
            if (!equipInventory)
            {
                Debug.LogWarning("[EquipActionPanel] equipInventory 미할당 - 해제 취소");
                break;
            }

            var entry = equipInventory.GetAt(_ctxEquipIndex);
            if (entry != null)
            {
                Debug.Log($"[EquipActionPanel] Unequip - entry typeId={entry.typeId}, itemId={entry.itemId}, name={entry.displayName}");

                if (registry &&
                    registry.TryResolveObject(entry.typeId, entry.itemId, out var obj) &&
                    obj)
                {
                    obj.SetActive(false);     // 실제 무기 오브젝트 비활성화
                }

                equipInventory.UnequipAt(_ctxEquipIndex);

                Debug.Log("[EquipActionPanel] Unequip → Inventory.AddItem 호출");
                inventory.AddItem(            // 인벤토리로 반환(내구도/무게 포함)
                    entry.typeId,
                    entry.itemId,
                    1,
                    entry.displayName,
                    entry.icon,
                    entry.durability,
                    entry.maxDurability,
                    entry.weight
                );
            }
            else
            {
                Debug.LogWarning($"[EquipActionPanel] Unequip - entry null (ctxEquipIndex={_ctxEquipIndex})");
            }
            break;
        }

        case ActionMode.Wear:                 // 의류 입기
        {
            if (!apparelInventory || !apparelRegistry)
            {
                Debug.LogWarning("[EquipActionPanel] Wear - apparelInventory/apparelRegistry 미할당");
                break;
            }

            if (!apparelRegistry.TryGetData(_ctxTypeId, _ctxItemId, out var meta))
            {
                Debug.LogWarning("[Apparel] 레지스트리에 없는 의류입니다. 입기 취소.");
                break;
            }

            int tier = meta.wearSlotTier;
            int slot = meta.wearSlot;

            if (wearStateRegistry &&
                wearStateRegistry.enforceWhitelist &&
                !wearStateRegistry.HasRegisteredSlot(tier, slot))
            {
                Debug.Log($"[Apparel] 미등록 착용부위 → 입기 거부 (tier,slot={tier},{slot})");
                break;
            }

            bool ok = apparelInventory.WearFirstEmpty(
                _ctxTypeId,
                _ctxItemId,
                _ctxName,
                _ctxIcon,
                _ctxDurability,
                _ctxMaxDurability,
                _ctxWeight
            );

            Debug.Log($"[EquipActionPanel] WearFirstEmpty 결과 ok={ok}");

            if (ok)
            {
                Debug.Log($"[EquipActionPanel] Wear 성공 - 인벤토리 차감 시도 (ctxIndex={_ctxInventoryIndex})");

                // ⭐ 의류도 인덱스 기반으로 정확히 차감
                if (_ctxInventoryIndex >= 0)
                {
                    Debug.Log("[EquipActionPanel] (Wear) ConsumeItemAtIndex 호출");
                    inventory.ConsumeItemAtIndex(_ctxInventoryIndex, 1);
                }
                else
                {
                    Debug.Log("[EquipActionPanel] (Wear) ctxIndex<0 → 키 기반 ConsumeItem 호출");
                    inventory.ConsumeItem(_ctxTypeId, _ctxItemId, 1);
                }

                wearStateRegistry?.ApplyWearData(meta, tier, slot);
            }
            break;
        }

        case ActionMode.TakeOff:              // 의류 벗기
        {
            if (!apparelInventory)
            {
                Debug.LogWarning("[EquipActionPanel] TakeOff - apparelInventory 미할당");
                break;
            }

            var entry = apparelInventory.GetAt(_ctxApparelIndex);
            if (entry != null)
            {
                Debug.Log($"[EquipActionPanel] TakeOff - entry typeId={entry.typeId}, itemId={entry.itemId}, name={entry.displayName}");

                int tier = 0, slot = 0;
                if (apparelRegistry &&
                    apparelRegistry.TryGetData(entry.typeId, entry.itemId, out var meta))
                {
                    tier = meta.wearSlotTier;
                    slot = meta.wearSlot;
                }

                apparelInventory.TakeOffAt(_ctxApparelIndex);

                Debug.Log("[EquipActionPanel] TakeOff → Inventory.AddItem 호출");
                inventory.AddItem(            // 인벤토리로 반환
                    entry.typeId,
                    entry.itemId,
                    1,
                    entry.displayName,
                    entry.icon,
                    entry.durability,
                    entry.maxDurability,
                    entry.weight
                );

                wearStateRegistry?.RevertWear(tier, slot);
            }
            else
            {
                Debug.LogWarning($"[EquipActionPanel] TakeOff - entry null (ctxApparelIndex={_ctxApparelIndex})");
            }
            break;
        }

case ActionMode.ConsumeItem:
{
    if (_consumeEffect == null) break;
    if (_targetInfo == null) break;   // ⭐ 없으면 수행 불가

    // 1개 소비
    inventory.ConsumeItemAtIndex(_ctxInventoryIndex, 1);

    var obj = _targetInfo;            // ⭐ 전달받은 ObjectInfo 사용

    obj.SetCurrentNutrition(obj.currentNutrition + _consumeEffect.nutritionDelta);
    obj.SetCurrentHydration(obj.currentHydration + _consumeEffect.hydrationDelta);

    obj.bleedRate = Mathf.Max(0, obj.bleedRate + _consumeEffect.bleedDelta);

    // ⭐⭐ 출혈 적용 핵심! (누락되어 있던 부분)
    obj.EnsureBleedLoopIfNeeded();   // ← 이 한 줄이 출혈 데미지/혈흔/플래시를 즉시 반영시킴

    if (_consumeEffect.hpDelta > 0)
        obj.Heal(_consumeEffect.hpDelta);
    else if (_consumeEffect.hpDelta < 0)
        obj.ApplyDamage(Mathf.Abs(_consumeEffect.hpDelta));

    break;
}



    }

    Hide();                                    // 완료 후 패널 닫기
}

}