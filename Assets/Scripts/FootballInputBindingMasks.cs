using UnityEngine.InputSystem;

public static class FootballInputBindingMasks
{
    public static InputBinding? FromControlSource(FootballPlayerControlSource source)
    {
        return source switch
        {
            FootballPlayerControlSource.WasdKeyboard => InputBinding.MaskByGroup("WASD"),
            FootballPlayerControlSource.ArrowKeyboard => InputBinding.MaskByGroup("Arrows"),
            FootballPlayerControlSource.Gamepad => InputBinding.MaskByGroup("Gamepad"),
            _ => null
        };
    }
}
