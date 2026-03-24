using UnityEngine;     // 유니티 기본
using UnityEngine.AI;  // NavMesh 경로 계산용
using System.Collections; // ✅ 코루틴 사용



[AddComponentMenu("CreatureAI/Entity Movement Simple AI")] // 인스펙터 메뉴 경로
[RequireComponent(typeof(Rigidbody2D))]                    // Rigidbody2D 필수
public class EntityMovementAI : MonoBehaviour
{
    // ==============================
    // REFERENCES
    // ==============================

    [Header("필수 참조")]
    [SerializeField] private Rigidbody2D rb;               // 이동용 Rigidbody2D
    [SerializeField] private SpriteRenderer sprite;        // 4방향 스프라이트 변경용

    // ==============================
    // MOVE SETTINGS
    // ==============================

    [Header("이동 설정")]
    [Min(0f)][SerializeField] private float moveSpeed = 3.5f;           // 최대 이동 속도
    [Min(0f)][SerializeField] private float accel = 30f;                // (CreatureAI 방식) 가속도
    [Min(0f)][SerializeField] private float decel = 30f;                // (CreatureAI 방식) 감속도
    [Range(0f, 1f)][SerializeField] private float diagonalScale = 0.9f;  // 대각 이동 속도 보정(0~1)

    [Header("도착 판정(유하게)")]
    [Min(0f)][SerializeField] private float arriveDistance = 0.25f;     // 기본 도착 거리
    [Min(0f)][SerializeField] private float arriveEpsilon = 0.15f;      // 추가 여유(유한 판정)

    // ==============================
    // SPRITE (4 DIR)
    // ==============================

    [Header("4방향 스프라이트")]
    [SerializeField] private Sprite upSprite;             // 위
    [SerializeField] private Sprite downSprite;           // 아래
    [SerializeField] private Sprite leftSprite;           // 왼쪽
    [SerializeField] private Sprite rightSprite;          // 오른쪽

    [Header("이미지 변경 안정화(히스테리시스)")]
    [Min(0f)][SerializeField] private float minFacingSwitchInterval = 0.06f; // 최소 전환 간격(초)

    private Vector2 lastMoveDir8 = Vector2.right;         // 이전 이동 8방향 방향 기억(대각 포함)
    private bool hasLastMoveDir8 = false;                 // 이전 이동 방향 유효 여부
    private bool wasMovingLastFrame = false;              // 직전 프레임 이동 중이었는지

    private float lastFacingChangeTime = -999f;           // 마지막으로 이미지 방향이 실제 변경된 시간
    private Vector2 appliedCardinal = Vector2.right;      // 실제로 적용(표시) 중인 방향(4방향)
    private Vector2 lastCardinal = Vector2.right;         // 스프라이트 적용용(4방향)

    // ==============================
    // WOBBLE (MOVE ROTATION)
    // ==============================

    public enum WobbleAxis { X, Y, Z }                    // 회전 축 선택

    [Header("이동 중 좌우 회전(진자)")]
    [SerializeField] private Transform wobbleTarget;      // 회전 적용 대상(없으면 미적용)
    [SerializeField] private WobbleAxis wobbleAxis = WobbleAxis.Z; // 회전 축
    [SerializeField] private float wobbleAmplitude = 15f; // 회전 각도 한계(±)
    [SerializeField] private float wobbleSpeed = 180f;    // 각속도(도/초)
    [SerializeField] private bool wobbleStartPositive = true; // 시작 방향(+/-)
    [SerializeField] private float wobbleIdleReturnSpeed = 360f; // 정지 시 0도로 복귀 속도

    private float wobbleAngle = 0f;                       // 현재 회전 각도
    private int wobbleSign = 1;                           // 회전 진행 방향(+1/-1)

    // ==============================
    // NAVMESH PATH (CreatureAI 스타일)
    // ==============================

    [Header("NavMesh 우회 경로(옵션)")]
    [SerializeField] private bool useNavMeshPathing = true; // NavMesh 경로 기반 우회 사용 여부
    [SerializeField] private NavMeshAgent navAgent;         // 경로 계산 전용 Agent(이동은 Rigidbody2D로 직접 처리)

    [Min(0f)][SerializeField] private float repathInterval = 0.25f;                // 경로 재계산 주기(초)
    [Min(0f)][SerializeField] private float navmeshSampleRadius = 1.0f;            // 현재 위치를 NavMesh 위로 스냅할 반경
    [Min(0f)][SerializeField] private float navmeshDestinationSampleRadius = 2.0f; // 목적지를 NavMesh 위로 스냅할 반경
    [SerializeField] private bool warpToNavMeshWhenOff = true;                     // NavMesh 밖이면 Warp로 복구할지 여부

