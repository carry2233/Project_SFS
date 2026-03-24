using UnityEngine;
using UnityEngine.Tilemaps;
using TMPro;                           // 텍스트
using UnityEngine.UI;                  // 이미지

[AddComponentMenu("Build/Preview + Assign UI (Integrated)")]
public class PreviewAndAssignManager : MonoBehaviour
{
    [Header("타일맵")]
    [SerializeField] private Tilemap blueprintTilemap;           // ✅ 구상도 타일맵
    [SerializeField] private Tilemap finalTilemap;               // ✅ 최종 타일맵

    [Header("프리뷰 색상")]
    [SerializeField] private Color placeableColor = new(1, 1, 1, 0.75f);      // ✅ 설치 가능 색
    [SerializeField] private Color blockedColor = new(1, 0.4f, 0.4f, 0.75f);  // ✅ 설치 불가 색

    [Header("설치 가능 판정(부채꼴/시야)")]
    [SerializeField] private Transform origin;                   // ✅ 부채꼴 원점(회전/위치)
    [SerializeField] private float radius = 4f;                  // ✅ 반경
    [SerializeField, Range(0, 180)] private float halfAngleDeg = 35f; // ✅ 반시야각
    [SerializeField] private Vector2 forwardAxis = Vector2.right;// ✅ 전방 기준(지역축)
    [SerializeField] private LayerMask losMask;                  // ✅ 시야 레이 마스크

    [Header("자원 할당 UI(월드 캔버스)")]
    public Canvas worldCanvas;                 // ✅ 월드 캔버스(표시 켜고 끄기용)
    public Transform assignListPanel;          // ✅ 리스트 패널(Vertical Layout Group)
    public GameObject assignItemEntryPrefab;   // ✅ 항목 프리팹(자식: "Icon"(Image), "Name"(TMP))
    public BuildRequirementService requirementService; // ✅ 요구/할당 데이터 소스
    [SerializeField] private Vector3 worldOffset = new(0, 0.7f, 0);// ✅ 오프셋

    [Header("표시 데이터/인벤토리")]
    [SerializeField] private WallItemTypeCatalog typeCatalog;    // ✅ 타입→표시정보
    [SerializeField] private Inventory inventory;                // ✅ 인벤토리 참조
    [SerializeField] private BuildRequirementService reqSvc;     // ✅ 자원 로직(보유/소비)

    [Header("재료 텍스트 색상")]
    [SerializeField] private Color enoughColor = Color.green;    // 필요 수량 이상일 때 텍스트 색
    [SerializeField] private Color defaultColor = Color.white;   // 기본 텍스트 색

    // 상태
    private BuildItemData currentItem;                           // ✅ 현재 건물 데이터
    private TileBase previewTile;                                // ✅ 프리뷰 타일
    private Camera _cam;                                         // ✅ 카메라 캐시
    private bool isStructure;                                    // 현재 아이템이 구조물인지 여부
    private BuildDirection currentDirection = BuildDirection.Up; // 현재 프리뷰 방향(상/하/좌/우)

