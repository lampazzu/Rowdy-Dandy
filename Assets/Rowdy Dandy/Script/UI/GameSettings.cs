using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// Player settings: display, audio and gameplay. Saved in PlayerPrefs and applied when the game starts.
// Other scripts read the values directly, e.g. GameSettings.SfxVolume, GameSettings.ScreenShake.
public static class GameSettings
{
    private const string Prefix = "RD_Settings_";

    // ---------------------------------------------------------------- values
    public static int ResolutionIndex { get; private set; }
    public static FullScreenMode DisplayMode { get; private set; } = FullScreenMode.FullScreenWindow;
    public static bool VSync { get; private set; }
    public static int FrameLimit { get; private set; } = 60;        // -1 = unlimited

    public static float MasterVolume { get; private set; } = 1f;
    public static float MusicVolume { get; private set; } = 0.8f;
    public static float SfxVolume { get; private set; } = 1f;
    public static float RowdyVoiceVolume { get; private set; } = 1f; // Rowdy's grunts / hurt voice (on top of Sound Effects)
    public static float CatVoiceVolume { get; private set; } = 1f;   // Wig / Nick voice lines (on top of Sound Effects)

    public static float ScreenShake { get; private set; } = 1f;     // 0 .. 1
    public static bool Vibration { get; private set; } = true;
    public static bool DamageNumbers { get; private set; } = true;
    public static float DamageNumberSize { get; private set; } = 1f; // x the (already halved) base size of damage numbers
    public static float MessageSize { get; private set; } = 1f;      // x the (already halved) base size of CRITICAL! / COUNTER! / EXECUTED!...
    public static bool ShowFps { get; private set; }
    // "Respect": how many of the pushy melee enemies (Big Wolf, Transform Wolf, Werefast) may press Rowdy at once;
    // the rest hang back and wait for a turn. 0 = no limit.
    public static int CrowdLimit { get; private set; } = 4;
    // Style rank panel (StyleRank): on / off, size, which screen corner
    public static bool StyleRankOn { get; private set; } = true;
    public static float StyleRankSize { get; private set; } = 0.6f;
    public static int StyleRankPosition { get; private set; } = 1; // index into RankPositions

    public static event Action Changed;

    public static readonly int[] FrameLimits = { 30, 60, 120, 144, 240, -1 };
    public static readonly FullScreenMode[] DisplayModes = { FullScreenMode.FullScreenWindow, FullScreenMode.ExclusiveFullScreen, FullScreenMode.Windowed };
    public static readonly float[] ShakeLevels = { 0f, 0.5f, 1f };
    public static readonly float[] TextSizes = { 0.5f, 0.75f, 1f, 1.25f, 1.5f, 2f };
    public static readonly float[] RankSizes = { 0.4f, 0.5f, 0.6f, 0.75f, 0.9f, 1f };
    public static readonly string[] RankPositions = { "Top Left", "Top Center", "Top Right", "Bottom Left", "Bottom Center", "Bottom Right" };

    private static List<Vector2Int> resolutions;
    private static bool loaded;

    // Project Settings > Audio > Global Volume (the game is mixed around it, 0.2). Master Volume scales it
    // instead of replacing it, so 100% sounds like the game always did.
    private static float projectVolume = 1f;
    private static void ApplyMasterVolume() => AudioListener.volume = projectVolume * MasterVolume;

    // Distinct screen sizes the monitor supports, smallest first
    public static List<Vector2Int> Resolutions
    {
        get
        {
            if (resolutions == null)
            {
                resolutions = Screen.resolutions.Select(r => new Vector2Int(r.width, r.height)).Distinct().OrderBy(r => r.x * r.y).ToList();
                if (resolutions.Count == 0) resolutions.Add(new Vector2Int(Screen.width, Screen.height));
            }
            return resolutions;
        }
    }

    // ---------------------------------------------------------------- load / save / apply
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void LoadOnStartup()
    {
        projectVolume = ReadProjectVolume();
        Load();
        ApplyDisplay();
        ApplyMasterVolume();
    }

