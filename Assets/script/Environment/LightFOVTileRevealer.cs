using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;
using UnityEngine.Rendering.Universal;

[AddComponentMenu("Rendering/Light FOV Tile Revealer (DDA)")]
public class LightFOVTileRevealer : MonoBehaviour
{
    // ====== 타겟 & 오버레이 ======
    [Header("타겟/오버레이")]
    [SerializeField] private Tilemap targetTilemap;           // 가시성 판정 대상 타일맵(칠해진 타일 존재 확인)
    [SerializeField] private Tilemap overlayTilemap;          // 가림용 오버레이 타일맵(검은 타일을 찍음)
    [SerializeField] private TileBase overlayTile;            // 오버레이에 사용할 검은 타일

    // ====== 라이트 / FOV ======
    [Header("라이트/FOV")]
    [SerializeField] private Light2D sourceLight;             // 기준이 되는 2D 라이트(Spot 권장)
    [SerializeField] private bool useLightParams = true;      // 라이트의 반지름/각도 사용 여부(true면 라이트에서 읽음)
    [SerializeField] private float radiusOverride = 12f;      // 수동 반지름(라이트값 미사용 시)
    [SerializeField] private float outerAngleDegOverride = 90f; // 수동 외부 시야각(도)
    [SerializeField] private Vector2 forwardAxis = Vector2.right; // 라이트 전방 기준축(기본: X+)
    [SerializeField] private float angleOffsetDeg = 0f;       // 전방축 보정 각도(필요 시)

    // ====== 레이 샘플/품질 ======
    [Header("샘플/품질")]
    [SerializeField, Min(4)]  private int rayCount = 256;     // 레이 개수(각도 등분 수)
    [SerializeField] private bool ignoreStartCell = true;     // 시작 셀(라이트가 있는 셀)을 히트에서 제외
    [SerializeField] private bool revealNeighborPadding = true; // 히트 셀 주변 여유 노출 여부
    [SerializeField, Range(0, 2)] private int neighborPad = 1; // 여유 칸 수(0~2 권장)
    [SerializeField] private int maxCellsPerRay = 2048;       // 레이 당 최대 셀 스텝(안전장치)

    // ====== 갱신 제어 ======
    [Header("갱신 제어")]
    [SerializeField] private bool recomputeEveryFrame = true; // 매 프레임 재계산 여부
    [SerializeField] private float moveThreshold = 0.02f;     // 위치 변화 임계값(초과 시 재계산)
    [SerializeField] private float rotateThresholdDeg = 0.5f; // 회전 변화 임계값(초과 시 재계산)
    [SerializeField] private float recomputeInterval = 0f;    // 최소 재계산 간격(초)

    // ====== 범위 제한(선택) ======
    [Header("검사 범위(선택)")]
    [SerializeField] private bool useClampBounds = false;     // 특정 BoundsInt로 후보 셀 제한할지
    [SerializeField] private BoundsInt clampToBounds;         // 제한할 그리드 범위

    // ====== 디버그 ======
    [Header("디버그")]
    [SerializeField] private bool drawGizmos = false;         // 기즈모 표시
    [SerializeField] private Color gizmoRayColor = Color.yellow; // 레이 색
    [SerializeField] private Color gizmoHitColor = Color.green;  // 히트 셀 색

    // ---- 여기부터 신규 ----
[SerializeField] private bool drawRayLines = true;                // 레이 라인 그리기 여부
[SerializeField] private Color gizmoRayLineColor = new(1f,1f,0f,0.6f); // 레이 라인 색(반투명 노랑)
[SerializeField] private bool drawToHitCenter = true;             // 히트 시 셀 중심까지 라인
// -----------------------

    // ---- 내부 상태 ----
    private HashSet<Vector3Int> visibleCells = new();         // 이번 프레임 보이는 셀 집합
    private HashSet<Vector3Int> lastVisibleCells = new();     // 이전 프레임 보이는 셀 집합
    private HashSet<Vector3Int> overlayActiveCells = new();   // 현재 오버레이에 실제로 칠해진 셀
    private List<Vector3Int> wedgeCandidates = new();         // 부채꼴 내부 후보 셀 캐시
    private bool wedgeCacheValid = false;                     // 후보 캐시 유효 여부

