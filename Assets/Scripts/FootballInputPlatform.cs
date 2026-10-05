using UnityEngine;

public static class FootballInputPlatform
{
#if UNITY_EDITOR
    public static bool SimulateMobileInEditor
    {
        get => UnityEditor.EditorPrefs.GetBool(EditorPreferenceKey, false);
        set => UnityEditor.EditorPrefs.SetBool(EditorPreferenceKey, value);
    }

    private static string EditorPreferenceKey => "Football.MobileInputSimulation." + Application.dataPath;
#endif

    public static bool IsMobile
    {
        get
        {
#if UNITY_EDITOR
            return SimulateMobileInEditor || Application.isMobilePlatform;
#elif UNITY_WEBGL
            var type = Playgama.Bridge.device.type;
            return type == Playgama.Modules.Device.DeviceType.Mobile ||
                type == Playgama.Modules.Device.DeviceType.Tablet;
#else
            return Application.isMobilePlatform;
#endif
        }
    }
}
