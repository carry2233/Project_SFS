using System.Collections.Generic;                       // List 사용
using UnityEngine;                                      // Unity 기본
using UnityEngine.Rendering.Universal;                  // Light2D (URP 2D)

public class TimeSystem : MonoBehaviour
{
    [Header("라이트(리스트1)")]
    [SerializeField] private List<Light2D> lights = new();        // ✅ 리스트1: 강도 적용 대상 Light2D 목록

    [Header("강도 설정(씬 시작 시 1회 적용)")]
    [SerializeField] private float baseIntensity = 1f;            // ✅ 기본강도값(기본: 1)
    [SerializeField] private float intensityMultiplier = 1f;      // ✅ 강도값배율
    [SerializeField] private float finalIntensity = 1f;           // ✅ 최종강도값(= 기본 * 배율)

    [Header("시간 설정")]
    [SerializeField] private int maxDayHours = 24;                // ✅ 최대하루시간(정수, 기본: 24)
    [SerializeField] private int currentHour = 7;                 // ✅ 현재하루시간(정수, 기본: 7)
    [SerializeField] private int currentMinute = 30;              // ✅ 현재 분(정수, 기본: 30)
    [SerializeField] private int secondsPerSecond = 60;           // ✅ 초당 지나가는 시간(정수, 초단위, 기본: 60=1분)

    [Header("현재 상태")]
    [SerializeField] private DayPhase currentPhase = DayPhase.Morning; // ✅ 현재 새벽/아침/낮/저녁/밤 상태
    [SerializeField] private int currentDay = 1;                  // ✅ 현재 일자(정수, 하루가 0시로 넘어갈 때 +1)

    [Header("시간대 기준(실수: H.MM 형식, 예: 0.25=0시25분)")]
    [SerializeField] private float dawnStart = 0.00f;             // ✅ 새벽 시작(기본: 0.00)
    [SerializeField] private float morningStart = 5.00f;          // ✅ 아침 시작(기본: 5.00)
    [SerializeField] private float dayStart = 10.00f;             // ✅ 낮 시작(기본: 10.00)
    [SerializeField] private float eveningStart = 17.00f;         // ✅ 저녁 시작(기본: 17.00)
    [SerializeField] private float nightStart = 21.00f;           // ✅ 밤 시작(기본: 21.00)

    [Header("시간대별 강도 배율(시간 변화에 따라 적용)")]
[SerializeField] private float dawnMultiplier = 0.6f;          // ✅ 새벽 배율
[SerializeField] private float morningMultiplier = 0.9f;       // ✅ 아침 배율
[SerializeField] private float dayMultiplier = 1.2f;           // ✅ 낮 배율
[SerializeField] private float eveningMultiplier = 0.8f;       // ✅ 저녁 배율
[SerializeField] private float nightMultiplier = 0.4f;         // ✅ 밤 배율

[Header("배율 변화(부드러운 전환)")]
[SerializeField] private float currentMultiplier = 1f;           // ✅ 현재 시각 기반 배율(매 프레임 계산)

private float lastAppliedFinalIntensity = float.NaN;             // ✅ 마지막으로 적용한 최종강도(변화 감지)

private const float ApplyEpsilon = 0.0001f;                      // ✅ 변경 감지용 임계값(기존 유지)

private struct LightKeyframe                                     // ✅ (시간, 배율) 키프레임 구조
{
    public int timeMin;                                          // 키프레임 시간(총 분)
    public float mul;                                            // 키프레임 배율
    public LightKeyframe(int t, float m) { timeMin = t; mul = m; } // 생성자
}



    private float accumulatedGameSeconds = 0f;                    // ✅ 내부 누적 게임 초(분/시 변환용)

    public enum DayPhase                                          // ✅ 시간대 열거형
    {
        Dawn,                                                     // 새벽
        Morning,                                                  // 아침
        Day,                                                      // 낮
        Evening,                                                  // 저녁
        Night                                                     // 밤
    }

private void Start()                                             // ✅ 씬 시작 초기화
{
    ClampAndNormalizeTime();                                     // 시간값 보정
    UpdateDayPhase();                                            // 현재 시간대 상태 계산(표시용)

    currentMultiplier = EvaluateMultiplierByTime();              // ✅ 현재 시각 기반 배율 계산
    ApplyLightIntensity();                                       // ✅ 라이트 강도 적용
}



private void Update()                                            // ✅ 시간 진행 + 라이트 연속 변화
{
    TickTime();                                                  // 초→분→시→일 진행
    UpdateDayPhase();                                            // 시간대 상태 갱신(표시용)

    float before = currentMultiplier;                            // 이전 배율
    currentMultiplier = EvaluateMultiplierByTime();              // ✅ 현재 시각 기반 배율(구간 보간)

    if (Mathf.Abs(currentMultiplier - before) > ApplyEpsilon)    // ✅ 배율이 실제로 바뀐 경우만
        ApplyLightIntensity();                                   // 라이트 갱신
}



