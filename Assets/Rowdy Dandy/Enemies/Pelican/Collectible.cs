using UnityEngine;

public class Collectible : MonoBehaviour
{
    [SerializeField] private float healAmount = 10f; // Amount of health this collectible restores
    private int ignorePlayerLayer;
    private int originalLayer;

    private void Awake()
    {
        ignorePlayerLayer = LayerMask.NameToLayer("IgnorePlayer");
        originalLayer = LayerMask.NameToLayer("projectileLayer");
    }

    private void Update()
    {
        GameObject player = GameObject.FindGameObjectWithTag("Player");

        if (player != null)
        {
            Health playerHealth = player.GetComponent<Health>();

            if (playerHealth != null)
            {
                // Ignore collision if player health is full
                if (playerHealth.currentHealth >= playerHealth.startingHealth)
                {
                    gameObject.layer = ignorePlayerLayer;
                }
                else
                {
                    gameObject.layer = originalLayer; // Allow collision
                }
            }
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        Health playerHealth = other.GetComponent<Health>();

        if (playerHealth != null && playerHealth.currentHealth < playerHealth.startingHealth)
        {
            playerHealth.AddHealth(healAmount);
            Destroy(gameObject);
        }
    }
}