    private float repathTimer = 0f;                        // 재경로 타이머
    private Vector3 sampledDestination3D;                  // NavMesh 위로 샘플된 목적지(3D)
    private bool hasSampledDestination = false;            // 샘플 목적지 유효 여부

    // ==============================
// MOVE/FACING LOCK (근접 공격 잠금)  // ✅ 추가
// ==============================

[Header("근접 공격 시 이동/방향 잠금")]
[SerializeField] private bool allowMoveAndFacingChange = true; // 여부변수1(이동+방향 변경 가능)
private Coroutine moveFacingLockRoutine;                       // 잠금 코루틴 핸들
private int moveFacingLockToken = 0;                           // 중복 요청 안전 토큰

// ==============================
// DIRECTION OFFSET (4방향 로컬 포지션) // ✅ 추가
// ==============================

[Header("방향별 로컬 포지션 적용")]
[SerializeField] private Transform directionOffsetTarget; // 방향에 따라 로컬포지션 바뀔 대상
[SerializeField] private Vector3 offsetUp;               // 위 방향 로컬 포지션
[SerializeField] private Vector3 offsetDown;             // 아래 방향 로컬 포지션
[SerializeField] private Vector3 offsetLeft;             // 왼쪽 방향 로컬 포지션
[SerializeField] private Vector3 offsetRight;            // 오른쪽 방향 로컬 포지션

// ==============================
// EXTERNAL FACING OVERRIDE (조준 방향 등 외부 방향 고정)  // ✅ 추가
// ==============================

[Header("외부 방향 오버라이드(조준 등)")]
private bool externalFacingActive = false;        // 외부 방향 적용 중인지
private bool externalFacingJustActivated = false; // 외부 방향 최초 적용 프레임인지
private Vector2 externalFacingDir = Vector2.right; // 외부에서 받은 방향 벡터(월드 기준)



    [Header("NavMesh 디버그(로그)")]
    [SerializeField] private bool debugNavStateLog = false; // NavMesh 상태 로그 출력 여부
    [SerializeField, Min(0f)] private float debugLogInterval = 0.5f; // 로그 출력 간격(초)
    private float debugLogTimer = 0f;                       // 로그 타이머

    

    // ==============================
    // RUNTIME STATE
    // ==============================

    private Vector2 destination;                             // 현재 목적지(월드 좌표)
    private bool hasDestination = false;                     // 목적지 유효 여부
    private bool isMoving = false;                           // 이동 실행 여부(외부에서 켜고 끔)
    private bool arrivedThisFrame = false;                   // 이번 프레임 도착 플래그

    private float currentSpeed = 0f;                         // (CreatureAI 방식) 현재 속도 값
    private Quaternion baseLocalRotation;                    // 시작 시 기준 회전(고정 유지용)

    // ==============================
    // UNITY
    // ==============================

    private void Reset() // 자동 할당
    {
        if (!rb) rb = GetComponent<Rigidbody2D>();                       // rb 자동 연결
        if (!sprite) sprite = GetComponentInChildren<SpriteRenderer>();  // sprite 자동 탐색
        if (!navAgent) navAgent = GetComponent<NavMeshAgent>();          // navAgent 자동 탐색
    }

    private void Awake() // 초기 세팅
    {
        if (!rb) rb = GetComponent<Rigidbody2D>();                       // rb 보장
        if (!sprite) sprite = GetComponentInChildren<SpriteRenderer>();  // sprite 보장

        baseLocalRotation = transform.localRotation;                     // 기준 회전 저장
        wobbleSign = wobbleStartPositive ? 1 : -1;                       // wobble 시작 방향 설정

        if (useNavMeshPathing && navAgent == null)                       // agent 자동 연결(있으면 사용)
            navAgent = GetComponent<NavMeshAgent>();                     // 같은 오브젝트에서 가져오기

        ApplyAgent2DSettings();                                          // (CreatureAI 방식) 2D 고정 세팅
        ApplyAgentStoppingDistance();                                    // agent stoppingDistance 반영
    }

    private void OnEnable() // 실행 중에도 값이 바뀌면 복구
    {
        ApplyAgent2DSettings();                                          // (CreatureAI 방식) 방어적 재세팅
        ApplyAgentStoppingDistance();                                    // 멈춤 거리 반영
    }

