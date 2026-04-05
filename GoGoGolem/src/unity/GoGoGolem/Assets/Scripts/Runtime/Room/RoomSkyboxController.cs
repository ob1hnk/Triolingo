using UnityEngine;

/// <summary>
/// Room 씬 Skybox 전환
///
/// Signal Receiver에서 호출:
///   SetEveningSkybox()  — BeforeLetter (05Daybreak)
///   SetNightSkybox()    — AfterLetter  (03Midnight)
///   SetMorningSkybox()  — Morning      (01Midday)
/// </summary>
public class RoomSkyboxController : MonoBehaviour
{
    [SerializeField] private Material eveningSkybox;
    [SerializeField] private Material nightSkybox;
    [SerializeField] private Material morningSkybox;

    private void Start()
    {
        SetEveningSkybox();
    }

    public void SetEveningSkybox()
    {
        if (eveningSkybox != null) RenderSettings.skybox = eveningSkybox;
    }

    public void SetNightSkybox()
    {
        if (nightSkybox != null) RenderSettings.skybox = nightSkybox;
    }

    public void SetMorningSkybox()
    {
        if (morningSkybox != null) RenderSettings.skybox = morningSkybox;
    }
}
