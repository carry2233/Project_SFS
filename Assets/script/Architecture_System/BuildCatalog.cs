using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "BuildCatalog", menuName = "Build/Build Catalog")]
public class BuildCatalog : ScriptableObject
{
    [Tooltip("카테고리 목록 (벽/작업대/포탑 등)")]
    public List<BuildCategoryData> categories = new List<BuildCategoryData>(); // 카테고리 목록

    // 전체 카테고리 리스트 반환
    public IReadOnlyList<BuildCategoryData> GetCategories() // 카테고리 조회
    {
        return categories;
    }

// ✅ 카테고리 ID를 int로 사용하는 버전
public IReadOnlyList<BuildItemData> GetItemsByCategoryId(int categoryId) // 카테고리 ID로 아이템 조회 메서드
{
    if (categoryId == 0) return System.Array.Empty<BuildItemData>(); // 0이면 비어있는 결과 반환(기본값 가드)

    var cat = categories.Find(c => c != null && c.id == categoryId); // ID가 같은 카테고리 찾기
    return cat != null && cat.items != null ? cat.items : System.Array.Empty<BuildItemData>(); // 카테고리 아이템 리스트 또는 빈 배열
}

public BuildItemData GetItem(int typeId, int itemId) // (typeId,itemId)로 아이템 조회
{
    foreach (var category in categories)             // 모든 카테고리 순회
    {
        if (category == null || category.items == null) continue; // null 가드

        foreach (var item in category.items)         // 카테고리 내 아이템 순회
        {
            if (item == null) continue;              // null 가드

            // ✅ 현재 구조: BuildItemData에는 typeId가 아니라 wallTypeId/wallItemId가 있음
            int resolvedItemId = (item.wallItemId != 0) ? item.wallItemId : item.id; // itemId 결정(0이면 id 사용)

            if (item.wallTypeId == typeId && resolvedItemId == itemId)               // 타입/아이템 동시 일치
                return item;                                                         // 찾으면 반환
        }
    }
    return null;                                                                     // 못 찾으면 null
}



}