    private static float ReadProjectVolume()
    {
#if UNITY_EDITOR
        // In the editor a volume set during a previous Play session can stick around, so read the setting itself
        UnityEngine.Object[] assets = UnityEditor.AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/AudioManager.asset");
        if (assets.Length > 0)
        {
            UnityEditor.SerializedProperty volume = new UnityEditor.SerializedObject(assets[0]).FindProperty("m_Volume");
            if (volume != null) return volume.floatValue;
        }
#endif
        return AudioListener.volume; // in a build this is still the Project Settings value at startup
    }

    public static void Load()
    {
        if (loaded) return;
        loaded = true;

        Vector2Int current = new Vector2Int(Screen.width, Screen.height);
        int currentIndex = Resolutions.FindIndex(r => r == current);
        if (currentIndex < 0) currentIndex = Resolutions.Count - 1; // unknown size: default to the largest

        ResolutionIndex = Mathf.Clamp(PlayerPrefs.GetInt(Prefix + "Resolution", currentIndex), 0, Resolutions.Count - 1);
        DisplayMode = (FullScreenMode)PlayerPrefs.GetInt(Prefix + "DisplayMode", (int)Screen.fullScreenMode);
        VSync = PlayerPrefs.GetInt(Prefix + "VSync", QualitySettings.vSyncCount > 0 ? 1 : 0) == 1;
        FrameLimit = PlayerPrefs.GetInt(Prefix + "FrameLimit", 60);

        MasterVolume = PlayerPrefs.GetFloat(Prefix + "Master", 1f);
        MusicVolume = PlayerPrefs.GetFloat(Prefix + "Music", 0.8f);
        SfxVolume = PlayerPrefs.GetFloat(Prefix + "Sfx", 1f);
        RowdyVoiceVolume = PlayerPrefs.GetFloat(Prefix + "RowdyVoice", 1f);
        CatVoiceVolume = PlayerPrefs.GetFloat(Prefix + "CatVoice", 1f);

        ScreenShake = PlayerPrefs.GetFloat(Prefix + "Shake", 1f);
        Vibration = PlayerPrefs.GetInt(Prefix + "Vibration", 1) == 1;
        DamageNumbers = PlayerPrefs.GetInt(Prefix + "DamageNumbers", 1) == 1;
        DamageNumberSize = PlayerPrefs.GetFloat(Prefix + "NumberSize", 1f);
        MessageSize = PlayerPrefs.GetFloat(Prefix + "MessageSize", 1f);
        ShowFps = PlayerPrefs.GetInt(Prefix + "ShowFps", 0) == 1;
        CrowdLimit = PlayerPrefs.GetInt(Prefix + "CrowdLimit", 4);
        StyleRankOn = PlayerPrefs.GetInt(Prefix + "RankOn", 1) == 1;
        StyleRankSize = PlayerPrefs.GetFloat(Prefix + "RankSize", 0.6f);
        StyleRankPosition = Mathf.Clamp(PlayerPrefs.GetInt(Prefix + "RankPosition", 1), 0, RankPositions.Length - 1);
    }

    private static void Save()
    {
        PlayerPrefs.SetInt(Prefix + "Resolution", ResolutionIndex);
        PlayerPrefs.SetInt(Prefix + "DisplayMode", (int)DisplayMode);
        PlayerPrefs.SetInt(Prefix + "VSync", VSync ? 1 : 0);
        PlayerPrefs.SetInt(Prefix + "FrameLimit", FrameLimit);
        PlayerPrefs.SetFloat(Prefix + "Master", MasterVolume);
        PlayerPrefs.SetFloat(Prefix + "Music", MusicVolume);
        PlayerPrefs.SetFloat(Prefix + "Sfx", SfxVolume);
        PlayerPrefs.SetFloat(Prefix + "RowdyVoice", RowdyVoiceVolume);
        PlayerPrefs.SetFloat(Prefix + "CatVoice", CatVoiceVolume);
        PlayerPrefs.SetFloat(Prefix + "Shake", ScreenShake);
        PlayerPrefs.SetInt(Prefix + "Vibration", Vibration ? 1 : 0);
        PlayerPrefs.SetInt(Prefix + "DamageNumbers", DamageNumbers ? 1 : 0);
        PlayerPrefs.SetFloat(Prefix + "NumberSize", DamageNumberSize);
        PlayerPrefs.SetFloat(Prefix + "MessageSize", MessageSize);
        PlayerPrefs.SetInt(Prefix + "ShowFps", ShowFps ? 1 : 0);
        PlayerPrefs.SetInt(Prefix + "CrowdLimit", CrowdLimit);
        PlayerPrefs.SetInt(Prefix + "RankOn", StyleRankOn ? 1 : 0);
        PlayerPrefs.SetFloat(Prefix + "RankSize", StyleRankSize);
        PlayerPrefs.SetInt(Prefix + "RankPosition", StyleRankPosition);
        PlayerPrefs.Save();
    }

