using System.Reflection;       // 리플렉션용
using UnityEngine;

[AddComponentMenu("Inventory/Equipment Durability Manager")]
public class EquipmentDurabilityManager : MonoBehaviour
{
    [Header("참조")]
    public EquipInventory equipInventory;          // 장착 인벤토리 참조(EquipInventory)
    public EquipInventoryUI equipInventoryUI;      // 활성 슬롯 확인용(EquipInventoryUI)
    public WeaponAndToolTypeRegistry registry;     // linkedObject/내구도 규칙 조회용

    [Header("플레임스로워 감지 설정")]
    public string flameClassName = "FlameThrower"; // 감지할 클래스명(string)
    public string flameFieldName = "isFiring";     // 우클릭 유지 상태 필드명(string)
    public float flameTickSeconds = 0.5f;          // n초마다 durability 1 감소(float)
    public int flameDurabilityPerTick = 1;         // 틱당 내구도 감소량(int)

    [Header("총기 감지 설정")]
    public string gunClassName = "GunShooting";    // 감지할 클래스명(string)
    public string gunFieldName = "recentlyFired";  // 발사 시점 필드명(string)
    public int gunDurabilityPerShot = 1;           // 사격 1회당 내구도 감소량(int)

    [Header("디버깅")]
    public bool verboseLogs = true;                 // 디버깅 로그 On/Off(bool)
    public bool warnOncePerFrame = true;            // 프레임당 경고 1회만(bool)
    private int _lastWarnFrame = -1;                // 경고 프레임 기록(int)

    [Header("총기 감지 안정화(옵션)")]
    public float gunEdgeHoldSeconds = 0.05f;        // 발사 엣지 유지 시간(float)

    [Header("파손 지연(프레임)")]
    public int breakDisableFrames = 5;              // 파손 시 linkedObject 비활성/해제 지연 프레임(int)

    // ====== 지연 파손 큐 ======
    private class PendingBreakEntry                   // 내부 구조체: 지연 파손 항목
    {
        public int slotIndex;                         // 슬롯 인덱스(int)
        public int typeId;                            // 당시 아이템 typeId(int)
        public int itemId;                            // 당시 아이템 itemId(int)
        public string displayName;                    // 로그용 이름(string)
        public GameObject linkedObj;                  // 연결 오브젝트(GameObject)
        public int framesLeft;                        // 남은 대기 프레임(int)
    }

    private readonly System.Collections.Generic.List<PendingBreakEntry> _breakQueue = new(); // 파손 대기 리스트
    private readonly System.Collections.Generic.HashSet<int> _breakQueuedSlots = new();      // 슬롯 중복 방지 집합

    private float _gunEdgeTimer;                    // 엣지 유지 타이머(float)
    private float _flameTimer;                      // 플레임 틱 누적(float)

