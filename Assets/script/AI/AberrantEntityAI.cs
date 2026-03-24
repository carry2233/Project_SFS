using UnityEngine;

/// <summary>
/// 개체 AI 메인 컨트롤러
/// - 상태 판단(Wander / Flee / Chase)
/// - 이동 좌표 계산
/// - EntityMovementAI에 목적지 전달
/// </summary>
public class AberrantEntityAI : MonoBehaviour
{
    // ==============================
    // ENUM (상태 / 성향)
    // ==============================

    /// <summary>
    /// 개체 AI 상태
    /// </summary>
    public enum EntityAIState
    {
        Wander,         // 배회 상태
        Flee,           // 도망 상태
        Retaliate,      // 반격 상태
        AggroPlayer     // 플레이어 추적 상태
    }

    /// <summary>
    /// 개체 성향
    /// </summary>
    public enum BehaviorPersonality
    {
        Defensive,      // 방어적 (피격 시 도망)
        Neutral,        // 중립 (피격 시 반격)
        Aggressive      // 공격적 (플레이어 감지 시 추적)
    }

    // ==============================
    // REFERENCES (필수 참조)
    // ==============================

    [Header("필수 참조")]
    [SerializeField] private EntityMovementAI movementAI;     // 이동 실행 담당 스크립트
    [SerializeField] private MeleeWeaponAimAI meleeAimAI;     // 근접 전투/조준 제어
    [SerializeField] private RangedWeaponAimAI rangedAimAI;   // ✅ 원거리 조준/사격 제어(신규)
    [SerializeField] private ObjectInfo objectInfo;           // 피격 이벤트 수신용
    [SerializeField] private Transform playerTransform;       // 플레이어 Transform

    private Transform self;                                   // 자기 자신 Transform 캐시

    // ==============================
    // STATE / PERSONALITY
    // ==============================

    [Header("상태 / 성향")]
    [SerializeField] private BehaviorPersonality personality; // 개체 성향
    [SerializeField] private EntityAIState currentState;      // 현재 상태

    private float stateEnterTime;                             // 상태 진입 시각

    // ==============================
    // TARGET
    // ==============================

    private Transform currentTarget;                          // 현재 타겟(플레이어 또는 공격자)

    // ==============================
    // WANDER (배회 설정)
    // ==============================

    [Header("배회(Wander) 설정")]
    [SerializeField] private float wanderRadius = 5f;         // 배회 반경
    [SerializeField] private float wanderWaitMin = 1f;        // 목적지 갱신 최소 시간
    [SerializeField] private float wanderWaitMax = 3f;        // 목적지 갱신 최대 시간

    private Vector2 wanderCenter;                             // 배회 중심 좌표
    private float wanderRetargetEndTime;                      // 목적지 재선정 타이머 종료 시각
    private bool wanderInitialized;                           // Wander 최초 진입 여부

    // ==============================
    // FLEE (도망 설정)
    // ==============================

    [Header("도망(Flee) 설정")]
    [SerializeField] private float fleeDistance = 6f;         // 도망 시 이동 거리
    [SerializeField] private float fleeDuration = 3f;         // 도망 지속 시간
    [SerializeField] private float fleeEndDistance = 7f;   // 이 거리 이상 벌어지면 도망 종료

    private Transform fleeTarget;                           // 도망 대상(공격자)
    private float fleeTimer;                                // 도망 타이머

    // ==============================
    // CHASE (추적 설정)
    // ==============================

    [Header("추적(Chase) 설정")]
    [SerializeField] private float chaseKeepDistance = 8f;    // 추적 유지 거리
    [SerializeField] private float chaseLostDuration = 4f;    // 추적 포기 시간

    private float chaseLostTimer;                              // 추적 이탈 누적 타이머

    // ==============================
    // PLAYER DETECT (플레이어 감지)
    // ==============================

    [Header("플레이어 감지")]
    [SerializeField] private float playerDetectRange = 7f;    // 플레이어 감지 거리

