using System.Globalization;

/// <summary>Shared validation for saved profiles, local players and server commands.</summary>
public static class PlayerNickname
{
    public const int MaxLength = 24;

    public static bool TryNormalize(string nickname, out string normalized)
    {
        normalized = null;
        if (string.IsNullOrWhiteSpace(nickname))
            return false;

        string candidate = nickname.Trim();
        // A text element counts an emoji or a letter with combining marks as one character.
        if (new StringInfo(candidate).LengthInTextElements > MaxLength)
            return false;

        for (int i = 0; i < candidate.Length; i++)
        {
            char character = candidate[i];
            if (char.IsControl(character) || character == '<' || character == '>' ||
                character == '\u2028' || character == '\u2029')
                return false;

            if (char.IsHighSurrogate(character))
            {
                if (i + 1 >= candidate.Length || !char.IsLowSurrogate(candidate[++i]))
                    return false;
            }
            else if (char.IsLowSurrogate(character))
            {
                return false;
            }
        }

        normalized = candidate;
        return true;
    }
}
