using UnityEngine;

// The interact button (E / the gamepad's top face button) is shared: weapon pickup, cat swap, rat grab, checkpoint rest.
// Whoever acts on a press calls Use(); the checkpoint rest (checked in LateUpdate) skips a press already used.
public static class Interact
{
    private static int usedFrame = -1;

    public static bool Pressed => !PauseMenu.IsPaused && GameInput.Down(GameInput.Act.Interact);
    public static void Use() => usedFrame = Time.frameCount;
    public static bool UsedThisFrame => usedFrame == Time.frameCount;
    public static string ButtonName => GameInput.Icon(GameInput.Act.Interact); // icon character, for PixelText
}
