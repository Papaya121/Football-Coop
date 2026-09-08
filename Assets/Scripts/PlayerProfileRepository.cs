using UnityEngine;

public interface IPlayerProfileRepository
{
    bool TryLoad(out PlayerProfile profile);
    void Save(PlayerProfile profile);
}

public sealed class PlayerPrefsPlayerProfileRepository : IPlayerProfileRepository
{
    private const string NicknameKey = "PlayerProfile.Nickname";
    private const string RatingKey = "PlayerProfile.Rating";

    public bool TryLoad(out PlayerProfile profile)
    {
        if (!PlayerPrefs.HasKey(NicknameKey))
        {
            profile = null;
            return false;
        }

        string nickname = PlayerPrefs.GetString(NicknameKey);
        if (string.IsNullOrWhiteSpace(nickname))
        {
            profile = null;
            return false;
        }

        profile = new PlayerProfile(nickname, PlayerPrefs.GetInt(RatingKey, PlayerProfileService.InitialRating));
        return true;
    }

    public void Save(PlayerProfile profile)
    {
        PlayerPrefs.SetString(NicknameKey, profile.Nickname);
        PlayerPrefs.SetInt(RatingKey, profile.Rating);
        PlayerPrefs.Save();
    }
}