    // ==============================
// FLEE / CHASE 목적지 갱신 주기 (추가)
// ==============================

[Header("경로 갱신 주기(성능)")]
[SerializeField] private float fleeRepathInterval = 0.25f;     // Flee 목적지 갱신 주기(초)
[SerializeField] private float chaseRepathInterval = 0.15f;    // Chase 목적지 갱신 주기(초)

// (선택) 너무 잦은 갱신 방지용
[SerializeField] private float chaseRepathMinTargetMove = 0.25f; // 타겟이 이 거리 이상 움직였을 때만 갱신

[Header("디버그: 타겟 라인 표시")]
[SerializeField] private bool drawTargetLine = true; // 현재 타겟까지 직선 표시 여부


// ==============================
// COMBAT WEAPON TYPE  // ✅ 추가
// ==============================

public enum CombatWeaponType
{
    Melee,   // 근접 무기만 사용
    Ranged   // 원거리 무기만 사용
}

[Header("전투 무기 타입")]
[SerializeField] private CombatWeaponType combatWeaponType = CombatWeaponType.Melee; // 이 AI가 사용할 무기 타입

[Header("디버그: 원거리 사격 여부 변수")] 
[SerializeField] private bool shouldAimRanged = false;  // 원거리: 조준해야 하는가? (거리판단 결과)
[SerializeField] private bool shouldFireRanged = false; // 원거리: 사격해야 하는가? (거리판단 결과)



// ==============================
// MELEE SETTINGS (AimAI 공유 설정)  // ✅ 추가
// ==============================

[Header("근접 전투 설정(AimAI 공유)")]
[SerializeField, Min(0f)] private float aimEnterDistance = 6f;        // 조준 진입 거리
[SerializeField, Min(0f)] private float meleeTriggerDistance = 1.8f;  // 근접 공격 트리거 거리
[SerializeField, Min(0f)] private float aimHysteresis = 0.25f;        // 조준 유지 여유 거리
[SerializeField, Min(0f)] private float meleeCooldown = 0.7f;         // 근접 공격 쿨다운(초)
[SerializeField, Min(0f)] private float meleeMoveLockDuration = 0.35f;// 근접 공격 시 이동/방향 잠금 시간(초)

// [추가] 근접 추적(Chase) 전용 목적지 갱신 캐시  // ✅ 근접 전투용 추적 분리
private float nextMeleeChaseRepathTime = 0f;      // 근접: 다음 목적지 갱신 시각
private Vector2 lastMeleeChaseTargetPos;          // 근접: 마지막으로 목적지 갱신에 사용한 타겟 위치
private bool hasLastMeleeChaseTargetPos = false;  // 근접: lastMeleeChaseTargetPos 유효 여부



// ==============================
// RANGED (총 전투 설정)
// ==============================

[Header("원거리 전투(총) 설정")]
[SerializeField, Min(0f)] private float minApproachDistance = 2.5f;   // 최소 접근 거리(미만이면 '거리 벌리기' 시작)
[SerializeField, Min(0f)] private float shootDistance = 6.0f;         // 사격 실행 거리(이내면 조준/사격)
[SerializeField, Min(0f)] private float tooCloseFleeDistance = 6.0f;  // 너무 가까울 때 반대 방향으로 잡을 목적지 거리
[SerializeField, Min(0.01f)] private float tooCloseRepathInterval = 0.25f; // 너무 가까울 때 목적지 갱신 주기(초)  // ✅ 추가

[Header("NavMesh 직선 막힘 설정")]
[SerializeField] private bool chaseWhenNavMeshLineBlocked = true;     // ✅ 사격 거리여도 NavMesh 직선이 막히면 추적 유지
[SerializeField, Min(0f)] private float navMeshLineCheckInterval = 0.15f; // ✅ NavMesh 직선 막힘 검사 간격(성능용)

private float nextNavMeshLineCheckTime = 0f;    // ✅ 다음 막힘 검사 시각
private bool cachedNavMeshLineBlocked = false; // ✅ 최근 막힘 결과 캐시


private float nextTooCloseRepathTime = 0f; // 너무 가까울 때 목적지 갱신 타이머
private bool wasTooCloseLastFrame = false; // ✅ 추가: too-close(최소접근 미만) 상태 진입/유지 감지용

// ▶ 원거리 전투 프레임 분리 제어
private bool rangedCombatJustEnabled = false; // 원거리 전투가 이번 프레임에 켜졌는지 여부





private float nextFleeRepathTime = 0f;        // 다음 Flee 목적지 갱신 시각
private float nextChaseRepathTime = 0f;       // 다음 Chase 목적지 갱신 시각
private Vector2 lastChaseTargetPos;           // 마지막으로 목적지 갱신에 사용한 타겟 위치
private bool hasLastChaseTargetPos = false;   // lastChaseTargetPos 유효 여부


