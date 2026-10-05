using System;
using UnityEngine;
using UnityEngine.Audio;

public enum FootballAudioChannel
{
    Master,
    SFX,
    Music,
    UI
}

/// <summary>Shared mixer routing and normalized volume controls for the settings UI.</summary>
public static class FootballAudioMixer
{
    private const float MinimumDecibels = -80f;
    private static AudioMixer _mixer;

    public static AudioMixer Mixer
    {
        get
        {
            if (_mixer == null)
                _mixer = Resources.Load<AudioMixer>("FootballAudioMixer");

            return _mixer;
        }
    }

    public static AudioMixerGroup GetGroup(FootballAudioChannel channel)
    {
        if (Mixer == null)
            return null;

        string name = GetChannelName(channel);
        AudioMixerGroup[] groups = Mixer.FindMatchingGroups(name);

        for (int i = 0; i < groups.Length; i++)
        {
            if (groups[i].name == name)
                return groups[i];
        }

        return null;
    }

    // Apply from Start or later, after Unity has initialized the mixer.
    public static bool SetVolume(FootballAudioChannel channel, float volume)
    {
        if (float.IsNaN(volume) || float.IsInfinity(volume))
            return false;

        volume = Mathf.Clamp01(volume);
        float decibels = volume <= 0.0001f ? MinimumDecibels : 20f * Mathf.Log10(volume);
        return Mixer != null && Mixer.SetFloat(GetChannelName(channel) + "Volume", decibels);
    }

    public static float GetVolume(FootballAudioChannel channel)
    {
        if (Mixer == null || !Mixer.GetFloat(GetChannelName(channel) + "Volume", out float decibels))
            return 1f;

        return decibels <= MinimumDecibels ? 0f : Mathf.Clamp01(Mathf.Pow(10f, decibels / 20f));
    }

    private static string GetChannelName(FootballAudioChannel channel)
    {
        switch (channel)
        {
            case FootballAudioChannel.Master: return "Master";
            case FootballAudioChannel.SFX: return "SFX";
            case FootballAudioChannel.Music: return "Music";
            case FootballAudioChannel.UI: return "UI";
            default: throw new ArgumentOutOfRangeException(nameof(channel), channel, null);
        }
    }
}
