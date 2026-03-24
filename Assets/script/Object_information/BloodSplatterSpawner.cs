using System.Collections.Generic;                         // 리스트/딕셔너리
using UnityEngine;                                        // 유니티 기본

[AddComponentMenu("VFX/Blood Splatter Spawner")]          // 인스펙터 메뉴
public class BloodSplatterSpawner : MonoBehaviour
{
    [Header("풀/프리팹 설정")]
    [SerializeField] private Transform poolParent;         // ✅ 혈흔 인스턴스가 붙을 부모(풀 컨테이너)
    [SerializeField] private List<GameObject> bloodPrefabs = new(); // ✅ 혈흔 프리팹 후보 리스트

    [Header("스폰 기준/반경")]
    [SerializeField] private Transform spawnOrigin;        // ✅ 기준 위치(예: 발밑 Empty)
    [SerializeField, Min(0f)] private float spawnRadius = 0.5f; // ✅ 원형 랜덤 반경

    [Header("생명주기(혈흔)")]
    [SerializeField, Min(0f)] private float minKeepSeconds = 2f;    // ✅ 최소 유지 시간
    [SerializeField, Min(0f)] private float maxKeepSeconds = 4f;    // ✅ 최대 유지 시간
    [SerializeField, Min(0f)] private float fadeOutSeconds = 0.8f;  // ✅ 페이드 소요 시간
    [SerializeField] private AnimationCurve fadeCurve = null;        // ✅ 페이드 곡선(선택)

    [Header("회전 랜덤")]
    [SerializeField] private bool randomizeZRotation = true; // ✅ Z축 회전 랜덤 사용 여부
    [SerializeField] private Vector2 zRotationRange = new Vector2(0f, 360f); // ✅ Z회전 범위(도)

    [Header("시체 스폰(사망시)")]
    [SerializeField] private Transform corpseParent;       // ✅ 시체 전용 부모(반드시 이 밑에 생성)

    [Header("기즈모")]
    [SerializeField] private bool showGizmo = true;        // ✅ 씬 뷰에 스폰 반경 원 표시
    [SerializeField] private Color gizmoColor = new Color(0.8f, 0f, 0f, 0.35f); // ✅ 원 색

    // 프리팹별 서브 풀 컨테이너(안전한 재사용을 위함)
    private readonly Dictionary<GameObject, Transform> _subPools = new(); // ✅ 프리팹→서브풀 매핑

    private void Reset()                                    // 🔧 자동 할당 보조
    {
        if (!poolParent) poolParent = transform;
        if (!spawnOrigin) spawnOrigin = transform;
        // corpseParent는 “시체 부모용 오브젝트”를 외부에서 명시적으로 지정 권장
    }

    public void Spawn()                                     // ▶ 원 내부 랜덤 위치 + Z랜덤 회전으로 혈흔 스폰
    {
        if (!spawnOrigin || bloodPrefabs.Count == 0 || !poolParent) return;

        // 1) 프리팹 랜덤 선택
        var prefab = bloodPrefabs[Random.Range(0, bloodPrefabs.Count)];
        if (!prefab) return;

        // 2) 프리팹별 서브 풀 컨테이너 확보
        var subPool = GetOrCreateSubPool(prefab);

        // 3) 풀에서 대여(예약 플래그 방식)
        var go = RentFromPool(subPool, prefab);
        if (!go) return;

        // 4) 위치/회전 설정(반경 랜덤 + Z랜덤)
        Vector2 rnd = Random.insideUnitCircle * spawnRadius;
        Vector3 pos = spawnOrigin.position + new Vector3(rnd.x, rnd.y, 0f);
        float z = randomizeZRotation ? Random.Range(zRotationRange.x, zRotationRange.y) : 0f;
        go.transform.SetPositionAndRotation(pos, Quaternion.Euler(0f, 0f, z));

        // 5) 활성화 + 라이프사이클 시작
        var item = go.GetComponent<BloodPoolItem>();
        if (item) item.PrepareForReuse();
        go.SetActive(true);
        if (item)
        {
            float keep = Random.Range(minKeepSeconds, Mathf.Max(minKeepSeconds, maxKeepSeconds));
            item.BeginLifecycle(keep, fadeOutSeconds, fadeCurve);
        }
    }

