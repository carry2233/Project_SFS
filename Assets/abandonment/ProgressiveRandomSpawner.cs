using System.Collections.Generic;                // 리스트 사용
using UnityEngine;                               // 유니티 기본

[AddComponentMenu("Spawner/Progressive Random Spawner")]   // 인스펙터 메뉴 경로
public class ProgressiveRandomSpawner : MonoBehaviour
{
    // ─────────────────────────────────────────────────────────────
    // 활성화 대상
    // ─────────────────────────────────────────────────────────────
    [Header("대기 후 활성화할 대상")]
    [SerializeField] private GameObject targetToActivate;   // ▶ 대기 종료 시 활성화할 오브젝트(초기 비활성 권장)

    // ─────────────────────────────────────────────────────────────
    // 스폰 설정
    // ─────────────────────────────────────────────────────────────
    [Header("스폰 설정")]
    [SerializeField] private GameObject spawnPrefab;        // ▶ 생성할 프리팹
    [SerializeField] private List<Transform> spawnSources = new(); // ▶ 위치를 참조할 트랜스폼 리스트(랜덤 선택)
    [SerializeField] private bool inheritSourceRotation = false;    // ▶ 스폰 시 소스 회전 사용 여부
    [SerializeField] private Transform optionalParent;      // ▶ 생성 결과의 부모(선택)

    // ─────────────────────────────────────────────────────────────
    // 타이밍/가속 설정
    // ─────────────────────────────────────────────────────────────
    [Header("타이밍/가속")]
    [Tooltip("씬 시작 후 스폰 로직을 시작하기 전까지의 대기 시간(초)")]
    [SerializeField] private float initialDelay = 3f;       // ▶ 씬 시작 후 대기 시간

    [Tooltip("초기 생성 주기(초). 시간이 흐르면 감소합니다.")]
    [SerializeField] private float baseInterval = 2f;       // ▶ 시작 주기(초)

    [Tooltip("초마다 생성 주기에서 빠질 값(초/초). 예: 0.1이면 매초 0.1초씩 짧아짐")]
    [SerializeField] private float intervalDecreasePerSecond = 0.1f; // ▶ 주기 감소량(초당)

    [Tooltip("감소하더라도 이 값 아래로 내려가지 않게 하는 하한(안전용)")]
    [SerializeField] private float minInterval = 0.2f;      // ▶ 최소 생성 주기(안전 하한)

    // ─────────────────────────────────────────────────────────────
    // 내부 상태
    // ─────────────────────────────────────────────────────────────
    private float _currentInterval;                         // ▶ 현재 적용 중인 생성 주기
    private float _nextSpawnTime;                           // ▶ 다음 스폰 시각(Time.time 기준)
    private bool _running;                                  // ▶ 스폰 루프 동작 여부

    // ─────────────────────────────────────────────────────────────
    // 유니티 라이프사이클
    // ─────────────────────────────────────────────────────────────
    private void Start()                                    // ▶ 씬 시작 시
    {
        StartCoroutine(RunFlow());                          // ▶ 전체 실행 플로우
    }

    // ─────────────────────────────────────────────────────────────
    // 코루틴/로직
    // ─────────────────────────────────────────────────────────────
    private System.Collections.IEnumerator RunFlow()        // ▶ 전체 실행 플로우 코루틴
    {
        // 1) 시작 대기
        if (initialDelay > 0f)
            yield return new WaitForSeconds(initialDelay);

        // 2) 대기 종료 시 활성화
        if (targetToActivate != null && !targetToActivate.activeSelf)
            targetToActivate.SetActive(true);

        // 3) 스폰 루프 시작 준비
        _currentInterval = Mathf.Max(0.0001f, baseInterval);
        _nextSpawnTime = Time.time + _currentInterval;
        _running = true;

        // 4) 스폰 루프
        while (_running && enabled)
        {
            // 시간이 지남에 따라 주기 감소 (프레임 단위)
            _currentInterval = Mathf.Max(minInterval,
                _currentInterval - intervalDecreasePerSecond * Time.deltaTime);

            // 스폰 타이밍 도달 시 생성
            if (Time.time >= _nextSpawnTime)
            {
                TrySpawnOnce();                             // ▶ 1회 스폰 시도
                _nextSpawnTime = Time.time + _currentInterval;
            }

            yield return null;                              // ▶ 다음 프레임
        }
    }

    private void TrySpawnOnce()                             // ▶ 1회 스폰 시도
    {
        if (spawnPrefab == null) return;
        if (spawnSources == null || spawnSources.Count == 0) return;

        // 유효한 소스만 랜덤 선택
        int guard = 0;                                     // ▶ 무한루프 방지
        Transform selected = null;                         // ▶ 선택된 소스
        while (guard++ < 10 && selected == null)
        {
            var cand = spawnSources[Random.Range(0, spawnSources.Count)];
            if (cand != null) selected = cand;
        }
        if (selected == null) return;

        // 위치/회전 준비
        Vector3 pos = selected.position;                   // ▶ 소스 위치
        Quaternion rot = inheritSourceRotation ? selected.rotation : Quaternion.identity; // ▶ 회전 적용

        // ── 실제 생성 ────────────────────────────────────────────
        GameObject go = Instantiate(spawnPrefab, pos, rot, optionalParent); // ▶ 프리팹 생성(부모 선택 적용)
        if (!go.activeSelf) go.SetActive(true);            // ▶ 프리팹이 Inactive여도 강제 활성화(비활성 생성 방지)

        // ── 생성 직후 ObjectInfo 초기화(요청사항) ───────────────
        // 루트에서 못 찾으면 자식에서라도 찾아서 초기화
        var info = go.GetComponent<ObjectInfo>();
        if (info == null) info = go.GetComponentInChildren<ObjectInfo>(true);

        if (info != null)
        {
            // 1) 출혈량 0으로 초기화(Start 실행 전 프레임에 세팅되어 코루틴 시작 방지)
            info.bleedRate = 0;                             // ▶ 출혈량 초기화

            // 2) 현재체력 = 최대체력으로 세팅
            //    - ObjectInfo의 프로퍼티/메서드 구조에 맞춰 안전하게 호출
            info.SetCurrentHealth(info.MaxHealth);          // ▶ 현재체력 = 최대체력
        }
    }

    // ─────────────────────────────────────────────────────────────
    // 외부 제어(옵션)
    // ─────────────────────────────────────────────────────────────
    public void StopSpawning()                              // ▶ 스폰 중단
    {
        _running = false;
    }

    public void ResumeSpawning()                            // ▶ 스폰 재개(대기 없이)
    {
        if (_running) return;
        _nextSpawnTime = Time.time + _currentInterval;
        _running = true;
        StartCoroutine(RunFlow());
    }
}
