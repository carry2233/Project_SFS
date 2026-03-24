using System.Collections.Generic;        // HashSet
using UnityEngine;

[AddComponentMenu("Build/Build Mode Controller (Rewired)")]
public class BuildModeController : MonoBehaviour
{
    [Header("참조")]
    [SerializeField] private BuildCatalog catalog;                      // ✅ 카탈로그
    [SerializeField] private PreviewAndAssignManager previewAssign;     // ✅ 프리뷰+할당 UI
    [SerializeField] private BuildProgressAndPlacer progressPlacer;     // ✅ 게이지+설치
    [SerializeField] private BuildMenuUI buildMenu;                     // ✅ 메뉴(토글 가드)
    [SerializeField] private BuildRequirementService reqSvc;            // ✅ [추가] 자원 서비스
    [SerializeField] private Inventory inventory;                       // ✅ [추가] 인벤토리 참조
    [SerializeField] private BuildProgressAndPlacer buildProgressAndPlacer; // ★ 추가


[Header("입력")]
[SerializeField] private KeyCode cancelKey = KeyCode.Escape;    // 빌드 모드 취소 키
[SerializeField] private KeyCode rotateLeftKey = KeyCode.Q;     // 건물 회전(좌측) 키
[SerializeField] private KeyCode rotateRightKey = KeyCode.E;    // 건물 회전(우측) 키





    // 상태
    private static bool _isBuildModeActive;                              // ✅ 글로벌 플래그
    public static bool IsBuildModeActive => _isBuildModeActive;          // ✅ 조회용

    private BuildItemData currentItem;                                   // ✅ 현재 빌드 아이템
    private Camera _cam;                                                 // ✅ 카메라 캐시
    private BuildDirection currentDirection = BuildDirection.Up;    // 현재 건물 방향(상/하/좌/우)





    private void OnEnable()                                              // ✅ 구독
    {
        BuildMenuUI.OnEnterBuildMode += HandleEnterBuildMode;            // 메뉴 이벤트 수신
    }

    private void OnDisable()                                             // ✅ 해제
    {
        BuildMenuUI.OnEnterBuildMode -= HandleEnterBuildMode;
    }

    private void Awake()                                                 // ✅ 초기화
    {
        _cam = Camera.main;
        ExitBuildMode(); // 안전 상태에서 시작
    }

private void Update()                                           // 프레임마다 빌드 입력 처리
{
    if (!_isBuildModeActive) return;

    // ESC → 빌드 모드 종료
    if (Input.GetKeyDown(cancelKey))
    {
        ExitBuildMode();
        return;
    }

    // 회전 입력 처리(Q/E)
    if (Input.GetKeyDown(rotateLeftKey))                        // 왼쪽 회전
    {
        RotateCurrent(-1);
    }
    else if (Input.GetKeyDown(rotateRightKey))                  // 오른쪽 회전
    {
        RotateCurrent(+1);
    }

    // 프리뷰/자원 UI 갱신
    previewAssign?.Tick();

    // 게이지/설치 갱신
    progressPlacer?.Tick(currentItem);
}



    // ───────────── 이벤트 핸들러 ─────────────
private void HandleEnterBuildMode(int itemId)    // 빌드 모드 진입 이벤트 처리
{
    currentItem = FindItem(itemId);             // 선택된 빌드 아이템 찾기
    if (currentItem == null)
    {
        Debug.LogWarning($"[BuildMode] 카탈로그에 ID {itemId} 아이템이 없습니다.");
        return;
    }

    _isBuildModeActive = true;                  // 빌드 모드 활성 플래그 ON
    currentDirection = BuildDirection.Up;       // 기본 방향을 위로 초기화

    // buildingPrefab의 BuildingFootprint를 ProgressPlacer에 전달
    BuildingFootprint footprint = null;         // 구조물 차지범위 컴포넌트 참조
    if (currentItem.buildingPrefab)             // buildingPrefab이 할당되어 있으면
    {
        footprint = currentItem.buildingPrefab
            .GetComponentInChildren<BuildingFootprint>(); // 자식 포함 검색
    }
    progressPlacer?.SetStructureFootprint(footprint);     // 현재 아이템용 footprint 설정

    previewAssign?.Begin(currentItem, currentDirection);  // 프리뷰 시작
    progressPlacer?.ResetAll();                          // 게이지/진행 리셋
    progressPlacer?.SetDirection(currentDirection);       // 설치 방향 전달
    previewAssign?.SetDirection(currentDirection);        // 프리뷰 방향 전달

    // ★ 추가: 빌드 모드 진입 시 이미 건설된 칸들을 타일1(프리뷰 스타일)로 표시
    progressPlacer?.ApplyOccupancyStylePreview();

    if (buildMenu) buildMenu.CloseUI();                   // 빌드 메뉴 닫기
    Debug.Log($"[BuildMode] Enter: {currentItem.displayName} (ID:{itemId})");
}


public void ExitBuildMode()                               // 빌드 모드 종료 처리
{
    _isBuildModeActive = false;                           // 빌드 모드 비활성
    currentItem = null;                                   // 현재 아이템 해제

    previewAssign?.End();                                 // 프리뷰 종료
    progressPlacer?.ResetAll();                           // 게이지/슬라이더 리셋
    progressPlacer?.SetStructureFootprint(null);          // footprint 참조 초기화

    // ★ 추가: 빌드 모드 종료 시 차지된 칸들을 타일2(일반 스타일)로 되돌리기
    progressPlacer?.ApplyOccupancyStyleNormal();
}



private BuildItemData FindItem(int id)                          // ID로 BuildItemData 검색
{
    if (!catalog) return null;
    var cats = catalog.GetCategories();                         // 카테고리 목록 조회
    for (int i = 0; i < cats.Count; i++)
    {
        var cat = cats[i];
        if (cat == null) continue;

        for (int j = 0; j < cat.items.Count; j++)
        {
            var it = cat.items[j];
            if (it == null) continue;
            if (it.id == id) return it;                         // ID 일치 시 반환
        }
    }
    return null;                                                // 못 찾으면 null
}


private void RotateCurrent(int step)                            // 현재 방향을 90도 단위로 회전
{
    int dir = (int)currentDirection;
    dir += step;
    if (dir < 0) dir += 4;
    if (dir > 3) dir -= 4;
    currentDirection = (BuildDirection)dir;

    previewAssign?.SetDirection(currentDirection);              // 프리뷰 방향 전달
    progressPlacer?.SetDirection(currentDirection);             // 설치 방향 전달
}

public void SelectBuildItem(BuildItemData item) // 빌드 아이템 선택 요청 메서드
{
    if (item == null) return;                   // null 가드
    HandleEnterBuildMode(item.id);              // ✅ 기존 진입 로직 재사용(메서드/상태 일원화)
}






}
