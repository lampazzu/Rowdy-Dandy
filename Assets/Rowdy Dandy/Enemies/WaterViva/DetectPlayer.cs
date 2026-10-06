using UnityEngine;

public class DetectPlayer : MonoBehaviour
{
    private void OnTriggerEnter2D(Collider2D other)
    {
        // Check if the collider has the "Player" tag
        if (other.CompareTag("Player"))
        {
            // Assuming you have an Animator component attached to the same GameObject
            Animator animator = GetComponent<Animator>();

            // Set the animation trigger "Splish"
            animator.SetTrigger("Splish");
        }
    }
    private void OnTriggerExit2d(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            // Assuming you have an Animator component attached to the same GameObject
            Animator animator = GetComponent<Animator>();

            // Set the animation trigger "Splish"
            animator.ResetTrigger("Splish");
        }
    }
}