    private void Update() // 이동/도착/스프라이트/회전 처리
    {
        transform.localRotation = baseLocalRotation;                     // 어떤 경우에도 기준 회전 유지
        arrivedThisFrame = false;                                        // 매 프레임 도착 플래그 초기화

        // 이동 OFF 또는 목적지 없음
        if (!isMoving || !hasDestination)
        {
            currentSpeed = 0f;                                           // 속도 즉시 0
            if (rb) rb.linearVelocity = Vector2.zero;                    // 잔속도 제거

        // ✅ 추가: 정지 중이어도 조준 방향이 있으면 그 방향으로 이미지 갱신
        if (externalFacingActive) // 외부 방향 활성 상태면
            UpdateFacingByExternal(externalFacingDir); // 외부 방향 기준 이미지 적용
        else
            ResetFacingContext(); // 기존 정지 처리 유지

        UpdateWobble(); // 정지 상태 복귀 wobble
        return;
        }

        Vector2 pos = rb.position;                                       // 현재 위치

        // (1) 도착 판정은 최종 목적지(destination) 기준
        float arriveThreshold = arriveDistance + arriveEpsilon;          // 도착 임계값(여유 포함)
        float distToDest = Vector2.Distance(pos, destination);           // 최종 목적지까지 거리
        if (distToDest <= arriveThreshold)
        {
            currentSpeed = 0f;                                           // 속도 0
            rb.linearVelocity = Vector2.zero;                            // 속도 벡터 0

            isMoving = false;                                            // 이동 OFF
            arrivedThisFrame = true;                                     // 도착 플래그

            if (useNavMeshPathing && navAgent != null)                   // NavMesh 사용 중이면
                navAgent.ResetPath();                                    // (CreatureAI 방식) 경로 초기화

            ResetFacingContext();                                        // 도착으로 정지 시 방향 컨텍스트 초기화
            UpdateWobble();                                              // 정지 복귀 wobble
            return;
        }

        // (2) (CreatureAI 방식) agent 상태 갱신 + 목적지 재설정(주기/무경로 시)
        UpdateAgentStateAndRepath(pos);                                  // 워프/재경로/nextPosition 동기화

        // (3) (CreatureAI 방식) steeringTarget 기반 원본 방향 계산
        Vector2 desiredDir = ComputeDesiredMove(pos);                    // 의도 방향(정규화)

        // (4) 8방향 스냅(대각 속도 보정 포함)
        Vector2 snappedMove = Quantize8(desiredDir);                     // 이동 벡터(대각 보정 포함)

        // (5) 가속/감속으로 Rigidbody2D 이동 출력
        ApplyMovement(snappedMove);                                      // CreatureAI 스타일 velocity 제어

        // (6) 의도 방향 기반 4방향 스프라이트(기존 안정화 로직 유지)
    // ✅ 변경: 외부 방향이 활성화면 외부 방향 우선, 아니면 기존 이동 의도 방향 사용
    if (externalFacingActive) // 외부 방향이 있으면
        UpdateFacingByExternal(externalFacingDir); // 외부 방향 적용
    else
        UpdateFacingByIntent(desiredDir); // 기존 이동 방향 적용

    UpdateWobble(); // wobble 적용
    }

    // ==============================
    // PUBLIC API (AI에서 호출)
    // ==============================

    public void SetDestination(Vector2 worldPosition) // 목적지 설정 + (필요 시) agent 목적지 갱신
    {
        destination = worldPosition;                                   // 목적지 설정
        hasDestination = true;                                         // 목적지 유효

        repathTimer = repathInterval;                                  // 다음 Update에서 즉시 재경로 유도

        if (!useNavMeshPathing || navAgent == null) return;            // NavMesh 미사용이면 종료

        if (warpToNavMeshWhenOff)                                      // 현재 위치가 NavMesh 밖이면 복구 시도
            TryWarpAgentToNavMesh(rb ? rb.position : (Vector2)transform.position); // 현재 위치 복구
    }

public void SetMoving(bool enable)
{
    isMoving = enable;

    if (!isMoving)
    {
        currentSpeed = 0f;
        if (rb) rb.linearVelocity = Vector2.zero;

        // ✅ 수정: NavMesh 위에 있을 때만 ResetPath
        if (useNavMeshPathing && navAgent != null &&
            navAgent.enabled && navAgent.isOnNavMesh)
        {
            navAgent.ResetPath();
        }
    }
}

public void StopImmediate()
{
    currentSpeed = 0f;
    if (rb) rb.linearVelocity = Vector2.zero;
    isMoving = false;

    // ✅ 수정: 동일한 안전 가드
    if (useNavMeshPathing && navAgent != null &&
        navAgent.enabled && navAgent.isOnNavMesh)
    {
        navAgent.ResetPath();
    }
}


    public bool IsMoving() // 이동 중인지 반환
    {
        return isMoving;                                               // 이동 플래그 반환
    }