    private Vector3 lastPos;                                   // 마지막 라이트 위치(재계산 판단용)
    private float lastYawDeg;                                  // 마지막 라이트 방위각(재계산 판단용)
    private float lastUpdateTime;                              // 마지막 재계산 시각

    private Grid grid;                                         // Grid 참조(셀 크기/좌표 변환)
    private Vector3 cellSize;                                  // 그리드 셀 크기(월드 단위)
    private static readonly Vector3Int[] Neigh8 =              // 8방향 이웃 오프셋
    {
        new( 1, 0, 0), new(-1, 0, 0), new(0, 1, 0), new(0,-1, 0),
        new( 1, 1, 0), new(-1, 1, 0), new(1,-1, 0), new(-1,-1, 0)
    };

    private void Awake() // 초기화
    {
        if (!targetTilemap || !overlayTilemap || !overlayTile)
        {
            Debug.LogError("[LightFOVTileRevealer] 타일맵/오버레이/타일 연결이 필요합니다.", this);
        }
        if (!sourceLight)
        {
            Debug.LogWarning("[LightFOVTileRevealer] Source Light이 비어 있습니다. 수동 파라미터를 사용합니다.", this);
        }

        grid = targetTilemap.layoutGrid;
        cellSize = grid ? (Vector3)grid.cellSize : Vector3.one;
        lastPos = GetLightOrigin(); // 시작 위치 저장
        lastYawDeg = GetLightYawDeg(); // 시작 각도 저장
        lastUpdateTime = -999f;
    }

    private void Update() // 매 프레임 갱신(필요 시)
    {
        if (!ShouldRecompute()) return;
        RecomputeFOV(); // FOV 재계산 및 오버레이 갱신
    }

    private bool ShouldRecompute() // 재계산 여부 판단
    {
        if (recomputeEveryFrame) return true;

        float now = Time.time;
        if (now - lastUpdateTime < recomputeInterval) return false;

        Vector3 pos = GetLightOrigin();
        float yaw = GetLightYawDeg();

        if ((pos - lastPos).sqrMagnitude > (moveThreshold * moveThreshold)) return true;
        if (Mathf.Abs(Mathf.DeltaAngle(yaw, lastYawDeg)) > rotateThresholdDeg) return true;

        return false;
    }

    private Vector3 GetLightOrigin() // 라이트 원점(월드)
    {
        return sourceLight ? (Vector3)sourceLight.transform.position : transform.position;
    }

    private float GetLightYawDeg() // 라이트 방위각(전방의 각도, 도)
    {
        // 전방축(forwardAxis)을 라이트 트랜스폼으로 변환해 월드 방향을 얻는다.
        Vector3 dirW = (sourceLight ? sourceLight.transform : transform).TransformDirection(new Vector3(forwardAxis.x, forwardAxis.y, 0f));
        Vector2 dir2 = new Vector2(dirW.x, dirW.y).normalized;
        float deg = Mathf.Atan2(dir2.y, dir2.x) * Mathf.Rad2Deg + angleOffsetDeg;
        return deg;
    }

