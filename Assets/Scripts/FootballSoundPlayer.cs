using System.Collections;
using ObjectPool;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class FootballSoundPlayer : MonoBehaviour
{
    [SerializeField] private FootballSoundCatalog _catalog;
    [SerializeField, Min(0)] private int _preloadCount = 12;
    [SerializeField, Range(0f, 1f)] private float _masterVolume = 1f;
    [SerializeField, Range(0f, 1f)] private float _spatialBlend = 1f;
    [SerializeField, Min(0f)] private float _defaultMinDistance = 1f;
    [SerializeField, Min(0f)] private float _defaultMaxDistance = 30f;

    private static FootballSoundPlayer _instance;

    private GameObjectPool _pool;
    private GameObject _runtimePrefab;

    public static FootballSoundPlayer Instance => _instance;

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }

        _instance = this;
        InitializePool();
    }

    private void OnDestroy()
    {
        if (_instance == this)
            _instance = null;

        if (_runtimePrefab != null)
            Destroy(_runtimePrefab);
    }

    public static bool TryPlay(string id, Vector3 position, float volumeMultiplier = 1f)
    {
        if (_instance == null)
            return false;

        return _instance.Play(id, position, volumeMultiplier);
    }

    public bool Play(string id, Vector3 position, float volumeMultiplier = 1f)
    {
        if (_catalog == null || !_catalog.TryGetRandomClip(id, out AudioClip clip, out float entryVolume))
            return false;

        InitializePool();

        GameObject sourceObject = _pool.Get();
        sourceObject.transform.SetPositionAndRotation(position, Quaternion.identity);
        sourceObject.transform.SetParent(transform, true);

        AudioSource source = sourceObject.GetComponent<AudioSource>();
        source.clip = clip;
        source.volume = Mathf.Clamp01(_masterVolume * entryVolume * volumeMultiplier);
        source.spatialBlend = _spatialBlend;
        source.minDistance = _defaultMinDistance;
        source.maxDistance = _defaultMaxDistance;
        source.Play();

        StartCoroutine(ReturnWhenFinished(sourceObject, clip.length));
        return true;
    }

    private void InitializePool()
    {
        if (_pool != null)
            return;

        _runtimePrefab = new GameObject("Pooled Football AudioSource");
        _runtimePrefab.SetActive(false);

        AudioSource source = _runtimePrefab.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.rolloffMode = AudioRolloffMode.Linear;

        _pool = new GameObjectPool(_runtimePrefab, _preloadCount);
    }

    private IEnumerator ReturnWhenFinished(GameObject sourceObject, float duration)
    {
        yield return new WaitForSeconds(duration);

        if (sourceObject == null)
            yield break;

        AudioSource source = sourceObject.GetComponent<AudioSource>();

        if (source != null)
        {
            source.Stop();
            source.clip = null;
        }

        _pool.Return(sourceObject);
    }
}
