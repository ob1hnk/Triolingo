using UnityEngine;

/// <summary>
/// 다른 스크립트에서 호출할 때
/// golemFaceController.SetFace(GolemFaceController.GolemEmotion.Happy);
/// </summary>

public class GolemFaceController : MonoBehaviour
{
    public Renderer faceRenderer;

    public enum GolemEmotion { Neutral, Happy, Sad, Angry, Surprise, Fear }

    [SerializeField] private GolemEmotion currentEmotion = GolemEmotion.Neutral;
    [SerializeField] private Color faceColor = Color.black;

    private Vector2[] faceUVOffsets = new Vector2[]
    {
        new Vector2(0f,      0.5f),  // Neutral
        new Vector2(0.333f,  0.5f),  // Happy
        new Vector2(0.666f,  0.5f),  // Sad
        new Vector2(0f,      0f),    // Angry
        new Vector2(0.333f,  0f),    // Surprise
        new Vector2(0.666f,  0f),    // Fear
    };

    private void ApplyMaterial(Material mat, GolemEmotion emotion, Color color)
    {
        Vector2 offset = faceUVOffsets[(int)emotion];
        mat.SetVector("_MainTex_ST", new Vector4(0.333f, 0.5f, offset.x, offset.y));
        mat.SetColor("_Color", color);
    }

    void Start()
    {
        if (faceRenderer == null)
            faceRenderer = GetComponent<Renderer>();

        ApplyMaterial(faceRenderer.material, currentEmotion, faceColor);
    }

    void OnValidate()
    {
        if (faceRenderer == null)
            faceRenderer = GetComponent<Renderer>();

        if (faceRenderer != null && faceRenderer.sharedMaterial != null)
            ApplyMaterial(faceRenderer.sharedMaterial, currentEmotion, faceColor);
    }

    public void SetFace(GolemEmotion emotion)
    {
        currentEmotion = emotion;
        ApplyMaterial(faceRenderer.material, currentEmotion, faceColor);
    }

    public void SetFaceColor(Color color)
    {
        faceColor = color;
        ApplyMaterial(faceRenderer.material, currentEmotion, faceColor);
    }
}