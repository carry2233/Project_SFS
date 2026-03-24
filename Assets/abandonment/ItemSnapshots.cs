using UnityEngine;

[System.Serializable]                            // 전투 수치 스냅샷
public class EquipmentStatsSnap
{
    public int attack;                          // 공격력
    public int trueDamage;                      // 절대위력
    public float bleed;                         // 출혈
}

[System.Serializable]                            // 공통 데이터 스냅샷
public class ItemCommonDataSnap
{
    public int itemId;                          // 아이템 ID
    public int typeId;                          // 종류 ID
    public string fallbackDisplayName;          // 표시 이름
    public Sprite fallbackIcon;                 // 아이콘
}

[System.Serializable]                            // 무게 데이터 스냅샷
public class ItemWeightDataSnap
{
    public float weight;                        // 무게
}

[System.Serializable]                            // 갯수 데이터 스냅샷
public class ItemCountDataSnap
{
    public int stackCount;                      // 스택 수
}

// ───────────────────────── 타입별 복사본 ─────────────────────────

[System.Serializable]
public class MeleeItemSnapshot
{
    public ItemCommonDataSnap common;           // 공통
    public EquipmentStatsSnap equip;            // 전투 수치
    public ItemWeightDataSnap weight;           // 무게
    public string objectId;                     // 고유 ID
    public int durability;                      // 내구도
}

[System.Serializable]
public class RangedItemSnapshot
{
    public ItemCommonDataSnap common;           // 공통
    public EquipmentStatsSnap equip;            // 전투 수치
    public ItemWeightDataSnap weight;           // 무게
    public string objectId;                     // 고유 ID
    public int durability;                      // 내구도
    public int penetration;                     // 관통력
    public int range;                           // 사거리
}

[System.Serializable]
public class ToolItemSnapshot
{
    public ItemCommonDataSnap common;           // 공통
    public EquipmentStatsSnap equip;            // 전투 수치(선택)
    public ItemWeightDataSnap weight;           // 무게
    public string objectId;                     // 고유 ID
    public int durability;                      // 내구도
    public float resourceDropRate;              // 자원 드랍률
}

[System.Serializable]                            // 자원아이템 스냅샷
public class ResourceItemSnapshot
{
    public ItemCommonDataSnap common;           // 공통
    public ItemWeightDataSnap weight;           // 무게
    // objectId 없음
}

[System.Serializable]                            // 일반아이템 스냅샷 (✅ 수정)
public class GeneralItemSnapshot
{
    public ItemCommonDataSnap common;           // 공통
    public ItemCountDataSnap count;             // 갯수
    public ItemWeightDataSnap weight;           // ✅ (추가) 일반 아이템 무게
    // objectId 없음
}

// ─────────────── 의류 스냅샷(참고: 기존에 이미 추가했다면 그대로 유지) ───────────────

[System.Serializable]
public class ApparelStatsSnap
{
    public int absoluteDefense;                 // 절대방어
    public int defenseRate;                     // 방어율
    public int stoppingPower;                   // 저지력
    public int wearStage;                       // 착용단계
    public int wearSlot;                        // 착용부위
}

[System.Serializable]
public class ApparelItemSnapshot
{
    public ItemCommonDataSnap common;           // 공통
    public ApparelStatsSnap apparel;            // 의류 스탯
    public ItemWeightDataSnap weight;           // 무게
    public string objectId;                     // 고유 ID
    public int durability;                      // 내구도
}
