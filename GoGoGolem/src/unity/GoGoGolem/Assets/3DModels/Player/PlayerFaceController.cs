using UnityEngine;

public enum FaceExpression { Default, Sad, Angry, Brag, Surprised, Closed }

public class PlayerFaceController : MonoBehaviour
{
    [Header("Parts")]
    public Renderer eyebrows;
    public Renderer eyes;
    public Renderer mouth;

    [Header("Textures")]
    public Texture[] eyebrowTextures;
    public Texture[] eyeTextures;
    public Texture[] mouthTextures;

    public FaceExpression currentExpression;

    public void SetExpression(FaceExpression expression)
    {
        int i = (int)expression;
        eyebrows.sharedMaterial.SetTexture("_MainTex", eyebrowTextures[i]);
        eyes.sharedMaterial.SetTexture("_MainTex", eyeTextures[i]);
        mouth.sharedMaterial.SetTexture("_MainTex", mouthTextures[i]);
    }

    void OnValidate()
    {
        if (eyebrows != null && eyes != null && mouth != null &&
        eyebrows.sharedMaterial != null && eyes.sharedMaterial != null && mouth.sharedMaterial != null &&
        eyebrowTextures != null && eyeTextures != null && mouthTextures != null)
        SetExpression(currentExpression);
    }
}