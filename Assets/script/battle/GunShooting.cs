using UnityEngine;
using System.Collections.Generic;

[AddComponentMenu("Combat/GunShooting")]
public class GunShooting : MonoBehaviour
{
    // ─────────────────────────────────────────────────────────────────────────────
    // 🔗 참조
    // ─────────────────────────────────────────────────────────────────────────────
    [Header("참조")]
    [SerializeField] private Transform spawnPoint;          // 탄환 생성 위치/방향(총구)
    [SerializeField] private Transform poolParent;          // 오브젝트 풀 부모(계층 구조용 오브젝트)
    [SerializeField] private GameObject bulletPrefab;       // 탄환 프리팹(BulletCompat 필수)

    // ─────────────────────────────────────────────────────────────────────────────
    // ⚙ 발사 설정
    // ─────────────────────────────────────────────────────────────────────────────
    [Header("발사 설정 (정수)")]
    [SerializeField] private int bulletSpeed = 5;           // 탄환 속도(정수, 1이면 초당 1유닛 이동)
    [SerializeField] private int bulletRange = 10;          // 탄환 사거리(정수, 유닛)

    // ─────────────────────────────────────────────────────────────────────────────
    // 🎯 정확도/분산 설정
    // ─────────────────────────────────────────────────────────────────────────────
    [Header("정확도 시스템")]
    [Range(0,100)][SerializeField] private int baseAccuracy = 100;  // 기본 적중률(%)
    [SerializeField] private int minAccuracy = 0;                   // 적중률 하한(%)
    [SerializeField] private int maxAccuracy = 100;                 // 적중률 상한(%)
    [SerializeField] private float dispersionPerShot = 10f;         // 사격 분산: 발사 1회당 적중률 감소(%)
    [SerializeField] private float recoveryPerSecond = 20f;         // 복원력: 사격 중이든 아니든 초당 회복(%/s) ※수정 반영

    // ─────────────────────────────────────────────────────────────────────────────
    // 🔺 부채꼴 각도(정확도→각도 매핑) & 시각화
    // ─────────────────────────────────────────────────────────────────────────────
    [Header("부채꼴 각도 설정 (정확도→각도 매핑)")]
    [SerializeField] private float maxConeAngleDeg = 120f;  // 정확도 0%일 때 부채꼴 최대 각도(°)
    [SerializeField] private float minConeAngleDeg = 0f;    // 정확도 100%일 때 최소 각도(°)

    [Header("콘 시각화(선택)")]
    [SerializeField] private bool visualizeCone = true;     // 콘(부채꼴) 시각화 사용 여부
    [SerializeField] private LineRenderer coneRenderer;     // 콘 그리기용 라인렌더러(선택)
    [SerializeField] private int coneSegments = 24;         // 콘 호(arc) 분할 수
    [SerializeField] private float coneVisualRadius = 2f;   // 콘 시각화 반경(씬에서 보이는 길이)

    // ─────────────────────────────────────────────────────────────────────────────
    // ⌨ 입력 제어
    // ─────────────────────────────────────────────────────────────────────────────
    [Header("입력 제어")]
    [SerializeField] public bool inputEnabled = true;       // 마우스 입력 사용 여부(플레이어용 ON, AI용 OFF)
    [SerializeField] private KeyCode modeSwitchKey = KeyCode.R; // 모드 전환 키(기본 R)

    [Header("스탯 연동")]
    [SerializeField] private RangedStatSystem rangedStatSystem; // 단방향 참조

[Header("정확도 원본 값")]
[SerializeField] private int baseAccuracyRaw;
[SerializeField] private int minAccuracyRaw;
[SerializeField] private int maxAccuracyRaw;
[SerializeField] private float recoveryPerSecondRaw;

[Header("정확도 적용 값")]
[SerializeField] private int baseAccuracyApplied;
[SerializeField] private int minAccuracyApplied;
[SerializeField] private int maxAccuracyApplied;
[SerializeField] private float recoveryPerSecondApplied;

[Header("공격자 설정")]
[SerializeField] private Transform attackerRoot; // 발사자(플레이어/AI 루트)

// ==============================
// ⌨ 입력 제어  (기존 영역 근처에 추가)
// ==============================

[Header("AI 제어")]
[SerializeField] private bool aiControlOnly = false; // AI 전용 제어 여부(true면 입력발사 차단)

    // ─────────────────────────────────────────────────────────────────────────────
    // 🔫 발사 모드 설정(신규)
    // ─────────────────────────────────────────────────────────────────────────────
    private enum FireMode { Semi, Auto, Burst }             // 발사 모드 열거형

