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

        // Interact here = rest (reload at this checkpoint, morning)
        if (GetComponent<CheckpointRest>() == null) gameObject.AddComponent<CheckpointRest>();
    }

    // Is this point on (or within margin of) any checkpoint's trigger? Used to pause the enemy waves while resting.
    public static bool IsNear(Vector2 point, float margin)
    {
        foreach (RespawnTrigger r in allRespawnPoints)
        {
            if (r == null || !r.isActiveAndEnabled) continue;
            if (!r.TryGetComponent(out Collider2D c) || !c.enabled) continue;
            Bounds b = c.bounds;
            b.Expand(new Vector3(margin * 2f, margin * 2f, 0f));
            b.extents = new Vector3(b.extents.x, b.extents.y, 1000f);
            if (b.Contains(point)) return true;
        }
        return false;
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

            // Already the saved checkpoint (Rowdy respawned on it)?
            bool wasAlreadySaved = false;
            if (PlayerPrefs.HasKey("RespawnX") && TryGetComponent(out Collider2D own))
            {
                Bounds b = own.bounds;
                b.Expand(new Vector3(3f, 3f, 0f));
                b.extents = new Vector3(b.extents.x, b.extents.y, 1000f);
                wasAlreadySaved = b.Contains(new Vector3(PlayerPrefs.GetFloat("RespawnX"), PlayerPrefs.GetFloat("RespawnY"), 0f));
            }

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

            // Additional actions or function calls (not when he just respawned here: it was already his checkpoint)
            if (!wasAlreadySaved) OnRespawnPointActivated(other);
        }
    }

    [Header("Activation juice")]
    [Tooltip("Fraction of Rowdy's max health restored when a checkpoint is activated")]
    [SerializeField, Range(0f, 1f)] private float healFraction = 0.2f;

    // "CHECKPOINT!" banner, heal, golden rings + pixel fountain, a little hit-stop and shake
    private void OnRespawnPointActivated(Collider2D rowdy)
    {
        Collider2D own = GetComponent<Collider2D>();
        Vector3 center = own != null ? own.bounds.center : transform.position;
        Vector3 top = own != null ? new Vector3(center.x, own.bounds.max.y, 0f) : transform.position + Vector3.up;

        RunStats.Checkpoints++;
        IconPopup.Show(top + Vector3.up * 0.5f, null, "CHECKPOINT!", new Color(1f, 0.85f, 0.3f), 1.6f, 2.2f);
        PulseRing.Spawn(center, new Color(1f, 0.85f, 0.35f, 1f), 1.8f, 0.5f);
        PulseRing.Spawn(center, new Color(1f, 1f, 1f, 0.8f), 3.2f, 0.8f);
        FXParticle.Burst(top, new Color(1f, 0.85f, 0.35f), 26, 2f, 5f, 6f, 1f, true);
        FXParticle.Burst(center, new Color(1f, 1f, 1f), 10, 1f, 3f, -1f, 0.8f);
        FXSound.Play("Checkpoint", 0.9f, 1f);
        TimeSlowController.HitStop(0.08f, 0.1f);
        ScreenShake.Impulse(0.35f);
        GamepadRumble.Pulse(0.3f, 0.5f, 0.15f);
        StartCoroutine(Squash());
        Tutorials.Show(Tutorials.Topic.Checkpoint, null, 1.2f);

        if (rowdy != null && rowdy.TryGetComponent(out Health health) && healFraction > 0f)
            health.AddHealth(health.startingHealth * healFraction);
    }

    // The checkpoint itself bounces
    private System.Collections.IEnumerator Squash()
    {
        Vector3 baseScale = transform.localScale;
        for (float t = 0f; t < 1f; t += Time.unscaledDeltaTime / 0.35f)
        {
            float k = Mathf.Sin(t * Mathf.PI * 2f) * (1f - t) * 0.25f;
            transform.localScale = new Vector3(baseScale.x * (1f + k), baseScale.y * (1f - k), baseScale.z);
            yield return null;
        }
        transform.localScale = baseScale;
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

}