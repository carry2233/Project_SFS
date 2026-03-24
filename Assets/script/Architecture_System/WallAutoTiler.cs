using UnityEngine;                 // ✅ Unity 기본
using UnityEngine.Tilemaps;        // ✅ Tilemap API
using System.Collections.Generic;  // ✅ List

// ─────────────────────────────────────────────────────────────
// [추가] 동일 파일에 인터페이스를 함께 선언 (네임스페이스 없이 전역)
// ─────────────────────────────────────────────────────────────
public interface IWallAutoTiler
{
    void RefreshAround(Vector3Int center);   // ✅ 중심 셀 기준 주변 갱신
}

[AddComponentMenu("Build/Walls/Wall Auto Tiler")]
public class WallAutoTiler : MonoBehaviour, IWallAutoTiler
{
    [Header("참조")]
    [SerializeField] private Tilemap targetTilemap;        // ✅ 규칙 적용 대상 타일맵
    [SerializeField] private WallNeighborRuleSet ruleSet;  // ✅ 이웃 규칙 SO

    // 8방 오프셋
    private static readonly Vector3Int[] Dirs =
    {
        new(0, 1, 0),    // up
        new(0,-1, 0),    // down
        new(-1,0, 0),    // left
        new(1, 0, 0),    // right
        new(-1,1, 0),    // upLeft
        new(1, 1, 0),    // upRight
        new(-1,-1,0),    // downLeft
        new(1,-1, 0),    // downRight
    };

    public void RefreshAround(Vector3Int center)  // ✅ 자신+8방 규칙 적용
    {
        if (!targetTilemap || !ruleSet) return;  // 가드
        ApplyAt(center);                         // 중심
        for (int i = 0; i < Dirs.Length; i++)    // 8방
            ApplyAt(center + Dirs[i]);
    }

    private void ApplyAt(Vector3Int cell)        // ✅ 단일 셀 규칙 적용
    {
        if (!targetTilemap) return;                             // 가드
        var curTile = targetTilemap.GetTile(cell);              // 현재 타일
        if (!curTile) return;                                   // 비어있으면 패스

        bool[] neigh = ReadNeighborFlags(cell);                 // 8방 존재여부 배열
        var rule = FindFirstMatch(neigh);                       // 첫 매칭 규칙
        if (rule == null) return;                               // 매칭 없으면 유지

        if (rule.applyTile && curTile != rule.applyTile)        // 타일 변경
            targetTilemap.SetTile(cell, rule.applyTile);

        // 회전(항상 갱신)
        var rot = Quaternion.Euler(0, 0, rule.applyRotationZ);  // Z 회전
        targetTilemap.SetTransformMatrix(cell, Matrix4x4.TRS(Vector3.zero, rot, Vector3.one));
        targetTilemap.RefreshTile(cell);                        // 타일 리프레시
    }

    private bool[] ReadNeighborFlags(Vector3Int cell) // ✅ 8방 플래그 읽기
    {
        bool HasWall(Vector3Int c)
        {
            var t = targetTilemap.GetTile(c);                          // 해당 칸 타일
            if (!t) return false;                                      // 없음
            if (ruleSet.wallTiles == null || ruleSet.wallTiles.Count == 0) return true; // 임의
            return ruleSet.wallTiles.Contains(t);                      // 지정된 벽 타일만 인정
        }

        return new bool[]
        {
            HasWall(cell + Dirs[0]),   // up
            HasWall(cell + Dirs[1]),   // down
            HasWall(cell + Dirs[2]),   // left
            HasWall(cell + Dirs[3]),   // right
            HasWall(cell + Dirs[4]),   // upLeft
            HasWall(cell + Dirs[5]),   // upRight
            HasWall(cell + Dirs[6]),   // downLeft
            HasWall(cell + Dirs[7]),   // downRight
        };
    }

    private WallNeighborRuleSet.Rule FindFirstMatch(bool[] n) // ✅ 규칙 리스트 위에서부터 우선 매칭
    {
        if (ruleSet == null || ruleSet.rules == null || n == null || n.Length < 8)  // 가드
            return null;

        for (int i = 0; i < ruleSet.rules.Count; i++)
        {
            var r = ruleSet.rules[i];      // 현재 규칙
            var m = r.mask;                // 현재 규칙의 8방 마스크

            bool isMatch;
            if (r.ignoreDiagonals)         // (옵션) 대각 이웃 무시 모드
            {
                // 상/하/좌/우만 정확 비교 (대각은 검사하지 않음)
                isMatch =
                    (m.up    == n[0]) &&
                    (m.down  == n[1]) &&
                    (m.left  == n[2]) &&
                    (m.right == n[3]);
            }
            else
            {
                // 8방 모두 정확 일치 비교
                isMatch =
                    (m.up        == n[0]) &&
                    (m.down      == n[1]) &&
                    (m.left      == n[2]) &&
                    (m.right     == n[3]) &&
                    (m.upLeft    == n[4]) &&
                    (m.upRight   == n[5]) &&
                    (m.downLeft  == n[6]) &&
                    (m.downRight == n[7]);
            }

            if (isMatch) return r;         // 첫 매칭 규칙 반환
        }

        return null;                       // 매칭 없음
    }
}
