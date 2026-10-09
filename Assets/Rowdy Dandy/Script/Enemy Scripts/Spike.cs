using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Spike : MonoBehaviour
{
    [SerializeField] private float damage;
    [SerializeField] private bool isFrecha;
    [SerializeField] private GameObject frechaExplosionPrefab;
    [SerializeField] private Animator anima;

    [Header("Fairness (enemy contact boxes that start an attack, e.g. the Horse Rider)")]
    [Tooltip("Boxes on an enemy that trigger its attack (Anima set): touching it starts the attack animation, and the damage lands telegraphTime later only if Rowdy is still in the box (no instant contact damage). Never from off screen.")]
    [SerializeField] private bool telegraphContact = true;
    [SerializeField] private float telegraphTime = 0.2f;

    private EnemyHealth owner;
    private Collider2D box;
    private bool pending;
    private float lastAttack = -10f;

    private bool bornWithOwner;

    private void Awake()
    {
        owner = GetComponentInParent<EnemyHealth>();
        box = GetComponent<Collider2D>();
        // Active from the start = a contact box (Horse Rider's CollisionDmg). Boxes an attack clip switches on later
        // (Sharkwolf's bite, wolves' slashes) wake up mid-attack and must hit right away.
        bornWithOwner = owner != null && (!owner.HasAwoken || Time.frameCount - owner.AwakeFrame <= 1);
    }

    private void OnEnable() => pending = false;

    // The hit's damage under the current balance (Jarvis: per enemy + per zone; Lamp: the Inspector value)
    private float Damage => Balance.EnemyDamage(this, owner, damage);

    // Contact box on an enemy that kicks off its attack animation (not arrows / bombs / attack hitboxes)
    private bool IsAttackStarter => telegraphContact && bornWithOwner && anima != null && !isFrecha && owner != null;

    private void OnTriggerStay2D(Collider2D collision)
    {
        // Rowdy was already inside while it was off screen / recovering: try again
        if (IsAttackStarter && !pending && Time.time - lastAttack > 1f && collision.CompareTag("Player")) TryTelegraphedAttack(collision);
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        // Check if the object colliding has the "Player" tag
        if (collision.CompareTag("Player"))
        {
            if (IsAttackStarter)
            {
                if (!pending) TryTelegraphedAttack(collision);
                return;
            }

            // Get the Health component from the player
            Health playerHealth = collision.GetComponent<Health>();

            if (playerHealth != null)
            {
                // Apply damage to the player
                playerHealth.TakeDamage(Damage);

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

    private void TryTelegraphedAttack(Collider2D player)
    {
        if (owner.enemydead || StatusEffects.IsStunned(owner.gameObject)) return;
        if (!EnemyFairness.OnScreen(EnemyFairness.BodyCenter(owner), 0.01f)) return;
        // Runs on the enemy: the attack clip switches this box off on its first frame
        owner.StartCoroutine(TelegraphedAttack(player));
    }

    private IEnumerator TelegraphedAttack(Collider2D player)
    {
        pending = true;
        Vector3 offset = box != null ? box.bounds.center - owner.transform.position : Vector3.zero;
        Vector2 size = box != null ? (Vector2)box.bounds.size : Vector2.one * 0.7f;
        anima.SetTrigger("shark attack"); // the attack animation itself is the warning
        yield return new WaitForSeconds(telegraphTime);
        pending = false;
        lastAttack = Time.time;
        if (owner == null || owner.enemydead || player == null) yield break;

        // Lands only if Rowdy is still where the box is now (dodged / jumped away = no damage)
        foreach (Collider2D hit in Physics2D.OverlapBoxAll(owner.transform.position + offset, size * 1.1f, 0f))
        {
            if (hit != player) continue;
            if (player.TryGetComponent(out Health playerHealth)) playerHealth.TakeDamage(Damage);
            break;
        }
    }
}