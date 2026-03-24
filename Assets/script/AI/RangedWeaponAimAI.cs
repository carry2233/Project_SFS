using System;
using UnityEngine;

[AddComponentMenu("Combat/RangedWeaponAimAI(AI원거리조준)")]
public class RangedWeaponAimAI : MonoBehaviour
{
    // ─────────────────────────────────────────────────────────
    // ■ 직렬화 프리셋 구조 (MeleeWeaponAimAI 구조 유지)  // ✅ 신규
    // ─────────────────────────────────────────────────────────
[Serializable]
public struct DirectionPreset
{
    public Vector3 localPosition;   // 로컬 포지션
    public Vector3 localEuler;      // 로컬 오일러
    public bool useOrderOffset;     // 오더 오프셋 사용 여부
    public int orderOffset;         // 오더 오프셋 값
    public bool overrideOrder;      // 오더 직접 덮어쓰기 여부
    public int orderInLayer;        // 덮어쓸 오더값

    public enum FlipAxis { None, X, Y } // 플립 적용 축(None=미적용, X=flipX, Y=flipY)
    public FlipAxis flipAxis;           // 프리셋별 플립 축 설정
}


    public enum FourDir { Up, Right, Down, Left } // 4방향 열거형

    // ─────────────────────────────────────────────────────────
    // ■ 참조  // ✅ 신규
    // ─────────────────────────────────────────────────────────
    [Header("참조")]
    [SerializeField] private Transform owner;                // 소유자(기준 Transform)
    [SerializeField] private Transform presetPivot;          // 프리셋 포즈/회전 적용 대상
    [SerializeField] private Transform weaponRoot;           // 조준 회전(Z-Yaw) 적용 대상
    [SerializeField] private SpriteRenderer weaponSR;        // 무기 스프라이트(정렬 조정용)
    [SerializeField] private EntityMovementAI movementAI;    // 이동/스프라이트(조준 방향 전달용)
    [SerializeField] private GunShooting gunShooting;        // 실제 발사 실행 담당

    // ─────────────────────────────────────────────────────────
    // ■ 프리셋(비조준)  // ✅ 신규
    // ─────────────────────────────────────────────────────────
    [Header("방향별 프리셋 - 비조준(Non-Aim)")]
    [SerializeField] private DirectionPreset nonAimUp;       // 비조준-위
    [SerializeField] private DirectionPreset nonAimRight;    // 비조준-오른쪽
    [SerializeField] private DirectionPreset nonAimDown;     // 비조준-아래
    [SerializeField] private DirectionPreset nonAimLeft;     // 비조준-왼쪽

    // ─────────────────────────────────────────────────────────
    // ■ 프리셋(조준)  // ✅ 신규
    // ─────────────────────────────────────────────────────────
    [Header("방향별 프리셋 - 조준(Aim)")]
    [SerializeField] private DirectionPreset aimUp;          // 조준-위
    [SerializeField] private DirectionPreset aimRight;       // 조준-오른쪽
    [SerializeField] private DirectionPreset aimDown;        // 조준-아래
    [SerializeField] private DirectionPreset aimLeft;        // 조준-왼쪽

    // ─────────────────────────────────────────────────────────
    // ■ 동작 파라미터(포즈/회전/정렬)  // ✅ 신규
    // ─────────────────────────────────────────────────────────
    [Header("동작 파라미터")]
    [SerializeField, Min(0f)] private float poseLerpSpeed = 10f;      // 포즈 보간 속도
    [SerializeField, Min(0f)] private float turnSpeedDegPerSec = 720f; // 조준 회전 속도(도/초)
    [SerializeField] private int baseOrderInLayer = 0;                 // 기본 정렬 오더

    // ─────────────────────────────────────────────────────────
    // ■ AI 사격 설정(요청사항)  // ✅ 신규
    // ─────────────────────────────────────────────────────────
    public enum AIFireMode { Single, Auto, Burst } // 단발/연발/점사

    [Header("AI 사격 설정")]
    [SerializeField] private AIFireMode fireMode = AIFireMode.Single; // AI 발사 모드
    [SerializeField, Min(0.01f)] private float singleFirePeriod = 0.8f; // 단발 발사 주기(초)
    [SerializeField, Min(0.01f)] private float autoFirePeriod = 0.12f;  // 연발 발사 주기(초)
    [SerializeField, Min(1)] private int burstCount = 3;                // 점사 탄수
    [SerializeField, Min(0.01f)] private float burstFirePeriod = 0.12f; // 점사 발사 주기(초) (탄간/다음점사 공통)

