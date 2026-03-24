// StatMetricsManager.cs  (스탯통계매니저)
// ✅ 변경점: (1) 씬 시작 시 자동 섞기 옵션 + Start()에서 ShuffleAll() 호출
//           (2) 외부에서 리스트 변화를 "데이터와 함께" 인식하도록 스냅샷 이벤트 추가
//           (3) ShuffleAll()/SetStat() 시 스냅샷 이벤트 발행

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI; // 버튼 클릭용

[AddComponentMenu("Stats/Stat Metrics Manager (스탯통계매니저)")]
[DisallowMultipleComponent]
public class StatMetricsManager : MonoBehaviour
{
    [Serializable]
    public class StatEntry // 하나의 스탯 데이터 묶음
    {
        [Header("키(매칭에 사용되는 서로 다른 정수 2개)")]
        public int keyPrimary;      // 1차 키(int)  // 스탯 식별용(예: 카테고리) 
        public int keySecondary;    // 2차 키(int)  // 스탯 식별용(예: 세부 ID)

        [Header("스탯 데이터")]
        public string statName;     // 스탯 이름(string)  // UI 표시용
        public int value;           // 현재 스탯 값(int)   // 분자
        public int maxValue = 100;  // 최대 스탯 값(int)   // 분모
    }

    [Header("데이터")]
    [SerializeField] private List<StatEntry> stats = new(); // 스탯 리스트  // A가 보관

    [Header("UI 프리팹(옵션)")]
    public GameObject statUiPrefab; // 스탯 반영 UI 프리팹(StatBindingUI 포함)  // 필요 시 인스턴스화

    [Header("조작 UI")]
    [SerializeField] private Button shuffleButton; // 섞기 버튼(모든 스탯 값을 랜덤으로 변경)

    [Header("시작 옵션")]
    [SerializeField] private bool shuffleOnSceneStart = true; // ✅ 씬 시작 시 섞기 효과를 한 번 적용할지

    public event Action OnStatsChanged; // 스탯 갱신 알림(페이로드 없음)
    public event Action<IReadOnlyList<StatEntry>> OnStatsSnapshotChanged; // ✅ 스냅샷 알림(데이터 포함)

    public bool TryGetStat(int keyA, int keyB, out StatEntry entry) // 키 두 개로 스탯 찾기
    {
        entry = null; // 기본값
        for (int i = 0; i < stats.Count; i++)
        {
            var e = stats[i];
            if (e != null && e.keyPrimary == keyA && e.keySecondary == keyB)
            {
                entry = e;
                return true;
            }
        }
        return false;
    }

    public void SetStat(int keyA, int keyB, string name, int value, int max) // 스탯 추가/갱신
    {
        value = Mathf.Max(0, value);
        max   = Mathf.Max(1, max);

        if (TryGetStat(keyA, keyB, out var e))
        {
            e.statName = name;
            e.value = Mathf.Clamp(value, 0, max);
            e.maxValue = max;
        }
        else
        {
            stats.Add(new StatEntry
            {
                keyPrimary = keyA,
                keySecondary = keyB,
                statName = name,
                value = Mathf.Clamp(value, 0, max),
                maxValue = max
            });
        }

        OnStatsChanged?.Invoke();                         // 변경 알림(기존)
        OnStatsSnapshotChanged?.Invoke(MakeSnapshot());   // ✅ 스냅샷 알림(복사본 전달)
    }

    public IReadOnlyList<StatEntry> GetAllStats() // 전체 스탯 읽기 전용 뷰 제공
    {
        return stats;
    }

    private void OnValidate() // 수치 보정(에디터 변경 시)
    {
        if (stats == null) return;
        foreach (var e in stats)
        {
            if (e == null) continue;
            e.maxValue = Mathf.Max(1, e.maxValue);
            e.value = Mathf.Clamp(e.value, 0, e.maxValue);
        }
    }

    private void Awake() // 버튼 리스너 연결
    {
        if (shuffleButton != null)
        {
            shuffleButton.onClick.RemoveListener(ShuffleAll); // 중복 방지
            shuffleButton.onClick.AddListener(ShuffleAll);    // 클릭 시 섞기
        }
    }

    private void Start() // ✅ 씬 시작 시 섞기 효과 적용(옵션)
    {
        if (shuffleOnSceneStart)
            ShuffleAll(); // 버튼 눌렀을 때와 동일한 효과
    }

    private void OnDestroy() // 리스너 해제
    {
        if (shuffleButton != null)
            shuffleButton.onClick.RemoveListener(ShuffleAll);
    }

    public void ShuffleAll() // 모든 스탯 값을 0~최대 사이 랜덤으로 변경
    {
        if (stats == null || stats.Count == 0)
        {
            OnStatsChanged?.Invoke();
            OnStatsSnapshotChanged?.Invoke(MakeSnapshot()); // ✅ 빈 스냅샷도 전달
            return;
        }

        for (int i = 0; i < stats.Count; i++)
        {
            var e = stats[i];
            if (e == null) continue;

            int max = Mathf.Max(1, e.maxValue);
            e.value = UnityEngine.Random.Range(0, max + 1); // 0~max 포함 랜덤
        }

        OnStatsChanged?.Invoke();                       // 변경 알림(기존)
        OnStatsSnapshotChanged?.Invoke(MakeSnapshot()); // ✅ 스냅샷 알림(복사본 전달)
    }

    private List<StatEntry> MakeSnapshot() // ✅ 외부 전달용 깊은 복사본 생성
    {
        var copy = new List<StatEntry>(stats?.Count ?? 0);
        if (stats != null)
        {
            foreach (var s in stats)
            {
                if (s == null) continue;
                copy.Add(new StatEntry
                {
                    keyPrimary = s.keyPrimary,
                    keySecondary = s.keySecondary,
                    statName = s.statName,
                    value = s.value,
                    maxValue = s.maxValue
                });
            }
        }
        return copy;
    }
}
