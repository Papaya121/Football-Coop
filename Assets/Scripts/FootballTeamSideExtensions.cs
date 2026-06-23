public static class FootballTeamSideExtensions
{
    public static FootballTeamSide Opposite(this FootballTeamSide side)
    {
        return side == FootballTeamSide.Left ? FootballTeamSide.Right : FootballTeamSide.Left;
    }
}
