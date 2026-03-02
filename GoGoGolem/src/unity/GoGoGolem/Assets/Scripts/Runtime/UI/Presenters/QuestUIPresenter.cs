using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 퀘스트 로그 UI Presenter.
/// - Global.ToggleQuest (Tab) 입력으로 show/hide를 직접 관리한다.
/// - 퀘스트 시작/완료 이벤트를 받아 View를 갱신한다.
/// - GameState를 변경하지 않으므로 게임이 계속 진행된다.
/// </summary>
public class QuestUIPresenter : MonoBehaviour
{
    [SerializeField] private QuestUIView view;

    [Header("Event Channels")]
    [SerializeField] private QuestGameEvent onQuestStartedEvent;
    [SerializeField] private QuestGameEvent onQuestCompletedEvent;
    [SerializeField] private QuestObjectiveGameEvent onObjectiveCompletedEvent;

    private QuestManager questManager;
    private GameInputActions.GlobalActions _globalActions;
    private bool _initialized = false;
    private bool _isVisible = false;

    private void Awake()
    {
        if (view == null)
            view = GetComponent<QuestUIView>();
    }

    private void Start()
    {
        questManager = FindObjectOfType<QuestManager>();

        if (InputModeController.Instance == null)
        {
            Debug.LogError("[QuestUIPresenter] InputModeController를 찾을 수 없습니다.");
            return;
        }

        _globalActions = InputModeController.Instance.GetGlobalActions();
        _globalActions.ToggleQuest.performed += OnToggleQuest;
        _initialized = true;

        onQuestStartedEvent?.Register(OnQuestStarted);
        onQuestCompletedEvent?.Register(OnQuestCompleted);
        onObjectiveCompletedEvent?.Register(OnObjectiveCompleted);

        LoadActiveQuests();
        view.Hide();
    }

    private void OnEnable()
    {
        if (!_initialized) return;
        _globalActions.ToggleQuest.performed += OnToggleQuest;
    }

    private void OnDisable()
    {
        if (!_initialized) return;
        _globalActions.ToggleQuest.performed -= OnToggleQuest;
    }

    private void OnDestroy()
    {
        onQuestStartedEvent?.Unregister(OnQuestStarted);
        onQuestCompletedEvent?.Unregister(OnQuestCompleted);
        onObjectiveCompletedEvent?.Unregister(OnObjectiveCompleted);
    }

    private void OnToggleQuest(InputAction.CallbackContext ctx)
    {
        if (_isVisible)
        {
            Hide();
            return;
        }

        // 다른 UI(인벤토리, 대화)가 열려 있을 때는 열지 않는다
        if (GameStateManager.Instance.CurrentState != GameState.Gameplay) return;

        Show();
    }

    public void Show()
    {
        _isVisible = true;
        view.Show();
    }

    public void Hide()
    {
        _isVisible = false;
        view.Hide();
    }

    public void Toggle()
    {
        if (_isVisible) Hide();
        else if (GameStateManager.Instance.CurrentState == GameState.Gameplay) Show();
    }

    private void OnQuestStarted(Quest quest)
    {
        view.AddQuestEntry(quest.QuestID, quest.QuestType, quest.QuestName, quest.GetAllObjectives());
    }

    private void OnQuestCompleted(Quest quest)
    {
        view.RemoveQuestEntry(quest.QuestID);
    }

    private void OnObjectiveCompleted(QuestObjective objective)
    {
        view.UpdateObjectiveCompleted(objective.ObjectiveID);
    }

    private void LoadActiveQuests()
    {
        if (questManager == null) return;
        foreach (var quest in questManager.GetAllActiveQuests())
            OnQuestStarted(quest);
    }
}
