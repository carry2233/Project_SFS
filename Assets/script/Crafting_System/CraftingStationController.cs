using UnityEngine;                    // 유니티 기본 네임스페이스

[AddComponentMenu("Crafting/Crafting Station Controller")]
public class CraftingStationController : MonoBehaviour // 제작대 상호작용을 관리하는 컴포넌트
{
    [Header("데이터")]
    public CraftingRecipeList recipeList;        // 이 제작대에서 사용할 제작 레시피 리스트(ScriptableObject)

    [Header("UI 참조")]
    public CraftingUIController craftingUI;      // 제작 UI를 제어하는 컨트롤러 참조

    [Header("UI 자동 찾기 설정")]
    public string craftingUITag = "CraftingUI";  // 씬에서 CraftingUIController를 찾을 때 사용할 태그 이름

    [Header("상호작용 설정")]
    public KeyCode interactionKey = KeyCode.P;   // 제작 UI 열기/닫기용 상호작용 키

    private bool _playerInRange;                // 플레이어가 제작대 범위 안에 있는지 여부
    private bool _isOpenThisStation;            // 현재 이 제작대 기준으로 UI가 열려 있는지 여부

    private void Awake() // 오브젝트가 활성화될 때(UI 자동 참조 시도)
    {
        // 인스펙터에서 수동으로 craftingUI를 넣지 않았다면, 태그 기반으로 자동 검색
        if (craftingUI == null && !string.IsNullOrEmpty(craftingUITag))
        {
            GameObject uiObj = GameObject.FindGameObjectWithTag(craftingUITag); // 지정 태그를 가진 오브젝트 찾기
            if (uiObj != null)
            {
                craftingUI = uiObj.GetComponent<CraftingUIController>();         // 해당 오브젝트에서 CraftingUIController 가져오기
                if (craftingUI == null)
                {
                    Debug.LogWarning(
                        $"[CraftingStationController] 태그 '{craftingUITag}'를 가진 오브젝트에서 CraftingUIController를 찾지 못했습니다.",
                        uiObj
                    );
                }
            }
            else
            {
                Debug.LogWarning(
                    $"[CraftingStationController] 태그 '{craftingUITag}'를 가진 오브젝트를 찾지 못했습니다."
                );
            }
        }
    }

    private void Reset() // 컴포넌트 추가 시 기본값 설정
    {
        interactionKey = KeyCode.P;             // 기본 상호작용 키를 P로 설정
        craftingUITag = "CraftingUI";          // 기본 UI 태그 이름을 CraftingUI로 설정
    }

    private void Update() // 매 프레임 상호작용 키 입력을 감지
    {
        if (!_playerInRange) return;            // 플레이어가 범위 안에 없으면 무시
        if (!craftingUI) return;                // UI 참조가 없으면 무시
        if (recipeList == null) return;         // 레시피 리스트가 없으면 무시

        if (Input.GetKeyDown(interactionKey))   // 상호작용 키가 눌렸을 때
        {
            if (_isOpenThisStation)             // 이미 이 제작대 기준으로 열려 있다면
            {
                craftingUI.CancelAndClose();    // 진행 중 제작 취소 + UI 닫기
                _isOpenThisStation = false;     // 열림 상태 플래그 초기화
            }
            else
            {
                craftingUI.Open(this, recipeList); // 이 제작대와 레시피 리스트를 넘겨 UI 열기
                _isOpenThisStation = true;         // 열림 상태 플래그 설정
            }
        }
    }

    private void OnTriggerEnter2D(Collider2D other) // 2D 트리거 진입 시 플레이어 감지
    {
        if (other.CompareTag("Player"))         // Player 태그를 가진 콜라이더인지 확인
        {
            _playerInRange = true;             // 범위 안 상태로 설정
        }
    }

    private void OnTriggerExit2D(Collider2D other) // 2D 트리거에서 이탈 시 처리
    {
        if (other.CompareTag("Player"))         // Player 태그를 가진 콜라이더인지 확인
        {
            _playerInRange = false;            // 범위 밖 상태로 설정
            if (_isOpenThisStation && craftingUI) // 이 제작대 기준으로 UI가 열려 있다면
            {
                craftingUI.CancelAndClose();   // 제작 취소 + UI 닫기
            }
            _isOpenThisStation = false;        // 열림 상태 플래그 초기화
        }
    }

    private void OnTriggerEnter(Collider other) // 3D 트리거 진입 시 플레이어 감지(3D용 선택사항)
    {
        if (other.CompareTag("Player"))         // Player 태그를 가진 콜라이더인지 확인
        {
            _playerInRange = true;             // 범위 안 상태로 설정
        }
    }

    private void OnTriggerExit(Collider other) // 3D 트리거 이탈 시 처리(3D용 선택사항)
    {
        if (other.CompareTag("Player"))         // Player 태그를 가진 콜라이더인지 확인
        {
            _playerInRange = false;            // 범위 밖 상태로 설정
            if (_isOpenThisStation && craftingUI) // 이 제작대 기준으로 UI가 열려 있다면
            {
                craftingUI.CancelAndClose();   // 제작 취소 + UI 닫기
            }
            _isOpenThisStation = false;        // 열림 상태 플래그 초기화
        }
    }
}
