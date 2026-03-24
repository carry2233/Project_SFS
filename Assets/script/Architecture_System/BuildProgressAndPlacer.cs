using System;                                      // StringComparison
using System.Collections;                           // Coroutine
using System.Collections.Generic;                   // Dictionary, List, HashSet
using UnityEngine;                                  // Unity 기본
using UnityEngine.UI;                               // Slider
using UnityEngine.Tilemaps;                         // Tilemap
using Unity.AI.Navigation;                          // NavMeshSurface (Unity 6)

[AddComponentMenu("Build/Build Progress + Placer (Integrated)")]
public class BuildProgressAndPlacer : MonoBehaviour
{
    // ─────────────────────────────────────────────────────────
    // (A) 참조
    // ─────────────────────────────────────────────────────────
    [Header("참조")]
    [SerializeField] private PreviewAndAssignManager preview;         // ✅ 프리뷰/판정 제공자
    [SerializeField] private BuildRequirementService reqSvc;          // ✅ 자원/할당 서비스
    [SerializeField] private Inventory inventory;                     // ✅ 인벤토리(유지)
    [SerializeField] private Tilemap anchorTilemap;                   // ✅ 월드 좌표 기준용(없으면 Final/Blueprint 사용)

    // ─────────────────────────────────────────────────────────
    // (B) 게이지 UI
    // ─────────────────────────────────────────────────────────
    [Header("월드 게이지 UI")]
    [SerializeField] private RectTransform sliderRoot;                // ✅ 슬라이더 루트
    [SerializeField] private Slider progressSlider;                   // ✅ 진행 슬라이더
    [SerializeField] private Vector3 sliderOffset = new(0, 0.5f, 0);  // ✅ 게이지 오프셋

    // ─────────────────────────────────────────────────────────
    // (C) 입력
    // ─────────────────────────────────────────────────────────
    [Header("입력/취소")]
    [SerializeField] private KeyCode holdKey = KeyCode.Mouse0;        // ✅ 유지 키(좌클릭)
    [SerializeField, Min(0.05f)] private float cancelDistance = 0.75f;// ✅ 진행 중 흔들림 임계(거리)

    // ─────────────────────────────────────────────────────────
    // (D) 완료 효과 & 벽 규칙
    // ─────────────────────────────────────────────────────────
    [Header("완료 효과(선택)")]
    [SerializeField] private AudioSource sfxSource;                   // ✅ 설치 사운드 소스
    [SerializeField] private AudioClip placeClip;                     // ✅ 설치 사운드 클립
    [SerializeField] private MonoBehaviour wallAutoTilerRef;          // ✅ IWallAutoTiler 참조 원본
    private IWallAutoTiler wallAutoTiler;                             // ✅ 캐시된 자동 타일러

    // ─────────────────────────────────────────────────────────
    // (E) [추가] 공유 반영(기존 유지)
    // ─────────────────────────────────────────────────────────
    [Header("공유 반영")]
    [SerializeField] private Tilemap sharedTilemap;                   // ✅ Final과 동일 타일을 같이 칠할 타일맵(선택)

    // ─────────────────────────────────────────────────────────
    // (F) 베이크용 프리팹 스폰 & NavMesh 리베이크
    // ─────────────────────────────────────────────────────────
    [Header("베이크용 프리팹 스폰")]
    [SerializeField] private bool spawnPrefabOnFinalize = true;       // ✅ 최종 설치 시 프리팹 스폰 여부
    [SerializeField] private Transform poolRoot;                      // ✅ 스폰될 프리팹들의 부모(풀 루트)
    [SerializeField] private GameObject bakePrefab;                   // ✅ 베이크에 사용할 공용 프리팹
    [SerializeField] private Vector3 placedOffset = Vector3.zero;     // ✅ 셀 중심 대비 오프셋
    [SerializeField] private bool applyTileRotationZ = false;         // ✅ FinalTilemap의 Z회전 적용 여부

