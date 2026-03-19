using System.Collections;
using TMPro;
using UnityEngine;
using Yarn.Unity;

/// <summary>
/// 대화 중 화자명 '주인공'을 저장된 플레이어 이름으로 교체한다.
/// DialogueRunner에 등록된 다른 Presenter(LinePresenter)와 함께 동작한다.
/// </summary>
public class PlayerNamePresenter : DialoguePresenterBase
{
    [SerializeField] private TMP_Text characterNameText;

    private const string PlayerCharacterKey = "주인공";

    public override YarnTask RunLineAsync(LocalizedLine line, LineCancellationToken token)
    {
        if (line.CharacterName == PlayerCharacterKey && GameManager.Instance != null && GameManager.Instance.HasPlayerName)
            StartCoroutine(ReplaceNameNextFrame());

        return YarnTask.CompletedTask;
    }

    private IEnumerator ReplaceNameNextFrame()
    {
        yield return null;
        if (characterNameText != null)
            characterNameText.text = GameManager.Instance.PlayerName;
    }

    public override YarnTask<DialogueOption?> RunOptionsAsync(DialogueOption[] dialogueOptions, LineCancellationToken cancellationToken)
        => YarnTask<DialogueOption?>.FromResult(null);

    public override YarnTask OnDialogueStartedAsync() => YarnTask.CompletedTask;

    public override YarnTask OnDialogueCompleteAsync() => YarnTask.CompletedTask;
}
