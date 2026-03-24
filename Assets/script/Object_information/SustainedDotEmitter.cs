using UnityEngine;                                    // 유니티 기본

[RequireComponent(typeof(Collider2D))]                // 2D 트리거 필수
[AddComponentMenu("Combat/Sustained DOT Emitter")]    // 인스펙터 메뉴
public class SustainedDotEmitter : MonoBehaviour
{
    [Header("DOT 키")]
    public int typeId = 1;                             // ✅ 지속피해종류ID
    public int checkId = 0;                            // ✅ 판별ID

    [Header("필터/옵션")]
    public LayerMask targetLayers = ~0;                // ✅ 대상 레이어 마스크
    public bool useTriggerStay = false;                // ✅ OnTriggerStay 사용 여부(연속 적용)
    public bool logHit = false;                        // ✅ 적용 로그 On/Off

    private Collider2D _col;                           // ✅ 자기 콜라이더 캐시

    private void Reset()                               // ▶ 초기 셋업
    {
        _col = GetComponent<Collider2D>();
        if (_col) _col.isTrigger = true;              // 트리거 강제
    }

    private void Awake()                               // ▶ 캐시
    {
        _col = GetComponent<Collider2D>();
    }

    private void OnTriggerEnter2D(Collider2D other)    // ▶ 진입 시 1회 적용
    {
        if (useTriggerStay) return;                    // Stay 모드면 Enter 무시
        TryApply(other);
    }

    private void OnTriggerStay2D(Collider2D other)     // ▶ 유지 중 연속 적용(옵션)
    {
        if (!useTriggerStay) return;                   // Enter 모드면 Stay 무시
        TryApply(other);
    }

    private void TryApply(Collider2D other)            // ▶ 수신자 탐색/호출
    {
        if (!other) return;

        // 레이어 필터 체크
        if (((1 << other.gameObject.layer) & targetLayers) == 0) return;

        // 대상에서 SustainedDotReceiver 찾기(직접 또는 부모)
        var recv = other.GetComponent<SustainedDotReceiver>();
        if (!recv) recv = other.GetComponentInParent<SustainedDotReceiver>();
        if (!recv) return;

        // 대상의 triggerCollider와 충돌했는지까지 강하게 확인하고 싶다면 여기서 비교 가능(옵션)
        // ex) if (recv && recv.TriggerColliderReference && other != recv.TriggerColliderReference) return;

        recv.OnDotHit(typeId, checkId);               // DOT 부여

        if (logHit) Debug.Log($"[DOT Emit] from={name} → target={other.name} key=({typeId},{checkId})");
    }
}
