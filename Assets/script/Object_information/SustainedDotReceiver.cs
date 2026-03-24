using System;
using System.Collections.Generic;                     // 딕셔너리/리스트
using UnityEngine;                                    // 유니티 기본

[AddComponentMenu("Combat/Sustained DOT Receiver")]   // 인스펙터 메뉴
public class SustainedDotReceiver : MonoBehaviour
{
    // ─────────────────────────────────────────────────────────────────────────────
    // 데이터 구조
    // ─────────────────────────────────────────────────────────────────────────────

    [Serializable]
    public struct DotKey
    {
        public int typeId;                            // ✅ 지속피해종류ID
        public int checkId;                           // ✅ 판별ID

        public DotKey(int t, int c) { typeId = t; checkId = c; }

        public override int GetHashCode()             // ▶ 딕셔너리 키 해시
        {
            unchecked { return (typeId * 486187739) ^ checkId; }
        }
        public override bool Equals(object obj)       // ▶ 동등성 비교
        {
            if (obj is DotKey other) return other.typeId == typeId && other.checkId == checkId;
            return false;
        }
    }

    [Serializable]
    public class FlashConfig
    {
        public Color targetColor = new Color(1f, 0.4f, 0.1f, 1f); // ✅ 목표 색상(예: 화상 주황)
        [Min(0f)] public float upTime = 0.08f;          // ✅ 색상 상승 시간
        [Min(0f)] public float holdTime = 0.12f;        // ✅ 색상 유지 시간
        [Min(0f)] public float downTime = 0.25f;        // ✅ 색상 하강 시간
        public AnimationCurve upCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);   // ✅ 상승 곡선
        public AnimationCurve downCurve = AnimationCurve.EaseInOut(0, 0, 1, 1); // ✅ 하강 곡선
    }

    [Serializable]
    public class DotSlot
    {
        [Header("키")]
        public int typeId;                            // ✅ 지속피해종류ID
        public int checkId;                           // ✅ 판별ID

        [Header("색 변화(플래시)")]
        public FlashConfig flash = new FlashConfig(); // ✅ 색 변화 설정

        [Header("지속피해 수치")]
        [Min(0.05f)] public float tickInterval = 1f;  // ✅ 피해 적용 주기(초)
        public int damagePerTick = 5;                 // ✅ 피해량(정수)
        [Range(0, 100)] public int resistancePercent; // ✅ 피해 내성%(0~100)

        [Header("지속시간/카운터")]
        [Min(0f)] public float counterSeconds = 3f;   // ✅ 카운터 시간(지속시간)
    }

    private class ActiveState
    {
        public DotSlot slot;                          // ✅ 참조 슬롯
        public float remain;                          // ✅ 남은 지속시간
        public float nextTick;                        // ✅ 다음 틱까지 남은 시간
        public float flashTimer;                      // ✅ 색 변화 타이머(Up/Hold/Down)
        public enum FlashPhase { Idle, Up, Hold, Down }
        public FlashPhase phase = FlashPhase.Idle;    // ✅ 색 변화 단계
        public Dictionary<SpriteRenderer, Color> original = new(); // ✅ 원색 캐시
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // 인스펙터
    // ─────────────────────────────────────────────────────────────────────────────

    [Header("필수 참조")]
    [SerializeField] private ObjectInfo objectInfo;           // ✅ 대상 ObjectInfo 참조
    [SerializeField] private Collider2D triggerCollider;      // ✅ 대상 트리거 콜라이더(읽기용)

    [Header("색 변화 대상")]
    [SerializeField] private List<SpriteRenderer> flashTargets = new(); // ✅ 색 변화 대상 리스트

    [Header("지속피해 슬롯(프리셋)")]
    [SerializeField] private List<DotSlot> dotSlots = new();  // ✅ (typeId,checkId)별 설정 슬롯

    [Header("로깅/옵션")]
    [SerializeField] private bool logApply = false;           // ✅ 적용 로그 On/Off
    [SerializeField] private bool logTick = false;            // ✅ 틱 로그 On/Off

    // ─────────────────────────────────────────────────────────────────────────────
    // 내부 상태
    // ─────────────────────────────────────────────────────────────────────────────

    private readonly Dictionary<DotKey, ActiveState> _active = new(); // ✅ 활성 DOT 상태
    private readonly Dictionary<DotKey, DotSlot> _lookup = new();     // ✅ 슬롯 조회 캐시

    private void Reset()                                       // ▶ 자동 할당
    {
        if (!objectInfo) objectInfo = GetComponent<ObjectInfo>();
        if (!triggerCollider && objectInfo) triggerCollider = objectInfo.triggerCollider;
    }

    private void Awake()                                       // ▶ 슬롯 조회 딕셔너리 구성
    {
        _lookup.Clear();
        foreach (var s in dotSlots)
        {
            var key = new DotKey(s.typeId, s.checkId);
            if (!_lookup.ContainsKey(key))
                _lookup.Add(key, s);
        }
    }

    private void OnDisable()                                   // ▶ 비활성화 시 색 원복
    {
        foreach (var kv in _active)
        {
            var st = kv.Value;
            RestoreColors(st);
        }
        _active.Clear();
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // 외부 호출: 지속피해 부여(Emitter가 호출)
    // ─────────────────────────────────────────────────────────────────────────────

    public void OnDotHit(int typeId, int checkId)              // ▶ DOT 적용 요청(카운터 리셋/초기 틱 처리)
    {
        var key = new DotKey(typeId, checkId);
        if (!_lookup.TryGetValue(key, out var slot)) return;   // 슬롯 없으면 무시

        if (!_active.TryGetValue(key, out var st))
        {
            // 신규 적용: 즉시 1틱(피해+색변화) → 다음 틱부터 주기 동작
            st = new ActiveState
            {
                slot = slot,
                remain = slot.counterSeconds,
                nextTick = 0f,                                 // ✅ 즉시 틱 발생을 위해 0으로 시작
                phase = ActiveState.FlashPhase.Idle,
                flashTimer = 0f
            };
            CacheOriginalColors(st);
            _active[key] = st;

            // 즉시 틱 1회 수행
            DoTickAndFlash(st, key);
            st.nextTick = Mathf.Max(0.01f, slot.tickInterval); // 다음 틱 예약
        }
        else
        {
            // 이미 지속피해 중이라면: "카운터 지난 시간만 0으로" → remain 리셋
            st.remain = slot.counterSeconds;                   // ✅ 지속시간만 리셋
            // 지금 틱 스케줄은 유지(즉시 추가 피해 없음)
            if (logApply)
                Debug.Log($"[DOT Refresh] target={name} key=({typeId},{checkId}) remain={st.remain:F2}s");
        }
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // 메인 루프(Update 1개로 전체 관리)
    // ─────────────────────────────────────────────────────────────────────────────

    private void Update()                                      // ▶ 활성 DOT 틱/색 처리
    {
        if (_active.Count == 0) return;

        var removeKeys = ListPool<DotKey>.Get();               // 임시 리스트(가비지 절감)
        float dt = Time.deltaTime;

        foreach (var kv in _active)
        {
            var key = kv.Key;
            var st = kv.Value;
            var slot = st.slot;

            // 1) 틱 타이머 진행 및 필요 시 피해+색변화 적용
            st.nextTick -= dt;
            if (st.nextTick <= 0f)
            {
                DoTickAndFlash(st, key);                       // ✅ 매 틱마다 피해+색변화
                st.nextTick += Mathf.Max(0.01f, slot.tickInterval);
            }

            // 2) 색 변화 진행(틱마다 시작된 플래시 사이클을 자연스럽게 끝냄)
            UpdateFlash(st, dt);

            // 3) 남은 시간 차감/종료
            st.remain -= dt;
            if (st.remain <= 0f)
            {
                st.remain = 0f;
                // 종료 시: Down 단계로 유도, downTime 경과 후 완전 제거
                if (st.phase != ActiveState.FlashPhase.Down)
                {
                    st.phase = ActiveState.FlashPhase.Down;
                    st.flashTimer = 0f;
                }
                else
                {
                    st.flashTimer += dt;
                    if (st.flashTimer >= slot.flash.downTime)
                        removeKeys.Add(key);
                }
            }
        }

        // 상태 제거 + 색 원복
        foreach (var key in removeKeys)
        {
            if (_active.TryGetValue(key, out var st))
            {
                RestoreColors(st);
                _active.Remove(key);
            }
        }
        ListPool<DotKey>.Release(removeKeys);
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // 핵심 유틸: "한 틱" 처리 + "색변화 사이클 시작"
    // ─────────────────────────────────────────────────────────────────────────────

    private void DoTickAndFlash(ActiveState st, DotKey key)    // ▶ 피해 적용 + 플래시 사이클 시작
    {
        // 피해 계산/적용
        int finalDamage = ComputeDamage(st.slot.damagePerTick, st.slot.resistancePercent);
        if (finalDamage > 0 && objectInfo != null)
        {
            objectInfo.ApplyDamage(finalDamage);
            if (logTick)
                Debug.Log($"[DOT Tick] target={name} key=({key.typeId},{key.checkId}) dmg={finalDamage}");
        }

        // 플래시 사이클(Up→Hold→Down) 새로 시작
        st.phase = ActiveState.FlashPhase.Up;
        st.flashTimer = 0f;
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // 유틸: 색 변화/피해 계산/원색 캐시
    // ─────────────────────────────────────────────────────────────────────────────

    private void CacheOriginalColors(ActiveState st)           // ▶ 최초 1회 원색 캐시
    {
        st.original.Clear();
        foreach (var sr in flashTargets)
        {
            if (!sr) continue;
            if (!st.original.ContainsKey(sr))
                st.original.Add(sr, sr.color);
        }
    }

    private void RestoreColors(ActiveState st)                 // ▶ 원래 색상 복귀
    {
        foreach (var kv in st.original)
        {
            var sr = kv.Key;
            if (!sr) continue;
            sr.color = kv.Value;
        }
    }

    private void UpdateFlash(ActiveState st, float dt)         // ▶ 색 변화 트랜지션 처리
    {
        var slot = st.slot;
        var f = slot.flash;

        switch (st.phase)
        {
            case ActiveState.FlashPhase.Up:
                if (f.upTime <= 0f)
                {
                    ApplyColorLerp(st, 1f);                   // 즉시 목표색
                    st.phase = ActiveState.FlashPhase.Hold;
                    st.flashTimer = 0f;
                }
                else
                {
                    st.flashTimer += dt;
                    float t = Mathf.Clamp01(st.flashTimer / f.upTime);
                    float k = f.upCurve != null ? Mathf.Clamp01(f.upCurve.Evaluate(t)) : t;
                    ApplyColorLerp(st, k);
                    if (t >= 1f)
                    {
                        st.phase = ActiveState.FlashPhase.Hold;
                        st.flashTimer = 0f;
                    }
                }
                break;

        case ActiveState.FlashPhase.Hold:
                if (f.holdTime <= 0f)
                {
                    st.phase = ActiveState.FlashPhase.Down;
                    st.flashTimer = 0f;
                }
                else
                {
                    st.flashTimer += dt;
                    if (st.flashTimer >= f.holdTime)
                    {
                        st.phase = ActiveState.FlashPhase.Down;
                        st.flashTimer = 0f;
                    }
                }
                break;

            case ActiveState.FlashPhase.Down:
                if (f.downTime <= 0f)
                {
                    ApplyColorLerp(st, 0f);                   // 즉시 원색
                    st.phase = ActiveState.FlashPhase.Idle;
                    st.flashTimer = 0f;
                }
                else
                {
                    st.flashTimer += dt;
                    float t = Mathf.Clamp01(st.flashTimer / f.downTime);
                    float k = f.downCurve != null ? Mathf.Clamp01(f.downCurve.Evaluate(t)) : t;
                    ApplyColorLerp(st, 1f - k);               // 목표→원색 역보간
                    if (t >= 1f)
                    {
                        st.phase = ActiveState.FlashPhase.Idle;
                        st.flashTimer = 0f;
                    }
                }
                break;
        }
    }

    private void ApplyColorLerp(ActiveState st, float k)       // ▶ 원색↔목표색 보간 적용
    {
        var to = st.slot.flash.targetColor;
        foreach (var sr in flashTargets)
        {
            if (!sr) continue;
            if (!st.original.TryGetValue(sr, out var oc)) oc = sr.color;
            Color c = Color.LerpUnclamped(oc, to, k);
            c.a = sr.color.a;
            sr.color = c;
        }
    }

    private int ComputeDamage(int baseDamage, int resistPct)   // ▶ 내성% 적용 피해 계산
    {
        baseDamage = Mathf.Max(0, baseDamage);
        resistPct = Mathf.Clamp(resistPct, 0, 100);
        float mul = 1f - (resistPct / 100f);
        int dmg = Mathf.FloorToInt(baseDamage * mul);
        return Mathf.Max(0, dmg);
    }
}

// ─────────────────────────────────────────────────────────────────────────────
// 간단한 List 풀(가비지 절감용): 필요 없으면 List<T> 새로 만들어 써도 무방
// ─────────────────────────────────────────────────────────────────────────────
static class ListPool<T>
{
    static readonly Stack<List<T>> pool = new();
    public static List<T> Get() => pool.Count > 0 ? pool.Pop() : new List<T>(8);
    public static void Release(List<T> list) { list.Clear(); pool.Push(list); }
}
