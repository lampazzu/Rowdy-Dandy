using UnityEngine;

public class AudioManager : MonoBehaviour
{
    public GameObject audioPrefab; // Assign your audio prefab in the inspector
    public float volume = 0.5f; // Set your desired volume level (0.0 to 1.0)

    public void PlaySound()
    {
        GameObject audioInstance = Instantiate(audioPrefab);
        AudioSource audioSource = audioInstance.GetComponent<AudioSource>();

        if (audioSource != null)
        {
            audioSource.volume = volume; // Set the volume for this instance
            audioSource.Play();
            Destroy(audioInstance, audioSource.clip.length); // Destroy after playing
        }
    }
}