    [System.Serializable]
    private class ModeConfig
    {
        public bool enabled = true;                         // ✅ 모드 사용 여부
        public int priority = 0;                            // ✅ 모드 우선순위(낮을수록 먼저)
        public float firePeriod = 0.1f;                     // ✅ 발사 주기(초) - Auto/Burst에서 사용
        public int burstCount = 3;                          // ✅ 점사 탄수(클릭당) - Burst에서 사용
    }

    [Header("모드: 단발(Semi)")]
    [SerializeField] private ModeConfig semi = new ModeConfig { enabled = true,  priority = 0, firePeriod = 0.1f,  burstCount = 1 }; // 단발 기본 설정
    [Header("모드: 연발(Auto)")]
    [SerializeField] private ModeConfig auto = new ModeConfig { enabled = true,  priority = 1, firePeriod = 0.12f, burstCount = 1 }; // 연발 기본 설정
    [Header("모드: 점사(Burst)")]
    [SerializeField] private ModeConfig burst = new ModeConfig { enabled = true, priority = 2, firePeriod = 0.08f, burstCount = 3 }; // 점사 기본 설정

    // ─────────────────────────────────────────────────────────────────────────────
    // 🧠 내부 상태
    // ─────────────────────────────────────────────────────────────────────────────
    private float currentAccuracy;                          // 현재 적중률(0~100)
    private float currentConeAngle;                         // 현재 부채꼴 각도(도)

    private List<FireMode> availableModes = new List<FireMode>(); // 사용 가능 모드 목록(우선순위 정렬)
    private int currentModeIndex = 0;                       // 현재 모드 인덱스(availableModes 기준)
    private float nextFireTime = 0f;                        // 다음 발사 가능 시각(Time.time)
    private int burstShotsLeft = 0;                         // 점사 남은 탄수
    private float nextBurstShotTime = 0f;                   // 점사 내부 연사 타이밍
    private int shotsThisFrame = 0;                         // ✅ 내구도 계산용: Consume 전까지 누적된 발사 횟수

    // ─────────────────────────────────────────────────────────────────────────────
    // ♻ 초기화
    // ─────────────────────────────────────────────────────────────────────────────
private void Awake()
{
    // 원본값 백업
    baseAccuracyRaw = baseAccuracy;
    minAccuracyRaw  = minAccuracy;
    maxAccuracyRaw  = maxAccuracy;
    recoveryPerSecondRaw = recoveryPerSecond;

    ApplyRangedStatBonusIfReady(); // ✅ 스탯 보정 반영

    currentAccuracy = Mathf.Clamp(
        baseAccuracyApplied,
        minAccuracyApplied,
        maxAccuracyApplied
    );

    currentConeAngle = AccuracyToConeAngle(currentAccuracy);
    SetupConeRenderer();
    RebuildAvailableModes();
    EnsureValidCurrentMode();
}


#if UNITY_EDITOR
    private void OnValidate()                               // 인스펙터 값 변경 시 정합성 유지
    {
        RebuildAvailableModes();                            // 모드 목록 재구성(우선순위 반영)
        EnsureValidCurrentMode();                           // 현재 모드 유효화
    }
#endif

    // ─────────────────────────────────────────────────────────────────────────────
    // ⏱ 매 프레임
    // ─────────────────────────────────────────────────────────────────────────────
    private void Update()                                   // 매 프레임: 복원/각도/시각화/입력/발사 처리
    {
            // ✅ 추가: AI 전용 제어면 입력 처리만 차단(정확도/각도 계산은 유지)
    if (aiControlOnly) // AI 전용이면
    {
        if (coneRenderer) coneRenderer.enabled = false; // 콘 시각화는 항상 OFF
        RecoverAccuracy(Time.deltaTime); // 정확도 회복은 유지
        currentConeAngle = AccuracyToConeAngle(currentAccuracy); // 각도 갱신은 유지
        return; // 입력/모드전환/입력발사 로직 차단
    }
        // 라인렌더러는 우클릭 유지 동안에만 활성화
        if (coneRenderer)
            coneRenderer.enabled = visualizeCone && Input.GetMouseButton(1); // 우클릭 유지 시만 표시

        // 우클릭(조준) 시작 시 정확도 리셋
        if (inputEnabled && Input.GetMouseButtonDown(1))
        {
            currentAccuracy = Mathf.Clamp(baseAccuracy, minAccuracy, maxAccuracy);
        }

        // ▼▼▼ 수정: 좌클릭 유지 여부와 상관없이 항상 회복 시작
        RecoverAccuracy(Time.deltaTime);                    // 사격 중이든 아니든 회복 수행
        // ▲▲▲ 수정 끝

        currentConeAngle = AccuracyToConeAngle(currentAccuracy); // 정확도→각도로 환산
        if (visualizeCone) UpdateConeVisual();              // 콘 시각화 갱신

        // 모드 전환 입력
        if (inputEnabled && Input.GetKeyDown(modeSwitchKey))
        {
            SwitchToNextMode();                             // 다음 사용 가능 모드로 전환
        }

        // 발사 입력 처리 (우클릭 유지 조건 공통)
        if (!inputEnabled) return;
        bool aimHeld = Input.GetMouseButton(1);             // 우클릭 유지 여부
        if (!aimHeld) { ClearBurstState(); return; }        // 조준 해제 시 점사 상태 초기화

        var mode = GetCurrentMode();                        // 현재 모드 조회

        switch (mode)
        {
            case FireMode.Semi:
                HandleSemiMode();                           // 단발
                break;
            case FireMode.Auto:
                HandleAutoMode();                           // 연발
                break;
            case FireMode.Burst:
                HandleBurstMode();                          // 점사
                break;
        }
    }


