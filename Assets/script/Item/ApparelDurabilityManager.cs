using UnityEngine;    // ✅ 유니티 기본 네임스페이스

[AddComponentMenu("Combat/Apparel Durability Manager (의류 내구도 관리자)")] // ✅ 인스펙터 메뉴 경로
public class ApparelDurabilityManager : MonoBehaviour
{
    // ─────────────────────────────────────────────────────────────────────────────
    // 🔗 참조 (References)
    // ─────────────────────────────────────────────────────────────────────────────

    [Header("참조 인벤토리")]
    [SerializeField] private ApparelInventory apparelInventory;          // ✅ 현재 착용 중인 의류 슬롯/내구도 정보를 가진 인벤토리

    [Header("착용 상태 레지스트리")]
    [SerializeField] private ApparelWearStateRegistry wearStateRegistry; // ✅ 부위별로 어떤 의류가 착용 중인지 관리하는 레지스트리

    // ─────────────────────────────────────────────────────────────────────────────
    // ⚙ 설정 값 (Settings)
    // ─────────────────────────────────────────────────────────────────────────────

    [Header("내구도 감소 설정")]
    [SerializeField, Min(1)]
    private int durabilityLossPerHit = 1;                                // ✅ 피격 1회당 줄어들 내구도(고정값, 피해량과 무관)

    // ─────────────────────────────────────────────────────────────────────────────
    // 🧩 퍼블릭 API
    // ─────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// 플레이어가 피격되었을 때, 피격 판정 부위 키를 전달받아
    /// 그 부위에 해당하는 의류의 내구도를 감소시키는 진입 메서드.
    /// (ObjectInfo / PlayerInfo 쪽에서 호출 예정)
    /// </summary>
/// <summary>
/// 플레이어가 피격되었을 때, 피격 판정 부위 (tier,slot)을 전달받아
/// 그 부위에 해당하는 의류의 내구도를 감소시키는 진입 메서드.
/// </summary>
public void NotifyArmourHit(int wearSlotTier, int wearSlot)       // ✅ 부위 (티어, 슬롯) 기준 진입점
{
    // 0) 필수 참조가 없으면 아무 것도 하지 않음
    if (!apparelInventory || !wearStateRegistry)
        return;

    // 1) 현재 그 부위에 입혀져 있는 의류 메타 조회
    if (!wearStateRegistry.TryGetCurrentData(wearSlotTier, wearSlot, out var meta))
    {
        // 해당 부위에 의류가 없으면 내구도 감소 없음
        return;
    }

    // 2) 실제 내구도 감소 및 파손 처리 위임
    ApplyDurabilityLoss(meta, durabilityLossPerHit);
}


    // ─────────────────────────────────────────────────────────────────────────────
    // 🔍 내부 구현 (슬롯 조회 / 내구도 감소)
    // ─────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// (keyPrimary, keySecondary) 부위 키를 기준으로
    /// 현재 착용 중인 의류의 슬롯 인덱스를 찾아오는 헬퍼.
    /// </summary>


    /// <summary>
    /// 특정 의류 슬롯의 내구도를 감소시키고,
    /// 0 이하가 되면 장착 해제 + 아이템 삭제까지 담당하는 헬퍼.
    /// 실제 구현은 ApparelInventory 쪽에 추가한 메서드를 호출하는 방식으로 최소화.
    /// </summary>
/// <summary>
/// 특정 의류 메타의 내구도를 감소시키고,
/// 0 이하가 되면 장착 해제 + 아이템 삭제까지 담당하는 헬퍼.
/// 실제 구현은 ApparelInventory 쪽 메서드를 호출하는 방식으로 최소화.
/// </summary>
private void ApplyDurabilityLoss(ApparelTypeRegistry.ApparelData meta, int amount) // ✅ 메타 기반 내구도 감소
{
    if (!apparelInventory)
        return;

    if (meta == null)
        return;

    if (amount <= 0)
        return;

    // ApparelInventory에 이미 구현된 메서드 활용
    //  - meta.typeId / meta.itemId로 슬롯을 찾아서
    //  - 내구도 amount만큼 감소
    //  - 0 이하면 슬롯을 null로 처리(장착 해제 + 아이템 소멸)
    apparelInventory.TryApplyDurabilityByMeta(meta, amount);
}


/// <summary>
/// ObjectInfo에서 넘어온 WearDefenseEntry를 기반으로
/// 해당 부위에 있는 의류의 내구도를 감소시키는 통합 처리 메서드.
/// </summary>
public void OnHitWearDefense(ObjectInfo.WearDefenseEntry defense, bool penetrates)
{
    if (defense == null) return;
    if (!wearStateRegistry || !apparelInventory) return;

    // WearDefenseEntry에는 이미 부위 정보(wearSlotTier, wearSlot)가 들어있음
    int tier = defense.wearSlotTier;
    int slot = defense.wearSlot;

    // 1) 부위(tier,slot) → 현재 착용 중인 의류 메타 조회
    if (!wearStateRegistry.TryGetCurrentData(tier, slot, out var meta))
        return; // 해당 부위에 의류 없음

    // 2) 내구도 감소 적용
    ApplyDurabilityLoss(meta, durabilityLossPerHit);
}



    // ─────────────────────────────────────────────────────────────────────────────
    // 🧪 디버그용 보조 (선택)
    // ─────────────────────────────────────────────────────────────────────────────

#if UNITY_EDITOR
    [ContextMenu("Test Hit (1,1)")]                                      // ✅ 에디터에서 컨텍스트 메뉴로 간단 테스트 (예: (1,1) 부위 피격)
    private void EditorTestHit()                                         // ✅ 인스펙터 컨텍스트 메뉴에서 호출해볼 수 있는 테스트 메서드
    {
        NotifyArmourHit(1, 1);
    }
#endif
}
