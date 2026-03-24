using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Build/Walls/Wall Item Type Catalog", fileName = "WallItemTypeCatalog")]
public class WallItemTypeCatalog : ScriptableObject
{
    [Serializable]
    public class Entry                                                 // 항목
    {
        public int typeId;                                            // 타입 ID
        public int itemId;              // 아이템 ID (0이면 타입 공통 등으로 사용 가능)
        public string displayName;                                    // 표시 이름
        public Sprite icon;                                           // 아이콘
    }

    [Header("타입ID → 표시정보 매핑")]
    public List<Entry> entries = new();                               // 목록

public Entry Find(int typeId)                       // 타입만으로 검색(구버전 호환용)
{
    for (int i = 0; i < entries.Count; i++)
        if (entries[i] != null && entries[i].typeId == typeId)
            return entries[i];
    return null;
}

public Entry Find(int typeId, int itemId)          // 타입ID+아이템ID 둘 다 일치 검색
{
    for (int i = 0; i < entries.Count; i++)
    {
        var e = entries[i];
        if (e == null) continue;
        if (e.typeId == typeId && e.itemId == itemId)
            return e;
    }
    return null;
}

}
