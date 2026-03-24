using System.Collections.Generic;                    // 리스트 사용
using UnityEngine;                                   // 유니티 기본 네임스페이스
using UnityEngine.Serialization;                     // ✅ FormerlySerializedAs 사용(인스펙터 값 유지)


[AddComponentMenu("Combat/MeleeWeaponAttack")]       // 인스펙터 메뉴 경로
public class MeleeWeaponAttack : MonoBehaviour
{
    // ─────────────────────────────────────────────────────────────────────────────
    // 설정형 타입들
    // ─────────────────────────────────────────────────────────────────────────────

    public enum RadiusMode                                 // 공전 반지름 결정 방식
    { 
        UseCurrentOffset,                                  // 시작 시점의 pivot→orbitObject 오프셋 길이 사용
        FixedRadius                                        // 고정 반지름 값 사용
    }

    [System.Flags]
    public enum SpinAxes                                   // 무기 스핀 적용 축(X/Y/Z 선택 플래그)
    {
        X = 1,                                             // X축
        Y = 2,                                             // Y축
        Z = 4                                              // Z축
    }

    public enum SpinSpace                                  // 스핀 회전의 좌표계(Local/World)
    {
        Local,                                             // 로컬축 기준
        World                                              // 월드축 기준
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // (A) 참조
    // ─────────────────────────────────────────────────────────────────────────────

    [Header("참조")]
    [SerializeField] private Transform pivot;              // 기준점(플레이어 등)
    [SerializeField] private Transform orbitObject;        // 공전 오브젝트(활성/비활성 대상)
    [SerializeField] private Transform weaponObject;       // 무기 오브젝트(스핀/종료 회전값 적용)
    [SerializeField] private Camera cameraForAim;          // 마우스→월드 변환용 카메라(비우면 Camera.main)

    [Header("전투 페이로드")]
    [SerializeField] private CombatPayload2D combatPayload; // ✅ CombatPayload2D 참조(최종공격력 적용 대상)


    // ─────────────────────────────────────────────────────────────────────────────
    // (B) 시각 요소
    // ─────────────────────────────────────────────────────────────────────────────

    [Header("공전 중 비활성화할 스프라이트")]
    [SerializeField] private List<SpriteRenderer> spritesToDisableWhileOrbit = new(); // 공전 중 꺼둘 스프라이트들

    // ─────────────────────────────────────────────────────────────────────────────
    // (C) 입력/트리거
    // ─────────────────────────────────────────────────────────────────────────────

    [Header("입력/트리거")]
    [SerializeField] private bool requireRightMouseHold = true; // 우클릭 유지 필요 여부
    [SerializeField] private int triggerMouseButton = 0;        // 발동 마우스 버튼(0=좌클릭)

    // ─────────────────────────────────────────────────────────────────────────────
    // (D) 공전(오비트) 파라미터
    // ─────────────────────────────────────────────────────────────────────────────

[Header("공전 설정")]
[FormerlySerializedAs("orbitDuration")]
[SerializeField, Min(0f)] private float baseOrbitDuration = 0.3f;     // ✅ 기본 공전 지속(초)

[FormerlySerializedAs("orbitAngularSpeed")]
[SerializeField] private float baseOrbitAngularSpeed = 360f;          // ✅ 기본 공전 각속도(°/s)

[SerializeField] private float startAngleOffsetZ = 0f;                // 시작각 보정(Z°)
[SerializeField] private bool useLocalRotation = true;                // orbitObject 회전을 로컬 Z로 적용할지
[SerializeField] private RadiusMode radiusMode = RadiusMode.UseCurrentOffset; // 반지름 모드
[SerializeField, Min(0f)] private float fixedRadius = 1f;             // 고정 반지름


