using UnityEngine;                 // 유니티 기본 네임스페이스
using UnityEngine.UI;              // UI(Button) 사용

[AddComponentMenu("UI/Quit Game Button")]               // 인스펙터 메뉴 경로
[DisallowMultipleComponent]                              // 중복 부착 방지
public class QuitGameButton : MonoBehaviour
{
    [Header("종료 버튼 할당")]
    [SerializeField] private Button quitButton;          // ▶ 게임 종료를 실행할 UI 버튼(인스펙터에서 할당)

    private void Reset()                                 // ▶ 에디터에서 컴포넌트 추가 시 기본 참조 시도
    {
        if (!quitButton) quitButton = GetComponent<Button>();
    }

    private void OnEnable()                              // ▶ 활성화 시 버튼 클릭 이벤트 등록
    {
        if (quitButton != null)
            quitButton.onClick.AddListener(OnQuitButtonClicked);
    }

    private void OnDisable()                             // ▶ 비활성화 시 버튼 클릭 이벤트 해제
    {
        if (quitButton != null)
            quitButton.onClick.RemoveListener(OnQuitButtonClicked);
    }

    public void OnQuitButtonClicked()                    // ▶ 버튼 클릭 시 호출될 메서드(인스펙터 OnClick에 직접 연결 가능)
    {
#if UNITY_EDITOR
        // 에디터 실행 중에는 플레이 모드만 종료
        UnityEditor.EditorApplication.isPlaying = false; // ▶ 에디터 플레이 종료
#else
        Application.Quit();                              // ▶ 빌드된 게임 종료
#endif
    }
}
