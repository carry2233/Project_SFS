using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class BuildCategoryData
{
    [Tooltip("카테고리 식별자 (예: wall, workbench, turret)")]
    public int id; // 카테고리 ID (정수)

    [Tooltip("카테고리 표시 이름 (예: 벽, 작업대, 포탑)")]
    public string displayName; // 카테고리 이름

    [Tooltip("카테고리 버튼에 표시할 아이콘 (선택)")]
    public Sprite icon; // 카테고리 아이콘

    [Tooltip("이 카테고리에 포함될 건축물들")]
    public List<BuildItemData> items = new List<BuildItemData>(); // 카테고리 내 건축물 리스트
}
