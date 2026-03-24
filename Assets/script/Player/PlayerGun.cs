using System;
using System.Reflection;
using UnityEngine;

[AddComponentMenu("Character/PlayerGun")]
public class PlayerGun : MonoBehaviour
{
    [Header("참조")]
    [SerializeField] private PlayerMovement pm;                 // PlayerMovement 참조
    [SerializeField] private 시야방향 aim;                     // 시야방향 참조(조준 상태/마우스 yaw)
    [SerializeField] private Transform gunRoot;                 // A 오브젝트(총)의 Transform
    [SerializeField] private Transform gun;                     // 회전 보정 대상(추가)  // 회전 보정 적용 대상
    private SpriteRenderer gunSR;                               // A 오브젝트(총)의 SR
    private int cachedFollowOrder;                              // 따라가기용 기준 order 캐시
    [SerializeField] private SpriteRenderer followSourceSR;     // 레이어 순서 따라갈 기준 SR(플레이어 본체 등)

    [Header("기본 설정")]
    [SerializeField] private bool useLocal = true;              // 로컬 기준 적용 여부
    [SerializeField] private bool preferAimPose = true;         // 조준 중에는 조준 포즈를 우선 적용
    [SerializeField] private float wedgeHalfDeg = 45f;          // 방향 매핑용 부채꼴 반각(Up/Right/Down/Left)

    [Header("조준 추적 속도(시야방향.turnSpeed 사용)")]
    [SerializeField] private float aimTurnSpeedFallback = 360f; // 리플렉션 실패 시 사용할 대체 속도
    [SerializeField] private bool logWhenReflectFail = false;   // 리플렉션 실패 로그 출력 여부

    [Header("조준 포즈(우클릭 유지 시 적용, preferAimPose=true일 때 우선)")]
    [SerializeField] private Vector3 aimLocalPosition = new(0.3f, 0.05f, 0f); // 조준 시 위치 오프셋(로컬)
    [SerializeField] private Vector3 aimLocalEuler = new(0f, 0f, 0f);          // 조준 시 회전(로컬 오일러)

    // ─────────────────────────────
    // ★ NEW: 포지션+회전 고정 옵션
    // ─────────────────────────────
    [Header("Freeze 옵션 (우클릭 유지 + 좌클릭 순간)")]
    [SerializeField] private bool enablePositionRotationFreeze = true; // ★ NEW: 트리거 기능 ON/OFF
    [SerializeField, Min(0f)] private float freezeDuration = 0.2f;     // ★ NEW: 고정 유지 시간(초)

    // 내부 상태(Freeze)
    private bool isFrozen;                               // ★ NEW: 현재 고정 중인가
    private float freezeEndTime;                         // ★ NEW: 고정 해제 시각(Time.time 기준)
    private Vector3 frozenLocalPos;                      // ★ NEW: 저장한 로컬 포지션
    private Vector3 frozenLocalEuler;                    // ★ NEW: 저장한 로컬 오일러
    private Vector3 frozenWorldPos;                      // ★ NEW: 저장한 월드 포지션
    private Vector3 frozenWorldEuler;                    // ★ NEW: 저장한 월드 오일러

    [Serializable]
    public struct PosePreset
    {
        public Vector3 localPosition;     // 방향별 위치 오프셋(로컬)
        public Vector3 localEuler;        // 방향별 회전(로컬 오일러)

        // --- 정렬(레이어 순서) 옵션 ---
        public bool useOrderOffset;       // 따라가기 + 보정치 적용 여부
        public int  orderOffset;          // 보정치(예: -1이면 따라간 값에서 -1)

        public bool overrideOrder;        // (기존) 절대값 강제 여부
        public int  orderInLayer;         // (기존) 절대값
    }

    [Header("방향별 프리셋 - 비조준(Non-Aim)")]
    [SerializeField] private PosePreset nonAimUpPreset;         // 비조준: 위(Up)
    [SerializeField] private PosePreset nonAimRightPreset;      // 비조준: 오른쪽(Right)
    [SerializeField] private PosePreset nonAimDownPreset;       // 비조준: 아래(Down)
    [SerializeField] private PosePreset nonAimLeftPreset;       // 비조준: 왼쪽(Left)

    [Header("방향별 프리셋 - 조준(Aim)")]
    [SerializeField] private PosePreset aimUpPreset;            // 조준: 위(Up)
    [SerializeField] private PosePreset aimRightPreset;         // 조준: 오른쪽(Right)
    [SerializeField] private PosePreset aimDownPreset;          // 조준: 아래(Down)
    [SerializeField] private PosePreset aimLeftPreset;          // 조준: 왼쪽(Left)

