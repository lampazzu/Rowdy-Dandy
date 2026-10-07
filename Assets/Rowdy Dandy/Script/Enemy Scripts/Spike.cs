using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Spike : MonoBehaviour
{
    [SerializeField] private float damage;
    [SerializeField] private bool isFrecha;
    [SerializeField] private GameObject frechaExplosionPrefab;
    [SerializeField] private Animator anima;
    




   

        private void OnTriggerEnter2D(Collider2D collision)

    {
        // Check if the object colliding has the "Player" tag
        if (collision.CompareTag("Player"))
        {
            // Get the Health component from the player
            Health playerHealth = collision.GetComponent<Health>();
           

            if (playerHealth != null)
            {
                // Apply damage to the player
                playerHealth.TakeDamage(damage);

                // Not every spike has an Animator (e.g. Frecha arrows), so only trigger when one is assigned
                if (anima != null)
                {
                    anima.SetTrigger("shark attack");
                }
            }

            // If this spike is a "Frecha", instantiate explosion and destroy spike
            if (isFrecha)
            {
                if (frechaExplosionPrefab != null)
                {
                    // Instantiate the explosion effect
                    Instantiate(frechaExplosionPrefab, transform.position, Quaternion.identity);
                }
                // Destroy the spike game object
                Destroy(gameObject);
                
            }
        }


        if (collision.CompareTag("Ground"))

        {
            if (isFrecha)
            {
                if (frechaExplosionPrefab != null)
                {
                    // Instantiate the explosion effect
                    Instantiate(frechaExplosionPrefab, transform.position, Quaternion.identity);
                }
                // Destroy the spike game object
                Destroy(gameObject);
            }
        }
    }

}