    // ─────────────────────────────────────────────────────────
// ■ 조준→사격 지연(추가)  // ✅ 추가
// ─────────────────────────────────────────────────────────
[Header("조준→사격 지연(랜덤)")]                           // 조준 진입 후 사격까지 랜덤 대기 헤더  // ✅ 추가
[SerializeField, Min(0f)] private float aimToFireDelayMin = 0.05f; // 최소 지연(초)  // ✅ 추가
[SerializeField, Min(0f)] private float aimToFireDelayMax = 0.25f; // 최대 지연(초)  // ✅ 추가

private bool wasAimingLastFrame = false;                    // 조준 진입 감지용(이전 프레임)  // ✅ 추가
private bool aimDelayActive = false;                        // 현재 지연 대기 중인지  // ✅ 추가
private float aimDelayRemaining = 0f;                       // 남은 지연 시간(시간값1)  // ✅ 추가


    // ─────────────────────────────────────────────────────────
    // ■ 외부 제어  // ✅ 신규
    // ─────────────────────────────────────────────────────────
    [Header("외부 제어")]
    [SerializeField] private bool combatEnabled = true; // 상위 AI가 전투 허용/차단


private Transform target;                 // 현재 타겟  // (기존)
private bool aiming;                     // 현재 조준 상태(내부 진행 상태)  // (기존)

// ✅ 추가: 외부(AI)에서 들어온 "원하는" 조준/사격 입력을 저장
private bool desiredAim;                 // 외부에서 요청한 조준 ON/OFF  // ✅ 추가
private bool desiredFire;                // 외부에서 요청한 사격 ON/OFF  // ✅ 추가

    private FourDir cachedDir = FourDir.Right; // 최근 방향 캐시

    private float nextSingleTime = 0f; // 다음 단발 가능 시각
    private float nextAutoTime = 0f;   // 다음 연발 가능 시각
    private int burstLeft = 0;         // 점사 남은 탄수
    private float nextBurstTime = 0f;  // 점사 다음 발사 시각
    public bool IsCombatEnabled => combatEnabled; // ▶ 외부에서 현재 활성 상태 확인용


    // ─────────────────────────────────────────────────────────
    // ■ Unity  // ✅ 신규
    // ─────────────────────────────────────────────────────────
    private void Awake() // 자동 보정
    {
        if (owner == null) owner = transform; // owner 미지정 시 자기 자신 사용
    }

private void Update() // 매 프레임 전투 처리
{
    if (!combatEnabled) return; // 전투 차단이면 종료

    ProcessCombat(Time.deltaTime); // dt 전달(회전 속도/보간 정상)  // ✅ 수정
}






    // ─────────────────────────────────────────────────────────
    // ■ 외부 API (AberrantEntityAI에서 호출)  // ✅ 신규
    // ─────────────────────────────────────────────────────────
    public void SetTarget(Transform t) // 타겟 지정
    {
        target = t; // 타겟 저장
    }

