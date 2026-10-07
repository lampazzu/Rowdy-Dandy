using System.Collections;
using UnityEngine;
using UnityEngine.Rendering.Universal; // Ensure this namespace is included

public class HitStop : MonoBehaviour
{
    private float hitStopDuration = 0.1f;
    private Animator playerAnimator;
    private bool isHitStopActive = false;
    private bool isAttackingBeforeHitStop;
    private float originalSpeed = 1f;
    private Light2D light2D;

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Enemy"))
        {
            StartCoroutine(HitStopCoroutine());
        }
    }

    private void Awake()
    {
        light2D = GetComponent<Light2D>(); // Corrected casing here
    }

    private IEnumerator HitStopCoroutine()
    {
        if (!isHitStopActive)
        {
            playerAnimator = transform.root.GetComponent<Animator>();
            if (playerAnimator != null)
            {
                isHitStopActive = true;

                if (light2D != null)
                {
                    light2D.enabled = true;
                }

                isAttackingBeforeHitStop = IsInAttackAnimation(playerAnimator);
                originalSpeed = playerAnimator.speed;
                playerAnimator.speed = 0f;

                float startTime = Time.realtimeSinceStartup;

                while (Time.realtimeSinceStartup < startTime + hitStopDuration)
                {
                    yield return null;
                    // Check if the animation state changes
                    if (!isAttackingBeforeHitStop && IsInAttackAnimation(playerAnimator))
                    {
                        break; // Exit the loop if the animation state changes
                    }
                }

                playerAnimator.speed = originalSpeed;
                isHitStopActive = false;

                if (light2D != null)
                {
                    light2D.enabled = false;
                }
            }
        }
    }

    // The hitbox can be switched off mid hit-stop (e.g. a clip cut short), which kills the coroutine
    // before it restores the speed and leaves the animator frozen. Restore it here instead.
    private void OnDisable()
    {
        if (!isHitStopActive) return;

        if (playerAnimator != null) playerAnimator.speed = originalSpeed;
        if (light2D != null) light2D.enabled = false;
        isHitStopActive = false;
    }

    private bool IsInAttackAnimation(Animator animator)
    {
        AnimatorStateInfo currentState = animator.GetCurrentAnimatorStateInfo(0);
        return currentState.IsTag("JumpAttack") || currentState.IsTag("DuckingAttack") || currentState.IsTag("NeutralAttack");
    }
}