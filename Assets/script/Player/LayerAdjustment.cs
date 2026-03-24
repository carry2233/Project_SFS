using UnityEngine;
using System.Collections.Generic; // ✅ List<T>를 쓰려면 꼭 필요

[DisallowMultipleComponent]
[AddComponentMenu("Rendering/레이어조절")]
public class 레이어조절 : MonoBehaviour
{
    [Header("참조")]
    public Transform reference;                 // 위치 판단 기준 오브젝트(없으면 월드 원점)

[System.Serializable]
public class TargetEntry {
    public SpriteRenderer renderer; // 적용할 스프라이트렌더러
    public int offset;              // 기본 order에 더해줄 오프셋
}
public List<TargetEntry> targetRenderers = new(); // 여러 타겟(렌더러+오프셋) 리스트



    [Header("동작 설정")]
    [SerializeField] private int baseOrder = 0;           // 기준 위치(Δ=0)일 때 기본 Order
    [Min(0.0001f)]
    public float zUnitsPerOrder = 1f;                     // **이제 Y 단위로 사용**(이름만 유지) ← 변경 주석
    [SerializeField] private bool invert = false;         // 증가 방향 반전(Y↑일수록 Order↓)
    [SerializeField] private bool useLocalZ = false;      // 월드 대신 로컬 좌표 사용할지(이 필드명은 그대로 사용)

    [Header("범위/갱신")]
    [SerializeField] private int minOrder = -32768;       // Order 하한
    [SerializeField] private int maxOrder =  32767;       // Order 상한
    [SerializeField] private bool continuousUpdate = true;// 매 프레임 자동 갱신할지

    public enum UpdatePhase { Update, LateUpdate, FixedUpdate } // 갱신 시점
    [SerializeField] private UpdatePhase updatePhase = UpdatePhase.LateUpdate; // 기본: LateUpdate

    // 내부 캐시
    private float lastAppliedZ = float.NaN;               // 마지막 반영 Δ(이제 YΔ로 사용)  ← 의미 변경
    private int   lastAppliedOrder = int.MinValue;        // 마지막 반영 Order

private void Reset()
{
    if (targetRenderers == null || targetRenderers.Count == 0) // 리스트가 비었으면
    {
        var sr = GetComponent<SpriteRenderer>();
        if (sr != null) targetRenderers.Add(new TargetEntry { renderer = sr, offset = 0 });
    }
}




    private void OnValidate()                              // 에디터 값 변경 시 반영
    {
        if (zUnitsPerOrder < 0.0001f) zUnitsPerOrder = 0.0001f;
        if (!isActiveAndEnabled) return;
        Refresh();
    }

    private void Update()                                  // Update 단계
    {
        if (continuousUpdate && updatePhase == UpdatePhase.Update)
            TryApply();
    }

    private void LateUpdate()                              // LateUpdate 단계
    {
        if (continuousUpdate && updatePhase == UpdatePhase.LateUpdate)
            TryApply();
    }

    private void FixedUpdate()                             // FixedUpdate 단계
    {
        if (continuousUpdate && updatePhase == UpdatePhase.FixedUpdate)
            TryApply();
    }

    public void Refresh()                                  // 외부 수동 갱신 요청
    {
        lastAppliedZ = float.NaN;                         // 캐시 무효화
        lastAppliedOrder = int.MinValue;                  // 캐시 무효화
        TryApply();                                       // 즉시 반영
    }

    private void TryApply()                                // 현재 위치→Order 계산/적용
    {
    if (targetRenderers == null || targetRenderers.Count == 0) return; // 리스트 비면 종료

        // **Z가 아니라 Y 좌표를 기준으로 Δ를 계산**  ← 핵심 변경
        float ySelf = useLocalZ ? transform.localPosition.y : transform.position.y; // useLocalZ 플래그는 이름만 재사용
        float yRef  = 0f;
        if (reference != null)
            yRef = useLocalZ ? reference.localPosition.y : reference.position.y;

        float deltaY = ySelf - yRef;                      // 기준 대비 ΔY

        // 캐시 비교(필드명은 그대로 두되 의미는 ΔY로 사용)
        if (Mathf.Approximately(deltaY, lastAppliedZ) == false)
        {
            int order = CalculateOrder(deltaY);           // ΔY→Order
            ApplyOrder(order);                            // 적용
            lastAppliedZ = deltaY;                        // 캐시 업데이트
        }
    }

    private int CalculateOrder(float deltaY)               // ΔY를 Order로 변환
    {
        int step = Mathf.RoundToInt(deltaY / zUnitsPerOrder); // ΔY 단위 스텝
        if (invert) step = -step;
        int order = baseOrder + step;
        order = Mathf.Clamp(order, minOrder, maxOrder);
        return order;
    }

private void ApplyOrder(int order)
{
    if (order == lastAppliedOrder) return;
    if (targetRenderers == null) return;

    foreach (var entry in targetRenderers)
    {
        if (entry.renderer == null) continue;
        entry.renderer.sortingOrder = order + entry.offset; // 기본 order + 오프셋
    }

    lastAppliedOrder = order;
}


}