    private void RecomputeFOV() // FOV 재계산 + 오버레이 갱신
    {
        // 1) 파라미터 수집
        Vector3 origin = GetLightOrigin();
        float centerYaw = GetLightYawDeg();
        float radius = useLightParams && sourceLight ? sourceLight.pointLightOuterRadius : radiusOverride;
        float outerHalf = (useLightParams && sourceLight && sourceLight.lightType == Light2D.LightType.Point)
                        ? sourceLight.pointLightOuterAngle * 0.5f
                        : outerAngleDegOverride * 0.5f;

        // 2) 후보 셀 캐시(부채꼴 내부) 업데이트
        CollectWedgeCandidates(origin, centerYaw, radius, outerHalf);

        // 3) 레이 캐스팅(DDA)로 '첫 히트' 셀 모으기
        visibleCells.Clear();

        float startAng = centerYaw - outerHalf;
        float endAng   = centerYaw + outerHalf;
        float step = (endAng - startAng) / Mathf.Max(1, rayCount - 1);

        for (int i = 0; i < rayCount; i++)
        {
            float a = startAng + step * i;
            Vector2 dir = new Vector2(Mathf.Cos(a * Mathf.Deg2Rad), Mathf.Sin(a * Mathf.Deg2Rad));
            if (dir.sqrMagnitude < 1e-6f) continue;

            if (CastRayDDA(origin, dir.normalized, radius, out Vector3Int hitCell))
            {
                visibleCells.Add(hitCell);

                // 이웃 여유 패딩
                if (revealNeighborPadding && neighborPad > 0)
                {
                    AddNeighborPadding(hitCell, neighborPad, visibleCells);
                }
            }
        }

        // 4) 오버레이 갱신(보이는 셀 비우고, 나머지 후보는 채움)
        UpdateOverlayTiles();

        // 5) 상태 저장
        lastPos = origin;
        lastYawDeg = centerYaw;
        lastUpdateTime = Time.time;
        lastVisibleCells.Clear();
        foreach (var c in visibleCells) lastVisibleCells.Add(c);
    }

    private void CollectWedgeCandidates(Vector3 origin, float centerYaw, float radius, float outerHalf) // 부채꼴 내부 후보 셀 모으기
    {
        wedgeCandidates.Clear();

        // 간단한 AABB로 후보 범위 제한
        Vector3 minW = origin + new Vector3(-radius, -radius, 0f);
        Vector3 maxW = origin + new Vector3( radius,  radius, 0f);

        Vector3Int minC = targetTilemap.WorldToCell(minW);
        Vector3Int maxC = targetTilemap.WorldToCell(maxW);

        if (useClampBounds)
        {
            minC.x = Mathf.Max(minC.x, clampToBounds.xMin);
            minC.y = Mathf.Max(minC.y, clampToBounds.yMin);
            maxC.x = Mathf.Min(maxC.x, clampToBounds.xMax - 1);
            maxC.y = Mathf.Min(maxC.y, clampToBounds.yMax - 1);
        }

        Vector2 fwd = new Vector2(Mathf.Cos(centerYaw * Mathf.Deg2Rad), Mathf.Sin(centerYaw * Mathf.Deg2Rad));

        for (int y = minC.y; y <= maxC.y; y++)
        {
            for (int x = minC.x; x <= maxC.x; x++)
            {
                var cell = new Vector3Int(x, y, 0);
                Vector3 center = targetTilemap.GetCellCenterWorld(cell);
                Vector2 toCell = (Vector2)(center - origin);

                if (toCell.sqrMagnitude > radius * radius) continue; // 원 밖 제외

                // 각도 제한(부채꼴)
                float ang = Vector2.Angle(fwd, toCell);
                if (ang > outerHalf + 0.001f) continue;

                // 실제 타일이 있는 셀만 후보로(원한다면 빈칸도 가릴 수 있으나 여기선 최소화)
                if (targetTilemap.HasTile(cell))
                    wedgeCandidates.Add(cell);
            }
        }

        wedgeCacheValid = true;
    }

