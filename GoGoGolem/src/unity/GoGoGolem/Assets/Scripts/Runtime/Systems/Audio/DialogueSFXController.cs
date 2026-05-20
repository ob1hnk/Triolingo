using System.Collections.Generic;
using UnityEngine;
using Yarn.Unity;

/// <summary>
/// Yarn 대화 중 효과음 재생/정지.
/// DialogueRunner의 Dialogue Presenters 리스트에 추가 필요.
///
/// Yarn 명령어:
///   <<play_sfx "clipName">>  — 효과음 루프 재생
///   <<stop_sfx>>             — 즉시 정지
///
/// 대화를 넘기면 다음 라인 시작 시 자동 정지.
/// </summary>
public class DialogueSFXController : DialoguePresenterBase
{
    [System.Serializable]
    struct NamedClip
    {
        public string name;
        public AudioClip clip;
    }

    [SerializeField] AudioSource audioSource;
    [SerializeField] List<NamedClip> clips;
    [SerializeField] DialogueRunner dialogueRunner;

    bool _sfxJustStarted;

    void Awake()
    {
        dialogueRunner.AddCommandHandler<string>("play_sfx", PlaySFX);
        dialogueRunner.AddCommandHandler("stop_sfx", StopSFX);
    }

    public void PlaySFX(string clipName)
    {
        var entry = clips.Find(c => c.name == clipName);
        if (entry.clip == null)
        {
            Debug.LogWarning($"[DialogueSFXController] 클립을 찾을 수 없습니다: {clipName}");
            return;
        }
        audioSource.clip = entry.clip;
        audioSource.loop = false;
        audioSource.Play();
        _sfxJustStarted = true;
    }

    public void StopSFX()
    {
        if (audioSource.isPlaying) audioSource.Stop();
        _sfxJustStarted = false;
    }

    public override YarnTask RunLineAsync(LocalizedLine line, LineCancellationToken token)
    {
        if (_sfxJustStarted)
            _sfxJustStarted = false; // play_sfx 직후 라인 — SFX 유지
        else
            StopSFX();               // 그 다음 라인부터 — SFX 정지

        return YarnTask.CompletedTask;
    }

    public override YarnTask<DialogueOption?> RunOptionsAsync(DialogueOption[] options, LineCancellationToken token)
    {
        StopSFX();
        return YarnTask<DialogueOption?>.FromResult((DialogueOption?)null);
    }

    public override YarnTask OnDialogueStartedAsync()
        => YarnTask.CompletedTask;

    public override YarnTask OnDialogueCompleteAsync()
    {
        StopSFX();
        return YarnTask.CompletedTask;
    }
}
