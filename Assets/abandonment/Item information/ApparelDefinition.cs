using UnityEngine;

[System.Serializable] // 인스펙터 인라인 입력 허용
public class ApparelDefinition
{
    [Header("의류 전용 방어 스탯")]
    public int absoluteDefense;   // 절대방어(정수)
    public int defenseRate;       // 방어율(정수)
    public int stoppingPower;     // 저지력(정수)
    public int wearStage;         // 착용단계값(정수)
    public int wearSlot;          // 착용부위값(정수)
}