    private void ApplyInitialLightIntensity()                     // ✅ (요청사항) 씬 시작 시 강도 1회 적용
    {
        finalIntensity = baseIntensity * intensityMultiplier;     // 최종강도값 계산

        if (lights == null) return;                               // 가드
        for (int i = 0; i < lights.Count; i++)                    // 리스트1 순회
        {
            var l = lights[i];                                    // 라이트 참조
            if (l == null) continue;                              // 빈 칸 스킵
            l.intensity = finalIntensity;                         // ✅ 강도 적용(변화 없음: 여기서 끝)
        }
    }

    private void TickTime()                                       // ✅ 시간 진행 처리(초 → 분 → 시 → 일)
    {
        float add = Time.deltaTime * Mathf.Max(0, secondsPerSecond); // 이번 프레임에 추가할 게임 초
        accumulatedGameSeconds += add;                            // 누적

        if (accumulatedGameSeconds < 60f) return;                 // 60초(=1분) 미만이면 종료

        int addMinutes = Mathf.FloorToInt(accumulatedGameSeconds / 60f); // 증가할 분
        accumulatedGameSeconds = accumulatedGameSeconds % 60f;    // 남은 초 유지

        currentMinute += addMinutes;                              // 분 증가

        if (currentMinute >= 60)                                  // 분 → 시 승급
        {
            int addHours = currentMinute / 60;                    // 증가할 시
            currentMinute = currentMinute % 60;                   // 분 정리
            currentHour += addHours;                              // 시 증가
        }

        if (currentHour >= maxDayHours)                           // 시 → 일 승급(0시로 래핑)
        {
            int addDays = currentHour / Mathf.Max(1, maxDayHours); // 넘어간 일 수
            currentHour = currentHour % Mathf.Max(1, maxDayHours); // 0~(max-1)로 정리
            currentDay += addDays;                                // 일자 증가
        }
    }

private void UpdateDayPhase()                                    // ✅ 시간대 상태 판정(표시용)
{
    int nowTotalMin = currentHour * 60 + currentMinute;          // 현재 총 분(상태 판정은 분 단위면 충분)

    int dawnMin = ToTotalMinutes_HDotMM(dawnStart);              
    int morningMin = ToTotalMinutes_HDotMM(morningStart);
    int dayMin = ToTotalMinutes_HDotMM(dayStart);
    int eveningMin = ToTotalMinutes_HDotMM(eveningStart);
    int nightMin = ToTotalMinutes_HDotMM(nightStart);
    int endMin = Mathf.Max(1, maxDayHours) * 60;

    DayPhase next;

    if (nowTotalMin >= nightMin && nowTotalMin < endMin) next = DayPhase.Night;
    else if (nowTotalMin >= eveningMin && nowTotalMin < nightMin) next = DayPhase.Evening;
    else if (nowTotalMin >= dayMin && nowTotalMin < eveningMin) next = DayPhase.Day;
    else if (nowTotalMin >= morningMin && nowTotalMin < dayMin) next = DayPhase.Morning;
    else next = DayPhase.Dawn;

    currentPhase = next;                                         // ✅ 상태만 갱신(라이트 목표 갱신은 여기서 안 함)
}



    private int ToTotalMinutes_HDotMM(float timeValue)            // ✅ 실수(H.MM)를 총 분으로 변환(예: 0.25=0시25분)
    {
        int h = Mathf.FloorToInt(timeValue);                      // 시(정수)
        int m = Mathf.RoundToInt((timeValue - h) * 100f);         // 분(00~59)

        if (m < 0) m = 0;                                         // 방어
        if (m > 59) m = 59;                                       // 방어(H.75 같은 입력 방지)

        int safeMaxH = Mathf.Max(1, maxDayHours);                 // 0 방지
        if (h < 0) h = 0;                                         // 방어
        if (h >= safeMaxH) h = safeMaxH - 1;                      // 방어

        return h * 60 + m;                                        // 총 분
    }

