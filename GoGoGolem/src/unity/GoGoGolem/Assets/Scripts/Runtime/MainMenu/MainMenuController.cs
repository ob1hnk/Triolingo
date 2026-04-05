using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;

public class MainMenuController : MonoBehaviour
{
    [SerializeField] private string introSceneName = "Intro";
    [SerializeField] private SettingsPresenter settingsPresenter;
    [SerializeField] private PrivacyConsentPresenter privacyConsentPresenter;

    [Header("Quest Skip (Debug)")]
    [SerializeField] private string skipTargetScene = "Forest";

    private bool _pendingStart;

    private void OnEnable()
    {
        if (privacyConsentPresenter != null)
            privacyConsentPresenter.OnConsented += OnPrivacyConsented;
    }

    private void OnDisable()
    {
        if (privacyConsentPresenter != null)
            privacyConsentPresenter.OnConsented -= OnPrivacyConsented;
    }

    public void OnStartGame()
    {
        if (!PrivacyConsentPresenter.HasConsented)
        {
            _pendingStart = true;
            if (privacyConsentPresenter != null)
                privacyConsentPresenter.Show();
            return;
        }

        SceneManager.LoadScene(introSceneName);
    }

    public void OnSettings()
    {
        if (settingsPresenter != null)
            settingsPresenter.Toggle();
    }

    public void OnQuit()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    public void OnPrivacyPolicy()
    {
        if (privacyConsentPresenter != null)
            privacyConsentPresenter.Show();
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Alpha0))
        {
            SkipToMQ02OBJ03();
        }
    }

    private void SkipToMQ02OBJ03()
    {
        Debug.Log("[MainMenu] 0키 → MQ-02-OBJ-03 스킵 시작");

        // 세이브 데이터 구성: MQ-01 완료, MQ-02 OBJ-01/OBJ-02 완료
        var saveData = new QuestSaveData
        {
            lastSaveTime = System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
            saveVersion = 1
        };

        // MQ-01 완료 처리
        saveData.completedQuestIDs.Add("MQ-01");

        // MQ-02 진행 중: OBJ-01(P01,P02), OBJ-02(P03,P04,P05) 완료
        saveData.activeQuests.Add(new QuestProgressData
        {
            questID = "MQ-02",
            status = "InProgress",
            startTime = System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
            completedObjectiveIDs = new List<string>
            {
                "MQ-02-OBJ-01",
                "MQ-02-OBJ-02"
            },
            completedPhaseIDs = new List<string>
            {
                "MQ-02-P01",  // 강가 도착
                "MQ-02-P02",  // 상황 파악
                "MQ-02-P03",  // 아이템 사용
                "MQ-02-P04",  // 제스처 인식
                "MQ-02-P05"   // 전달 실패
            }
        });

        // JSON 파일 저장
        string saveFolderPath = Path.Combine(Application.persistentDataPath, "Saves");
        if (!Directory.Exists(saveFolderPath))
            Directory.CreateDirectory(saveFolderPath);

        string saveFilePath = Path.Combine(saveFolderPath, "quest_save.json");
        string json = JsonUtility.ToJson(saveData, true);
        File.WriteAllText(saveFilePath, json);

        Debug.Log($"[MainMenu] 세이브 파일 작성 완료: {saveFilePath}");

        // Forest 씬 전환 → ForestSpawn이 구간 3 스폰 포인트로 이동시킴
        SceneManager.LoadScene(skipTargetScene);
    }

    private void OnPrivacyConsented()
    {
        if (_pendingStart)
        {
            _pendingStart = false;
            SceneManager.LoadScene(introSceneName);
        }
    }
}
