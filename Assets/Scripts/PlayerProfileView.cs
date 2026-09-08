using System.Globalization;
using TMPro;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class PlayerProfileView : MonoBehaviour
{
    [SerializeField] private TMP_Text _nicknameText;
    [SerializeField] private TMP_Text _ratingText;

    private PlayerProfileService _profileService;

    private void OnEnable()
    {
        _profileService = LocalPlayerProfile.Service;
        _profileService.ProfileChanged += Refresh;
        Refresh(_profileService.Profile);
    }

    private void OnDisable()
    {
        if (_profileService != null)
            _profileService.ProfileChanged -= Refresh;
    }

    private void Refresh(PlayerProfile profile)
    {
        if (_nicknameText != null)
            _nicknameText.text = profile.Nickname;

        if (_ratingText != null)
            _ratingText.text = profile.Rating
                .ToString("N0", CultureInfo.InvariantCulture)
                .Replace(',', ' ');
    }
}
