using System;
using UnityEngine;

[AddComponentMenu("MeleeWeaponAimAI(AI근접공격)")]
public class MeleeWeaponAimAI : MonoBehaviour
{
    // ─────────────────────────────────────────────────────────
    // ■ 직렬화 프리셋 구조
    // ─────────────────────────────────────────────────────────
    [Serializable]
    public struct DirectionPreset
    {
        public Vector3 localPosition;           // 로컬 포지션
        public Vector3 localEuler;              // 로컬 오일러
        public bool useOrderOffset;             // 오더 오프셋 사용 여부
        public int orderOffset;                 // 오더 오프셋 값
        public bool overrideOrder;              // 오더 직접 덮어쓰기 여부
        public int orderInLayer;                // 덮어쓸 오더값
    }

    public enum FourDir { Up, Right, Down, Left }

    // ─────────────────────────────────────────────────────────
    // ■ 참조
    // ─────────────────────────────────────────────────────────
    [Header("참조")]
    [SerializeField] private Transform owner;
    [SerializeField] private Transform presetPivot;          // 프리셋 포즈/회전 적용 대상
    [SerializeField] private Transform weaponRoot;           // 조준 회전(Z-Yaw) 적용 대상
    [SerializeField] private SpriteRenderer weaponSR;
    [SerializeField] private EntityMovementAI creature;
    [SerializeField] private MeleeWeaponAttack melee;
    

    // ─────────────────────────────────────────────────────────
    // ■ 프리셋(비조준)
    // ─────────────────────────────────────────────────────────
    [Header("방향별 프리셋 - 비조준(Non-Aim)")]
    [SerializeField] private DirectionPreset nonAimUp;
    [SerializeField] private DirectionPreset nonAimRight;
    [SerializeField] private DirectionPreset nonAimDown;
    [SerializeField] private DirectionPreset nonAimLeft;

    // ─────────────────────────────────────────────────────────
    // ■ 프리셋(조준)
    // ─────────────────────────────────────────────────────────
    [Header("방향별 프리셋 - 조준(Aim)")]
    [SerializeField] private DirectionPreset aimUp;
    [SerializeField] private DirectionPreset aimRight;
    [SerializeField] private DirectionPreset aimDown;
    [SerializeField] private DirectionPreset aimLeft;

    // ─────────────────────────────────────────────────────────
    // ■ 동작 파라미터
    // ─────────────────────────────────────────────────────────
    [Header("동작 파라미터")]
    [SerializeField, Min(0f)] private float poseLerpSpeed = 10f;
    [SerializeField, Min(0f)] private float turnSpeedDegPerSec = 720f;
    [SerializeField] private int baseOrderInLayer = 0;


        // ─────────────────────────────────────────────────────────
// ■ 근접/조준 설정(AI에서 주입받아 사용)  // ✅ 변경
// ─────────────────────────────────────────────────────────
private float aiDistanceToAim = 6f;          // 조준 진입 거리(주입값)
private float aiDistanceToMelee = 1.8f;      // 근접 트리거 거리(주입값)
private float aiHysteresis = 0.25f;          // 조준 유지 여유(주입값)
private float aiMeleeCooldown = 0.7f;        // 근접 쿨다운(주입값)
private float aiMeleeMoveLockDuration = 0.35f;// 이동/방향 잠금 시간(주입값)

    private float meleeCdTimer = 0f;

    [Header("외부 제어")]
    [SerializeField] private bool combatEnabled = true; // 상위 AI가 제어


    // ─────────────────────────────────────────────────────────
    // ■ 내부 상태
    // ─────────────────────────────────────────────────────────
    private Transform target;
    private float currentYaw;
    private bool isAiming;
    private FourDir cachedDir = FourDir.Right;

    // ✅ 추가: 근접 공격을 "다음 프레임"에 실행하기 위한 대기 상태  // (4방향 전환 선행용)
    private bool pendingMeleeAttack = false;      // 다음 프레임 공격 실행 대기 여부
    private int pendingMeleeFrame = -1;           // 대기 시작 프레임 기록
    private Vector2 pendingFacingDir = Vector2.right; // 공격 전 확정할 방향(타겟 방향)