    private bool CastRayDDA(Vector3 origin, Vector2 dir, float maxDist, out Vector3Int hitCell) // DDA로 '첫 타일' 찾기
    {
        hitCell = default;

        // 시작 셀
        Vector3Int cell = targetTilemap.WorldToCell(origin);

        // 시작 셀 무시 옵션
        bool canHitStart = !ignoreStartCell;

        // DDA 준비
        int stepX = dir.x >= 0 ? 1 : -1;
        int stepY = dir.y >= 0 ? 1 : -1;

        float dx = Mathf.Abs(dir.x);
        float dy = Mathf.Abs(dir.y);

        float tDeltaX = (dx < 1e-6f) ? float.PositiveInfinity : (cellSize.x / dx);
        float tDeltaY = (dy < 1e-6f) ? float.PositiveInfinity : (cellSize.y / dy);

        // 현재 위치가 셀 경계로부터 얼마나 떨어져 있는지 계산 → 첫 경계까지의 t
        // 현재 셀의 경계(월드) 계산
        Vector3 cellCenter = targetTilemap.GetCellCenterWorld(cell);
        float minX = cellCenter.x - cellSize.x * 0.5f;
        float maxX = cellCenter.x + cellSize.x * 0.5f;
        float minY = cellCenter.y - cellSize.y * 0.5f;
        float maxY = cellCenter.y + cellSize.y * 0.5f;

        float tMaxX, tMaxY;

        if (stepX > 0) tMaxX = (dx < 1e-6f) ? float.PositiveInfinity : ( (maxX - origin.x) / dx );
        else           tMaxX = (dx < 1e-6f) ? float.PositiveInfinity : ( (origin.x - minX) / dx );

        if (stepY > 0) tMaxY = (dy < 1e-6f) ? float.PositiveInfinity : ( (maxY - origin.y) / dy );
        else           tMaxY = (dy < 1e-6f) ? float.PositiveInfinity : ( (origin.y - minY) / dy );

        // 누적 이동거리(=dir이 정규화되어 있으니 t가 곧 거리)
        float traveled = 0f;

        for (int i = 0; i < maxCellsPerRay; i++)
        {
            // 현재 셀 히트 검사
            if (canHitStart)
            {
                if (targetTilemap.HasTile(cell))
                {
                    hitCell = cell;
                    return true;
                }
            }
            else
            {
                // 한 번 스텝 이후부터 히트 허용
                canHitStart = true;
            }

            // 다음 경계로 이동(더 가까운 축 선택)
            if (tMaxX < tMaxY)
            {
                traveled = tMaxX;
                if (traveled > maxDist) break;

                tMaxX += tDeltaX;
                cell.x += stepX;
            }
            else
            {
                traveled = tMaxY;
                if (traveled > maxDist) break;

                tMaxY += tDeltaY;
                cell.y += stepY;
            }

            // 범위 제한
            if (useClampBounds && !clampToBounds.Contains(cell)) break;
        }

        return false; // 히트 없음
    }

    private void AddNeighborPadding(Vector3Int center, int pad, HashSet<Vector3Int> dst) // 히트 셀 주변 여유 추가
    {
        if (pad <= 0) return;

        // pad=1 → 8이웃, pad=2 → 2겹 반복
        var frontier = new Queue<(Vector3Int cell, int depth)>();
        frontier.Enqueue((center, 0));

        var visited = new HashSet<Vector3Int> { center };

        while (frontier.Count > 0)
        {
            var (c, d) = frontier.Dequeue();
            if (d == pad) continue;

            foreach (var o in Neigh8)
            {
                var n = c + o;
                if (visited.Contains(n)) continue;
                visited.Add(n);

                // 실제 타일이 있는 셀만 추가(불필요한 채색 최소화)
                if (targetTilemap.HasTile(n))
                {
                    dst.Add(n);
                    frontier.Enqueue((n, d + 1));
                }
            }
        }
    }

    private void UpdateOverlayTiles() // 오버레이 타일맵 갱신
    {
        if (!wedgeCacheValid)
        {
            // 안전: 캐시가 없다면 전체 클리어(드문 케이스)
            overlayTilemap.ClearAllTiles();
            overlayActiveCells.Clear();
        }

        // 이번 프레임에 갱신한 셀 추적(나중에 범위 밖 잔여 오버레이 제거용)
        var updatedThisFrame = new HashSet<Vector3Int>();

        // 후보 셀을 기준으로 "보이는 셀=비우기", "나머지=검은 타일"
        foreach (var cell in wedgeCandidates)
        {
            if (visibleCells.Contains(cell))
            {
                // 보이게: 오버레이 제거
                overlayTilemap.SetTile(cell, null);
                overlayActiveCells.Remove(cell);
            }
            else
            {
                // 가리기: 검은 타일 칠하기
                overlayTilemap.SetTile(cell, overlayTile);
                overlayActiveCells.Add(cell);
            }

            updatedThisFrame.Add(cell);
        }

        // 후보 밖에 남아 있던 과거 오버레이 타일 청소(라이트가 움직여 후보 영역이 바뀐 경우)
        if (overlayActiveCells.Count > 0)
        {
            var toClear = ListCache;
            toClear.Clear();
            foreach (var c in overlayActiveCells)
            {
                if (!updatedThisFrame.Contains(c))
                    toClear.Add(c);
            }
            foreach (var c in toClear)
            {
                overlayTilemap.SetTile(c, null);
                overlayActiveCells.Remove(c);
            }
        }
    }

