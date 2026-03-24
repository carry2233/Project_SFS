using TMPro;                              // TMP 텍스트용
using UnityEngine;                        // 유니티 기본 네임스페이스
using UnityEngine.UI;                     // Image 등 UI용

[AddComponentMenu("Crafting/Required Item Slot UI")]
public class RequiredItemSlotUI : MonoBehaviour // 필요 재료 슬롯 하나를 표시하는 UI 컴포넌트
{
    [Header("UI 참조")]
    public Image iconImage;                       // 필요 재료 아이콘 이미지
    public TextMeshProUGUI infoText;             // "[이름 현재/필요]" 형식의 텍스트

    [Header("색상 설정")]
    public Color enoughColor = Color.white;      // 재료가 충분할 때 텍스트 색
    public Color notEnoughColor = Color.red;     // 재료가 부족할 때 텍스트 색

    private CraftingRequiredItemEntry _required; // 이 슬롯이 표현하는 필요 재료 정의
    private int _haveCount;                      // 현재 인벤토리 보유 수량
    private int _needCount;                      // 필요한 수량

    public CraftingRequiredItemEntry Required => _required; // 외부에서 필요 재료 정의를 읽을 수 있는 프로퍼티

    public void SetData( // 슬롯 초기화: 필요 재료 정의와 현재 보유 수량을 주입
        CraftingRequiredItemEntry required,
        int haveCount)
    {
        _required = required;                   // 필요 재료 정의 저장
        _haveCount = haveCount;                 // 현재 보유 수량 저장
        _needCount = (required != null) ? required.requiredCount : 0; // 필요 수량 설정

        RefreshUI();                            // UI 갱신
    }

    public void RefreshAmount(int newHaveCount) // 현재 보유 수량만 변경하고 UI를 다시 갱신
    {
        _haveCount = newHaveCount;              // 보유 수량 갱신
        RefreshUI();                            // UI 다시 그리기
    }

    private void RefreshUI() // 내부용: 아이콘/텍스트/색상 갱신
    {
        if (_required == null)
        {
            if (iconImage != null)
            {
                iconImage.sprite = null;        // 아이콘 초기화
                iconImage.enabled = false;      // 이미지 숨김
            }

            if (infoText != null)
            {
                infoText.text = string.Empty;   // 텍스트 비우기
                infoText.color = enoughColor;   // 기본 색상으로 설정
            }

            return;
        }

        // 아이콘 설정
        if (iconImage != null)
        {
            iconImage.sprite = _required.icon;          // 필요 재료 아이콘 설정
            iconImage.enabled = (_required.icon != null); // 아이콘 유무에 따라 표시/숨김
        }

        // 텍스트 구성: "[이름 현재/필요]" 형식
        if (infoText != null)
        {
            string name = string.IsNullOrWhiteSpace(_required.displayName)
                ? $"Item {_required.typeId}:{_required.itemId}" // 이름이 비어 있으면 타입/아이디로 대체
                : _required.displayName;                        // 이름 사용

            infoText.text = $"{name}\n{_haveCount} / {_needCount}"; // "[이름 현재/필요]" 표시

            // 색상: 충분하면 흰색, 부족하면 빨간색
            bool enough = (_haveCount >= _needCount);             // 충분 여부 계산
            infoText.color = enough ? enoughColor : notEnoughColor; // 조건에 따라 색상 선택
        }
    }
}
