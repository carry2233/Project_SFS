// StatBindingUI.cs  (스탯반영ui)
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[AddComponentMenu("Stats/Stat Binding UI (스탯반영ui)")]
[DisallowMultipleComponent]
public class StatBindingUI : MonoBehaviour
{
    [Header("담당 키(서로 다른 정수 2개)")]
    public int keyPrimary;                   // 1차 키(int)  // 자신이 표시할 스탯 식별
    public int keySecondary;                 // 2차 키(int)  // 자신이 표시할 스탯 식별

    [Header("UI 참조")]
    public TextMeshProUGUI nameText;         // 스탯 이름 표시 TMP  // e.statName 반영
    public TextMeshProUGUI valueText;        // 값 표시 TMP        // "현재/최대" 형태
    public Slider valueSlider;               // 슬라이더           // 현재/최대 비율

    [Header("데이터 소스")]
    public StatMetricsManager manager;       // 스탯통계매니저 참조 // 없으면 자동 탐색 시도

    private void Reset() // 기본 참조 자동 할당 시도
    {
        if (!manager) manager = FindObjectOfType<StatMetricsManager>(); // 씬에서 탐색
    }

    private void OnEnable() // 구독 및 즉시 갱신
    {
        if (!manager) manager = FindObjectOfType<StatMetricsManager>();
        if (manager != null)
            manager.OnStatsChanged += Refresh; // 스탯 변경 시 UI 갱신

        Refresh(); // 최초 1회 반영
    }

    private void OnDisable() // 구독 해제
    {
        if (manager != null)
            manager.OnStatsChanged -= Refresh;
    }

    public void Refresh() // 현재 키에 해당하는 스탯을 찾아 UI에 반영
    {
        if (!manager)
        {
            ApplyEmpty();
            return;
        }

        if (manager.TryGetStat(keyPrimary, keySecondary, out var e) && e != null)
        {
            if (nameText)  nameText.text  = string.IsNullOrWhiteSpace(e.statName) ? "Stat" : e.statName; // 이름 반영
            if (valueText) valueText.text = $"{e.value}/{e.maxValue}";                                    // 값 텍스트

            if (valueSlider)
            {
                valueSlider.minValue = 0f;
                valueSlider.maxValue = 1f;
                float ratio = (e.maxValue > 0) ? (e.value / (float)e.maxValue) : 0f;
                valueSlider.value = Mathf.Clamp01(ratio); // 슬라이더 비율
            }
        }
        else
        {
            ApplyEmpty();
        }
    }

    private void ApplyEmpty() // 매칭 실패 시 기본 표시
    {
        if (nameText)  nameText.text  = "-";
        if (valueText) valueText.text = "0/0";
        if (valueSlider)
        {
            valueSlider.minValue = 0f;
            valueSlider.maxValue = 1f;
            valueSlider.value    = 0f;
        }
    }
}
