using System.Collections.Generic;

namespace Multimodal.Voice
{
    /// <summary>
    /// 음성 API STREAM_START 메시지에 포함할 퀘스트 진행 상황 페이로드.
    /// Newtonsoft.Json이 snake_case 필드명 그대로 직렬화하여 Python Pydantic DTO와 매칭됨.
    /// </summary>
    public class QuestContextPayload
    {
        public List<ActiveQuestPayload> active_quests = new List<ActiveQuestPayload>();
        public List<string> completed_quest_ids = new List<string>();
    }

    public class ActiveQuestPayload
    {
        public string quest_id;
        public string quest_name;
        public string quest_type;   // "MainQuest" | "SubQuest"
        public string status;       // "InProgress"
        public float progress;      // 0.0 ~ 1.0
        public List<QuestObjectivePayload> objectives = new List<QuestObjectivePayload>();
    }

    public class QuestObjectivePayload
    {
        public string objective_id;
        public string description;
        public bool is_completed;
        public float progress;      // 0.0 ~ 1.0
        public List<QuestPhasePayload> phases = new List<QuestPhasePayload>();
    }

    public class QuestPhasePayload
    {
        public string phase_id;
        public string phase_type;   // "Dialogue" | "Interaction" | "Event" | ...
        public string content_id;
        public bool is_completed;
    }
}
