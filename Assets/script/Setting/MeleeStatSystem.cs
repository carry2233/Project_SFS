using UnityEngine;

public class MeleeStatSystem : MonoBehaviour
{
    [Header("Target Stat Key")]
    [SerializeField] private int keyPrimary;
    [SerializeField] private int keySecondary;

    [Header("Copied Melee Stat")]
    [SerializeField]
    private StatSaveManager.MirrorEntry meleeStat;

[Header("위력 보정 계산 설정")]
[SerializeField] private float powerBonusPerStat = 0.05f;   // 스탯 1당 위력 증가치

[Header("보정 결과(런타임)")]
[SerializeField] private float calculatedPowerBonus = 0f;  // 보정된 위력값
[SerializeField] private bool isPowerBonusReady = false;   // 보정 계산 완료 여부



public void CopyFromEntity(EntityStatSystem entityStatSystem)
{
    if (entityStatSystem == null) return;

    isPowerBonusReady = false;
    calculatedPowerBonus = 0f;

    if (entityStatSystem.TryGetStat(keyPrimary, keySecondary, out var stat))
    {
        meleeStat = stat;

        int statValue = meleeStat != null ? meleeStat.value : 0;
        calculatedPowerBonus = statValue * Mathf.Max(0f, powerBonusPerStat);
        isPowerBonusReady = true;
    }
}


// ─────────────────────────────────────────
// MeleeWeaponAttack에서 조회할 전용 API
// ─────────────────────────────────────────
// ───────── 외부 제공용 API ─────────
public bool IsPowerBonusReady()
{
    return isPowerBonusReady;
}

public float GetCalculatedPowerBonus()
{
    return calculatedPowerBonus;
}



    // 근접 전투 로직에서 사용할 접근자
    public StatSaveManager.MirrorEntry GetMeleeStat()
    {
        return meleeStat;
    }
}
