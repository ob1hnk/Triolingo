using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.SceneManagement;
using Demo.Chapters.Prologue;

public class ForestAfternoonController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private PlayableDirector cutsceneDirector;
    [SerializeField] private CameraFollow cameraFollow;
    [SerializeField] private ForestSkyboxController skyboxController;
    [SerializeField] private GameObject grandfatherDefault;
    [SerializeField] private GameObject grandfatherAfternoon;

    [Header("Quest Settings")]
    [SerializeField] private ForestQuestController questController;
    [SerializeField] private string triggerPhaseID = "MQ-02-P09";
    [SerializeField] private string completedPhaseID = "MQ-02-P10";

    [Header("Events")]
    [SerializeField] private StringGameEvent requestStartDialogueEvent;
    [SerializeField] private GameEvent onDialogueCompletedEvent;

    [Header("Scene Transition")]
    [SerializeField] private string nextScene = "Room";

    private void OnEnable()
    {
        onDialogueCompletedEvent?.Register(OnDialogueCompleted);
    }

    private void OnDisable()
    {
        onDialogueCompletedEvent?.Unregister(OnDialogueCompleted);
    }

    private bool _waitingForDialogueComplete;

    private void Start()
    {
        bool triggered = questController != null && questController.IsPhaseCompleted(triggerPhaseID);
        bool completed = questController != null && questController.IsPhaseCompleted(completedPhaseID);

        if (triggered && !completed)
        {
            skyboxController?.SetSunsetSkybox();
            grandfatherDefault?.SetActive(false);
            grandfatherAfternoon?.SetActive(true);
        }
    }

    // WindSkillInteractable onInteract UnityEvent → 연결
    public void StartCutscene()
    {
        if (cameraFollow != null)
            cameraFollow.enabled = false;

        cutsceneDirector.extrapolationMode = DirectorWrapMode.Hold;
        cutsceneDirector.timeUpdateMode = DirectorUpdateMode.UnscaledGameTime;
        cutsceneDirector.time = 0;
        cutsceneDirector.Play();
    }

    // 타임라인 Signal → DLG-012 실행
    public void OnStartDialogue()
    {
        _waitingForDialogueComplete = true;
        requestStartDialogueEvent?.Raise("DLG-012");
    }

    // DLG-012 완료 → 씬 전환
    private void OnDialogueCompleted()
    {
        if (!_waitingForDialogueComplete) return;
        _waitingForDialogueComplete = false;
        SceneManager.LoadScene(nextScene);
    }

#if UNITY_EDITOR
    [ContextMenu("테스트: 컷씬 강제 시작")]
    private void TestStartCutscene() => StartCutscene();
#endif
}
