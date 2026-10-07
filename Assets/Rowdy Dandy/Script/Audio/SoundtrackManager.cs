using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

// Plays the music for the stretch of the level Rowdy is in (Resources/Soundtrack.asset), crossfading on changes.
// Survives scene reloads, so dying doesn't restart the song. Uses the Music volume setting and keeps playing
// while paused. Scene AudioSources that play one of the soundtrack's songs (e.g. PelichAnus' own copy of his song)
// are silenced so nothing plays twice. Created automatically.
public class SoundtrackManager : MonoBehaviour
{
    private static SoundtrackManager instance;

    private SoundtrackConfig config;
    private AudioSource[] players;
    private float[] fades;            // 0..1 per player
    private float[] sectionVolumes;
    private int activePlayer;
    private SoundtrackConfig.Section current;
    private Transform rowdy;
    private float findTimer;
    private readonly HashSet<AudioClip> soundtrackClips = new HashSet<AudioClip>();
    private readonly List<AudioSource> sceneCopies = new List<AudioSource>();

    public static bool Owns(AudioSource source) => instance != null && source != null && source.gameObject == instance.gameObject;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Create()
    {
        if (instance != null) return;
        SoundtrackConfig loaded = Resources.Load<SoundtrackConfig>("Soundtrack");
        if (loaded == null || loaded.sections.Count == 0) return; // no soundtrack set up: leave the scenes' own music alone

        var go = new GameObject("SoundtrackManager (auto)");
        DontDestroyOnLoad(go);
        instance = go.AddComponent<SoundtrackManager>();
        instance.config = loaded;
    }

    private void Awake()
    {
        players = new AudioSource[2];
        fades = new float[2];
        sectionVolumes = new float[2];
        for (int i = 0; i < 2; i++)
        {
            AudioSource source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = true;
            source.spatialBlend = 0f;
            source.priority = 0;
            source.ignoreListenerPause = true; // the pause menu pauses sound effects, not the music
            source.volume = 0f;
            players[i] = source;
        }
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void Start()
    {
        foreach (SoundtrackConfig.Section section in config.sections)
            if (section.music != null) soundtrackClips.Add(section.music);
        FindSceneCopies();
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        rowdy = null;
        findTimer = 0f;
        if (!config.keepPlayingOnReload) current = null;
        FindSceneCopies();
    }

    private void FindSceneCopies()
    {
        sceneCopies.Clear();
        if (soundtrackClips.Count == 0) return;

        // Only in scenes the soundtrack covers; other scenes keep their own music
        string sceneName = SceneManager.GetActiveScene().name;
        bool covered = false;
        foreach (SoundtrackConfig.Section section in config.sections) if (AppliesTo(section, sceneName)) { covered = true; break; }
        if (!covered) return;

        foreach (AudioSource source in FindObjectsByType<AudioSource>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (Owns(source)) continue;
            if (source.clip != null && soundtrackClips.Contains(source.clip)) sceneCopies.Add(source);
        }
    }

    private void Update()
    {
        // Silence the scene's own copies of the soundtrack songs
        for (int i = sceneCopies.Count - 1; i >= 0; i--)
        {
            AudioSource copy = sceneCopies[i];
            if (copy == null) { sceneCopies.RemoveAt(i); continue; }
            if (copy.isPlaying) copy.Stop();
        }

        if (rowdy == null)
        {
            findTimer -= Time.unscaledDeltaTime;
            if (findTimer <= 0f)
            {
                findTimer = 0.5f;
                GameObject player = GameObject.FindGameObjectWithTag("Player");
                if (player != null) rowdy = player.transform;
            }
        }

        if (rowdy != null) ChooseSection(rowdy.position.x);
        UpdateFades();
    }

    private void ChooseSection(float x)
    {
        string sceneName = SceneManager.GetActiveScene().name;

        // Stay in the current section until clearly out of it
        if (current != null && AppliesTo(current, sceneName) &&
            x >= current.fromX - config.borderMargin && x < current.toX + config.borderMargin) return;

        SoundtrackConfig.Section next = null;
        foreach (SoundtrackConfig.Section section in config.sections)
        {
            if (AppliesTo(section, sceneName) && x >= section.fromX && x < section.toX) { next = section; break; }
        }
        if (next == current) return;

        current = next;
        Debug.Log($"Music: {(next != null ? next.name : "(none)")} (x {x:F1})");
        PlayClip(next != null ? next.music : null, next != null ? next.volume : 0f);
    }

    private static bool AppliesTo(SoundtrackConfig.Section section, string sceneName) =>
        string.IsNullOrEmpty(section.scene) || section.scene == sceneName;

    private void PlayClip(AudioClip clip, float volume)
    {
        AudioSource active = players[activePlayer];

        // Same song carries on into the next section (just adjust its level)
        if (clip != null && active.clip == clip && active.isPlaying)
        {
            sectionVolumes[activePlayer] = volume;
            return;
        }

        // Fade the current one out and the new one in on the other player
        activePlayer = 1 - activePlayer;
        AudioSource next = players[activePlayer];
        next.Stop();
        next.clip = clip;
        sectionVolumes[activePlayer] = volume;
        fades[activePlayer] = 0f;
        if (clip != null) next.Play();
    }

    private void UpdateFades()
    {
        float step = config.crossfade > 0f ? Time.unscaledDeltaTime / config.crossfade : 1f;
        for (int i = 0; i < 2; i++)
        {
            bool isActive = i == activePlayer && players[i].clip != null;
            fades[i] = Mathf.MoveTowards(fades[i], isActive ? 1f : 0f, step);
            players[i].volume = fades[i] * sectionVolumes[i] * GameSettings.MusicVolume;
            if (!isActive && fades[i] <= 0f && players[i].isPlaying) players[i].Stop();
        }
    }
}