    public void SetCombatInput(bool aimOn, bool fireOn) // ✅ 추가: 외부 입력만 저장(실행은 Update가 담당)
{
    desiredAim = aimOn;   // 조준 입력 저장
    desiredFire = fireOn; // 사격 입력 저장
}



public void SetCombatEnabled(bool enabled) // 전투 허용/차단
{
    combatEnabled = enabled;

    if (!combatEnabled)
    {
        ForceStopAiming(); // 조준/방향 오버라이드 완전 해제
    }
}


public void TickCombat(bool aimOn, bool fireOn) // ✅ 수정: 이제는 입력 저장만 하고, 실제 실행은 Update가 담당
{
    SetCombatInput(aimOn, fireOn); // ✅ 입력 저장
    // ProcessCombat()은 Update()에서 매 프레임 실행되므로 여기서 직접 실행하지 않음(중복 방지)
}

private void ProcessCombat(float dt) // 매 프레임 실행되는 실제 조준/회전/발사 처리  // ✅ 수정
{
    if (!combatEnabled) return; // 전투 차단이면 종료  // (기존)
    if (!owner || !presetPivot || !weaponRoot) return; // 필수 참조 가드  // (기존)

    if (target == null) // 타겟 없으면 정리  // (기존)
    {
        ForceStopAiming(); // 조준/방향 오버라이드 해제  // (기존)
        return;
    }

    // ✅ 조준 상태 반영(외부 입력 → 내부 상태)
    aiming = desiredAim; // 외부 입력을 현재 조준 상태로 반영  // (기존)

    // ✅ 조준 진입 감지(이번 프레임에 false → true로 바뀐 경우)
    bool aimJustEntered = (!wasAimingLastFrame && aiming); // 조준 진입 여부  // ✅ 추가
    if (aimJustEntered)
        BeginAimToFireDelay(); // 시간값1 확정 및 지연 시작  // ✅ 추가

    if (!aiming) // 비조준이면  // (기존)
    {
        ApplyNonAimPose(); // 비조준 프리셋 적용  // (기존)
        weaponRoot.localRotation = Quaternion.identity; // 회전 초기화  // (기존)

        if (movementAI != null) movementAI.ClearExternalFacingOverride(); // 외부 방향 해제  // (기존)
        burstLeft = 0; // 점사 상태 정리  // (기존)

        // ✅ 비조준으로 돌아가면 지연도 취소(다음 조준 진입 때 다시 랜덤)
        aimDelayActive = false; // 지연 비활성  // ✅ 추가
        aimDelayRemaining = 0f; // 남은 시간 초기화  // ✅ 추가

        wasAimingLastFrame = false; // 이전 프레임 조준 상태 갱신  // ✅ 추가
        return;
    }

    // ✅ 조준 상태면: 매 프레임 타겟 방향으로 회전 갱신
    ApplyAimPose(); // 조준 프리셋 적용  // (기존)
    ApplyYawRotation(dt); // weaponRoot가 타겟 방향을 실시간 추적  // (기존)

    Vector2 to = (Vector2)(target.position - owner.position); // 타겟 방향 벡터  // (기존)
    if (movementAI != null) movementAI.SetExternalFacingOverride(to); // 조준 방향 기준 4방향  // (기존)

    // ✅ 조준 중이라면 지연 타이머 진행
    if (aimDelayActive) // 지연 대기 중이면  // ✅ 추가
    {
        aimDelayRemaining -= dt; // 남은 시간 감소  // ✅ 추가
        if (aimDelayRemaining <= 0f) // 지연 종료면  // ✅ 추가
        {
            aimDelayRemaining = 0f; // 보정  // ✅ 추가
            aimDelayActive = false; // 발사 허용  // ✅ 추가
        }
    }

    // ✅ 발사 게이트: desiredFire가 켜져 있어도, 지연이 끝나야만 발사
    if (desiredFire && !aimDelayActive) // 사격 ON + 지연 종료  // ✅ 수정
        TryFireByMode(); // 모드별 발사  // (기존)
    else
    {
        // 사격 OFF면 점사 중단(기존)
        if (!desiredFire) burstLeft = 0; // 사격 OFF면 점사 중단  // (기존)

        // ✅ 지연 중에는 점사 잔여가 남아있지 않게 안전 정리(옵션 성격이지만 안정성↑)
        if (aimDelayActive) burstLeft = 0; // 지연 중 점사 진행 방지  // ✅ 추가
    }

    wasAimingLastFrame = aiming; // 이전 프레임 조준 상태 갱신  // ✅ 추가
}