    private void Update() // 매 프레임 감시(void) // (활성 슬롯 동작 감지 + 파손 큐 처리)
    {
        // 0) 파손 지연 큐 처리
        ProcessBreakQueue();

        if (!equipInventory || !equipInventoryUI || !registry)
        {
            if (verboseLogs && CanWarn()) Debug.LogWarning("[Durability] 참조 누락(equipInventory/equipInventoryUI/registry).");
            return;
        }

        // 1) 활성 슬롯 식별
        int activeIndex = GetActiveIndex();
        if (activeIndex < 0)
        {
            if (verboseLogs && CanWarn()) Debug.Log("[Durability] 활성 슬롯 없음(_activeIndex < 0).");
            return;
        }

        var slots = equipInventory.Slots;
        if (activeIndex >= slots.Count)
        {
            if (verboseLogs && CanWarn()) Debug.LogWarning($"[Durability] 활성 인덱스 {activeIndex}가 슬롯 수 {slots.Count} 초과.");
            return;
        }

        var entry = slots[activeIndex];
        if (entry == null)
        {
            if (verboseLogs && CanWarn()) Debug.Log($"[Durability] 활성 슬롯 {activeIndex}가 비어 있음(null).");
            return;
        }

        // 파손 대기 슬롯은 입력/소모 스킵
        if (_breakQueuedSlots.Contains(activeIndex))
        {
            if (verboseLogs && CanWarn()) Debug.Log($"[Durability] 파손 대기 중으로 입력/소모 스킵(slot={activeIndex}).");
            return;
        }

        // 2) linkedObject 해석
        if (!registry.TryResolveObject(entry.typeId, entry.itemId, out var linkedObj) || !linkedObj)
        {
            if (verboseLogs && CanWarn()) Debug.LogWarning($"[Durability] linkedObject 조회 실패: ({entry.typeId},{entry.itemId}).");
            return;
        }
        if (!linkedObj.activeInHierarchy)
        {
            if (verboseLogs && CanWarn()) Debug.Log($"[Durability] linkedObject 비활성 → 감시 중단. name={linkedObj.name}");
            return;
        }

        bool decHappened = false;

        // 🔥 플레임: 우클릭+좌클릭 유지 시 시간 틱 소모
        var flame = linkedObj.GetComponent(flameClassName);
        if (flame != null)
        {
            bool aimingHeld = Input.GetMouseButton(1);
            bool firingHeld = Input.GetMouseButton(0);
            if (aimingHeld && firingHeld)
            {
                _flameTimer += Time.deltaTime;
                if (verboseLogs) Debug.Log($"[Durability] Flame holding... timer={_flameTimer:F3}/{flameTickSeconds:F3}");
                if (_flameTimer >= flameTickSeconds)
                {
                    _flameTimer = 0f;
                    AdjustDurability(activeIndex, -flameDurabilityPerTick, linkedObj); // 내부 감소
                    decHappened = true;
                }
            }
            else
            {
                _flameTimer = 0f;
            }
        }

        // 🔫 총기: GunShooting에서 보고한 '발사 횟수' 기반으로 소모
        var gunComp = linkedObj.GetComponent<GunShooting>();   // ✅ 활성 무기에 GunShooting이 붙어있는지 확인
        if (gunComp != null)
        {
            int shots = gunComp.ConsumeShotsThisFrame();       // ✅ 이번 Consume 시점까지 누적된 발사 횟수 가져오기(+ 내부 리셋)
            if (shots > 0)
            {
                int totalDelta = -gunDurabilityPerShot * shots; // 사격 1회당 감소량 × 발사 횟수
                AdjustDurability(activeIndex, totalDelta, linkedObj); // 내부 내구도 감소 처리
                if (verboseLogs)
                    Debug.Log($"[Durability] Gun shots={shots}, durability Δ={totalDelta}");
                decHappened = true;
            }
        }

        if (!decHappened && verboseLogs && CanWarn())
            Debug.Log("[Durability] 소모 없음(무기 동작 아님/감지 조건 불충족).");
    }

    private int GetActiveIndex() // 활성 슬롯 인덱스 반환(int) // UI 비공개 필드 리플렉션
    {
        var field = typeof(EquipInventoryUI).GetField("_activeIndex",
            BindingFlags.Instance | BindingFlags.NonPublic);

        if (field == null)
        {
            if (verboseLogs && CanWarn()) Debug.LogError("[Durability] EquipInventoryUI._activeIndex 리플렉션 실패(필드 없음).");
            return -1;
        }

        int idx = (int)field.GetValue(equipInventoryUI);
        if (verboseLogs) Debug.Log($"[Durability] activeIndex={idx}");
        return idx;
    }

    private bool CanWarn() // 프레임당 경고 1회 제한(bool) // 스팸 방지
    {
        if (!warnOncePerFrame) return true;
        if (_lastWarnFrame == Time.frameCount) return false;
        _lastWarnFrame = Time.frameCount;
        return true;
    }

