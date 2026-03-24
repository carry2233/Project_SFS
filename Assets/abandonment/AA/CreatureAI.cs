using UnityEngine;                    // 유니티 기본
using UnityEngine.AI;                 // NavMeshAgent
using System;                         // Math 유틸

[AddComponentMenu("CreatureAI")] // 인스펙터 메뉴 경로
[RequireComponent(typeof(Rigidbody2D))]                     // Rigidbody2D 필수
[RequireComponent(typeof(NavMeshAgent))]                    // NavMeshAgent 필수
public class CreatureAI : MonoBehaviour
{
    // ─────────────────────────────────────────────────────────────────────────────
    // 0) 내부 타입/유틸
    // ─────────────────────────────────────────────────────────────────────────────

    public enum AIState { Idle, Chase, Arrive }           // ✅ 간단 상태 머신
    public enum WobbleAxis { X, Y, Z }                    // ✅ 회전 축 선택(피치/요/롤)

    // ─────────────────────────────────────────────────────────────────────────────
    // 1) 참조
    // ─────────────────────────────────────────────────────────────────────────────

    [Header("참조")]
    [SerializeField] private Rigidbody2D rb;              // ✅ 이동용 Rigidbody2D
    [SerializeField] private NavMeshAgent agent;          // ✅ 경로계산용 NavMeshAgent(위치/회전 갱신은 끔)
    [SerializeField] private SpriteRenderer sprite;       // ✅ 방향 스프라이트 표시용
    [SerializeField] public Transform target;


    // ─────────────────────────────────────────────────────────────────────────────
    // 2) 이동 설정
    // ─────────────────────────────────────────────────────────────────────────────

    [Header("이동 설정")]
    [Min(0f)][SerializeField] private float moveSpeed = 3.5f;   // ✅ 최대 이동 속도(유닛/초)
    [Min(0f)][SerializeField] private float accel = 30f;        // ✅ 가속도(속력/초)
    [Min(0f)][SerializeField] private float decel = 30f;        // ✅ 감속도(속력/초)
    [Range(0f, 1f)][SerializeField] private float diagonalScale = 0.9f; // ✅ 대각 속도 보정(1=그대로)

    [Header("경로/재탐색")]
    [Min(0.05f)][SerializeField] private float repathInterval = 0.25f; // ✅ 경로 재계산 간격(초)
    [SerializeField] private float arriveDistance = 1.0f;               // ✅ 목표 접근 종료 거리
    [SerializeField] private float sampleRadius = 2.0f;                 // ✅ NavMesh.SamplePosition 반경

    // ─────────────────────────────────────────────────────────────────────────────
    // 3) 스프라이트 전환(4방향)
    // ─────────────────────────────────────────────────────────────────────────────

    [Header("4방향 스프라이트")]
    [SerializeField] private Sprite upSprite;             // ✅ 위쪽 스프라이트
    [SerializeField] private Sprite downSprite;           // ✅ 아래쪽 스프라이트
    [SerializeField] private Sprite leftSprite;           // ✅ 왼쪽 스프라이트
    [SerializeField] private Sprite rightSprite;          // ✅ 오른쪽 스프라이트
    [SerializeField] private float minVelToFlip = 0.05f;  // ✅ 스프라이트 전환 최소 속도 임계

    // ─────────────────────────────────────────────────────────────────────────────
    // 4) 좌우 반복 회전(이동 중)
    // ─────────────────────────────────────────────────────────────────────────────

    [Header("이동 중 좌우 반복 회전(피치/요/롤)")]
    [SerializeField] private Transform wobbleTarget;      // ✅ 회전을 적용할 대상(예: 무기/상체 등)
    [SerializeField] private WobbleAxis wobbleAxis = WobbleAxis.Z; // ✅ 회전 축 선택(X/Y/Z)
    [SerializeField] private float wobbleAmplitude = 15f; // ✅ 회전 한계(±도)
    [SerializeField] private float wobbleSpeed = 180f;    // ✅ 각속도(도/초)
    [SerializeField] private bool wobbleStartPositive = true; // ✅ 시작 방향(+쪽부터 회전)
    [SerializeField] private float wobbleIdleReturnSpeed = 360f; // ✅ 정지 시 0도로 복귀 속도(도/초)

