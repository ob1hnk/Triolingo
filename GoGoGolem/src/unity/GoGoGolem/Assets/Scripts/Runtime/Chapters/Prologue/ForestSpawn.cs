using UnityEngine;
using System.Collections;

/// <summary>
/// Forest 씬 진입 시 퀘스트 상태에 따라 플레이어/골렘 스폰 위치 설정
/// 구간 0: 퀘스트 없음 ~ MQ-01-P03 진행 중
/// 구간 1: MQ-01-P04 완료
/// 구간 2: MQ-01-P05 완료
/// 구간 3: MQ-02 시작 ~ MQ-02-P04 진행 중
/// 구간 4: MQ-02-P05 완료(제스처 씬 복귀) or MQ-02-P09 완료(제스처 씬 복귀)
/// </summary>
public class ForestSpawn : MonoBehaviour
{
    [System.Serializable]
    public struct SpawnConfig
    {
        public Transform playerSpawnPoint;
        public Transform golemSpawnPoint;
    }

    private const string QuestID_MQ01      = "MQ-01";
    private const string ObjectiveID_MQ01  = "MQ-01-OBJ-01";
    private const string PhaseID_MQ01_P04  = "MQ-01-P04";
    private const string PhaseID_MQ01_P05  = "MQ-01-P05";

    private const string QuestID_MQ02      = "MQ-02";
    private const string ObjectiveID_OBJ02 = "MQ-02-OBJ-02";
    private const string ObjectiveID_OBJ04 = "MQ-02-OBJ-04";
    private const string PhaseID_MQ02_P05  = "MQ-02-P05";
    private const string PhaseID_MQ02_P09  = "MQ-02-P09";

    [Header("References")]
    [SerializeField] private GameObject player;
    [SerializeField] private GameObject golem;

    [Header("Spawn Configs (Index 0 ~ 4)")]
    [SerializeField] private SpawnConfig[] spawnConfigs = new SpawnConfig[5];

    private IEnumerator Start()
    {
        yield return new WaitUntil(() => Managers.Instance != null && Managers.Quest != null);
        yield return new WaitForSeconds(0.1f);

        int index = GetSpawnIndex();

#if UNITY_EDITOR
        Debug.Log($"<color=white>[ForestSpawn]</color> <color=yellow>SpawnIndex: {index}</color>");
#endif

        ApplySpawn(spawnConfigs[index]);
    }

    private int GetSpawnIndex()
    {
        if (Managers.Quest == null) return 0;

        // 구간 4: 제스처 씬 복귀 (MQ-02-P05 or MQ-02-P09 완료)
        if (Managers.Quest.IsPhaseCompleted(QuestID_MQ02, ObjectiveID_OBJ04, PhaseID_MQ02_P09) ||
            Managers.Quest.IsPhaseCompleted(QuestID_MQ02, ObjectiveID_OBJ02, PhaseID_MQ02_P05))
            return 4;

        // 구간 3: MQ-02 진행 중
        if (Managers.Quest.IsQuestActive(QuestID_MQ02) || Managers.Quest.IsQuestCompleted(QuestID_MQ02))
            return 3;

        // 구간 2: MQ-01-P05 완료
        if (Managers.Quest.IsPhaseCompleted(QuestID_MQ01, ObjectiveID_MQ01, PhaseID_MQ01_P05))
            return 2;

        // 구간 1: MQ-01-P04 완료
        if (Managers.Quest.IsPhaseCompleted(QuestID_MQ01, ObjectiveID_MQ01, PhaseID_MQ01_P04))
            return 1;

        return 0;
    }

    private void ApplySpawn(SpawnConfig config)
    {
        if (player != null && config.playerSpawnPoint != null)
        {
            var cc = player.GetComponent<CharacterController>();
            if (cc != null) cc.enabled = false;

            player.transform.position = config.playerSpawnPoint.position;
            player.transform.rotation = config.playerSpawnPoint.rotation;

            if (cc != null) cc.enabled = true;
        }

        if (golem != null && config.golemSpawnPoint != null)
        {
            golem.transform.position = config.golemSpawnPoint.position;
            golem.transform.rotation = config.golemSpawnPoint.rotation;
        }
    }
}