using System.Collections;
using UnityEngine;

[AddComponentMenu("Combat/Flame")]
public class Flame : MonoBehaviour
{
    [Header("트레일 초기화 설정")]
    [SerializeField] private float trailDefaultTime = 0.2f;        // 트레일 기본 time(예: 0.2)
    [SerializeField] private int trailRestoreFrames = 2;           // 활성화 후 time 복구 지연 프레임(1~2 권장)

    // ── 수명/이동 파라미터 ──
    private int speed;                                             // 이동 속도(정수, 1이면 초당 1유닛)
    private int range;                                             // 사거리(정수, 유닛) - Shooter에서 보정됨
    private float lifeTime;                                        // 자동 비활성까지 시간(초) = range/speed
    private float elapsed;                                         // 경과 시간(초)
    private bool initialized;                                      // Initialize 완료 여부

    // ── 스케일 성장 파라미터 ──
    private float growPerSecond;                                    // 초당 스케일 증가량(XYZ 동일 적용)
    private Vector3 scaleOnDisable;                                 // 비활성화 시 적용할 최종 스케일(XYZ)

    // ── 캐시 ──
    private TrailRenderer[] trails;                                 // 자식 트레일 캐시
    private Coroutine trailRoutine;                                 // 트레일 복구 코루틴 핸들
    private Rigidbody rb;                                           // 3D 리지드바디(선택)
    private Rigidbody2D rb2d;                                       // 2D 리지드바디(선택)

    private void Awake()                                            // 컴포넌트/트레일 캐싱
    {
        trails = GetComponentsInChildren<TrailRenderer>(true);      // 자식 트레일 모두 캐싱
        rb = GetComponent<Rigidbody>();                             // 3D 리지드바디(있으면)
        rb2d = GetComponent<Rigidbody2D>();                         // 2D 리지드바디(있으면)
    }

    public void Initialize(                                         // 속도/사거리/스케일 성장 초기화
        int speed,                                                  // 이동 속도(정수)
        int range,                                                  // 유효 사거리(정수)
        float growPerSecond,                                        // 초당 스케일 증가량(XYZ 동일)
        Vector3 scaleOnDisable                                      // 비활성화 시 적용할 스케일(XYZ)
    )
    {
        this.speed = Mathf.Max(1, speed);                           // 분모 0 방지
        this.range = Mathf.Max(0, range);                           // 음수 방지

        this.growPerSecond = growPerSecond;                         // 스케일 성장량 저장
        this.scaleOnDisable = scaleOnDisable;                       // 비활성화 시 적용할 스케일 저장

        float raw = (float)this.range / (float)this.speed;          // 수명(초) = 사거리/속도
        lifeTime = Mathf.Max(0.01f, Mathf.Round(raw * 100f) / 100f);// 최소 0.01s, 소수 둘째 반올림

        elapsed = 0f;                                               // 경과 리셋
        initialized = true;                                         // 초기화 완료

        if (trailRoutine != null) StopCoroutine(trailRoutine);      // 기존 코루틴 정지
        trailRoutine = StartCoroutine(RestoreTrailsAfterFrames());  // 프레임 지연 후 트레일 복구
    }

    private IEnumerator RestoreTrailsAfterFrames()                  // 활성화 직후 트레일 초기화/복구
    {
        ImmediateTrailClear();                                      // time=0, emitting=false, Clear()

        for (int i = 0; i < Mathf.Max(1, trailRestoreFrames); i++)  // 1~2 프레임 대기
            yield return null;

        foreach (var t in trails)                                   // 기본 time 복구 + 방출 ON
        {
            if (!t) continue;
            t.time = Mathf.Max(0f, trailDefaultTime);               // 기본 time
            t.emitting = true;                                      // 방출 ON
        }
        trailRoutine = null;                                        // 종료 표식
    }

    private void ImmediateTrailClear()                              // 트레일 즉시 초기화(재활성 직/후)
    {
        foreach (var t in trails)
        {
            if (!t) continue;
            t.emitting = false;                                     // 방출 중단
            t.time = 0f;                                            // 수명 0
            t.Clear();                                              // 버퍼 비우기
        }
    }

    private void OnDisable()                                        // 비활성화 시 정리(풀 반환 시점)
    {
        if (trailRoutine != null)
        {
            StopCoroutine(trailRoutine);                            // 코루틴 정지
            trailRoutine = null;
        }
        ImmediateTrailClear();                                      // 잔상 제거

        // 요청사항: 비활성화 시 지정한 스케일 적용(풀에 돌아갈 때 스케일 정규화)
        transform.localScale = scaleOnDisable;                      // 최종 스케일 강제 세팅

        if (rb)  { rb.linearVelocity = Vector3.zero; rb.angularVelocity = Vector3.zero; } // 3D 속도 초기화
        if (rb2d){ rb2d.linearVelocity = Vector2.zero; rb2d.angularVelocity = 0f; }       // 2D 속도 초기화(Unity 6)
    }

    private void Update()                                           // 이동/수명 소거/스케일 성장
    {
        if (!initialized) return;

        // 이동
        transform.position += transform.up * (speed * Time.deltaTime); // 로컬 Y+ 전진

        // 스케일 성장(초당 growPerSecond 만큼 XYZ에 동일 가산)
        if (!Mathf.Approximately(growPerSecond, 0f))
        {
            float delta = growPerSecond * Time.deltaTime;           // 프레임 보정
            transform.localScale += new Vector3(delta, delta, delta); // XYZ 동일 증가
        }

        // 수명 타이머
        elapsed += Time.deltaTime;                                  // 경과 누적
        if (elapsed >= lifeTime)                                    // 수명 도달
        {
            gameObject.SetActive(false);                            // 풀로 복귀(비활성화)
        }
    }
}
