using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

[CreateAssetMenu(menuName = "Build/Walls/Wall Neighbor Rule Set", fileName = "WallNeighborRuleSet")]
public class WallNeighborRuleSet : ScriptableObject
{
    [Serializable]
    public struct NeighborMask                                       // 8방 이웃 마스크
    {
        public bool up;                                              // 상
        public bool down;                                            // 하
        public bool left;                                            // 좌
        public bool right;                                           // 우
        public bool upLeft;                                          // 좌상
        public bool upRight;                                         // 우상
        public bool downLeft;                                        // 좌하
        public bool downRight;                                       // 우하
    }

    [Serializable]
    public class Rule                                                 // 규칙 한 칸
    {
        public NeighborMask mask;                                     // 8방 체크 마스크
        public float applyRotationZ;                                   // 적용 회전(Z축 도)
        public TileBase applyTile;                                     // 적용 타일
        public bool ignoreDiagonals;                                    // 대각 이웃 무시
    }

    [Header("규칙 리스트 (위에서부터 우선매칭)")]
    public List<Rule> rules = new();                                   // 규칙 목록

    [Header("벽으로 취급할 타일(없으면 '타일이 있음'만으로 판단)")]
    public List<TileBase> wallTiles = new();                            // 벽 타일 집합(선택)
}