    public bool ConsumeArrivedFlag() // 도착 플래그 소비(한 번만 true)
    {
        if (!arrivedThisFrame) return false;                           // 도착 없으면 false
        arrivedThisFrame = false;                                      // 소비 후 초기화
        return true;                                                   // 도착 true
    }



    public void SetTarget(Vector2 targetPos) // (호환용) 외부에서 SetTarget 호출 시 목적지로 처리
    {
        SetDestination(targetPos);                                     // 기존 API 호환(목적지 지정)
    }

    // ==============================
    // NAVMESH (CreatureAI 방식 핵심)
    // ==============================

    private void ApplyAgent2DSettings() // agent 2D 고정 세팅
    {
        if (!useNavMeshPathing || navAgent == null) return;            // 가드

        navAgent.updatePosition = false;                               // Transform 자동 이동 금지
        navAgent.updateRotation = false;                               // 회전 자동 업데이트 금지
        navAgent.updateUpAxis = false;                                 // 2D(XY) 전제

        if (Mathf.Abs(navAgent.baseOffset) > Mathf.Epsilon)            // 2D 권장: baseOffset=0
            navAgent.baseOffset = 0f;                                  // baseOffset 0
    }

    private void ApplyAgentStoppingDistance() // agent stoppingDistance 반영
    {
        if (!useNavMeshPathing || navAgent == null) return;            // 가드
        navAgent.stoppingDistance = Mathf.Max(0.01f, arriveDistance);  // 멈춤 거리(최소값 보정)
    }

private void UpdateAgentStateAndRepath(Vector2 currentPos) // NavMeshAgent 상태 동기화 및 경로 보정
{
    if (!useNavMeshPathing) return;          // NavMesh 사용 안 하면 종료
    if (navAgent == null) return;            // Agent 없으면 종료
    if (!navAgent.enabled) return;           // 비활성 Agent면 종료

    // Rigidbody 위치를 Agent에 동기화
    navAgent.nextPosition = new Vector3(currentPos.x, currentPos.y, 0f);

    // ─────────────────────────────────────────
    // ✅ NavMesh 밖에 있을 경우 복구(Warp)
    // ─────────────────────────────────────────
    if (!navAgent.isOnNavMesh)
    {
        if (!warpToNavMeshWhenOff) return;   // 복구 옵션 꺼져 있으면 종료

        // 현재 위치 기준으로 NavMesh 샘플 시도
        if (TrySampleNavMeshPoint(currentPos, navmeshSampleRadius, out Vector3 onMeshPos))
        {
            navAgent.Warp(onMeshPos);        // NavMesh 위로 강제 이동
        }
        else
        {
            return; // 복구 실패 → 이번 프레임 경로 계산 중단
        }
    }

    // ─────────────────────────────────────────
    // 이후부터는 isOnNavMesh == true 보장
    // ─────────────────────────────────────────

    // 이동 중이고 목적지가 유효할 때만 경로 계산
    if (isMoving && hasDestination)
    {
        navAgent.SetDestination(destination); // 경로 재계산
    }
}


    private Vector2 ComputeDesiredMove(Vector2 currentPos) // steeringTarget 기반 이동 방향
    {
        // NavMesh 우회 비사용이면 직진 방향
        if (!useNavMeshPathing || navAgent == null)
            return (destination - currentPos).sqrMagnitude < 1e-6f ? Vector2.zero : (destination - currentPos).normalized;

        // agent가 유효 경로가 없으면(혹은 아직 계산 전이면) 목적지 직진 폴백
        if (!navAgent.hasPath || navAgent.pathStatus == NavMeshPathStatus.PathInvalid)
        {
            if (hasSampledDestination) // 샘플 목적지 폴백(기존 기능 유지)
            {
                Vector2 sd = new Vector2(sampledDestination3D.x, sampledDestination3D.y);
                return (sd - currentPos).sqrMagnitude < 1e-6f ? Vector2.zero : (sd - currentPos).normalized;
            }

            return (destination - currentPos).sqrMagnitude < 1e-6f ? Vector2.zero : (destination - currentPos).normalized;
        }

        // steeringTarget(다음 코너)로 이동 방향 결정
        Vector3 steer = navAgent.steeringTarget;                       // 다음 코너(우회 핵심)
        Vector2 dir = (new Vector2(steer.x, steer.y) - currentPos);     // 코너까지 벡터
        return (dir.sqrMagnitude < 1e-6f) ? Vector2.zero : dir.normalized;
    }

