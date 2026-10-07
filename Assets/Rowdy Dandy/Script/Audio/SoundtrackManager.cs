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

    // Where each song was when it last faded out, so coming back to a biome picks the song up where it left off
    private readonly Dictionary<AudioClip, float> resumeTimes = new Dictionary<AudioClip, float>();

    // Pause menu muffle: a low-pass on every music source (soundtrack + looping ambience), eased in and out
    private const float MuffledCutoff = 650f;
    private const float OpenCutoff = 22000f;
    private const float MuffleSpeed = 6f;         // per second, in "fraction of the way" terms
    private const float MuffledVolume = 0.6f;
    private static readonly List<AudioLowPassFilter> muffleFilters = new List<AudioLowPassFilter>();
    private static float muffle;                  // 0 = clear, 1 = fully muffled
    private static float appliedMuffle = -1f;
    private static int appliedFilterCount = -1;
    public static float MuffleVolume => Mathf.Lerp(1f, MuffledVolume, muffle);

    public static bool Owns(AudioSource source) => instance != null && source != null && source.transform.IsChildOf(instance.transform);

    // Music sources found by AudioVolumeManager get muffled with the soundtrack while paused
    public static void AddMuffle(AudioSource source)
    {
        if (source == null || source.GetComponent<AudioLowPassFilter>() != null) return;
        AudioLowPassFilter filter = source.gameObject.AddComponent<AudioLowPassFilter>();
        filter.cutoffFrequency = OpenCutoff;
        filter.lowpassResonanceQ = 1f;
        filter.enabled = false;
        muffleFilters.Add(filter);
    }

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
            // One child per player, so each can carry its own low-pass filter (filters act on their GameObject's source)
            var holder = new GameObject("Player " + (i + 1));
            holder.transform.SetParent(transform, false);
            AudioSource source = holder.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = true;
            source.spatialBlend = 0f;
            source.priority = 0;
            source.ignoreListenerPause = true; // the pause menu pauses sound effects, not the music
            source.volume = 0f;
            players[i] = source;
            AddMuffle(source);
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
        UpdateMuffle();
        UpdateFades();
    }

    private static void UpdateMuffle()
    {
        float target = PauseMenu.IsPaused ? 1f : 0f;
        muffle = Mathf.MoveTowards(muffle, target, MuffleSpeed * Time.unscaledDeltaTime);
        if (muffle == appliedMuffle && muffleFilters.Count == appliedFilterCount) return;
        appliedMuffle = muffle;
        appliedFilterCount = muffleFilters.Count;

        // Exponential sweep sounds even, a linear one jumps straight to "dull"
        float cutoff = Mathf.Exp(Mathf.Lerp(Mathf.Log(OpenCutoff), Mathf.Log(MuffledCutoff), muffle));
        bool on = muffle > 0.001f;
        for (int i = muffleFilters.Count - 1; i >= 0; i--)
        {
            AudioLowPassFilter filter = muffleFilters[i];
            if (filter == null) { muffleFilters.RemoveAt(i); continue; }
            filter.cutoffFrequency = cutoff;
            filter.enabled = on;
        }
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

        // Walked back before the old song finished fading out: just fade it back in, no restart
        int other = 1 - activePlayer;
        if (clip != null && players[other].clip == clip && players[other].isPlaying)
        {
            activePlayer = other;
            sectionVolumes[activePlayer] = volume;
            return;
        }

        // Fade the current one out and the new one in on the other player
        activePlayer = other;
        AudioSource next = players[activePlayer];
        if (next.isPlaying) Remember(next);
        next.Stop();
        next.clip = clip;
        sectionVolumes[activePlayer] = volume;
        fades[activePlayer] = 0f;
        if (clip == null) return;

        // Carry on from where this song was left (long tracks get to play out instead of restarting every visit)
        if (resumeTimes.TryGetValue(clip, out float time) && time > 0f && time < clip.length - 1f) next.time = time;
        next.Play();
    }

    private void Remember(AudioSource source)
    {
        if (source.clip != null) resumeTimes[source.clip] = source.time;
    }

    private void UpdateFades()
    {
        float step = config.crossfade > 0f ? Time.unscaledDeltaTime / config.crossfade : 1f;
        float pauseVolume = MuffleVolume;
        for (int i = 0; i < 2; i++)
        {
            bool isActive = i == activePlayer && players[i].clip != null;
            fades[i] = Mathf.MoveTowards(fades[i], isActive ? 1f : 0f, step);
            players[i].volume = fades[i] * sectionVolumes[i] * GameSettings.MusicVolume * pauseVolume;
            if (!isActive && fades[i] <= 0f && players[i].isPlaying)
            {
                Remember(players[i]);
                players[i].Stop();
            }
        }
    }
}
