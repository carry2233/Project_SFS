using UnityEngine;

[AddComponentMenu("Visual/Directional Sprite Switcher (Event-Driven, Aim-Priority)")]
public class DirectionalSpriteSwitcher : MonoBehaviour
{
    // ===== 인스펙터 =====
    [Header("참조")]
    public PlayerMovement playerMovement;                // ✅ PlayerMovement 직접 참조(이벤트 구독)
    public SpriteRenderer spriteRenderer;                // ✅ 최종 적용 대상 SR
    public Transform targetTransform;                    // ✅ 포지션 오프셋 적용 대상(없으면 this)

    [Header("방향별 스프라이트 (4장)")]
    public Sprite upSprite;                              // ✅ Up 스프라이트
    public Sprite downSprite;                            // ✅ Down 스프라이트
    public Sprite leftSprite;                            // ✅ Left 스프라이트
    public Sprite rightSprite;                           // ✅ Right 스프라이트

    [Header("전환 정책")]
    public bool preferPlayerFacing = true;               // ✅ (폴백용) PM 방향 우선 사용 여부
    [Range(0f, 0.25f)] public float minSwitchInterval = 0.06f; // ✅ 폴백시 히스테리시스
    public float movingSpeedThreshold = 0.02f;           // ✅ 폴백 이동 임계
    public float axisDeadZone = 0.05f;                   // ✅ 폴백 축 데드존

    [Header("방향별 레이어순서 오프셋")]
    public SpriteRenderer sortingFollowRenderer;         // ✅ 레이어값 참조 대상
    public bool useSortingOffset = true;                 // ✅ 방향별 오프셋 사용
    public int upSortingOffset = 0;                      // ✅ Up 오프셋
    public int downSortingOffset = 0;                    // ✅ Down 오프셋
    public int leftSortingOffset = 0;                    // ✅ Left 오프셋
    public int rightSortingOffset = 0;                   // ✅ Right 오프셋

    [Header("방향별 로컬 포지션 오프셋")]
    public bool usePositionOffset = true;                // ✅ 포지션 오프셋 사용
    public Vector3 upPositionOffset = Vector3.zero;      // ✅ Up 포지션 오프셋
    public Vector3 downPositionOffset = Vector3.zero;    // ✅ Down 포지션 오프셋
    public Vector3 leftPositionOffset = Vector3.zero;    // ✅ Left 포지션 오프셋
    public Vector3 rightPositionOffset = Vector3.zero;   // ✅ Right 포지션 오프셋

    [Header("디버그")]
    public bool verboseLogs = false;                     // ✅ 디버그 로그

    // ===== 내부 상태 =====
    private Vector3 _prevPos;                            // ✅ 폴백 추정용 이전 위치
    private Direction _lastDir = Direction.Down;         // ✅ 마지막 확정 방향
    private float _lastSwitchTime = -999f;               // ✅ 마지막 전환 시각
    private Vector3 _baseLocalPos;                       // ✅ 기준 로컬 포지션

    private enum Direction { Up, Down, Left, Right }     // ✅ 4방향 열거형

    private void Awake() // ✅ 초기화
    {
        if (!playerMovement) playerMovement = GetComponent<PlayerMovement>();
        if (!spriteRenderer) spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        if (!targetTransform) targetTransform = transform;

        _baseLocalPos = targetTransform.localPosition;
        _prevPos = transform.position;

        // 초기 상태 반영(이벤트 수신 전)
        if (playerMovement)
        {
            _lastDir = MapPMDir(playerMovement.EffectiveFacing);
        }
        ApplySprite(_lastDir, true);
        ApplyPositionFixed(_lastDir);
    }

    private void OnEnable() // ✅ 이벤트 구독
    {
        if (playerMovement != null)
            playerMovement.OnEffectiveFacingChanged += HandleFacingChanged;
    }

    private void OnDisable() // ✅ 이벤트 해제
    {
        if (playerMovement != null)
            playerMovement.OnEffectiveFacingChanged -= HandleFacingChanged;
    }

    private void HandleFacingChanged(PlayerMovement.Direction pmDir) // ✅ 방향 변경 즉시 반응
    {
        var dir = MapPMDir(pmDir);
        if (dir == _lastDir) return;

        _lastDir = dir;
        _lastSwitchTime = Time.time;
        ApplySprite(_lastDir, true);
        ApplyPositionFixed(_lastDir);

        if (verboseLogs) Debug.Log($"[DSS] (event) Dir={_lastDir}");
    }