    // ─────────────────────────────────────────────────────────
    // ■ 외부 제어용 API
    // ─────────────────────────────────────────────────────────
    public void SetOwner(Transform t) { owner = t; }
    public void SetCreature(EntityMovementAI ai) { creature = ai; }
    public void SetMelee(MeleeWeaponAttack m) { melee = m; }
    public void SetWeapon(Transform root, SpriteRenderer sr)
    { weaponRoot = root; weaponSR = sr; }
    public void SetPresetPivot(Transform pivot) { presetPivot = pivot; }
    public void SetSpeeds(float poseLerp, float turnSpeed)
    { poseLerpSpeed = Mathf.Max(0f, poseLerp); turnSpeedDegPerSec = Mathf.Max(0f, turnSpeed); }

    public void SetDistances(float dAim, float dMelee, float hys) // ✅ 호환용: 거리 설정(내부는 AI 주입값 변수로)
{
    aiDistanceToAim = Mathf.Max(0f, dAim);      // 조준 진입 거리(주입값) 설정
    aiDistanceToMelee = Mathf.Max(0f, dMelee);  // 근접 트리거 거리(주입값) 설정
    aiHysteresis = Mathf.Max(0f, hys);          // 조준 유지 히스테리시스(주입값) 설정
}


    // ─────────────────────────────────────────────────────────
    // ■ 유니티 루프
    // ─────────────────────────────────────────────────────────
private void Update() // ✅ AI 주입값 기반 조준/근접 트리거 + (추가) 공격 1프레임 지연 실행
{
    if (!owner || !presetPivot || !weaponRoot) return; // 필수 참조 가드

    target = ResolveTarget(); // 타겟 확보
    if (!target) return; // 타겟 없으면 종료

    // 상위 AI가 전투 비활성화한 경우
    if (!combatEnabled)
    {
        pendingMeleeAttack = false; // ✅ 추가: 대기 공격 취소
        isAiming = false; // 조준 해제
        ApplyNonAimPose(); // 비조준 포즈 적용
        weaponRoot.localRotation = Quaternion.identity; // 회전 초기화
        return; // 공격/조준 로직 차단
    }

    float dt = Time.deltaTime; // 델타타임
    float dist = Vector2.Distance(owner.position, target.position); // 타겟과 거리
    if (meleeCdTimer > 0f) meleeCdTimer -= dt; // 쿨다운 감소

    // ✅ 추가: "방향 확정 → 다음 프레임 공격 실행" 대기 처리
    if (pendingMeleeAttack) // 공격 대기 중이면
    {
        // 대기 중 타겟이 멀어지면 취소(근접 범위 유지 필요)
        if (dist > aiDistanceToMelee) // 근접 범위를 벗어나면
        {
            pendingMeleeAttack = false; // 대기 취소
        }
        else if (Time.frameCount > pendingMeleeFrame) // 다음 프레임이 되면
        {
            TryExecutePendingMeleeAttack(); // ✅ 다음 프레임 공격 실행 시도
        }
    }

    if (dist <= aiDistanceToMelee) // ✅ 근접 트리거 거리(주입값)
    {
        isAiming = true; // 조준 상태
        ApplyAimPose(); // 조준 포즈
        ApplyYawRotation(dt); // 타겟 yaw 추적

        TryMeleeTrigger(); // ✅ 근접 공격 "예약" 시도(방향 확정 선행)
    }
    else if (dist <= aiDistanceToAim) // ✅ 조준 진입 거리(주입값)
    {
        isAiming = true; // 조준 상태
        ApplyAimPose(); // 조준 포즈
        ApplyYawRotation(dt); // 타겟 yaw 추적
    }
    else
    {
        pendingMeleeAttack = false; // ✅ 추가: 근접 범위 밖이면 대기 취소

        if (isAiming && dist <= aiDistanceToAim + aiHysteresis) // ✅ 히스테리시스(주입값)
        {
            ApplyAimPose(); // 조준 유지
            ApplyYawRotation(dt); // yaw 유지
        }
        else
        {
            isAiming = false; // 조준 해제
            ApplyNonAimPose(); // 비조준 포즈
            weaponRoot.localRotation = Quaternion.identity; // 회전 초기화
        }
    }
}




