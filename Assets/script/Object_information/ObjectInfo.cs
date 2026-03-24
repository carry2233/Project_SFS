using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[AddComponentMenu("Stats/Object Info (객체정보)")]     // 인스펙터 메뉴
[DisallowMultipleComponent]
public class ObjectInfo : MonoBehaviour
{
    [Serializable]
    public class StatLikeEntry
    {
        [Header("키(서로 다른 정수 2개)")]
        public int keyPrimary;                                 // ✅ 1차 키
        public int keySecondary;                               // ✅ 2차 키

        [Header("스탯 데이터")]
        public string statName;                                // ✅ 스탯 이름
        public int value;                                      // ✅ 현재 값
        public int maxValue = 100;                             // ✅ 최대 값
    }

    [Serializable]
    public class WearDefenseEntry
    {
        public int absoluteDefense;                            // ✅ 절대 방어치
        [Range(0,100)] public int defenseRate;                 // ✅ 방어율(%)
        public int wearSlotTier;                               // ✅ 슬롯 티어
        public int wearSlot;                                   // ✅ 슬롯 인덱스
    }

    [Header("체력")]
    [SerializeField] private int currentHealth = 100;          // ✅ 현재 체력
    [SerializeField] private int maxHealth = 100;              // ✅ 최대 체력
    [SerializeField][Range(0,100)] private int healthPercent = 100; // ✅ 체력%

    [Header("기타 상태")]
    public int bleedRate = 0;                                  // ✅ 출혈(초당 HP 감소량)
    public int stoppingPower = 0;                              // ✅ 저지력(관통력 감소량)

    [Header("부가 스탯 리스트")]
    public List<StatLikeEntry> stats = new();                  // ✅ 추가 스탯

    [Header("착용 방어 스냅샷")]
    public List<WearDefenseEntry> wearDefenses = new();        // ✅ 방어 스냅샷

    [Header("충돌/로깅")]
    public Collider2D triggerCollider;                         // ✅ 피격 검출 트리거 콜라이더
    public bool logHits = true;                                // ✅ 로그 출력 여부
    public enum LogLevel { Standard }
    public LogLevel logLevel = LogLevel.Standard;              // ✅ 로그 레벨

    [Header("피격 색상 플래시")]
    public List<SpriteRenderer> flashTargets = new();          // ✅ 플래시 대상
    public Color hitFlashColor = new Color(1f,0.3f,0.3f,1f);   // ✅ 플래시 색
    [Min(0.01f)] public float flashReturnSpeed = 6f;           // (호환용)
    [Header("자연스러운 연출(시간·곡선)")]
    [Min(0f)] public float flashUpDuration = 0.05f;            // ✅ 상승 시간
    [Min(0f)] public float flashHoldDuration = 0.06f;          // ✅ 유지 시간
    [Min(0f)] public float flashDownDuration = 0.22f;          // ✅ 하강 시간
    public AnimationCurve upCurve = AnimationCurve.EaseInOut(0,0,1,1);   // ✅ 상승 곡선
    public AnimationCurve downCurve = AnimationCurve.EaseInOut(0,0,1,1); // ✅ 하강 곡선

    [Header("사망 처리")]
    public GameObject deactivateOnDeath;                       // ✅ 사망 시 비활성화 대상(없으면 self)
    private bool _isDead;                                      // ✅ 사망 1회 보장

    [Header("내구도 감소 연동")]
    [SerializeField] private EquipmentDurabilityManager durabilityManager; // ✅ 내구도 매니저
    [SerializeField] private ApparelDurabilityManager apparelDurabilityManager; // 의류 내구도 매니저(신규)

    [SerializeField] private ApparelWearStateRegistry apparelWearStateRegistry; // 의류 상태 레지스트리


    [Header("혈흔/시체 스폰 연동")]
    [SerializeField] private BloodSplatterSpawner bloodSpawner; // ✅ 혈흔/시체 스포너
    [SerializeField] private bool spawnCorpseOnDeath = true;    // ✅ 사망 시 시체 생성 여부
    [SerializeField] private GameObject corpsePrefab;           // ✅ 시체 프리팹