    [Header("입력 제어")]
    [SerializeField] public bool inputEnabled = true; // 마우스 입력 사용 여부(플레이어용 ON, AI용 OFF)


    private void Reset()
    {
        if (pm == null) pm = GetComponentInParent<PlayerMovement>();
        if (aim == null) aim = GetComponentInParent<시야방향>();
        if (gunRoot == null) gunRoot = transform;
        if (gun == null) gun = gunRoot;                         // 회전 보정 대상 기본값  // 기본 대상 설정
        if (followSourceSR == null)
        {
            followSourceSR = GetComponentInParent<SpriteRenderer>();
        }
        gunSR = FindTargetSRFromChildren(); // 🔸자식에서 검색하여 적용 대상 SR 캐시
    }

    private void Awake()
    {
        if (pm == null) pm = GetComponentInParent<PlayerMovement>();
        if (aim == null) aim = GetComponentInParent<시야방향>();
        if (gunRoot == null) gunRoot = transform;
        if (gun == null) gun = gunRoot;                         // 회전 보정 대상 보정  // 기본 대상 보정
        if (followSourceSR == null) followSourceSR = GetComponentInParent<SpriteRenderer>();
        if (gunSR == null) gunSR = FindTargetSRFromChildren(); // 🔸자식에서 최종 보정
    }

    private void LateUpdate()                                   // 주 프레임 갱신(스프라이트 정렬 후 반영 권장)
    {
        if (pm == null || gunRoot == null) return;

        // ★ NEW: 트리거 갱신(우클릭 유지 + 좌클릭 순간에 고정 시작)
        UpdateFreezeTrigger();  // 고정 시작 조건 확인/시작

        // ★ NEW: 고정 상태 갱신(시간 만료 시 자동 해제)
        UpdateFreezeState();    // 시간 경과로 해제되는지 검사

        // 1) 기준 SR의 레이어 순서 따라가기(기본값)
        int followOrder = TryGetFollowOrder();
        cachedFollowOrder = followOrder;

        // ★ NEW: 고정 중이면 저장된 포즈를 ‘강제 적용’하고, 나머지 계산을 스킵
        if (isFrozen)
        {
            ApplyFrozenPose();                 // 저장된 위치/회전 그대로 유지
            return;                            // 아래 기존 포즈/회전 계산은 건너뜀
        }

        // 2) 현재 모드 판정
        bool isAiming = (aim != null && aim.IsAiming);

        // 3) 방향/포즈 결정
        if (isAiming)
        {
            float yaw = GetCurrentYawDeg();
            PlayerMovement.Direction dir = DirectionFromYaw(yaw);
            PosePreset aimP = GetAimPreset(dir);

            if (preferAimPose)
                ApplyPositionOnly(aimLocalPosition); // 조준 전용 위치
            else
                ApplyPresetPositionOnly(in aimP);    // 프리셋 위치만

            // ✅ 조준 시 회전: 최종 Y = 마우스 yaw + 프리셋.localEuler.y, X/Z는 프리셋 값 고정
            ApplyAimRotationWithPreset(yaw, in aimP, GetAimTurnSpeed());

            ApplySortingOrder(in aimP, followOrder);
        }
        else
        {
            PlayerMovement.Direction dir = GetFacingFromPM();
            PosePreset nonAimP = GetNonAimPreset(dir);

            ApplyPresetPositionOnly(in nonAimP);      // gunRoot에는 위치만 적용

            // 🔹 조준 해제 시 gunRoot의 Y 회전값 0으로 초기화
            if (useLocal)
            {
                Vector3 rootE = gunRoot.localEulerAngles;
                rootE.y = 0f;
                gunRoot.localEulerAngles = rootE;
            }
            else
            {
                Vector3 rootE = gunRoot.eulerAngles;
                rootE.y = 0f;
                gunRoot.eulerAngles = rootE;
            }

            ApplyGunPoseRotation(in nonAimP);         // 회전(XYZ)은 gun(자식)에서 전담
            ApplySortingOrder(in nonAimP, followOrder);
        }
    }

