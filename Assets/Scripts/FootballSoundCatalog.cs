using System;
using UnityEngine;

[CreateAssetMenu(fileName = "FootballSoundCatalog", menuName = "Football/Sound Catalog")]
public sealed class FootballSoundCatalog : ScriptableObject
{
    [SerializeField] private FootballSoundEntry[] _entries = Array.Empty<FootballSoundEntry>();

    public bool TryGetRandomClip(string id, out AudioClip clip, out float volume)
    {
        clip = null;
        volume = 1f;

        if (string.IsNullOrWhiteSpace(id) || _entries == null)
            return false;

        for (int i = 0; i < _entries.Length; i++)
        {
            FootballSoundEntry entry = _entries[i];

            if (entry == null || entry.Id != id)
                continue;

            return entry.TryGetRandomClip(out clip, out volume);
        }

        return false;
    }
}

[Serializable]
public sealed class FootballSoundEntry
{
    [SerializeField] private string _id;
    [SerializeField] private AudioClip[] _clips = Array.Empty<AudioClip>();
    [SerializeField, Range(0f, 1f)] private float _volume = 1f;

    public string Id => _id;

    public bool TryGetRandomClip(out AudioClip clip, out float volume)
    {
        clip = null;
        volume = _volume;

        if (_clips == null || _clips.Length == 0)
            return false;

        int startIndex = UnityEngine.Random.Range(0, _clips.Length);

        for (int i = 0; i < _clips.Length; i++)
        {
            AudioClip candidate = _clips[(startIndex + i) % _clips.Length];

            if (candidate == null)
                continue;

            clip = candidate;
            return true;
        }

        return false;
    }
}