    [Header("NavMesh 리베이크")]
    [SerializeField] private List<NavMeshSurface> navSurfaces = new();// ✅ 리베이크 대상 Surface들
    [SerializeField, Min(0f)] private float rebakeDelay = 0.25f;      // ✅ 디바운스 지연

    private readonly Dictionary<Vector3Int, GameObject> placedByCell = new(); // ✅ 같은 셀 중복 방지
    private Coroutine rebakeCo;                                        // ✅ 디바운스 코루틴 캐시

    // ─────────────────────────────────────────────────────────
    // (G) 상태
    // ─────────────────────────────────────────────────────────
    private bool inProgress;                                           // ✅ 진행 중 여부
    private Vector3 startWorld;                                        // ✅ 진행 시작 월드 좌표
    private Vector3Int targetCell;                                     // ✅ 타겟 셀
    private float timer;                                               // ✅ 진행 타이머
    private Camera _cam;                                               // ✅ 카메라
    private BuildDirection currentDirection = BuildDirection.Up;       // 현재 설치 방향

    // ─────────────────────────────────────────────────────────
    // (H) 구조물 프리뷰(차지범위/구상도)
    // ─────────────────────────────────────────────────────────
    [Header("구조물 프리뷰(차지범위/구상도)")]
    [SerializeField] private Tilemap footprintTilemap;                 // 구조물 차지범위 표시용 타일맵
    [SerializeField] private TileBase footprintTile;                   // 일반 차지칸 표시용 타일
    [SerializeField] private TileBase footprintCenterTile;             // 중심 차지칸 표시용 타일
    [SerializeField] private TileBase footprintBlockedTile;            // 겹침 시 표시용 타일

    [SerializeField] private GameObject structurePreviewObject;        // 건물 구상도 오브젝트
    [SerializeField] private SpriteRenderer structurePreviewRenderer;  // 구상도용 SpriteRenderer

    private BuildingFootprint structureFootprint;                      // 구조물 차지범위 데이터
    private bool lastStructureBlocked;                                 // 최근 겹침 여부

    // ─────────────────────────────────────────────────────────
    // (I) 차지 유무 타일맵1 + 단계별 타일
    // ─────────────────────────────────────────────────────────
    [Header("차지 유무 타일맵1 (Occupancy Map)")]
    [SerializeField] private Tilemap occupancyTilemap;                 // 차지 유무 타일맵
    [SerializeField] private TileBase occupancyPreviewTile;            // 빌드 모드 중 표시 타일
    [SerializeField] private TileBase occupancyOccupiedTile;           // 일반 모드 표시 타일
    private readonly HashSet<Vector3Int> occupiedCells = new();        // 이미 건설된 셀 목록

    [Header("벽 프리팹 부모")]
    [SerializeField] private Transform wallPrefabParent; // ✅ 생성되는 벽 프리팹들을 한 곳에 모아두기(선택)

    private readonly Dictionary<Vector3Int, WallPrefabController> wallPrefabByCell = new(); // ✅ 벽 프리팹 추적용

    private void Awake()                                               // 초기화
    {
        _cam = Camera.main;                                            // 메인 카메라 캐시
        wallAutoTiler = wallAutoTilerRef as IWallAutoTiler;            // 자동 타일러 캐시
        ShowGauge(false);                                              // 게이지 숨기기
        SetGauge(0f);                                                  // 게이지 0

        if (structurePreviewObject)                                    // 구상도 오브젝트가 있으면
        {
            if (!structurePreviewRenderer)                             // 렌더러 자동 캐시
                structurePreviewRenderer = structurePreviewObject.GetComponentInChildren<SpriteRenderer>();

            if (!structureFootprint)                                   // 외부에서 설정 안 됐을 때만
                structureFootprint = structurePreviewObject.GetComponent<BuildingFootprint>();

            structurePreviewObject.SetActive(false);                   // 시작 비활성
        }

        ClearFootprintPreview();                                       // 차지범위 타일맵 초기화
    }

