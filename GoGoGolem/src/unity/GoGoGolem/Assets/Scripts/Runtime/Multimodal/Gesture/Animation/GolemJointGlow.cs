using UnityEngine;
using System.Collections.Generic;

public class GolemJointGlow : MonoBehaviour
{
    [Header("Glow Settings")]
    public Color glowColor = new Color(0.3f, 0.8f, 1f, 1f);
    public float glowIntensity = 3.0f;
    public float sphereScale = 0.002f;
    public float fingerSphereScale = 0.001f;

    [Header("Alpha Settings")]
    [Range(0f, 1f)] public float outerAlpha = 0.05f;
    [Range(0f, 1f)] public float midAlpha = 0.3f;
    [Range(0f, 1f)] public float innerAlpha = 1.0f;

    [Header("Pulse Settings")]
    public bool enablePulse = false;
    public float pulseSpeed = 2.0f;
    public float pulseAmount = 0.3f;

    private List<string> jointNames = new List<string>
    {
        "Arm.L", "Hand.L",
        "Finger1_1.L", "Finger1_2.L",
        "Finger2_2.L", "Finger2_3.L",
        "Finger3_2.L", "Finger3_3.L",
        "Finger4_2.L", "Finger4_3.L",
        "Finger5_2.L", "Finger5_3.L",
        "Arm.R", "Hand.R",
        "Finger1_1.R", "Finger1_2.R",
        "Finger2_2.R", "Finger2_3.R",
        "Finger3_2.R", "Finger3_3.R",
        "Finger4_2.R", "Finger4_3.R",
        "Finger5_2.R", "Finger5_3.R",
    };

    private List<string> fingerJointNames = new List<string>
    {
        "Finger1_1.L", "Finger1_2.L",
        "Finger2_2.L", "Finger2_3.L",
        "Finger3_2.L", "Finger3_3.L",
        "Finger4_2.L", "Finger4_3.L",
        "Finger5_2.L", "Finger5_3.L",
        "Finger1_1.R", "Finger1_2.R",
        "Finger2_2.R", "Finger2_3.R",
        "Finger3_2.R", "Finger3_3.R",
        "Finger4_2.R", "Finger4_3.R",
        "Finger5_2.R", "Finger5_3.R",
    };

    private List<GameObject> glowSpheres = new List<GameObject>();

    void Start()
    {
        CreateGlowSpheres();
    }

    Material CreateLayerMaterial(float alpha)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null) shader = Shader.Find("Unlit/Color");

        Material mat = new Material(shader);
        mat.SetFloat("_Surface", 1f);
        mat.SetFloat("_Blend", 0f);
        mat.SetOverrideTag("RenderType", "Transparent");
        mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.One);
        mat.SetInt("_ZWrite", 0);
        mat.renderQueue = 3000;

        Color c = new Color(glowColor.r * glowIntensity, glowColor.g * glowIntensity, glowColor.b * glowIntensity, alpha);
        mat.SetColor("_BaseColor", c);
        mat.EnableKeyword("_ALPHAPREMULTIPLY_ON");

        return mat;
    }

    void CreateGlowSpheres()
    {
        foreach (string jointName in jointNames)
        {
            Transform joint = FindDeepChild(transform, jointName);
            if (joint == null)
            {
                Debug.LogWarning($"Joint not found: {jointName}");
                continue;
            }

            bool isFinger = fingerJointNames.Contains(jointName);
            float baseScale = isFinger ? fingerSphereScale : sphereScale;

            CreateSphere(joint, jointName + "_GlowOuter", baseScale * 2.0f, outerAlpha);
            CreateSphere(joint, jointName + "_GlowMid", baseScale * 1.2f, midAlpha);
            CreateSphere(joint, jointName + "_GlowInner", baseScale * 0.6f, innerAlpha);
        }
    }

    GameObject CreateSphere(Transform parent, string name, float scale, float alpha)
    {
        GameObject sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        sphere.name = name;
        sphere.transform.SetParent(parent);
        sphere.transform.localPosition = Vector3.zero;
        sphere.transform.localRotation = Quaternion.identity;
        sphere.transform.localScale = Vector3.one * scale;

        Destroy(sphere.GetComponent<SphereCollider>());
        sphere.GetComponent<Renderer>().material = CreateLayerMaterial(alpha);
        glowSpheres.Add(sphere);
        return sphere;
    }

    void Update()
    {
        if (!enablePulse) return;
        float pulse = 1f + Mathf.Sin(Time.time * pulseSpeed) * pulseAmount;
        foreach (GameObject sphere in glowSpheres)
        {
            if (sphere == null) continue;
            bool isFinger = sphere.name.Contains("Finger");
            float baseScale = isFinger ? fingerSphereScale : sphereScale;
            float layerMult = sphere.name.Contains("Outer") ? 2.0f :
                              sphere.name.Contains("Mid") ? 1.2f : 0.6f;
            sphere.transform.localScale = Vector3.one * baseScale * layerMult * pulse;
        }
    }

    Transform FindDeepChild(Transform parent, string name)
    {
        foreach (Transform child in parent)
        {
            if (child.name == name) return child;
            Transform result = FindDeepChild(child, name);
            if (result != null) return result;
        }
        return null;
    }
}