    // 리스트 캐시(할당 줄이기)
    private static readonly List<Vector3Int> ListCache = new();
    
private void OnDrawGizmosSelected() // 디버그 기즈모
    {
        if (!drawGizmos) return;

        // 1) 현재 파라미터 수집 --------------------------------------------
        Vector3 origin = Application.isPlaying ? GetLightOrigin() : transform.position; // 원점
        float centerYaw = Application.isPlaying ? GetLightYawDeg() : 0f;                // 방위각(도)

        // 🔸 URP 2D 'Spot'은 API상 Point 타입 + OuterAngle로 표현됨
        float radius = (sourceLight && useLightParams) ? sourceLight.pointLightOuterRadius : radiusOverride; // 반지름
        float outerHalf = (sourceLight && useLightParams /*&& sourceLight.lightType == Light2D.LightType.Point*/)
                        ? sourceLight.pointLightOuterAngle * 0.5f
                        : outerAngleDegOverride * 0.5f;

        // 2) 부채꼴 윤곽선 ---------------------------------------------------
        Gizmos.color = gizmoRayColor; // 윤곽선 색
        int seg = 32;                 // 윤곽선 샘플
        for (int i = 0; i < seg; i++)
        {
            float a0 = (centerYaw - outerHalf) + (outerHalf * 2f) * i / seg;
            float a1 = (centerYaw - outerHalf) + (outerHalf * 2f) * (i + 1) / seg;
            Vector3 p0 = origin + new Vector3(Mathf.Cos(a0 * Mathf.Deg2Rad), Mathf.Sin(a0 * Mathf.Deg2Rad), 0f) * radius;
            Vector3 p1 = origin + new Vector3(Mathf.Cos(a1 * Mathf.Deg2Rad), Mathf.Sin(a1 * Mathf.Deg2Rad), 0f) * radius;
            Gizmos.DrawLine(p0, p1);
        }

        // 3) 레이 라인(실제 쏜 방향) -----------------------------------------
        if (drawRayLines && targetTilemap) // 토글 + 타일맵 연결돼 있을 때만
        {
            Gizmos.color = gizmoRayLineColor; // 레이 라인 색

            float startAng = centerYaw - outerHalf;
            float endAng = centerYaw + outerHalf;
            float step = (endAng - startAng) / Mathf.Max(1, rayCount - 1);

            for (int i = 0; i < rayCount; i++)
            {
                float a = startAng + step * i;
                Vector2 dir = new Vector2(Mathf.Cos(a * Mathf.Deg2Rad), Mathf.Sin(a * Mathf.Deg2Rad)); // 레이 방향

                // CastRayDDA 재사용: 히트 셀이 있으면 그 위치까지, 없으면 반지름 끝까지 라인
                if (CastRayDDA(origin, dir.normalized, radius, out Vector3Int hitCell))
                {
                    Vector3 end = drawToHitCenter
                        ? targetTilemap.GetCellCenterWorld(hitCell)                         // 히트 셀 중심까지
                        : origin + (Vector3)dir.normalized * radius;                        // (옵션) 그냥 끝까지
                    Gizmos.DrawLine(origin, end);
                }
                else
                {
                    Vector3 end = origin + (Vector3)dir.normalized * radius;               // 히트 없음 → 반경 끝
                    Gizmos.DrawLine(origin, end);
                }
            }
        }

        // 4) 히트 셀(런타임 집합) --------------------------------------------
        if (Application.isPlaying && visibleCells != null && visibleCells.Count > 0)
        {
            Gizmos.color = gizmoHitColor; // 히트 셀 박스 색
            foreach (var c in visibleCells)
            {
                Vector3 center = targetTilemap.GetCellCenterWorld(c);
                Gizmos.DrawWireCube(center, new Vector3(cellSize.x, cellSize.y, 0.02f));
            }
        }
    }

}