    public void SetCombatEnabled(bool enabled) // 공격 허용/차단
{
    combatEnabled = enabled;
}

public void SetTarget(Transform t) // 타겟 직접 지정
{
    target = t;
}


    // ─────────────────────────────────────────────────────────
    // ■ 목표 해석
    // ─────────────────────────────────────────────────────────
    private Transform ResolveTarget()
    {
        //if (creature && creature.target != null) return creature.target;
        return target;
    }

    private static float AngleDeg(Vector2 v)
    {
        float a = Mathf.Atan2(v.y, v.x) * Mathf.Rad2Deg;
        if (a < 0f) a += 360f;
        return a;
    }

    private FourDir YawToFourDir(float yaw)
    {
        if (InRange(yaw, 315f, 360f) || InRange(yaw, 0f, 45f)) return FourDir.Right;
        if (InRange(yaw, 45f, 135f)) return FourDir.Up;
        if (InRange(yaw, 135f, 225f)) return FourDir.Left;
        if (InRange(yaw, 225f, 315f)) return FourDir.Down;
        return FourDir.Right;
    }

    private bool InRange(float v, float min, float max)
    {
        return v > min && v <= max;
    }

    // ─────────────────────────────────────────────────────────
    // ■ 프리셋 선택/적용
    // ─────────────────────────────────────────────────────────
    private DirectionPreset GetPreset(bool aim, FourDir dir)
    {
        if (aim)
        {
            switch (dir)
            {
                case FourDir.Up: return aimUp;
                case FourDir.Right: return aimRight;
                case FourDir.Down: return aimDown;
                case FourDir.Left: return aimLeft;
            }
        }
        else
        {
            switch (dir)
            {
                case FourDir.Up: return nonAimUp;
                case FourDir.Right: return nonAimRight;
                case FourDir.Down: return nonAimDown;
                case FourDir.Left: return nonAimLeft;
            }
        }
        return aimRight;
    }

    // ─────────────────────────────────────────────────────────
    // ■ 포즈 적용 (단순 적용 방식)
    // ─────────────────────────────────────────────────────────
    private void ApplyAimPose()
    {
        Vector2 to = target.position - owner.position;
        float yaw = AngleDeg(to);
        cachedDir = YawToFourDir(yaw);
        var preset = GetPreset(true, cachedDir);

        // ✅ 수정: 단순히 프리셋의 값을 바로 적용
        presetPivot.localPosition = preset.localPosition;
        presetPivot.localRotation = Quaternion.Euler(preset.localEuler);

        // ✅ 수정: 오더 오프셋 누적 방식
        ApplySorting(preset);
    }

    private void ApplyNonAimPose()
    {
        Vector2 to = target.position - owner.position;
        float yaw = AngleDeg(to);
        cachedDir = YawToFourDir(yaw);
        var preset = GetPreset(false, cachedDir);

        // ✅ 수정: 단순 적용
        presetPivot.localPosition = preset.localPosition;
        presetPivot.localRotation = Quaternion.Euler(preset.localEuler);

        // ✅ 수정: 오더 오프셋 누적 방식
        ApplySorting(preset);
    }

    // ─────────────────────────────────────────────────────────
    // ■ Z-Yaw 회전(조준 상태)
    // ─────────────────────────────────────────────────────────
    private void ApplyYawRotation(float dt)
    {
        Vector2 to = target.position - owner.position;
        float targetYaw = AngleDeg(to);
        currentYaw = Mathf.MoveTowardsAngle(currentYaw, targetYaw, turnSpeedDegPerSec * dt);
        weaponRoot.localRotation = Quaternion.Euler(0f, 0f, currentYaw);
    }

    // ─────────────────────────────────────────────────────────
    // ■ 스프라이트 오더 적용 (누적형)
    // ─────────────────────────────────────────────────────────
    private void ApplySorting(DirectionPreset p)
    {
        if (!weaponSR) return;

        // ✅ 수정: OrderOffset이 고정이 아닌 누적형으로 적용
        if (p.overrideOrder)
        {
            weaponSR.sortingOrder = p.orderInLayer;
        }
        else
        {
            int currentOrder = weaponSR.sortingOrder;  // 현재 오더값 기반으로 누적
            int order = baseOrderInLayer;

            if (p.useOrderOffset)
                order = currentOrder + p.orderOffset;  // 기존 값에 더함
            else
                order = currentOrder + baseOrderInLayer; // 기본값만 누적

            weaponSR.sortingOrder = order;
        }
    }

