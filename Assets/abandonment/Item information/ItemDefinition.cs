using UnityEngine;

[System.Serializable] // 인스펙터에 인라인으로 펼쳐 입력 가능
public class ItemCommonData
{
    [Header("식별/분류")]
    public int itemId;                    // 아이템 식별용 ID(번역/DB 조회 키, 선택)
    public int typeId;                    // 아이템 종류 ID(무기/도구/자원 등 분류)

    [Header("표시(폴백)")]
    public string fallbackDisplayName;    // DB 미사용/미등록 시 사용할 표시 이름
    public Sprite fallbackIcon;           // DB 미사용/미등록 시 사용할 아이콘
}