    // ─────────────────────────────────────────────────────────────────────────────
    // 5) 상태/내부 변수
    // ─────────────────────────────────────────────────────────────────────────────

    [Header("상태")]
    [SerializeField] private AIState state = AIState.Idle; // ✅ 현재 상태
    private float repathTimer = 0f;                        // ✅ 재탐색 타이머
    private Vector2 lastMoveDir = Vector2.right;           // ✅ 최근 이동 입력(8방향 양자화 전)
    private Vector2 lastCardinal = Vector2.right;          // ✅ 최근 확정된 4방향(스프라이트용)
    private float currentSpeed = 0f;                       // ✅ 현재 속도 값
    private float wobbleAngle = 0f;                        // ✅ 현재 회전 각도(±amplitude)
    private int wobbleSign = 1;                            // ✅ 현재 회전 진행 방향(+1/-1)

    // ─────────────────────────────────────────────────────────────────────────────
    // 유니티 생명주기
    // ─────────────────────────────────────────────────────────────────────────────

    private void Reset()                                   // ▶ 컴포넌트 자동 할당 + 2D 보정
    {
        if (!rb) rb = GetComponent<Rigidbody2D>();
        if (!agent) agent = GetComponent<NavMeshAgent>();
        if (!sprite) sprite = GetComponentInChildren<SpriteRenderer>();
        ApplyAgent2DSettings();                            // ✅ 에이전트 2D 자동 세팅
    }

    private void OnValidate()                              // ▶ 에디터 값 변경 시에도 2D 보정 유지
    {
        if (!agent) agent = GetComponent<NavMeshAgent>();
        ApplyAgent2DSettings();                            // ✅ 에이전트 2D 자동 세팅(반복)
    }

    private void Awake()                                   // ▶ 참조 확인 + 2D 보정 + 기본 세팅
    {
        if (!rb) rb = GetComponent<Rigidbody2D>();
        if (!agent) agent = GetComponent<NavMeshAgent>();
        if (!sprite) sprite = GetComponentInChildren<SpriteRenderer>();

        ApplyAgent2DSettings();                            // ✅ NavMeshAgent 2D 강제 세팅
        agent.stoppingDistance = Mathf.Max(0.01f, arriveDistance); // ✅ 멈춤 거리
        if (Mathf.Abs(agent.baseOffset) > Mathf.Epsilon)   // ✅ 2D 권장: 베이스 오프셋 0
            agent.baseOffset = 0f;

        // 회전 처음 진행 방향 설정
        wobbleSign = wobbleStartPositive ? 1 : -1;
    }

    private void OnEnable()                                // ▶ 실행 중에도 값이 바뀌면 복구
    {
        ApplyAgent2DSettings();                            // ✅ 방어적 재세팅
    }

