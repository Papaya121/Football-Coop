using System;
using UnityEngine;

public sealed class PlayerProfileService
{
    public const int InitialRating = 1000;

    private readonly IPlayerProfileRepository _repository;
    private readonly IPlayerRatingPolicy _ratingPolicy;
    private PlayerProfile _profile;
    private bool _ratedMatchInProgress;
    private int _provisionalRatingDelta;

    public int RatingBeforeCurrentMatch { get; private set; }

    public PlayerProfileService(IPlayerProfileRepository repository, IPlayerRatingPolicy ratingPolicy)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _ratingPolicy = ratingPolicy ?? throw new ArgumentNullException(nameof(ratingPolicy));
    }

    public event Action<PlayerProfile> ProfileChanged;

    public PlayerProfile Profile => _profile ??= LoadOrCreateProfile();

    public int RecordMatch(PlayerMatchOutcome outcome)
    {
        int delta = _ratingPolicy.GetRatingDelta(outcome);
        Profile.ChangeRating(delta);
        _repository.Save(Profile);
        ProfileChanged?.Invoke(Profile);
        return delta;
    }

    public void BeginRatedMatch()
    {
        if (_ratedMatchInProgress)
            return;

        _ratedMatchInProgress = true;

        // A defeat is persisted up front so force-closing the application cannot
        // be used to avoid the rating penalty. A completed match reconciles it.
        int ratingBeforeMatch = Profile.Rating;
        RatingBeforeCurrentMatch = ratingBeforeMatch;
        Profile.ChangeRating(_ratingPolicy.GetRatingDelta(PlayerMatchOutcome.Defeat));
        _provisionalRatingDelta = Profile.Rating - ratingBeforeMatch;
        _repository.Save(Profile);
        ProfileChanged?.Invoke(Profile);
    }

    public int CompleteRatedMatch(PlayerMatchOutcome outcome)
    {
        if (!_ratedMatchInProgress)
            return 0;

        _ratedMatchInProgress = false;

        int resultDelta = _ratingPolicy.GetRatingDelta(outcome);
        Profile.ChangeRating(resultDelta - _provisionalRatingDelta);
        _provisionalRatingDelta = 0;
        _repository.Save(Profile);
        ProfileChanged?.Invoke(Profile);
        return resultDelta;
    }

    private PlayerProfile LoadOrCreateProfile()
    {
        if (_repository.TryLoad(out PlayerProfile profile))
            return profile;

        var createdProfile = new PlayerProfile(GenerateNickname(), InitialRating);
        _repository.Save(createdProfile);
        return createdProfile;
    }

    private static string GenerateNickname()
    {
        return $"Player {UnityEngine.Random.Range(1000, 10000)}";
    }
}

public static class LocalPlayerProfile
{
    private static PlayerProfileService _service;

    public static PlayerProfileService Service => _service ??= new PlayerProfileService(
        new PlayerPrefsPlayerProfileRepository(),
        new PlayerRatingPolicy());

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetService()
    {
        _service = null;
    }
}