    // ==============================
    // UNITY LIFE CYCLE
    // ==============================

    private void Awake()
    {
        self = transform;                                     // 자기 Transform 캐시
    }

private void OnEnable() // 활성화 시 초기화/이벤트 연결
{
    // 피격 이벤트 등록
    if (objectInfo != null)
        objectInfo.OnDamaged += HandleDamaged;

    // ✅ 추가: AimAI가 사용할 근접 설정값을 AI에서만 주입
    if (meleeAimAI != null)
        meleeAimAI.ApplyCombatSettingsFromAI(aimEnterDistance, meleeTriggerDistance, aimHysteresis, meleeCooldown, meleeMoveLockDuration); // 설정 주입

    wanderCenter = self.position;                         // 시작 위치를 배회 중심으로 설정
    wanderInitialized = false;                            // 배회 초기화 플래그 리셋
    ChangeState(EntityAIState.Wander);                    // 게임 시작 시 Wander 진입
}



    private void Update()
    {
        CheckPlayerDetection();                               // 플레이어 감지 검사

        switch (currentState)
        {
            case EntityAIState.Wander:
                UpdateWander();
                break;

            case EntityAIState.Flee:
                UpdateFlee();
                break;

            case EntityAIState.Retaliate:
            case EntityAIState.AggroPlayer:
                UpdateChase();
                break;
        }
    }

    // ==============================
    // STATE CHANGE
    // ==============================

    /// <summary>
    /// 상태 변경 공통 처리
    /// </summary>
private void ChangeState(EntityAIState newState) // 상태 변경 공통 처리(근접 추적 캐시 초기화 추가)
{
    movementAI.SetMoving(false);                // 이동 off
    movementAI.StopImmediate();                 // 즉시 정지

    currentState = newState;                    // 상태 갱신
    stateEnterTime = Time.time;                 // 상태 진입 시간
    chaseLostTimer = 0f;                        // 추적 포기 타이머 초기화

    nextFleeRepathTime = 0f;                    // Flee 재목적지 타이머 초기화
    nextChaseRepathTime = 0f;                   // (원거리) Chase 재목적지 타이머 초기화
    nextTooCloseRepathTime = 0f;                // 너무 가까울 때 타이머 초기화
    wasTooCloseLastFrame = false;               // too-close 진입 감지 초기화
    hasLastChaseTargetPos = false;              // (원거리) 타겟 위치 캐시 초기화

    // ✅ 추가: (근접) 타겟 위치 캐시 초기화
    nextMeleeChaseRepathTime = 0f;              // 근접: 재목적지 타이머 초기화
    hasLastMeleeChaseTargetPos = false;         // 근접: 타겟 위치 캐시 초기화

    // ✅ 추가: NavMesh 직선 막힘 캐시 초기화(상태 전환 시 잔상 방지)
    nextNavMeshLineCheckTime = 0f;          // 다음 검사 시각 초기화
    cachedNavMeshLineBlocked = false;       // 캐시 초기화


    if (newState == EntityAIState.Wander)       // Wander 진입 처리
        EnterWander();                          // Wander 시작
}



    /// <summary>
    /// Wander 상태 진입 처리
    /// </summary>
    private void EnterWander()
    {
        Vector2 pos = SelectWanderWorldPosition();            // 배회 위치 선정
        movementAI.SetDestination(pos);                       // 목적지 전달
        movementAI.SetMoving(true);                           // 이동 시작

        wanderRetargetEndTime =
            Time.time + Random.Range(wanderWaitMin, wanderWaitMax); // 목적지 갱신 타이머 시작

        wanderInitialized = true;
    }