    // PlayerGun의 "자식 오브젝트"에서 SpriteRenderer를 찾아 적용 대상으로 사용
    private SpriteRenderer FindTargetSRFromChildren() // 자식에서 적용 대상 SR 검색
    {
        SpriteRenderer sr = transform.GetComponentInChildren<SpriteRenderer>();
        if (sr == null)
        {
            Debug.LogWarning("[PlayerGun] 자식에서 SpriteRenderer를 찾지 못했습니다. 정렬 순서 적용 대상이 없습니다.", this);
        }
        return sr;
    }

    // ─────────────────────────────────────────────────────────────
    // ★ NEW: Freeze 트리거/상태/적용
    // ─────────────────────────────────────────────────────────────
    private void UpdateFreezeTrigger() // ★ NEW: 우클릭 유지 + 좌클릭 Down → 고정 시작
    {
        if (!inputEnabled) return;
        if (!enablePositionRotationFreeze) return;                  // 옵션 꺼져 있으면 무시
        if (!Input.GetMouseButton(1)) return;                       // 우클릭 유지 중일 때만
        if (!Input.GetMouseButtonDown(0)) return;                   // 좌클릭 '누른 순간'만 트리거

        StartFreeze(freezeDuration);                                // 고정 시작
    }

    private void UpdateFreezeState() // ★ NEW: 고정 유지/해제 타이밍 갱신
    {
        if (!isFrozen) return;
        if (Time.time >= freezeEndTime || gunRoot == null)          // 시간 만료 또는 대상 소실
        {
            isFrozen = false;                                       // 자동 해제
        }
    }

    public void StartFreeze(float duration) // ★ NEW: 외부 호출용 - 강제 고정 시작
    {
        if (gunRoot == null) return;

        // 현재 포즈 저장
        if (useLocal)
        {
            frozenLocalPos   = gunRoot.localPosition;               // 현재 로컬 포지션 저장
            frozenLocalEuler = gunRoot.localEulerAngles;            // 현재 로컬 오일러 저장
        }
        else
        {
            frozenWorldPos   = gunRoot.position;                    // 현재 월드 포지션 저장
            frozenWorldEuler = gunRoot.eulerAngles;                 // 현재 월드 오일러 저장
        }

        isFrozen = true;                                            // 고정 상태 시작
        freezeEndTime = Time.time + Mathf.Max(0f, duration);        // 해제 예정 시각 기록
    }

    public void CancelFreeze() // ★ NEW: 외부 호출용 - 즉시 해제
    {
        isFrozen = false;
    }

    private void ApplyFrozenPose() // ★ NEW: 저장된 포즈를 그대로 적용(포지션+회전 모두)
    {
        if (gunRoot == null) return;

        if (useLocal)
        {
            gunRoot.localPosition   = frozenLocalPos;               // 저장된 로컬 포지션 적용
            gunRoot.localEulerAngles = frozenLocalEuler;            // 저장된 로컬 오일러 적용
        }
        else
        {
            gunRoot.position        = frozenWorldPos;               // 저장된 월드 포지션 적용
            gunRoot.eulerAngles     = frozenWorldEuler;             // 저장된 월드 오일러 적용
        }
    }

    // ─────────────────────────────────────────────────────────────
    // 방향/프리셋 적용
    // ─────────────────────────────────────────────────────────────
    private PlayerMovement.Direction GetFacingFromPM()       // 현재 바라보는 방향 읽기(옵션 A)
    {
        try
        {
            return pm.CurrentFacingForExternal;              // PM에 추가한 퍼블릭 getter 사용
        }
        catch (Exception)
        {
            // 만약 PM에 getter가 없다면, 이동 벡터 기반으로 추정 (폴백)
            Vector3 md = pm != null ? pm.CurrentMoveDir : Vector3.zero;
            if (md.sqrMagnitude <= 0f) return PlayerMovement.Direction.Down; // 기본값
            float yaw = Mathf.Atan2(md.x, md.y) * Mathf.Rad2Deg; // XY→Yaw(Up=0)
            return DirectionFromYaw(yaw);
        }
    }

    private PlayerMovement.Direction DirectionFromYaw(float yawDeg) // yaw(도) → 4방향 버킷
    {
        yawDeg = Normalize180(yawDeg);
        // Up(0°), Right(90°), Down(±180°), Left(-90°) 기준, 반구간(-half, +half]
        if (InWedge(yawDeg,   0f, wedgeHalfDeg))  return PlayerMovement.Direction.Up;
        if (InWedge(yawDeg,  90f, wedgeHalfDeg))  return PlayerMovement.Direction.Right;
        if (InWedge(yawDeg, 180f, wedgeHalfDeg) || InWedge(yawDeg, -180f, wedgeHalfDeg))
            return PlayerMovement.Direction.Down;
        return PlayerMovement.Direction.Left;
    }