    private void ApplyRangedStatBonusIfReady()
{
    baseAccuracyApplied = baseAccuracyRaw;
    minAccuracyApplied  = minAccuracyRaw;
    maxAccuracyApplied  = maxAccuracyRaw;
    recoveryPerSecondApplied = recoveryPerSecondRaw;

    if (rangedStatSystem == null) return;
    if (!rangedStatSystem.IsAccuracyBonusReady()) return;

    baseAccuracyApplied += Mathf.RoundToInt(rangedStatSystem.GetBaseAccuracyBonus());
    minAccuracyApplied  += Mathf.RoundToInt(rangedStatSystem.GetMinAccuracyBonus());
    maxAccuracyApplied  += Mathf.RoundToInt(rangedStatSystem.GetMaxAccuracyBonus());
    recoveryPerSecondApplied += rangedStatSystem.GetRecoveryBonus();

    // 정합성 보정
    if (minAccuracyApplied > maxAccuracyApplied)
        minAccuracyApplied = maxAccuracyApplied;

    baseAccuracyApplied = Mathf.Clamp(
        baseAccuracyApplied,
        minAccuracyApplied,
        maxAccuracyApplied
    );

    // 실제 사격 로직에서 사용할 값 교체
    baseAccuracy = baseAccuracyApplied;
    minAccuracy  = minAccuracyApplied;
    maxAccuracy  = maxAccuracyApplied;
    recoveryPerSecond = recoveryPerSecondApplied;
}


    // ─────────────────────────────────────────────────────────────────────────────
    // 🔁 모드 처리
    // ─────────────────────────────────────────────────────────────────────────────
    private void HandleSemiMode()                           // 단발: 좌클릭 '순간'에 한 발
    {
        if (Input.GetMouseButtonDown(0))
        {
            Fire();                                         // 발사
        }
    }

    private void HandleAutoMode()                           // 연발: 좌클릭 '유지' 동안 주기적 발사
    {
        if (!Input.GetMouseButton(0)) return;
        float period = GetConfig(FireMode.Auto).firePeriod;
        if (Time.time >= nextFireTime)
        {
            Fire();                                         // 발사
            nextFireTime = Time.time + Mathf.Max(0.01f, period);
        }
    }

    private void HandleBurstMode()                          // 점사: 좌클릭 '순간'에 N발, 주기 간격으로 소진
    {
        var cfg = GetConfig(FireMode.Burst);
        float period = Mathf.Max(0.01f, cfg.firePeriod);
        int count = Mathf.Max(1, cfg.burstCount);

        if (Input.GetMouseButtonDown(0) && burstShotsLeft <= 0)
        {
            burstShotsLeft = count;                         // 점사 시작
            nextBurstShotTime = 0f;                         // 첫 발 즉시 가능
        }

        if (burstShotsLeft > 0 && Time.time >= nextBurstShotTime)
        {
            Fire();                                         // 발사
            burstShotsLeft--;
            nextBurstShotTime = Time.time + period;
        }
    }