    // ==============================
    // UPDATE STATES
    // ==============================

    /// <summary>
    /// 배회 상태 업데이트
    /// </summary>
    private void UpdateWander()
    {
        meleeAimAI.SetCombatEnabled(false);                   // 배회 중 전투 비활성
        rangedAimAI?.SetCombatEnabled(false); // 원거리 전투 OFF  // ✅ 추가
        rangedAimAI?.ClearCombatInput();      // 원거리 입력 초기화  // ✅ 추가
        

        if (!wanderInitialized)
        {
            EnterWander();
            return;
        }

        // 목적지 도착 시 이동 중단
        if (movementAI.ConsumeArrivedFlag())
        {
            movementAI.SetMoving(false);
            return;
        }

        // 타이머 종료 전까지 대기
        if (Time.time < wanderRetargetEndTime)
            return;

        // 타이머 종료 → 새 목적지 선정
        Vector2 next = SelectWanderWorldPosition();
        movementAI.SetDestination(next);
        movementAI.SetMoving(true);

        wanderRetargetEndTime =
            Time.time + Random.Range(wanderWaitMin, wanderWaitMax);
    }

    /// <summary>
    /// 도망 상태 업데이트
    /// </summary>
private void UpdateFlee() // 도망 상태 업데이트
{
    if (fleeTarget == null)                     // 도망 대상이 없으면
    {
        ChangeState(EntityAIState.Wander);      // 배회로 복귀
        return;
    }

    fleeTimer -= Time.deltaTime;               // 도망 타이머 감소

    float distance = Vector2.Distance(transform.position, fleeTarget.position); // 공격자와 거리

    if (fleeTimer <= 0f || distance >= fleeEndDistance) // 도망 종료 조건
    {
        fleeTarget = null;                     // 도망 대상 해제
        ChangeState(EntityAIState.Wander);     // 배회로 복귀
        return;
    }

    // 전투 비활성(도망 중) - 기존 기본 기능 유지
    if (rangedAimAI != null) // ✅ 추가: 도망 중 총 전투 off
        rangedAimAI.SetCombatEnabled(false); // 원거리 전투 OFF

    // 목적지 갱신은 주기마다만 수행 (프레임당 X)
    if (Time.time < nextFleeRepathTime)        // 아직 갱신 시각이 아니면
    {
        movementAI.SetMoving(true);            // 이동은 계속 유지
        return;
    }

    nextFleeRepathTime = Time.time + fleeRepathInterval; // 다음 갱신 예약

    Vector2 fleeDir = ((Vector2)transform.position - (Vector2)fleeTarget.position).normalized; // 반대 방향
    Vector2 desiredFleePos = (Vector2)transform.position + fleeDir * fleeDistance;             // 반대방향 끝점

    // NavMesh 목적지로 보정(비베이크/외곽 방지)
    Vector2 sampledFleePos;                    // 샘플된 목적지
    if (movementAI.TrySampleDestinationOnNavMesh(desiredFleePos, out sampledFleePos)) // NavMesh 샘플 성공?
        movementAI.SetDestination(sampledFleePos); // 샘플 목적지로 설정
    else
        movementAI.SetDestination(desiredFleePos);  // 실패 시 원본(폴백)

    movementAI.SetMoving(true);                // 이동 실행
}




