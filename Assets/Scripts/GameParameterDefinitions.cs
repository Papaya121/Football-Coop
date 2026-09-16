public enum GameParameterId
{
    BallGravity,
    BallBounce,
    BallScale,
    PlayerGravity,
    PlayerJump,
    PlayerScale,
    PlayerAirAcceleration,
    BallKickForce,
    BallMaxSpeed,
    BallMaxLobAngle
}

public static class GameParameterDefinitions
{
    public const string BallGravityKey = "ball_gravity";
    public const string BallBounceKey = "ball_bounce";
    public const string BallScaleKey = "ball_scale";
    public const string PlayerGravityKey = "player_gravity";
    public const string PlayerJumpKey = "player_jump";
    public const string PlayerScaleKey = "player_scale";
    public const string PlayerAirAccelerationKey = "player_air_acceleration";
    public const string BallKickForceKey = "ball_kick_force";
    public const string BallMaxSpeedKey = "ball_max_speed";
    public const string BallMaxLobAngleKey = "ball_max_lob_angle";

    public const float DefaultBallGravity = 11.5f;
    public const float DefaultBallBounce = 0.8f;
    public const float DefaultBallScale = 0.7f;
    public const float DefaultPlayerGravity = 14.3f;
    public const float DefaultPlayerJump = 7f;
    public const float DefaultPlayerScale = 0.8f;
    public const float DefaultPlayerAirAcceleration = 12f;
    public const float DefaultBallKickForce = 27f;
    public const float DefaultBallMaxSpeed = 27f;
    public const float DefaultBallMaxLobAngle = 35f;
    public const float LegacyMinimumBallKickForce = 15f;

    public const float MinBallGravity = 0f;
    public const float MaxBallGravity = 20f;
    public const float MinBallBounce = 0f;
    public const float MaxBallBounce = 1f;
    public const float MinBallScale = 0.5f;
    public const float MaxBallScale = 2f;
    public const float MinPlayerGravity = 0f;
    public const float MaxPlayerGravity = 20f;
    public const float MinPlayerJump = 0f;
    public const float MaxPlayerJump = 15f;
    public const float MinPlayerScale = 0.5f;
    public const float MaxPlayerScale = 2f;
    public const float MinPlayerAirAcceleration = 0f;
    public const float MaxPlayerAirAcceleration = 40f;
    public const float MinBallKickForce = 0f;
    public const float MaxBallKickForce = 40f;
    public const float MinBallMaxLobAngle = 0f;
    public const float MaxBallMaxLobAngle = 80f;
    public const float MinBallMaxSpeed = 1f;
    public const float MaxBallMaxSpeed = 40f;

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
            case GameParameterId.PlayerAirAcceleration:
                return PlayerAirAccelerationKey;
            case GameParameterId.BallKickForce:
                return BallKickForceKey;
            case GameParameterId.BallMaxSpeed:
                return BallMaxSpeedKey;
            case GameParameterId.BallMaxLobAngle:
                return BallMaxLobAngleKey;
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
            case GameParameterId.PlayerAirAcceleration:
                return DefaultPlayerAirAcceleration;
            case GameParameterId.BallKickForce:
                return DefaultBallKickForce;
            case GameParameterId.BallMaxSpeed:
                return DefaultBallMaxSpeed;
            case GameParameterId.BallMaxLobAngle:
                return DefaultBallMaxLobAngle;
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
            case PlayerAirAccelerationKey:
                return DefaultPlayerAirAcceleration;
            case BallKickForceKey:
                return DefaultBallKickForce;
            case BallMaxSpeedKey:
                return DefaultBallMaxSpeed;
            case BallMaxLobAngleKey:
                return DefaultBallMaxLobAngle;
            default:
                return 0f;
        }
    }

    public static float GetMinValue(GameParameterId parameter)
    {
        switch (parameter)
        {
            case GameParameterId.BallGravity:
                return MinBallGravity;
            case GameParameterId.BallBounce:
                return MinBallBounce;
            case GameParameterId.BallScale:
                return MinBallScale;
            case GameParameterId.PlayerGravity:
                return MinPlayerGravity;
            case GameParameterId.PlayerJump:
                return MinPlayerJump;
            case GameParameterId.PlayerScale:
                return MinPlayerScale;
            case GameParameterId.PlayerAirAcceleration:
                return MinPlayerAirAcceleration;
            case GameParameterId.BallKickForce:
                return MinBallKickForce;
            case GameParameterId.BallMaxLobAngle:
                return MinBallMaxLobAngle;
            case GameParameterId.BallMaxSpeed:
                return MinBallMaxSpeed;
            default:
                return 0f;
        }
    }

    public static float GetMaxValue(GameParameterId parameter)
    {
        switch (parameter)
        {
            case GameParameterId.BallGravity:
                return MaxBallGravity;
            case GameParameterId.BallBounce:
                return MaxBallBounce;
            case GameParameterId.BallScale:
                return MaxBallScale;
            case GameParameterId.PlayerGravity:
                return MaxPlayerGravity;
            case GameParameterId.PlayerJump:
                return MaxPlayerJump;
            case GameParameterId.PlayerScale:
                return MaxPlayerScale;
            case GameParameterId.PlayerAirAcceleration:
                return MaxPlayerAirAcceleration;
            case GameParameterId.BallKickForce:
                return MaxBallKickForce;
            case GameParameterId.BallMaxLobAngle:
                return MaxBallMaxLobAngle;
            case GameParameterId.BallMaxSpeed:
                return MaxBallMaxSpeed;
            default:
                return 1f;
        }
    }

    public static bool IsBallParameter(string key)
    {
        return key == BallGravityKey ||
            key == BallBounceKey ||
            key == BallScaleKey ||
            key == BallKickForceKey ||
            key == BallMaxLobAngleKey ||
            key == BallMaxSpeedKey;
    }

    public static bool IsPlayerParameter(string key)
    {
        return key == PlayerGravityKey ||
            key == PlayerJumpKey ||
            key == PlayerScaleKey ||
            key == PlayerAirAccelerationKey;
    }
}
