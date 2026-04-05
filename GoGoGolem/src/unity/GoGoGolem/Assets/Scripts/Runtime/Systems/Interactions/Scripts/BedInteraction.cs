using System;
using UnityEngine;
using UnityEngine.Playables;

/// <summary>
/// 침대 상호작용 - 잠들기
///
/// 사용법:
///   1. Bed 오브젝트에 이 컴포넌트 + Collider 추가
///   2. Layer를 Interactable로 설정
///   3. sleepDirector에 PlayableDirector를 연결 (타임라인 재생용)
///   4. blockedBark에 BarkTrigger 연결 (블록 상태에서 출력할 대사)
///   5. RoomStateManager가 OnSlept 이벤트를 구독하여 학 귀환 애니메이션 트리거
///
/// 초기 상태: Collider는 비활성(낮에는 잠들기 불가)
/// RoomStateManager가 낮→밤 전환 후 Collider를 활성화
/// </summary>
public class BedInteraction : MonoBehaviour, IInteractable
{
    [Header("Prompt")]
    [SerializeField] private InteractionPromptData promptData;

    [Header("Timeline")]
    [SerializeField] private PlayableDirector sleepDirector;

    [Header("Blocked Bark")]
    [Tooltip("블록 상태에서 Interact 시 발화할 BarkTrigger")]
    [SerializeField] private BarkTrigger blockedBark;

    public event Action OnSlept;

    private bool _isPlaying;
    private bool _canInteract = true;
    private bool _isBlocked;

    public InteractionType InteractionType => InteractionType.Sleep;
    public bool CanInteract => _canInteract && !_isPlaying;

    public void SetCanInteract(bool value) => _canInteract = value;

    /// <summary>
    /// 블록 상태 설정. true이면 Interact 시 blockedBark가 발화되고 잠들기가 차단됨.
    /// </summary>
    public void SetBlocked(bool value)
    {
        _isBlocked = value;
        if (!value && blockedBark != null) blockedBark.ResetTrigger();
    }

    public string GetActionLabel() => promptData != null ? promptData.ActionLabel : "자러가기";
    public Sprite GetKeyHintSprite() => promptData != null ? promptData.KeyHintSprite : null;
    public Vector3 GetPromptOffset() => promptData != null ? promptData.WorldOffset : new Vector3(0f, 1.5f, 0f);

    public void Interact()
    {
        if (_isPlaying) return;

        if (_isBlocked)
        {
            if (blockedBark != null) blockedBark.Fire();
            return;
        }

        if (sleepDirector != null)
        {
            _isPlaying = true;
            sleepDirector.stopped += OnTimelineStopped;
            sleepDirector.Play();
        }
        else
        {
            OnSlept?.Invoke();
        }
    }

    private void OnTimelineStopped(PlayableDirector director)
    {
        director.stopped -= OnTimelineStopped;
        _isPlaying = false;
        OnSlept?.Invoke();
    }
}