    /// <summary>
    /// 추적/반격 상태 업데이트
    /// </summary>
private void UpdateChase() // ✅ 추적/전투 상태 업데이트(근접 추적/원거리 유지거리 + NavMesh 비베이크 차단 확장)
{
    // ─────────────────────────────
    // 0) 타겟 유효성 체크
    // ─────────────────────────────
    if (currentTarget == null || !currentTarget.gameObject.activeInHierarchy) // 타겟이 없거나 비활성화면
    {
        currentTarget = null;                          // 타겟 초기화
        meleeAimAI?.SetCombatEnabled(false);           // 근접 전투 OFF
        rangedAimAI?.SetCombatEnabled(false);          // 원거리 전투 OFF
        rangedAimAI?.ClearCombatInput();               // 원거리 입력 초기화
        wasTooCloseLastFrame = false;                  // too-close 상태 초기화
        ChangeState(EntityAIState.Wander);             // 배회로 전환
        return;                                        // 종료
    }

    float dist = Vector2.Distance(self.position, currentTarget.position); // 타겟과의 거리(공통)

    // ─────────────────────────────
    // 1) 근접 무기: 근접 전투용 추적(이동 ON) + 근접거리면 정지 후 공격(AimAI가 트리거)
    // ─────────────────────────────
    if (combatWeaponType == CombatWeaponType.Melee) // 근접 고정 사용
    {
        // 원거리 완전 비활성
        rangedAimAI?.SetCombatEnabled(false);         // 원거리 전투 OFF
        rangedAimAI?.ClearCombatInput();              // 원거리 입력 초기화
        wasTooCloseLastFrame = false;                 // 원거리 too-close 상태 초기화(근접에서는 미사용)

        // 근접 전투 활성 + 타겟 전달
        meleeAimAI?.SetCombatEnabled(true);           // 근접 전투 ON
        meleeAimAI?.SetTarget(currentTarget);         // 타겟 전달

        // 근접 트리거 거리 이내면: 이동 정지(공격은 MeleeWeaponAimAI가 BeginAttack 시도)
        if (dist <= meleeTriggerDistance)             // 근접 공격 거리 이내
        {
            movementAI.SetMoving(false);              // 이동 OFF
            movementAI.StopImmediate();               // 즉시 정지
            return;
        }

        // 근접 공격 거리 밖이면: 타겟에게 추적 이동(목적지 갱신 주기/이동량 제한 + NavMesh 보정)
        bool timeReady = Time.time >= nextMeleeChaseRepathTime; // 목적지 갱신 타이밍 체크
        bool moveReady = true;                                   // 타겟 이동량 체크 결과
        Vector2 targetPos = currentTarget.position;              // 현재 타겟 위치

        if (hasLastMeleeChaseTargetPos)                          // 이전 타겟 위치가 있으면
            moveReady = Vector2.Distance(targetPos, lastMeleeChaseTargetPos) >= chaseRepathMinTargetMove; // 최소 이동량 조건

        if (timeReady && moveReady)                              // 조건 충족 시에만 목적지 갱신
        {
            nextMeleeChaseRepathTime = Time.time + chaseRepathInterval; // 다음 갱신 예약
            lastMeleeChaseTargetPos = targetPos;                        // 마지막 타겟 위치 저장
            hasLastMeleeChaseTargetPos = true;                          // 저장 유효 플래그

            if (movementAI.TrySampleDestinationOnNavMesh(targetPos, out Vector2 sampledTargetPos)) // NavMesh 위로 보정
                movementAI.SetDestination(sampledTargetPos);            // 보정된 목적지
            else
                movementAI.SetDestination(targetPos);                   // 실패 시 원본(폴백)
        }

        movementAI.SetMoving(true); // ✅ 이동 ON 보장
        return;
    }

    // ─────────────────────────────
    // 2) 원거리 무기: 유지거리(minApproachDistance) + 사격거리(shootDistance) + NavMesh 비베이크 차단이면 추적 유지
    // ─────────────────────────────
    // 근접 완전 비활성
    meleeAimAI?.SetCombatEnabled(false);                 // 근접 전투 OFF

    // 원거리 전투 활성 + 타겟 전달
    rangedAimAI?.SetCombatEnabled(true);                 // 원거리 전투 ON
    rangedAimAI?.SetTarget(currentTarget);               // 타겟 전달

    // 2-1) 너무 가까우면(유지거리) 거리 벌리기(기존 too-close 로직 유지)
    if (dist < minApproachDistance)                      // 너무 가까운 상태면
    {
        // too-close 진입/주기 조건(기존 변수 유지)
        if (!wasTooCloseLastFrame || Time.time >= nextTooCloseRepathTime) // 첫 진입 or 갱신 시각 도달
        {
            nextTooCloseRepathTime = Time.time + tooCloseRepathInterval;  // 다음 갱신 예약

            Vector2 fleeDir = ((Vector2)self.position - (Vector2)currentTarget.position).normalized; // 타겟 반대 방향
            Vector2 chaseTargetPos = (Vector2)currentTarget.position;                                  // 타겟 위치
            Vector2 desiredPos = chaseTargetPos + fleeDir * (dist + tooCloseFleeDistance);            // 유지거리 + 추가 이격

            if (movementAI.TrySampleDestinationOnNavMesh(desiredPos, out Vector2 sampledPos))         // NavMesh 보정
                movementAI.SetDestination(sampledPos);                                                // 보정 목적지
            else
                movementAI.SetDestination(desiredPos);                                                // 보정 실패 폴백
        }

        wasTooCloseLastFrame = true;                    // too-close 상태 유지
        rangedAimAI?.SetCombatInput(true, false);       // 조준 ON / 사격 OFF(이동하며 거리 벌리기)
        movementAI.SetMoving(true);                     // 이동 ON
        return;
    }

    wasTooCloseLastFrame = false; // too-close 탈출 처리

    // 2-2) (추가) 사격거리 이내라도 NavMesh 직선이 막히면(비베이크/홀/단절) 추적 유지
    bool navLineBlocked = false; // ✅ NavMesh 직선 차단 여부
    if (chaseWhenNavMeshLineBlocked) // ✅ 기능 사용 시
    {
        if (dist <= shootDistance) // 사격거리 안에서만 검사(불필요 호출 방지)
        {
            if (Time.time >= nextNavMeshLineCheckTime) // 검사 간격 도달?
            {
                nextNavMeshLineCheckTime = Time.time + navMeshLineCheckInterval; // 다음 검사 예약
                cachedNavMeshLineBlocked = (movementAI != null) &&
                    movementAI.IsNavMeshLineBlocked((Vector2)self.position, (Vector2)currentTarget.position); // ✅ 차단 검사
            }
            navLineBlocked = cachedNavMeshLineBlocked; // 캐시 결과 사용
        }
        else
        {
            nextNavMeshLineCheckTime = 0f;             // 사격거리 밖이면 다음 진입 때 즉시 재검사
            cachedNavMeshLineBlocked = false;          // 캐시 리셋
        }
    }

    // 2-3) 사격거리 이내 + NavMesh 직선이 안 막힘: 정지 후 사격
    if (dist <= shootDistance && !navLineBlocked) // ✅ 변경: 막힘이 없을 때만 멈춰 사격
    {
        rangedAimAI?.SetCombatInput(true, true);  // 조준 ON / 사격 ON
        movementAI.SetMoving(false);              // 이동 OFF
        movementAI.StopImmediate();               // 즉시 정지
        return;
    }

    // 2-4) 사격거리 이내라도 막힘이면: 사격하지 않고 추적 계속(막힘 해소 지점까지 이동)
    if (dist <= shootDistance && navLineBlocked) // ✅ 막힘이면 계속 추적
    {
        rangedAimAI?.SetCombatInput(true, false); // 조준 ON / 사격 OFF(막힘 해소 전까지)
        // 아래 추적 로직으로 이어짐(그대로 사용)
    }
    else
    {
        // 2-5) 사격거리 밖: 추적하며 조준 유지(사격 OFF)
        rangedAimAI?.SetCombatInput(true, false); // 조준 ON / 사격 OFF
    }

    // ─────────────────────────────
    // 3) 추적 이동(공통): 기존 목적지 갱신 주기/이동량 제한 + NavMesh 보정 재사용
    // ─────────────────────────────
    bool chaseTimeReady = Time.time >= nextChaseRepathTime; // 목적지 갱신 타이밍 체크
    bool chaseMoveReady = true;                              // 타겟 이동량 체크 결과
    Vector2 chasePos = currentTarget.position;               // 현재 타겟 위치

    if (hasLastChaseTargetPos)                               // 이전 타겟 위치가 있으면
        chaseMoveReady = Vector2.Distance(chasePos, lastChaseTargetPos) >= chaseRepathMinTargetMove; // 최소 이동량 조건

    if (chaseTimeReady && chaseMoveReady)                    // 조건 충족 시에만 목적지 갱신
    {
        nextChaseRepathTime = Time.time + chaseRepathInterval; // 다음 갱신 예약
        lastChaseTargetPos = chasePos;                         // 마지막 타겟 위치 저장
        hasLastChaseTargetPos = true;                          // 저장 유효 플래그

        if (movementAI.TrySampleDestinationOnNavMesh(chasePos, out Vector2 sampledChasePos)) // NavMesh 보정
            movementAI.SetDestination(sampledChasePos);                                       // 보정 목적지
        else
            movementAI.SetDestination(chasePos);                                              // 실패 시 원본(폴백)
    }

    movementAI.SetMoving(true); // ✅ 추적 이동 ON
}




private void LateUpdate()
{
    rangedCombatJustEnabled = false; // ▶ 다음 프레임부터 입력 허용
}




