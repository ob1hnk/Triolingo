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
        eyebrows.material.SetTexture("_MainTex", eyebrowTextures[i]);
        eyes.material.SetTexture("_MainTex", eyeTextures[i]);
        mouth.material.SetTexture("_MainTex", mouthTextures[i]);
    }

    void Update()
    {
        #if UNITY_EDITOR
        SetExpression(currentExpression);
        #endif
    }
}