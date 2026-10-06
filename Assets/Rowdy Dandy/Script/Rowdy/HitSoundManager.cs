using System.Collections;
using UnityEngine;

public class HitSoundManager : MonoBehaviour
{
    [SerializeField] private AudioSource[] audioSources; // 5 AudioSource components
    [SerializeField] private float cooldownTime = 0.2f; // Cooldown to avoid spam
    [SerializeField] private float volumeBoost = 2.5f; // Multiply the volume beyond normal limits

    private bool canPlay = true;

    private void Update()
    {
        if (PlayerDamage.hitEnemy && canPlay)
        {
            PlayRandomHitSound();
            StartCoroutine(PlayCooldown());
        }
    }

    private void PlayRandomHitSound()
    {
        if (audioSources.Length > 0)
        {
            int randomIndex = Random.Range(0, audioSources.Length); // Pick a random AudioSource
            AudioSource selectedAudio = audioSources[randomIndex];

            if (selectedAudio != null)
            {
                selectedAudio.volume = Mathf.Clamp01(selectedAudio.volume) * volumeBoost; // Boost volume artificially
                selectedAudio.Play();
            }
        }
        else
        {
            Debug.LogWarning("HitSoundManager: No AudioSources assigned!");
        }
    }

    private IEnumerator PlayCooldown()
    {
        canPlay = false;
        yield return new WaitForSeconds(cooldownTime);
        canPlay = true;
    }
}
