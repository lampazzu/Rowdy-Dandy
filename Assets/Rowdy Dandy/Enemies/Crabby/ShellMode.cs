using UnityEngine;

public class ShellMode : MonoBehaviour
{
    public Animator animator; // Reference to the Animator component

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision.tag == "Player")
        {
            animator.SetBool("shell", true); // Set the "shell" boolean parameter to true
        }
    }

    private void OnTriggerExit2D(Collider2D collision)
    {
        if (collision.tag == "Player")
        {
            animator.SetBool("shell", false);
        }
    }
}