    private void ClampAndNormalizeTime()                          // ✅ 시작값 보정(음수/범위 초과 방지)
    {
        maxDayHours = Mathf.Max(1, maxDayHours);                  // 최소 1
        currentHour = Mathf.Clamp(currentHour, 0, maxDayHours - 1); // 0~max-1
        currentMinute = Mathf.Clamp(currentMinute, 0, 59);        // 0~59
        secondsPerSecond = Mathf.Max(0, secondsPerSecond);        // 음수 방지
        currentDay = Mathf.Max(1, currentDay);                    // 1일부터
    }

private void ApplyLightIntensity()                               // ✅ 라이트 강도 적용
{
    finalIntensity = baseIntensity * intensityMultiplier * currentMultiplier; // ✅ 기본 * (기존배율) * (시간기반배율)

    if (!float.IsNaN(lastAppliedFinalIntensity) &&
        Mathf.Abs(finalIntensity - lastAppliedFinalIntensity) <= ApplyEpsilon) // ✅ 변화 없으면 스킵
        return;

    lastAppliedFinalIntensity = finalIntensity;                  // 마지막 적용값 갱신

    if (lights == null) return;                                  // 가드
    for (int i = 0; i < lights.Count; i++)                       // 리스트1 순회
    {
        var l = lights[i];                                       // 라이트 참조
        if (l == null) continue;                                 // 빈 칸 스킵
        l.intensity = finalIntensity;                            // ✅ 강도 적용
    }
}

private float EvaluateMultiplierByTime()                         // ✅ 하루 시간에 따라 배율을 ‘계속’ 보간 계산
{
    int dayEndMin = Mathf.Max(1, maxDayHours) * 60;              // 하루 끝(총 분)

    // 현재 시각(총 분, 소수 포함: accumulatedGameSeconds로 분의 소수 반영)
    float nowMin = (currentHour * 60) + currentMinute + (accumulatedGameSeconds / 60f);

    // 키프레임(시간 시작점들 + 해당 배율)
    var keys = new List<LightKeyframe>(5)                        // 키프레임 리스트
    {
        new LightKeyframe(ToTotalMinutes_HDotMM(dawnStart), dawnMultiplier),       // 새벽 키
        new LightKeyframe(ToTotalMinutes_HDotMM(morningStart), morningMultiplier), // 아침 키
        new LightKeyframe(ToTotalMinutes_HDotMM(dayStart), dayMultiplier),         // 낮 키
        new LightKeyframe(ToTotalMinutes_HDotMM(eveningStart), eveningMultiplier), // 저녁 키
        new LightKeyframe(ToTotalMinutes_HDotMM(nightStart), nightMultiplier)      // 밤 키
    };

    // 시간 순 정렬
    keys.Sort((a, b) => a.timeMin.CompareTo(b.timeMin));         // 시간 오름차순 정렬

    // now가 첫 키프레임보다 앞이면(예: dawnStart가 0이 아닐 때) → 다음날로 넘겨서 처리
    if (nowMin < keys[0].timeMin)                                // 랩어라운드 보정
        nowMin += dayEndMin;                                     // 하루 길이만큼 더해 다음날로 취급

    // 구간 찾기: keys[i] ~ keys[i+1] 사이를 Lerp
    for (int i = 0; i < keys.Count; i++)                         // 구간 탐색
    {
        int t0 = keys[i].timeMin;                                // 구간 시작 시간
        int t1 = (i == keys.Count - 1)                           // 구간 끝 시간(마지막이면 첫 키 + 하루)
            ? keys[0].timeMin + dayEndMin
            : keys[i + 1].timeMin;

        float m0 = keys[i].mul;                                  // 시작 배율
        float m1 = (i == keys.Count - 1)                         // 끝 배율(마지막이면 첫 키 배율로 연결)
            ? keys[0].mul
            : keys[i + 1].mul;

        if (nowMin >= t0 && nowMin < t1)                         // 현재 시각이 이 구간이면
        {
            float u = (t1 == t0) ? 1f : (nowMin - t0) / (t1 - t0); // 진행률(0~1)
            return Mathf.Lerp(m0, m1, u);                         // ✅ 시간 기반 연속 보간 배율
        }
    }

    return keys[0].mul;                                          // fallback
}


}
