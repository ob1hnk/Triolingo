using System.Collections.Generic;

/// <summary>
/// 플레이어 관련 영속 데이터 컨테이너.
/// GameManager가 소유하며 PlayerPrefs를 통해 저장/불러오기한다.
/// </summary>
[System.Serializable]
public class PlayerData
{
    /// <summary>가장 최근에 전송한 편지의 task_id. 답장을 받기 전까지 유지된다.</summary>
    public string currentLetterId;

    /// <summary>튜토리얼을 완료한 제스처 타입 이름 목록.</summary>
    public List<string> learnedGestures = new List<string>();

    // --- Future ---
    // public string playerName;
    // public List<string> npcNames;   // 플레이어가 작명한 NPC 이름 목록
    // public int currentDayIndex;     // 게임 내 날짜 (낮밤 Timeline 연동)
}