    private bool TrySampleNavMeshPoint(Vector2 pos2D, float radius, out Vector3 sampled3D) // 근처 NavMesh 점 찾기
    {
        NavMeshHit hit;                                                // 샘플 결과
        Vector3 query = new Vector3(pos2D.x, pos2D.y, 0f);              // 2D(XY) 기준 쿼리

        if (NavMesh.SamplePosition(query, out hit, radius, NavMesh.AllAreas)) // 근처 유효점 탐색
        {
            sampled3D = hit.position;                                  // 샘플된 NavMesh 위치
            return true;                                               // 성공
        }

        sampled3D = query;                                             // 실패 시 원본
        return false;                                                  // 실패
    }

    private void TryWarpAgentToNavMesh(Vector2 currentPos) // agent가 NavMesh 밖일 때 복구
    {
        if (!useNavMeshPathing || navAgent == null) return;            // 가드
        if (!navAgent.enabled) return;                                 // 가드

        if (TrySampleNavMeshPoint(currentPos, navmeshSampleRadius, out Vector3 onMesh)) // 근처 점 찾기
            navAgent.Warp(onMesh);                                     // NavMesh 위로 강제 이동
    }

    private void DebugNavState(Vector2 currentPos) // NavMesh 상태 로그(옵션)
    {
        if (!debugNavStateLog) return;                                 // 로그 OFF면 종료
        if (!useNavMeshPathing || navAgent == null) return;            // NavMesh 미사용/Agent 없음이면 종료

        debugLogTimer -= Time.deltaTime;                               // 타이머 감소
        if (debugLogTimer > 0f) return;                                // 아직 간격이 안 됐으면 종료
        debugLogTimer = debugLogInterval;                              // 다음 출력까지 간격 재설정

        Debug.Log($"[NavDebug] isOnNavMesh={navAgent.isOnNavMesh}");    // NavMesh 위 여부
        Debug.Log($"[NavDebug] hasPath={navAgent.hasPath} pathStatus={navAgent.pathStatus}"); // 경로 상태
        Debug.Log($"[NavDebug] sampled={hasSampledDestination} dest={destination} sampledDest={sampledDestination3D}"); // 목적지/샘플
        Debug.Log($"[NavDebug] pos={currentPos} steer={navAgent.steeringTarget} nextPos={navAgent.nextPosition}"); // 위치/조향
    }

    // ==============================
    // MOVE OUTPUT (CreatureAI 방식 가속/감속)
    // ==============================

private void ApplyMovement(Vector2 moveVec) // ✅ 잠금 상태면 이동 출력 차단
{
    if (!allowMoveAndFacingChange) // 여부변수1 OFF면
    {
        currentSpeed = 0f; // 속도 0
        if (rb) rb.linearVelocity = Vector2.zero; // 즉시 정지
        return; // 이동 출력 중단
    }

    float dt = Time.deltaTime; // 델타타임

    float targetSpeed = (moveVec == Vector2.zero) ? 0f : moveSpeed; // 목적 속력
    float rate = (targetSpeed > currentSpeed) ? accel : decel;      // 가감속 선택
    currentSpeed = Mathf.MoveTowards(currentSpeed, targetSpeed, rate * dt); // 속도 갱신

    rb.linearVelocity = moveVec * currentSpeed; // Rigidbody2D 속도 적용
}


    // ==============================
    // FACING (의도 방향 기반) - 기존 로직 유지
    // ==============================

private void UpdateFacingByIntent(Vector2 dirNormalized) // ✅ 잠금 상태면 방향 갱신 차단
{
    if (!allowMoveAndFacingChange) return; // 여부변수1 OFF면 방향 변경 금지

    Vector2 curDir8 = Quantize8Direction(dirNormalized); // 판정용 8방향
    bool curIsDiagonal = Mathf.Abs(curDir8.x) > 0.1f && Mathf.Abs(curDir8.y) > 0.1f; // 대각 여부

    Vector2 desired = ResolveDesiredCardinal(curDir8, curIsDiagonal); // 원하는 4방향 계산

    bool justStartedMoving = !wasMovingLastFrame || !hasLastMoveDir8; // 정지→이동 시작 판정
    TryApplyFacing(desired, justStartedMoving); // 적용 시도

    lastMoveDir8 = curDir8; // 이전 8방향 저장
    hasLastMoveDir8 = true; // 유효 처리
    wasMovingLastFrame = true; // 이동 중 기록
}