    // 이벤트
    public event Action<int,int,int> OnHealthChanged;           // ✅ 체력 변경 이벤트
    public event Action OnDied;                                 // ✅ 사망 이벤트


[Header("영양 시스템")]
public int currentNutrition = 100;     // 현재 영양
public int maxNutrition = 100;         // 최대 영양
public int nutritionThreshold = 20;    // 부족 판정값
public int nutritionDamage = 1;        // 부족 시 HP 감소
public float nutritionInterval = 1f;   // 피해 주기
private Coroutine _nutritionRoutine;   // 루프 핸들

[Header("수분 시스템")]
public int currentHydration = 100;     // 현재 수분
public int maxHydration = 100;         // 최대 수분
public int hydrationThreshold = 20;    // 부족 판정값
public int hydrationDamage = 1;        // 부족 시 HP 감소
public float hydrationInterval = 1f;   // 피해 주기
private Coroutine _hydrationRoutine;   // 루프 핸들

// ⭐ 피격 이벤트 (공격자 전달)
public event Action<Transform, CombatPayload2D> OnDamaged;



    // 내부 상태
    private Dictionary<SpriteRenderer, Color> _originalColors;  // ✅ 원색 캐시
    private Coroutine _flashRoutine;                            // ✅ 플래시 코루틴
    private Coroutine _bleedRoutine;                            // ✅ 출혈 코루틴

    public int CurrentHealth                                    // ▶ 현재 체력 프로퍼티
    {
        get => currentHealth;
        set { SetCurrentHealth(value); }
    }

    public int MaxHealth                                        // ▶ 최대 체력 프로퍼티
    {
        get => maxHealth;
        set { SetMaxHealth(value); }
    }

    public int HealthPercent => healthPercent;                   // ▶ 체력% 읽기 전용

    private void Reset()                                         // 🔧 자동 할당
    {
        if (!triggerCollider) triggerCollider = GetComponent<Collider2D>();
        if (!durabilityManager) durabilityManager = FindObjectOfType<EquipmentDurabilityManager>();
        if (!bloodSpawner) bloodSpawner = FindObjectOfType<BloodSplatterSpawner>();
    }

    private void Start()                                         // ▶ 시작 보정
    {
        RecalculatePercentAndClamp();
        RaiseHealthChanged();

        if (triggerCollider && !triggerCollider.isTrigger)
            triggerCollider.isTrigger = true;

        CacheOriginalColorsIfNeeded();

        if (!durabilityManager) durabilityManager = FindObjectOfType<EquipmentDurabilityManager>();
        if (!bloodSpawner) bloodSpawner = FindObjectOfType<BloodSplatterSpawner>();

        EnsureBleedLoopIfNeeded();                               // ✅ 시작 시 bleedRate>0이면 루프 시작
    }

    public void SetCurrentNutrition(int v)
{
    currentNutrition = Mathf.Clamp(v, 0, maxNutrition);
    EnsureNutritionLoop();
}

public void SetCurrentHydration(int v)
{
    currentHydration = Mathf.Clamp(v, 0, maxHydration);
    EnsureHydrationLoop();
}

private void EnsureNutritionLoop()
{
    if (currentNutrition <= nutritionThreshold && _nutritionRoutine == null && !_isDead)
        _nutritionRoutine = StartCoroutine(Co_NutritionLoop());
}

private void EnsureHydrationLoop()
{
    if (currentHydration <= hydrationThreshold && _hydrationRoutine == null && !_isDead)
        _hydrationRoutine = StartCoroutine(Co_HydrationLoop());
}

private IEnumerator Co_NutritionLoop()
{
    while (currentNutrition <= nutritionThreshold && !_isDead)
    {
        ApplyDamage(nutritionDamage);
        TriggerHitFlash(); // 기존 피격 플래시 그대로 활용
        yield return new WaitForSeconds(nutritionInterval);
    }
    _nutritionRoutine = null;
}

private IEnumerator Co_HydrationLoop()
{
    while (currentHydration <= hydrationThreshold && !_isDead)
    {
        ApplyDamage(hydrationDamage);
        TriggerHitFlash();
        yield return new WaitForSeconds(hydrationInterval);
    }
    _hydrationRoutine = null;
}


    private void OnValidate()                                    // ▶ 에디터 변경 보정
    {
        RecalculatePercentAndClamp();
        RaiseHealthChanged();
        TryDeathProcessIfNeeded();
        if (!durabilityManager) durabilityManager = FindObjectOfType<EquipmentDurabilityManager>();
        if (!bloodSpawner) bloodSpawner = FindObjectOfType<BloodSplatterSpawner>();
    }

    public void SetCurrentHealth(int newValue)                   // ▶ 현재 체력 세팅
    {
        currentHealth = newValue;
        RecalculatePercentAndClamp();
        RaiseHealthChanged();
        TryDeathProcessIfNeeded();
    }

