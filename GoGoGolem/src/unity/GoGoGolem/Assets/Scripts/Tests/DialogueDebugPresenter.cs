using UnityEngine;
using Yarn.Unity;

/// <summary>
/// 대화 시스템 디버그용 Presenter.
/// DialogueRunner의 Dialogue Views에 등록해두면 각 단계를 로그로 확인할 수 있다.
/// </summary>
public class DialogueDebugPresenter : DialoguePresenterBase
{
    public override YarnTask OnDialogueStartedAsync()
    {
        Debug.Log("[DialogueDebug] OnDialogueStartedAsync");
        return YarnTask.CompletedTask;
    }

    public override YarnTask RunLineAsync(LocalizedLine line, LineCancellationToken token)
    {
        Debug.Log($"[DialogueDebug] RunLineAsync | TextID={line.TextID} | Raw=\"{line.RawText}\" | Char=\"{line.CharacterName}\"");
        return YarnTask.CompletedTask;
    }

    public override YarnTask<DialogueOption?> RunOptionsAsync(DialogueOption[] options, LineCancellationToken token)
    {
        Debug.Log($"[DialogueDebug] RunOptionsAsync | {options.Length}개 선택지");
        for (int i = 0; i < options.Length; i++)
            Debug.Log($"  [{i}] {options[i].Line.RawText}");
        return YarnTask<DialogueOption?>.FromResult(null);
    }

    public override YarnTask OnDialogueCompleteAsync()
    {
        Debug.Log("[DialogueDebug] OnDialogueCompleteAsync");
        return YarnTask.CompletedTask;
    }
}