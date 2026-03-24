using UnityEngine;

[DisallowMultipleComponent] // 중복 부착 방지
[AddComponentMenu("Movement/Object Moving")] // 컴포넌트 메뉴 경로
public class ObjectMoving : MonoBehaviour
{
    public enum Axis { X, Y, Z } // 축 선택용 열거형

    [Header("키 설정")]
    [SerializeField] private KeyCode toggleKey = KeyCode.Space;  // 토글 키(기본: 스페이스)

    [Header("이동 설정")]
    [Tooltip("초당 이동 속도(단위/초). 음수 가능")]
    [SerializeField] private float moveSpeed = 2f;               // 이동 속도(+/– 가능)
    [Tooltip("이동이 적용될 로컬 축(X/Y/Z)")]
    [SerializeField] private Axis moveAxis = Axis.X;             // 이동 적용 축

    [Header("회전 설정")]
    [Tooltip("초당 회전 속도(도/초). 음수 가능")]
    [SerializeField] private float rotateSpeed = 90f;            // 회전 속도(+/– 가능)
    [Tooltip("회전이 적용될 로컬 축(X/Y/Z)")]
    [SerializeField] private Axis rotateAxis = Axis.Y;           // 회전 적용 축

    [Header("동작 옵션")]
    [Tooltip("토글 초기 상태(시작 시 적용 여부)")]
    [SerializeField] private bool isActive = true;               // 이동/회전 적용 여부 토글 상태
    [Tooltip("이동/회전 연산을 로컬 좌표계로 수행(권장). 끄면 월드 기준")]
    [SerializeField] private bool useLocalSpace = true;          // 로컬/월드 공간 선택

    private void Update() // 매 프레임 갱신
    {
        // 토글 키 입력 감지
        if (Input.GetKeyDown(toggleKey)) // 토글 키를 눌렀는가?
        {
            isActive = !isActive;        // 이동/회전 적용 여부 토글
        }

        if (!isActive) return;           // 비활성화면 이동/회전 미적용

        // 이동 처리
        if (moveSpeed != 0f)             // 속도가 0이 아니면
        {
            Vector3 dir = AxisToVector(moveAxis);                                 // 이동 축을 벡터로 변환
            Vector3 delta = dir * moveSpeed * Time.deltaTime;                     // 프레임 보정 이동량
            if (useLocalSpace) transform.Translate(delta, Space.Self);            // 로컬 기준 이동
            else               transform.Translate(delta, Space.World);           // 월드 기준 이동
        }

        // 회전 처리
        if (rotateSpeed != 0f)           // 회전 속도가 0이 아니면
        {
            Vector3 axis = AxisToVector(rotateAxis);                              // 회전 축 벡터
            float deltaAngle = rotateSpeed * Time.deltaTime;                      // 프레임 보정 회전각
            if (useLocalSpace) transform.Rotate(axis, deltaAngle, Space.Self);    // 로컬 기준 회전
            else               transform.Rotate(axis, deltaAngle, Space.World);   // 월드 기준 회전
        }
    }

    private static Vector3 AxisToVector(Axis axis) // 축 열거형 → 단위 벡터 변환
    {
        switch (axis)
        {
            case Axis.X: return Vector3.right;   // X축
            case Axis.Y: return Vector3.up;      // Y축
            case Axis.Z: return Vector3.forward; // Z축
            default:      return Vector3.right;  // 안전망
        }
    }

    // --- 공개 설정자(필요 시 인게임에서 제어) ---
    public void SetActive(bool value) { isActive = value; }                 // 적용 여부 직접 설정
    public void ToggleActive()        { isActive = !isActive; }             // 적용 여부 토글
    public void SetMoveSpeed(float v) { moveSpeed = v; }                    // 이동 속도 설정
    public void SetRotateSpeed(float v) { rotateSpeed = v; }                // 회전 속도 설정
    public void SetMoveAxis(Axis a)   { moveAxis = a; }                     // 이동 축 설정
    public void SetRotateAxis(Axis a) { rotateAxis = a; }                   // 회전 축 설정
    public void SetToggleKey(KeyCode k) { toggleKey = k; }                  // 토글 키 설정
}
