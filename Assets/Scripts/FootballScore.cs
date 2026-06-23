public sealed class FootballScore
{
    public int Left { get; private set; }
    public int Right { get; private set; }

    public void AddGoal(FootballTeamSide side)
    {
        if (side == FootballTeamSide.Left)
            Left++;
        else
            Right++;
    }
}
