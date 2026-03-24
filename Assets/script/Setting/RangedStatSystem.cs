using UnityEngine;

public class RangedStatSystem : MonoBehaviour
{
    [Header("Target Stat Key")]
    [SerializeField] private int keyPrimary;
    [SerializeField] private int keySecondary;

    [Header("Copied Ranged Stat")]
    [SerializeField]
    private StatSaveManager.MirrorEntry rangedStat;

    [Header("사격 스탯 → 정확도 보정 설정")]
[SerializeField] private float baseAccuracyPerStat = 0.5f;   // 스탯 1당 기본 적중률 증가
[SerializeField] private float minAccuracyPerStat  = 0.2f;   // 스탯 1당 하한 증가
[SerializeField] private float maxAccuracyPerStat  = 0.3f;   // 스탯 1당 상한 증가
[SerializeField] private float recoveryPerStat     = 0.4f;   // 스탯 1당 복원력 증가

[Header("계산 결과(런타임)")]
[SerializeField] private float baseAccuracyBonus;
[SerializeField] private float minAccuracyBonus;
[SerializeField] private float maxAccuracyBonus;
[SerializeField] private float recoveryBonus;
[SerializeField] private bool isAccuracyBonusReady;



public void CopyFromEntity(EntityStatSystem entityStatSystem)
{
    if (entityStatSystem == null) return;

    isAccuracyBonusReady = false;

    if (entityStatSystem.TryGetStat(keyPrimary, keySecondary, out var stat))
    {
        rangedStat = stat; // Deep Copy

        int value = rangedStat != null ? rangedStat.value : 0;

        baseAccuracyBonus = value * baseAccuracyPerStat;
        minAccuracyBonus  = value * minAccuracyPerStat;
        maxAccuracyBonus  = value * maxAccuracyPerStat;
        recoveryBonus     = value * recoveryPerStat;

        isAccuracyBonusReady = true;
    }
}

public bool IsAccuracyBonusReady()
{
    return isAccuracyBonusReady;
}

public float GetBaseAccuracyBonus() => baseAccuracyBonus;
public float GetMinAccuracyBonus()  => minAccuracyBonus;
public float GetMaxAccuracyBonus()  => maxAccuracyBonus;
public float GetRecoveryBonus()     => recoveryBonus;


    // 사격 로직에서 사용할 접근자
    public StatSaveManager.MirrorEntry GetRangedStat()
    {
        return rangedStat;
    }
}
