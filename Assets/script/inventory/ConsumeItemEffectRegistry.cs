using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName="Registry/Consume Item Effect Registry")]
public class ConsumeItemEffectRegistry : ScriptableObject
{
    [Serializable]
    public class EffectData
    {
        public int typeId;               // 타입
        public int itemId;               // 아이템ID
        public string actionLabel;       // 패널에 표시할 문구 (ex: “먹기”)

        public int nutritionDelta;       // 영양 변화(±)
        public int hydrationDelta;       // 수분 변화(±)
        public int bleedDelta;           // 출혈 변화(±)
        public int hpDelta;              // 체력 변화(±)
    }

    public List<EffectData> list = new();
    private Dictionary<string, EffectData> _map;

    private string MakeKey(int typeId, int itemId)
        => $"{typeId}:{itemId}";

    private void OnEnable()
    {
        _map = new Dictionary<string, EffectData>();
        foreach (var e in list)
        {
            if (e == null) continue;
            _map[MakeKey(e.typeId, e.itemId)] = e;
        }
    }

    public bool TryGetEffect(int typeId, int itemId, out EffectData data)
    {
        return _map.TryGetValue(MakeKey(typeId, itemId), out data);
    }
}
