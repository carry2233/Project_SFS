using System;
using System.Reflection;
using UnityEngine;

[DisallowMultipleComponent]
[AddComponentMenu("Character/시야방향")]
public class 시야방향 : MonoBehaviour
{
    [Header("연동")]
    [SerializeField] private PlayerMovement pm;               // ✅ PlayerMovement 참조

    [Header("회전 타겟/방식")]
    [SerializeField] private Transform rotateTarget;          // ✅ 회전시킬 대상
    [SerializeField] private bool useLocalEuler = true;       // ✅ 로컬 회전 적용

    [Header("회전 속성")]
    [SerializeField] private float turnSpeed = 360f;          // ✅ 회전 속도(°/s)
    [SerializeField] private float deadZone = 0.0001f;        // ✅ 미세 입력 무시
    [SerializeField] private bool freezeWhenIdle = true;      // ✅ 정지 시 회전 멈춤
    [SerializeField] private bool keepLastDirectionWhenIdle = true; // ✅ 정지 시 마지막 유지

    [Header("시작 시 정렬")]
    [SerializeField] private bool snapToInitialFacingOnStart = true; // ✅ 시작 스냅
    [SerializeField] private bool logFacingDetection = false;        // ✅ 로그
    [SerializeField] private Camera cam;                 // ✅ 조준 카메라
    public bool IsAiming { get; private set; }           // ✅ 조준 상태
    public float CurrentYawDeg { get; private set; }     // ✅ 현재 Yaw(도)
    public Transform RotateTarget => rotateTarget;       // ✅ 회전 대상

    private float lastTargetYawDeg = 0f;                 // ✅ 마지막 목표 Yaw
    private bool  hasAnyAim = false;                     // ✅ 조준 기록

    private void Reset()
    {
        if (pm == null) pm = GetComponent<PlayerMovement>();
        if (rotateTarget == null) rotateTarget = transform;
    }

    private void Awake()
    {
        if (pm == null) pm = GetComponent<PlayerMovement>();
        if (rotateTarget == null) rotateTarget = transform;
        if (cam == null) cam = Camera.main;
    }

    private void Start()
    {
        if (!snapToInitialFacingOnStart || pm == null || rotateTarget == null) return;

        object facingValue = TryGetPublicFacingProperty(pm, out string usedPropName);
        if (facingValue == null) facingValue = TryGetPrivateInitialFacingField(pm);

        if (facingValue != null)
        {
            float yaw = DirectionToYaw(facingValue);
            lastTargetYawDeg = yaw;
            hasAnyAim = true;

            if (useLocalEuler)
            {
                Vector3 e = rotateTarget.localEulerAngles;
                e.y = yaw;
                rotateTarget.localEulerAngles = e;
            }
            else
            {
                Vector3 e = rotateTarget.eulerAngles;
                e.y = yaw;
                rotateTarget.eulerAngles = e;
            }

            if (logFacingDetection)
                Debug.Log($"[시야방향] 초기 방향 스냅 (yaw={yaw})", this);
        }
        else
        {
            if (logFacingDetection)
                Debug.Log($"[시야방향] 초기 방향 정보 없음, 스냅 생략.", this);
        }
    }

    private void LateUpdate()
    {
        if (pm == null || rotateTarget == null) return;

        Vector3 moveDir = pm.CurrentMoveDir;
        bool isMoving = pm.IsMoving;

        if (moveDir.sqrMagnitude < deadZone * deadZone)
            isMoving = false;

        float targetYawDeg = lastTargetYawDeg;
        IsAiming = Input.GetMouseButton(1);

        if (IsAiming && cam != null)
        {
            Vector3 mp = Input.mousePosition;
            Vector3 mw = cam.ScreenToWorldPoint(new Vector3(mp.x, mp.y, Mathf.Abs(cam.transform.position.z - rotateTarget.position.z)));
            mw.z = rotateTarget.position.z;

            Vector3 v = mw - rotateTarget.position;
            v.z = 0f;
            if (v.sqrMagnitude > deadZone * deadZone)
            {
                Vector3 mapped = new Vector3(v.x, 0f, v.y);
                targetYawDeg = Mathf.Atan2(mapped.x, mapped.z) * Mathf.Rad2Deg;
                lastTargetYawDeg = targetYawDeg;
                hasAnyAim = true;
            }
        }
        else
        {
            if (isMoving)
            {
                Vector3 mapped = new Vector3(moveDir.x, 0f, moveDir.y);
                if (mapped.sqrMagnitude > 0f)
                {
                    targetYawDeg = Mathf.Atan2(mapped.x, mapped.z) * Mathf.Rad2Deg;
                    lastTargetYawDeg = targetYawDeg;
                    hasAnyAim = true;
                }
            }
            else
            {
                if (!keepLastDirectionWhenIdle && hasAnyAim == false)
                    targetYawDeg = 0f;
                if (freezeWhenIdle) return;
            }
        }

        if (useLocalEuler)
        {
            Vector3 e = rotateTarget.localEulerAngles;
            float currYaw = e.y;
            float nextYaw = Mathf.MoveTowardsAngle(currYaw, targetYawDeg, turnSpeed * Time.deltaTime);
            e.y = nextYaw;
            rotateTarget.localEulerAngles = e;
        }
        else
        {
            Vector3 e = rotateTarget.eulerAngles;
            float currYaw = e.y;
            float nextYaw = Mathf.MoveTowardsAngle(currYaw, targetYawDeg, turnSpeed * Time.deltaTime);
            e.y = nextYaw;
            rotateTarget.eulerAngles = e;
        }

        CurrentYawDeg = useLocalEuler ? rotateTarget.localEulerAngles.y : rotateTarget.eulerAngles.y;
    }

    // ---- 리플렉션/도우미(기존 유지) ----
    private System.Object TryGetPublicFacingProperty(PlayerMovement target, out string usedPropName)
    {
        usedPropName = null;
        var pInit = target.GetType().GetProperty("InitialFacing", BindingFlags.Instance | BindingFlags.Public);
        if (pInit != null && pInit.CanRead) { usedPropName = "InitialFacing"; return pInit.GetValue(target); }
        var pCurr = target.GetType().GetProperty("CurrentFacing", BindingFlags.Instance | BindingFlags.Public);
        if (pCurr != null && pCurr.CanRead) { usedPropName = "CurrentFacing"; return pCurr.GetValue(target); }
        return null;
    }

    private System.Object TryGetPrivateInitialFacingField(PlayerMovement target)
    {
        FieldInfo f = target.GetType().GetField("initialFacing", BindingFlags.Instance | BindingFlags.NonPublic);
        if (f != null) return f.GetValue(target);
        return null;
    }

    private float DirectionToYaw(System.Object facingValue)
    {
        string name = facingValue.ToString();
        switch (name)
        {
            case "Up": return 0f;
            case "Down": return 180f;
            case "Left": return -90f;
            case "Right": return 90f;
            default: return 0f;
        }
    }
}
