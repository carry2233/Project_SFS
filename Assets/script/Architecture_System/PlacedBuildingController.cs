using UnityEngine;                               // 유니티 기본 네임스페이스

[AddComponentMenu("Build/Placed Building Controller (설치된 건물 컨트롤러)")] // 인스펙터 메뉴 경로
public class PlacedBuildingController : MonoBehaviour
{
    [Header("렌더러/스프라이트 설정")]
    public SpriteRenderer targetRenderer;        // 방향에 따라 이미지를 바꿀 SpriteRenderer
    public Sprite spriteUp;                      // 위쪽 방향 스프라이트
    public Sprite spriteRight;                   // 오른쪽 방향 스프라이트
    public Sprite spriteDown;                    // 아래쪽 방향 스프라이트
    public Sprite spriteLeft;                    // 왼쪽 방향 스프라이트

    [Header("회전 설정")]
    public bool rotateTransform = true;          // Transform 자체를 회전할지 여부
    public bool applySpriteFlip = false;         // 스프라이트 플립을 사용할지 여부(필요 시)

    private BuildDirection currentDirection = BuildDirection.Up; // 현재 방향 상태

    private void Awake()                         // 초기화(Awake 시점)
    {
        if (!targetRenderer)                     // SpriteRenderer가 비어있으면
        {
            targetRenderer = GetComponentInChildren<SpriteRenderer>(); // 자식에서 자동 검색
        }
    }

    /// <summary>
    /// 설치 직후 외부에서 방향을 넘겨줄 때 호출되는 초기화 함수.
    /// </summary>
    public void Initialize(BuildDirection direction) // 설치 시 방향 초기화
    {
        currentDirection = direction;            // 현재 방향 저장
        ApplyDirectionVisual();                  // 방향에 따른 시각 표현 적용
    }

    /// <summary>
    /// 현재 방향 상태를 기준으로 Transform 회전 및 스프라이트를 적용한다.
    /// </summary>
    private void ApplyDirectionVisual()          // 방향에 따른 회전/스프라이트 적용
    {
        // 1) Transform 회전
        if (rotateTransform)                    // Transform 회전을 사용할 경우
        {
            float angle = DirectionToAngle(currentDirection); // 방향 → 각도 변환
            transform.localRotation = Quaternion.Euler(0f, 0f, angle); // 로컬 Z 회전 적용
        }

        // 2) SpriteRenderer 스프라이트/플립 설정
        if (targetRenderer)                     // 타겟 렌더러가 있을 때만 처리
        {
            Sprite sprite = GetSpriteForDirection(currentDirection); // 방향에 맞는 스프라이트 선택
            targetRenderer.sprite = sprite;      // 선택된 스프라이트 적용

            if (applySpriteFlip)                // 필요할 경우 플립 사용
            {
                // 예: Left/Right를 같은 스프라이트로 공유하고 플립만 다르게 주고 싶을 때 사용
                targetRenderer.flipX = (currentDirection == BuildDirection.Left); // 왼쪽일 때만 좌우 플립
            }
            else
            {
                targetRenderer.flipX = false;    // 플립 사용 안 할 때는 항상 false
            }
        }
    }

    /// <summary>
    /// 현재 방향에 해당하는 스프라이트를 반환한다.
    /// (지정되지 않은 경우 null이 될 수 있음)
    /// </summary>
    private Sprite GetSpriteForDirection(BuildDirection direction) // 방향에 맞는 스프라이트 반환
    {
        switch (direction)                      // 방향별로 분기
        {
            default:
            case BuildDirection.Up:    return spriteUp;    // 위쪽 스프라이트
            case BuildDirection.Right: return spriteRight; // 오른쪽 스프라이트
            case BuildDirection.Down:  return spriteDown;  // 아래쪽 스프라이트
            case BuildDirection.Left:  return spriteLeft;  // 왼쪽 스프라이트
        }
    }

    /// <summary>
    /// BuildDirection을 Z축 회전 각도로 변환한다.
    /// (BuildProgressAndPlacer의 DirectionToAngle과 같은 기준)
    /// </summary>
    private float DirectionToAngle(BuildDirection direction) // 방향 → 회전 각도 변환
    {
        switch (direction)                      // 방향에 따라 각도 결정
        {
            default:
            case BuildDirection.Up:    return 0f;    // 위: 0도
            case BuildDirection.Right: return -90f;  // 오른쪽: -90도(시계 방향)
            case BuildDirection.Down:  return 180f;  // 아래: 180도
            case BuildDirection.Left:  return 90f;   // 왼쪽: 90도
        }
    }
}