    public void SetStructureFootprint(BuildingFootprint footprint)     // 구조물 차지범위 설정
    {
        structureFootprint = footprint;                                // footprint 교체
    }

    public void SetDirection(BuildDirection dir)                       // 현재 설치 방향 설정
    {
        currentDirection = dir;                                        // 방향 저장
    }

    public void ResetAll()                                             // 강제 리셋
    {
        inProgress = false;                                            // 진행 종료
        timer = 0f;                                                    // 타이머 리셋
        ShowGauge(false);                                              // 게이지 숨김
        SetGauge(0f);                                                  // 값 0

        ClearFootprintPreview();                                       // 차지범위 지우기
        if (structurePreviewObject) structurePreviewObject.SetActive(false); // 구상도 숨기기

        lastStructureBlocked = false;                                  // 겹침 상태 리셋
        // occupancyTilemap은 "이미 건설된 칸" 정보라 유지
    }

    public void Tick(BuildItemData item)                               // 매 프레임 게이지/설치 처리
    {
        if (_cam == null || item == null || preview == null) return;   // 가드

        var world = _cam.ScreenToWorldPoint(Input.mousePosition);      // 마우스 월드
        world.z = 0f;                                                  // 2D 고정
        var final = preview.GetFinalTilemap();                         // Final 타일맵
        var cell = final ? final.WorldToCell(world) : Vector3Int.zero; // 셀 좌표

        bool hasBlueprintHere = preview.HasBlueprintAt(cell);          // 구상도 존재?
        bool hasEnoughResources = (reqSvc != null && inventory != null) && reqSvc.HasResources(inventory, item); // 재료 충분?

        bool structureBlocked = false;                                 // 구조물 겹침 여부
        if (item.kind == BuildKind.Structure)                          // 구조물인 경우
        {
            structureBlocked = !UpdateStructurePreviewAndCheck(item, cell); // 프리뷰+겹침 검사
        }
        else
        {
            if (structurePreviewObject) structurePreviewObject.SetActive(false); // 구상도 숨기기
            ClearFootprintPreview();                                   // 차지범위 지우기
            lastStructureBlocked = false;                               // 상태 리셋
        }

        bool cellOccupied = false;                                     // 벽/타일용 차지 여부
        if (item.kind != BuildKind.Structure)                          // 벽/타일이면
        {
            cellOccupied = IsCellOccupiedForPlacement(cell);           // 점유 검사
        }

        bool canBuild;                                                 // 설치 가능 여부
        if (item.kind == BuildKind.Structure)                          // 구조물
        {
            canBuild = preview.IsPlaceableAtWorld(world) && hasEnoughResources && !structureBlocked; // 조건
        }
        else                                                           // 벽/타일
        {
            canBuild = hasBlueprintHere && preview.IsPlaceableAtWorld(world) && hasEnoughResources && !cellOccupied; // 조건
        }

        if (!inProgress)                                               // 진행 시작 전
        {
            if (!canBuild) return;                                     // 불가면 종료

            if (Input.GetKey(holdKey))                                 // 홀드 시작
            {
                inProgress = true;                                     // 진행 ON
                targetCell = cell;                                     // 타겟 셀 고정
                startWorld = world;                                    // 시작 위치
                timer = 0f;                                            // 타이머 0
                ShowGauge(true);                                       // 게이지 표시
                UpdateGaugePosition(targetCell);                       // 위치 갱신
                SetGauge(0f);                                          // 값 0
            }
            return;                                                    // 종료
        }

        if (!Input.GetKey(holdKey)) { Cancel(); return; }              // 키 뗌 취소
        if (cell != targetCell) { Cancel(); return; }                  // 셀 이동 취소

        if (item.kind != BuildKind.Structure && !hasBlueprintHere)     // 벽/타일은 구상도 없으면 취소
        {
            Cancel(); return;                                          // 취소
        }

        if (!canBuild) { Cancel(); return; }                           // 조건 깨지면 취소
        if ((world - startWorld).sqrMagnitude > cancelDistance * cancelDistance) { Cancel(); return; } // 흔들림 취소

        timer += Time.deltaTime;                                       // 시간 누적
        float t = Mathf.Clamp01(timer / Mathf.Max(0.05f, item.holdTime)); // 진행 비율
        SetGauge(t);                                                   // 게이지 값
        UpdateGaugePosition(targetCell);                               // 게이지 위치

        if (t >= 1f)                                                   // 완료
        {
            inProgress = false;                                        // 진행 OFF
            ShowGauge(false);                                          // 게이지 숨김
            TryPlaceFinal(item, targetCell);                           // 최종 설치
        }
    }

