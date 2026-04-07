using UnityEngine;

/// <summary>
/// Forest 씬 Skybox 전환
///
/// ForestAfternoonController에서 호출:
///   SetDaySkybox()    — 기본 낮 (MQ-02-P09 이전)
///   SetSunsetSkybox() — 노을 (MQ-02-P09 완료 후)
/// </summary>
public class ForestSkyboxController : MonoBehaviour
{
    [SerializeField] private Material daySkybox;
    [SerializeField] private Material sunsetSkybox;

    private void Start()
    {
        if (daySkybox != null)
            RenderSettings.skybox = daySkybox;
    }

    public void SetDaySkybox()
    {
        if (daySkybox != null) RenderSettings.skybox = daySkybox;
    }

    public void SetSunsetSkybox()
    {
        if (sunsetSkybox != null) RenderSettings.skybox = sunsetSkybox;
    }
}
