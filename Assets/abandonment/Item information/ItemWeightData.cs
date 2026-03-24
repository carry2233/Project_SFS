using UnityEngine;

[System.Serializable]                                // 인라인 직렬화 허용
public class ItemWeightData
{
    [Header("무게 설정")]
    [Tooltip("아이템 무게(프로젝트 기준 단위, 예: kg)")]
    public float weight;                             // 무게(실수형)
}
