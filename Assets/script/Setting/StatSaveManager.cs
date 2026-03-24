// StatSaveManager.cs  (스탯저장매너져 → 영어: StatSaveManager)
// ✅ DontDestroyOnLoad 로 유지되며, StatMetricsManager 의 스냅샷 이벤트를 구독해
//    "복사-붙여넣기" 방식으로 리스트를 모방(깊은 복사)합니다.
//    스크립트 참조는 여기(A)에서만 매니저를 바라봅니다(단방향).

using System.Collections.Generic;
using UnityEngine;


[AddComponentMenu("Stats/Stat Save Manager (스탯저장매너져)")]
[DisallowMultipleComponent]
public class StatSaveManager : MonoBehaviour
{
    [Header("싱글턴")]
    public static StatSaveManager Instance;                 // 싱글턴 인스턴스

    [Header("저장 데이터(깊은 복사본)")]
    [SerializeField] private List<MirrorEntry> savedStats = new(); // 모방 리스트

    [System.Serializable]
    public class MirrorEntry // 외부 저장용 미러 구조체(필드 값만 복사)
    {
        public int keyPrimary;       // 1차 키(int)
        public int keySecondary;     // 2차 키(int)
        public string statName;      // 스탯 이름
        public int value;            // 현재 값
        public int maxValue = 100;   // 최대 값

        public MirrorEntry DeepCopy() // ✅ 깊은 복사 메서드(추가)
            {
        return new MirrorEntry
        {
            keyPrimary = keyPrimary,
            keySecondary = keySecondary,
            statName = statName,
            value = value,
            maxValue = maxValue
        };
            }
    }

    private StatMetricsManager _source;                     // 데이터 소스(단방향 참조)

    private void Awake() // 싱글턴 + DontDestroyOnLoad
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject); // ✅ 씬 전환에도 유지
    }

    private void OnEnable() // 소스 탐색/구독
    {
        HookToSource();     // 소스 찾고 이벤트 구독
        TryInitialSync();   // 시작 시 한 번 동기화(씬 시작시 섞기 반영 포함)
    }

    private void OnDisable() // 구독 해제
    {
        if (_source != null)
            _source.OnStatsSnapshotChanged -= OnSourceSnapshotChanged;
    }

    private void HookToSource() // 소스 매니저 찾기 + 이벤트 구독
    {
        // 이미 연결되어 있으면 패스
        if (_source != null) return;

        _source = FindObjectOfType<StatMetricsManager>(); // 씬에서 탐색
        if (_source != null)
        {
            // 스냅샷 이벤트 구독(값이 바뀔 때마다 깊은 복사로 저장)
            _source.OnStatsSnapshotChanged += OnSourceSnapshotChanged;
        }
    }

    private void TryInitialSync() // 초기 동기화(씬 시작 시 상태 수집)
    {
        if (_source == null) _source = FindObjectOfType<StatMetricsManager>();
        if (_source == null) return;

        // 현재 상태 스냅샷을 직접 요청(직접 메서드 없으니 GetAllStats()로 복사)
        CopyFromReadOnlyList(_source.GetAllStats()); // 현재 상태를 그대로 모방
    }

    private void OnSourceSnapshotChanged(IReadOnlyList<StatMetricsManager.StatEntry> snapshot) // 🔔 소스 변경 수신
    {
        CopyFromReadOnlyList(snapshot); // 전달받은 스냅샷으로 완전 덮어쓰기
    }

    private void CopyFromReadOnlyList(IReadOnlyList<StatMetricsManager.StatEntry> list) // 깊은 복사 수행
    {
        savedStats ??= new List<MirrorEntry>();
        savedStats.Clear();

        if (list != null)
        {
            for (int i = 0; i < list.Count; i++)
            {
                var s = list[i];
                if (s == null) continue;
                savedStats.Add(new MirrorEntry
                {
                    keyPrimary   = s.keyPrimary,    // 키 복사
                    keySecondary = s.keySecondary,  // 키 복사
                    statName     = s.statName,      // 이름 복사
                    value        = s.value,         // 값 복사
                    maxValue     = s.maxValue       // 최대값 복사
                });
            }
        }

        // (옵션) 디버그 로그
        // Debug.Log($"[StatSaveManager] 스냅샷 갱신 완료: {savedStats.Count}개 항목 저장.");
    }

    // ===== 외부 접근 API =====

    public IReadOnlyList<MirrorEntry> GetSavedStats() // 읽기 전용 뷰
    {
        return savedStats;
    }

    // StatSaveManager.cs
// StatSaveManager.cs (추가)
public IReadOnlyList<MirrorEntry> GetSavedStatList()
{
    return savedStats; // 내부 리스트 직접 수정 불가
}



    public bool TryGetSavedStat(int keyA, int keyB, out MirrorEntry entry) // 키로 조회
    {
        entry = null;
        if (savedStats == null) return false;

        for (int i = 0; i < savedStats.Count; i++)
        {
            var e = savedStats[i];
            if (e != null && e.keyPrimary == keyA && e.keySecondary == keyB)
            {
                entry = e;
                return true;
            }
        }
        return false;
    }
}
