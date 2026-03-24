using System.Collections;                            // 코루틴 사용
using UnityEngine;                                   // 유니티 기본

[AddComponentMenu("VFX/Blood Pool Item (예약 플래그)")] // 인스펙터 메뉴
public class BloodPoolItem : MonoBehaviour
{
    [Header("필수 컴포넌트")]
    [SerializeField] private SpriteRenderer sr;      // ✅ 투명도 조절용 스프라이트렌더러

    [Header("예약/상태")]
    public bool inUse = false;                        // ✅ 예약 플래그(선점 여부)
    private Coroutine lifeRoutine;                    // ✅ 유지+페이드 코루틴 핸들

    private void Reset()                              // 🔧 인스펙터 자동 할당
    {
        if (!sr) sr = GetComponentInChildren<SpriteRenderer>();
    }

    private void OnDisable()                          // 🧹 비활성화 시 안전 정리
    {
        // 코루틴 종료
        if (lifeRoutine != null)
        {
            StopCoroutine(lifeRoutine);
            lifeRoutine = null;
        }
        // 예약 해제
        inUse = false;
    }

    public void PrepareForReuse()                     // ♻️ 재사용 초기화(알파/코루틴 정리)
    {
        if (lifeRoutine != null)
        {
            StopCoroutine(lifeRoutine);
            lifeRoutine = null;
        }
        if (sr)
        {
            var c = sr.color;
            c.a = 1f;
            sr.color = c;
        }
    }

    public void BeginLifecycle(                       // ▶ 유지시간+페이드아웃 시작
        float keepSeconds,                            //   유지 시간(초)
        float fadeSeconds,                            //   페이드 시간(초)
        AnimationCurve fadeCurve = null               //   페이드 곡선(0→1), null이면 선형
    )
    {
        if (lifeRoutine != null)
        {
            StopCoroutine(lifeRoutine);
            lifeRoutine = null;
        }
        lifeRoutine = StartCoroutine(Co_Lifecycle(keepSeconds, fadeSeconds, fadeCurve));
    }

    private IEnumerator Co_Lifecycle(                 // ⏱ 유지 후 서서히 투명도 0 → 풀 복귀
        float keepSeconds,
        float fadeSeconds,
        AnimationCurve fadeCurve
    )
    {
        // 유지
        if (keepSeconds > 0f)
            yield return new WaitForSeconds(keepSeconds);

        // 페이드
        if (sr && fadeSeconds > 0f)
        {
            float t = 0f;
            Color baseC = sr.color;
            while (t < 1f)
            {
                t += Time.deltaTime / Mathf.Max(0.0001f, fadeSeconds);
                float k = Mathf.Clamp01(t);
                if (fadeCurve != null) k = Mathf.Clamp01(fadeCurve.Evaluate(k));
                float a = Mathf.Lerp(1f, 0f, k);
                sr.color = new Color(baseC.r, baseC.g, baseC.b, a);
                yield return null;
            }
        }

        // 풀 복귀(비활성화 + 예약 해제)
        gameObject.SetActive(false);
        inUse = false;
        lifeRoutine = null;
    }
}