    private void Awake()                                         // ✅ 초기화
    {
        _cam = Camera.main;
        if (!origin) origin = transform;
        ShowAssignUI(false);
    }

public void Begin(BuildItemData item)                        // 프리뷰 시작(기본 Up 방향)
{
    Begin(item, BuildDirection.Up);
}

public void Begin(BuildItemData item, BuildDirection dir)    // 프리뷰 시작(방향 포함)
{
    currentItem = item;
    previewTile = (item != null) ? item.previewTile : null;
    currentDirection = dir;
    isStructure = (item != null && item.kind == BuildKind.Structure); // 구조물 여부 판정

    ClearBlueprint();
    ShowAssignUI(false);
}

public void End()                                            // 프리뷰 종료
{
    currentItem = null;
    previewTile = null;
    isStructure = false;
    ClearBlueprint();
    ShowAssignUI(false);
}

public void SetDirection(BuildDirection dir)                 // 외부에서 프리뷰 방향 설정
{
    currentDirection = dir;
    // (향후 BuildingFootprint/스프라이트 회전 로직에서 currentDirection 사용 예정)
}

public void Tick()                                           // 매 프레임 프리뷰/자원 UI 갱신
{
    if (currentItem == null || blueprintTilemap == null || _cam == null) return;

    // 마우스 위치/셀
    var world = _cam.ScreenToWorldPoint(Input.mousePosition); world.z = 0f;
    var cell = blueprintTilemap.WorldToCell(world);

    bool placeable = IsPointInSector(world) && IsLineOfSightClear(origin.position, world); // 부채꼴+시야
    bool hasBlueprintHere = blueprintTilemap.HasTile(cell);                                // 현재 셀에 구상도 존재 여부

    // ───────── 구조물 모드일 때: 항상 마우스 기준 1셀 프리뷰 ─────────
    if (isStructure)
    {
        ClearBlueprint();                                           // 구조물은 1셀만 사용(이후 footprint에서 확장)

        if (placeable && (!finalTilemap || !finalTilemap.HasTile(cell)))
        {
            if (previewTile) blueprintTilemap.SetTile(cell, previewTile); // 구상도 타일 칠하기
            SetTileColor(cell, placeable ? placeableColor : blockedColor);
            hasBlueprintHere = true;
        }
        else
        {
            hasBlueprintHere = false;
        }
    }
    // ───────── 벽 모드일 때: 기존처럼 드래그로 여러 셀 칠하기 ─────────
    else
    {
        // 좌클릭 드래그로 구상도 칠하기
        if (Input.GetMouseButton(0))
        {
            if (placeable && !hasBlueprintHere && (!finalTilemap || !finalTilemap.HasTile(cell)))
            {
                if (previewTile) blueprintTilemap.SetTile(cell, previewTile); // 구상도 타일 칠하기
                SetTileColor(cell, placeable ? placeableColor : blockedColor);
                hasBlueprintHere = true;
            }
        }
    }

    // ───────── 자원 UI 갱신 ─────────
    if (hasBlueprintHere)
    {
        RefreshAssignedList(cell);                                  // 현재 셀 기준 재료 목록/색 갱신

        if (worldCanvas)
            worldCanvas.transform.position =
                blueprintTilemap.GetCellCenterWorld(cell) + worldOffset; // UI 위치 이동

        SetAssignVisible(true);                                     // 자원 UI 표시
    }
    else
    {
        SetAssignVisible(false);                                    // 자원 UI 숨김
    }
}



    // ───────────── 외부 쿼리 ─────────────
    public bool IsPlaceableAtWorld(Vector3 worldPos)             // ✅ 설치 가능 판정(월드)
    {
        return IsPointInSector(worldPos) && IsLineOfSightClear(origin.position, worldPos);
    }

    public bool HasBlueprintAt(Vector3Int cell)                  // ✅ 해당 셀에 구상도 존재?
    {
        return blueprintTilemap && blueprintTilemap.HasTile(cell);
    }

    public Tilemap GetBlueprintTilemap() => blueprintTilemap;    // ✅ 구상도 타일맵 반환
    public Tilemap GetFinalTilemap() => finalTilemap;            // ✅ 최종 타일맵 반환

    // ───────────── 내부: UI/판정 유틸 ─────────────

    private void ShowAssignUI(bool v)                               // ✅ 할당 UI 표시/숨김
    {
        if (worldCanvas) worldCanvas.enabled = v;
    }

    private void ClearBlueprint()                                   // ✅ 구상도 비우기
    {
        if (blueprintTilemap) blueprintTilemap.ClearAllTiles();
    }

    private void SetTileColor(Vector3Int cell, Color c)             // ✅ 타일 컬러 설정
    {
        if (!blueprintTilemap) return;
        blueprintTilemap.SetTileFlags(cell, TileFlags.None);
        blueprintTilemap.SetColor(cell, c);
    }

    private bool IsPointInSector(Vector3 world)                     // ✅ 부채꼴 판정
    {
        Vector2 o = origin ? (Vector2)origin.position : Vector2.zero;
        Vector2 to = (Vector2)world - o;
        if (to.magnitude > radius) return false;

        Vector2 fwd = (Vector2)(origin.rotation * new Vector3(forwardAxis.x, forwardAxis.y, 0f));
        if (fwd.sqrMagnitude < 1e-4f) fwd = Vector2.right;
        fwd.Normalize();

        float cos = Vector2.Dot(fwd, to.normalized);
        return cos >= Mathf.Cos(halfAngleDeg * Mathf.Deg2Rad);
    }