    private void TryPlaceFinal(BuildItemData item, Vector3Int cell)    // 최종 설치 시도
    {
        if (item == null) return;                                      // 가드

        if (reqSvc != null && inventory != null)                       // 자원 소비
        {
            if (!reqSvc.TryConsume(inventory, item)) return;           // 부족하면 중단
        }

        if (item.kind == BuildKind.Structure)                          // 구조물
        {
            PlaceStructure(item, cell);                                // 구조물 설치
        }
        else
        {
            PlaceWall(item, cell);                                     // 벽/타일 설치
        }

        reqSvc?.RaiseAssignChanged();                                  // UI 갱신 이벤트
    }

private void PlaceWall(BuildItemData item, Vector3Int cell)        // ✅ 벽/타일 설치(프리팹 벽 방식)
{
    var final = preview.GetFinalTilemap();                         // Final 타일맵
    if (!final || !item.previewTile) return;                       // 가드

    Vector3Int finalCell = cell;                                   // 셀 확정

    final.SetTile(finalCell, item.previewTile);                    // 타일 배치
    final.RefreshTile(finalCell);                                  // 리프레시

    if (!final.HasTile(finalCell))                                 // 배치 실패 안전망
    {
        Debug.LogWarning($"[BuildProgress] Tile placement failed at {finalCell}");
        return;
    }

    if (sharedTilemap)                                             // 공유 타일맵도 칠할 경우(기존 유지)
    {
        sharedTilemap.SetTile(finalCell, item.previewTile);        // 공유 타일 배치
        sharedTilemap.RefreshTile(finalCell);                      // 리프레시
    }

    if (occupancyTilemap)                                          // 점유 기록(기존 유지)
    {
        occupiedCells.Add(finalCell);                              // 점유 셀 추가
        ApplyOccupancyStyleToCell(finalCell);                      // 현재 모드 스타일 적용
    }

    var bp = preview.GetBlueprintTilemap();                        // 블루프린트 타일맵
    if (bp) bp.SetTile(finalCell, null);                           // 프리뷰 제거

    // ✅ 핵심 변경: WallHealthManager 등록 제거 → 벽 프리팹 생성으로 대체
    TrySpawnWallPrefab(item, final, finalCell);                    // ✅ 벽 프리팹 생성/초기화(파괴 가능 벽일 때만 내부에서 처리)

    wallAutoTiler?.RefreshAround(finalCell);                       // 오토타일 갱신(기존 유지)

    if (spawnPrefabOnFinalize) RequestRebake();                    // 네비 리베이크(기존 유지)
    if (sfxSource && placeClip) sfxSource.PlayOneShot(placeClip);  // 사운드(기존 유지)
}