    // ─────────────────────────────────────────────────────────────────────────────
    // (E) 무기 스핀(공전 중 무기 자체 회전)
    // ─────────────────────────────────────────────────────────────────────────────

[Header("무기 스핀(공전 중 무기 자체 회전)")]
[FormerlySerializedAs("weaponSpinSpeed")]
[SerializeField] private float baseWeaponSpinSpeed = 0f;              // ✅ 기본 무기 스핀 속도(°/s)

[SerializeField] private SpinAxes weaponSpinAxes = SpinAxes.Z;         // 스핀 축
[SerializeField] private SpinSpace weaponSpinSpace = SpinSpace.Local;  // 로컬/월드

[Header("위력 스케일(요청 규칙)")]
[SerializeField, Min(0f)] private float minOrbitDuration = 0.05f;      // ✅ 공전시간 최소값(너무 짧아지는 것 방지)
[SerializeField, Min(0)] private int baseAttackDamage = 10;            // ✅ 기본 공격력(정수)

[Header("최종값(런타임 계산, 실사용)")]
[SerializeField] private int finalAttackDamage = 10;                   // ✅ 최종 공격력(정수)
[SerializeField] private float finalOrbitDuration = 0.3f;              // ✅ 최종 공전 지속(초)
[SerializeField] private float finalOrbitAngularSpeed = 360f;          // ✅ 최종 공전 각속도(°/s)
[SerializeField] private float finalWeaponSpinSpeed = 0f;              // ✅ 최종 무기 스핀 속도(°/s)

private float baseOrbitTotalDegrees = 0f;                               // ✅ 기본 총 공전 회전각(°) = baseSpeed * baseDuration
private float baseSpinTotalDegrees = 0f;                                // ✅ 기본 총 무기회전 회전각(°) = baseSpinSpeed * baseDuration



    // ─────────────────────────────────────────────────────────────────────────────
    // (F) 종료 시 적용값
    // ─────────────────────────────────────────────────────────────────────────────

    [Header("종료 시 적용")]
    [SerializeField] private float weaponZOnDisable = 0f;            // 종료 시 무기 Z(절대값)
    [SerializeField] private bool reactivateSpritesOnEnd = true;     // 종료 시 스프라이트 복구
    [SerializeField] private bool deactivateOrbitObjectOnEnd = true; // 종료 시 공전 오브젝트 비활성

    // ===== 종료 시 회전값 적용 (추가됨) =====
    [Header("종료 시 공전오브젝트 회전값 설정")]
    [SerializeField] private bool applyOrbitObjectZOnDisable = true;  // 종료 시 Z 회전 적용
    [SerializeField] private float orbitObjectZOnDisable = 0f;        // 종료 시 공전오브젝트 Z
    [SerializeField] private float orbitObjectXOnDisable = 90f;       // 종료 시 공전오브젝트 X

    [Header("공격 중 조작 잠금")]
    [SerializeField] private MovementToggleRelay movementRelay;        // PlayerMovement 토글 릴레이
    [SerializeField] private bool lockOnBegin = true;                   // 시작 시 잠금
    [SerializeField] private bool unlockOnEnd = true;                   // 종료 시 해제

    [Header("입력 제어")]
    [SerializeField] public bool inputEnabled = true;                  // 입력 사용 여부

    // ─────────────────────────────────────────────────────────────────────────────
    // (★ 기존) 잠금 범위
    // ─────────────────────────────────────────────────────────────────────────────
    [Header("잠금 범위 (옵션)")]
    [SerializeField] private bool lockOnlyForThisAttack = false;       // 본인 공격만 잠금 여부

    // ─────────────────────────────────────────────────────────────────────────────
    // (★ 추가) 호환 정수
    // ─────────────────────────────────────────────────────────────────────────────
    [Header("호환 정수 (같은 값끼리만 정지 작동)")]
    [SerializeField] private int compatibilityId = 0;                  // 이 공격의 호환 정수(ID)

    // ─────────────────────────────────────────────────────────────────────────────
// (B-2) 트레일 / 재공격 딜레이(추가)
// ─────────────────────────────────────────────────────────────────────────────

[Header("트레일(공전 중만 활성)")]
[SerializeField] private List<TrailRenderer> trailsToToggle = new();  // ✅ 공전 중 켤 트레일들(리스트1)
[SerializeField] private bool clearTrailsWhenOff = true;              // 트레일 끌 때 Clear 할지 여부

[Header("재공격 딜레이")]
[SerializeField, Min(0f)] private float reAttackDelay = 0.2f;         // ✅ 공격 종료 후 재공격까지 대기(초)
private float reAttackTimer = 0f;                                     // 남은 재공격 대기 시간(초)

// ─────────────────────────────────────────────────────────────
// [추가] PlayerGun 참조 (공격 중 gunRoot Freeze용)
// ─────────────────────────────────────────────────────────────
[Header("External Control")]
[SerializeField] private PlayerGun playerGun;   // ✅ 공격 중 PlayerGun 제어 차단용

[Header("스탯 연동(단방향 참조)")]
[SerializeField] private MeleeStatSystem meleeStatSystem;        // MeleeStatSystem 참조(인스펙터 연결)


[Header("위력 값 단계별 표시")]

[SerializeField, Min(0.0001f)]
private float basePowerValue = 1f;          // ① 기본 위력값 (설계 기준)

[SerializeField]
private float bonusPowerValue = 0f;         // ② 보정된 위력값 (스탯 기반)

[SerializeField]
private float appliedPowerValue = 1f;       // ③ 실질 적용된 위력값 (최종)

[Header("위력 → 템포 압축 조절")]
[SerializeField, Min(0f)]
private float powerCompressionFactor = 1f; 
// 0 = 위력과 무관
// 1 = 기존과 동일
// >1 = 위력 효과 과장



[Header("위력 보정 적용 상태")]
[SerializeField] private bool powerBonusApplied = false;          // 보정치 1회 적용 여부(누적 방지)




