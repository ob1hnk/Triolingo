namespace MyAssets.Runtime.Data.Quest
{
    /// <summary>
    /// 퀘스트 타입
    /// </summary>
    public enum QuestType
    {
        MainQuest,      // MQ
        SubQuest,       // SQ
        Tutorial        // TQ
    }

    /// <summary>
    /// 퀘스트 상태
    /// </summary>
    public enum QuestStatus
    {
        NotStarted,     // 시작 안함
        InProgress,     // 진행중
        Completed,      // 완료
        Failed          // 실패
    }

    /// <summary>
    /// Phase 타입
    /// </summary>
    public enum PhaseType
    {
        Dialogue,       // DLG - 대화
        Interaction,    // INT - 상호작용
        Event,          // 이벤트
        Investigation,  // 조사
        Collection,     // 수집
        Combat          // 전투
    }

    /// <summary>
    /// 보상 타입
    /// </summary>
    public enum RewardType
    {
        Item,           // 아이템
        Gold,           // 골드
        Experience,     // 경험치
        Skill           // 스킬
    }
}