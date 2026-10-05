using System;

internal static class Program
{
    private static int _checks;

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
        _checks++;
    }

    private static void Main()
    {
        Check(PlayerNickname.TryNormalize("  Футболист  ", out string normalized) && normalized == "Футболист", "Trim and Cyrillic");
        Check(PlayerNickname.TryNormalize(new string('x', 24), out _), "Maximum length");
        Check(PlayerNickname.TryNormalize(string.Concat(System.Linq.Enumerable.Repeat("⚽", 24)), out _), "Unicode nickname");
        Check(PlayerNickname.TryNormalize(string.Concat(System.Linq.Enumerable.Repeat("e\u0301", 24)), out _), "Combining characters");
        foreach (string invalid in new[] { null, "", "   ", new string('x', 25), "a\nb", "a\tb", "<b>Name</b>", "a\u2028b", "\ud800", "\udc00" })
            Check(!PlayerNickname.TryNormalize(invalid, out _), "Reject invalid nickname");

        var repository = new PlayerPrefsPlayerProfileRepository();
        repository.Save(new PlayerProfile("Original", 1234));
        var service = new PlayerProfileService(repository, new PlayerRatingPolicy());
        PlayerProfile originalProfile = service.Profile;
        int notifications = 0;
        service.ProfileChanged += profile =>
        {
            notifications++;
            Check(repository.TryLoad(out PlayerProfile saved) && saved.Nickname == profile.Nickname, "Saved before notification");
        };

        Check(service.TrySetNickname("  Новый ник  "), "Change nickname");
        Check(service.Profile.Nickname == "Новый ник" && service.Profile.Rating == 1234, "Keep rating");
        Check(ReferenceEquals(originalProfile, service.Profile), "Keep profile identity");
        Check(notifications == 1, "Notify once");
        int saveCount = UnityEngine.PlayerPrefs.SaveCount;
        Check(service.TrySetNickname("Новый ник"), "Accept unchanged nickname");
        Check(notifications == 1 && UnityEngine.PlayerPrefs.SaveCount == saveCount, "No redundant save/event");
        Check(!service.TrySetNickname("<color=red>bad</color>"), "Reject markup through service");
        Check(service.Profile.Nickname == "Новый ник" && notifications == 1 && UnityEngine.PlayerPrefs.SaveCount == saveCount, "Invalid input does not mutate/save/notify");

        var reloaded = new PlayerProfileService(repository, new PlayerRatingPolicy());
        Check(reloaded.Profile.Nickname == "Новый ник" && reloaded.Profile.Rating == 1234, "Persistence across service instances");

        service.BeginRatedMatch();
        int provisionalRating = service.Profile.Rating;
        Check(service.TrySetNickname("During match"), "Rename during rated match");
        Check(service.Profile.Rating == provisionalRating, "Keep provisional rating");
        service.CompleteRatedMatch(PlayerMatchOutcome.Victory);
        Check(service.Profile.Rating == 1259 && service.Profile.Nickname == "During match", "Rated match reconciles after rename");

        UnityEngine.PlayerPrefs.SetString("PlayerProfile.Nickname", "<invalid>");
        Check(!repository.TryLoad(out _), "Invalid persisted data does not throw");
        Console.WriteLine($"Player profile tests passed: {_checks} checks.");
    }
}