    // ─────────────────────────────────────────────────────────────────────────────
    // 내부 상태
    // ─────────────────────────────────────────────────────────────────────────────

    private bool isOrbiting = false;                        // 공전 진행 중 여부
    private float orbitTimer = 0f;                          // 공전 경과(초)
    private float startAngleDeg = 0f;                       // 시작 각도(Z°)
    private float radius = 0f;                              // 반지름
    private Vector2 cachedOffsetAtStart = Vector2.zero;     // 시작 오프셋
    private bool hadSpritesDisabled = false;                // 공전 중 스프라이트 끔 여부
    private Camera cam;                                     // 유효 카메라
    private bool isRightMousePrimed = false;   // ✅ 우클릭이 먼저 눌렸는지 여부
    // ─────────────────────────────────────────
// [추가] 공격 실행 가능 여부 (입력 순서 잠금용)
// ─────────────────────────────────────────
private bool canTriggerAttack = true;   // ✅ 좌클릭 선행 시 false로 잠김



    

    private void Reset()                                    // 기본 셋업
    {
        if (cameraForAim == null) cameraForAim = Camera.main;
    }

private void Awake()
{
    cam = cameraForAim != null ? cameraForAim : Camera.main;
    if (pivot == null) pivot = transform;

    ApplyPowerFromStatIfReady();   // ✅ 위력값 갱신
    RecalculateFinalStats();       // ✅ 실전 수치 계산

    SetTrailsActive(false);
}





private void Update()                                                  // 입력 / 상태 업데이트
{
    TickReAttackCooldown();                                            // 재공격 딜레이 감소

    if (!inputEnabled)                                                 // ✅ 입력 비활성화(AI 등)라면
    {
        if (isOrbiting)                                                // ✅ 공전 중이면 입력 없이도 공전은 진행
        {
            UpdateOrbit();                                             // 공전 진행
        }
        return;                                                        // 입력 로직은 실행하지 않음
    }

    bool leftDown  = Input.GetMouseButtonDown(triggerMouseButton);     // 좌클릭 Down
    bool leftHeld  = Input.GetMouseButton(triggerMouseButton);         // 좌클릭 Hold
    bool leftUp    = Input.GetMouseButtonUp(triggerMouseButton);       // 좌클릭 Up

    bool rightDown = Input.GetMouseButtonDown(1);                      // 우클릭 Down
    bool rightHeld = Input.GetMouseButton(1);                          // 우클릭 Hold
    bool rightUp   = Input.GetMouseButtonUp(1);                        // 우클릭 Up

    // ─────────────────────────────────────────
    // [1] 모든 입력이 해제되면 사이클 리셋
    // ─────────────────────────────────────────
    if (!leftHeld && !rightHeld)                                       // 입력 모두 해제
    {
        canTriggerAttack = true;                                       // 다시 공격 가능
        isRightMousePrimed = false;                                    // 우클릭 선행 초기화
    }

    // ─────────────────────────────────────────
    // [2] 좌클릭이 먼저 눌리면 이번 사이클 공격 금지
    // ─────────────────────────────────────────
    if (leftDown && !rightHeld)                                        // 좌클릭 선행
    {
        canTriggerAttack = false;                                      // 공격 잠금
    }

    // ─────────────────────────────────────────
    // [3] 우클릭이 먼저 눌리면 선행 상태 인정
    // ─────────────────────────────────────────
    if (rightDown && canTriggerAttack)                                 // 우클릭 선행 조건 충족
    {
        isRightMousePrimed = true;                                     // 우클릭 선행 인정
    }

    if (rightUp)                                                       // 우클릭 해제
    {
        isRightMousePrimed = false;                                    // 선행 해제
    }

    // ─────────────────────────────────────────
    // [4] 공격 실행 조건
    // ─────────────────────────────────────────
    bool attackRequested =
        canTriggerAttack &&                                            // 좌클릭 선행이 아니어야 함
        isRightMousePrimed &&                                          // 우클릭 선행 필수
        leftHeld;                                                      // 좌클릭 Hold

    if (!isOrbiting && attackRequested && reAttackTimer <= 0f)         // 공격 시작 조건
    {
        BeginOrbit();                                                  // 공격 시작
    }

    // ─────────────────────────────────────────
    // [5] 공격 진행
    // ─────────────────────────────────────────
    if (isOrbiting)                                                    // 공전 중
    {
        UpdateOrbit();                                                 // 공전 진행
    }
}


private void ApplyPowerFromStatIfReady()
{
    // 기본값으로 항상 초기화
    bonusPowerValue = 0f;
    appliedPowerValue = basePowerValue;

    if (meleeStatSystem == null) return;
    if (!meleeStatSystem.IsPowerBonusReady()) return;

    // ② 보정된 위력값
    bonusPowerValue = meleeStatSystem.GetCalculatedPowerBonus();

    // ③ 실질 적용된 위력값
    appliedPowerValue = Mathf.Max(0.0001f, basePowerValue + bonusPowerValue);
}


private void RecalculateFinalStats()
{
    // ─────────────────────────────────────
    // ① 실질 적용된 위력값
    // ─────────────────────────────────────
    float power = Mathf.Max(0.0001f, appliedPowerValue);

    // ─────────────────────────────────────
    // ② 위력 → 템포 압축 강도 반영
    // 총 회전각은 고정, 시간/속도만 재분배
    // ─────────────────────────────────────
    float effectivePower =
        1f + (power - 1f) * powerCompressionFactor;

    effectivePower = Mathf.Max(0.0001f, effectivePower);

    // ─────────────────────────────────────
    // ③ 공격력 계산 (위력 직접 반영)
    // ─────────────────────────────────────
    finalAttackDamage =
        Mathf.RoundToInt(baseAttackDamage * power);

    // ─────────────────────────────────────
    // ④ 공전 시간 계산 (압축된 위력 사용)
    // ─────────────────────────────────────
    finalOrbitDuration =
        Mathf.Max(minOrbitDuration, baseOrbitDuration / effectivePower);

    // ─────────────────────────────────────
    // ⑤ 총 회전각 고정
    // ─────────────────────────────────────
    baseOrbitTotalDegrees =
        baseOrbitAngularSpeed * baseOrbitDuration;

    baseSpinTotalDegrees =
        baseWeaponSpinSpeed * baseOrbitDuration;

    // ─────────────────────────────────────
    // ⑥ 속도 역산 (각도 / 시간)
    // ─────────────────────────────────────
    finalOrbitAngularSpeed =
        baseOrbitTotalDegrees / finalOrbitDuration;

    finalWeaponSpinSpeed =
        baseSpinTotalDegrees / finalOrbitDuration;

    // ─────────────────────────────────────
    // ⑦ 페이로드 반영
    // ─────────────────────────────────────
    if (combatPayload != null)
        combatPayload.attackPower = finalAttackDamage;
}





public bool BeginAttack()                                              // 외부(AI 등) 시작 호출
{
    if (isOrbiting) return false;                                      // 이미 공격 중이면 불가 :contentReference[oaicite:3]{index=3}
    if (reAttackTimer > 0f) return false;                              // ✅ 쿨다운 중이면 불가
    BeginOrbit();                                                      // 시작
    return true;
}


