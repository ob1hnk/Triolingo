using UnityEngine;
using UnityEngine.Playables;

public class ForestAfternoonController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private PlayableDirector cutsceneDirector;
    [SerializeField] private GameObject grandfatherNPC;
    [SerializeField] private CameraFollow cameraFollow;
    [SerializeField] private GameObject dialogueCanvas;

    [Header("Quest Settings")]
    [SerializeField] private string triggerQuestID;

    [Header("Events")]
    [SerializeField] private QuestGameEvent onQuestCompletedEvent;
    [SerializeField] private StringGameEvent requestStartDialogueEvent;
    [SerializeField] private GameEvent onDialogueCompletedEvent;

    private bool _cutscenePlayed;

    private void OnEnable()
    {
        onQuestCompletedEvent.Register(OnQuestCompleted);
        if (onDialogueCompletedEvent != null)
            onDialogueCompletedEvent.Register(OnDialogueCompleted);
    }

    private void OnDisable()
    {
        onQuestCompletedEvent.Unregister(OnQuestCompleted);
        if (onDialogueCompletedEvent != null)
            onDialogueCompletedEvent.Unregister(OnDialogueCompleted);
    }

    private void OnQuestCompleted(Quest quest)
    {
        if (_cutscenePlayed || quest.QuestID != triggerQuestID) return;
        _cutscenePlayed = true;
        StartCutscene();
    }

#if UNITY_EDITOR
    [ContextMenu("테스트: 컷씬 강제 시작")]
    private void TestStartCutscene()
    {
        _cutscenePlayed = true;
        StartCutscene();
    }
#endif

    private void StartCutscene()
    {
        if (cameraFollow != null)
            cameraFollow.enabled = false;

        cutsceneDirector.timeUpdateMode = DirectorUpdateMode.UnscaledGameTime;
        cutsceneDirector.time = 0;
        cutsceneDirector.Play();
    }

    public void OnStartDialogue()
    {
        if (dialogueCanvas != null)
            dialogueCanvas.SetActive(true);
        requestStartDialogueEvent.Raise("DLG-012");
    }

    private void OnDialogueCompleted()
    {
        if (dialogueCanvas != null)
            dialogueCanvas.SetActive(false);
    }

    public void OnTimelineEnd()
    {
        if (cameraFollow != null)
            cameraFollow.enabled = true;

        if (grandfatherNPC != null)
            grandfatherNPC.SetActive(true);
    }
}
