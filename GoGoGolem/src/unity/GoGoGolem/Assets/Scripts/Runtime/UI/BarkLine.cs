using UnityEngine;

/// <summary>
/// Bark 시스템에서 화자+대사 한 줄을 표현하는 구조체.
/// </summary>
[System.Serializable]
public struct BarkLine
{
    [Tooltip("화자 이름. '주인공'이면 PlayerData.playerName으로 치환됨.")]
    public string speaker;

    [TextArea(2, 5)]
    public string message;

    public BarkLine(string speaker, string message)
    {
        this.speaker = speaker;
        this.message = message;
    }
}