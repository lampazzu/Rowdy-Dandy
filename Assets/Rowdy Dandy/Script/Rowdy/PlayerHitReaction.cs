using System.Collections;
using UnityEngine;

// Makes getting hit read clearly and feel weighty. Called by Health.TakeDamage (added automatically by PlayerMovement).
// Works alongside the GettingHit clip (its red tint / isWounded stay as they are):
//  - short freeze on impact (pauses Rowdy's animator, like HitStop does)
//  - knockback AWAY from whatever hit him, with a small hop, and a brief moment without control
//  - blinking while invincible (Health's damage cooldown), so it's clear when he can't be hurt
//  - optional impact sound / hurt voices
// Untick the component to go back to the old behavior.
[RequireComponent(typeof(PlayerMovement))]
public class PlayerHitReaction : MonoBehaviour
{
    [Header("Impact Freeze")]
    [Tooltip("Real-time seconds Rowdy's animation freezes on impact. 0 = off.")]
    [SerializeField] private float freezeTime = 0.07f;

    [Header("Knockback")]
    [SerializeField] private float knockbackSpeed = 3f;
    [SerializeField] private float knockbackHop = 2f;
    [Tooltip("Seconds without movement control after the hit.")]
    [SerializeField] private float stunTime = 0.15f;
    [Tooltip("How far to look for what hit Rowdy (to push him away from it).")]
    [SerializeField] private float sourceSearchRadius = 3f;

    [Header("Invincibility Blink")]
    [SerializeField] private bool blink = true;
    [SerializeField] private float blinkRate = 14f;

    [Header("Sound (optional)")]
    [Tooltip("Played on hit in addition to Health's bloodhit sound. Leave empty for none.")]
    [SerializeField] private AudioClip impactSound;
    [Tooltip("One is picked at random per hit. Leave empty for none.")]
    [SerializeField] private AudioClip[] hurtVoices;
    [SerializeField, Range(0f, 1f)] private float soundVolume = 0.8f;

    private PlayerMovement movement;
    private Health health;
    private Animator animator;
    private SpriteRenderer spriteRenderer;
    private Collider2D body;
    private AudioSource audioSource;
    private Coroutine blinkRoutine, freezeRoutine;

    private void Awake()
    {
        movement = GetComponent<PlayerMovement>();
        health = GetComponent<Health>();
        animator = GetComponent<Animator>();
        spriteRenderer = GetComponent<SpriteRenderer>();
        body = GetComponent<Collider2D>();
    }

    private void OnDisable()
    {
        if (spriteRenderer != null) spriteRenderer.enabled = true;
    }

    public void OnHit(float damage)
    {
        if (!isActiveAndEnabled) return;

        Vector2 center = body != null ? (Vector2)body.bounds.center : (Vector2)transform.position;
        float direction = AwayFromSource(center);

        if (freezeTime > 0f && animator != null)
        {
            if (freezeRoutine != null) StopCoroutine(freezeRoutine);
            freezeRoutine = StartCoroutine(FreezeThenKnockback(direction));
        }
        else
        {
            movement.ApplyKnockback(new Vector2(direction * knockbackSpeed, knockbackHop), stunTime);
        }

        PlaySounds();

        if (blink && spriteRenderer != null)
        {
            if (blinkRoutine != null) StopCoroutine(blinkRoutine);
            blinkRoutine = StartCoroutine(Blink(health != null ? health.damageCooldown : 1f));
        }
    }

    // +1 = push right, -1 = push left: away from the nearest enemy / hazard, or backwards if none is found
    private float AwayFromSource(Vector2 center)
    {
        float best = float.MaxValue;
        float direction = transform.localScale.x >= 0f ? -1f : 1f;

        foreach (Collider2D col in Physics2D.OverlapCircleAll(center, sourceSearchRadius))
        {
            if (col == null || col.transform.IsChildOf(transform)) continue;
            bool isThreat = col.CompareTag("Enemy") || col.GetComponentInParent<EnemyHealth>() != null || col.GetComponent<Spike>() != null;
            if (!isThreat) continue;

            Vector2 closest = col.ClosestPoint(center);
            float distance = Vector2.Distance(closest, center);
            if (distance >= best) continue;

            float dx = center.x - col.bounds.center.x;
            if (Mathf.Abs(dx) < 0.05f) continue; // directly above/below: keep the default
            best = distance;
            direction = Mathf.Sign(dx);
        }
        return direction;
    }

    private IEnumerator FreezeThenKnockback(float direction)
    {
        float previousSpeed = animator.speed;
        animator.speed = 0f;
        yield return new WaitForSecondsRealtime(freezeTime);
        animator.speed = previousSpeed > 0f ? previousSpeed : 1f;
        movement.ApplyKnockback(new Vector2(direction * knockbackSpeed, knockbackHop), stunTime);
        freezeRoutine = null;
    }

    private void PlaySounds()
    {
        bool hasVoice = hurtVoices != null && hurtVoices.Length > 0;
        if (impactSound == null && !hasVoice) return;

        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 0f;
        }

        if (impactSound != null) audioSource.PlayOneShot(impactSound, soundVolume);
        if (hasVoice)
        {
            AudioClip voice = hurtVoices[Random.Range(0, hurtVoices.Length)];
            if (voice != null)
            {
                audioSource.pitch = Random.Range(0.95f, 1.05f);
                audioSource.PlayOneShot(voice, soundVolume);
            }
        }
    }

    private IEnumerator Blink(float duration)
    {
        float end = Time.time + duration;
        while (Time.time < end && (health == null || health.currentHealth > 0f))
        {
            spriteRenderer.enabled = Mathf.FloorToInt(Time.time * blinkRate * 2f) % 2 == 0;
            yield return null;
        }
        spriteRenderer.enabled = true;
        blinkRoutine = null;
    }

}
