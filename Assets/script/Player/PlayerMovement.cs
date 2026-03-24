using System;
using System.Collections.Generic;                       // 리스트/이벤트용
using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class PlayerMovement : MonoBehaviour
{
    // ===== 필수 컴포넌트 =====
    [Header("필수 컴포넌트")]
    [SerializeField] private Rigidbody2D rb2D;                 // ✅ 2D 리지드바디 참조
    [SerializeField] private SpriteRenderer spriteRenderer;    // ✅ 방향 스프라이트 대상

    // ===== 이동 설정 =====
    [Header("이동 설정")]
    [SerializeField] private float moveSpeed = 5f;             // ✅ 기본 이동 속도(유닛/초)
    [SerializeField] private bool autoConstraints = true;      // ✅ 제약/중력 자동 설정 여부

    // ===== 이동 속도 배율/최종값 =====
    [Header("이동 속도 배율/최종값")]
    public float moveSpeedMultiplier = 1f;                     // ✅ 이동속도 배율
    public float finalMoveSpeed = 0f;                          // ✅ 최종 이동 속도

    // ===== 입력 키 =====
    [Header("입력 키 (인스펙터에서 변경 가능)")]
    [SerializeField] private KeyCode upKey = KeyCode.W;        // ✅ 위 이동 키
    [SerializeField] private KeyCode downKey = KeyCode.S;      // ✅ 아래 이동 키
    [SerializeField] private KeyCode leftKey = KeyCode.A;      // ✅ 좌 이동 키
    [SerializeField] private KeyCode rightKey = KeyCode.D;     // ✅ 우 이동 키

    // ===== 방향별 스프라이트 =====
    [Header("방향별 스프라이트")]
    [SerializeField] private Sprite upSprite;                  // ✅ 위 보기 스프라이트
    [SerializeField] private Sprite downSprite;                // ✅ 아래 보기 스프라이트
    [SerializeField] private Sprite leftSprite;                // ✅ 좌 보기 스프라이트
    [SerializeField] private Sprite rightSprite;               // ✅ 우 보기 스프라이트

    // ===== 초기 바라보는 방향 =====
    public enum Direction { Up, Down, Left, Right }            // ✅ 4방향 열거형
    [SerializeField] private Direction initialFacing = Direction.Down; // ✅ 시작 기본 방향

    // ===== 이동 중 스윙(좌우 흔들림) 설정 =====
    public enum FirstSwing { Positive, Negative }               // ✅ 먼저 도는 쪽(+/-)
    [Header("회전(스윙) 설정")]
    [SerializeField] private Transform rotateTarget;            // ✅ 회전시킬 대상
    [SerializeField] private float swingAngle = 15f;            // ✅ 스윙 최대 각도(절반)
    [SerializeField] private float swingSpeed = 180f;           // ✅ 기본 스윙 속도(°/s)
    public float swingScale = 1f;                               // ✅ 속도 배율 반영 강도
    public float finalSwingSpeed = 0f;                          // ✅ 최종 스윙 속도

    // ===== 에임 연동(스프라이트) =====
    [Header("에임 연동(스프라이트)")]
    [SerializeField] private 시야방향 aim;                      // ✅ 조준 상태/야우 읽기용 참조(기존 필드 재사용)
    [SerializeField] private Transform facingYawSource;         // ✅ 야우 기준 소스
    [SerializeField] private bool useAimYawForSprites = true;   // ✅ 조준 중엔 야우 기반 전환
    [SerializeField] private float wedgeHalfDeg = 45f;          // ✅ 4방향 부채꼴 반각(기본 45°)

    public Direction CurrentFacingForExternal => currentFacing; // ✅ 외부 접근용 현재 키기반 방향

    // ===== 조작 허용 스위치(릴레이 토글) =====
    [Header("조작 허용 스위치")]
    [SerializeField] private bool allowMovement = true;         // ✅ 이동 로직 허용
    [SerializeField] private bool allowFacingByKeys = true;     // ✅ 방향키 기반 스프라이트 전환 허용
    [SerializeField] private bool allowAimBasedFacing = true;   // ✅ 조준(Yaw) 기반 스프라이트 전환 허용

    // ===== (추가) 조준 포함 "유효 방향" 이벤트/설정 =====
    [Header("유효 방향(조준 포함) 이벤트/설정")]
    [SerializeField, Tooltip("에임 경계각에서 출렁임 방지용 최소 전환 간격(초)")]
    private float minFacingSwitchInterval = 0.06f;              // ✅ 히스테리시스(최소 전환 간격)
    [SerializeField, Tooltip("잠금 중 방향 이벤트를 억제할지 여부(릴레이에서 켜고/끔)")]
    private bool suppressFacingEvents = false;                  // ✅ 잠금 중 이벤트 억제 토글

    public event Action<Direction> OnEffectiveFacingChanged;    // ✅ 유효 방향 변경 이벤트
    public Direction EffectiveFacing { get; private set; } = Direction.Down; // ✅ 현재 유효 방향(조준 포함)
    public bool IsAiming => (aim != null && aim.IsAiming);      // ✅ 조준 상태 패스스루
    public bool SuppressFacingEvents                            // ✅ 외부에서 억제 토글
    {
        get => suppressFacingEvents;
        set => suppressFacingEvents = value;
    }

    // ===== 외부 힘(임펄스) 잔속도 차단 =====
[Header("외부 힘 잔속도 차단(입력 없을 때)")]
[SerializeField] private bool blockExternalVelocityWhenNoInput = true; // ✅ 입력 없을 때 외부 잔속도 차단 여부
[SerializeField, Min(0f)] private float externalVelocityThreshold = 0.10f; // ✅ 이 값 이상 속도면 0으로 덮어쓰기 임계값
[SerializeField] private bool alsoZeroAngularVelocity = true; // ✅ 각속도도 같이 0으로 만들지 여부


    // ===== 내부 상태 =====
    private readonly List<KeyCode> heldOrder = new List<KeyCode>(); // ✅ 최근 키 입력 순서
    private Direction currentFacing;                          // ✅ 현재 키기반 바라보는 방향
    private Vector3 moveDir = Vector3.zero;                  // ✅ 이동 방향(XY 정규화)
    private float baseYaw = 0f;                              // ✅ 스윙 기준각(Z 의미적)
    private float oscAngle = 0f;                             // ✅ 스윙 오프셋 각도
    private int swingDir = +1;                               // ✅ 스윙 진행 방향(+/-)
    private bool wasMoving = false;                          // ✅ 직전 프레임 이동 여부
    private const float angleEps = 0.01f;                    // ✅ 0도 근접 오차
    private float lastFacingChangeTime = -999f;              // ✅ 마지막 유효 방향 변경 시각(히스테리시스용)

    // 읽기 전용 편의
    public Vector3 CurrentMoveDir => moveDir;               // ✅ 현재 이동 벡터
    public bool IsMoving => moveDir.sqrMagnitude > 0f;      // ✅ 이동 중 여부

    private void Reset() // ✅ 컴포넌트 자동 할당
    {
        rb2D = GetComponent<Rigidbody2D>();
        if (spriteRenderer == null) spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        if (rotateTarget == null) rotateTarget = (spriteRenderer != null) ? spriteRenderer.transform : transform;
    }

    private void Awake() // ✅ 시작 전 초기 세팅
    {
        if (rb2D == null) rb2D = GetComponent<Rigidbody2D>();
        if (autoConstraints)
        {
            rb2D.gravityScale = 0f;
            rb2D.constraints = RigidbodyConstraints2D.FreezeRotation;
            rb2D.interpolation = RigidbodyInterpolation2D.Interpolate;
            rb2D.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        }

        if (rotateTarget == null)
            rotateTarget = (spriteRenderer != null) ? spriteRenderer.transform : transform;

        if (aim == null) aim = GetComponent<시야방향>();     // ✅ 시야방향 참조 보정
        if (facingYawSource == null && aim != null)
            facingYawSource = aim.RotateTarget != null ? aim.RotateTarget : aim.transform;
    }

    private void Start() // ✅ 플레이 시작 시 1회
    {
        currentFacing = initialFacing;
        ApplyFacingSprite(currentFacing);

        baseYaw = rotateTarget.localEulerAngles.z;
        oscAngle = 0f;
        swingDir = +1;
        wasMoving = false;

        moveSpeedMultiplier = Mathf.Max(0.01f, moveSpeedMultiplier);
        swingScale = Mathf.Max(0f, swingScale);
        finalMoveSpeed = moveSpeed * moveSpeedMultiplier;
        finalSwingSpeed = swingSpeed * (1f + (moveSpeedMultiplier - 1f) * swingScale);

        EffectiveFacing = GetEffectiveFacingInstant();       // ✅ 초기 유효 방향 계산/기록
        lastFacingChangeTime = Time.time;
    }

    private void Update() // ✅ 입력 처리/방향 결정/스윙 갱신/유효방향 이벤트 발행
    {
        // ── 조작 완전 금지 시(릴레이) ──
        bool controlLocked = !allowMovement && !allowFacingByKeys && !allowAimBasedFacing;
        if (controlLocked)
        {
            UpdateSwingRotation(false);
            wasMoving = false;
            TryPublishEffectiveFacing(keepTimeGuard: false); // ✅ 잠금 중에도(필요시) 유지
            return;
        }

        // 1) 키 다운/업 추적
        TrackKey(upKey);
        TrackKey(downKey);
        TrackKey(leftKey);
        TrackKey(rightKey);

        // 2) 축 입력 읽기
        bool up = Input.GetKey(upKey);
        bool down = Input.GetKey(downKey);
        bool left = Input.GetKey(leftKey);
        bool right = Input.GetKey(rightKey);
        bool conflictX = left && right;
        bool conflictZ = up && down;

        int x = (right ? 1 : 0) + (left ? -1 : 0);
        int y = (up ? 1 : 0) + (down ? -1 : 0);
        if (conflictX) x = 0;
        if (conflictZ) y = 0;

        Vector3 raw = new Vector3(x, y, 0f);
        moveDir = raw.sqrMagnitude > 0f ? raw.normalized : Vector3.zero;
        bool isMoving = moveDir.sqrMagnitude > 0f;

        // 3) 스프라이트 전환(키/에임 경로)
        bool aimingForSprite = useAimYawForSprites && aim != null && aim.IsAiming && facingYawSource != null;

        if (aimingForSprite && allowAimBasedFacing)
        {
            float yaw = (aim != null) ? Normalize180(aim.CurrentYawDeg)
                                      : Normalize180(facingYawSource.eulerAngles.y);
            Direction face = DirectionFromYaw(yaw, wedgeHalfDeg);
            if (face != currentFacing)
            {
                currentFacing = face;
                ApplyFacingSprite(currentFacing);
            }
        }
        else if (isMoving && allowFacingByKeys)
        {
            KeyCode recent = GetMostRecentValidKey(conflictX, conflictZ);
            if (recent != KeyCode.None)
            {
                Direction face = KeyToDirection(recent);
                if (face != currentFacing)
                {
                    currentFacing = face;
                    ApplyFacingSprite(currentFacing);
                }
            }
        }

        // 4) 스윙 갱신
        UpdateSwingRotation(isMoving);
        wasMoving = isMoving;

        // 5) 유효 방향(조준 포함) 계산/이벤트 발행
        TryPublishEffectiveFacing(keepTimeGuard: true);
    }

private void FixedUpdate() // ✅ 물리 이동
{
    ApplyNoInputExternalVelocityBlock(); // ✅ 추가: 입력 없을 때 외부 잔속도 차단

    if (!allowMovement) return; // ✅ 기존: 이동 허용 OFF면 이동 처리 종료

    if (moveDir.sqrMagnitude > 0f) // ✅ 기존: 입력이 있을 때만 MovePosition 이동
    {
        Vector2 curr = rb2D.position; // ✅ 현재 위치
        Vector2 delta = new Vector2(moveDir.x, moveDir.y) * finalMoveSpeed * Time.fixedDeltaTime; // ✅ 이동량
        rb2D.MovePosition(curr + delta); // ✅ 이동 적용
    }
}


    // ===== 유효 방향(조준 포함) 계산/이벤트 =====
    private Direction GetEffectiveFacingInstant() // ✅ 지금 프레임의 "조준 포함" 방향 계산
    {
        if (aim != null && aim.IsAiming)                           // 조준 중이면 Yaw → 4방향
        {
            float yaw = Normalize180(aim.CurrentYawDeg);
            return DirectionFromYaw(yaw, wedgeHalfDeg);
        }
        return currentFacing;                                       // 비조준이면 키기반
    }

    private void TryPublishEffectiveFacing(bool keepTimeGuard) // ✅ 변경 시 이벤트 발행
    {
        Direction now = GetEffectiveFacingInstant();

        // 히스테리시스: 조준 중이면 즉시 반영, 비조준이면 최소 간격 준수
        bool timeOk = true;
        if (keepTimeGuard && (aim == null || !aim.IsAiming))
        {
            timeOk = (Time.time - lastFacingChangeTime) >= minFacingSwitchInterval;
        }

        if (now != EffectiveFacing && timeOk)
        {
            EffectiveFacing = now;
            lastFacingChangeTime = Time.time;

            if (!suppressFacingEvents)                              // 잠금 중 억제 옵션
                OnEffectiveFacingChanged?.Invoke(EffectiveFacing);
        }
        else
        {
            // 값은 갱신하되 이벤트는 조건 불충족 시 발행 안 함
            EffectiveFacing = now;
        }
    }

    // ===== 스윙 회전 =====
    private void UpdateSwingRotation(bool isMoving) // ✅ 이동 여부에 따른 스윙 회전
    {
        if (rotateTarget == null) return;

        if (!wasMoving && isMoving && Mathf.Abs(oscAngle) <= angleEps)
            swingDir = +1;

        if (isMoving)
        {
            oscAngle += swingDir * finalSwingSpeed * Time.deltaTime;
            if (oscAngle >= +swingAngle) { oscAngle = +swingAngle; swingDir = -1; }
            if (oscAngle <= -swingAngle) { oscAngle = -swingAngle; swingDir = +1; }
        }
        else
        {
            oscAngle = Mathf.MoveTowards(oscAngle, 0f, finalSwingSpeed * Time.deltaTime);
        }

        Vector3 euler = rotateTarget.localEulerAngles;
        float targetYaw = baseYaw + oscAngle;
        euler.y = targetYaw;
        rotateTarget.localEulerAngles = euler;
    }

    // ===== 입력/도우미 =====
    private void TrackKey(KeyCode key) // ✅ 키 입력 순서/상태 추적
    {
        if (Input.GetKeyDown(key))
            if (!heldOrder.Contains(key)) heldOrder.Add(key);

        if (Input.GetKeyUp(key))
            heldOrder.Remove(key);
    }

    private KeyCode GetMostRecentValidKey(bool conflictX, bool conflictZ) // ✅ 상충 축 제외 후 최근 키
    {
        for (int i = heldOrder.Count - 1; i >= 0; i--)
        {
            KeyCode k = heldOrder[i];
            if (conflictX && (k == leftKey || k == rightKey)) continue;
            if (conflictZ && (k == upKey || k == downKey)) continue;
            return k;
        }
        return KeyCode.None;
    }

    private Direction KeyToDirection(KeyCode key) // ✅ 키→방향 매핑
    {
        if (key == upKey) return Direction.Up;
        if (key == downKey) return Direction.Down;
        if (key == leftKey) return Direction.Left;
        if (key == rightKey) return Direction.Right;
        return currentFacing;
    }

    private void ApplyFacingSprite(Direction dir) // ✅ 방향별 스프라이트 적용
    {
        if (spriteRenderer == null) return;
        switch (dir)
        {
            case Direction.Up:    if (upSprite)    spriteRenderer.sprite = upSprite;    break;
            case Direction.Down:  if (downSprite)  spriteRenderer.sprite = downSprite;  break;
            case Direction.Left:  if (leftSprite)  spriteRenderer.sprite = leftSprite;  break;
            case Direction.Right: if (rightSprite) spriteRenderer.sprite = rightSprite; break;
        }
    }

    public void ApplyKnockback(Vector3 impulse) // ✅ 외부 넉백(2D)
    {
        Vector2 imp2D = new Vector2(impulse.x, impulse.y);
        rb2D.AddForce(imp2D, ForceMode2D.Impulse);
    }

    private static float Normalize180(float deg) // ✅ 각도 정규화([-180,180))
    {
        deg %= 360f;
        if (deg >= 180f) deg -= 360f;
        if (deg < -180f) deg += 360f;
        return deg;
    }

    private static bool InWedge(float deg, float center, float half) // ✅ 반구간(-half, +half]
    {
        float d = Normalize180(deg - center);
        return (d > -half && d <= half);
    }

    private Direction DirectionFromYaw(float yawDeg, float half) // ✅ Yaw→4방향 매핑
    {
        if (InWedge(yawDeg, 0f, half)) return Direction.Up;
        if (InWedge(yawDeg, 90f, half)) return Direction.Right;
        if (InWedge(yawDeg, 180f, half) || InWedge(yawDeg, -180f, half)) return Direction.Down;
        return Direction.Left;
    }

    // ===== 릴레이 연동용 프로퍼티 =====
    public bool AllowMovement { get => allowMovement; set => allowMovement = value; }           // ✅ 이동 허용
    public bool AllowFacingByKeys { get => allowFacingByKeys; set => allowFacingByKeys = value; } // ✅ 키 전환 허용
    public bool AllowAimBasedFacing { get => allowAimBasedFacing; set => allowAimBasedFacing = value; } // ✅ 에임 전환 허용

    public void ForceZeroVelocity2D() // ✅ 속도 0 강제(정지)
    {
        if (rb2D != null) rb2D.linearVelocity = Vector2.zero;
    }

    // ✅ 현재 유효 방향을 즉시 재계산/동기화하고, 이벤트를 '강제 1회' 발행하는 메서드
public void ForcePublishEffectiveFacing() // 현재 유효 방향 강제 발행(히스테리시스/억제 무시)
{
    // 1) 최신 유효 방향 재계산
    Direction now = GetEffectiveFacingInstant(); // 현재 프레임 기준 유효 방향(조준 포함)
    EffectiveFacing = now;                       // 내부 값 동기화

    // 2) 억제/간격 가드 무시하고 이벤트 1회 발행
    bool prevSuppress = suppressFacingEvents;    // 기존 억제 상태 백업
    suppressFacingEvents = false;                // 이벤트 억제 해제(일시)

    // 시간 간격 가드도 우회하기 위해 직접 호출
    lastFacingChangeTime = Time.time;            // (선택) 타임스탬프 갱신
    OnEffectiveFacingChanged?.Invoke(EffectiveFacing); // 🔔 강제 1회 이벤트 발행

    suppressFacingEvents = prevSuppress;         // 억제 상태 원복
}

private void ApplyNoInputExternalVelocityBlock() // ✅ 입력 없음 + 속도 존재 시 속도 완전 차단
{
    if (!blockExternalVelocityWhenNoInput) return; // ✅ 기능 OFF면 종료
    if (rb2D == null) return; // ✅ 리지드바디 없으면 종료

    if (moveDir.sqrMagnitude > 0.0001f) return; // ✅ 입력(이동 방향) 있으면 덮어쓰기 금지

    Vector2 v = rb2D.linearVelocity; // ✅ 현재 선속도
    float th = externalVelocityThreshold; // ✅ 임계값
    if (v.sqrMagnitude <= th * th) return; // ✅ 임계값 이하면 무시

    rb2D.linearVelocity = Vector2.zero; // ✅ 선속도 완전 차단(덮어쓰기)
    if (alsoZeroAngularVelocity) rb2D.angularVelocity = 0f; // ✅ 각속도도 차단(선택)
}


}
