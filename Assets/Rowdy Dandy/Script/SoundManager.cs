using UnityEngine;
using System.Collections.Generic;

public class SoundManager : MonoBehaviour
{
    public static SoundManager Instance { get; private set; }

    [System.Serializable]
    public class Sound
    {
        public string soundName;
        public AudioClip clip;
        [Range(0f, 1f)] public float volume = 1f;
        [Range(0.1f, 3f)] public float pitch = 1f;
        [Tooltip("Rowdy's own voice: also scaled by the Rowdy Voice setting. Sounds named 'Grunt...' count automatically.")]
        public bool rowdyVoice;
    }

    private static bool IsRowdyVoice(Sound sound) =>
        sound.rowdyVoice || sound.soundName.StartsWith("Grunt", System.StringComparison.OrdinalIgnoreCase);

    public List<Sound> sounds = new List<Sound>();
    private Dictionary<string, Sound> soundDictionary = new Dictionary<string, Sound>();

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
            return;
        }

        // DontDestroyOnLoad only works on root objects; when this lives under Rowdy it just follows Rowdy's lifetime
        if (transform.parent == null)
        {
            DontDestroyOnLoad(gameObject);
        }

        foreach (Sound sound in sounds)
        {
            soundDictionary[sound.soundName] = sound;
        }
    }

    // One-shot for scripts holding their own clip (weapon drops, cats...). Same route as PlaySound:
    // played at the camera so it isn't faded by distance, scaled by the Sound Effects setting.
    public static void PlaySfx(AudioClip clip, float volume = 1f)
    {
        volume *= GameSettings.SfxVolume;
        if (!AudioGuard.Safe(clip, ref volume)) return;
        Camera cam = Camera.main;
        Vector3 playPosition = cam != null ? cam.transform.position : Vector3.zero;
        if (!AudioGuard.SafePosition(playPosition)) return;
        AudioSource.PlayClipAtPoint(clip, playPosition, volume);
    }

    public void PlaySound(string soundName)
    {
        // Events with no sound name filled in are treated as "no sound" instead of warning every time
        if (string.IsNullOrEmpty(soundName)) return;

        if (soundDictionary.TryGetValue(soundName, out Sound sound))
        {
            // Clip can be missing/destroyed after a scene reload since this object survives scene changes
            if (sound.clip == null)
            {
                Debug.LogWarning("Sound clip missing or destroyed: " + soundName);
                return;
            }

            // Camera.main can be briefly null during scene transitions
            Camera cam = Camera.main;
            Vector3 playPosition = cam != null ? cam.transform.position : transform.position;

            float volume = sound.volume * GameSettings.SfxVolume;
            if (IsRowdyVoice(sound)) volume *= GameSettings.RowdyVoiceVolume;
            if (!AudioGuard.Safe(sound.clip, ref volume) || !AudioGuard.SafePosition(playPosition)) return;
            AudioSource.PlayClipAtPoint(sound.clip, playPosition, volume);
        }
        else
        {
            Debug.LogWarning("Sound not found: " + soundName);
        }
    }
}