    // ========= 기존 내부 로직(인덱스 기반) =========

private void AdjustDurability(int slotIndex, int delta, GameObject linkedObj) // 내구도 조정(void) // +/- 적용 + 파손 큐 등록
{
    var slots = equipInventory?.Slots;
    if (slots == null)
    {
        if (verboseLogs && CanWarn()) Debug.LogError("[Durability] slots=null");
        return;
    }
    if (slotIndex < 0 || slotIndex >= slots.Count)
    {
        if (verboseLogs && CanWarn()) Debug.LogWarning($"[Durability] 잘못된 인덱스 slotIndex={slotIndex} (count={slots.Count})");
        return;
    }

    var entry = slots[slotIndex];
    if (entry == null)
    {
        if (verboseLogs && CanWarn()) Debug.Log($"[Durability] 슬롯 {slotIndex}가 비어있어 조정 불가.");
        return;
    }

    int before = entry.durability;
    entry.durability += delta;
    if (verboseLogs) Debug.Log($"[Durability] '{entry.displayName}' 내구도 {before} -> {entry.durability} (Δ{delta})");

    if (entry.durability <= 0)
    {
        entry.durability = 0;
        EnqueueBreak(slotIndex, entry, linkedObj); // 지연 파손 큐
    }

    // ✅ 여기 추가: 내구도 변경 사실을 인벤토리/UI에 알림
    if (equipInventory != null)
    {
        equipInventory.NotifyChanged(); // 장착 인벤토리 변경 알림 → EquipInventoryUI.RefreshAll 트리거
    }
}


    private void ProcessBreakQueue() // 파손 지연 큐 처리(void)
    {
        if (_breakQueue.Count == 0) return;

        for (int i = _breakQueue.Count - 1; i >= 0; i--)
        {
            var pb = _breakQueue[i];
            pb.framesLeft--;

            if (pb.framesLeft > 0) continue;

            if (pb.linkedObj)
            {
                pb.linkedObj.SetActive(false);
                if (verboseLogs) Debug.Log($"[Durability] (지연) linkedObject 비활성화 완료: {pb.linkedObj.name}");
            }

            bool didUnequip = false;
            if (equipInventory)
            {
                var slots = equipInventory.Slots;
                if (pb.slotIndex >= 0 && pb.slotIndex < slots.Count)
                {
                    var cur = slots[pb.slotIndex];
                    if (cur != null && cur.typeId == pb.typeId && cur.itemId == pb.itemId)
                    {
                        equipInventory.UnequipAt(pb.slotIndex);
                        didUnequip = true;
                        if (verboseLogs) Debug.Log($"[Durability] (지연) 슬롯 {pb.slotIndex} 장착 해제 완료.");
                    }
                    else
                    {
                        if (verboseLogs && CanWarn())
                            Debug.Log($"[Durability] (지연) 슬롯/아이템 불일치 → 해제 스킵 (slot={pb.slotIndex}, queued={pb.typeId}:{pb.itemId}).");
                    }
                }
            }

            if (equipInventoryUI)
            {
                equipInventoryUI.RefreshAll();
                if (verboseLogs) Debug.Log("[Durability] (지연) UI 리프레시 완료.");
            }

            _breakQueuedSlots.Remove(pb.slotIndex);
            _breakQueue.RemoveAt(i);
        }
    }

    private void EnqueueBreak(int slotIndex, EquipInventory.EquippedEntry entry, GameObject linkedObj) // 파손 지연 큐 등록(void)
    {
        if (_breakQueuedSlots.Contains(slotIndex))
        {
            if (verboseLogs && CanWarn()) Debug.Log($"[Durability] (지연) 이미 큐에 등록된 슬롯입니다. slot={slotIndex}");
            return;
        }

        int frames = Mathf.Max(0, breakDisableFrames);
        var pb = new PendingBreakEntry
        {
            slotIndex = slotIndex,
            typeId = entry.typeId,
            itemId = entry.itemId,
            displayName = entry.displayName,
            linkedObj = linkedObj,
            framesLeft = frames
        };

        _breakQueue.Add(pb);
        _breakQueuedSlots.Add(slotIndex);

        if (verboseLogs) Debug.Log($"[Durability] (지연) '{entry.displayName}' 파손 등록: {frames}프레임 뒤 비활성/해제 예정 (slot={slotIndex}).");
    }

