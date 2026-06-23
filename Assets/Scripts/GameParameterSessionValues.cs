using System;
using System.Collections.Generic;
using UnityEngine;

public static class GameParameterSessionValues
{
    private static readonly Dictionary<string, float> _values = new Dictionary<string, float>();

    public static event Action<string, float> ValueChanged;

    public static float GetValue(GameParameterId parameter)
    {
        return GetValue(GameParameterDefinitions.GetKey(parameter), GameParameterDefinitions.GetDefaultValue(parameter));
    }

    public static float GetValue(string key, float defaultValue)
    {
        if (string.IsNullOrWhiteSpace(key))
            return defaultValue;

        return _values.TryGetValue(key, out float value) ? value : defaultValue;
    }

    public static void SetValue(GameParameterId parameter, float value)
    {
        SetValue(GameParameterDefinitions.GetKey(parameter), value);
    }

    public static void SetValue(string key, float value)
    {
        if (string.IsNullOrWhiteSpace(key))
            return;

        if (_values.TryGetValue(key, out float currentValue) && Mathf.Approximately(currentValue, value))
            return;

        _values[key] = value;
        ValueChanged?.Invoke(key, value);
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
