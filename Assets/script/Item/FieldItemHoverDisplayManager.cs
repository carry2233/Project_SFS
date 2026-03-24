using TMPro;                                    // TMP 사용
using UnityEngine;                              // 유니티 엔진
using UnityEngine.UI;                           // UI.Image

[AddComponentMenu("Item Runtime/Field Item Hover Display Manager (부채꼴+원점회전+레이캐스트)")]
public class FieldItemHoverDisplayManager : MonoBehaviour
{
    [Header("부채꼴 설정")]
    public Transform origin;                     // 부채꼴 원점(회전/위치 기준)          // 원점
    public float radius = 3.5f;                  // 반경(레이 길이)                      // 반경
    [Range(0f, 180f)]
    public float halfAngleDeg = 35f;             // 반시야각                              // 각도
    public Vector2 forwardAxis = Vector2.right;  // 전방 기준(지역 벡터, 원점 회전 반영)   // 전방축

    [Header("레이어/입력")]
    public LayerMask itemLayer;                  // 아이템 레이어                         // 아이템 레이어
    public bool requireLineOfSight = true;       // 원점→대상 레이 첫 히트 필수 여부       // 시야 체크
    public bool lockToTarget = true;             // UI를 타겟 위치에 고정                 // 위치 고정

    [Header("월드 캔버스(UI)")]
    public Canvas worldCanvas;                   // World Space 캔버스(필수)             // 월드 캔버스
    public Image iconImage;                      // 아이콘 이미지                         // 아이콘
    public TextMeshProUGUI nameText;             // 이름+수량 텍스트(TMP)                 // 텍스트
    public TextMeshProUGUI countText;            // [호환] 이전 수량 텍스트(미사용)        // 호환용
    public Vector3 worldOffset = new Vector3(0f, 1.0f, 0f); // 타겟 위 오프셋               // 오프셋

    [Header("시야 차단 설정(장애물)")]
    public LayerMask obstacleLayer;     // 아이템이 아닌 '진짜 장애물'만 포함하는 레이어
    public string obstacleTag = "Obstacle";  // 태그 기반 필터링(타일맵 포함)

    [Header("디버그 표시")]
public bool debugRaycast = true;       // 레이캐스트 디버그 출력 On/Off
public Color debugRayColor = Color.yellow; // 레이 색상
public Color debugHitColor = Color.red;    // 히트 지점 색상



    private Camera _cam;                         // 메인 카메라                           // 카메라
    private FieldItem _current;                  // 현재 Hover 중인 아이템                 // 현재

    private void Reset()                         // 인스펙터 기본값 보정
    {
        if (!origin) origin = transform;
        if (radius < 0.1f) radius = 3.5f;
        if (forwardAxis.sqrMagnitude < 0.001f) forwardAxis = Vector2.right;
    }

    private void Awake()                         // 초기화
    {
        _cam = Camera.main;
        SetUIVisible(false);                     // 시작 시 숨김
        if (countText) { countText.text = string.Empty; countText.gameObject.SetActive(false); } // 호환 숨김
    }

    private void Update()                        // 마우스 오버 + 부채꼴/시야 판정 + UI 배치
    {
        if (!origin) return;

        // 1) 마우스 아래 2D 콜라이더 확인
        var mouseWorld = _cam ? (Vector2)_cam.ScreenToWorldPoint(Input.mousePosition)
                              : (Vector2)Input.mousePosition;
        var hit = Physics2D.OverlapPoint(mouseWorld, itemLayer);
        var hovered = hit ? hit.GetComponent<FieldItem>() : null;

        // 2) 부채꼴 + (옵션) 시야 레이캐스트 검사
        if (hovered && IsInSectorAndVisible(hit))
        {
            if (hovered != _current)
            {
                _current = hovered;
                RefreshUIFromTarget(_current);   // 아이콘/텍스트 갱신
            }

            // 3) UI 위치 고정
            if (lockToTarget && worldCanvas)
            {
                worldCanvas.transform.position = hit.bounds.center + worldOffset;
            }

            SetUIVisible(true);
        }
        else
        {
            _current = null;
            SetUIVisible(false);
        }
    }

private bool IsInSectorAndVisible(Collider2D target)
{
    if (!target || !origin) return false;

    Vector2 o = origin.position;                       
    Vector2 c = target.bounds.center;                  
    Vector2 to = c - o;                                
    float dist = to.magnitude;                         

    // 1) 부채꼴 반경 체크
    if (dist > radius) return false;

    // 2) 부채꼴 각도 체크
    Vector2 fwd = ForwardFromOrigin();
    float cos = Vector2.Dot(fwd, to.normalized);
    float cosLim = Mathf.Cos(halfAngleDeg * Mathf.Deg2Rad);
    if (cos < cosLim) return false;

    // 3) (옵션) 시야 차단 검사
    if (!requireLineOfSight)
        return true;

    // 3-A) 장애물 레이어로 Raycast
    RaycastHit2D hit = Physics2D.Raycast(o, to.normalized, dist, obstacleLayer);

    // ⭐⭐⭐ 디버그용 Gizmo 출력 추가 부분 ⭐⭐⭐
    if (debugRaycast)
    {
        Debug.DrawLine(o, o + to.normalized * dist, debugRayColor);  // 레이 경로 표시

        if (hit.collider)
        {
            Debug.DrawLine(hit.point, hit.point + Vector2.one * 0.15f, debugHitColor); // 히트 지점 강조
            Debug.Log($"[HoverDebug] Ray hit: {hit.collider.name}, Tag={hit.collider.tag}, Layer={LayerMask.LayerToName(hit.collider.gameObject.layer)}");
        }
        else
        {
            Debug.Log("[HoverDebug] Ray hit nothing.");
        }
    }
    // ⭐⭐⭐ 디버그 추가 끝 ⭐⭐⭐


    // 3-B) 히트된 장애물이 없으면 통과
    if (!hit.collider)
        return true;

    // 3-C) 태그 기반 장애물 판정
    if (hit.collider.CompareTag(obstacleTag))
        return false; // → 시야 차단됨

    // 3-D) 장애물이 아니면 무시하고 통과
    return true;
}



