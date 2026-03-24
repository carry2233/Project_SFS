using System.Collections;
using UnityEngine;

[AddComponentMenu("Combat/BulletCompat")]
public class BulletCompat : MonoBehaviour
{
    [Header("트레일 초기화 설정")]
    [SerializeField] private float trailDefaultTime = 0.2f; // 인스펙터 기본 time(예: 0.2)
    [SerializeField] private int trailRestoreFrames = 2;    // 활성화 후 time 복구까지 지연 프레임 수(1~2 권장)

    // ───────── 타이머 전용 비활성화 구성 ─────────
    private int speed;                                      // 이동 속도(정수, 1이면 초당 1유닛)
    private int range;                                      // 사거리(정수, 유닛)
    private float lifeTime;                                 // 자동 비활성화 시간(초) = (range/speed) 반올림(소수 둘째 자리)
    private float elapsed;                                  // 경과 시간(초)
    private bool initialized;                               // 초기화 여부

    // ───────── 트레일/물리 캐시 ─────────
    private TrailRenderer[] trails;                         // 자식 트레일 캐시
    private Coroutine trailRoutine;                         // 트레일 복구 코루틴 핸들
    private Rigidbody rb;                                   // 3D 물리(선택)
    private Rigidbody2D rb2d;                               // 2D 물리(선택)

    private void Awake()                                    // 컴포넌트/트레일 캐싱
    {
        trails = GetComponentsInChildren<TrailRenderer>(true); // 자식 트레일 모두 캐싱
        rb = GetComponent<Rigidbody>();                        // 3D 리지드바디(있으면)
        rb2d = GetComponent<Rigidbody2D>();                    // 2D 리지드바디(있으면)
    }

    public void Initialize(int speed, int range)            // 초기값 설정(수명 자동 계산)
    {
        this.speed = Mathf.Max(1, speed);                   // 속도(정수) 보정(분모 0 방지)
        this.range = Mathf.Max(0, range);                   // 사거리(정수) 보정

        float raw = (float)this.range / (float)this.speed;  // 실수 계산(초)
        lifeTime = Mathf.Max(0.01f, Mathf.Round(raw * 100f) / 100f); // 0.01 이상, 소수 둘째 자리 반올림

        elapsed = 0f;                                       // 경과 시간 리셋
        initialized = true;                                 // 초기화 완료

        if (trailRoutine != null) StopCoroutine(trailRoutine);    // 기존 코루틴 정지
        trailRoutine = StartCoroutine(RestoreTrailsAfterFrames()); // 프레임 지연 후 time 복구
    }

    private IEnumerator RestoreTrailsAfterFrames()          // 활성화 후 트레일 time 복구 코루틴
    {
        ImmediateTrailClear();                              // 활성화 직후: time=0, emitting=false, Clear()

        for (int i = 0; i < Mathf.Max(1, trailRestoreFrames); i++) // 1~2 프레임 대기
            yield return null;

        foreach (var t in trails)                           // 기본 time 복구 + 방출 시작
        {
            if (!t) continue;
            t.time = Mathf.Max(0f, trailDefaultTime);       // 기본 time 복구
            t.emitting = true;                              // 방출 ON
        }
        trailRoutine = null;                                // 종료 표식
    }

    private void ImmediateTrailClear()                      // 트레일 즉시 초기화(재활성 직/후)
    {
        foreach (var t in trails)
        {
            if (!t) continue;
            t.emitting = false;                             // 방출 중단
            t.time = 0f;                                    // 수명 0
            t.Clear();                                      // 버퍼 비우기
        }
    }

    private void OnDisable()                                // 비활성화 시 정리(풀 반환 직전/직후)
    {
        if (trailRoutine != null)                           // 코루틴 정지
        {
            StopCoroutine(trailRoutine);
            trailRoutine = null;
        }
        ImmediateTrailClear();                              // 잔상 제거

        if (rb)  { rb.linearVelocity = Vector3.zero; rb.angularVelocity = Vector3.zero; } // 3D 속도 초기화
        if (rb2d){ rb2d.linearVelocity = Vector2.zero; rb2d.angularVelocity = 0f; } // 2D 속도 초기화(Unity 6)
    }

    private void Update()                                   // 이동 및 타이머 기반 비활성화
    {
        if (!initialized) return;

        transform.position += transform.up * (speed * Time.deltaTime); // 로컬 Y+ 이동
        elapsed += Time.deltaTime;                           // 타이머 누적
        if (elapsed >= lifeTime)                             // 수명 도달 시
        {
            gameObject.SetActive(false);                     // 풀 반환(비활성화)
        }
    }
}
