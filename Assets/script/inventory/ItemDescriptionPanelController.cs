using TMPro;                            // TMP 텍스트용 네임스페이스
using UnityEngine;                     // 유니티 기본 네임스페이스
using UnityEngine.UI;                  // UI 버튼용 네임스페이스

[AddComponentMenu("UI/Item Description Panel Controller (아이템 설명창 제어)")]
public class ItemDescriptionPanelController : MonoBehaviour
{
    [Header("UI 참조")]
    public GameObject rootPanel;              // 설명창 UI 루트 오브젝트1 (활성/비활성 토글 대상)
    public TMP_Text descriptionText;          // 설명을 표시할 TMP 텍스트(TMP)1
    public Button closeButton;                // 설명창 닫기 버튼

    [Header("설명 레지스트리")]
    public ItemDescriptionRegistry descriptionRegistry; // 아이템 타입/ID별 설명 텍스트를 가진 ScriptableObject

    [Header("초기 설정")]
    public bool hideOnStart = true;           // 시작 시 설명창을 비활성화 할지 여부

        [Header("아이콘 이미지")]
    public Image itemIconImage;               // 슬롯 아이템 아이콘을 표시할 UI 이미지1

    private void Awake()                      // 초기화 시점에 버튼 이벤트 등록 및 초기 상태 설정
    {
        if (closeButton != null)
        {
            closeButton.onClick.AddListener(Hide);   // 닫기 버튼에 Hide 메서드 등록
        }

        if (hideOnStart)
        {
            SetPanelActive(false);                  // 시작 시 설명창 숨기기
        }
    }

    private void OnDestroy()                 // 오브젝트 파괴 시 버튼 이벤트 해제
    {
        if (closeButton != null)
        {
            closeButton.onClick.RemoveListener(Hide); // 이벤트 리스너 제거
        }
    }

    public void ShowForItem(int typeId, int itemId)   // 슬롯에서 넘겨준 타입/아이템ID로 설명창을 띄우는 메서드
    {
        if (descriptionRegistry == null) return;      // 레지스트리가 없으면 리턴
        if (descriptionText == null) return;          // 텍스트가 없으면 리턴
        if (rootPanel == null) return;                // 루트 패널이 없으면 리턴

        // ScriptableObject에서 설명 텍스트를 가져오기
        if (descriptionRegistry.TryGetDescriptionText(typeId, itemId, out string text))
        {
            descriptionText.text = text;              // 설명 텍스트 복사
        }
        else
        {
            descriptionText.text = string.Empty;      // 매칭 실패 시 빈 텍스트 또는 기본 문구
        }

        SetPanelActive(true);                        // 패널 활성화
    }

    public void Hide()                               // 설명창을 숨기는 메서드 (버튼/외부에서 호출)
    {
        SetPanelActive(false);                       // 패널 비활성화
    }

    public void ShowDescription(int typeId, int itemId) // 슬롯/인벤토리에서 호출할 공개 진입 메서드(텍스트만)
    {
        ShowDescription(typeId, itemId, null);          // 아이콘 없이 텍스트만 표시
    }

    public void ShowDescription(int typeId, int itemId, Sprite iconSprite) // 텍스트 + 아이콘 표시용 메서드
    {
        // 1) 아이콘 이미지 세팅
        if (itemIconImage != null)
        {
            itemIconImage.sprite = iconSprite;          // 넘어온 아이콘 스프라이트 적용
            itemIconImage.enabled = (iconSprite != null); // 아이콘이 있으면 보이게, 없으면 숨김
        }

        // 2) 기존 텍스트 표시 로직 재사용
        ShowForItem(typeId, itemId);                    // ScriptableObject에서 텍스트 찾아서 표시
    }


    public void HidePanel()                            // 인벤토리 UI에서 호출할 패널 숨김 메서드
    {
        Hide();                                        // 기존 Hide 메서드 재사용
    }


    public void ClearText()                          // 설명 텍스트만 지우는 메서드(필요 시 사용)
    {
        if (descriptionText != null)
        {
            descriptionText.text = string.Empty;     // 텍스트 초기화
        }
    }

    public bool IsVisible()                          // 패널의 현재 활성 상태를 반환하는 헬퍼 메서드
    {
        if (rootPanel == null) return false;         // 루트가 없으면 항상 false
        return rootPanel.activeSelf;                 // activeSelf 값 반환
    }

    private void SetPanelActive(bool active)         // 내부용 패널 활성/비활성 처리 메서드
    {
        if (rootPanel != null)
        {
            rootPanel.SetActive(active);             // 루트 패널 활성/비활성
        }
    }
}
