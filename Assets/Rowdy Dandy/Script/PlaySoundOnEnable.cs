using UnityEngine;

// Plays a SoundManager sound every time this object is switched on (e.g. by an animation clip turning on an attack hitbox).
public class PlaySoundOnEnable : MonoBehaviour
{
    [Tooltip("Name of the sound in the SoundManager list.")]
    [SerializeField] private string soundName = "WolfAttack";

    private void OnEnable()
    {
        if (!string.IsNullOrEmpty(soundName) && SoundManager.Instance != null)
        {
            SoundManager.Instance.PlaySound(soundName);
        }
    }
}
