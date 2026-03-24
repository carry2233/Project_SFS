using System;
using System.Collections.Generic;
using UnityEngine;

[AddComponentMenu("Inventory/Stack Rule Registry (갯수 중복 규칙 + 의류 예외 처리)")]
[DisallowMultipleComponent]
public class StackRuleRegistry : MonoBehaviour
{
    [Serializable]
    public class StackRule
    {
        [Header("식별자")]
        public int typeId;                     // 종류 id
        public int itemId;                     // 아이템 id

        [Header("스택 규칙")]
        public bool canStack = true;           // 중복(스택) 가능 여부
        [Min(1)]
        public int maxStack = 999;             // 최대 중첩 수(최소 1)
    }

    [Header("규칙 리스트(인스펙터에서 편집)")]
    public List<StackRule> rules = new();      // 규칙 원본 리스트

    [Header("전역 기본값(규칙 없을 때 사용)")]
    public bool defaultCanStack = true;        // 기본: 중복 가능
    [Min(1)]
    public int defaultMaxStack = 999;          // 기본: 최대 999

    private readonly Dictionary<string, StackRule> _map = new();   // 규칙 딕셔너리 캐시
    public static StackRuleRegistry Instance { get; private set; } // 싱글톤 인스턴스

    private void Awake()                       // 초기화/인덱스 구축
    {
        if (Instance != null && Instance != this)
            Debug.LogWarning("[StackRuleRegistry] 중복 인스턴스 발견, 최신 인스턴스로 교체합니다.");

        Instance = this;
        RebuildIndex();
    }

    private void OnValidate()                  // 에디터 값 변경 시 인덱스 갱신
    {
        RebuildIndex();
    }

    public void RebuildIndex()                 // 규칙 딕셔너리 재구축
    {
        _map.Clear();
        if (rules == null) return;

        foreach (var r in rules)
        {
            if (r == null) continue;
            r.maxStack = Mathf.Max(1, r.maxStack);
            string key = MakeKey(r.typeId, r.itemId);
            _map[key] = r; // 마지막 항목 우선
        }
    }

    /// <summary>
    /// 주어진 (typeId,itemId)에 대한 스택 규칙을 반환.
    /// 의류(ApparelTypeRegistry) 등록된 아이템은 기본적으로 스택 불가 처리.
    /// </summary>
    public void GetRuleOrDefault(int typeId, int itemId, out bool canStack, out int maxStack)
    {
        // 1️⃣ 사용자 정의 규칙 우선
        if (_map.TryGetValue(MakeKey(typeId, itemId), out var rule))
        {
            canStack = rule.canStack;
            maxStack = rule.maxStack;
            return;
        }

        // 2️⃣ 신규 추가: 의류는 기본적으로 스택 불가 처리
        var apparelReg = FindObjectOfType<ApparelTypeRegistry>();
        if (apparelReg && apparelReg.IsApparel(typeId, itemId))
        {
            canStack = false;
            maxStack = 1;
            return;
        }

        // 3️⃣ 기본 규칙 적용
        canStack = defaultCanStack;
        maxStack = Mathf.Max(1, defaultMaxStack);
    }

    public static string MakeKey(int typeId, int itemId)           // 키 유틸("type:item")
    {
        return $"{typeId}:{itemId}";
    }
}