    private void ClearBurstState()                          // 점사 상태 초기화
    {
        burstShotsLeft = 0;
        nextBurstShotTime = 0f;
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // 🔄 모드 전환/구성
    // ─────────────────────────────────────────────────────────────────────────────
    private void RebuildAvailableModes()                    // 사용 가능 모드 목록 재구성(우선순위)
    {
        var temp = new List<(FireMode mode, int pri)>();
        if (semi.enabled)  temp.Add((FireMode.Semi,  semi.priority));
        if (auto.enabled)  temp.Add((FireMode.Auto,  auto.priority));
        if (burst.enabled) temp.Add((FireMode.Burst, burst.priority));
        temp.Sort((a, b) => a.pri.CompareTo(b.pri));        // 우선순위 오름차순
        availableModes.Clear();
        foreach (var t in temp) availableModes.Add(t.mode);
    }

    private void EnsureValidCurrentMode()                   // 현재 모드 유효화
    {
        if (availableModes.Count == 0)
        {
            availableModes.Add(FireMode.Semi);
            currentModeIndex = 0;
            semi.enabled = true;
        }
        currentModeIndex = Mathf.Clamp(currentModeIndex, 0, availableModes.Count - 1);
    }

    private void SwitchToNextMode()                         // 다음 사용 가능 모드로 전환(순환)
    {
        if (availableModes.Count <= 1) return;
        currentModeIndex = (currentModeIndex + 1) % availableModes.Count;
        ClearBurstState();                                   // 모드 전환 시 점사 상태 초기화
        nextFireTime = 0f;                                   // 연발 타이밍 리셋
    }

    private FireMode GetCurrentMode()                       // 현재 모드 조회
    {
        if (availableModes.Count == 0) return FireMode.Semi;
        return availableModes[currentModeIndex];
    }

    private ModeConfig GetConfig(FireMode mode)             // 모드별 설정 반환
    {
        switch (mode)
        {
            case FireMode.Semi:  return semi;
            case FireMode.Auto:  return auto;
            case FireMode.Burst: return burst;
        }
        return semi;
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // 🎯 발사 루틴(API)
    // ─────────────────────────────────────────────────────────────────────────────
    public void FireOneShot()                               // AI가 직접 호출해 한 발 발사(마우스 입력과 무관)
    {
        Fire();                                             // 기존 비공개 발사 루틴 호출
    }

    public int ConsumeShotsThisFrame()                      // ✅ 내구도 매니저용: 이번까지 누적된 발사 횟수를 가져오고 0으로 리셋
{
    int count = shotsThisFrame;                         // 지금까지 기록된 발사 횟수 복사
    shotsThisFrame = 0;                                 // 다음 프레임 계산을 위해 0으로 초기화
    return count;                                       // 이번 Consume 시점까지의 발사 횟수 반환
}

private void Fire()                                     // 발사 처리(풀에서 꺼내 초기화)
{
    if (!spawnPoint || !poolParent || !bulletPrefab) return;

    GameObject bullet = GetFromPoolOrCreate();          // 풀에서 비활성 오브젝트 획득 또는 생성
    if (!bullet) return;

    // === 정렬 1: 탄환 로컬 X+를 spawnPoint X+에 맞춤 ===
    Quaternion alignRight = Quaternion.FromToRotation(Vector3.right, spawnPoint.right);

    // === 정렬 2: 프리팹 기본 회전 추가(아티스트 기준 유지) ===
    Quaternion prefabRot = bulletPrefab.transform.rotation;
    Quaternion baseRot = alignRight * prefabRot;

    // === 정렬 3: 정확도→부채꼴 내 랜덤 각도(Z축 회전) 적용 ===
    float half = currentConeAngle * 0.5f;
    float randAngle = Random.Range(-half, +half);
    Quaternion coneRot = Quaternion.AngleAxis(randAngle, Vector3.forward);

    // 최종 회전 = 기본 회전 * 콘 회전
    Quaternion finalRot = baseRot * coneRot;

    // 위치/회전 적용 후 활성화
    bullet.transform.SetPositionAndRotation(spawnPoint.position, finalRot);
    bullet.SetActive(true);

    var payload = bullet.GetComponent<CombatPayload2D>();
    if (payload != null)
    {
        payload.SetAttacker(attackerRoot);
    }

    // 초기화(속도/사거리 전달 → 수명 자동 계산)
    var compat = bullet.GetComponent<BulletCompat>();
    if (compat != null)
    {
        compat.Initialize(bulletSpeed, bulletRange);
    }

    currentAccuracy = Mathf.Clamp(
    currentAccuracy - dispersionPerShot,
    minAccuracyApplied,
    maxAccuracyApplied
);
    shotsThisFrame++;                                   // ✅ 이번 Consume 대상 발사 횟수 +1 (사격 1회당 1 증가)
}

    // ─────────────────────────────────────────────────────────────────────────────
    // ♻ 정확도 복원/계산
    // ─────────────────────────────────────────────────────────────────────────────
private void RecoverAccuracy(float dt)
{
    currentAccuracy += recoveryPerSecondApplied * dt;
    currentAccuracy = Mathf.Clamp(
        currentAccuracy,
        minAccuracyApplied,
        maxAccuracyApplied
    );
}


    private float AccuracyToConeAngle(float accuracy)       // 정확도(%) → 부채꼴 각도(도)
    {
        float a = Mathf.Clamp01(accuracy / 100f);           // 0~1 정규화
        return Mathf.Lerp(maxConeAngleDeg, minConeAngleDeg, a); // 정확도 높을수록 각도 축소
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // 📐 라인렌더러 시각화
    // ─────────────────────────────────────────────────────────────────────────────
    private void SetupConeRenderer()                        // 라인렌더러 초기화(선택)
    {
        if (!coneRenderer) return;
        coneRenderer.positionCount = Mathf.Max(1, coneSegments) + 3; // segs + 3
        coneRenderer.loop = false;
        coneRenderer.useWorldSpace = true;
        coneRenderer.enabled = false;                       // 기본 비활성(우클릭 시만 활성)
    }

    private void UpdateConeVisual()                         // 콘 시각화 갱신
    {
        if (!coneRenderer || !spawnPoint) return;

        Vector3 center = spawnPoint.position;
        Quaternion basis = Quaternion.FromToRotation(Vector3.right, spawnPoint.right);
        Quaternion prefabRot = bulletPrefab ? bulletPrefab.transform.rotation : Quaternion.identity;
        Quaternion baseRot = basis * prefabRot;

        float half = currentConeAngle * 0.5f;
        int segs = Mathf.Max(1, coneSegments);
        int idx = 0;

        coneRenderer.SetPosition(idx++, center);            // 0: 원점
        Vector3 leftDir = (Quaternion.AngleAxis(-half, Vector3.forward) * Vector3.up);
        leftDir = baseRot * leftDir;
        coneRenderer.SetPosition(idx++, center + leftDir * coneVisualRadius);

        for (int i = 1; i <= segs; i++)
        {
            float t = i / (float)segs;
            float ang = Mathf.Lerp(-half, +half, t);
            Vector3 dir = (Quaternion.AngleAxis(ang, Vector3.forward) * Vector3.up);
            dir = baseRot * dir;
            coneRenderer.SetPosition(idx++, center + dir * coneVisualRadius);
        }

        coneRenderer.SetPosition(idx++, center);            // 마지막 점 = 원점
        if (idx != coneRenderer.positionCount)
            coneRenderer.positionCount = idx;
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // 🧰 오브젝트 풀 유틸
    // ─────────────────────────────────────────────────────────────────────────────
    private GameObject GetFromPoolOrCreate()               // 풀에서 비활성 객체 찾기 또는 새로 생성
    {
        for (int i = 0; i < poolParent.childCount; i++)
        {
            var child = poolParent.GetChild(i).gameObject;
            if (!child.activeSelf) return child;
        }
        var go = Instantiate(bulletPrefab, poolParent);
        go.SetActive(false);
        return go;
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()                    // 에디터에서 선택 시 간단 Gizmos 표시
    {
        if (!visualizeCone || !spawnPoint) return;

        Gizmos.color = new Color(1f, 0.7f, 0.1f, 0.75f);
        Vector3 center = spawnPoint.position;

        Quaternion basis = Quaternion.FromToRotation(Vector3.right, spawnPoint.right);
        Quaternion prefabRot = bulletPrefab ? bulletPrefab.transform.rotation : Quaternion.identity;
        Quaternion baseRot = basis * prefabRot;

        float half = (Application.isPlaying ? currentConeAngle : AccuracyToConeAngle(baseAccuracy)) * 0.5f;

        Vector3 leftDir = baseRot * (Quaternion.AngleAxis(-half, Vector3.forward) * Vector3.up);
        Vector3 rightDir = baseRot * (Quaternion.AngleAxis(+half, Vector3.forward) * Vector3.up);

        Gizmos.DrawLine(center, center + leftDir * coneVisualRadius);
        Gizmos.DrawLine(center, center + rightDir * coneVisualRadius);

        Gizmos.color = new Color(0.2f, 0.8f, 1f, 0.75f);
        Vector3 midDir = baseRot * Vector3.up;
        Gizmos.DrawLine(center, center + midDir * coneVisualRadius);
    }
#endif
}
