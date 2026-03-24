using System.Collections.Generic;                  // List<T> 사용을 위한 네임스페이스
using UnityEngine;                                 // ScriptableObject, GameObject, Sprite 등을 위한 네임스페이스

[CreateAssetMenu(
    fileName = "NewCraftingRecipeList",            // 에셋 생성 시 기본 파일 이름
    menuName = "Crafting/Crafting Recipe List"     // 에셋 생성 메뉴 경로
)]
public class CraftingRecipeList : ScriptableObject // 제작 레시피 전체를 담는 ScriptableObject
{
    public List<CraftingRecipeEntry> recipes = new(); // 제작 가능한 아이템 레시피 리스트
}

[System.Serializable]
public class CraftingRecipeEntry                  // 제작할 아이템 한 칸(레시피 1개)에 대한 데이터
{
    [Header("결과 아이템 설정")]
    public GameObject resultPrefab;               // 결과로 제작될 아이템 프리팹(FieldItem가 붙어 있어야 함)
    public GameObject descriptionTextPrefab;      // 결과 아이템 설명용 TMP 텍스트 프리팹(메인 설명 영역에 붙일 것)

    [Header("제작 시간 / 정렬 순서")]
    public float craftTimeSeconds = 1f;           // 이 레시피의 제작 시간(초 단위)
    public int displayOrder = 0;                  // 제작 아이템 슬롯 나열 순서(작을수록 위/앞에 배치)

    [Header("필요 재료 리스트")]
    public List<CraftingRequiredItemEntry> requiredItems = new(); // 이 레시피에 필요한 재료 리스트
}

[System.Serializable]
public class CraftingRequiredItemEntry            // 레시피에서 필요한 재료 1종에 대한 데이터
{
    [Header("아이템 식별 정보")]
    public int typeId;                            // 인벤토리/아이템 시스템에서 사용하는 타입 ID
    public int itemId;                            // 인벤토리/아이템 시스템에서 사용하는 아이템 ID

    [Header("표시용 정보")]
    public string displayName;                    // UI에 표시할 아이템 이름
    public Sprite icon;                           // UI에 표시할 아이템 아이콘

    [Header("필요 수량 / 정렬 순서")]
    public int requiredCount = 1;                 // 이 재료가 필요한 개수
    public int displayOrder = 0;                  // 필요 아이템 슬롯 나열 순서(작을수록 위/앞에 배치)
}
