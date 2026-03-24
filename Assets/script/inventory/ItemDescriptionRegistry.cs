using System;                           // Serializable 특성용 네임스페이스
using System.Collections.Generic;       // List<T> 사용
using TMPro;                            // TMP 텍스트용 네임스페이스
using UnityEngine;                     // ScriptableObject 기본 네임스페이스

[CreateAssetMenu(
    fileName = "ItemDescriptionRegistry",
    menuName = "Item/Item Description Registry (아이템 설명 레지스트리)")]
public class ItemDescriptionRegistry : ScriptableObject
{
    [Serializable]
    public class Entry                      // 타입/아이템ID별 설명 데이터를 담는 클래스
    {
        [Header("식별 키")]
        public int typeId;                  // 아이템 종류 ID
        public int itemId;                  // 아이템 ID

        [Header("설명 텍스트 프리팹")]
        public TMP_Text descriptionPrefab;  // 설명이 적혀 있는 TMP 텍스트 프리팹(또는 참조)
    }

    [Header("설명 리스트")]
    public List<Entry> entries = new();     // (typeId,itemId)별 설명 데이터 리스트

    /// <summary>
    /// typeId, itemId에 해당하는 설명 텍스트를 찾아 문자열로 반환하는 메서드.
    /// </summary>
    public bool TryGetDescriptionText(int typeId, int itemId, out string text) // 아이템 키로 설명 텍스트를 찾는 메서드
    {
        // 기본값 설정
        text = string.Empty;                          // 기본 빈 문자열

        if (entries == null || entries.Count == 0)    // 리스트가 비어있으면 실패
            return false;

        // 순회하면서 첫 매칭 항목 찾기
        for (int i = 0; i < entries.Count; i++)
        {
            var e = entries[i];                       // 현재 엔트리
            if (e == null) continue;                  // null 엔트리 스킵

            if (e.typeId == typeId && e.itemId == itemId) // 타입/아이템ID 일치 검사
            {
                if (e.descriptionPrefab != null)      // TMP 텍스트 프리팹이 있는 경우
                {
                    text = e.descriptionPrefab.text;  // 프리팹에 적힌 텍스트를 그대로 가져오기
                }
                else
                {
                    text = string.Empty;              // 프리팹이 없으면 빈 문자열
                }

                return true;                          // 매칭 성공
            }
        }

        return false;                                 // 매칭 실패
    }
}
