using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerDetector : MonoBehaviour
{
    [SerializeField] private float damage;
    [SerializeField] private bool isFrecha;
    [SerializeField] private GameObject frechaExplosionPrefab;
    [SerializeField] private Animator anima;
    [SerializeField] private bool isChargeAttack = false;
    [SerializeField] private bool isSharkWolf = false;






    private void OnTriggerEnter2D(Collider2D collision)
    {
        // Check if the object colliding has the "Player" tag
        if (collision.CompareTag("Player"))
        {
            // Trigger the animation regardless of whether it's a charge attack
            anima.SetTrigger("shark attack");


            if (isSharkWolf)

            {

                if (isChargeAttack)
                {
                    return;
                }

            }


            Health playerHealth = collision.GetComponent<Health>();

            if (playerHealth != null)
            {
                // Apply damage to the player
                playerHealth.TakeDamage(damage);
            }

            // If this spike is a "Frecha", instantiate explosion and destroy spike
            if (isFrecha)
            {
                if (frechaExplosionPrefab != null)
                {
                    Instantiate(frechaExplosionPrefab, transform.position, Quaternion.identity);
                }
                Destroy(gameObject);
            }
        }

        if (collision.CompareTag("Ground"))
        {
            if (isFrecha)
            {
                if (frechaExplosionPrefab != null)
                {
                    Instantiate(frechaExplosionPrefab, transform.position, Quaternion.identity);
                }
                Destroy(gameObject);
            }
        }
    }

}
