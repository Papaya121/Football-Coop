using System;
using UnityEngine;
using UnityEngine.InputSystem;

public static class LocalPlayerSetupSession
{
    public const int PlayerCapacity = 2;

    private static readonly FootballPlayerControlSource[] _sources = new FootballPlayerControlSource[PlayerCapacity];
    private static readonly int[] _deviceIds = new int[PlayerCapacity];

    public static event Action Changed;

    public static int PlayerCount { get; private set; }
    public static bool IsConfirmed { get; private set; }
    public static bool IsReady => PlayerCount == PlayerCapacity;

    public static bool TryAdd(FootballPlayerControlSource source, InputDevice device)
    {
        if (IsConfirmed || IsReady || device == null || Contains(source, device))
            return false;

        _sources[PlayerCount] = source;
        _deviceIds[PlayerCount] = device.deviceId;
        PlayerCount++;
        Changed?.Invoke();
        return true;
    }

    public static void Confirm()
    {
        if (!IsReady)
            throw new InvalidOperationException("Two input sources must be assigned before starting a local game.");

        IsConfirmed = true;
        Changed?.Invoke();
    }

    public static bool TryGetPlayer(int index, out FootballPlayerControlSource source, out InputDevice device)
    {
        if (index < 0 || index >= PlayerCount)
        {
            source = default;
            device = null;
            return false;
        }

        source = _sources[index];
        device = InputSystem.GetDeviceById(_deviceIds[index]);
        return device != null;
    }

    public static void Clear()
    {
        PlayerCount = 0;
        IsConfirmed = false;
        Array.Clear(_sources, 0, _sources.Length);
        Array.Clear(_deviceIds, 0, _deviceIds.Length);
        Changed?.Invoke();
    }

    private static bool Contains(FootballPlayerControlSource source, InputDevice device)
    {
        for (int i = 0; i < PlayerCount; i++)
        {
            if (_sources[i] != source)
                continue;

            if (source != FootballPlayerControlSource.Gamepad || _deviceIds[i] == device.deviceId)
                return true;
        }

        return false;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetSession()
    {
        Clear();
    }
}
