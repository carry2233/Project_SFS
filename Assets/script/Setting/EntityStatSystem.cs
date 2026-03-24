using System.Collections.Generic;
using UnityEngine;

public class EntityStatSystem : MonoBehaviour
{
    [Header("Entity Stat List (Copied)")]
    [SerializeField]
    private List<StatSaveManager.MirrorEntry> entityStats = new();

    [Header("Linked Sub Stat Systems")]
    [SerializeField] private MeleeStatSystem meleeStatSystem;
    [SerializeField] private RangedStatSystem rangedStatSystem;

    private void Awake()
    {
        DontDestroyOnLoad(gameObject);

        CopyFromStatSaveManager();   // 저장 스탯 전체 복사
        NotifySubSystems();          // ✅ 방법 A: 직접 신호
    }

    private void CopyFromStatSaveManager()
    {
        StatSaveManager saveManager =
            FindObjectOfType<StatSaveManager>(true); // 비활성/DontDestroy 포함

        if (saveManager == null) return;

        entityStats.Clear();

        foreach (var entry in saveManager.GetSavedStatList())
        {
            entityStats.Add(entry.DeepCopy());       // ✅ 반드시 Deep Copy
        }
    }

    private void NotifySubSystems()
    {
        if (meleeStatSystem != null)
            meleeStatSystem.CopyFromEntity(this);

        if (rangedStatSystem != null)
            rangedStatSystem.CopyFromEntity(this);
    }

    // ─────────────────────────────────────────
    // 단일 스탯 조회용
    // ─────────────────────────────────────────
    public bool TryGetStat(
        int keyPrimary,
        int keySecondary,
        out StatSaveManager.MirrorEntry result)
    {
        foreach (var stat in entityStats)
        {
            if (stat.keyPrimary == keyPrimary &&
                stat.keySecondary == keySecondary)
            {
                result = stat.DeepCopy();             // 단일도 Deep Copy
                return true;
            }
        }

        result = default;
        return false;
    }
}