    private Vector2 ResolveDesiredCardinal(Vector2 curDir8, bool curIsDiagonal) // 판정만 담당(적용 X)
    {
        bool curIsHorizontal = Mathf.Abs(curDir8.x) > 0.9f && Mathf.Abs(curDir8.y) < 0.1f; // 좌/우
        bool curIsVertical = Mathf.Abs(curDir8.y) > 0.9f && Mathf.Abs(curDir8.x) < 0.1f;   // 상/하

        if (curIsHorizontal) return new Vector2(Mathf.Sign(curDir8.x), 0f); // 좌/우
        if (curIsVertical) return new Vector2(0f, Mathf.Sign(curDir8.y));   // 상/하

        if (!curIsDiagonal) return appliedCardinal;                    // 애매하면 현재 적용 방향 유지

        // 정지에서 바로 대각 시작 → 가로 우선
        if (!wasMovingLastFrame || !hasLastMoveDir8)
            return new Vector2(Mathf.Sign(curDir8.x), 0f);              // 가로 우선

        bool prevIsHorizontal = Mathf.Abs(lastMoveDir8.x) > 0.9f && Mathf.Abs(lastMoveDir8.y) < 0.1f; // 이전 좌/우
        bool prevIsVertical = Mathf.Abs(lastMoveDir8.y) > 0.9f && Mathf.Abs(lastMoveDir8.x) < 0.1f;   // 이전 상/하
        bool prevIsDiagonal = Mathf.Abs(lastMoveDir8.x) > 0.1f && Mathf.Abs(lastMoveDir8.y) > 0.1f;   // 이전 대각

        if (prevIsVertical) return new Vector2(Mathf.Sign(curDir8.x), 0f);   // 위/아래 + 가로 추가 → 가로
        if (prevIsHorizontal) return new Vector2(0f, Mathf.Sign(curDir8.y)); // 좌/우 + 세로 추가 → 세로
        if (prevIsDiagonal) return appliedCardinal;                           // 대각→대각 : 토글 억제(유지)

        return new Vector2(Mathf.Sign(curDir8.x), 0f);                        // 예외: 가로 우선
    }

    private void TryApplyFacing(Vector2 desired, bool immediate) // 적용 책임(히스테리시스)
    {
        if ((desired - appliedCardinal).sqrMagnitude < 0.0001f) return; // 동일 방향이면 종료

        if (immediate)                                                  // 이동 시작 프레임은 즉시 적용
        {
            CommitFacing(desired);                                      // 즉시 적용
            return;
        }

        bool timeOk = (Time.time - lastFacingChangeTime) >= minFacingSwitchInterval; // 최소 전환 간격 확인
        if (timeOk) CommitFacing(desired);                              // 시간 조건 만족 시 적용
    }

private void CommitFacing(Vector2 cardinal) // ✅ 스프라이트 변경 + 오프셋 적용
{
    appliedCardinal = cardinal;     // 적용 방향 갱신
    lastCardinal = cardinal;        // 스프라이트 적용용 갱신
    lastFacingChangeTime = Time.time; // 변경 시간 기록
    ApplySpriteByCardinal();        // 스프라이트 변경 호출

    ApplyDirectionOffsetByFacing(); // ✅ 방향별 로컬 포지션 즉시 적용
}

private void ApplyDirectionOffsetByFacing() // ✅ 현재 방향에 맞게 로컬 포지션 적용
{
    if (!directionOffsetTarget) return; // 대상 없으면 종료

    if (appliedCardinal.x > 0.5f) directionOffsetTarget.localPosition = offsetRight; // 오른쪽
    else if (appliedCardinal.x < -0.5f) directionOffsetTarget.localPosition = offsetLeft; // 왼쪽
    else if (appliedCardinal.y > 0.5f) directionOffsetTarget.localPosition = offsetUp; // 위
    else directionOffsetTarget.localPosition = offsetDown; // 아래
}



    private void ApplySpriteByCardinal() // lastCardinal 기준 스프라이트 적용
    {
        if (!sprite) return;                                            // 가드

        if (lastCardinal.x > 0.5f) sprite.sprite = rightSprite;         // 오른쪽
        else if (lastCardinal.x < -0.5f) sprite.sprite = leftSprite;    // 왼쪽
        else if (lastCardinal.y > 0.5f) sprite.sprite = upSprite;       // 위
        else sprite.sprite = downSprite;                                // 아래(기본)
    }

    private void ResetFacingContext() // 정지 시 다음 이동을 "새 시작"으로 취급
    {
        wasMovingLastFrame = false;                                     // 정지 상태 기록
        hasLastMoveDir8 = false;                                        // 이전 이동 방향 무효화
    }

