using UnityEngine;

// Menu sounds (pause menu, Rowdy Notes...), fighting-game style: a tick on every cursor move, a punchy confirm,
// a soft back, a card flick for value / page changes. Clips live in Resources/UISounds (copied from the
// "Sounds from hags hand" pack) - swap a file there to change a sound. Plays while the game is paused,
// follows the Sound Effects volume. Created on first use.
public static class UISound
{
    public enum Cue { Move, Confirm, Back, Change, Page, Open, Locked, Unlock }

    // file name in Resources/UISounds, volume, random pitch range
    private static readonly (string file, float volume, float pitchJitter)[] Cues =
    {
        ("UI_Move", 0.55f, 0.04f),
        ("UI_Confirm", 0.7f, 0f),
        ("UI_Back", 0.6f, 0f),
        ("UI_Change", 0.5f, 0.05f),
        ("UI_Page", 0.6f, 0.03f),
        ("UI_Open", 0.55f, 0f),
        ("UI_Locked", 0.5f, 0f),
        ("UI_Unlock", 0.65f, 0f),
    };

    private static AudioSource source;
    private static AudioClip[] clips;
    private static float lastMoveTime = -1f;

    public static bool Owns(AudioSource candidate) => candidate != null && candidate == source;

    public static void Play(Cue cue)
    {
        if (!Ready()) return;
        int i = (int)cue;
        AudioClip clip = clips[i];
        if (clip == null) return;

        // Holding a direction repeats moves quickly; don't let the ticks pile into a buzz
        if (cue == Cue.Move)
        {
            if (Time.unscaledTime - lastMoveTime < 0.045f) return;
            lastMoveTime = Time.unscaledTime;
        }

        var (_, volume, jitter) = Cues[i];
        source.pitch = 1f + Random.Range(-jitter, jitter);
        source.PlayOneShot(clip, volume * GameSettings.SfxVolume);
    }

    private static bool Ready()
    {
        if (source != null) return true;

        var go = new GameObject("UISound (auto)");
        Object.DontDestroyOnLoad(go);
        source = go.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.spatialBlend = 0f;
        source.ignoreListenerPause = true; // menus are used while AudioListener.pause is on
        source.priority = 16;

        clips = new AudioClip[Cues.Length];
        for (int i = 0; i < Cues.Length; i++) clips[i] = Resources.Load<AudioClip>("UISounds/" + Cues[i].file);
        return true;
    }
}