    private void PlaceStructure(BuildItemData item, Vector3Int cell)   // 구조물 설치 처리
    {
        Vector3 worldPos;                                              // 설치 위치
        var final = preview.GetFinalTilemap();                         // Final
        if (final) worldPos = final.GetCellCenterWorld(cell) + placedOffset;      // Final 기준
        else if (anchorTilemap) worldPos = anchorTilemap.GetCellCenterWorld(cell) + placedOffset; // Anchor 기준
        else worldPos = (Vector3)cell + placedOffset;                  // fallback

        var bp = preview.GetBlueprintTilemap();                        // 블루프린트
        if (bp) bp.SetTile(cell, null);                                // 프리뷰 제거

        if (item.buildingPrefab != null)                               // 프리팹 설치
        {
            var go = Instantiate(item.buildingPrefab, worldPos, Quaternion.identity); // 생성
            var placed = go.GetComponent<PlacedBuildingController>();   // 방향 컨트롤러
            if (placed != null) placed.Initialize(currentDirection);   // 방향 적용
            else go.transform.rotation = Quaternion.Euler(0f, 0f, DirectionToAngle(currentDirection)); // 회전만 적용
        }

        if (occupancyTilemap && structureFootprint != null)            // 구조물 점유 처리
        {
            foreach (var c in structureFootprint.GetCells(cell, currentDirection))
            {
                occupiedCells.Add(c);                                  // 점유 목록 추가
                ApplyOccupancyStyleToCell(c);                          // 스타일 적용
            }
        }

        if (sfxSource && placeClip) sfxSource.PlayOneShot(placeClip);  // 사운드
    }

    private float DirectionToAngle(BuildDirection dir)                 // 방향 → Z회전 각도
    {
        switch (dir)
        {
            default:
            case BuildDirection.Up: return 0f;                         // 위
            case BuildDirection.Right: return -90f;                    // 오른쪽
            case BuildDirection.Down: return 180f;                     // 아래
            case BuildDirection.Left: return 90f;                      // 왼쪽
        }
    }

    private void ShowGauge(bool v)                                     // 게이지 표시/숨김
    {
        if (sliderRoot) sliderRoot.gameObject.SetActive(v);            // 활성 토글
    }

    private void SetGauge(float p)                                     // 게이지 값 설정
    {
        if (progressSlider) progressSlider.value = p;                  // Slider 값
    }

    private void UpdateGaugePosition(Vector3Int cell)                  // 게이지 위치 갱신
    {
        if (!sliderRoot) return;                                       // 가드

        Vector3 basePos;                                               // 기준 위치
        var final = preview.GetFinalTilemap();                         // Final
        var bp = preview.GetBlueprintTilemap();                        // Blueprint

        if (anchorTilemap) basePos = anchorTilemap.GetCellCenterWorld(cell);      // Anchor 우선
        else if (bp) basePos = bp.GetCellCenterWorld(cell);            // Blueprint
        else if (final) basePos = final.GetCellCenterWorld(cell);      // Final
        else basePos = (Vector3)cell;                                  // fallback

        sliderRoot.position = basePos + sliderOffset;                  // 위치 적용
    }

    private void Cancel()                                              // 진행 취소
    {
        inProgress = false;                                            // 진행 OFF
        ShowGauge(false);                                              // 숨김
        SetGauge(0f);                                                  // 0
    }

    private void ClearFootprintPreview()                               // 차지범위 타일 지우기
    {
        if (footprintTilemap) footprintTilemap.ClearAllTiles();        // 전체 클리어
    }