    private Vector2 Quantize8Direction(Vector2 dir) // 판정용 8방향 스냅(대각 보정 X)
    {
        if (dir.sqrMagnitude < 1e-6f) return Vector2.zero;              // 거의 0이면 정지

        float angle = Mathf.Atan2(dir.y, dir.x);                        // 라디안 각도
        float step = Mathf.PI / 4f;                                     // 45도
        int sector = Mathf.RoundToInt(angle / step);                    // 가장 가까운 섹터
        float snappedAngle = sector * step;                             // 스냅 각도

        return new Vector2(Mathf.Cos(snappedAngle), Mathf.Sin(snappedAngle)); // 단위 벡터 반환
    }

    // ==============================
    // MOVE (8 DIR SNAP)
    // ==============================

    private Vector2 Quantize8(Vector2 dir) // 이동용 8방향 스냅(대각 속도 보정 포함)
    {
        if (dir.sqrMagnitude < 1e-6f) return Vector2.zero;              // 거의 0이면 정지

        float angle = Mathf.Atan2(dir.y, dir.x);                        // 라디안 각도
        float step = Mathf.PI / 4f;                                     // 45도
        int sector = Mathf.RoundToInt(angle / step);                    // 가장 가까운 섹터
        float snappedAngle = sector * step;                             // 스냅 각도

        Vector2 q = new Vector2(Mathf.Cos(snappedAngle), Mathf.Sin(snappedAngle)); // 스냅 방향(기본 길이 1)
        bool isDiagonal = Mathf.Abs(q.x) > 0.1f && Mathf.Abs(q.y) > 0.1f;           // 대각 여부
        if (isDiagonal) q *= diagonalScale;                             // 대각 보정(정규화하지 않음)

        return q;                                                       // 최종 스냅 벡터(대각이면 길이 < 1)
    }

    // ==============================
    // WOBBLE
    // ==============================

    private void UpdateWobble() // 이동 중 진자 회전, 정지 시 0도로 복귀
    {
        if (!wobbleTarget) return;                                      // 대상 없으면 미적용

        float dt = Time.deltaTime;                                      // 델타타임
        bool movingNow = rb != null && rb.linearVelocity.sqrMagnitude > 0.001f; // 이동 중 여부

        if (movingNow)
        {
            wobbleAngle += wobbleSpeed * wobbleSign * dt;               // 각도 진행

            if (wobbleAngle > wobbleAmplitude)                          // 상한 도달
            {
                wobbleAngle = wobbleAmplitude;                          // 클램프
                wobbleSign = -1;                                        // 방향 반전
            }
            else if (wobbleAngle < -wobbleAmplitude)                    // 하한 도달
            {
                wobbleAngle = -wobbleAmplitude;                         // 클램프
                wobbleSign = +1;                                        // 방향 반전
            }
        }
        else
        {
            if (Mathf.Abs(wobbleAngle) > 0.01f)                         // 정지 시 0도로 복귀
            {
                float sgn = Mathf.Sign(-wobbleAngle);                   // 0도 방향
                float step = wobbleIdleReturnSpeed * dt * sgn;          // 이동량
                float next = wobbleAngle + step;                        // 다음 각도

                if (Mathf.Sign(wobbleAngle) != Mathf.Sign(next) || Mathf.Abs(next) < 0.01f) // 0도 통과
                    wobbleAngle = 0f;                                   // 0도 고정
                else
                    wobbleAngle = next;                                 // 점진 복귀
            }
        }

        Vector3 e = wobbleTarget.localEulerAngles;                      // 현재 로컬 오일러
        switch (wobbleAxis)                                             // 선택 축에 적용
        {
            case WobbleAxis.X: e.x = wobbleAngle; break;                // X축
            case WobbleAxis.Y: e.y = wobbleAngle; break;                // Y축
            case WobbleAxis.Z: e.z = wobbleAngle; break;                // Z축(2D 일반)
        }
        wobbleTarget.localEulerAngles = e;                              // 최종 적용
    }

