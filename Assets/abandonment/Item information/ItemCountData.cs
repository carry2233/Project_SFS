using UnityEngine;

[System.Serializable]                                // 인라인 직렬화 허용
public class ItemCountData
{
    [Header("스택/겹침")]
    [Tooltip("겹친아이템수(스택 수량)")]
    public int stackCount;                           // 겹친아이템수(정수)
}