    private static float Normalize180(float deg)             // 각도 정규화([-180, 180))
    {
        deg %= 360f;
        if (deg >= 180f) deg -= 360f;
        if (deg < -180f) deg += 360f;
        return deg;
    }

    private static bool InWedge(float deg, float center, float half) // 반구간(-half, +half]
    {
        float d = Normalize180(deg - center);
        return (d > -half && d <= half);
    }

    private void ApplyPresetPose(in PosePreset p) // 선택된 프리셋 포즈 적용(위치+회전)
    {
        ApplyLocalPose(p.localPosition, p.localEuler);
    }

    private void ApplyLocalPose(Vector3 pos, Vector3 euler)  // 위치/회전 적용(로컬/월드 선택)
    {
        if (useLocal)
        {
            gunRoot.localPosition = pos;
            gunRoot.localEulerAngles = euler;
        }
        else
        {
            gunRoot.position = transform.TransformPoint(pos);         // 간단 변환(월드)
            Vector3 worldEuler = (transform.rotation * Quaternion.Euler(euler)).eulerAngles;
            gunRoot.eulerAngles = worldEuler;
        }
    }

    private PosePreset GetNonAimPreset(PlayerMovement.Direction dir) // 비조준 프리셋 선택
    {
        return dir switch
        {
            PlayerMovement.Direction.Up    => nonAimUpPreset,
            PlayerMovement.Direction.Right => nonAimRightPreset,
            PlayerMovement.Direction.Down  => nonAimDownPreset,
            _                              => nonAimLeftPreset,
        };
    }

    private PosePreset GetAimPreset(PlayerMovement.Direction dir)     // 조준 프리셋 선택
    {
        return dir switch
        {
            PlayerMovement.Direction.Up    => aimUpPreset,
            PlayerMovement.Direction.Right => aimRightPreset,
            PlayerMovement.Direction.Down  => aimDownPreset,
            _                              => aimLeftPreset,
        };
    }

    // ─────────────────────────────────────────────────────────────
    // 조준 회전 처리
    // ─────────────────────────────────────────────────────────────
    private float GetCurrentYawDeg()                         // 현재 yaw(도) 읽기(시야방향)
    {
        if (aim != null) return aim.CurrentYawDeg;          // 시야방향이 이미 갱신해줌
        // aim이 없으면 PM의 이동벡터 기준으로 추정
        Vector3 md = (pm != null) ? pm.CurrentMoveDir : Vector3.up;
        if (md.sqrMagnitude <= 0f) md = Vector3.up;
        return Mathf.Atan2(md.x, md.y) * Mathf.Rad2Deg;     // XY→Yaw(Up=0)
    }

    private float GetAimTurnSpeed()                         // 시야방향.turnSpeed(리플렉션) or 폴백
    {
        if (aim == null) return aimTurnSpeedFallback;

        // 1) 퍼블릭 프로퍼티 "TurnSpeed" 우선
        PropertyInfo p = aim.GetType().GetProperty("TurnSpeed", BindingFlags.Instance | BindingFlags.Public);
        if (p != null && p.CanRead)
        {
            object v = p.GetValue(aim);
            if (v is float fProp) return Mathf.Max(0f, fProp);
        }

        // 2) private 필드 "turnSpeed" 폴백
        FieldInfo f = aim.GetType().GetField("turnSpeed", BindingFlags.Instance | BindingFlags.NonPublic);
        if (f != null)
        {
            object v = f.GetValue(aim);
            if (v is float fField) return Mathf.Max(0f, fField);
        }

        if (logWhenReflectFail)
            Debug.LogWarning("[PlayerGun] 시야방향.turnSpeed 접근 실패 → aimTurnSpeedFallback 사용", this);

        return Mathf.Max(0f, aimTurnSpeedFallback);
    }

