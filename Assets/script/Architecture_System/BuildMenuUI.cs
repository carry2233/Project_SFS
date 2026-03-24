using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;                    // 버튼
using TMPro;                            // TMP

[AddComponentMenu("BuildUI/Build Menu UI (Integrated)")]
public class BuildMenuUI : MonoBehaviour
{
    [Header("토글/루트")]
    [SerializeField] private KeyCode toggleKey = KeyCode.B;    // ✅ 메뉴 토글 키
    [SerializeField] private CanvasGroup rootPanel;            // ✅ 루트 패널

    [Header("카테고리/아이템 패널")]
    [SerializeField] private Transform categoryPanel;          // ✅ 카테고리 버튼 부모
    [SerializeField] private Transform itemListPanel;          // ✅ 아이템 버튼 부모
    public Image categoryIconImage;                            // ✅ 카테고리 썸네일 표시용
    public Image itemIconImage;                                // ✅ 아이템 썸네일 표시용

    [Header("버튼 프리팹(단순 버튼)")]
    [SerializeField] private Button categoryButtonPrefab;      // ✅ 카테고리용 버튼 프리팹
    [SerializeField] private Button itemButtonPrefab;          // ✅ 아이템용 버튼 프리팹

    [Header("텍스트/데이터")]
    [SerializeField] private TMP_Text currentCategoryText; // 현재 카테고리 라벨
    [SerializeField] private BuildCatalog catalog;         // 빌드 카탈로그 ScriptableObject
    [SerializeField] private int defaultCategoryId = 0;    // 기본 카테고리 ID (0이면 자동 선택)

    [Header("빌드모드 상태 참조(토글 가드)")]
    [SerializeField] private BuildModeController buildMode;    // ✅ 빌드모드 컨트롤러

// ─────────────────────────────────────────────────────────
public static event Action<int> OnBuildItemSelected;   // 아이템 선택 이벤트 (int ID)
public static event Action<int> OnEnterBuildMode;      // 건설 모드 진입 이벤트 (int ID)
// ─────────────────────────────────────────────────────────

private int currentCategoryId = 0;                     // 현재 카테고리 ID (정수)
    private bool initialized = false;                          // ✅ 초기화 여부
    private readonly List<GameObject> _catPool = new();        // ✅ 카테고리 버튼 풀
    private readonly List<GameObject> _itemPool = new();       // ✅ 아이템 버튼 풀

    private void Start()                                       // ✅ 시작 시 초기화
    {
        EnsurePanelState(false);
        InitializeUI();
    }

    private void Update()                                      // ✅ 입력 체크
    {
        if (buildMode && BuildModeController.IsBuildModeActive) return; // 빌드중엔 토글X
        if (Input.GetKeyDown(toggleKey)) ToggleUI();
    }

    public void ToggleUI()                                     // ✅ 메뉴 토글
    {
        bool toOpen = rootPanel && rootPanel.alpha <= 0f;
        EnsurePanelState(toOpen);
    }

    public void OpenUI()  { EnsurePanelState(true); }          // ✅ 열기
    public void CloseUI() { EnsurePanelState(false); }         // ✅ 닫기

