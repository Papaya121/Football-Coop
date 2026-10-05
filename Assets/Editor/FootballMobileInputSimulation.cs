using UnityEditor;

public static class FootballMobileInputSimulation
{
    private const string MenuPath = "Tools/Football/Mobile Input Simulation";

    [MenuItem(MenuPath)]
    private static void Toggle()
    {
        FootballInputPlatform.SimulateMobileInEditor = !FootballInputPlatform.SimulateMobileInEditor;
    }

    [MenuItem(MenuPath, true)]
    private static bool ValidateToggle()
    {
        Menu.SetChecked(MenuPath, FootballInputPlatform.SimulateMobileInEditor);
        return !EditorApplication.isPlayingOrWillChangePlaymode;
    }
}