    private bool UpdateStructurePreviewAndCheck(BuildItemData item, Vector3Int centerCell) // 구조물 프리뷰/겹침 검사
    {
        if (item == null) return false;                                // 가드

        var final = preview.GetFinalTilemap();                         // Final
        var bp = preview.GetBlueprintTilemap();                        // Blueprint

        Vector3 basePos;                                               // 기준 위치
        if (anchorTilemap) basePos = anchorTilemap.GetCellCenterWorld(centerCell);
        else if (bp) basePos = bp.GetCellCenterWorld(centerCell);
        else if (final) basePos = final.GetCellCenterWorld(centerCell);
        else basePos = (Vector3)centerCell;

        if (structurePreviewObject)                                    // 구상도 표시
        {
            structurePreviewObject.SetActive(true);                    // 활성
            structurePreviewObject.transform.position = basePos;       // 위치
            structurePreviewObject.transform.rotation = Quaternion.Euler(0f, 0f, DirectionToAngle(currentDirection)); // 회전
            ApplyStructurePreviewSprite(item);                         // 스프라이트 적용
        }

        ClearFootprintPreview();                                       // 타일 지우기

        if (!structureFootprint || !footprintTilemap)                  // footprint 없으면 설치 가능으로 처리
        {
            lastStructureBlocked = false;                              // 캐시
            return true;                                               // 가능
        }

        bool blocked = false;                                          // 겹침 여부
        var finalMap = preview.GetFinalTilemap();                      // Final

        foreach (var cell in structureFootprint.GetCells(centerCell, currentDirection))
        {
            bool hasFinal = finalMap && finalMap.HasTile(cell);        // Final에 타일?
            bool hasOccupancy = occupiedCells.Contains(cell);          // 점유 셀?
            bool isBlockedCell = hasFinal || hasOccupancy;             // 하나라도 true면 겹침

            if (isBlockedCell)
            {
                if (footprintBlockedTile) footprintTilemap.SetTile(cell, footprintBlockedTile);
                blocked = true;
            }
            else
            {
                if (cell == centerCell && footprintCenterTile) footprintTilemap.SetTile(cell, footprintCenterTile);
                else if (footprintTile) footprintTilemap.SetTile(cell, footprintTile);
            }
        }

        lastStructureBlocked = blocked;                                // 캐시
        return !blocked;                                               // 설치 가능 여부
    }

    private void ApplyStructurePreviewSprite(BuildItemData item)        // 구조물 구상도 스프라이트 적용
    {
        if (!structurePreviewRenderer || item == null) return;          // 가드

        Sprite s = null;                                               // 적용 스프라이트
        switch (currentDirection)
        {
            default:
            case BuildDirection.Up: s = item.previewSpriteUp; break;
            case BuildDirection.Right: s = item.previewSpriteRight; break;
            case BuildDirection.Down: s = item.previewSpriteDown; break;
            case BuildDirection.Left: s = item.previewSpriteLeft; break;
        }

        structurePreviewRenderer.sprite = s;                           // 적용
    }

    private bool IsCellOccupiedForPlacement(Vector3Int cell)            // 설치 시 셀 차지 여부
    {
        if (occupiedCells.Contains(cell)) return true;                 // 점유 목록 기준
        var final = preview.GetFinalTilemap();                          // Final
        if (final && final.HasTile(cell)) return true;                 // 안전망
        return false;                                                  // 비어있음
    }

    public void ApplyOccupancyStylePreview()                            // 빌드 모드 스타일(타일1)
    {
        if (!occupancyTilemap || !occupancyPreviewTile) return;        // 가드
        foreach (var cell in occupiedCells) occupancyTilemap.SetTile(cell, occupancyPreviewTile); // 적용
    }

    public void ApplyOccupancyStyleNormal()                             // 일반 모드 스타일(타일2)
    {
        if (!occupancyTilemap || !occupancyOccupiedTile) return;       // 가드
        foreach (var cell in occupiedCells) occupancyTilemap.SetTile(cell, occupancyOccupiedTile); // 적용
    }

    private void ApplyOccupancyStyleToCell(Vector3Int cell)             // 단일 셀에 현재 모드 타일 적용
    {
        if (!occupancyTilemap) return;                                  // 가드

        if (BuildModeController.IsBuildModeActive)                      // 빌드 모드면
        {
            if (occupancyPreviewTile) occupancyTilemap.SetTile(cell, occupancyPreviewTile);
        }
        else                                                            // 일반 모드면
        {
            if (occupancyOccupiedTile) occupancyTilemap.SetTile(cell, occupancyOccupiedTile);
        }
    }

    private void RequestRebake()                                        // 리베이크 요청(디바운스)
    {
        if (rebakeCo != null) StopCoroutine(rebakeCo);                  // 기존 취소
        rebakeCo = StartCoroutine(CoRebake());                          // 시작
    }

