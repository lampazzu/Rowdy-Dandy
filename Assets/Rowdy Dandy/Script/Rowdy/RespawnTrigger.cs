using System.Collections.Generic;
using UnityEngine;

public class RespawnTrigger : MonoBehaviour
{
    [Header("Settings")]
    [SerializeField] private AudioClip activationSound; // Sound effect to play on activation
    [SerializeField] private bool isActivated = false; // Indicates if the respawn point is active

    private AudioSource audioSource; // Audio source component
    private Animator anim;

    // Static list to keep track of all respawn points
    private static List<RespawnTrigger> allRespawnPoints = new List<RespawnTrigger>();

    private void Awake()
    {
        // Add an AudioSource component if not already attached
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
        }

        // Get the Animator component
        anim = GetComponent<Animator>();
        if (anim == null)
        {
            Debug.LogWarning("Animator component not found on RespawnTrigger object.");
        }

        // Register this respawn point
        allRespawnPoints.Add(this);
    }

    private void OnDestroy()
    {
        // Unregister this respawn point when it is destroyed
        allRespawnPoints.Remove(this);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player") && !isActivated)
        {
            // Deactivate all other respawn points
            DeactivateAllRespawnPoints();

            // Save the player's current position as the new respawn point
            Vector2 spawnPoint = other.transform.position;
            PlayerPrefs.SetFloat("RespawnX", spawnPoint.x);
            PlayerPrefs.SetFloat("RespawnY", spawnPoint.y);
            PlayerPrefs.Save();

            // Mark this trigger as activated
            isActivated = true;

            // Play activation sound
            PlayActivationSound();

            // Set animator "isActivated" to true
            if (anim != null)
            {
                anim.SetBool("isActivated", true);
            }

            // Additional actions or function calls
            OnRespawnPointActivated();
        }
    }

    private void DeactivateAllRespawnPoints()
    {
        foreach (var respawnPoint in allRespawnPoints)
        {
            respawnPoint.isActivated = false;

            // Set animator "isActivated" to false for other respawn points
            if (respawnPoint.anim != null)
            {
                respawnPoint.anim.SetBool("isActivated", false);
            }
        }
    }

    private void PlayActivationSound()
    {
        // Play sound effect if assigned
        if (activationSound != null)
        {
            audioSource.PlayOneShot(activationSound);
        }
    }

    private void OnRespawnPointActivated()
    {
        // Custom actions that occur upon activation (e.g., UI notification, achievement, etc.)
        Debug.Log("Respawn point activated!");
    }
}