using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TriggerInvincibility : MonoBehaviour
{
    private Health healthScript;

    void Start()
    {
        healthScript = GameObject.FindWithTag("Player").GetComponent<Health>();
    }

    void OnEnable() // This runs automatically when the script is enabled
    {
        if (healthScript != null)
        {
            healthScript.TriggerInvincibility();
            Debug.Log("Invincibility activated!");
        }
        else
        {
            Debug.LogError("Health script not found! Is the Player tagged correctly?");
        }
    }
}
