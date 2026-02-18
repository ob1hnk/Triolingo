using System.Collections.Generic;
using System.IO;
using UnityEngine;

/// <summary>
/// 퀘스트 진행 상태를 JSON 파일로 저장하고 로드합니다.
/// 책임: 파일 입출력 및 직렬화/역직렬화
/// </summary>
public class QuestSaveSystem
{
    private readonly string saveFolderPath;
    private readonly string saveFileName = "quest_save.json";
    private string SaveFilePath => Path.Combine(saveFolderPath, saveFileName);

    public QuestSaveSystem()
    {
        // Application.persistentDataPath 사용
        saveFolderPath = Path.Combine(Application.persistentDataPath, "Saves");

        // 폴더가 없으면 생성
        if (!Directory.Exists(saveFolderPath))
        {
            Directory.CreateDirectory(saveFolderPath);
            Debug.Log($"[QuestSaveSystem] Created save folder: {saveFolderPath}");
        }
    }

    #region Save/Load

    /// <summary>
    /// 퀘스트 진행 상태 저장
    /// </summary>
    public void SaveQuests(
        Dictionary<string, Quest> activeQuests,
        Dictionary<string, Quest> completedQuests)
    {
        try
        {
            // Save 데이터 생성
            QuestSaveData saveData = CreateSaveData(activeQuests, completedQuests);

            // JSON으로 직렬화
            string json = JsonUtility.ToJson(saveData, true);

            // 파일에 쓰기
            File.WriteAllText(SaveFilePath, json);

            Debug.Log($"[QuestSaveSystem] ✓ Saved {saveData.activeQuests.Count} active, {saveData.completedQuestIDs.Count} completed quests");
            Debug.Log($"[QuestSaveSystem] Save file: {SaveFilePath}");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[QuestSaveSystem] Failed to save quests: {e.Message}");
        }
    }

    /// <summary>
    /// 퀘스트 진행 상태 로드
    /// </summary>
    public QuestSaveData LoadQuests()
    {
        if (!HasSaveFile())
        {
            Debug.Log("[QuestSaveSystem] No save file found. Starting fresh.");
            return new QuestSaveData();
        }

        try
        {
            // 파일 읽기
            string json = File.ReadAllText(SaveFilePath);

            // JSON에서 역직렬화
            QuestSaveData saveData = JsonUtility.FromJson<QuestSaveData>(json);

            Debug.Log($"[QuestSaveSystem] ✓ Loaded {saveData.activeQuests.Count} active, {saveData.completedQuestIDs.Count} completed quests");
            Debug.Log($"[QuestSaveSystem] Last save: {saveData.lastSaveTime}");

            return saveData;
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[QuestSaveSystem] Failed to load quests: {e.Message}");
            return new QuestSaveData();
        }
    }

    #endregion

    #region File Management

    /// <summary>
    /// 세이브 파일 존재 여부 확인
    /// </summary>
    public bool HasSaveFile()
    {
        return File.Exists(SaveFilePath);
    }

    /// <summary>
    /// 세이브 파일 삭제
    /// </summary>
    public void DeleteSaveFile()
    {
        if (HasSaveFile())
        {
            File.Delete(SaveFilePath);
            Debug.Log($"[QuestSaveSystem] Deleted save file: {SaveFilePath}");
        }
        else
        {
            Debug.LogWarning("[QuestSaveSystem] No save file to delete.");
        }
    }

    /// <summary>
    /// 세이브 파일 경로 반환
    /// </summary>
    public string GetSavePath()
    {
        return SaveFilePath;
    }

    #endregion

    #region Private Methods

    /// <summary>
    /// QuestSaveData 생성
    /// </summary>
    private QuestSaveData CreateSaveData(
        Dictionary<string, Quest> activeQuests,
        Dictionary<string, Quest> completedQuests)
    {
        QuestSaveData saveData = new QuestSaveData
        {
            lastSaveTime = System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
            saveVersion = 1
        };

        // 활성 퀘스트 저장
        foreach (var quest in activeQuests.Values)
        {
            QuestProgressData progressData = new QuestProgressData
            {
                questID = quest.QuestID,
                status = quest.Status.ToString(),
                startTime = System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
            };

            // 완료된 Objective ID 수집
            foreach (var objective in quest.GetAllObjectives())
            {
                if (objective.IsCompleted)
                {
                    progressData.completedObjectiveIDs.Add(objective.ObjectiveID);
                }

                // 완료된 Phase ID 수집
                foreach (var phase in objective.GetAllPhases())
                {
                    if (phase.IsCompleted)
                    {
                        progressData.completedPhaseIDs.Add(phase.PhaseID);
                    }
                }
            }

            saveData.activeQuests.Add(progressData);
        }

        // 완료된 퀘스트 ID 저장
        foreach (var questID in completedQuests.Keys)
        {
            saveData.completedQuestIDs.Add(questID);
        }

        return saveData;
    }

    #endregion

    #region Debug Methods

    /// <summary>
    /// 세이브 파일 내용 출력 (디버그용)
    /// </summary>
    public void PrintSaveFileContent()
    {
        if (!HasSaveFile())
        {
            Debug.Log("[QuestSaveSystem] No save file exists.");
            return;
        }

        try
        {
            string json = File.ReadAllText(SaveFilePath);
            Debug.Log($"[QuestSaveSystem] Save file content:\n{json}");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[QuestSaveSystem] Failed to read save file: {e.Message}");
        }
    }

    #endregion
}
