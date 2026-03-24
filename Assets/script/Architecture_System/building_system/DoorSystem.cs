using UnityEngine;

public class DoorSystem2D : MonoBehaviour
{
    [Header("문 회전 대상")]
    public Transform targetRotator;                      // 회전할 문 오브젝트

    [Header("문 탐지용 콜라이더 (Trigger X)")]
    public Collider2D detectionCollider;                 // 문이 들고 있는 탐지 콜라이더(Trigger = OFF)

    [Header("회전 설정")]
    public float speedMultiplier = 1f;                   // 속도 배율
    public float duration = 1f;                          // 회전 지속 시간
    public AnimationCurve curve;                         // 보간 커브

    public enum DoorMode { Open, Closed }               // 문 상태
    public DoorMode startMode = DoorMode.Closed;         // 씬 시작 모드

    public Vector3 openOffset = new Vector3(0, 0, 90);   // 열림 회전값
    public Vector3 closeOffset = new Vector3(0, 0, 0);   // 닫힘 회전값

    private DoorMode currentMode;                        // 현재 모드
    private bool isMoving = false;                       // 회전 중 여부

    private void Start()
    {
        currentMode = startMode;                         // 시작 모드 설정

        // 시작 모드 적용
        if (currentMode == DoorMode.Open)
            targetRotator.localEulerAngles = openOffset;
        else
            targetRotator.localEulerAngles = closeOffset;
    }

    public void ChangeState()                            // 문 상태 전환
    {
        if (isMoving) return;
        StartCoroutine(RotateRoutine());
    }

    private System.Collections.IEnumerator RotateRoutine()
    {
        isMoving = true;

        Vector3 startRot = targetRotator.localEulerAngles; 
        Vector3 targetRot = (currentMode == DoorMode.Open)
            ? closeOffset
            : openOffset;

        float t = 0f;

        while (t < 1f)
        {
            t += Time.deltaTime * speedMultiplier / duration;
            float c = curve.Evaluate(Mathf.Clamp01(t));

            targetRotator.localEulerAngles =
                Vector3.Lerp(startRot, targetRot, c);

            yield return null;
        }

        targetRotator.localEulerAngles = targetRot;         // 최종 보정
        currentMode = (currentMode == DoorMode.Open)
            ? DoorMode.Closed
            : DoorMode.Open;

        isMoving = false;
    }
}