    public static void ApplyDisplay()
    {
        QualitySettings.vSyncCount = VSync ? 1 : 0;
        Application.targetFrameRate = FrameLimit;
#if !UNITY_EDITOR
        Vector2Int size = Resolutions[Mathf.Clamp(ResolutionIndex, 0, Resolutions.Count - 1)];
        Screen.SetResolution(size.x, size.y, DisplayMode);
#endif
    }

    private static void Commit(bool display = false)
    {
        Save();
        if (display) ApplyDisplay();
        ApplyMasterVolume();
        Changed?.Invoke();
    }

    // ---------------------------------------------------------------- setters (used by the pause menu)
    public static void SetResolution(int index) { ResolutionIndex = Mathf.Clamp(index, 0, Resolutions.Count - 1); Commit(true); }
    public static void SetDisplayMode(FullScreenMode mode) { DisplayMode = mode; Commit(true); }
    public static void SetVSync(bool on) { VSync = on; Commit(true); }
    public static void SetFrameLimit(int fps) { FrameLimit = fps; Commit(true); }
    public static void SetMasterVolume(float v) { MasterVolume = Mathf.Clamp01(v); Commit(); }
    public static void SetMusicVolume(float v) { MusicVolume = Mathf.Clamp01(v); Commit(); }
    public static void SetSfxVolume(float v) { SfxVolume = Mathf.Clamp01(v); Commit(); }
    public static void SetRowdyVoiceVolume(float v) { RowdyVoiceVolume = Mathf.Clamp01(v); Commit(); }
    public static void SetCatVoiceVolume(float v) { CatVoiceVolume = Mathf.Clamp01(v); Commit(); }
    public static void SetScreenShake(float v) { ScreenShake = Mathf.Clamp01(v); Commit(); }
    public static void SetVibration(bool on) { Vibration = on; Commit(); }
    public static void SetDamageNumbers(bool on) { DamageNumbers = on; Commit(); }
    public static void SetDamageNumberSize(float v) { DamageNumberSize = Mathf.Clamp(v, 0.25f, 3f); Commit(); }
    public static void SetMessageSize(float v) { MessageSize = Mathf.Clamp(v, 0.25f, 3f); Commit(); }
    public static void SetShowFps(bool on) { ShowFps = on; Commit(); }
    public static void SetCrowdLimit(int n) { CrowdLimit = Mathf.Clamp(n, 0, 8); Commit(); }
    public static void SetStyleRankOn(bool on) { StyleRankOn = on; Commit(); }
    public static void SetStyleRankSize(float v) { StyleRankSize = Mathf.Clamp(v, 0.3f, 1.5f); Commit(); }
    public static void SetStyleRankPosition(int i) { StyleRankPosition = (i % RankPositions.Length + RankPositions.Length) % RankPositions.Length; Commit(); }

    public static string DisplayModeName(FullScreenMode mode)
    {
        switch (mode)
        {
            case FullScreenMode.ExclusiveFullScreen: return "Fullscreen";
            case FullScreenMode.FullScreenWindow: return "Borderless";
            case FullScreenMode.Windowed: return "Windowed";
            default: return mode.ToString();
        }
    }
}
