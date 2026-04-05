using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 씬 전환 상호작용
///
/// 사용법:
///   1. 오브젝트에 이 컴포넌트 + Collider 추가
///   2. Layer를 Interactable로 설정
///   3. Inspector에서 sceneName, actionLabel 설정
/// </summary>
public class ChangeSceneInteraction : MonoBehaviour, IInteractable
{
    [Header("Scene")]
    [SerializeField] private string sceneName;

    [Header("Prompt")]
    [SerializeField] private InteractionPromptData promptData;

    public InteractionType InteractionType => InteractionType.ChangeScene;
    public bool CanInteract => !string.IsNullOrEmpty(sceneName);

    public string GetActionLabel() => promptData != null ? promptData.ActionLabel : "이동하기";
    public Sprite GetKeyHintSprite() => promptData != null ? promptData.KeyHintSprite : null;
    public Vector3 GetPromptOffset() => promptData != null ? promptData.WorldOffset : new Vector3(0f, 1.5f, 0f);

    public void Interact()
    {
        if (string.IsNullOrEmpty(sceneName))
        {
            Debug.LogError("[ChangeSceneInteraction] sceneName이 설정되지 않았습니다.");
            return;
        }

        Debug.Log($"[ChangeSceneInteraction] 씬 전환: {sceneName}");
        SceneManager.LoadScene(sceneName);
    }
}