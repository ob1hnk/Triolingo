using UnityEngine;

/// <summary>
/// 다른 스크립트에서 호출할 때
/// golemFaceController.SetFace(GolemFaceController.GolemEmotion.Happy);
/// </summary>

public class GolemFaceController : MonoBehaviour
{
    public Renderer faceRenderer;

    [Tooltip("밤에 표시할 별도 face 오브젝트. 같은 위치에 배치 후 연결.")]
    [SerializeField] private Renderer nightFaceRenderer;

    public enum GolemEmotion { Neutral, Happy, Sad, Angry, Surprise, Fear }

    [SerializeField] private GolemEmotion currentEmotion = GolemEmotion.Neutral;
    [SerializeField] private Color faceColor = Color.black;
    [SerializeField] private Color nightFaceColor = Color.black;

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
        var tiling = new Vector4(0.333f, 0.5f, offset.x, offset.y);
        mat.SetVector("_MainTex_ST", tiling);
        mat.SetColor("_Color", color);
    }

    void Start()
    {
        if (faceRenderer == null)
            faceRenderer = GetComponent<Renderer>();

        ApplyMaterial(faceRenderer.material, currentEmotion, faceColor);

        if (nightFaceRenderer != null)
        {
            nightFaceRenderer.gameObject.SetActive(false);
            ApplyMaterial(nightFaceRenderer.material, currentEmotion, nightFaceColor);
        }
    }

    void OnValidate()
    {
        if (faceRenderer == null)
            faceRenderer = GetComponent<Renderer>();
    }

    public void SetFace(GolemEmotion emotion)
    {
        currentEmotion = emotion;
        ApplyMaterial(faceRenderer.material, currentEmotion, faceColor);
        if (nightFaceRenderer != null)
            ApplyMaterial(nightFaceRenderer.material, currentEmotion, nightFaceColor);
    }

    public void SetNightFace(GolemEmotion emotion)
    {
        if (nightFaceRenderer == null) return;
        ApplyMaterial(nightFaceRenderer.material, emotion, nightFaceColor);
    }

    public void SetFaceColor(Color color)
    {
        faceColor = color;
        ApplyMaterial(faceRenderer.material, currentEmotion, faceColor);
    }

    public void FadeToNightColor(float duration = 1.5f)
    {
        if (nightFaceRenderer == null) return;
        SetNightFace(GolemEmotion.Fear);
        faceRenderer.enabled = false;
        nightFaceRenderer.gameObject.SetActive(true);
    }

    public void FadeToDefaultColor(float duration = 1.5f)
    {
        if (nightFaceRenderer == null) return;
        nightFaceRenderer.gameObject.SetActive(false);
        faceRenderer.enabled = true;
    }
}