    private void Update() // ✅ 폴백(안전망): PM이 없거나 이벤트가 안 오는 경우 대비
    {
        if (playerMovement != null) return; // 이벤트 기반이면 폴백 불필요

        // 아래는 기존 구조 유지(최소한의 안전망)
        float now = Time.time;
        var cur = transform.position;
        var delta = cur - _prevPos;
        _prevPos = cur;

        Direction desired = _lastDir;
        if (delta.sqrMagnitude >= movingSpeedThreshold * movingSpeedThreshold)
        {
            float ax = Mathf.Abs(delta.x);
            float ay = Mathf.Abs(delta.y);
            if (!(ax < axisDeadZone && ay < axisDeadZone))
            {
                desired = (ax >= ay) ? (delta.x >= 0f ? Direction.Right : Direction.Left)
                                     : (delta.y >= 0f ? Direction.Up    : Direction.Down);
            }
        }

        bool canSwitch = (now - _lastSwitchTime) >= minSwitchInterval;
        if (desired != _lastDir && canSwitch)
        {
            _lastDir = desired;
            _lastSwitchTime = now;
            ApplySprite(_lastDir, true);
            ApplyPositionFixed(_lastDir);
            if (verboseLogs) Debug.Log($"[DSS] (fallback) Dir={_lastDir}");
        }

        ApplyPositionFixed(_lastDir);
    }

    private void LateUpdate() // ✅ 레이어 순서 따라가기 + 방향 오프셋
    {
        if (!spriteRenderer) return;

        int baseOrder = spriteRenderer.sortingOrder;
        if (sortingFollowRenderer) baseOrder = sortingFollowRenderer.sortingOrder;

        int dirSO = useSortingOffset ? GetSortingOffsetForDir(_lastDir) : 0;
        spriteRenderer.sortingOrder = baseOrder + dirSO;

        if (verboseLogs) Debug.Log($"[DSS] Sorting = {baseOrder} + {dirSO} = {spriteRenderer.sortingOrder}");
    }

    // ===== 도우미 =====
    private Direction MapPMDir(PlayerMovement.Direction d) // ✅ PM.Direction → 로컬 Direction 매핑
    {
        switch (d)
        {
            case PlayerMovement.Direction.Up: return Direction.Up;
            case PlayerMovement.Direction.Down: return Direction.Down;
            case PlayerMovement.Direction.Left: return Direction.Left;
            case PlayerMovement.Direction.Right: return Direction.Right;
        }
        return Direction.Down;
    }

    private void ApplySprite(Direction dir, bool force = false) // ✅ 스프라이트 교체
    {
        if (!spriteRenderer) return;
        switch (dir)
        {
            case Direction.Up:    if (upSprite)    spriteRenderer.sprite = upSprite;    break;
            case Direction.Down:  if (downSprite)  spriteRenderer.sprite = downSprite;  break;
            case Direction.Left:  if (leftSprite)  spriteRenderer.sprite = leftSprite;  break;
            case Direction.Right: if (rightSprite) spriteRenderer.sprite = rightSprite; break;
        }
    }

    private void ApplyPositionFixed(Direction dir) // ✅ 기준 + 방향 오프셋 덮어쓰기
    {
        if (!targetTransform) return;
        Vector3 off = GetPosOffsetForDir(dir);
        targetTransform.localPosition = _baseLocalPos + (usePositionOffset ? off : Vector3.zero);
    }

    private int GetSortingOffsetForDir(Direction dir) // ✅ 방향별 정렬 오프셋 조회
    {
        switch (dir)
        {
            case Direction.Up:    return upSortingOffset;
            case Direction.Down:  return downSortingOffset;
            case Direction.Left:  return leftSortingOffset;
            case Direction.Right: return rightSortingOffset;
        }
        return 0;
    }

    private Vector3 GetPosOffsetForDir(Direction dir) // ✅ 방향별 포지션 오프셋 조회
    {
        switch (dir)
        {
            case Direction.Up:    return upPositionOffset;
            case Direction.Down:  return downPositionOffset;
            case Direction.Left:  return leftPositionOffset;
            case Direction.Right: return rightPositionOffset;
        }
        return Vector3.zero;
    }
}
