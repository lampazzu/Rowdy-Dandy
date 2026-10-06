using UnityEngine;

public class OffsetAnimation
    : MonoBehaviour
{
    private Animator animator;

    void Start()
    {
        animator = GetComponent<Animator>();

        if (animator != null)
        {
            // Randomly offset the starting time of the animation
            float randomOffset = Random.Range(0f, 1f);  // Random value between 0 and 1
            animator.Play(animator.GetCurrentAnimatorStateInfo(0).fullPathHash, -1, randomOffset);
        }
    }
}
