using UnityEngine;

/// <summary>Persists the menu volume and applies it even when gameplay is opened directly.</summary>
public sealed class FootballAudioSettings : MonoBehaviour
{
    private const string MasterVolumeKey = "Football.Audio.MasterVolume";

    public static float MasterVolume => Mathf.Clamp01(PlayerPrefs.GetFloat(MasterVolumeKey, 1f));

    public static bool SetMasterVolume(float volume)
    {
        if (!FootballAudioMixer.SetVolume(FootballAudioChannel.Master, volume))
            return false;

        PlayerPrefs.SetFloat(MasterVolumeKey, Mathf.Clamp01(volume));
        PlayerPrefs.Save();
        return true;
    }

    private void Start()
    {
        FootballAudioMixer.SetVolume(FootballAudioChannel.Master, MasterVolume);
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Initialize()
    {
        var host = new GameObject(nameof(FootballAudioSettings));
        DontDestroyOnLoad(host);
        host.AddComponent<FootballAudioSettings>();
    }
}
