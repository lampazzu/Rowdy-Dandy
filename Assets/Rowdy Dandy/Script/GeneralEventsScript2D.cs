using UnityEngine;
using UnityEngine.Events;

public class GeneralEventsScript2D : MonoBehaviour
{
    [System.Serializable]
    public class CooldownEvent
    {
        public float cooldownDuration = 2f;
        public UnityEvent eventToTrigger;
        [HideInInspector]
        public float cooldownTimer;
    }

    [Header("Events")]
    public CooldownEvent[] enterEvents;
    public CooldownEvent[] stayEvents;
    public CooldownEvent[] exitEvents;

    [Header("Target Tag")]
    public string targetTag = "Bullet"; // Tag to determine target collider, defaults to "Bullet"

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag(targetTag))
        {
            TriggerEvents(enterEvents);
        }
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        if (other.CompareTag(targetTag))
        {
            TriggerEvents(stayEvents);
        }
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (other.CompareTag(targetTag))
        {
            TriggerEvents(exitEvents);
        }
    }

    private void Update()
    {
        UpdateCooldownTimers(enterEvents);
        UpdateCooldownTimers(stayEvents);
        UpdateCooldownTimers(exitEvents);
    }

    private void TriggerEvents(CooldownEvent[] events)
    {
        foreach (var cooldownEvent in events)
        {
            if (cooldownEvent.cooldownTimer <= 0f)
            {
                cooldownEvent.eventToTrigger.Invoke();
                cooldownEvent.cooldownTimer = cooldownEvent.cooldownDuration;
            }
        }
    }

    private void UpdateCooldownTimers(CooldownEvent[] events)
    {
        foreach (var cooldownEvent in events)
        {
            if (cooldownEvent.cooldownTimer > 0f)
            {
                cooldownEvent.cooldownTimer -= Time.deltaTime;
            }
        }
    }
}
