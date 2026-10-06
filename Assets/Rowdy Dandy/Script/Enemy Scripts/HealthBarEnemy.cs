using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class HealthBarEnemy : MonoBehaviour
{
    [SerializeField] private EnemyHealth enemyHealth; // Reference to the EnemyHealth script

    [SerializeField] private Image totalHealth;  // Image representing the total health (background bar)
    [SerializeField] private Image currentHealth; // Image representing the current health (foreground bar)
    [SerializeField] private Image delayedHealth; // The secondary health bar that will update slowly

    [SerializeField] private float lerpSpeed = 5f; // Speed at which the delayed health bar updates

    private float delayedHealthTarget; // Target value for delayed health

    void Start()
    {
        if (enemyHealth == null || totalHealth == null || currentHealth == null || delayedHealth == null)
        {
            Debug.LogWarning("HealthBarEnemy: Missing references. Please assign all required components.");
            enabled = false; // Disable the script to prevent errors
            return;
        }

        totalHealth.fillAmount = 1f;
        delayedHealthTarget = enemyHealth.currentenemyHealth / enemyHealth.startingenemyHealth;
    }

    void Update()
    {
        if (enemyHealth == null || currentHealth == null || delayedHealth == null)
            return;

        float currentHealthRatio = enemyHealth.currentenemyHealth / enemyHealth.startingenemyHealth;
        currentHealth.fillAmount = currentHealthRatio;

        if (Mathf.Abs(delayedHealth.fillAmount - currentHealthRatio) > 0.01f)
        {
            delayedHealth.fillAmount = Mathf.Lerp(delayedHealth.fillAmount, currentHealthRatio, lerpSpeed * Time.deltaTime);
        }
    }
}