    // ==============================
    // EVENTS
    // ==============================

    /// <summary>
    /// 피격 시 호출되는 이벤트
    /// </summary>
private void HandleDamaged(Transform attacker, CombatPayload2D payload)
{
    if (attacker == null) return;

    currentTarget = attacker;

    // 방어적 성향 → 도망
    if (personality == BehaviorPersonality.Defensive)
    {
        fleeTarget = attacker;          // ✅ 도망 대상 설정
        fleeTimer  = fleeDuration;      // ✅ 도망 타이머 초기화
        ChangeState(EntityAIState.Flee);
    }
    else
    {
        ChangeState(EntityAIState.Retaliate);
    }
}


    // ==============================
    // UTIL
    // ==============================

    /// <summary>
    /// 배회 반경 내 랜덤 월드 좌표 반환
    /// </summary>
    private Vector2 SelectWanderWorldPosition()
    {
        return wanderCenter +
               Random.insideUnitCircle * wanderRadius;
    }

    /// <summary>
    /// 플레이어 감지 검사 (공격적 성향 전용)
    /// </summary>
    private void CheckPlayerDetection()
    {
        if (playerTransform == null) return;
        if (personality != BehaviorPersonality.Aggressive) return;
        if (currentState != EntityAIState.Wander) return;

        float dist =
            Vector2.Distance(self.position, playerTransform.position);

        if (dist <= playerDetectRange)
        {
            currentTarget = playerTransform;
            ChangeState(EntityAIState.AggroPlayer);
        }
    }