    public void SetMaxHealth(int newMax)                         // ▶ 최대 체력 세팅
    {
        maxHealth = newMax;
        RecalculatePercentAndClamp();
        RaiseHealthChanged();
        TryDeathProcessIfNeeded();
    }

    public void ApplyDamage(int amount)                          // ▶ 데미지 적용(양수만)
    {
        SetCurrentHealth(currentHealth - Mathf.Abs(amount));
    }

    public void Heal(int amount)                                 // ▶ 회복(양수만)
    {
        SetCurrentHealth(currentHealth + Mathf.Abs(amount));
    }

    private void RecalculatePercentAndClamp()                    // ▶ 체력% 재계산/클램프
    {
        maxHealth = Mathf.Max(1, maxHealth);
        currentHealth = Mathf.Clamp(currentHealth, 0, maxHealth);
        float pct = (currentHealth / (float)maxHealth) * 100f;
        healthPercent = Mathf.Clamp(Mathf.RoundToInt(pct), 0, 100);
    }

    private void RaiseHealthChanged()                            // ▶ 체력 변경 이벤트 발행
    {
        OnHealthChanged?.Invoke(currentHealth, maxHealth, healthPercent);
    }

    private void TryDeathProcessIfNeeded()                       // ▶ 사망 처리(0 진입 시 1회)
    {
        if (_isDead) return;
        if (currentHealth > 0) return;

        // 비활성화 전에 시체 스폰(시체 부모에 자식으로 생성)
        if (spawnCorpseOnDeath && bloodSpawner && corpsePrefab)
        {
            Vector3 pos = triggerCollider ? (Vector3)triggerCollider.bounds.center : transform.position;
            bloodSpawner.SpawnCorpseAt(pos, corpsePrefab);
        }

        _isDead = true;
        OnDied?.Invoke();

        var target = deactivateOnDeath ? deactivateOnDeath : gameObject;
        if (target) target.SetActive(false);
    }

    private void OnTriggerEnter2D(Collider2D other)              // ▶ 피격 진입
    {
        TryHandleHit(other, "Enter");
    }