    // ─────────────────────────────────────────────────────────
    // ■ 근접 공격 트리거
    // ─────────────────────────────────────────────────────────
private void TryMeleeTrigger() // ✅ 근접 공격 "방향 확정 후" 다음 프레임 실행 예약
{
    if (!combatEnabled) return;            // 전투 OFF면 종료
    if (!melee) return;                    // 근접 공격기 없으면 종료
    if (meleeCdTimer > 0f) return;         // 쿨다운 중이면 종료
    if (pendingMeleeAttack) return;        // ✅ 이미 대기 중이면 중복 예약 금지

    if (target == null) return;            // 타겟 없으면 종료

    // ✅ 1) 타겟 방향으로 4방향 상태를 먼저 확정시키기(ExternalFacingOverride)
    Vector2 toTarget = (Vector2)(target.position - owner.position); // 타겟 방향 벡터
    if (toTarget.sqrMagnitude < 0.000001f) return; // 방향이 거의 0이면 종료

    pendingFacingDir = toTarget; // ✅ 대기 공격에 사용할 방향 저장

    if (creature != null) // 이동 AI가 있으면
    {
        creature.SetExternalFacingOverride(pendingFacingDir); // ✅ 4방향 전환을 먼저 유도(이번 프레임)
    }

    // ✅ 2) 공격은 "다음 프레임"에 실행(방향 전환이 Update에서 반영될 시간 확보)
    pendingMeleeAttack = true;             // 대기 시작
    pendingMeleeFrame = Time.frameCount;   // 현재 프레임 기록
}

private void TryExecutePendingMeleeAttack() // ✅ 예약된 근접 공격을 실행(다음 프레임)
{
    if (!pendingMeleeAttack) return;       // 대기 중 아니면 종료
    pendingMeleeAttack = false;            // ✅ 먼저 해제(중복 실행 방지)

    if (!combatEnabled) return;            // 전투 OFF면 종료
    if (!melee) return;                    // 공격기 없으면 종료
    if (meleeCdTimer > 0f) return;         // 쿨다운이면 종료

    bool started = melee.BeginAttack();    // 실제 공격 시작 시도
    if (!started) return;                  // 시작 실패면 종료(다음 프레임에 다시 예약될 수 있음)

    meleeCdTimer = aiMeleeCooldown;        // 쿨다운 적용

    if (creature != null)                  // 이동/방향 잠금이 필요하면
        creature.RequestMeleeMoveFacingLock(aiMeleeMoveLockDuration); // 잠금 요청(기존 기능 유지)
}


public void ApplyCombatSettingsFromAI(float dAim, float dMelee, float hys, float meleeCd, float moveLockDur) // ✅ AI 설정 주입
{
    aiDistanceToAim = Mathf.Max(0f, dAim);                 // 조준 거리 반영
    aiDistanceToMelee = Mathf.Max(0f, dMelee);             // 근접 거리 반영
    aiHysteresis = Mathf.Max(0f, hys);                     // 히스테리시스 반영
    aiMeleeCooldown = Mathf.Max(0f, meleeCd);              // 쿨다운 반영
    aiMeleeMoveLockDuration = Mathf.Max(0f, moveLockDur);  // 잠금 시간 반영
}



#if UNITY_EDITOR
private void OnDrawGizmosSelected() // ✅ 주입값 기준 반경 표시
{
    if (!owner) return; // 가드
    Gizmos.color = new Color(0f, 1f, 0.7f, 0.25f); // 조준 반경 색
    Gizmos.DrawWireSphere(owner.position, aiDistanceToAim); // 조준 반경
    Gizmos.color = new Color(1f, 0.3f, 0.2f, 0.35f); // 근접 반경 색
    Gizmos.DrawWireSphere(owner.position, aiDistanceToMelee); // 근접 반경
}
#endif

}
