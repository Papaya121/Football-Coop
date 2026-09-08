using GameAnalyticsSDK;
using UnityEngine;

public static class FootballAnalytics
{
    public const string PressButtonPlay = "menu:press_button_play";
    public const string PressButtonAi = "menu:press_button_ai";
    public const string PressButtonTutorial = "menu:press_button_tutorial";
    public const string PressButtonOnline = "menu:press_button_online";
    public const string PressButtonExit = "menu:press_button_exit";
    public const string PressButtonBack = "menu:press_button_back";
    public const string PressButtonStartLocal = "menu:press_button_start_local";
    public const string PressButtonStartOnline = "menu:press_button_start_online";
    public const string PressButtonCancelMatchmaking = "menu:press_button_cancel_matchmaking";

    private const string StartTutorialEvent = "tutorial:start_tutorial";
    private const string EndTutorialEvent = "tutorial:end_tutorial";
    private const string SkipTutorialEvent = "tutorial:skip_tutorial";
    private const string StartMatchOnlineEvent = "gameplay:start_match_online";
    private const string StartMatchLocalEvent = "gameplay:start_match_local";
    private const string PlayerGoalEvent = "gameplay:player_goal";
    private const string OpponentGoalEvent = "gameplay:opponent_goal";
    private const string ScissorKickGoalEvent = "gameplay:scissor_kick_goal";
    private const string HeadKickGoalEvent = "gameplay:head_kick_goal";
    private const string LegKickGoalEvent = "gameplay:leg_kick_goal";
    private const string AutoGoalEvent = "gameplay:auto_goal";
    private const string EndMatchPlayerWinEvent = "gameplay:end_match_player_win";
    private const string EndMatchDrawEvent = "gameplay:end_match_draw";
    private const string AbortMatchEvent = "gameplay:abort_match";

    private static bool _matchActive;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetState()
    {
        _matchActive = false;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Initialize()
    {
#if UNITY_SERVER
        return;
#else
        if (Object.FindAnyObjectByType<GameAnalytics>() == null)
        {
            GameObject analyticsObject = new GameObject("GameAnalytics");
            analyticsObject.AddComponent<GameAnalytics>();
        }

        if (!GameAnalytics.Initialized)
            GameAnalytics.Initialize();
#endif
    }

    public static void MenuButton(string eventName) => Send(eventName);

    public static void TutorialStarted() => Send(StartTutorialEvent);

    public static void TutorialFinished(bool skipped)
    {
        Send(skipped ? SkipTutorialEvent : EndTutorialEvent);
    }

    public static void MatchStarted(bool online)
    {
        _matchActive = true;
        Send(online ? StartMatchOnlineEvent : StartMatchLocalEvent);
    }

    public static void Goal(bool scoredByPlayer, FootballGoalKickType kickType, bool ownGoal)
    {
        Send(scoredByPlayer ? PlayerGoalEvent : OpponentGoalEvent);

        if (ownGoal)
        {
            Send(AutoGoalEvent);
            return;
        }

        switch (kickType)
        {
            case FootballGoalKickType.Leg:
                Send(LegKickGoalEvent);
                break;
            case FootballGoalKickType.Head:
                Send(HeadKickGoalEvent);
                break;
            case FootballGoalKickType.Scissor:
                Send(ScissorKickGoalEvent);
                break;
        }
    }

    public static void MatchFinished(bool playerWon, bool draw)
    {
        _matchActive = false;

        if (draw)
            Send(EndMatchDrawEvent);
        else if (playerWon)
            Send(EndMatchPlayerWinEvent);
    }

    public static void AbortMatch()
    {
        if (!_matchActive)
            return;

        _matchActive = false;
        Send(AbortMatchEvent);
    }

    private static void Send(string eventName)
    {
#if !UNITY_SERVER
        if (GameAnalytics.Initialized)
            GameAnalytics.NewDesignEvent(eventName);
#endif
    }
}