    // ─────────────────────────────────────────────────────────
    // ■ 모드별 발사  // ✅ 신규
    // ─────────────────────────────────────────────────────────
    private void TryFireByMode() // 모드별 발사 스케줄링
    {
        if (gunShooting == null) return; // 총 참조 없으면 종료

        float now = Time.time; // 현재 시각

        switch (fireMode) // 모드 분기
        {
            case AIFireMode.Single:
                if (now >= nextSingleTime) // 단발 쿨타임 종료면
                {
                    gunShooting.FireOneShot(); // 한 발 발사
                    nextSingleTime = now + singleFirePeriod; // 다음 단발 예약
                }
                break;

            case AIFireMode.Auto:
                if (now >= nextAutoTime) // 연발 주기 도달이면
                {
                    gunShooting.FireOneShot(); // 한 발 발사(연발은 주기로 반복 호출)
                    nextAutoTime = now + autoFirePeriod; // 다음 연발 예약
                }
                break;

            case AIFireMode.Burst:
                if (burstLeft <= 0 && now >= nextBurstTime) // 점사 시작 조건
                {
                    burstLeft = Mathf.Max(1, burstCount); // 점사 탄수 세팅
                }

                if (burstLeft > 0 && now >= nextBurstTime) // 점사 진행 조건
                {
                    gunShooting.FireOneShot(); // 한 발 발사
                    burstLeft--; // 남은 탄수 감소
                    nextBurstTime = now + burstFirePeriod; // 다음 발사 예약(탄간/다음점사 공통)
                }
                break;
        }
    }

    // ─────────────────────────────────────────────────────────
    // ■ 조준 각도/방향 판정  // ✅ 신규
    // ─────────────────────────────────────────────────────────
    private static float AngleDeg(Vector2 v) // 벡터 -> 0~360 각도
    {
        float a = Mathf.Atan2(v.y, v.x) * Mathf.Rad2Deg; // 라디안 -> 도
        if (a < 0f) a += 360f; // 0~360 보정
        return a; // 반환
    }

    private FourDir YawToFourDir(float yaw) // 90도 부채꼴(상/우/하/좌) 분류
    {
        if (yaw > 315f || yaw <= 45f) return FourDir.Right; // 오른쪽
        if (yaw > 45f && yaw <= 135f) return FourDir.Up;    // 위
        if (yaw > 135f && yaw <= 225f) return FourDir.Left; // 왼쪽
        return FourDir.Down;                                 // 아래
    }

    // ─────────────────────────────────────────────────────────
    // ■ 프리셋 선택/적용  // ✅ 신규
    // ─────────────────────────────────────────────────────────
    private DirectionPreset GetPreset(bool aim, FourDir dir) // 조준/비조준 + 방향 프리셋 선택
    {
        if (aim)
        {
            switch (dir)
            {
                case FourDir.Up: return aimUp;       // 조준-위
                case FourDir.Right: return aimRight; // 조준-오른쪽
                case FourDir.Down: return aimDown;   // 조준-아래
                case FourDir.Left: return aimLeft;   // 조준-왼쪽
            }
        }
        else
        {
            switch (dir)
            {
                case FourDir.Up: return nonAimUp;       // 비조준-위
                case FourDir.Right: return nonAimRight; // 비조준-오른쪽
                case FourDir.Down: return nonAimDown;   // 비조준-아래
                case FourDir.Left: return nonAimLeft;   // 비조준-왼쪽
            }
        }
        return aimRight; // 폴백
    }

    private void ApplyAimPose() // 조준 프리셋 적용
    {
        Vector2 to = (Vector2)(target.position - owner.position); // 타겟 방향
        float yaw = AngleDeg(to); // 각도
        cachedDir = YawToFourDir(yaw); // 방향 캐싱

        DirectionPreset preset = GetPreset(true, cachedDir); // 조준 프리셋 선택
        ApplyPreset(preset); // 프리셋 적용
    }

    private void ApplyNonAimPose() // 비조준 프리셋 적용
    {
        Vector2 to = (Vector2)(target.position - owner.position); // 타겟 방향
        float yaw = AngleDeg(to); // 각도
        cachedDir = YawToFourDir(yaw); // 방향 캐싱

        DirectionPreset preset = GetPreset(false, cachedDir); // 비조준 프리셋 선택
        ApplyPreset(preset); // 프리셋 적용
    }

private void ApplyPreset(DirectionPreset preset) // 프리셋 값 적용(보간 포함)
{
    // 포즈 보간(요청 구조: 동작 파라미터 유지)
    presetPivot.localPosition = Vector3.Lerp(presetPivot.localPosition, preset.localPosition, Time.deltaTime * poseLerpSpeed); // 위치 보간
    Quaternion targetRot = Quaternion.Euler(preset.localEuler); // 목표 회전
    presetPivot.localRotation = Quaternion.Slerp(presetPivot.localRotation, targetRot, Time.deltaTime * poseLerpSpeed); // 회전 보간

    ApplySorting(preset); // 정렬 적용
    ApplyFlip(preset);    // ✅ 추가: 스프라이트 플립 적용
}

private void ApplyFlip(DirectionPreset preset) // ✅ 프리셋 기반 weaponSR 플립 적용
{
    if (weaponSR == null) return; // 스프라이트 없으면 종료

    weaponSR.flipX = false; // 기본값 초기화(항상 통제)
    weaponSR.flipY = false; // 기본값 초기화(항상 통제)

    switch (preset.flipAxis) // 프리셋 축 설정에 따라 적용
    {
        case DirectionPreset.FlipAxis.X:
            weaponSR.flipX = true; // X축 플립 적용
            break;

        case DirectionPreset.FlipAxis.Y:
            weaponSR.flipY = true; // Y축 플립 적용
            break;

        case DirectionPreset.FlipAxis.None:
        default:
            break; // 미적용
    }
}


