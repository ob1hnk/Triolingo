using UnityEngine;

[CreateAssetMenu(fileName = "InteractionPromptData", menuName = "GoGoGolem/Interaction Prompt Data")]
public class InteractionPromptData : ScriptableObject
{
    [SerializeField] private string actionLabel;
    [SerializeField] private Sprite keyHintSprite;
    [SerializeField] private Vector3 worldOffset = new Vector3(0f, 1.5f, 0f);

    public string ActionLabel => actionLabel;
    public Sprite KeyHintSprite => keyHintSprite;
    public Vector3 WorldOffset => worldOffset;
}
