using System.Collections.Generic;
using UnityEngine;

public static class GameParameterSessionValues
{
    private static readonly Dictionary<string, float> _values = new Dictionary<string, float>();

    public static float GetValue(string key, float defaultValue)
    {
        if (string.IsNullOrWhiteSpace(key))
            return defaultValue;

        return _values.TryGetValue(key, out float value) ? value : defaultValue;
    }

    public static void SetValue(string key, float value)
    {
        if (string.IsNullOrWhiteSpace(key))
            return;

        _values[key] = value;
    }

    public static bool TryGetValue(string key, out float value)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            value = 0f;
            return false;
        }

        return _values.TryGetValue(key, out value);
    }

    public static void Clear()
    {
        _values.Clear();
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetSession()
    {
        Clear();
    }
}