    private Vector2 ForwardFromOrigin()                    // 원점 회전 반영 전방 계산
    {
        var dir = (Vector2)(origin.rotation * new Vector3(forwardAxis.x, forwardAxis.y, 0f));
        if (dir.sqrMagnitude < 0.0001f) dir = Vector2.right;
        return dir.normalized;
    }

    private void RefreshUIFromTarget(FieldItem fi)         // UI 데이터 갱신
    {
        if (!fi) return;

        if (iconImage)
        {
            iconImage.sprite = fi.icon;
            iconImage.enabled = (fi.icon != null);
        }

        if (nameText)
        {
            // 이름 폴백
            string nameStr = !string.IsNullOrWhiteSpace(fi.displayName)
                ? fi.displayName
                : $"아이템({fi.typeId}:{fi.itemId})";

            // 스택 규칙으로 수량 표기 여부 결정
            StackRuleRegistry.Instance.GetRuleOrDefault(fi.typeId, fi.itemId, out bool canStack, out _);
            bool showCount = canStack && fi.count > 1;

            nameText.text = showCount ? $"{nameStr} x{fi.count}" : nameStr;
        }

        if (countText) { countText.text = string.Empty; countText.gameObject.SetActive(false); } // 호환 숨김
    }

    private void SetUIVisible(bool visible)                 // UI 표시/숨김 토글
    {
        if (worldCanvas) worldCanvas.enabled = visible;
    }

    // ====== 씬 뷰 기즈모: 부채꼴(회전 반영) ======
    private void OnDrawGizmos()       { DrawSectorGizmo(false); }  // 항상(연한 색)
    private void OnDrawGizmosSelected(){ DrawSectorGizmo(true); }   // 선택(진한 색)

    private void DrawSectorGizmo(bool selected)             // 부채꼴 그리기
    {
        var ori = origin ? origin : transform;
        Vector3 o = ori.position;
        Vector2 fwd = ForwardFromOrigin();

        Color baseCol = selected ? new Color(1f, 0.6f, 0f, 0.9f) : new Color(1f, 0.6f, 0f, 0.35f);
        Gizmos.color = baseCol;

        int seg = Mathf.Clamp(32, 8, 128);
        float baseDeg = Mathf.Atan2(fwd.y, fwd.x) * Mathf.Rad2Deg;
        Vector2 leftDir  = AngleToDir(baseDeg - halfAngleDeg);
        Vector2 rightDir = AngleToDir(baseDeg + halfAngleDeg);

        Gizmos.DrawLine(o, o + (Vector3)(leftDir  * radius));
        Gizmos.DrawLine(o, o + (Vector3)(rightDir * radius));

        Vector3 prev = o + (Vector3)(leftDir * radius);
        for (int i = 1; i <= seg; i++)
        {
            float t = i / (float)seg;
            float a = Mathf.Lerp(-halfAngleDeg, halfAngleDeg, t);
            Vector3 next = o + (Vector3)(AngleToDir(baseDeg + a) * radius);
            Gizmos.DrawLine(prev, next);
            prev = next;
        }
    }

    private static Vector2 AngleToDir(float deg)           // 각도(도)→단위벡터
    {
        float rad = deg * Mathf.Deg2Rad;
        return new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));
    }
}