    private void OnTriggerStay2D(Collider2D other)               // ▶ 필요 시 유지 처리
    {
        // TryHandleHit(other, "Stay");
    }

private void TryHandleHit(Collider2D other, string phase)    // ▶ 피격 처리 본체
{
    if (!other) return;                                      // ▶ 방어 코드

    // 1) 콜라이더에서 CombatPayload2D 찾기
    var payload = other.GetComponent<CombatPayload2D>();     // ▶ 같은 오브젝트에서 페이로드 찾기
    if (!payload) payload = other.GetComponentInParent<CombatPayload2D>(); // ▶ 부모에서 재시도
    if (!payload)
    {
        if (logHits)
            Debug.LogWarning($"[HIT {phase}] 대상={name} | 원인={other.name} | CombatPayload2D 없음");
        return;
    }

    // 2) 방어 데이터(절대/방어율) 가져오기
    var chosen  = PickRandomDefense();                       // ▶ 방어 스냅샷 중 하나 선택(없으면 null)
    int absDef  = chosen != null ? chosen.absoluteDefense : 0;                // ▶ 절대 방어치
    int defRate = chosen != null ? Mathf.Clamp(chosen.defenseRate, 0, 100) : 0; // ▶ 방어율(%)

    // 3) 관통 여부(절대 힘 vs 절대 방어)
    bool penetrates = payload.absolutePower > absDef;        // ▶ 절대위력이 방어를 초과하면 관통

    // 4) 저지력 적용(관통력 감소)
    if (payload.canReducePenetration && stoppingPower > 0)
    {
        payload.ReducePenetration(stoppingPower);            // ▶ 관통력 감소
        // 관통력이 0이 되어 페이로드가 비활성화되더라도
        // 이번 프레임의 히트 처리는 계속 진행
    }

    // 5) 혈흔/플래시 연출
    if (penetrates && bloodSpawner != null)
        bloodSpawner.Spawn();                                // ▶ 관통 시 혈흔 스폰

    if (penetrates)
        TriggerHitFlash();                                   // ▶ 피격 플래시

    // 6) 피해량 계산
    int rawDamage   = payload.attackPower;                   // ▶ 기본 공격력
    int finalDamage = penetrates ? ComputeReducedDamage(rawDamage, defRate) : 0; // ▶ 방어율 적용

    int hpBefore = CurrentHealth;                            // ▶ 이전 체력
    int brBefore = bleedRate;                                // ▶ 이전 출혈량

    if (finalDamage > 0)
        ApplyDamage(finalDamage);                            // ▶ 최종 피해 적용

        // ⭐⭐⭐ [추가] 공격자 전달 이벤트 (AI용)
if (payload.Attacker != null)
{
    OnDamaged?.Invoke(payload.Attacker, payload);
}


    if (penetrates && payload.bleedRate > 0)
        bleedRate += payload.bleedRate;                      // ▶ 관통 + 출혈 수치가 있을 때 출혈 누적

    int hpAfter = CurrentHealth;                             // ▶ 적용 후 체력
    int brAfter = bleedRate;                                 // ▶ 적용 후 출혈량
    int hpDelta = hpBefore - hpAfter;                        // ▶ 줄어든 체력
    int brDelta = brAfter - brBefore;                        // ▶ 늘어난 출혈량

    // 7) 로그 출력
    if (logHits && logLevel == LogLevel.Standard)
    {
        string comp   = $"AbsPow {payload.absolutePower} vs AbsDef {absDef} → {(penetrates ? "관통" : "방어")}";
        string dmgTxt = $"피해=원 {rawDamage} → 최종 {finalDamage}";
        string hpTxt  = $"HP {hpBefore}→{hpAfter} ({(hpDelta == 0 ? "0" : "-" + hpDelta)})";
        string brTxt  = $"출혈 {brBefore}→{brAfter} ({(brDelta == 0 ? "0" : "+" + brDelta)})";
        string penTxt = payload.canReducePenetration
            ? $"관통력 {payload.currentPenetration}/{payload.maxPenetration}"
            : "관통력 감소 비허용";

          Debug.Log($"[HIT {phase}] 대상={name} | 원={payload.sourceName} | 방어율={defRate}% | {dmgTxt} | {hpTxt}, {brTxt} | {penTxt}");
    }

    // 8) 무기/도구 내구도 감소
    if (durabilityManager != null)
        durabilityManager.AdjustDurabilityByPayload(payload, penetrates); // ▶ 무기/도구 내구도 감소

    // 9) 의류 내구도 감소(신규)
    if (apparelDurabilityManager != null && chosen != null)
        apparelDurabilityManager.OnHitWearDefense(chosen, penetrates);    // ▶ 방어에 사용된 의류 내구도 감소

    // 10) 출혈 루프 보장
    EnsureBleedLoopIfNeeded();                               // ▶ bleedRate>0이면 출혈 코루틴 시작

    // 9) 의류 내구도 감소
if (apparelDurabilityManager != null && chosen != null)
    apparelDurabilityManager.OnHitWearDefense(chosen, penetrates);

// 🔻🔻 (추가) 부위 의류 색플래시
if (chosen != null)
    TriggerApparelHitFlash(chosen.wearSlotTier, chosen.wearSlot);

}



    private WearDefenseEntry PickRandomDefense()                 // ▶ 방어 스냅샷 무작위 선택
    {
        if (wearDefenses == null || wearDefenses.Count == 0) return null;
        int idx = UnityEngine.Random.Range(0, wearDefenses.Count);
        return wearDefenses[idx];
    }

    private int ComputeReducedDamage(int rawDamage, int defenseRate) // ▶ 방어율 기반 피해감소
    {
        rawDamage   = Mathf.Max(0, rawDamage);
        defenseRate = Mathf.Clamp(defenseRate, 0, 100);
        float reduced = rawDamage * (1f - defenseRate / 100f);
        int result = Mathf.RoundToInt(reduced);
        return Mathf.Max(0, result);
    }

    private void CacheOriginalColorsIfNeeded()                   // ▶ 원래 색상 캐시
    {
        if (_originalColors != null) return;
        _originalColors = new Dictionary<SpriteRenderer, Color>(flashTargets.Count);
        foreach (var sr in flashTargets)
        {
            if (!sr) continue;
            if (!_originalColors.ContainsKey(sr))
                _originalColors.Add(sr, sr.color);
        }
    }

    private void TriggerHitFlash()                               // ▶ 피격 플래시 시작
    {
        CacheOriginalColorsIfNeeded();

        if (_flashRoutine != null)
        {
            StopCoroutine(_flashRoutine);
            _flashRoutine = null;
        }
        _flashRoutine = StartCoroutine(Co_HitFlashSmooth());
    }

