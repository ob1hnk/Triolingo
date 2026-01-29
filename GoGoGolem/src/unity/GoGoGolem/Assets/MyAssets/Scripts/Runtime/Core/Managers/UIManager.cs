using UnityEngine;
using MyAssets.UI.Presenters;

public class UIManager : MonoBehaviour
{
    [SerializeField] private InventoryUIPresenter inventoryPresenter;
    public InventoryUIPresenter Inventory => inventoryPresenter;

    private void Start()
    {
        if (inventoryPresenter == null)
        {
            Debug.LogError("UIManager: InventoryUIPresenter가 할당되지 않았습니다.");
            return;
        }
        
        // 상태 변화 구독
        GameStateManager.Instance.OnStateChanged += OnGameStateChanged;
        
        // 초기 상태 설정
        inventoryPresenter.Hide();
    }

    private void OnGameStateChanged(GameState oldState, GameState newState)
    {
        switch (newState)
        {
            case GameState.InventoryUI:
                HandleInventoryOpen();
                break;
                
            case GameState.Gameplay:
                if (oldState == GameState.InventoryUI)
                {
                    HandleInventoryClose();
                }
                break;
        }
    }

    private void HandleInventoryOpen()
    {
        Time.timeScale = 0f;
        inventoryPresenter.Show();
    }

    private void HandleInventoryClose()
    {
        Time.timeScale = 1f;
        inventoryPresenter.Hide();
    }

    private void OnDestroy()
    {
        if (GameStateManager.Instance != null)
        {
            GameStateManager.Instance.OnStateChanged -= OnGameStateChanged;
        }
    }
}