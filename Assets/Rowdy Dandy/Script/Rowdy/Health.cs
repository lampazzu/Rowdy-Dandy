using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public class Health : MonoBehaviour
{
    [SerializeField] public float startingHealth;
    public float currentHealth { get; private set; }
    private Animator anim;
    private SpriteRenderer redboy;
    [SerializeField] private AudioSource bloodhit;
    [SerializeField] private AudioSource deathSFX;
    [SerializeField] private AudioSource healSFX;
    private bool dead;
    [SerializeField] public float damageCooldown = 2f;
    public float lastDamageTime;
    [SerializeField] private bool isDead = false;
    [SerializeField] private Animator healingEffectAnimator;

    private bool isInvincible = false; // Invincibility flag
    [SerializeField] private float invincibilityDuration = 15f; // Adjust duration as needed

    private void Awake()
    {
        currentHealth = startingHealth;
        anim = GetComponent<Animator>();
        redboy = GetComponent<SpriteRenderer>();
    }

    public void AddHealth(float _value)
    {
        if (_value > 0 && currentHealth < startingHealth)
        {
            healSFX.Play();
            if (healingEffectAnimator != null)
                healingEffectAnimator.SetTrigger("PlayHealingEffect");
        }
        currentHealth = Mathf.Clamp(currentHealth + _value, 0, startingHealth);
    }

    public void Respawn()
    {
        dead = false;
        AddHealth(startingHealth);
        anim.ResetTrigger("dead");
        anim.Play("Idle");
        GetComponent<PlayerMovement>().enabled = true;
        redboy.color = Color.white;
    }

    public void TakeDamage(float _damage)
    {
        if (isInvincible) return; // Ignore damage when invincible

        if (Time.time - lastDamageTime > damageCooldown)
        {
            float before = currentHealth;
            currentHealth = Mathf.Clamp(currentHealth - _damage, 0, startingHealth);
            RunStats.RecordDamageTaken(before - currentHealth);
            if (currentHealth > 0)
            {
                anim.SetTrigger("hit");
                bloodhit.Play();
                lastDamageTime = Time.time;
                if (TryGetComponent(out PlayerHitReaction reaction)) reaction.OnHit(_damage);
            }
            else if (!dead)
            {
                anim.SetTrigger("dead");
                dead = true;
                RunStats.Deaths++;
                CatRoster.MarkDied(); // costs a cat once the scene reloads
                anim.SetTrigger("IsDead");
                gameObject.tag = "Untagged";
            }
        }
    }

    public void TriggerInvincibility()
    {
        StartCoroutine(InvincibilityRoutine());
    }

    private IEnumerator InvincibilityRoutine()
    {
        isInvincible = true;
        yield return new WaitForSeconds(invincibilityDuration);
        isInvincible = false;
    }

    void ResetScene()
    {
        Scene currentScene = SceneManager.GetActiveScene();
        SceneManager.LoadScene(currentScene.buildIndex);
    }

    void Update()
    {
        if (isDead)
        {
            ResetScene();
        }
    }
}
