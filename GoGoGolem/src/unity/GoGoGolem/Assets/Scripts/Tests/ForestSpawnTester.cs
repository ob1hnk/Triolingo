using UnityEngine;
using System.Collections;

/// <summary>
/// ForestSpawn 구간별 스폰 로직 테스트용
/// QuestManager에 퀘스트 상태를 강제 주입해 원하는 구간을 시뮬레이션
/// </summary>
public class ForestSpawnTester : MonoBehaviour
{
    [Header("MQ-01 상태")]
    public bool mq01_P04_Done;
    public bool mq01_P05_Done;

    [Header("MQ-02 상태")]
    public bool mq02_InProgress;
    public bool mq02_P05_Done;
    public bool mq02_P09_Done;

    private void Awake()
    {
        StartCoroutine(SetupSpawnTestEnvironment());
    }

    private IEnumerator SetupSpawnTestEnvironment()
    {
        yield return new WaitUntil(() => Managers.Instance != null && Managers.Quest != null);

        if (mq01_P04_Done || mq01_P05_Done)
        {
            Managers.Quest.StartQuest("MQ-01");
            if (mq01_P04_Done) Managers.Quest.CompletePhase("MQ-01", "MQ-01-OBJ-01", "MQ-01-P04");
            if (mq01_P05_Done) Managers.Quest.CompletePhase("MQ-01", "MQ-01-OBJ-01", "MQ-01-P05");
        }

        if (mq02_InProgress || mq02_P05_Done || mq02_P09_Done)
        {
            Managers.Quest.StartQuest("MQ-02");
            if (mq02_P05_Done) Managers.Quest.CompletePhase("MQ-02", "MQ-02-OBJ-02", "MQ-02-P05");
            if (mq02_P09_Done) Managers.Quest.CompletePhase("MQ-02", "MQ-02-OBJ-04", "MQ-02-P09");
        }

        Debug.Log("<color=lime>[ForestSpawnTester] 퀘스트 상태 주입 완료</color>");
    }
}