    private bool IsLineOfSightClear(Vector3 from, Vector3 to)       // ✅ 시야(레이) 판정
    {
        var dir = (to - from);
        float dist = dir.magnitude;
        if (dist <= 0.001f) return true;
        dir /= dist;

        var hit = Physics2D.Raycast(from, dir, dist - 0.01f, losMask);
        return !hit.collider; // 막는 콜라이더가 없을 때 클리어
    }

    private void OnEnable() // ✅ 이벤트 구독
    {
        if (requirementService != null)
            requirementService.OnAssignChanged += RefreshAssignedList_NoCell; // (더미) 트리거용
        // 초기 표시 필요 시 무효 셀로 리빌드 시도
        RefreshAssignedList_NoCell();
    }

    private void OnDisable() // ✅ 이벤트 해제
    {
        if (requirementService != null)
            requirementService.OnAssignChanged -= RefreshAssignedList_NoCell;
    }

    public void SetAssignVisible(bool on) // ✅ 월드 캔버스 표시/숨김
    {
        if (worldCanvas) worldCanvas.enabled = on;
    }

    // ───────────── [추가] 셀 기준 리스트 리빌드 ─────────────
public void RefreshAssignedList(Vector3Int cell) // 현재 호버 셀 기준으로 자원 목록 갱신
{
    if (!assignListPanel || !assignItemEntryPrefab || currentItem == null) return;
    ClearAssignList();

    if (currentItem.requirements == null || currentItem.requirements.Length == 0) return;

    foreach (var need in currentItem.requirements)
    {
        var go = Instantiate(assignItemEntryPrefab, assignListPanel);           // 항목 프리팹 생성
        var icon = FindChildByName<Image>(go.transform, "Icon");                // 아이콘 찾기
        var name = FindChildByName<TextMeshProUGUI>(go.transform, "Name");      // 텍스트 찾기

        string display = $"({need.typeId}:{need.itemId})";                      // 기본 표시 문자열
        Sprite s = null;

        if (typeCatalog != null)
        {
            var entry = typeCatalog.Find(need.typeId, need.itemId);             // 타입+아이템으로 찾기
            if (entry != null)
            {
                display = entry.displayName;                                    // 카탈로그 이름 사용
                s = entry.icon;                                                 // 카탈로그 아이콘 사용
            }
        }

        int owned = 0;                                                          // 현재 보유 개수
        if (reqSvc != null && inventory != null)
        {
            owned = reqSvc.GetOwnedCount(inventory, need.typeId, need.itemId); // 인벤 기준 보유 수 조회
        }

        if (icon != null)
        {
            icon.sprite = s;
            icon.enabled = (s != null);                                        // 아이콘이 있을 때만 표시
        }

        if (name != null)
        {
            name.text = $"{display} {owned}/{need.count}";                     // "이름  보유/필요"
            name.color = (owned >= need.count) ? enoughColor : defaultColor;   // 충족 여부에 따른 색상
        }
    }
}



    private void RefreshAssignedList_NoCell() // ✅ 이벤트 트리거용(셀 정보 없을 때는 초기화만)
    {
        if (!assignListPanel) return;
        ClearAssignList();
    }

    private void ClearAssignList() // ✅ 자식 오브젝트 모두 제거
    {
        if (!assignListPanel) return;
        for (int i = assignListPanel.childCount - 1; i >= 0; i--)
            Destroy(assignListPanel.GetChild(i).gameObject);
    }

    private static T FindChildByName<T>(Transform root, string childName) where T : Component // ✅ 이름검색 유틸
    {
        if (!root) return null;
        var t = root.Find(childName);                                  // 1차: 직계 탐색
        if (t) return t.GetComponent<T>();

        foreach (Transform c in root)                                  // 2차: 깊이 탐색
        {
            var r = FindChildByName<T>(c, childName);
            if (r) return r;
        }
        return null;
    }
}
