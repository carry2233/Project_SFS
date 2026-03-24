using UnityEngine;

public class CameraFollowMouse : MonoBehaviour
{
    [Header("Camera Settings")]
    public float followSpeed = 5f;                     // 카메라 추적 속도(lerp 계수)
    public Transform player;                           // 따라갈 플레이어

    [Header("Zoom Settings")]
    public float zoomSpeed = 2f;                       // 줌(휠) 속도
    public float minZoom = 5f;                         // 직교 카메라 최소 사이즈
    public float maxZoom = 20f;                        // 직교 카메라 최대 사이즈

    [Header("Movement Settings (XY 평면)")]
    public float baseRadius = 5f;                      // 줌 최소일 때 벗어날 기본 반경
    public float zoomMultiplier = 1f;                  // 줌 최대일 때 반경을 baseRadius*(1+zoomMultiplier)
    public bool useCircularLimit = true;               // 플레이어 중심 원형 제한 반경 클램프 여부

    [Header("Mouse Influence Settings")]
    public float movementFactor = 2f;                  // 마우스 영향 가중치
    public float maxMouseDistance = 0.5f;              // 뷰포트 중앙에서 이 거리 이상이면 영향 100%

    [Header("Key Settings")]
    public KeyCode toggleFollowKey = KeyCode.Space;    // 마우스 추적 토글 키

    private float currentRadius;                       // 현재 반경(줌에 따라 가변)
    private bool isMouseFollowEnabled = true;          // 마우스 추적 기능 On/Off
    private float currentZoom;                         // 현재 직교 카메라 사이즈

    private Vector3 lastPlayerPosition;                // 이전 프레임의 플레이어 위치
    private Camera mainCam;                            // 메인 카메라 참조

    private void Start()                               // 시작 시 초기화
    {
        if (player == null)
        {
            Debug.LogError("플레이어 Transform이 설정되지 않았습니다.");
            enabled = false;
            return;
        }

        mainCam = Camera.main;
        if (mainCam == null)
        {
            Debug.LogError("Main Camera를 찾을 수 없습니다.");
            enabled = false;
            return;
        }

        if (!mainCam.orthographic)
            Debug.LogWarning("본 스크립트는 직교(Orthographic) 카메라 기준으로 설계되었습니다.");

        currentZoom = mainCam.orthographicSize;       // 현재 줌 사이즈 기록
        UpdateRadius();                                // 반경 계산
        lastPlayerPosition = player.position;          // 마지막 플레이어 위치 초기화
    }

    private void Update()                              // 입력(키/휠) 처리
    {
        HandleToggleFollow();                          // 마우스 추적 토글
        HandleZoom();                                  // 마우스 휠 줌
    }

    private void LateUpdate()                          // 추적 이동(렌더 직전)
    {
        if (isMouseFollowEnabled && Input.GetMouseButton(1)) // 마우스 추적 On + 우클릭 중
        {
            FollowMouse();                             // 마우스 영향 모드
        }
        else
        {
            FollowPlayer();                            // 플레이어 중심 모드
        }

        lastPlayerPosition = player.position;          // 마지막 플레이어 위치 갱신
    }

    private void HandleZoom()                          // 줌(휠) 처리
    {
        float scrollInput = Input.GetAxis("Mouse ScrollWheel");
        if (Mathf.Abs(scrollInput) > 0.01f)
        {
            currentZoom = Mathf.Clamp(
                currentZoom - scrollInput * zoomSpeed,
                minZoom, maxZoom);
            mainCam.orthographicSize = currentZoom;
            UpdateRadius();
        }
    }

    private void UpdateRadius()                        // 줌에 따른 반경 계산
    {
        float denom = Mathf.Max(0.0001f, maxZoom - minZoom);
        float t = (currentZoom - minZoom) / denom;     // 0~1
        currentRadius = baseRadius * (1f + t * zoomMultiplier);
    }

    private void FollowPlayer()                        // 플레이어 중심 추적
    {
        MoveCamera(lastPlayerPosition);                // 이전 위치 기준으로 부드럽게 이동
    }

    private void FollowMouse()                         // 마우스 영향 추적(**XY** 기준)  ← 변경
    {
        if (mainCam == null || player == null) return;

        Vector3 mouseViewportPos = mainCam.ScreenToViewportPoint(Input.mousePosition); // 0~1
        Vector2 offsetFromCenter = new Vector2(
            mouseViewportPos.x - 0.5f,
            mouseViewportPos.y - 0.5f);

        float distance = offsetFromCenter.magnitude;
        float movementStrength =
            Mathf.Clamp(distance / maxMouseDistance, 0f, 1f) * movementFactor;

        // **뷰포트 X→월드 X, 뷰포트 Y→월드 Y** 로 매핑  ← 변경
        Vector3 xyOffset = new Vector3(offsetFromCenter.x, offsetFromCenter.y, 0f);
        Vector3 targetFocusPoint =
            player.position + xyOffset * movementStrength * currentRadius;

        MoveCamera(targetFocusPoint);
    }

    private void MoveCamera(Vector3 targetPosition)    // 목표점으로 카메라 이동 + 반경 클램프(**XY**)
    {
        if (useCircularLimit)
        {
            Vector3 delta = targetPosition - player.position;
            Vector3 deltaXY = new Vector3(delta.x, delta.y, 0f); // **XY 성분만**  ← 변경
            float dist = deltaXY.magnitude;
            if (dist > 0f)
            {
                float clamped = Mathf.Min(dist, currentRadius);
                Vector3 dirXY = deltaXY / dist;
                targetPosition = player.position + dirXY * clamped;
            }
            else
            {
                targetPosition = player.position;
            }
        }

        targetPosition.z = transform.position.z;        // 카메라 **깊이(Z) 고정**  ← 변경
        transform.position = Vector3.Lerp(
            transform.position, targetPosition, followSpeed * Time.deltaTime);
    }

    private void HandleToggleFollow()                   // 마우스 추적 On/Off 토글
    {
        if (Input.GetKeyDown(toggleFollowKey))
        {
            isMouseFollowEnabled = !isMouseFollowEnabled;
            Debug.Log($"Mouse Follow Enabled: {isMouseFollowEnabled}");
        }
    }

    private void OnDrawGizmos()                         // 반경 시각화(에디터)
    {
        if (player != null && useCircularLimit)
        {
            Gizmos.color = Color.blue;
            // XY 평면에서의 반경 표현은 Scene 뷰 2D 모드로 확인 권장
            Gizmos.DrawWireSphere(player.position, currentRadius);
        }
    }
}
