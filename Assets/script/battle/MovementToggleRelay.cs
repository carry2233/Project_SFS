using System.Collections.Generic;                       // HashSet 소유자 관리
using UnityEngine;

[AddComponentMenu("Gameplay/Movement Toggle Relay")]
public class MovementToggleRelay : MonoBehaviour
{
    [Header("제어 대상")]
    [SerializeField] private PlayerMovement target;                 // 제어할 PlayerMovement

    [Header("잠금 옵션(공격 중에 비활성화할 항목)")]
    [SerializeField] private bool lockMovement = true;              // 이동 로직 비활성화
    [SerializeField] private bool lockFacingByKeys = true;          // 키 기반 스프라이트 전환 비활성화
    [SerializeField] private bool lockAimBasedFacing = true;        // 에임 기반 스프라이트 전환 비활성화

    [Header("부가 옵션")]
    [SerializeField] private bool zeroVelocityOnLock = true;        // 잠금 시 즉시 정지
    [SerializeField, Tooltip("잠금 중 '유효 방향 변경 이벤트'를 억제(연출 고정용)")]
    private bool suppressFacingEventsOnLock = false;                // 잠금 중 방향이벤트 억제

    // ─────────────────────────────────────────────────────────────────────────────
    // (★ 추가) 소유자 기반 잠금
    // ─────────────────────────────────────────────────────────────────────────────
    [Header("소유자 기반 잠금(옵션)")]
    [SerializeField] private bool supportOwnerScopedLock = true;    // 소유자 기반 잠금 기능

    // ─────────────────────────────────────────────────────────────────────────────
    // (★ 추가) 호환 정수
    // ─────────────────────────────────────────────────────────────────────────────
    [Header("호환 정수 (같은 값의 요청만 적용)")]
    [SerializeField] private int compatibilityId = 0;               // 이 릴레이가 수락할 호환 정수(ID)

    // 내부 상태 캐시
    private bool cachedAllowMovement;                               // 원래 allowMovement
    private bool cachedAllowFacingByKeys;                           // 원래 allowFacingByKeys
    private bool cachedAllowAimBasedFacing;                         // 원래 allowAimBasedFacing
    private bool cachedSuppressFacingEvents;                        // 원래 suppressFacingEvents
    private bool hasCache = false;                                  // 캐시 유효 여부

    // 잠금 상태(전역/소유자)
    private bool globalLocked = false;                              // 전역 잠금
    private readonly HashSet<object> ownerLocks = new();            // 소유자 잠금(현재 호환 ID용)
    private bool Applied => globalLocked || ownerLocks.Count > 0;   // 실제 잠금 적용 여부

    // ─────────────────────────────────────────────────────────────────────────────
    // 퍼블릭 API (기존 전역/신규 오버로드)
    // ─────────────────────────────────────────────────────────────────────────────

    public void SetLocked(bool locked)                              // 전역 잠금 토글(기존)
    {
        if (target == null) return;
        if (globalLocked == locked) return;

        globalLocked = locked;
        UpdateApplyState();
    }

    public void SetLockedBy(object owner, bool locked)              // 소유자 잠금(구버전, 호환)
    {
        // 호환 정수 판별이 불가능하므로, 이 오버로드는 "현재 릴레이의 호환 ID에 해당"한다고 가정
        // → 새 구조로 옮기면 다음 오버로드 사용 권장
        SetLockedBy(owner, locked, compatibilityId);
    }

    public void SetLockedBy(object owner, bool locked, int requestCompatibilityId) // ★ 소유자 잠금(호환 ID 포함)
    {
        if (!supportOwnerScopedLock) { SetLocked(locked); return; } // 비활성화 시 전역 잠금으로 대체
        if (target == null || owner == null) return;

        // 호환 정수 불일치 → 요청 무시
        if (requestCompatibilityId != compatibilityId) return;

        bool changed = false;
        if (locked)
            changed = ownerLocks.Add(owner);                        // 소유자 추가
        else
            changed = ownerLocks.Remove(owner);                     // 소유자 해제

        if (changed) UpdateApplyState();
    }

    public bool IsLockedBy(object owner)                            // 특정 소유자 잠금 여부
    {
        if (owner == null) return false;
        return ownerLocks.Contains(owner);
    }

    public void ReleaseAllLocksOf(object owner)                     // 해당 소유자의 모든 잠금 해제
    {
        if (owner == null) return;
        if (ownerLocks.Remove(owner)) UpdateApplyState();
    }

    public void SetTarget(PlayerMovement pm)                        // 제어 대상 교체
    {
        target = pm;
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // 내부 구현
    // ─────────────────────────────────────────────────────────────────────────────

    private void UpdateApplyState()                                  // 실제 적용/복원 전환
    {
        if (Applied)
        {
            ApplyLockIfNeeded();                                     // 잠금 적용
        }
        else
        {
            RestoreIfNeeded();                                       // 잠금 복원
        }
    }

    private void ApplyLockIfNeeded()                                 // 잠금 적용
    {
        if (target == null) return;
        if (!hasCache)                                               // 최초 적용 시에만 캐시
        {
            cachedAllowMovement = target.AllowMovement;
            cachedAllowFacingByKeys = target.AllowFacingByKeys;
            cachedAllowAimBasedFacing = target.AllowAimBasedFacing;
            cachedSuppressFacingEvents = target.SuppressFacingEvents;
            hasCache = true;
        }

        if (lockMovement)        target.AllowMovement = false;       // 이동 차단
        if (lockFacingByKeys)    target.AllowFacingByKeys = false;   // 키기반 회전 차단
        if (lockAimBasedFacing)  target.AllowAimBasedFacing = false; // 에임기반 회전 차단

        if (suppressFacingEventsOnLock) target.SuppressFacingEvents = true; // 방향 이벤트 억제
        if (zeroVelocityOnLock)  target.ForceZeroVelocity2D();       // 즉시 정지
    }

private void RestoreIfNeeded()                                   // 잠금 복원
{
    if (target == null) return;
    if (hasCache)
    {
        if (lockMovement)        target.AllowMovement = cachedAllowMovement;          // 이동 허용 복원
        if (lockFacingByKeys)    target.AllowFacingByKeys = cachedAllowFacingByKeys;  // 키기반 허용 복원
        if (lockAimBasedFacing)  target.AllowAimBasedFacing = cachedAllowAimBasedFacing; // 조준기반 허용 복원

        target.SuppressFacingEvents = cachedSuppressFacingEvents;                      // 방향 이벤트 억제 복원
    }

    hasCache = false;                                                                  // 캐시 해제

    // ★★★ 핵심 추가: 해제 프레임에 즉시 동기화 신호 1회 발행(이벤트 강제) ★★★
    target.ForcePublishEffectiveFacing();                                              // 🔔 강제 1회 이벤트 발행
}


    private void OnDisable()                                         // 비활성화 시 안전 해제
    {
        globalLocked = false;
        ownerLocks.Clear();
        UpdateApplyState();
    }

    
}
