using System;

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