    // ==============================
// GIZMOS (씬 반경 시각화)
// ==============================

private void OnDrawGizmosSelected() // 선택 시 반경 표시
{
    Vector3 center = Application.isPlaying
        ? (Vector3)wanderCenter          // 플레이 중이면 저장된 배회 중심
        : transform.position;            // 에디터에서는 현재 위치

    // ------------------------------
    // Wander 반경 (녹색)
    // ------------------------------
    Gizmos.color = new Color(0f, 1f, 0f, 0.25f); // 반투명 녹색
    Gizmos.DrawWireSphere(center, wanderRadius);

    // ------------------------------
    // 플레이어 감지 반경 (빨강)
    // ------------------------------
    Gizmos.color = new Color(1f, 0f, 0f, 0.25f); // 반투명 빨강
    Gizmos.DrawWireSphere(transform.position, playerDetectRange);

    // ------------------------------
    // 추적 유지 거리 (주황)
    // ------------------------------
    Gizmos.color = new Color(1f, 0.5f, 0f, 0.25f); // 주황
    Gizmos.DrawWireSphere(transform.position, chaseKeepDistance);
}

private void OnDrawGizmos() // 씬 뷰에 디버그 선 표시
{
    if (!drawTargetLine) return; // 표시 비활성화면 종료
    if (currentTarget == null) return; // 타겟이 없으면 종료

    Gizmos.color = Color.red; // 라인 색상(고정)
    Gizmos.DrawLine(transform.position, currentTarget.position); // 현재 인식 타겟까지 직선 표시
}


}
