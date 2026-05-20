using System;
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

    [Header("Bark (선택)")]
    [Tooltip("설정하면 Bark 출력 후 씬 전환. 비워두면 즉시 전환.")]
    [SerializeField] private BarkTrigger bark;

    public event Action OnInteracted;

    private bool _canInteract = true;

    public InteractionType InteractionType => InteractionType.ChangeScene;
    public bool CanInteract => _canInteract && !string.IsNullOrEmpty(sceneName);

    public void SetCanInteract(bool value) => _canInteract = value;

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

        Debug.Log($"[ChangeSceneInteraction] 상호작용: {sceneName}");
        OnInteracted?.Invoke();

        if (bark != null)
        {
            _canInteract = false; // Bark 중 재호출 방지
            bark.Fire(() =>
            {
                Debug.Log($"[ChangeSceneInteraction] Bark 완료. 씬 전환: {sceneName}");
                SceneManager.LoadScene(sceneName);
            });
        }
        else
        {
            SceneManager.LoadScene(sceneName);
        }
    }
}