    public void ForceEnd()     // 외부 강제 종료
    {
        if (isOrbiting)
            EndOrbit();
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // 공전 시작/진행/종료
    // ─────────────────────────────────────────────────────────────────────────────

private void BeginOrbit()                                               // 공전 시작
{
    if (pivot == null || orbitObject == null) return;
    if (reAttackTimer > 0f) return;

    ApplyPowerFromStatIfReady();   // 공격 직전에도 안전하게 동기화
    RecalculateFinalStats();

    // (이하 기존 코드 유지)
    hadSpritesDisabled = false;                                         
    if (spritesToDisableWhileOrbit != null)
    {
        foreach (var sr in spritesToDisableWhileOrbit)
        {
            if (sr == null) continue;
            if (!sr.enabled) continue;
            sr.enabled = false;
            hadSpritesDisabled = true;
        }
    }

    orbitObject.gameObject.SetActive(true);                             
    SetTrailsActive(true);                                              

    if (playerGun != null)
    {
        playerGun.StartFreeze(finalOrbitDuration);
    }

    orbitTimer = 0f;
    isOrbiting = true;

    if (lockOnBegin && movementRelay != null)
    {
        if (lockOnlyForThisAttack)
            movementRelay.SetLockedBy(this, true, compatibilityId);
        else
            movementRelay.SetLocked(true);
    }
}




private void UpdateOrbit() // 공전 진행: 로컬 Z 회전 누적
{
    float remaining = finalOrbitDuration - orbitTimer;            // ✅ 남은 공전 시간
    if (remaining <= 0f)                                          // 남은 시간이 없으면
    {
        EndOrbit();                                               // 종료
        return;
    }

    float step = Mathf.Min(Time.deltaTime, remaining);            // ✅ 남은 시간만큼만 처리(총각 고정 정확도↑)
    orbitTimer += step;                                           // 경과 시간 누적(최종 지속 기준)

    float deltaAngle = finalOrbitAngularSpeed * step;             // ✅ 최종 공전 각속도로 회전량 계산

    // 1) 공전 오브젝트: 로컬 Z 회전 누적
    if (orbitObject != null)
    {
        Vector3 currentEuler = orbitObject.localEulerAngles;      // 현재 오일러
        float newZ = currentEuler.z + deltaAngle;                 // Z 누적
        orbitObject.localRotation = Quaternion.Euler(currentEuler.x, currentEuler.y, newZ); // 적용
    }

    // 2) 무기 자체 스핀(최종 스핀 속도 사용)
    if (weaponObject != null && Mathf.Abs(finalWeaponSpinSpeed) > 0f)
    {
        float spinDelta = finalWeaponSpinSpeed * step;            // ✅ 최종 스핀 속도로 회전량 계산

        if (weaponSpinSpace == SpinSpace.Local)
        {
            Vector3 add = Vector3.zero;                           // 축별 추가값
            if ((weaponSpinAxes & SpinAxes.X) != 0) add.x = spinDelta;
            if ((weaponSpinAxes & SpinAxes.Y) != 0) add.y = spinDelta;
            if ((weaponSpinAxes & SpinAxes.Z) != 0) add.z = spinDelta;
            weaponObject.Rotate(add);                             // 로컬 회전
        }
        else
        {
            if ((weaponSpinAxes & SpinAxes.X) != 0) weaponObject.Rotate(Vector3.right,   spinDelta, Space.World);
            if ((weaponSpinAxes & SpinAxes.Y) != 0) weaponObject.Rotate(Vector3.up,      spinDelta, Space.World);
            if ((weaponSpinAxes & SpinAxes.Z) != 0) weaponObject.Rotate(Vector3.forward, spinDelta, Space.World);
        }
    }

    // 3) 종료 조건(최종 지속 기준)
    if (orbitTimer >= finalOrbitDuration)                         // ✅ 최종 공전시간 도달
        EndOrbit();                                               // 종료
}


    private void EndOrbit()                                             // 공전 종료
    {
            SetTrailsActive(false);                                             // ✅ 공전 끝나면 트레일 OFF(잔상 정리 포함)
        // 1) 껐던 스프라이트 복구
        if (reactivateSpritesOnEnd && hadSpritesDisabled && spritesToDisableWhileOrbit != null)
        {
            foreach (var sr in spritesToDisableWhileOrbit)
            {
                if (sr != null) sr.enabled = true;
            }
        }

        // 2) 무기 오브젝트 Z 절대 세팅
        if (weaponObject != null)
        {
            var we = weaponObject.localEulerAngles;
            we.z = weaponZOnDisable;
            weaponObject.localEulerAngles = we;
        }

        // 3) 공전 오브젝트 회전값(옵션)
        if (applyOrbitObjectZOnDisable && orbitObject != null)
        {
            orbitObject.localRotation = Quaternion.Euler(orbitObjectXOnDisable, 0f, orbitObjectZOnDisable);
        }

        // 4) 공전 오브젝트 비활성(옵션)
        if (deactivateOrbitObjectOnEnd && orbitObject != null)
        {
            orbitObject.gameObject.SetActive(false);
        }

        // 5) 상태 종료
        isOrbiting = false;

        // 6) 공격 종료 시 조작 해제
        if (unlockOnEnd && movementRelay != null)
        {
            if (lockOnlyForThisAttack)
                movementRelay.SetLockedBy(this, false, compatibilityId);  // ★ 호환 ID 포함 소유자 잠금 해제
            else
                movementRelay.SetLocked(false);                           // 기존 전역 잠금 해제
        }

        if (playerGun != null)
        {
            playerGun.CancelFreeze();   // ✅ gunRoot 제어권 PlayerGun으로 복귀
        }
           reAttackTimer = reAttackDelay;                                      // ✅ 공격 “후” 재공격 딜레이 시작
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // 유틸
    // ─────────────────────────────────────────────────────────────────────────────

    private Vector3 GetMouseWorldOnPlane(float planeZ)      // 마우스를 Z=planeZ 평면으로 투영
    {
        if (cam == null) cam = Camera.main;

        Vector3 sp = Input.mousePosition;
        Vector3 wp = cam.ScreenToWorldPoint(new Vector3(sp.x, sp.y, Mathf.Abs(cam.transform.position.z - planeZ)));
        wp.z = planeZ;
        return wp;
    }

    private static Vector2 AngleToXY(float degrees)         // 각도(도)→단위 원 벡터
    {
        float rad = degrees * Mathf.Deg2Rad;
        return new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));
    }

