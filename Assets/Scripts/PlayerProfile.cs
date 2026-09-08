using System;

public sealed class PlayerProfile
{
    public PlayerProfile(string nickname, int rating)
    {
        if (string.IsNullOrWhiteSpace(nickname))
            throw new ArgumentException("Nickname cannot be empty.", nameof(nickname));

        Nickname = nickname;
        Rating = Math.Max(0, rating);
    }

    public string Nickname { get; }
    public int Rating { get; private set; }

    public void ChangeRating(int delta)
    {
        Rating = Math.Max(0, Rating + delta);
    }
}
