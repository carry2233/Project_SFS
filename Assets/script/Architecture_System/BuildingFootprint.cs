using System.Collections.Generic;                // 리스트 사용을 위한 네임스페이스
using UnityEngine;                               // 유니티 기본 네임스페이스

[AddComponentMenu("Build/Building Footprint (건물 차지범위)")] // 인스펙터 메뉴 경로
public class BuildingFootprint : MonoBehaviour
{
    [Header("차지 범위 설정 (기준: 이 오브젝트의 위치)")]
    public bool includeCenterAsOccupied = true;  // 중심 셀도 차지범위로 포함할지 여부
    public List<Vector2Int> offsets = new();     // 중심 기준 차지 오프셋 목록 (정수 좌표)

    [Header("기즈모 색상 설정")]
    public Color centerColor = new Color(0f, 1f, 1f, 0.3f); // 중심 표시 색(민트, 반투명)
    public Color areaColor = Color.green;        // 차지범위 표시 색(초록, 테두리)

    [Header("기즈모 크기/옵션")]
    public float cellSize = 1f;                  // 한 타일 한 칸의 월드 크기(정사각형 한 변)
    public bool drawGizmos = true;               // 씬 뷰에서 기즈모 표시 여부

    /// <summary>
    /// 현재 방향 기준으로 차지 범위 오프셋들을 반환한다.
    /// </summary>
    public IEnumerable<Vector2Int> GetOffsets(BuildDirection direction) // 방향 기준 차지 오프셋 반환
    {
        if (includeCenterAsOccupied)             // 중심을 차지범위로 포함할 경우
        {
            yield return Vector2Int.zero;        // (0,0)을 먼저 반환
        }

        for (int i = 0; i < offsets.Count; i++)  // 정의된 오프셋들을 순회
        {
            Vector2Int raw = offsets[i];         // 원본 오프셋
            Vector2Int rotated = RotateOffset(raw, direction); // 방향에 따른 회전 적용
            yield return rotated;                // 회전된 오프셋 반환
        }
    }

    /// <summary>
    /// 특정 기준 셀(originCell)과 방향 기준으로 실제 셀 좌표들을 계산한다.
    /// (타일맵 셀 좌표용)
    /// </summary>
    public IEnumerable<Vector3Int> GetCells(Vector3Int originCell, BuildDirection direction) // 기준 셀+방향 기준 셀 좌표 반환
    {
        foreach (var offset in GetOffsets(direction)) // 회전된 오프셋들을 순회
        {
            yield return new Vector3Int(              // 기준 셀 + 오프셋을 셀 좌표로 반환
                originCell.x + offset.x,
                originCell.y + offset.y,
                originCell.z
            );
        }
    }

    /// <summary>
    /// (x,y) 오프셋을 BuildDirection 기준으로 90도 단위 회전시킨다.
    /// </summary>
    private Vector2Int RotateOffset(Vector2Int offset, BuildDirection direction) // 방향에 따른 오프셋 회전
    {
        int x = offset.x;                   // 원본 x
        int y = offset.y;                   // 원본 y

        switch (direction)                  // 방향에 따라 회전 연산
        {
            default:
            case BuildDirection.Up:         // 위: 회전 없음
                return new Vector2Int(x, y);

            case BuildDirection.Right:      // 오른쪽: (x,y) -> (y,-x)
                return new Vector2Int(y, -x);

            case BuildDirection.Down:       // 아래: (x,y) -> (-x,-y)
                return new Vector2Int(-x, -y);

            case BuildDirection.Left:       // 왼쪽: (x,y) -> (-y,x)
                return new Vector2Int(-y, x);
        }
    }

    /// <summary>
    /// 씬 뷰에서 선택되었을 때 차지범위를 기즈모로 그린다.
    /// 중심은 채워진 네모, 차지범위는 테두리 네모로 표시.
    /// </summary>
    private void OnDrawGizmosSelected()     // 오브젝트 선택 시 기즈모 그리기
    {
        if (!drawGizmos) return;           // 기즈모 표시 옵션이 꺼져 있으면 리턴

        // 현재 Transform을 기준으로 그리되,
        // 에디터 기즈모에서는 방향은 Transform의 로컬 회전을 사용한다.
        Vector3 center = transform.position; // 중심 월드 위치

        // 1) 중심 셀 그리기 (민트, 채워진 네모)
        Gizmos.color = centerColor;         // 중심 색 설정
        Gizmos.DrawCube(center, Vector3.one * cellSize); // 중심 셀(채워진 큐브)

        // 2) 차지범위 셀 그리기 (초록, 테두리 네모)
        Gizmos.color = areaColor;           // 범위 색 설정

        // includeCenterAsOccupied와 상관없이 offsets 리스트만 테두리로 그린다.
        for (int i = 0; i < offsets.Count; i++) // 오프셋들을 순회
        {
            Vector2Int raw = offsets[i];    // 원본 오프셋
            // Transform의 회전을 적용해 월드 위치 계산 (씬 시각용)
            Vector3 local = new Vector3(raw.x * cellSize, raw.y * cellSize, 0f); // 로컬 오프셋
            Vector3 worldPos = center + transform.rotation * local; // 회전 반영된 월드 위치

            Gizmos.DrawWireCube(worldPos, Vector3.one * cellSize); // 테두리 네모로 그리기
        }
    }
}