    // ✅ 조준 시 회전(축 완전 분리)
    // - gunRoot: Y(yaw) = 마우스 yaw "즉시 적용" (보간 없음)        // 조준 방향만 담당
    // - gun:     X/Y/Z = 프리셋 localEuler 전부 "즉시 적용"         // 모든 포즈 보정 전담
    private void ApplyAimRotationWithPreset(float mouseYaw, in PosePreset preset, float _ignored) // speed는 무시
    {
        float targetYaw = Normalize180(mouseYaw); // 부모는 순수 마우스 yaw만

        // 1) 부모(gunRoot): Y만 스냅 적용
        if (useLocal)
        {
            Vector3 rootE = gunRoot.localEulerAngles; // 로컬 회전
            rootE.y = targetYaw;                      // Y만 적용
            gunRoot.localEulerAngles = rootE;
        }
        else
        {
            Vector3 rootE = gunRoot.eulerAngles;      // 월드 회전
            rootE.y = targetYaw;
            gunRoot.eulerAngles = rootE;
        }

        // 2) 자식(gun): 프리셋 XYZ 모두 적용
        if (gun != null)
        {
            if (useLocal)
            {
                Vector3 childE = gun.localEulerAngles; // 로컬 회전
                childE.x = preset.localEuler.x;        // X
                childE.y = preset.localEuler.y;        // Y (프리셋 yaw 오프셋도 자식에서 처리)
                childE.z = preset.localEuler.z;        // Z
                gun.localEulerAngles = childE;
            }
            else
            {
                Vector3 childE = gun.eulerAngles;      // 월드 회전
                childE.x = preset.localEuler.x;
                childE.y = preset.localEuler.y;
                childE.z = preset.localEuler.z;
                gun.eulerAngles = childE;
            }
        }
    }

    // ✅ 프리셋 회전(XYZ)을 gun에만 적용 (gunRoot 회전은 건드리지 않음)
    private void ApplyGunPoseRotation(in PosePreset preset) // 자식 회전 적용
    {
        if (gun == null) return;

        if (useLocal)
        {
            Vector3 e = gun.localEulerAngles; // 자식 로컬 회전
            e.x = preset.localEuler.x;        // X
            e.y = preset.localEuler.y;        // Y
            e.z = preset.localEuler.z;        // Z
            gun.localEulerAngles = e;
        }
        else
        {
            Vector3 e = gun.eulerAngles;      // 자식 월드 회전
            e.x = preset.localEuler.x;
            e.y = preset.localEuler.y;
            e.z = preset.localEuler.z;
            gun.eulerAngles = e;
        }
    }

    // ─────────────────────────────────────────────────────────────
    // 레이어 순서 적용
    // ─────────────────────────────────────────────────────────────
    private int TryGetFollowOrder()                         // 기준 SR의 order 추출(없으면 0)
    {
        if (followSourceSR != null) return followSourceSR.sortingOrder;
        return 0;
    }

    private void ApplySortingOrder(in PosePreset p, int followOrder) // order 적용
    {
        if (gunSR == null) return;

        int finalOrder = followOrder;                 // 기본: 따라가기
        if (p.useOrderOffset)                         // 따라가기 + 보정치
            finalOrder = followOrder + p.orderOffset; // 예: 10 + (-1) = 9
        else if (p.overrideOrder)                     // 절대 고정
            finalOrder = p.orderInLayer;

        gunSR.sortingOrder = finalOrder;
    }

    private void ApplyPositionOnly(Vector3 pos) // 위치만 적용(로컬/월드 선택)
    {
        if (useLocal)
        {
            gunRoot.localPosition = pos;               // 위치만 로컬로 적용
            // 회전은 건드리지 않음
        }
        else
        {
            gunRoot.position = transform.TransformPoint(pos); // 월드 위치 변환 적용
            // 회전은 건드리지 않음
        }
    }

    private void ApplyPresetPositionOnly(in PosePreset p) // 선택된 프리셋의 위치만 적용
    {
        ApplyPositionOnly(p.localPosition);
    }

    // ─────────────────────────────────────────────────────────────
    // 에디터 편의(기즈모)
    // ─────────────────────────────────────────────────────────────
#if UNITY_EDITOR
    private void OnDrawGizmosSelected()                     // 현재 설정 포즈 가시화
    {
        if (gunRoot == null) return;

        // 현재 디렉션 미확정 시 '비조준 Up' 기준 미리보기
        PosePreset p = nonAimUpPreset; // ← upPreset → nonAimUpPreset 로 교체
        Vector3 previewPos = useLocal ? p.localPosition : transform.TransformPoint(p.localPosition);

        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(useLocal ? (gunRoot.parent ? gunRoot.parent.TransformPoint(previewPos) : transform.TransformPoint(previewPos))
                                       : previewPos, 0.05f);
    }
#endif
}
