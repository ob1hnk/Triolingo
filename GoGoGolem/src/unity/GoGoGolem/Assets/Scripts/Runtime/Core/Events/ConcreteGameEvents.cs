using System;
using UnityEngine;

// === String payload ===
[CreateAssetMenu(fileName = "NewStringEvent", menuName = "Events/Game Event (String)")]
public class StringGameEvent : GameEvent<string> { }

// === Quest payload ===
[CreateAssetMenu(fileName = "NewQuestEvent", menuName = "Events/Game Event (Quest)")]
public class QuestGameEvent : GameEvent<Quest> { }

// === QuestObjective payload ===
[CreateAssetMenu(fileName = "NewObjectiveEvent", menuName = "Events/Game Event (Objective)")]
public class QuestObjectiveGameEvent : GameEvent<QuestObjective> { }

// === QuestPhase payload ===
[CreateAssetMenu(fileName = "NewPhaseEvent", menuName = "Events/Game Event (Phase)")]
public class QuestPhaseGameEvent : GameEvent<QuestPhase> { }

// === GameState change payload ===
[Serializable]
public struct GameStateChange
{
    public GameState OldState;
    public GameState NewState;

    public GameStateChange(GameState oldState, GameState newState)
    {
        OldState = oldState;
        NewState = newState;
    }
}

[CreateAssetMenu(fileName = "NewGameStateEvent", menuName = "Events/Game Event (GameState)")]
public class GameStateChangeEvent : GameEvent<GameStateChange> { }

// === CompletePhase request payload ===
[Serializable]
public struct CompletePhaseRequest
{
    public string QuestID;
    public string ObjectiveID;
    public string PhaseID;

    public CompletePhaseRequest(string questID, string objectiveID, string phaseID)
    {
        QuestID = questID;
        ObjectiveID = objectiveID;
        PhaseID = phaseID;
    }
}

[CreateAssetMenu(fileName = "NewCompletePhaseEvent", menuName = "Events/Game Event (CompletePhase)")]
public class CompletePhaseGameEvent : GameEvent<CompletePhaseRequest> { }
