using UnityEngine;

[AddComponentMenu("Combat/FlameThrower")]
public class FlameThrower : MonoBehaviour
{
    [Header("참조")]
    [SerializeField] private Transform spawnPoint;                 // 분사 원점/방향(총구)
    [SerializeField] private Transform poolParent;                 // 오브젝트 풀 부모(계층 정리용)
    [SerializeField] private GameObject bulletPrefab;              // 분사체 프리팹(Flame 컴포넌트 필수)

    [Header("분사 설정 (정수)")]
    [SerializeField] private int bulletSpeed = 6;                  // 분사체 속도(정수, 1이면 초당 1유닛)
    [SerializeField] private int bulletRange = 12;                 // 분사체 기본 사거리(정수, 유닛)

    [Header("분사 제어")]
    [SerializeField] private bool fireWhileHolding = true;         // 우클릭+좌클릭 유지 동안 지속 분사 여부
    [SerializeField, Min(0f)] private float fireInterval = 0.05f;  // 분사 주기(초/발). 0.05s = 초당 20발

    [Header("화염방사 범위")]
    [Tooltip("부채꼴 전체 각도(도). 중앙(0°)에서 벗어날수록 사거리 선형 감소")]
    [SerializeField, Min(0f)] private float flamethrowerConeAngleDeg = 50f; // 화염방사 범위(°). 반각=angle/2

    [Header("스케일 성장(Flame에 전달)")]
    [SerializeField] private Vector3 startScale = Vector3.one;     // 분사체 스폰 시 시작 스케일(XYZ 전부)
    [SerializeField, Tooltip("초당 스케일 증가량(예: 0.5 → 매초 XYZ에 +0.5)")]
    private float scaleGrowPerSecond = 0.5f;                       // 초당 스케일 증가량(XYZ 동일 적용)
    [SerializeField, Tooltip("비활성화 시 적용할 스케일(XYZ)")]
    private Vector3 scaleOnDisable = Vector3.one;                  // 비활성화 시 최종 세팅할 스케일

    private float _fireTimer;                                      // 분사 타이머 누적(Interval 관리용)

    private void Update()                                          // 매 프레임: 입력 체크 및 분사 타이밍
    {
        if (!spawnPoint || !poolParent || !bulletPrefab) return;   // 필수 참조 누락 시 중단

        bool aiming = Input.GetMouseButton(1);                     // 우클릭(조준/분사 모드 유지)
        bool firing = Input.GetMouseButton(0);                     // 좌클릭(사격 입력)

        if (fireWhileHolding && aiming && firing)                  // 우클릭+좌클릭 유지 → 지속 분사
        {
            _fireTimer += Time.deltaTime;                          // 타이머 누적
            while (_fireTimer >= fireInterval)                     // 누적이 interval 이상이면
            {
                _fireTimer -= fireInterval;                        // interval 소모
                SpawnOneFlame();                                   // 한 발 스폰
            }
        }
        else
        {
            _fireTimer = 0f;                                       // 입력 해제 시 템포 리셋(버스트 방지)
        }
    }

    private void SpawnOneFlame()                                   // 분사체 한 발 스폰 처리
    {
        // 1) 무작위 각도 결정(±반각)
        float half = flamethrowerConeAngleDeg * 0.5f;              // 반각(°)
        float randAngle = (half > 0f) ? Random.Range(-half, +half) : 0f; // 반각 0 이하면 0°

        // 2) 각도 편차 → 사거리 보정(선형 감소)
        float normalized = (half > 0f) ? (Mathf.Abs(randAngle) / half) : 0f; // 0~1
        float effRangeF = bulletRange * (1f - normalized);         // 유효 사거리(0~기본)
        int effectiveRange = Mathf.Max(0, Mathf.RoundToInt(effRangeF)); // 정수 반올림(하한 0)

        // 3) 최종 분사 방향 계산 (로컬 Y+ 기준 전진)
        Vector3 baseDir = spawnPoint.up;                           // 중앙축 = spawnPoint의 up
        Vector3 shotDir = Quaternion.AngleAxis(randAngle, Vector3.forward) * baseDir; // Z축 회전

        // 4) 풀에서 오브젝트 획득 또는 생성
        GameObject go = GetFromPoolOrCreate();                     // 풀에서 가져오기/없으면 생성
        if (!go) return;

        // 5) 위치/회전/스케일 초기화
        Quaternion finalRot = Quaternion.FromToRotation(Vector3.up, shotDir); // up → shotDir
        go.transform.SetPositionAndRotation(spawnPoint.position, finalRot);   // 스폰 위치/회전
        go.transform.localScale = startScale;                     // 시작 스케일 적용
        go.SetActive(true);                                       // 활성화

        // 6) 초기화(속도/유효 사거리/스케일 성장 파라미터 전달)
        var flame = go.GetComponent<Flame>();                     // Flame 컴포넌트 참조
        if (flame != null)
        {
            flame.Initialize(
                bulletSpeed,                                      // 이동 속도
                effectiveRange,                                   // 유효 사거리
                scaleGrowPerSecond,                               // 초당 스케일 증가량(XYZ 동일)
                scaleOnDisable                                    // 비활성화 시 적용할 스케일
            );
        }
    }

    private GameObject GetFromPoolOrCreate()                       // 풀에서 비활성 객체 반환 또는 새 생성
    {
        for (int i = 0; i < poolParent.childCount; i++)            // 풀 자식 순회
        {
            var child = poolParent.GetChild(i).gameObject;
            if (!child.activeSelf) return child;                   // 비활성 발견 시 즉시 반환
        }
        var go = Instantiate(bulletPrefab, poolParent);            // 없으면 새로 생성
        go.SetActive(false);                                       // 풀 정책: 생성 직후 비활성
        return go;                                                 // 반환
    }
}