    private void EnsurePanelState(bool visible)                // ✅ 패널 상태 적용
    {
        if (!rootPanel) return;
        rootPanel.alpha = visible ? 1f : 0f;
        rootPanel.interactable = visible;
        rootPanel.blocksRaycasts = visible;
    }

public void InitializeUI() // 최초 구성 메서드
{
    if (initialized) return;
    initialized = true;

    RefreshCategories(); // 카테고리 버튼 생성

    if (defaultCategoryId == 0) // 기본 카테고리가 설정되지 않은 경우
    {
        var cats = catalog ? catalog.GetCategories() : null; // 카테고리 리스트 가져오기
        if (cats != null && cats.Count > 0)
            defaultCategoryId = cats[0].id; // 첫 번째 카테고리의 ID를 기본값으로 설정
    }

    if (defaultCategoryId != 0) // 기본 카테고리 ID가 유효한 경우
    {
        var cat = FindCategory(defaultCategoryId); // ID로 카테고리 찾기
        var name = cat != null ? cat.displayName : defaultCategoryId.ToString(); // 표시 이름 또는 ID 문자열
        OnCategorySelected(defaultCategoryId, name); // 카테고리 선택 처리
    }
    else
    {
        if (currentCategoryText) currentCategoryText.text = ""; // 카테고리 라벨 초기화
        ClearPool(_itemPool); // 아이템 버튼 풀 제거
    }
}


private void RefreshCategories() // 카테고리 버튼 빌드 메서드
{
    ClearPool(_catPool);
    if (!catalog || !categoryPanel || !categoryButtonPrefab) return;

    foreach (var cat in catalog.GetCategories())
    {
        if (cat == null) continue;
        var btn = Instantiate(categoryButtonPrefab, categoryPanel); // 카테고리 버튼 생성
        _catPool.Add(btn.gameObject);

        var txt = btn.GetComponentInChildren<TMP_Text>(true);       // 버튼 라벨 텍스트
        var img = FindChildByName<Image>(btn.transform, "Icon");    // 자식 중 "Icon" 이름의 이미지 찾기
        if (txt) txt.text = cat.displayName;
        if (img && cat.icon) { img.sprite = cat.icon; img.enabled = true; }

        int idCache = cat.id;               // 카테고리 ID 캐시 (정수)
        string nameCache = cat.displayName; // 카테고리 이름 캐시
        btn.onClick.AddListener(() => OnCategorySelected(idCache, nameCache)); // 클릭 시 카테고리 선택
    }
}

private void OnCategorySelected(int id, string name) // 카테고리 선택 처리 메서드
{
    currentCategoryId = id;                    // 현재 카테고리 ID 저장
    if (currentCategoryText) currentCategoryText.text = name; // 카테고리 라벨 갱신
    RefreshItemList(id);                       // 선택된 카테고리의 아이템 리스트 갱신
}

private void RefreshItemList(int categoryId) // 아이템 버튼 빌드 메서드
{
    ClearPool(_itemPool);
    if (!catalog || !itemListPanel || !itemButtonPrefab) return;

    foreach (var item in catalog.GetItemsByCategoryId(categoryId))
    {
        if (item == null) continue;
        var btn = Instantiate(itemButtonPrefab, itemListPanel); // 아이템 버튼 생성
        _itemPool.Add(btn.gameObject);

        var txt = btn.GetComponentInChildren<TMP_Text>(true);    // 버튼 라벨 텍스트
        var img = FindChildByName<Image>(btn.transform, "Icon"); // 자식 중 "Icon" 이름의 이미지 찾기
        if (txt) txt.text = item.displayName;
        if (img && item.icon) { img.sprite = item.icon; img.enabled = true; }

        int idCache = item.id; // 아이템 ID 캐시 (정수)
        btn.onClick.AddListener(() =>
        {
            OnBuildItemSelected?.Invoke(idCache); // 아이템 선택 이벤트 발행
            OnEnterBuildMode?.Invoke(idCache);    // 빌드 모드 진입 이벤트 발행
        });
    }
}


private BuildCategoryData FindCategory(int id) // ID로 카테고리 찾기 메서드
{
    if (!catalog) return null;
    foreach (var c in catalog.GetCategories())
        if (c != null && c.id == id) return c; // ID가 같은 카테고리 반환
    return null;
}

    private static void ClearPool(List<GameObject> pool)        // ✅ 버튼 풀 제거
    {
        for (int i = pool.Count - 1; i >= 0; i--)
            if (pool[i]) Destroy(pool[i]);
        pool.Clear();
    }

    public void ApplyCategorySelectionUI(BuildCategoryData category) // ✅ 선택 썸네일 반영
    {
        if (!categoryIconImage) return;
        var sprite = (category != null) ? category.icon : null;
        categoryIconImage.sprite = sprite;
        categoryIconImage.enabled = (sprite != null);
    }

    public void ApplyItemSelectionUI(BuildItemData item)        // ✅ 선택 썸네일 반영
    {
        if (!itemIconImage) return;
        var sprite = (item != null) ? item.icon : null;
        itemIconImage.sprite = sprite;
        itemIconImage.enabled = (sprite != null);
    }

    // ── 추가: 이름으로 자식 Transform을 찾아 컴포넌트를 가져오는 유틸 ──
    private static T FindChildByName<T>(Transform root, string childName) where T : Component // ✅ 이름검색 유틸
    {
        if (!root) return null;
        var t = root.Find(childName);                       // 1차: 직계 탐색
        if (t) return t.GetComponent<T>();
        foreach (Transform c in root)                       // 2차: 깊이 탐색
        {
            var r = FindChildByName<T>(c, childName);
            if (r) return r;
        }
        return null;
    }
}
