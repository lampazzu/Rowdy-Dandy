using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

// Keeps the game from going mute in big fights.
//   - One bad number (NaN / infinity volume or pitch, a NaN position on a 3D one-shot) poisons Unity's whole mix and
//     everything stays silent until the audio system restarts. AudioGuard sits on the AudioListener, checks the final
//     mix every buffer, and if it went bad it restarts the audio system and brings the music and loops back.
//   - Safe() is the gate every one-shot helper goes through (FXSound, SoundManager, UISound, BoonPicker...).
//   - Same clip spammed in one frame (ten enemies dying at once) is capped, so the voices aren't all eaten by copies.
//   - PauseMenu can leave AudioListener.pause on if a menu is destroyed mid-pause (scene reload): unstuck here too.
// Created automatically, re-attached to the listener on every scene load.
public class AudioGuard : MonoBehaviour
{
    private static AudioGuard instance;
    private static volatile bool poisoned;
    private static float poisonedSince = -1f;
    private static float lastReset = -10f;

    // per clip: how many times it started this frame
    private static readonly Dictionary<AudioClip, int> startsThisFrame = new Dictionary<AudioClip, int>();
    private static int countedFrame = -1;
    private const int MaxSameClipPerFrame = 3;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Hook()
    {
        Attach();
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
        AudioSettings.OnAudioConfigurationChanged -= OnConfigChanged;
        AudioSettings.OnAudioConfigurationChanged += OnConfigChanged;
    }

    private static void OnSceneLoaded(Scene s, LoadSceneMode m)
    {
        Attach();
        // Nothing is paused right after a load: a menu that paused and got destroyed with the old scene can't unpause
        if (!PauseMenu.IsPaused && AudioListener.pause) AudioListener.pause = false;
    }

    private static void Attach()
    {
        AudioListener listener = FindFirstObjectByType<AudioListener>();
        if (listener == null || listener.GetComponent<AudioGuard>() != null) return;
        instance = listener.gameObject.AddComponent<AudioGuard>();
    }

    // ---------------------------------------------------------------- the gate for one-shots
    // false = don't play it (bad numbers, or this clip already started enough times this frame)
    public static bool Safe(AudioClip clip, ref float volume, ref float pitch)
    {
        if (clip == null) return false;
        if (float.IsNaN(volume) || float.IsInfinity(volume)) return false;
        if (float.IsNaN(pitch) || float.IsInfinity(pitch)) pitch = 1f;
        volume = Mathf.Clamp(volume, 0f, 4f);
        pitch = Mathf.Clamp(pitch, 0.1f, 3f);
        if (volume <= 0f) return false;

        if (countedFrame != Time.frameCount)
        {
            countedFrame = Time.frameCount;
            startsThisFrame.Clear();
        }
        startsThisFrame.TryGetValue(clip, out int n);
        if (n >= MaxSameClipPerFrame) return false;
        startsThisFrame[clip] = n + 1;
        return true;
    }

    public static bool Safe(AudioClip clip, ref float volume)
    {
        float pitch = 1f;
        return Safe(clip, ref volume, ref pitch);
    }

    public static bool SafePosition(Vector3 p) =>
        !(float.IsNaN(p.x) || float.IsNaN(p.y) || float.IsNaN(p.z) || float.IsInfinity(p.x) || float.IsInfinity(p.y) || float.IsInfinity(p.z));

    // ---------------------------------------------------------------- watching the final mix (audio thread)
    private void OnAudioFilterRead(float[] data, int channels)
    {
        bool bad = false;
        for (int i = 0; i < data.Length; i++)
        {
            float v = data[i];
            if (float.IsNaN(v) || float.IsInfinity(v)) { bad = true; data[i] = 0f; }
        }
        if (bad) poisoned = true;
    }

    private void Update()
    {
        if (!PauseMenu.IsPaused && AudioListener.pause) AudioListener.pause = false;
        if (float.IsNaN(AudioListener.volume)) GameSettings.ReapplyMasterVolume();

        if (!poisoned) { poisonedSince = -1f; return; }
        poisoned = false;
        if (poisonedSince < 0f) { poisonedSince = Time.unscaledTime; return; }
        // Still bad a moment later: the mix is stuck, restart the audio system (at most every 2 s)
        if (Time.unscaledTime - poisonedSince < 0.2f || Time.unscaledTime - lastReset < 2f) return;
        poisonedSince = -1f;
        RestartAudio();
    }

    private static readonly List<AudioSource> loopsToRestore = new List<AudioSource>();

    private static void RestartAudio()
    {
        lastReset = Time.unscaledTime;
        Debug.LogWarning("AudioGuard: the audio mix went bad (NaN), restarting the audio system");
        loopsToRestore.Clear();
        foreach (AudioSource s in FindObjectsByType<AudioSource>(FindObjectsSortMode.None))
            if (s.isPlaying && s.loop) loopsToRestore.Add(s);
        AudioSettings.Reset(AudioSettings.GetConfiguration());
    }

    private static void OnConfigChanged(bool deviceWasChanged)
    {
        // Every source stopped: bring back what was looping (ambience) and the soundtrack
        foreach (AudioSource s in loopsToRestore)
            if (s != null && s.isActiveAndEnabled && !s.isPlaying) s.Play();
        loopsToRestore.Clear();
        SoundtrackManager.Revive();
        GameSettings.ReapplyMasterVolume();
    }
}
