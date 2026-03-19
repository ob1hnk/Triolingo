using UnityEngine;

[CreateAssetMenu(fileName = "InteractionPromptData", menuName = "GoGoGolem/Interaction Prompt Data")]
public class InteractionPromptData : ScriptableObject
{
    [SerializeField] private string actionLabel;
    [SerializeField] private Sprite keyHintSprite;

    public string ActionLabel => actionLabel;
    public Sprite KeyHintSprite => keyHintSprite;
}
