using System;
using System.Collections.Generic;
using UnityEngine;

[AddComponentMenu("Combat/Weapon & Tool Type Registry")]
[DisallowMultipleComponent]
public class WeaponAndToolTypeRegistry : MonoBehaviour
{
    [Serializable]
    public class MeleeData
    {
        public int typeId;                     // 종류 id(int)             // 식별용
        public int itemId;                     // 아이템 id(int)           // 식별용
        public int attackPower;                // 공격력(int)              // 전투용
        public int absolutePower;              // 절대위력(int)            // 관통 비교용
        public float bleedRate;                // 출혈 유발량(float)       // 출혈 누적
        public GameObject linkedObject;        // 연결 오브젝트(GameObject)// 장비 본체/히트박스 부모 등

        [Header("피격 페이로드(근접 히트박스/트리거)")]
        public CombatPayload2D payload;        // ✅ 피격에 전달될 CombatPayload2D // type/item/라벨 주입 대상

        [Header("내구도 감소 규칙")]
        public int durabilityOnPenetrate = 1;  // ✅ 방어실패(관통) 시 내구도 감소량
        public int durabilityOnBlocked = 0;    // ✅ 방어성공(무관통) 시 내구도 감소량
    }

    [Serializable]
    public class RangedData
    {
        public int typeId;                     // 종류 id(int)              // 식별용
        public int itemId;                     // 아이템 id(int)            // 식별용
        public int attackPower;                // 공격력(int)               // 전투용
        public int absolutePower;              // 절대위력(int)             // 관통 비교용
        public float bleedRate;                // 출혈 유발량(float)        // 출혈 누적
        public int range;                      // 사거리(int)               // 원거리 전용
        public int penetration;                // 관통력(int)               // 원거리 전용
        public GameObject linkedObject;        // 연결 오브젝트(GameObject) // 장비 본체/발사기 등
        // ⛔ Ranged는 이번 내구도 규칙/페이로드 연동 대상 아님(요청사항: 오직 Melee, Tool)
    }

    [Serializable]
    public class ToolData
    {
        public int typeId;                     // 종류 id(int)              // 식별용
        public int itemId;                     // 아이템 id(int)            // 식별용
        public int attackPower;                // 공격력(int)               // 전투용(채집 도구 등)
        public int absolutePower;              // 절대위력(int)             // 방어 비교용
        public float bleedRate;                // 출혈 유발량(float)        // 출혈 누적
        public int resourceRate;               // 자원 획득률(int)          // 도구 성능
        public GameObject linkedObject;        // 연결 오브젝트(GameObject) // 도구 본체/히트박스 부모 등

        [Header("피격 페이로드(도구 히트박스/트리거)")]
        public CombatPayload2D payload;        // ✅ 피격에 전달될 CombatPayload2D // type/item/라벨 주입 대상

        [Header("내구도 감소 규칙")]
        public int durabilityOnPenetrate = 1;  // ✅ 방어실패(관통) 시 내구도 감소량
        public int durabilityOnBlocked = 0;    // ✅ 방어성공(무관통) 시 내구도 감소량
    }

    [Header("리스트")]
    public List<MeleeData> meleeList = new();   // 근접 리스트(List<MeleeData>)    // 데이터 원본
    public List<RangedData> rangedList = new();// 원거리 리스트(List<RangedData>) // 데이터 원본
    public List<ToolData> toolList = new();    // 도구 리스트(List<ToolData>)     // 데이터 원본

    public enum ItemKind { None, Melee, Ranged, Tool } // 아이템 분류(enum)          // 분류용

    public ItemKind GetKind(int typeId, int itemId)    // 종류 판별 메서드(ItemKind) // 타입 식별
    {
        if (meleeList.Exists(x => x.typeId == typeId && x.itemId == itemId)) return ItemKind.Melee;
        if (rangedList.Exists(x => x.typeId == typeId && x.itemId == itemId)) return ItemKind.Ranged;
        if (toolList.Exists(x => x.typeId == typeId && x.itemId == itemId)) return ItemKind.Tool;
        return ItemKind.None;
    }

    public bool TryResolveObject(int typeId, int itemId, out GameObject obj) // 연결 오브젝트 찾기(bool) // 장비 찾기
    {
        var m = meleeList.Find(x => x.typeId == typeId && x.itemId == itemId);
        if (m != null) { obj = m.linkedObject; return obj; }
        var r = rangedList.Find(x => x.typeId == typeId && x.itemId == itemId);
        if (r != null) { obj = r.linkedObject; return obj; }
        var t = toolList.Find(x => x.typeId == typeId && x.itemId == itemId);
        if (t != null) { obj = t.linkedObject; return obj; }
        obj = null; return false;
    }

    private void Awake()                           // 초기화 메서드(void)             // 페이로드 동기화
    {
        SyncPayloadBinding();                      // ✅ 인스펙터에 연결된 payload에 식별/라벨 주입
    }

    private void OnValidate()                      // 에디터 값 변경 시(void)         // 페이로드 재동기화
    {
        SyncPayloadBinding();                      // ✅ 변경 시에도 즉시 반영
    }

    private void SyncPayloadBinding()              // 내부 메서드(void)               // payload에 type/item/라벨 주입
    {
        // 근접
        if (meleeList != null)
        {
            foreach (var m in meleeList)
            {
                if (m == null || m.payload == null) continue;
                m.payload.typeId = m.typeId;                           // ✅ 식별 주입
                m.payload.itemId = m.itemId;                           // ✅ 식별 주입
                if (string.IsNullOrWhiteSpace(m.payload.ownerName))    // 라벨(소유자) 보정
                    m.payload.ownerName = transform.root.name;
                if (string.IsNullOrWhiteSpace(m.payload.sourceName))   // 라벨(히트소스) 보정
                    m.payload.sourceName = m.linkedObject ? m.linkedObject.name : "Melee";
            }
        }

        // 도구
        if (toolList != null)
        {
            foreach (var t in toolList)
            {
                if (t == null || t.payload == null) continue;
                t.payload.typeId = t.typeId;                            // ✅ 식별 주입
                t.payload.itemId = t.itemId;                            // ✅ 식별 주입
                if (string.IsNullOrWhiteSpace(t.payload.ownerName))     // 라벨(소유자) 보정
                    t.payload.ownerName = transform.root.name;
                if (string.IsNullOrWhiteSpace(t.payload.sourceName))    // 라벨(히트소스) 보정
                    t.payload.sourceName = t.linkedObject ? t.linkedObject.name : "Tool";
            }
        }
    }

    public bool TryGetDurabilityCosts(            // 내구도 규칙 조회(bool)           // (관통/방어) 각각 반환
        int typeId, int itemId,
        out int onPenetrate, out int onBlocked)
    {
        // 근접 우선 탐색
        var m = meleeList != null ? meleeList.Find(x => x.typeId == typeId && x.itemId == itemId) : null;
        if (m != null)
        {
            onPenetrate = Mathf.Max(0, m.durabilityOnPenetrate);
            onBlocked   = Mathf.Max(0, m.durabilityOnBlocked);
            return true;
        }

        // 도구 탐색
        var t = toolList != null ? toolList.Find(x => x.typeId == typeId && x.itemId == itemId) : null;
        if (t != null)
        {
            onPenetrate = Mathf.Max(0, t.durabilityOnPenetrate);
            onBlocked   = Mathf.Max(0, t.durabilityOnBlocked);
            return true;
        }

        onPenetrate = 0;
        onBlocked   = 0;
        return false;
    }
}