    // ========= ✅ 신규 공개 API : 피격 페이로드/식별자 기반 감소 =========

    public void AdjustDurabilityByPayload(CombatPayload2D payload, bool penetrated) // 공개 메서드(void) // 페이로드 기반 감소
    {
        if (payload == null || registry == null || equipInventory == null)
        {
            if (verboseLogs && CanWarn()) Debug.LogWarning("[Durability] AdjustDurabilityByPayload: 참조 누락.");
            return;
        }

        int typeId = payload.typeId;
        int itemId = payload.itemId;

        if (!registry.TryGetDurabilityCosts(typeId, itemId, out int onPen, out int onBlk))
        {
            if (verboseLogs && CanWarn()) Debug.Log($"[Durability] 내구도 규칙 없음(type,item= {typeId}:{itemId}).");
            return;
        }

        int cost = penetrated ? onPen : onBlk;
        if (cost <= 0)
        {
            if (verboseLogs) Debug.Log($"[Durability] 감소 없음({(penetrated ? "관통" : "방어")}, cost=0).");
            return;
        }

        string reason = penetrated ? "근접/도구:관통" : "근접/도구:방어성공";
        AdjustDurabilityByTypeItem(typeId, itemId, -Mathf.Abs(cost), reason, payload.SourceLabel());
    }

    public void AdjustDurabilityByTypeItem( // 공개 메서드(void) // (type,item) 직접 지정 감소
        int typeId, int itemId, int delta, string reason, string sourceLabel = "")
    {
        var slots = equipInventory?.Slots;
        if (slots == null || slots.Count == 0)
        {
            if (verboseLogs && CanWarn()) Debug.LogWarning("[Durability] 슬롯 없음/참조 누락.");
            return;
        }

        // 1) 활성 슬롯 우선 감소
        int activeIndex = GetActiveIndex();
        if (activeIndex >= 0 && activeIndex < slots.Count)
        {
            var e = slots[activeIndex];
            if (e != null && e.typeId == typeId && e.itemId == itemId)
            {
                GameObject linkedObj = null;
                registry.TryResolveObject(e.typeId, e.itemId, out linkedObj);
                LogDurability(reason, e.displayName, e.durability, e.durability + delta, delta, sourceLabel);
                AdjustDurability(activeIndex, delta, linkedObj);
                return;
            }
        }

        // 2) 첫 일치 슬롯 찾아 감소
        for (int i = 0; i < slots.Count; i++)
        {
            var e = slots[i];
            if (e != null && e.typeId == typeId && e.itemId == itemId)
            {
                GameObject linkedObj = null;
                registry.TryResolveObject(e.typeId, e.itemId, out linkedObj);
                LogDurability(reason, e.displayName, e.durability, e.durability + delta, delta, sourceLabel);
                AdjustDurability(i, delta, linkedObj);
                return;
            }
        }

        if (verboseLogs && CanWarn())
            Debug.Log($"[Durability] 대상 장비를 장착 중이 아님(type,item={typeId}:{itemId}). 감소 스킵.");
    }

    private void LogDurability( // 내부 메서드(void) // 감소 로그 출력
        string reason, string displayName, int before, int after, int delta, string sourceLabel)
    {
        Debug.Log(
            $"[Durability] ({reason}) '{displayName}' 내구도 {before}→{after} (Δ{delta})"
            + (string.IsNullOrWhiteSpace(sourceLabel) ? "" : $" | from {sourceLabel}")
        );
    }
}
