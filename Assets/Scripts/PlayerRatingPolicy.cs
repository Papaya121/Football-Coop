public interface IPlayerRatingPolicy
{
    int GetRatingDelta(PlayerMatchOutcome outcome);
}

public sealed class PlayerRatingPolicy : IPlayerRatingPolicy
{
    public const int VictoryReward = 25;
    public const int DrawReward = 5;
    public const int DefeatPenalty = -20;

    public int GetRatingDelta(PlayerMatchOutcome outcome)
    {
        return outcome switch
        {
            PlayerMatchOutcome.Victory => VictoryReward,
            PlayerMatchOutcome.Draw => DrawReward,
            PlayerMatchOutcome.Defeat => DefeatPenalty,
            _ => 0
        };
    }
}