    private IEnumerator CoRebake()                                      // 지연 후 일괄 빌드
    {
        float wait = Mathf.Max(0f, rebakeDelay);                        // 대기 시간
        if (wait > 0f) yield return new WaitForSeconds(wait);           // 대기

        for (int i = 0; i < navSurfaces.Count; i++)                     // 리스트 순회
        {
            var s = navSurfaces[i];                                     // surface
            if (s) s.BuildNavMesh();                                    // 빌드
        }

        rebakeCo = null;                                                // 핸들 해제
    }

    private void TrySpawnWallPrefab(BuildItemData item, Tilemap final, Vector3Int finalCell) // ✅ 벽 프리팹 생성
{
    if (item == null) return;                                           // 가드
    if (!item.isDestructibleWall) return;                               // 파괴 가능 벽만 대상
    if (item.wallPrefab == null) return;                                // 프리팹 미지정이면 생성 안 함

    if (wallPrefabByCell.TryGetValue(finalCell, out var existing) && existing != null) // ✅ 중복 방지
        return;

    Vector3 worldPos = final ? final.GetCellCenterWorld(finalCell) : (Vector3)finalCell; // ✅ 생성 위치(셀 중앙)

    Transform parent = wallPrefabParent ? wallPrefabParent : null;      // ✅ 부모(선택)
    GameObject go = Instantiate(item.wallPrefab, worldPos, Quaternion.identity, parent); // ✅ 프리팹 생성

    WallPrefabController ctrl = go.GetComponent<WallPrefabController>(); // ✅ 컨트롤러 탐색(루트)
    if (ctrl == null) ctrl = go.GetComponentInChildren<WallPrefabController>(); // ✅ 자식에도 있으면 허용

    int typeId = item.wallTypeId;                                       // ✅ 벽 타입ID
    int itemId = (item.wallItemId != 0) ? item.wallItemId : item.id;    // ✅ 벽 아이템ID(0이면 build id)

NavMeshSurface surfaceForWall = null;                                // ✅ 벽 파괴 시 리베이크 대상(선택)
if (spawnPrefabOnFinalize && navSurfaces != null && navSurfaces.Count > 0) // ✅ 리스트가 있으면
{
    surfaceForWall = navSurfaces[0];                                 // ✅ 첫 번째 Surface 사용(간단 버전)
}

ctrl.Initialize(                                                     // 벽 프리팹 초기화
    owner: this,                                                     // 빌더
    cell: finalCell,                                                 // 셀 좌표
    mainWallTilemap: final,                                          // Final 타일맵
    sharedWallTilemap: sharedTilemap,                                // 공유 타일맵
    wallAutoTiler: wallAutoTiler,                                    // 오토타일러
    navSurface: surfaceForWall,                                      // ✅ 존재하는 값으로 전달
    typeId: typeId,                                                  // 타입ID
    itemId: itemId,                                                  // 아이템ID
    maxHealth: item.wallMaxHealth,                                   // 체력
    stoppingPower: item.wallStoppingPower,                           // 저지력
    defenseRate: item.wallDefenseRate,                               // 방어율
    absoluteDefense: item.wallAbsoluteDefense                        // 절대 방어
);


    wallPrefabByCell[finalCell] = ctrl;                                 // ✅ 매핑 저장(중복 방지/정리)
}

public void NotifyWallPrefabDestroyed(Vector3Int cell)                  // ✅ 벽 파괴 알림(점유 해제)
{
    wallPrefabByCell.Remove(cell);                                      // ✅ 추적 제거

    if (occupiedCells.Remove(cell))                                     // ✅ 점유 해제
    {
        if (occupancyTilemap)                                           // ✅ 점유 타일도 제거
        {
            occupancyTilemap.SetTile(cell, null);                       // 점유 표시 제거
            occupancyTilemap.RefreshTile(cell);                         // 리프레시
        }
    }
}


}