    public bool TrySampleDestinationOnNavMesh(Vector2 desiredWorldPos, out Vector2 sampledWorldPos) // 목적지 NavMesh 보정용
{
    sampledWorldPos = desiredWorldPos; // 기본값(폴백)

    if (!useNavMeshPathing || navAgent == null) // NavMesh 미사용/Agent 없음
        return false;                            // 샘플 불가

    Vector3 sampled3D; // 샘플 결과(3D)
    bool ok = TrySampleNavMeshPoint(desiredWorldPos, navmeshDestinationSampleRadius, out sampled3D); // 내부 샘플 호출
    if (!ok) return false; // 실패면 false

    sampledWorldPos = new Vector2(sampled3D.x, sampled3D.y); // 2D로 변환
    return true; // 성공
}

public void RequestMeleeMoveFacingLock(float duration) // ✅ 근접 공격 시 잠금 요청
{
    if (duration <= 0f) return; // 유효 시간 아니면 종료

    allowMoveAndFacingChange = false; // 이동/방향 변경 차단
    moveFacingLockToken++;           // 토큰 증가(이전 코루틴 무효화)

    if (moveFacingLockRoutine != null) // 기존 코루틴 있으면
        StopCoroutine(moveFacingLockRoutine); // 중단

    moveFacingLockRoutine = StartCoroutine(Co_MoveFacingUnlock(moveFacingLockToken, duration)); // 해제 예약
}

private IEnumerator Co_MoveFacingUnlock(int token, float duration) // ✅ 시간이 지나면 잠금 해제
{
    yield return new WaitForSeconds(duration); // 지정 시간 대기

    if (token != moveFacingLockToken) yield break; // 더 최신 요청이 있으면 무시

    allowMoveAndFacingChange = true; // 이동/방향 변경 복구
    moveFacingLockRoutine = null;    // 코루틴 핸들 해제
}

// ==============================
// PUBLIC API (외부 조준 방향 오버라이드)  // ✅ 추가
// ==============================

public void SetExternalFacingOverride(Vector2 worldDir) // 외부 방향 오버라이드 설정(조준용)
{
    if (worldDir.sqrMagnitude < 0.000001f) return; // 방향이 거의 0이면 무시

    externalFacingActive = true; // 외부 방향 활성화
    externalFacingDir = worldDir.normalized; // 방향 정규화 저장
    externalFacingJustActivated = true; // 최초 적용 플래그 ON
}

public void ClearExternalFacingOverride() // 외부 방향 오버라이드 해제(이동 방향으로 복귀)
{
    externalFacingActive = false; // 외부 방향 비활성화
    externalFacingJustActivated = false; // 최초 플래그 OFF
}

// ==============================
// INTERNAL (외부 방향을 4방향으로 변환 후 적용)  // ✅ 추가
// ==============================

private void UpdateFacingByExternal(Vector2 dirNormalized) // 외부 방향 기반 이미지 갱신
{
    if (!allowMoveAndFacingChange) return; // 근접 잠금 상태면 방향 변경 금지(기존 규칙 유지)

    Vector2 desired = ResolveCardinalFromDir(dirNormalized); // 상/하/좌/우 결정

    bool immediate = externalFacingJustActivated; // 처음 적용이면 즉시 반영
    TryApplyFacing(desired, immediate); // 기존 히스테리시스 적용 함수 재사용
    externalFacingJustActivated = false; // 최초 적용 플래그 소모
}

private Vector2 ResolveCardinalFromDir(Vector2 dir) // 90도 부채꼴(상/우/하/좌) 판정
{
    float yaw = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg; // 각도 계산
    if (yaw < 0f) yaw += 360f; // 0~360 정규화

    // Right: (315~360] or (0~45]
    if (yaw > 315f || yaw <= 45f) return Vector2.right; // 오른쪽
    // Up: (45~135]
    if (yaw > 45f && yaw <= 135f) return Vector2.up; // 위
    // Left: (135~225]
    if (yaw > 135f && yaw <= 225f) return Vector2.left; // 왼쪽
    // Down: (225~315]
    return Vector2.down; // 아래
}

public bool IsNavMeshLineBlocked(Vector2 fromWorldPos, Vector2 toWorldPos) // ✅ NavMesh 직선 차단 여부(비베이크/홀 포함)
{
    if (!useNavMeshPathing || navAgent == null) // NavMesh 미사용이면(기존 동작 유지)
        return false;                            // 차단 검사 안 함(=막힘 아님)

    // 시작/끝을 NavMesh 위로 샘플링(직선 검사 안정화)
    if (!TrySampleNavMeshPoint(fromWorldPos, navmeshSampleRadius, out Vector3 fromOnMesh)) // 시작점 샘플 실패
        return true;                                                                  // ✅ 샘플 실패는 '막힘'으로 간주(멈춰 사격 방지)

    if (!TrySampleNavMeshPoint(toWorldPos, navmeshDestinationSampleRadius, out Vector3 toOnMesh)) // 끝점 샘플 실패
        return true;                                                                        // ✅ 타겟이 NavMesh 밖이면 '막힘' 처리

    // NavMesh 상 직선이 중간에 경계/홀에 걸리면 hit가 발생(=차단)
    bool blocked = NavMesh.Raycast(fromOnMesh, toOnMesh, out NavMeshHit hit, NavMesh.AllAreas); // 직선 차단 검사
    return blocked;                                                                            // true면 중간에 비베이크/단절 존재
}

}
