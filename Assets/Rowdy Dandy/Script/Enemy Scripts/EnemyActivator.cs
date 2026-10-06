using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EnemyActivator : MonoBehaviour
{
    private List<GameObject> enemies = new List<GameObject>();

    private void Start()
    {
        // Get all child objects (enemies) and disable them initially
        foreach (Transform child in transform)
        {
            child.gameObject.SetActive(false);
            enemies.Add(child.gameObject);
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        // Check if the object that entered the trigger is on the "Spawner" layer
        if (other.gameObject.layer == LayerMask.NameToLayer("Spawner"))
        {
            ActivateEnemies(); // Activate enemies here
        }
    }

    private void ActivateEnemies()
    {
        foreach (GameObject enemy in enemies)
        {
            enemy.SetActive(true);  // Activate the enemy by setting it active
        }
    }
}
