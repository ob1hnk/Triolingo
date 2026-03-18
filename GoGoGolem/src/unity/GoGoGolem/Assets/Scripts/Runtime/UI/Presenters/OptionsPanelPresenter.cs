using UnityEngine;
using Yarn.Unity;

/// <summary>
/// 선택지가 있을 때만 OptionsPanel을 활성화하는 Presenter.
/// LinePresenter가 마지막 대사 후 DialoguePanel을 페이드아웃하므로,
/// 선택지 표시 시 DialoguePanel을 다시 복원한다.
/// </summary>
public class OptionsPanelPresenter : DialoguePresenterBase
{
    [SerializeField] private GameObject optionsPanel;
    [SerializeField] private CanvasGroup dialoguePanelCanvasGroup;

    private void Awake()
    {
        if (optionsPanel != null)
            optionsPanel.SetActive(false);
    }

    public override YarnTask OnDialogueStartedAsync()
    {
        if (optionsPanel != null)
            optionsPanel.SetActive(false);
        return YarnTask.CompletedTask;
    }

    public override YarnTask RunLineAsync(LocalizedLine line, LineCancellationToken token)
    {
        if (optionsPanel != null)
            optionsPanel.SetActive(false);
        return YarnTask.CompletedTask;
    }

    public override YarnTask<DialogueOption?> RunOptionsAsync(DialogueOption[] dialogueOptions, LineCancellationToken cancellationToken)
    {
        if (optionsPanel != null)
            optionsPanel.SetActive(true);

        if (dialoguePanelCanvasGroup != null)
            dialoguePanelCanvasGroup.alpha = 1f;

        return YarnTask<DialogueOption?>.FromResult(null);
    }

    public override YarnTask OnDialogueCompleteAsync()
    {
        if (optionsPanel != null)
            optionsPanel.SetActive(false);
        return YarnTask.CompletedTask;
    }
}