    private void Update()                                  // ▶ 상태/경로/스프라이트/회전
    {
        UpdateState();                                     // ✅ 상태 전이 및 목적지 설정

        Vector2 desired = ComputeDesiredMove();            // ✅ steeringTarget 기반 원본 방향
        Vector2 quantized = Quantize8(desired);            // ✅ 8방향 스냅

        UpdateCardinalFacing(quantized);                   // ✅ 최근 4방향 갱신 규칙 적용
        ApplySpriteByCardinal();                           // ✅ 스프라이트 반영
        ApplyMovement(quantized);                          // ✅ Rigidbody2D.velocity 제어
        UpdateWobble();                                    // ✅ wobbleTarget 회전 적용

        agent.nextPosition = transform.position;           // ✅ NavMesh 내부 위치 동기화
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // 에이전트 2D 자동 세팅(핵심 추가)
    // ─────────────────────────────────────────────────────────────────────────────

    private void ApplyAgent2DSettings()                    // ▶ updatePos/Rot/UpAxis 자동 False
    {
        if (!agent) return;
        agent.updatePosition = false;                      // ✅ NavMesh가 트랜스폼 이동 x
        agent.updateRotation = false;                      // ✅ NavMesh가 회전 갱신 x
        agent.updateUpAxis = false;                        // ✅ 2D(XY) 평면 사용
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // 상태/경로
    // ─────────────────────────────────────────────────────────────────────────────

    private void UpdateState()                             // ▶ 상태 전이/목표 지정 + 가드/워프
    {
        if (target == null)                                // 대상 없으면 Idle
        {
            state = AIState.Idle;
            return;
        }

        float dist = Vector2.Distance(transform.position, target.position); // ✅ 목표와의 거리
        if (dist <= arriveDistance)                        // 도달 판단
        {
            state = AIState.Arrive;
            agent.ResetPath();
            return;
        }

        // Chase 상태 유지/전이
        state = AIState.Chase;
        repathTimer += Time.deltaTime;
        if (repathTimer >= repathInterval || !agent.hasPath)
        {
            // ✅ NavMesh 위인지 확인하고, 아니라면 근처 유효 지점으로 복구(Warp)
            if (!agent.isOnNavMesh)
            {
                if (NavMesh.SamplePosition(transform.position, out var hit, sampleRadius, NavMesh.AllAreas))
                {
                    agent.Warp(hit.position);             // ✅ 메시에 올려놓기
                }
                else
                {
                    // 샘플 실패 시 이번 프레임은 경로 요청 생략
                    repathTimer = 0f;
                    return;
                }
            }

            agent.stoppingDistance = Mathf.Max(0.01f, arriveDistance); // ✅ 멈춤 거리 반영
            if (agent.enabled && agent.isOnNavMesh)                     // ✅ 가드 후 요청
                agent.SetDestination(target.position);

            repathTimer = 0f;
        }
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // 이동 벡터 계산/양자화
    // ─────────────────────────────────────────────────────────────────────────────

    private Vector2 ComputeDesiredMove()                    // ▶ 에이전트의 steeringTarget 기반 이동 방향
    {
        if (state == AIState.Idle)   return Vector2.zero;
        if (state == AIState.Arrive) return Vector2.zero;
        if (!agent.hasPath)          return Vector2.zero;

        Vector3 tgt = agent.steeringTarget;                // ✅ 다음 코너 지점
        Vector2 dir = ((Vector2)(tgt - transform.position)).normalized;
        lastMoveDir = dir;                                 // ✅ 최근 이동 입력 저장
        return dir;
    }

    private Vector2 Quantize8(Vector2 dir)                 // ▶ 8방향(45도 간격) 양자화
    {
        if (dir.sqrMagnitude < 1e-6f) return Vector2.zero;

        float angle = Mathf.Atan2(dir.y, dir.x);           // 라디안
        float step  = Mathf.PI / 4f;                       // 45°
        int   sector = Mathf.RoundToInt(angle / step);     // 가장 가까운 섹터
        float snapped = sector * step;                     // 스냅 각도
        Vector2 q = new Vector2(Mathf.Cos(snapped), Mathf.Sin(snapped));

        // 대각선 속도 보정(선택)
        if (Mathf.Abs(q.x) > 0.1f && Mathf.Abs(q.y) > 0.1f)
            q *= diagonalScale;

        return q.normalized;
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // 스프라이트(4방향)
    // ─────────────────────────────────────────────────────────────────────────────

    private void UpdateCardinalFacing(Vector2 q)           // ▶ 4방향 갱신 규칙(대각 이동 시 최근 값 유지)
    {
        // 충분히 움직일 때만 방향 갱신 시도
        if (rb != null && rb.linearVelocity.magnitude < minVelToFlip) return;

        // 순수 4방향 섹터일 때에만 lastCardinal 갱신
        if (Mathf.Abs(q.x) > 0.9f && Mathf.Abs(q.y) < 0.1f)      // 좌/우
            lastCardinal = new Vector2(Mathf.Sign(q.x), 0f);
        else if (Mathf.Abs(q.y) > 0.9f && Mathf.Abs(q.x) < 0.1f) // 상/하
            lastCardinal = new Vector2(0f, Mathf.Sign(q.y));
        // 대각이면 갱신하지 않고 유지
    }

    private void ApplySpriteByCardinal()                   // ▶ lastCardinal 기준 스프라이트 적용
    {
        if (!sprite) return;

        if (lastCardinal.x > 0.5f) sprite.sprite = rightSprite;     // 오른쪽
        else if (lastCardinal.x < -0.5f) sprite.sprite = leftSprite;// 왼쪽
        else if (lastCardinal.y > 0.5f) sprite.sprite = upSprite;   // 위
        else sprite.sprite = downSprite;                            // 아래(기본)
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // 이동 출력(가속/감속)
    // ─────────────────────────────────────────────────────────────────────────────

    private void ApplyMovement(Vector2 q)                  // ▶ 양자화 벡터로 가속/감속 이동
    {
        float dt = Time.deltaTime;

        float targetSpeed = (q == Vector2.zero) ? 0f : moveSpeed; // 목적 속력
        float rate = (targetSpeed > currentSpeed) ? accel : decel;// 가감속 선택
        currentSpeed = Mathf.MoveTowards(currentSpeed, targetSpeed, rate * dt);

        Vector2 vel = q * currentSpeed;                    // 목표 속도 벡터
        rb.linearVelocity = vel;                                 // Rigidbody2D 속도 적용
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // 이동 중 좌우 반복 회전(피치/요/롤)
    // ─────────────────────────────────────────────────────────────────────────────

    private void UpdateWobble()                            // ▶ 이동 중 진자 회전, 정지 시 0도로 복귀
    {
        if (!wobbleTarget) return;

        float dt = Time.deltaTime;
        bool isMoving = rb != null && rb.linearVelocity.sqrMagnitude > 0.001f;

        if (isMoving)
        {
            // 각속도 * 방향으로 진행
            wobbleAngle += wobbleSpeed * wobbleSign * dt;

            // 한계 도달 시 방향 반전 + 클램프
            if (wobbleAngle > wobbleAmplitude)
            {
                wobbleAngle = wobbleAmplitude;
                wobbleSign = -1;
            }
            else if (wobbleAngle < -wobbleAmplitude)
            {
                wobbleAngle = -wobbleAmplitude;
                wobbleSign = +1;
            }
        }
        else
        {
            // 정지 시 0도로 빠르게 복귀
            if (Mathf.Abs(wobbleAngle) > 0.01f)
            {
                float sgn = Mathf.Sign(-wobbleAngle);
                float step = wobbleIdleReturnSpeed * dt * sgn;
                float next = wobbleAngle + step;
                if (Mathf.Sign(wobbleAngle) != Mathf.Sign(next) || Mathf.Abs(next) < 0.01f)
                    wobbleAngle = 0f;
                else
                    wobbleAngle = next;
            }
        }

        // 선택 축에 회전 각 적용(현지 오일러에 덮어쓰기)
        Vector3 e = wobbleTarget.localEulerAngles;
        switch (wobbleAxis)
        {
            case WobbleAxis.X: e.x = wobbleAngle; break;   // 피치
            case WobbleAxis.Y: e.y = wobbleAngle; break;   // 롤
            case WobbleAxis.Z: e.z = wobbleAngle; break;   // 요(2D에서 주로 사용)
        }
        wobbleTarget.localEulerAngles = e;
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // 디버그 기즈모
    // ─────────────────────────────────────────────────────────────────────────────

#if UNITY_EDITOR
    [Header("디버그 표시")]
    [SerializeField] private bool debugGizmos = true;      // ✅ 기즈모 표시 On/Off
    [SerializeField] private Color arriveColor = new Color(0f, 0.8f, 0.2f, 0.2f); // ✅ 도착 반경 색

    private void OnDrawGizmosSelected()                    // ▶ 도착 반경/스티어링 타겟 표시
    {
        if (!debugGizmos) return;

        // 도착 반경
        Gizmos.color = arriveColor;
        Gizmos.DrawWireSphere(transform.position, Mathf.Max(0.01f, arriveDistance));

        // 스티어링 타겟
        var a = GetComponent<NavMeshAgent>();
        if (a && a.hasPath)
        {
            Gizmos.color = new Color(1f, 0.5f, 0f, 0.8f);
            Gizmos.DrawSphere(a.steeringTarget, 0.06f);
        }
    }
#endif
}
