public enum GameParameterId
{
    BallGravity,
    BallBounce,
    BallScale,
    PlayerGravity,
    PlayerJump,
    PlayerScale
}

public static class GameParameterDefinitions
{
    public const string BallGravityKey = "ball_gravity";
    public const string BallBounceKey = "ball_bounce";
    public const string BallScaleKey = "ball_scale";
    public const string PlayerGravityKey = "player_gravity";
    public const string PlayerJumpKey = "player_jump";
    public const string PlayerScaleKey = "player_scale";

    public const float DefaultBallGravity = 9.8f;
    public const float DefaultBallBounce = 0.8f;
    public const float DefaultBallScale = 1f;
    public const float DefaultPlayerGravity = 9.8f;
    public const float DefaultPlayerJump = 7f;
    public const float DefaultPlayerScale = 1f;

    public static string GetKey(GameParameterId parameter)
    {
        switch (parameter)
        {
            case GameParameterId.BallGravity:
                return BallGravityKey;
            case GameParameterId.BallBounce:
                return BallBounceKey;
            case GameParameterId.BallScale:
                return BallScaleKey;
            case GameParameterId.PlayerGravity:
                return PlayerGravityKey;
            case GameParameterId.PlayerJump:
                return PlayerJumpKey;
            case GameParameterId.PlayerScale:
                return PlayerScaleKey;
            default:
                return BallGravityKey;
        }
    }

    public static float GetDefaultValue(GameParameterId parameter)
    {
        switch (parameter)
        {
            case GameParameterId.BallGravity:
                return DefaultBallGravity;
            case GameParameterId.BallBounce:
                return DefaultBallBounce;
            case GameParameterId.BallScale:
                return DefaultBallScale;
            case GameParameterId.PlayerGravity:
                return DefaultPlayerGravity;
            case GameParameterId.PlayerJump:
                return DefaultPlayerJump;
            case GameParameterId.PlayerScale:
                return DefaultPlayerScale;
            default:
                return DefaultBallGravity;
        }
    }

    public static float GetDefaultValue(string key)
    {
        switch (key)
        {
            case BallGravityKey:
                return DefaultBallGravity;
            case BallBounceKey:
                return DefaultBallBounce;
            case BallScaleKey:
                return DefaultBallScale;
            case PlayerGravityKey:
                return DefaultPlayerGravity;
            case PlayerJumpKey:
                return DefaultPlayerJump;
            case PlayerScaleKey:
                return DefaultPlayerScale;
            default:
                return 0f;
        }
    }

    public static bool IsBallParameter(string key)
    {
        return key == BallGravityKey || key == BallBounceKey || key == BallScaleKey;
    }

    public static bool IsPlayerParameter(string key)
    {
        return key == PlayerGravityKey || key == PlayerJumpKey || key == PlayerScaleKey;
    }
}