    private static float Normalize360(float deg)            // 각도 정규화[0,360)
    {
        deg %= 360f;
        if (deg < 0f) deg += 360f;
        return deg;
    }

private void OnDisable()                                                // 비활성화 시 안전 해제
{
    SetTrailsActive(false);                                             // ✅ 혹시 켜져 있으면 끄기

    if (movementRelay != null)                                          // 기존 잠금 해제 유지 :contentReference[oaicite:7]{index=7}
    {
        if (lockOnlyForThisAttack)
            movementRelay.SetLockedBy(this, false, compatibilityId);
        else
            movementRelay.SetLocked(false);
    }
}


#if UNITY_EDITOR
    private void OnDrawGizmosSelected()                     // 궤도 가시화(에디터)
    {
        if (pivot == null || orbitObject == null) return;

        float r = 0f;
        if (radiusMode == RadiusMode.FixedRadius)
        {
            r = fixedRadius;
        }
        else
        {
            Vector2 off = (Vector2)(orbitObject.position - pivot.position);
            r = off.magnitude;
        }

        if (r > 0f)
        {
            Gizmos.color = Color.cyan;
            const int seg = 64;
            Vector3 prev = pivot.position + (Vector3)AngleToXY(0f) * r;
            prev.z = orbitObject.position.z;

            for (int i = 1; i <= seg; i++)
            {
                float a = (i / (float)seg) * 360f;
                Vector3 curr = pivot.position + (Vector3)AngleToXY(a) * r;
                curr.z = prev.z;
                Gizmos.DrawLine(prev, curr);
                prev = curr;
            }
        }
    }

    #endif

    private void SetTrailsActive(bool active)                               // ✅ 트레일 일괄 ON/OFF
{
    if (trailsToToggle == null || trailsToToggle.Count <= 0) return;    // 리스트 비었으면 종료

    foreach (var tr in trailsToToggle)                                  // 트레일 전부 처리
    {
        if (tr == null) continue;                                       // null 방지

        if (active)
        {
            tr.enabled = true;                                          // 컴포넌트 활성
            tr.emitting = true;                                         // 방출 시작
        }
        else
        {
            tr.emitting = false;                                        // 방출 중단
            if (clearTrailsWhenOff) tr.Clear();                         // 잔상 제거
            tr.enabled = false;                                         // 컴포넌트 비활성
        }
    }
}

private void TickReAttackCooldown()                                     // ✅ 재공격 쿨다운 감소
{
    if (reAttackTimer <= 0f) return;                                    // 이미 0이면 종료
    reAttackTimer -= Time.deltaTime;                                    // 시간 감소
    if (reAttackTimer < 0f) reAttackTimer = 0f;                         // 음수 방지
}




}
