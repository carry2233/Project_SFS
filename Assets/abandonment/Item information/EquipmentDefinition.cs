using UnityEngine;

[System.Serializable] // 인스펙터에 인라인으로 펼쳐 입력 가능
public class EquipmentStats
{
    [Header("전투 수치(템플릿 기본값처럼 사용)")]
    public int attack;                    // 공격력(정수)
    public int trueDamage;                // 절대위력/방어무시(정수)
    public float bleed;                   // 출혈(실수) - 프로젝트 규격(확률/틱딜/초당 등)을 하나로 고정하여 사용
}