    public void SpawnAt(Vector3 worldPos, bool randomZ = true) // ▶ 지정 위치에 혈흔 스폰(옵션: Z랜덤)
    {
        if (bloodPrefabs.Count == 0 || !poolParent) return;

        var prefab = bloodPrefabs[Random.Range(0, bloodPrefabs.Count)];
        if (!prefab) return;

        var subPool = GetOrCreateSubPool(prefab);
        var go = RentFromPool(subPool, prefab);
        if (!go) return;

        float z = (randomZ && randomizeZRotation) ? Random.Range(zRotationRange.x, zRotationRange.y) : 0f;
        go.transform.SetPositionAndRotation(worldPos, Quaternion.Euler(0f, 0f, z));

        var item = go.GetComponent<BloodPoolItem>();
        if (item) item.PrepareForReuse();
        go.SetActive(true);
        if (item)
        {
            float keep = Random.Range(minKeepSeconds, Mathf.Max(minKeepSeconds, maxKeepSeconds));
            item.BeginLifecycle(keep, fadeOutSeconds, fadeCurve);
        }
    }

    public void SpawnCorpseAt(Vector3 worldPos, GameObject corpsePrefab) // ▶ 시체 스폰(시체 부모의 자식)
    {
        if (!corpsePrefab) return;
        if (!corpseParent)
        {
            Debug.LogWarning("[BloodSplatterSpawner] corpseParent가 비어 있습니다. 시체를 생성하지 않습니다.");
            return;
        }

        var corpse = Instantiate(corpsePrefab, corpseParent);   // 시체 부모 밑에 생성
        corpse.transform.SetPositionAndRotation(worldPos, corpsePrefab.transform.rotation);
        corpse.SetActive(true);
    }

    // ───────────────── 내부: 프리팹별 서브풀/렌트 ─────────────────

    private Transform GetOrCreateSubPool(GameObject prefab)     // ▶ 프리팹별 서브 풀 Transform 반환/생성
    {
        if (_subPools.TryGetValue(prefab, out var t) && t) return t;

        var holder = new GameObject($"Pool_{prefab.name}");     // 서브풀 홀더 생성
        holder.transform.SetParent(poolParent, false);
        holder.transform.localPosition = Vector3.zero;
        holder.transform.localRotation = Quaternion.identity;
        holder.transform.localScale = Vector3.one;

        _subPools[prefab] = holder.transform;
        return holder.transform;
    }

    private GameObject RentFromPool(Transform subPool, GameObject prefab) // ▶ 서브풀에서 대여(예약 플래그)
    {
        // 비활성 + inUse=false 항목 탐색(발견 즉시 선점)
        for (int i = 0; i < subPool.childCount; i++)
        {
            var child = subPool.GetChild(i).gameObject;
            if (!child.activeSelf)
            {
                var item = child.GetComponent<BloodPoolItem>();
                if (item != null && !item.inUse)
                {
                    item.inUse = true;                         // 예약(선점)
                    return child;
                }
            }
        }
        // 없으면 새로 생성 후 예약
        var go = Instantiate(prefab, subPool);
        var createdItem = go.GetComponent<BloodPoolItem>();
        if (createdItem) createdItem.inUse = true;             // 예약
        go.SetActive(false);
        return go;
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()                         // 🧭 원형 반경 기즈모
    {
        if (!showGizmo) return;
        var origin = spawnOrigin ? spawnOrigin.position : transform.position;
        UnityEditor.Handles.color = gizmoColor;
        UnityEditor.Handles.DrawSolidDisc(origin, Vector3.forward, spawnRadius);
    }
#endif
}
