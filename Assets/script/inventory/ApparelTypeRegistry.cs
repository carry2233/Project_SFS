using System;
using System.Collections.Generic;
using UnityEngine;

[AddComponentMenu("Apparel/Apparel Type Registry (의류 메타데이터)")]
[DisallowMultipleComponent]
public class ApparelTypeRegistry : MonoBehaviour
{
[Serializable]
public class ApparelData
{
    [Header("식별")]
    public int typeId;              // 종류 id
    public int itemId;              // 아이템 id

    [Header("방어 수치")]
    public int absoluteDefense;     // 절대방어(정수)
    public int defenseRate;         // 방어률(정수, % 해석)

    [Header("착용 부위")]
    public int wearSlotTier;        // 착용부위단계(정수)
    public int wearSlot;            // 착용부위(정수)
    public int maxDurability;       // 최대 내구도

    [Header("렌더러(시각 토글 대상)")]
    public SpriteRenderer targetRenderer; // ✅ 활성/비활성할 SpriteRenderer
}


    [Header("의류 리스트(인스펙터에서 등록)")]
    public List<ApparelData> apparelList = new(); // 의류 데이터 목록

    private readonly Dictionary<string, ApparelData> _map = new(); // 빠른 접근용 맵
    public static ApparelTypeRegistry Instance { get; private set; } // 싱글톤

    private void Awake() // 초기화 및 인덱스 구성
    {
        if (Instance != null && Instance != this)
            Debug.LogWarning("[ApparelTypeRegistry] 중복 인스턴스 발견, 최신 인스턴스로 교체합니다.");
        Instance = this;
        RebuildIndex(); // 맵 재구축
    }

    private void OnValidate() // 에디터 값 변경 시 맵 갱신
    {
        RebuildIndex();
    }

    public void RebuildIndex() // 맵 재구축(중복시 마지막 항목 우선)
    {
        _map.Clear();
        if (apparelList == null) return;
        foreach (var a in apparelList)
        {
            if (a == null) continue;
            _map[MakeKey(a.typeId, a.itemId)] = a;
        }
    }

    public bool IsApparel(int typeId, int itemId) // 의류 여부 판정
    {
        return _map.ContainsKey(MakeKey(typeId, itemId));
    }

    public bool TryGetData(int typeId, int itemId, out ApparelData data) // 의류 메타 데이터 조회
    {
        return _map.TryGetValue(MakeKey(typeId, itemId), out data);
    }

public bool TryResolveRenderer(int typeId, int itemId, out SpriteRenderer sr) // ✅ 렌더러 조회
{
    if (TryGetData(typeId, itemId, out var d) && d != null && d.targetRenderer)
    {
        sr = d.targetRenderer; // 지정된 SpriteRenderer 반환
        return true;
    }
    sr = null;
    return false;
}


    public static string MakeKey(int typeId, int itemId) // 키 유틸("type:item")
    {
        return $"{typeId}:{itemId}";
    }
}