    private void ApplySorting(DirectionPreset preset) // 정렬/오더 적용
    {
        if (weaponSR == null) return; // 스프라이트 없으면 종료

        if (preset.overrideOrder) // 오더 덮어쓰기면
        {
            weaponSR.sortingOrder = preset.orderInLayer; // 지정 값 적용
            return;
        }

        int order = baseOrderInLayer; // 기본 오더
        if (preset.useOrderOffset) order += preset.orderOffset; // 오프셋 누적
        weaponSR.sortingOrder = order; // 최종 오더 적용
    }

    // ─────────────────────────────────────────────────────────
    // ■ 조준 회전  // ✅ 신규
    // ─────────────────────────────────────────────────────────
    private void ApplyYawRotation(float dt) // weaponRoot를 타겟 방향으로 회전
    {
        Vector2 to = (Vector2)(target.position - owner.position); // 타겟 방향
        if (to.sqrMagnitude < 0.000001f) return; // 너무 가까우면 무시

        float targetYaw = AngleDeg(to); // 목표 yaw(0~360)
        float currentYaw = weaponRoot.localEulerAngles.z; // 현재 yaw
        float nextYaw = Mathf.MoveTowardsAngle(currentYaw, targetYaw, turnSpeedDegPerSec * dt); // 속도 제한 회전
        weaponRoot.localRotation = Quaternion.Euler(0f, 0f, nextYaw); // Z축 회전 적용
    }

    // ─────────────────────────────────────────────────────────
    // ■ 정리  // ✅ 신규
    // ─────────────────────────────────────────────────────────
private void ForceStopAiming() // 전투 OFF/타겟 없음 시 정리
{
    aiming = false; // 조준 해제
    desiredAim = false; // 외부 조준 입력도 초기화
    desiredFire = false; // 외부 사격 입력도 초기화

    burstLeft = 0; // 점사 정리

    aimDelayActive = false; // 지연 상태 해제
    aimDelayRemaining = 0f; // 남은 시간 초기화
    wasAimingLastFrame = false; // 조준 진입 감지 상태 초기화

    if (movementAI != null) movementAI.ClearExternalFacingOverride(); // 외부 방향 해제

    if (weaponSR != null) // ✅ 추가: 플립 상태 정리
    {
        weaponSR.flipX = false; // X 플립 해제
        weaponSR.flipY = false; // Y 플립 해제
    }
}




    public void ClearCombatInput()
{
    desiredAim = false;   // 조준 입력 해제
    desiredFire = false;  // 사격 입력 해제
}

private void BeginAimToFireDelay() // 조준 진입 시 랜덤 지연(시간값1) 시작  // ✅ 추가
{
    float min = Mathf.Min(aimToFireDelayMin, aimToFireDelayMax); // 최소/최대 역전 방지  // ✅ 추가
    float max = Mathf.Max(aimToFireDelayMin, aimToFireDelayMax); // 최소/최대 역전 방지  // ✅ 추가

    aimDelayRemaining = UnityEngine.Random.Range(min, max);      // 시간값1 결정  // ✅ 추가
    aimDelayActive = aimDelayRemaining > 0f;                     // 0이면 즉시 발사 허용  // ✅ 추가

    burstLeft = 0;                                               // 조준 진입 시 점사 잔여 탄 정리  // ✅ 추가
}



}
