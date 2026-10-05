using System;

public sealed class PlayerProfile
{
    public PlayerProfile(string nickname, int rating)
    {
        if (!PlayerNickname.TryNormalize(nickname, out string normalized))
            throw new ArgumentException("Nickname must contain 1–24 text characters without control characters or markup.", nameof(nickname));

        Nickname = normalized;
        Rating = Math.Max(0, rating);
    }

    public string Nickname { get; private set; }
    public int Rating { get; private set; }

    public bool TrySetNickname(string nickname)
    {
        if (!PlayerNickname.TryNormalize(nickname, out string normalized))
            return false;

        Nickname = normalized;
        return true;
    }

    public void ChangeRating(int delta)
    {
        Rating = Math.Max(0, Rating + delta);
    }
}
