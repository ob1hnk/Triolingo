using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;

/// <summary>
/// 월드에 배치된 아이템에 "바람 스킬"을 시전하는 상호작용.
///
/// 사용법:
///   1. 바람 스킬을 시전할 수 있는 프리팹(예: 배치된 식량 꾸러미)에 추가
///   2. Layer를 Interactable로 설정
///   3. Collider가 있어야 PlayerInteraction의 OverlapSphere에 감지됨
///   4. On Interact UnityEvent에 씬 전용 컨트롤러의 메소드를 연결
///      (예: ForestQuestController.OnWindSkillUsed)
/// </summary>
public class WindSkillInteractable : MonoBehaviour, IInteractable
{
    [Header("Prompt")]
    [SerializeField] private InteractionPromptData promptData;

    [Header("On Interact")]
    [Tooltip("상호작용 시 실행할 동작. 씬 전용 컨트롤러의 메소드를 연결한다. 씬 로드 직전에 invoke된다.")]
    [SerializeField] private UnityEvent onInteract;

    [Header("Scene Transition")]
    [Tooltip("상호작용 후 로드할 씬 이름. 비워두면 씬 전환 없음.")]
    [SerializeField] private string targetSceneName = "Gesture Detection Wind";

    public InteractionType InteractionType => InteractionType.UseWindSkill;
    public string GetActionLabel() => promptData != null ? promptData.ActionLabel : "바람 스킬 사용";
    public Sprite GetKeyHintSprite() => promptData != null ? promptData.KeyHintSprite : null;

    public void Interact()
    {
        onInteract?.Invoke();

        if (!string.IsNullOrEmpty(targetSceneName))
            SceneManager.LoadScene(targetSceneName);
    }
}