    private IEnumerator Co_HitFlashSmooth()                      // ▶ 자연스러운 3단계 플래시
    {
        if (flashTargets == null || flashTargets.Count == 0) yield break;

        var fromColors = new Dictionary<SpriteRenderer, Color>(flashTargets.Count);
        foreach (var sr in flashTargets)
        {
            if (!sr) continue;
            fromColors[sr] = sr.color;
        }

        // Up
        if (flashUpDuration > 0f)
        {
            float t = 0f;
            while (t < 1f)
            {
                t += Time.deltaTime / Mathf.Max(0.0001f, flashUpDuration);
                float k = upCurve != null ? Mathf.Clamp01(upCurve.Evaluate(Mathf.Clamp01(t))) : Mathf.Clamp01(t);
                foreach (var kv in fromColors)
                {
                    var sr = kv.Key;
                    if (!sr) continue;
                    sr.color = Color.LerpUnclamped(kv.Value, hitFlashColor, k);
                }
                yield return null;
            }
        }
        else
        {
            foreach (var sr in flashTargets) if (sr) sr.color = hitFlashColor;
        }

        // Hold
        if (flashHoldDuration > 0f)
            yield return new WaitForSeconds(flashHoldDuration);

        // Down
        var fromNow = new Dictionary<SpriteRenderer, Color>(flashTargets.Count);
        foreach (var sr in flashTargets)
        {
            if (!sr) continue;
            fromNow[sr] = sr.color;
        }

        if (flashDownDuration > 0f)
        {
            float t = 0f;
            while (t < 1f)
            {
                t += Time.deltaTime / Mathf.Max(0.0001f, flashDownDuration);
                float k = downCurve != null ? Mathf.Clamp01(downCurve.Evaluate(Mathf.Clamp01(t))) : Mathf.Clamp01(t);
                foreach (var sr in flashTargets)
                {
                    if (!sr) continue;
                    var to = _originalColors.TryGetValue(sr, out var oc) ? oc : Color.white;
                    var fr = fromNow.TryGetValue(sr, out var fc) ? fc : hitFlashColor;
                    sr.color = Color.LerpUnclamped(fr, to, k);
                }
                yield return null;
            }
        }

        foreach (var sr in flashTargets)
        {
            if (!sr) continue;
            if (_originalColors.TryGetValue(sr, out var oc))
                sr.color = oc;
        }

        _flashRoutine = null;
    }

    // ───────────── 출혈 루프 ─────────────

    public void EnsureBleedLoopIfNeeded()                       // ▶ bleedRate>0면 코루틴 보장
    {
        if (bleedRate > 0 && _bleedRoutine == null && !_isDead)
            _bleedRoutine = StartCoroutine(Co_BleedLoop());
    }

    private IEnumerator Co_BleedLoop()                           // ▶ 1초마다 bleedRate만큼 HP 감소 + 혈흔(반경/Z랜덤)
    {
        while (true)
        {
            if (_isDead) break;

            int cur = Mathf.Max(0, bleedRate);                  // 현재 시점의 출혈량 스냅샷
            if (cur <= 0) break;

            ApplyDamage(cur);                                   // HP 감소

            if (bloodSpawner != null)                           // 피격 때와 동일하게 범위/Z랜덤 생성
                bloodSpawner.Spawn();

            yield return new WaitForSeconds(1f);                // 정확히 1초 간격
        }
        _bleedRoutine = null;
    }

    // ▶ (추가) 피격된 의류 SpriteRenderer만 색플래시
private void TriggerApparelHitFlash(int tier, int slot)
{
    if (!apparelWearStateRegistry) return;

    // 현재 착용된 부위의 Renderer 가져오기
    if (!apparelWearStateRegistry.TryGetCurrentRendererBySlot(tier, slot, out var sr))
        return;

    StartCoroutine(Co_ApparelHitFlash(sr));
}

// ▶ (추가) 단일 SpriteRenderer 색플래시 코루틴
private IEnumerator Co_ApparelHitFlash(SpriteRenderer sr)
{
    if (!sr) yield break;

    Color original = sr.color;
    float t = 0f;

    // Up
    while (t < flashUpDuration)
    {
        t += Time.deltaTime;
        float k = upCurve.Evaluate(t / flashUpDuration);
        sr.color = Color.Lerp(original, hitFlashColor, k);
        yield return null;
    }

    // Hold
    yield return new WaitForSeconds(flashHoldDuration);

    // Down
    t = 0f;
    while (t < flashDownDuration)
    {
        t += Time.deltaTime;
        float k = downCurve.Evaluate(t / flashDownDuration);
        sr.color = Color.Lerp(hitFlashColor, original, k);
        yield return null;
    }

    sr.color = original;
}

}
