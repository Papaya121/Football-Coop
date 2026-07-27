using Mirror;

public enum FootballMatchEvent : byte
{
    None,
    Whistle,
    Goal
}

public struct FootballFindMatchMessage : NetworkMessage
{
}

public struct FootballCancelSearchMessage : NetworkMessage
{
}

public struct FootballMatchSceneReadyMessage : NetworkMessage
{
    public uint MatchId;
}

public struct FootballGiveUpMessage : NetworkMessage
{
    public uint MatchId;
}

public struct FootballLeaveMatchMessage : NetworkMessage
{
    public uint MatchId;
}

public struct FootballReturnToMenuMessage : NetworkMessage
{
    public uint MatchId;
}

public struct FootballQueueStatusMessage : NetworkMessage
{
    public int WaitingPlayerCount;
}

public struct FootballMatchFoundMessage : NetworkMessage
{
    public uint MatchId;
    public FootballTeamSide Side;
}

public struct FootballMatchStateMessage : NetworkMessage
{
    public uint MatchId;
    public FootballMatchState State;
    public float MatchDurationSeconds;
    public float RemainingSeconds;
    public int CountdownValue;
    public int LeftScore;
    public int RightScore;
    public FootballTeamSide LastScoringSide;
    public FootballMatchResult Result;
    public uint EventSequence;
    public FootballMatchEvent Event;
}
