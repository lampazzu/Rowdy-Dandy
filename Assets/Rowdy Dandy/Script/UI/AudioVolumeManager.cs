using System.Collections.Generic;
using UnityEngine;

// Applies the Music / Sound Effects volume settings to every AudioSource in the game (no mixer needed).
//  - Music: looping clips 30s or longer (the soundtrack, ambience like the waves). Keeps playing while paused.
//  - Sound effects: everything else. SoundManager's one-shot sounds are scaled in SoundManager itself.
// Master volume is AudioListener.volume = Project Settings global volume x Master (set by GameSettings). Created automatically.
public class AudioVolumeManager : MonoBehaviour
{
    private class Tracked
    {
        public float baseVolume;
        public AudioClip clip;
        public bool isMusic;
    }

    private static AudioVolumeManager instance;
    private readonly Dictionary<AudioSource, Tracked> tracked = new Dictionary<AudioSource, Tracked>();
    private readonly List<AudioSource> toRemove = new List<AudioSource>();
    private float scanTimer;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Create()
    {
        if (instance != null) return;
        var go = new GameObject("AudioVolumeManager (auto)");
        DontDestroyOnLoad(go);
        instance = go.AddComponent<AudioVolumeManager>();
    }

    private void OnEnable() { GameSettings.Changed += ApplyAll; }
    private void OnDisable() { GameSettings.Changed -= ApplyAll; }

    private void Update()
    {
        // New sources appear all the time (spawned enemies, effects), so look for them a couple of times a second
        scanTimer -= Time.unscaledDeltaTime;
        if (scanTimer > 0f) return;
        scanTimer = 1f;
        Scan();
    }

    private void Scan()
    {
        toRemove.Clear();
        foreach (var pair in tracked) if (pair.Key == null) toRemove.Add(pair.Key);
        foreach (AudioSource dead in toRemove) tracked.Remove(dead);

        foreach (AudioSource source in FindObjectsByType<AudioSource>(FindObjectsSortMode.None))
        {
            if (source.gameObject.name == "One shot audio") continue; // SoundManager one-shots, already scaled
            if (SoundtrackManager.Owns(source)) continue;              // fades its own volume with the Music setting

            if (!tracked.TryGetValue(source, out Tracked info))
            {
                info = new Tracked { baseVolume = source.volume, clip = source.clip, isMusic = IsMusic(source) };
                tracked[source] = info;
                source.ignoreListenerPause = info.isMusic;
                Apply(source, info);
            }
            else if (info.clip != source.clip)
            {
                // (Re)classify when the clip changes
                info.clip = source.clip;
                info.isMusic = IsMusic(source);
                source.ignoreListenerPause = info.isMusic;
                Apply(source, info);
            }
        }
    }

    private static bool IsMusic(AudioSource source)
    {
        return source.clip != null && source.loop && source.clip.length >= 30f;
    }

    private void ApplyAll()
    {
        foreach (var pair in tracked) if (pair.Key != null) Apply(pair.Key, pair.Value);
    }

    private static void Apply(AudioSource source, Tracked info)
    {
        source.volume = info.baseVolume * (info.isMusic ? GameSettings.MusicVolume : GameSettings.SfxVolume);
    }
}
