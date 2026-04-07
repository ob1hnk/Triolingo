using TMPro;
using UnityEngine;
using Yarn.Unity;

/// <summary>
/// [DEPRECATED] 이름 치환은 DialogueUIView.ResolveSpeaker()가 담당합니다.
/// DialogueRunner의 dialoguePresenters 목록에서 이 컴포넌트를 제거하고 삭제해도 됩니다.
/// </summary>
public class PlayerNamePresenter : DialoguePresenterBase
{
    [SerializeField] private TMP_Text characterNameText;

    public override YarnTask RunLineAsync(LocalizedLine line, LineCancellationToken token)
        => YarnTask.CompletedTask;

    public override YarnTask<DialogueOption?> RunOptionsAsync(DialogueOption[] dialogueOptions, LineCancellationToken cancellationToken)
        => YarnTask<DialogueOption?>.FromResult(null);

    public override YarnTask OnDialogueStartedAsync() => YarnTask.CompletedTask;

    public override YarnTask OnDialogueCompleteAsync() => YarnTask.CompletedTask;
}
