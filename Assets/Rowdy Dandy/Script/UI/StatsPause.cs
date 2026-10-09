using UnityEngine;

// The stats screen (C / L2, PlayerStats.ToggleStatsUI) pauses the game while it's up. {INTERACT} on it zooms into the
// boon section (BoonHUD's expanded BUILD view: every boon with its full text); {MOVE} scrolls it.
public static class StatsPause
{
    public static bool IsOpen { get; private set; }
    public static bool Expanded { get; set; }
    public static float OpenedAt { get; private set; }
    private static int closedFrame = -10;
    public static bool BlocksPause => IsOpen || Time.frameCount <= closedFrame + 2;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetSession() { IsOpen = false; Expanded = false; }

    public static void Set(bool open)
    {
        if (open == IsOpen) return;
        if (open && PauseMenu.IsPaused) return; // something else already paused the game (menus): just show the panel
        IsOpen = open;
        Expanded = false;
        if (open) OpenedAt = Time.unscaledTime;
        else closedFrame = Time.frameCount;
        PauseMenu.SetExternalPause(open);
    }
}
