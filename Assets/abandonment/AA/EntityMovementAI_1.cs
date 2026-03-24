using UnityEngine;     // 유니티 기본
using UnityEngine.AI;  // NavMesh 경로 계산용

[AddComponentMenu("CreatureAI/Entity Movement Simple AI")] // 인스펙터 메뉴 경로
[RequireComponent(typeof(Rigidbody2D))]                    // Rigidbody2D 필수
public class EntityMovementAI_1 : MonoBehaviour
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
    [Min(0f)][SerializeField] private float moveSpeed = 3.5f;          // 최대 이동 속도
    [Range(0f, 1f)][SerializeField] private float diagonalScale = 0.9f; // 대각 이동 속도 보정(0~1)

    [Header("도착 판정(유하게)")]
    [Min(0f)][SerializeField] private float arriveDistance = 0.25f;    // 기본 도착 거리
    [Min(0f)][SerializeField] private float arriveEpsilon = 0.15f;     // 추가 여유(유한 판정)

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
    private Vector2 lastCardinal = Vector2.right;         // 기존 스프라이트 적용용(4방향)

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
    // NAVMESH PATH (steeringTarget 기반)
    // ==============================

    [Header("NavMesh 우회 경로(옵션)")]
    [SerializeField] private bool useNavMeshPathing = true; // NavMesh 경로 기반 우회 사용 여부
    [SerializeField] private NavMeshAgent navAgent;         // 경로 계산 전용 Agent(이동은 Rigidbody2D로 직접 처리)

    [Min(0f)][SerializeField] private float repathInterval = 0.25f;               // 경로 재계산 주기(초)
    [Min(0f)][SerializeField] private float navmeshSampleRadius = 1.0f;           // 현재 위치를 NavMesh 위로 스냅할 반경
    [Min(0f)][SerializeField] private float navmeshDestinationSampleRadius = 2.0f;// 목적지를 NavMesh 위로 스냅할 반경
    [SerializeField] private bool warpToNavMeshWhenOff = true;                    // NavMesh 밖이면 Warp로 복구할지 여부

    private float repathTimer = 0f;                         // 재경로 타이머
    private Vector3 sampledDestination3D;                   // NavMesh 위로 샘플된 목적지(3D)
    private bool hasSampledDestination = false;             // 샘플 목적지 유효 여부

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

    private Quaternion baseLocalRotation;                    // 시작 시 기준 회전(고정 유지용)

    // ==============================
    // UNITY
    // ==============================

    private void Reset() // 자동 할당
    {
        if (!rb) rb = GetComponent<Rigidbody2D>();                  // rb 자동 연결
        if (!sprite) sprite = GetComponentInChildren<SpriteRenderer>(); // sprite 자동 탐색
    }

    private void Awake() // 초기 세팅
    {
        if (!rb) rb = GetComponent<Rigidbody2D>();                  // rb 보장
        if (!sprite) sprite = GetComponentInChildren<SpriteRenderer>(); // sprite 보장

        baseLocalRotation = transform.localRotation;                // 기준 회전 저장
        wobbleSign = wobbleStartPositive ? 1 : -1;                  // wobble 시작 방향 설정

        if (useNavMeshPathing && navAgent == null)                  // agent 자동 연결(있으면 사용)
            navAgent = GetComponent<NavMeshAgent>();                // 같은 오브젝트에서 가져오기

        if (useNavMeshPathing && navAgent != null)                  // Agent를 경로 계산 전용으로 설정
        {
            navAgent.updatePosition = false;                        // Transform 자동 이동 금지
            navAgent.updateRotation = false;                        // 회전 자동 업데이트 금지
            navAgent.updateUpAxis = false;                          // 2D(XY) 전제
            navAgent.nextPosition = transform.position;             // 최초 동기화
        }
    }

    private void Update() // 이동/도착/스프라이트/회전 처리
    {
        transform.localRotation = baseLocalRotation;                // 어떤 경우에도 기준 회전 유지

        arrivedThisFrame = false;                                   // 매 프레임 도착 플래그 초기화

        if (!isMoving || !hasDestination)                           // 이동 OFF 또는 목적지 없음
        {
            ResetFacingContext();                                   // 정지로 전환 시 방향 컨텍스트 초기화
            UpdateWobble();                                         // 정지 상태 복귀 wobble
            return;
        }

        Vector2 pos = rb.position;                                  // 현재 위치

        // (1) 이동 방향은 NavMesh steeringTarget(코너) 기준으로 결정
        Vector2 moveTarget = GetMoveTargetByAgent(pos);             // steeringTarget 기반 타겟(우회 핵심)
        Vector2 toMove = moveTarget - pos;                          // 이동 타겟까지 벡터

        // (2) 도착 판정은 “최종 목적지(destination)” 기준으로 수행(중간 코너에서 멈춤 방지)
        Vector2 toDest = destination - pos;                         // 최종 목적지까지 벡터
        float distToDest = toDest.magnitude;                        // 최종 목적지까지 거리

        float arriveThreshold = arriveDistance + arriveEpsilon;     // 도착 임계값(여유 포함)
        if (distToDest <= arriveThreshold)                          // 최종 목적지 도착
        {
            rb.linearVelocity = Vector2.zero;                       // 속도 0
            isMoving = false;                                       // 이동 OFF
            arrivedThisFrame = true;                                // 도착 플래그

            ResetFacingContext();                                   // 도착으로 정지 시 방향 컨텍스트 초기화
            UpdateWobble();                                         // 정지 복귀 wobble
            return;
        }

        float distToMove = toMove.magnitude;                        // 이동 타겟까지 거리
        Vector2 dir = (distToMove > 1e-6f) ? (toMove / distToMove) : Vector2.zero; // 이동 의도 방향

        Vector2 snappedMove = Quantize8(dir);                       // 8방향 스냅(대각 보정 포함)
        rb.linearVelocity = snappedMove * moveSpeed;                // Rigidbody2D 속도 적용

        UpdateFacingByIntent(dir);                                  // 의도 방향 기반으로 4방향 스프라이트 확정
        UpdateWobble();                                             // 이동 wobble
    }

    // ==============================
    // PUBLIC API (AI에서 호출)
    // ==============================

    public void SetDestination(Vector2 worldPosition) // 목적지 설정 + NavMesh 목적지 갱신
    {
        destination = worldPosition;                                  // 목적지 설정
        hasDestination = true;                                        // 목적지 유효
        repathTimer = 0f;                                             // 목적지 변경 시 즉시 재경로 유도

        if (!useNavMeshPathing || navAgent == null) return;           // NavMesh 미사용이면 종료

        if (warpToNavMeshWhenOff)                                     // 현재 위치가 NavMesh 밖이면 복구 시도
            TryWarpAgentToNavMesh(rb ? rb.position : (Vector2)transform.position); // 현재 위치 복구

        // NavMesh 위에 올라온 상태에서만 SetDestination 호출(예외 방지)
        if (!navAgent.enabled || !navAgent.isOnNavMesh) return;       // NavMesh 위가 아니면 경로 요청 보류

        hasSampledDestination = TrySampleNavMeshPoint(destination, navmeshDestinationSampleRadius, out sampledDestination3D); // 목적지 스냅
        navAgent.SetDestination(hasSampledDestination ? sampledDestination3D : new Vector3(destination.x, destination.y, 0f)); // 목적지 갱신
    }

    public void SetMoving(bool enable) // 이동 실행 여부 설정
    {
        isMoving = enable;                                            // 이동 on/off
        if (!isMoving && rb) rb.linearVelocity = Vector2.zero;        // 끌 때 즉시 정지
    }

    public bool IsMoving() // 이동 중인지 반환
    {
        return isMoving;                                              // 이동 플래그 반환
    }

    public bool ConsumeArrivedFlag() // 도착 플래그 소비(한 번만 true)
    {
        if (!arrivedThisFrame) return false;                          // 도착 없으면 false
        arrivedThisFrame = false;                                     // 소비 후 초기화
        return true;                                                  // 도착 true
    }

    public void StopImmediate() // 즉시 정지(속도/목적지 유지)
    {
        if (rb) rb.linearVelocity = Vector2.zero;                     // 즉시 정지
        isMoving = false;                                             // 이동 OFF
    }

    public void SetTarget(Vector2 targetPos) // (호환용) 외부에서 SetTarget 호출 시 목적지로 처리
    {
        SetDestination(targetPos);                                    // 기존 API 호환(목적지 지정)
    }

    // ==============================
    // FACING (의도 방향 기반)
    // ==============================

    private void UpdateFacingByIntent(Vector2 dirNormalized) // 의도 방향 기반(속도 흔들림 영향 최소화)
    {
        Vector2 curDir8 = Quantize8Direction(dirNormalized);          // 판정용 8방향(보정 X)
        bool curIsDiagonal = Mathf.Abs(curDir8.x) > 0.1f && Mathf.Abs(curDir8.y) > 0.1f; // 대각 여부

        Vector2 desired = ResolveDesiredCardinal(curDir8, curIsDiagonal); // 원하는 4방향 계산

        bool justStartedMoving = !wasMovingLastFrame || !hasLastMoveDir8; // 정지→이동 시작 판정
        TryApplyFacing(desired, justStartedMoving);                   // 조건 만족 시에만 CommitFacing 호출

        lastMoveDir8 = curDir8;                                       // 이전 8방향 저장
        hasLastMoveDir8 = true;                                       // 유효 처리
        wasMovingLastFrame = true;                                    // 이동 중 기록
    }

    private Vector2 ResolveDesiredCardinal(Vector2 curDir8, bool curIsDiagonal) // 판정만 담당(적용 X)
    {
        bool curIsHorizontal = Mathf.Abs(curDir8.x) > 0.9f && Mathf.Abs(curDir8.y) < 0.1f; // 좌/우
        bool curIsVertical   = Mathf.Abs(curDir8.y) > 0.9f && Mathf.Abs(curDir8.x) < 0.1f; // 상/하

        if (curIsHorizontal) return new Vector2(Mathf.Sign(curDir8.x), 0f); // 좌/우
        if (curIsVertical)   return new Vector2(0f, Mathf.Sign(curDir8.y)); // 상/하

        if (!curIsDiagonal) return appliedCardinal;                  // 애매하면 현재 적용 방향 유지

        // 정지에서 바로 대각 시작 → 가로 우선(요구사항)
        if (!wasMovingLastFrame || !hasLastMoveDir8)
            return new Vector2(Mathf.Sign(curDir8.x), 0f);            // 가로 우선

        bool prevIsHorizontal = Mathf.Abs(lastMoveDir8.x) > 0.9f && Mathf.Abs(lastMoveDir8.y) < 0.1f; // 이전 좌/우
        bool prevIsVertical   = Mathf.Abs(lastMoveDir8.y) > 0.9f && Mathf.Abs(lastMoveDir8.x) < 0.1f; // 이전 상/하
        bool prevIsDiagonal   = Mathf.Abs(lastMoveDir8.x) > 0.1f && Mathf.Abs(lastMoveDir8.y) > 0.1f; // 이전 대각

        if (prevIsVertical)   return new Vector2(Mathf.Sign(curDir8.x), 0f); // 위/아래 + 가로 추가 → 가로
        if (prevIsHorizontal) return new Vector2(0f, Mathf.Sign(curDir8.y)); // 좌/우 + 세로 추가 → 세로
        if (prevIsDiagonal)   return appliedCardinal;                // 대각→대각 : 토글 억제(유지)

        return new Vector2(Mathf.Sign(curDir8.x), 0f);                // 예외: 가로 우선
    }

    private void TryApplyFacing(Vector2 desired, bool immediate) // 적용 책임(히스테리시스)
    {
        if ((desired - appliedCardinal).sqrMagnitude < 0.0001f) return; // 동일 방향이면 종료

        if (immediate)                                              // 이동 시작 프레임은 즉시 적용
        {
            CommitFacing(desired);                                  // 즉시 적용
            return;
        }

        bool timeOk = (Time.time - lastFacingChangeTime) >= minFacingSwitchInterval; // 최소 전환 간격 확인
        if (timeOk) CommitFacing(desired);                          // 시간 조건 만족 시 적용
    }

    private void CommitFacing(Vector2 cardinal) // 실제 스프라이트 변경은 여기서만 호출
    {
        appliedCardinal = cardinal;                                 // 적용 방향 갱신
        lastCardinal = cardinal;                                    // 스프라이트 적용용 갱신
        lastFacingChangeTime = Time.time;                           // 변경 시간 기록
        ApplySpriteByCardinal();                                    // 스프라이트 변경 호출(1곳)
    }

    private void ApplySpriteByCardinal() // lastCardinal 기준 스프라이트 적용
    {
        if (!sprite) return;                                        // 가드

        if (lastCardinal.x > 0.5f) sprite.sprite = rightSprite;     // 오른쪽
        else if (lastCardinal.x < -0.5f) sprite.sprite = leftSprite;// 왼쪽
        else if (lastCardinal.y > 0.5f) sprite.sprite = upSprite;   // 위
        else sprite.sprite = downSprite;                            // 아래(기본)
    }

    private void ResetFacingContext() // 정지 시 다음 이동을 "새 시작"으로 취급
    {
        wasMovingLastFrame = false;                                 // 정지 상태 기록
        hasLastMoveDir8 = false;                                    // 이전 이동 방향 무효화
    }

    private Vector2 Quantize8Direction(Vector2 dir) // 판정용 8방향 스냅(대각 보정 X)
    {
        if (dir.sqrMagnitude < 1e-6f) return Vector2.zero;          // 거의 0이면 정지

        float angle = Mathf.Atan2(dir.y, dir.x);                    // 라디안 각도
        float step = Mathf.PI / 4f;                                 // 45도
        int sector = Mathf.RoundToInt(angle / step);                // 가장 가까운 섹터
        float snappedAngle = sector * step;                         // 스냅 각도

        return new Vector2(Mathf.Cos(snappedAngle), Mathf.Sin(snappedAngle)); // 단위 벡터 반환
    }

    // ==============================
    // MOVE (8 DIR SNAP)
    // ==============================

    private Vector2 Quantize8(Vector2 dir) // 이동용 8방향 스냅(대각 속도 보정 포함)
    {
        if (dir.sqrMagnitude < 1e-6f) return Vector2.zero;          // 거의 0이면 정지

        float angle = Mathf.Atan2(dir.y, dir.x);                    // 라디안 각도
        float step = Mathf.PI / 4f;                                 // 45도
        int sector = Mathf.RoundToInt(angle / step);                // 가장 가까운 섹터
        float snappedAngle = sector * step;                         // 스냅 각도

        Vector2 q = new Vector2(Mathf.Cos(snappedAngle), Mathf.Sin(snappedAngle)); // 스냅 방향
        bool isDiagonal = Mathf.Abs(q.x) > 0.1f && Mathf.Abs(q.y) > 0.1f;           // 대각 여부
        if (isDiagonal) q *= diagonalScale;                         // 대각 보정(정규화하지 않음)

        return q;                                                   // 최종 스냅 벡터
    }

    // ==============================
    // NAVMESH (steeringTarget)
    // ==============================

    private Vector2 GetMoveTargetByAgent(Vector2 currentPos) // agent.steeringTarget을 따라가게 하는 타겟 결정
    {
        if (!useNavMeshPathing || navAgent == null)                 // NavMesh 우회 비사용이면 직진
            return destination;

        if (!hasDestination)                                        // 목적지 없으면 직진(실질적 이동 없음)
            return destination;

        navAgent.nextPosition = new Vector3(currentPos.x, currentPos.y, 0f); // agent 위치 동기화

        if (warpToNavMeshWhenOff && navAgent.enabled && !navAgent.isOnNavMesh) // NavMesh 밖이면
            TryWarpAgentToNavMesh(currentPos);                      // 근처 NavMesh 점으로 워프

        repathTimer -= Time.deltaTime;                              // 타이머 감소
        if (repathTimer <= 0f && navAgent.enabled && navAgent.isOnNavMesh) // 갱신 타이밍 + 유효 상태
        {
            hasSampledDestination = TrySampleNavMeshPoint(destination, navmeshDestinationSampleRadius, out sampledDestination3D); // 목적지 스냅 갱신
            navAgent.SetDestination(hasSampledDestination ? sampledDestination3D : new Vector3(destination.x, destination.y, 0f)); // 목적지 갱신
            repathTimer = repathInterval;                           // 다음 갱신까지 대기
        }

        DebugNavState(currentPos);                                  // 디버그 로그(옵션)

        if (navAgent.pathStatus == NavMeshPathStatus.PathInvalid)   // 경로 무효
        {
            if (hasSampledDestination)                              // 샘플 목적지 있으면 그쪽으로(최소한의 폴백)
                return new Vector2(sampledDestination3D.x, sampledDestination3D.y);

            return destination;                                     // 최후 폴백
        }

        Vector3 steer = navAgent.steeringTarget;                    // 다음 코너(우회 핵심)
        return new Vector2(steer.x, steer.y);                        // 2D 타겟 반환
    }

    private bool TrySampleNavMeshPoint(Vector2 pos2D, float radius, out Vector3 sampled3D) // 근처 NavMesh 점 찾기
    {
        NavMeshHit hit;                                             // 샘플 결과
        Vector3 query = new Vector3(pos2D.x, pos2D.y, 0f);           // 2D(XY) 기준 쿼리

        if (NavMesh.SamplePosition(query, out hit, radius, NavMesh.AllAreas)) // 근처 유효점 탐색
        {
            sampled3D = hit.position;                               // 샘플된 NavMesh 위치
            return true;                                            // 성공
        }

        sampled3D = query;                                          // 실패 시 원본
        return false;                                               // 실패
    }

    private void TryWarpAgentToNavMesh(Vector2 currentPos) // agent가 NavMesh 밖일 때 복구
    {
        if (navAgent == null) return;                               // 가드

        if (TrySampleNavMeshPoint(currentPos, navmeshSampleRadius, out Vector3 onMesh)) // 근처 점 찾기
            navAgent.Warp(onMesh);                                  // NavMesh 위로 강제 이동
    }

    private void DebugNavState(Vector2 currentPos) // NavMesh 상태 로그(옵션)
    {
        if (!debugNavStateLog) return;                              // 로그 OFF면 종료
        if (!useNavMeshPathing || navAgent == null) return;         // NavMesh 미사용/Agent 없음이면 종료

        debugLogTimer -= Time.deltaTime;                            // 타이머 감소
        if (debugLogTimer > 0f) return;                             // 아직 간격이 안 됐으면 종료
        debugLogTimer = debugLogInterval;                           // 다음 출력까지 간격 재설정

        Debug.Log($"[NavDebug] isOnNavMesh={navAgent.isOnNavMesh}"); // NavMesh 위 여부
        Debug.Log($"[NavDebug] pathStatus={navAgent.pathStatus}");   // 경로 상태
        Debug.Log($"[NavDebug] sampled={hasSampledDestination} dest={destination} sampledDest={sampledDestination3D}"); // 목적지/샘플
        Debug.Log($"[NavDebug] pos={currentPos} steer={navAgent.steeringTarget} nextPos={navAgent.nextPosition}"); // 위치/조향
    }

    // ==============================
    // WOBBLE
    // ==============================

    private void UpdateWobble() // 이동 중 진자 회전, 정지 시 0도로 복귀
    {
        if (!wobbleTarget) return;                                  // 대상 없으면 미적용

        float dt = Time.deltaTime;                                  // 델타타임
        bool movingNow = rb != null && rb.linearVelocity.sqrMagnitude > 0.001f; // 이동 중 여부

        if (movingNow)
        {
            wobbleAngle += wobbleSpeed * wobbleSign * dt;           // 각도 진행

            if (wobbleAngle > wobbleAmplitude)                      // 상한 도달
            {
                wobbleAngle = wobbleAmplitude;                      // 클램프
                wobbleSign = -1;                                    // 방향 반전
            }
            else if (wobbleAngle < -wobbleAmplitude)                // 하한 도달
            {
                wobbleAngle = -wobbleAmplitude;                     // 클램프
                wobbleSign = +1;                                    // 방향 반전
            }
        }
        else
        {
            if (Mathf.Abs(wobbleAngle) > 0.01f)                     // 정지 시 0도로 복귀
            {
                float sgn = Mathf.Sign(-wobbleAngle);               // 0도 방향
                float step = wobbleIdleReturnSpeed * dt * sgn;      // 이동량
                float next = wobbleAngle + step;                    // 다음 각도

                if (Mathf.Sign(wobbleAngle) != Mathf.Sign(next) || Mathf.Abs(next) < 0.01f) // 0도 통과
                    wobbleAngle = 0f;                               // 0도 고정
                else
                    wobbleAngle = next;                             // 점진 복귀
            }
        }

        Vector3 e = wobbleTarget.localEulerAngles;                  // 현재 로컬 오일러
        switch (wobbleAxis)                                         // 선택 축에 적용
        {
            case WobbleAxis.X: e.x = wobbleAngle; break;            // X축
            case WobbleAxis.Y: e.y = wobbleAngle; break;            // Y축
            case WobbleAxis.Z: e.z = wobbleAngle; break;            // Z축(2D 일반)
        }
        wobbleTarget.localEulerAngles = e;                          // 최종 적용
    }
}
