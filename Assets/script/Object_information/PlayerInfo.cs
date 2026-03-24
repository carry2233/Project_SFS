// PlayerInfo.cs — (변경) 시작/변경 감지 시 WearState → ObjectInfo.wearDefenses '복사-붙여넣기' 동기화
using System.Collections.Generic;  // List
using TMPro;                       // TMP
using UnityEngine;                 // Unity
using UnityEngine.SceneManagement; // Scene load

[AddComponentMenu("Stats/Player Info (플레이어정보)")]
[DisallowMultipleComponent]
public class PlayerInfo : MonoBehaviour
{
    [System.Serializable]
    public class StatusRule
    {
        [Range(0,100)] public int applyWhenPercentLessOrEqual = 30; // 반영 조건 퍼센트(이 값 이하 시 적용)
        public string displayText = "LOW";                           // 반영할 문자열
        public Color textColor = Color.red;                          // 상태 텍스트 색상
    }

    [Header("참조")]
    public ObjectInfo target;                            // 참조할 '객체정보'
    public ApparelWearStateRegistry wearStateRegistry;   // ✅ 착용 상태 레지스트리(복사 원본)

    [Header("UI")]
    public TextMeshProUGUI statusText;                   // 상태 표시용 TMP 텍스트

    [Header("규칙 리스트(위에서부터 우선)")]
    public StatusRule[] rules;                           // 퍼센트 조건, 표시 문자열, 색

    [Header("기본 표시(규칙 미적용 시)")]
    public string defaultText = "OK";                    // 기본 문자열
    public Color defaultColor = Color.white;             // 기본 색상

    [Header("사망 시 씬 전환")]
    public string loadSceneOnDeath = "";                 // 사망 시 로드할 씬 이름(빈 값이면 무시)
    private bool _sceneLoadedOnDeath;                    // 중복 로드 방지 플래그

    // 내부 버퍼(할당비용 감소용)
    private readonly List<ApparelWearStateRegistry.DefenseSnapshot> _exportBuffer = new(); // 내보낸 스냅샷 임시 버퍼

    private void Reset() // 기본 참조 자동 할당 시도
    {
        if (!target) target = GetComponentInParent<ObjectInfo>();     // 부모에서 탐색
        if (!wearStateRegistry) wearStateRegistry = FindObjectOfType<ApparelWearStateRegistry>(); // 씬에서 탐색
    }

    private void OnEnable() // 구독 및 즉시 갱신
    {
        TrySubscribeTarget();
        TrySubscribeWearState();
        ForceRefresh();                       // 현재 수치로 즉시 갱신
        SyncWearDefenseFromRegistry();        // ✅ 시작 시 1회 복사-붙여넣기
    }

    private void OnDisable() // 구독 해제
    {
        TryUnsubscribeTarget();
        TryUnsubscribeWearState();
    }

    private void OnValidate() // 에디터에서 참조 바뀔 때도 안전하게 갱신
    {
        TryUnsubscribeTarget();
        TryUnsubscribeWearState();
        TrySubscribeTarget();
        TrySubscribeWearState();
        ForceRefresh();                // UI 즉시 갱신
        SyncWearDefenseFromRegistry(); // 에디터에서도 즉시 반영(테스트 편의)
    }

    // ─────────────────────────────────────────────
    // 구독/해제
    // ─────────────────────────────────────────────
    private void TrySubscribeTarget() // 대상 구독
    {
        if (!target) return;
        target.OnHealthChanged += OnHealthChanged;
        target.OnDied += OnTargetDied;
    }

    private void TryUnsubscribeTarget() // 대상 구독 해제
    {
        if (!target) return;
        target.OnHealthChanged -= OnHealthChanged;
        target.OnDied -= OnTargetDied;
    }

    private void TrySubscribeWearState() // ✅ 레지스트리 변경 감지 구독
    {
        if (!wearStateRegistry) return;
        wearStateRegistry.OnChanged += OnWearStateChanged; // 변경 감지 시 동기화
    }

    private void TryUnsubscribeWearState() // 레지스트리 구독 해제
    {
        if (!wearStateRegistry) return;
        wearStateRegistry.OnChanged -= OnWearStateChanged;
    }

    // ─────────────────────────────────────────────
    // 콜백
    // ─────────────────────────────────────────────
    private void OnHealthChanged(int cur, int max, int percent) // 체력 이벤트 콜백
    {
        ApplyByPercent(percent);
    }

    private void OnTargetDied() // 대상 사망 시
    {
        if (_sceneLoadedOnDeath) return;
        if (string.IsNullOrWhiteSpace(loadSceneOnDeath)) return;

        _sceneLoadedOnDeath = true;
        SceneManager.LoadScene(loadSceneOnDeath); // 사망 시 지정 씬 로드
    }

    private void OnWearStateChanged() // ✅ 착용 레지스트리 변경 이벤트 콜백
    {
        SyncWearDefenseFromRegistry(); // 변경 시마다 즉시 복사-붙여넣기
    }

    // ─────────────────────────────────────────────
    // UI 갱신
    // ─────────────────────────────────────────────
    private void ForceRefresh() // 강제 갱신(현재 상태 그대로 UI 반영)
    {
        if (!target)
        {
            ApplyDefault();
            return;
        }
        ApplyByPercent(target.HealthPercent);
    }

    private void ApplyByPercent(int percent) // 퍼센트에 맞춰 UI 반영
    {
        if (!statusText) return;

        if (rules != null && rules.Length > 0)
        {
            for (int i = 0; i < rules.Length; i++)
            {
                var r = rules[i];
                if (r == null) continue;
                if (percent <= r.applyWhenPercentLessOrEqual)
                {
                    statusText.text = string.IsNullOrWhiteSpace(r.displayText) ? defaultText : r.displayText; // 텍스트(규칙)
                    statusText.color = r.textColor; // 색상(규칙)
                    return;
                }
            }
        }

        ApplyDefault(); // 규칙 미적용 시 기본 표시
    }

    private void ApplyDefault() // 기본 표시 적용
    {
        if (!statusText) return;
        statusText.text = defaultText;
        statusText.color = defaultColor;
    }

    // ─────────────────────────────────────────────
    // ✅ 착용 방어 스냅샷 동기화(복사-붙여넣기)
    // ─────────────────────────────────────────────
    private void SyncWearDefenseFromRegistry() // WearState → ObjectInfo.wearDefenses
    {
        if (!target) return;

        // 원본이 없으면 대상의 방어 스냅샷을 비움(방어 없음 상태)
        if (!wearStateRegistry)
        {
            if (target.wearDefenses != null) target.wearDefenses.Clear();
            return;
        }

        // 내보내기 호출
        wearStateRegistry.ExportCurrentDefense(_exportBuffer);

        // 대상 리스트에 그대로 복사-붙여넣기
        if (target.wearDefenses == null) target.wearDefenses = new List<ObjectInfo.WearDefenseEntry>();
        else target.wearDefenses.Clear();

        foreach (var s in _exportBuffer)
        {
            target.wearDefenses.Add(new ObjectInfo.WearDefenseEntry
            {
                wearSlotTier    = s.wearSlotTier,
                wearSlot        = s.wearSlot,
                absoluteDefense = s.absoluteDefense,
                defenseRate     = Mathf.Clamp(s.defenseRate, 0, 100)
            });
        }
    }
}
