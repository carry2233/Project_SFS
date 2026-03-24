using UnityEngine;

[AddComponentMenu("Item Runtime/Field Item Spawner")]
public class FieldItemSpawner : MonoBehaviour
{
    [System.Serializable]
    public class SpawnEntry
    {
        [Header("프리팹/식별")]
        public GameObject prefab;         // 스폰할 프리팹(필수: FieldItem 포함)       // 프리팹 참조
        public int typeId;                // 종류 id                                     // 식별용
        public int itemId;                // 아이템 id                                   // 식별용

        [Header("표시 데이터")]
        public string displayName;        // 초기 표시 이름                              // 이름
        public Sprite icon;               // 초기 아이콘                                 // 아이콘

        [Header("수량/배치")]
        public int count = 1;             // 초기 수량                                   // 스택 수량
        public Vector3 position;          // 스폰 위치                                   // 배치 좌표
        public Vector3 rotationEuler;     // 스폰 회전(Euler)                             // 배치 회전
    }

    [Header("스폰 목록")]
    public SpawnEntry[] entries;          // 스폰 항목들

    [ContextMenu("Spawn All")]
    public void SpawnAll()                // 모든 항목 스폰
    {
        if (entries == null) return;

        foreach (var e in entries)
        {
            if (!e.prefab)
            {
                Debug.LogWarning("[FieldItemSpawner] 프리팹이 비었습니다.");
                continue;
            }

            var go = Instantiate(e.prefab, e.position, Quaternion.Euler(e.rotationEuler), null);
            var fi = go.GetComponent<FieldItem>();
            if (!fi)
            {
                Debug.LogWarning("[FieldItemSpawner] 프리팹에 FieldItem이 없습니다.");
                continue;
            }

            fi.typeId = e.typeId;                                // 종류 id 설정
            fi.itemId = e.itemId;                                // 아이템 id 설정
            fi.displayName = e.displayName;                      // 이름 설정
            fi.icon = e.icon;                                    // 아이콘 설정
            fi.count = Mathf.Max(1, e.count);                    // 수량 보정
            fi.stackId = System.Guid.NewGuid().ToString();       // 스택 단위 Unique
        }
    }

    [ContextMenu("Clear Children")]
    public void ClearChildren()            // 자식 제거(테스트용)
    {
        var list = new System.Collections.Generic.List<GameObject>();
        foreach (Transform child in transform) list.Add(child.gameObject);
        foreach (var go in list) DestroyImmediate(go);
    }
}
