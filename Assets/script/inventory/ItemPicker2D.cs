using System.Collections.Generic;
using UnityEngine;

[AddComponentMenu("Item Runtime/Item Picker 2D (부채꼴+원점회전+레이캐스트 클릭 픽업)")]
public class ItemPicker2D : MonoBehaviour
{
    [Header("참조")]
    public Inventory inventory;                  // 인벤토리 참조

    [Header("부채꼴 설정")]
    public Transform origin;                     // 부채꼴 원점(회전/위치 기준)  // 원점
    public float radius = 3.5f;                  // 탐지 반경(레이 길이)        // 반경
    [Range(0,180)] public float halfAngleDeg=35; // 반시야각(전체 시야는 2배)    // 각도
    public Vector2 forwardAxis = Vector2.right;  // 원점의 전방 기준 벡터(지역) // 전방축

    [Header("레이어/검색")]
    public LayerMask itemLayer;                  // 아이템 레이어(필수)          // 아이템 레이어
    public bool requireLineOfSight = true;       // 원점→대상 시야막힘 불허 여부 // 시야 체크

    private Camera _cam;                         // 메인 카메라 캐시              // 카메라

    private void Reset()                         // 기본값 보정
    {
        if (!origin) origin = transform;
        if (radius < 0.1f) radius = 3.5f;
        if (forwardAxis.sqrMagnitude < 1e-4f) forwardAxis = Vector2.right;
    }

    private void Awake()                         // 초기화
    {
        _cam = Camera.main;
    }

    private void Update()                        // 입력 체크(좌클릭 픽업)
    {
        if (Input.GetMouseButtonDown(0))
            TryPickByMouse();                    // 좌클릭 픽업 시도
    }

private void TryPickByMouse()                // 마우스 좌클릭 픽업 본체 메서드
{
    if (!inventory || !origin) return;

    // 1) 마우스 아래 2D 콜라이더 찾기
    Vector2 mouseWorld = _cam
        ? (Vector2)_cam.ScreenToWorldPoint(Input.mousePosition)
        : (Vector2)Input.mousePosition;

    var col = Physics2D.OverlapPoint(mouseWorld, itemLayer);
    if (!col) return;

    var fi = col.GetComponent<FieldItem>();
    if (!fi) return;

    // 2) 부채꼴+시야(레이캐스트) 판정
    if (!IsInSectorAndVisible(col)) return;

    // 3) 픽업 수행
    Pickup(fi);                              // ✅ 이제 아래 메서드가 있으므로 정상
}

private void Pickup(FieldItem fi)           // 필드 아이템 실제 인벤토리로 넣는 메서드
{
    if (!fi || !inventory) return;

    // FieldItem에서 정보 꺼내서 인벤토리에 추가
    // (Inventory.AddItem 시그니처에 맞춰서 넣기)

    inventory.AddItem(
        fi.typeId,                          // 종류 id
        fi.itemId,                          // 아이템 id
        fi.count,                           // 개수
        fi.displayName,                     // 표시 이름
        fi.icon,                            // 아이콘
        fi.durability,                      // 현재 내구도
        fi.maxDurability,                   // 최대 내구도 (새 시그니처라면)
        fi.weight                           // 무게
    );

    // 필드에서 아이템 제거
    Destroy(fi.gameObject);
}

    private bool IsInSectorAndVisible(Collider2D target)  // 대상이 부채꼴 내 & (옵션)원점→대상 레이로 직접 가시인지 검사
    {
        if (!target || !origin) return false;

        Vector2 o = origin.position;                       // 원점 월드 좌표
        Vector2 c = target.bounds.center;                  // 대상 중심점
        Vector2 to = c - o;                                // 원점→대상 벡터
        float dist = to.magnitude;                         // 거리

        if (dist > radius) return false;                   // 반경 초과

        Vector2 fwd = ForwardFromOrigin();                 // 회전 반영 전방 벡터
        float cos = Vector2.Dot(fwd, to.normalized);       // 각도 코사인
        float cosLim = Mathf.Cos(halfAngleDeg * Mathf.Deg2Rad);
        if (cos < cosLim) return false;                    // 부채꼴 밖

        if (!requireLineOfSight) return true;

        // 원점→대상 레이캐스트로 "바로 그 콜라이더"가 1차 히트인지 확인
        var hit = Physics2D.Raycast(o, to.normalized, dist + 0.01f, itemLayer);
        if (!hit.collider) return false;

        return hit.collider == target;                     // 첫 히트가 대상과 동일해야 통과
    }

    private Vector2 ForwardFromOrigin()                    // 원점 회전 반영 전방 벡터 계산
    {
        var v = (Vector2)(origin.rotation * new Vector3(forwardAxis.x, forwardAxis.y, 0f));
        return v.sqrMagnitude < 1e-4f ? Vector2.right : v.normalized;
    }

private void PickupFieldItem(FieldItem fi)   // 필드 아이템 하나 줍기
{
    if (!fi) return;
    if (!inventory) return;

    // ✅ maxDurability까지 같이 전달
    bool ok = inventory.AddItem(
        fi.typeId,                          // 종류 id
        fi.itemId,                          // 아이템 id
        fi.count,                           // 개수
        fi.displayName,                     // 표시 이름
        fi.icon,                            // 아이콘
        fi.durability,                      // 현재 내구도
        fi.maxDurability,                   // ✅ 최대 내구도
        fi.weight                           // 무게
    );

    if (ok)
    {
        // 원래 하던 처리 (사운드/이펙트/FieldItem 제거 등)
        Destroy(fi.gameObject);
    }
}


    // ====== 씬 기즈모(부채꼴/전방) ======
    private void OnDrawGizmosSelected()                    // 선택 시 부채꼴 표시
    {
        if (!origin) origin = transform;
        Vector3 o = origin.position;
        Vector2 fwd = ForwardFromOrigin();

        Gizmos.color = new Color(0.1f, 0.8f, 1f, 0.35f);
        // 경계
        Vector2 left = DirFromAngleDeg(GetForwardDeg(fwd) - halfAngleDeg);
        Vector2 right= DirFromAngleDeg(GetForwardDeg(fwd) + halfAngleDeg);
        Gizmos.DrawLine(o, o + (Vector3)(left * radius));
        Gizmos.DrawLine(o, o + (Vector3)(right* radius));
        // 호
        int seg = 40;
        Vector3 prev = o + (Vector3)(left * radius);
        for (int i=1;i<=seg;i++){
            float t=i/(float)seg;
            float a=Mathf.Lerp(-halfAngleDeg, halfAngleDeg, t);
            Vector2 dir = DirFromAngleDeg(GetForwardDeg(fwd)+a);
            Vector3 next = o + (Vector3)(dir * radius);
            Gizmos.DrawLine(prev, next);
            prev = next;
        }
    }

    private static float GetForwardDeg(Vector2 fwd)        // 전방 벡터→각도(도)
    {
        return Mathf.Atan2(fwd.y, fwd.x) * Mathf.Rad2Deg;
    }

    private static Vector2 DirFromAngleDeg(float deg)      // 각도(도)→단위벡터
    {
        float r = deg * Mathf.Deg2Rad;
        return new Vector2(Mathf.Cos(r), Mathf.Sin(r));
    }
}
