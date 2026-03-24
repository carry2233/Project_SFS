using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class TrailOrientationLock : MonoBehaviour
{
    [System.Serializable]
    public class TrailEntry
    {
        [Header("필수")]
        public TrailRenderer trail; // 대상 TrailRenderer
        public Transform targetTransform; // 회전을 적용할 대상 Transform(비우면 trail.transform 사용)

        [Header("고정 회전 설정")]
        public bool useReferenceRotation = true; // 참조 회전을 사용할지 여부
        public Transform referenceTransform; // 고정 회전 참조 Transform
        public Vector3 fixedEulerAngles = Vector3.zero; // 참조가 없을 때 사용할 고정 Euler 회전값

        [Header("적용 타이밍")]
        public bool captureOnEnable = true; // OnEnable 때 회전값 캡처 여부
        public bool followReferenceEveryFrame = true; // 매 프레임 참조 회전을 다시 캡처할지 여부
        public bool clearTrailOnCapture = false; // 캡처 시 트레일 Clear 여부
        public bool applyInLateUpdate = true; // LateUpdate에서 적용할지 여부(끄면 Update에서 적용)
        public float rotationSlerpSpeed = 0f; // 0이면 즉시, 0보다 크면 부드럽게 보간 적용

        [HideInInspector]
        public Quaternion lockedRotation; // 내부 저장용 고정 회전값
    }

    [Header("트레일 목록")]
    [SerializeField] private List<TrailEntry> entries = new List<TrailEntry>(); // 트레일 엔트리 리스트

    private void Reset() // 초기 설정 자동 보조
    {
        AutoFillFromChildren(); // 자식의 TrailRenderer를 자동 등록(선택)
    }

    private void Awake() // 시작 전 초기화
    {
        ForceAlignmentToTransformZ_All(); // 모든 트레일 정렬 기준 강제
        CaptureAllBaseRotations(); // 모든 엔트리 회전 캡처
    }

    private void OnEnable() // 활성화 시점 처리
    {
        for (int i = 0; i < entries.Count; i++) // 엔트리 순회
        {
            TrailEntry e = entries[i]; // 엔트리 참조
            if (!IsEntryValid(e)) continue; // 유효성 검사

            if (e.captureOnEnable) // OnEnable 캡처 옵션
            {
                CaptureLockedRotation(e); // 회전값 캡처
                if (e.clearTrailOnCapture) e.trail.Clear(); // 트레일 초기화(선택)
            }
        }
    }

    private void Update() // Update 적용(엔트리별 옵션)
    {
        for (int i = 0; i < entries.Count; i++) // 엔트리 순회
        {
            TrailEntry e = entries[i]; // 엔트리 참조
            if (!IsEntryValid(e)) continue; // 유효성 검사
            if (e.applyInLateUpdate) continue; // LateUpdate에서 적용이면 스킵

            ApplyEntryRotation(e); // 회전 적용
        }
    }

    private void LateUpdate() // LateUpdate 적용(엔트리별 옵션)
    {
        for (int i = 0; i < entries.Count; i++) // 엔트리 순회
        {
            TrailEntry e = entries[i]; // 엔트리 참조
            if (!IsEntryValid(e)) continue; // 유효성 검사
            if (!e.applyInLateUpdate) continue; // Update에서 적용이면 스킵

            ApplyEntryRotation(e); // 회전 적용
        }
    }

    private void ApplyEntryRotation(TrailEntry e) // 엔트리 회전 적용 처리
    {
        if (e.followReferenceEveryFrame) // 매 프레임 캡처 옵션
            CaptureLockedRotation(e); // 현재 참조 회전을 다시 캡처

        Transform t = GetTargetTransform(e); // 실제 적용 대상 Transform

        if (e.rotationSlerpSpeed <= 0f) // 즉시 적용 모드
        {
            t.rotation = e.lockedRotation; // 고정 회전 즉시 적용
        }
        else // 보간 적용 모드
        {
            float lerpT = 1f - Mathf.Exp(-e.rotationSlerpSpeed * Time.deltaTime); // 프레임 독립 보간 계수
            t.rotation = Quaternion.Slerp(t.rotation, e.lockedRotation, lerpT); // 부드럽게 회전 적용
        }
    }

    private void CaptureLockedRotation(TrailEntry e) // 엔트리의 고정 회전값 캡처
    {
        if (e.useReferenceRotation && e.referenceTransform != null) // 참조 회전 사용 조건
            e.lockedRotation = e.referenceTransform.rotation; // 참조 회전을 캡처
        else
            e.lockedRotation = Quaternion.Euler(e.fixedEulerAngles); // 고정 Euler 회전을 캡처
    }

    private void CaptureAllBaseRotations() // 전체 엔트리 회전 캡처
    {
        for (int i = 0; i < entries.Count; i++) // 엔트리 순회
        {
            TrailEntry e = entries[i]; // 엔트리 참조
            if (!IsEntryValid(e)) continue; // 유효성 검사
            CaptureLockedRotation(e); // 회전값 캡처
        }
    }

    private void ForceAlignmentToTransformZ_All() // 모든 트레일 정렬 기준을 TransformZ로 강제
    {
        for (int i = 0; i < entries.Count; i++) // 엔트리 순회
        {
            TrailEntry e = entries[i]; // 엔트리 참조
            if (e == null || e.trail == null) continue; // null 방지
            e.trail.alignment = LineAlignment.TransformZ; // 로컬 Z축 기준으로 리본 정렬
        }
    }

    private Transform GetTargetTransform(TrailEntry e) // 엔트리에서 실제 회전 적용 대상 가져오기
    {
        if (e.targetTransform != null) return e.targetTransform; // targetTransform 우선
        return e.trail.transform; // 비어있으면 trail의 Transform 사용
    }

    private bool IsEntryValid(TrailEntry e) // 엔트리 유효성 검사
    {
        if (e == null) return false; // 엔트리 null 체크
        if (e.trail == null) return false; // TrailRenderer 필수
        if (GetTargetTransform(e) == null) return false; // 적용 대상 Transform 필수
        return true; // 유효
    }

    private void AutoFillFromChildren() // 자식 TrailRenderer를 자동으로 리스트에 채우기(선택 기능)
    {
        entries.Clear(); // 기존 목록 초기화
        TrailRenderer[] trs = GetComponentsInChildren<TrailRenderer>(true); // 자식 포함 TrailRenderer 수집

        for (int i = 0; i < trs.Length; i++) // 수집 결과 순회
        {
            TrailEntry e = new TrailEntry(); // 새 엔트리 생성
            e.trail = trs[i]; // TrailRenderer 할당
            e.targetTransform = trs[i].transform; // 기본 적용 대상 설정
            e.useReferenceRotation = true; // 기본값
            e.referenceTransform = trs[i].transform; // 기본값(같은 오브젝트도 가능)
            e.followReferenceEveryFrame = true; // 기본값(회전 변화 반영)
            entries.Add(e); // 리스트에 추가
        }
    }
}
