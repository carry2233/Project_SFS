using System;                                      // Serializable, Array
using UnityEngine;                                 // Unity 기본
using UnityEngine.Tilemaps;                        // TileBase

// 🔥 BuildItemData 클래스 바깥, 파일 최상단에 둬야 한다.
public enum BuildKind                              // 건설 종류 구분용 enum
{
    Wall = 0,
    Structure = 1
}

public enum BuildDirection                         // 건물 방향(회전 상태) enum
{
    Up = 0,
    Right = 1,
    Down = 2,
    Left = 3
}

[Serializable]
public class BuildItemData
{
    [Serializable]
    public struct RequirementEntry                 // 건설 요구 재료 한 칸
    {
        public int typeId;                         // 요구 타입 ID
        public int itemId;                         // 요구 아이템 ID
        public int count;                          // 필요 개수

        public RequirementEntry(int typeId, int itemId, int count) // 생성자
        {
            this.typeId = typeId;                  // 타입 ID 설정
            this.itemId = itemId;                  // 아이템 ID 설정
            this.count = count;                    // 필요 개수 설정
        }
    }

    [Header("식별 정보")]
    public int id;                                 // 건축물 ID (정수)
    public string displayName;                     // 표시 이름
    public Sprite icon;                            // 아이콘

    [Header("벽 식별 정보(전투/피격용)")]
    public int wallTypeId = 0;                     // ✅ [추가] 벽 타입ID(정수)
    public int wallItemId = 0;                     // ✅ [추가] 벽 아이템ID(정수, 0이면 id를 사용)

    [Header("벽 프리팹(물리/피격 담당)")]
    public GameObject wallPrefab;                     // ✅ 벽 프리팹 오브젝트1( BoxCollider2D TriggerX + WallPrefabController )

    [Header("벽 방어 스탯")]
    public bool isDestructibleWall = false;        // 이 아이템이 '체력/방어를 가진 파괴 가능한 벽'인지 여부
    [Min(1)]
    public int wallMaxHealth = 100;                // 벽 최대 체력
    public int wallStoppingPower = 0;              // 벽 저지력 (CombatPayload2D 관통력에서 깎이는 값)
    [Range(0, 100)]
    public int wallDefenseRate = 0;                // 벽 방어율 (%) – 피해량 감소용
    public int wallAbsoluteDefense = 0;            // 벽 절대 방어치 (absolutePower와 비교용)

    [Header("건설 종류")]
    public BuildKind kind = BuildKind.Wall;        // 건설 종류(Wall/Structure)

    [Header("건설 요구 재료 (타입ID+아이템ID 통합 구조)")]
    public RequirementEntry[] requirements = Array.Empty<RequirementEntry>(); // 요구 재료 목록

    [Header("프리뷰 설정 (프리팹 또는 타일)")]
    public GameObject previewPrefab;               // 프리팹형 프리뷰 (3D/스프라이트)
    public TileBase previewTile;                   // 타일맵형 프리뷰

    [Header("구조물 프리뷰 스프라이트(4방향)")]
    public Sprite previewSpriteUp;                 // 위쪽 구상도 스프라이트
    public Sprite previewSpriteRight;              // 오른쪽 구상도 스프라이트
    public Sprite previewSpriteDown;               // 아래쪽 구상도 스프라이트
    public Sprite previewSpriteLeft;               // 왼쪽 구상도 스프라이트

    [Header("설치 프리팹")]
    public GameObject buildingPrefab;              // 실제 기능을 가진 건물 프리팹(Structure 전용)

    [Header("설치 (게이지 시간)")]
    [Min(0.05f)]
    public float holdTime = 1.0f;                  // 좌클릭 유지 시간(초)
}
