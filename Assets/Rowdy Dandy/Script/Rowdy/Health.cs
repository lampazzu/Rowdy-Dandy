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

    [Header("Overheal (pelican hearts at full health)")]
    [Tooltip("Most extra life on top of the normal maximum (100 = double life)")]
    [SerializeField] private float maxOverheal = 100f;
    [Tooltip("Overheal lost per second, plus overhealDecayPercent of what's left")]
    [SerializeField] private float overhealDecay = 4f;
    [SerializeField] private float overhealDecayPercent = 0.08f;
    [Tooltip("Seconds a fresh overheal holds before it starts draining")]
    [SerializeField] private float overhealHold = 0.6f;

    // Extra life above the maximum: soaks damage first, drains quickly. Drawn on the health bar (HealthBar) and as a gold aura (RowdyAura).
    public float Overheal { get; private set; }
    public float MaxOverheal => maxOverheal;
    public bool IsDead => dead;
    private float overhealHoldUntil;

    private void Awake()
    {
        currentHealth = startingHealth;
        anim = GetComponent<Animator>();
        redboy = GetComponent<SpriteRenderer>();
        if (GetComponent<RowdyAura>() == null) gameObject.AddComponent<RowdyAura>();
        if (GetComponent<RowdyBuffs>() == null) gameObject.AddComponent<RowdyBuffs>();
        if (GetComponent<BoonRunner>() == null) gameObject.AddComponent<BoonRunner>(); // level-up boons + the werewolf
    }

    public void AddHealth(float _value) => AddHealth(_value, false);

    // overheal: what doesn't fit under the maximum becomes overheal (pelican hearts)
    public void AddHealth(float _value, bool overheal)
    {
        if (_value <= 0f) { currentHealth = Mathf.Clamp(currentHealth + _value, 0, startingHealth); return; }
        if (dead) return;

        float room = startingHealth - currentHealth;
        float healed = Mathf.Min(room, _value);
        float extra = overheal ? Mathf.Min(_value - healed, maxOverheal - Overheal) : 0f;

        if (healed > 0f || extra > 0f)
        {
            if (healSFX != null) healSFX.Play();
            if (healingEffectAnimator != null) healingEffectAnimator.SetTrigger("PlayHealingEffect");
            HealFX.Play(this, healed + extra, extra > 0f);
        }
        currentHealth = Mathf.Clamp(currentHealth + healed, 0, startingHealth);
        RunStats.Healed += healed;
        RunStats.OverhealGained += extra;
        if (extra > 0f)
        {
            Overheal += extra;
            overhealHoldUntil = Time.time + overhealHold;
        }
    }

    public bool CanTakeOverheal => !dead && (currentHealth < startingHealth || Overheal < maxOverheal - 0.5f);

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
        if (isInvincible || DevTools.GodMode || CheckpointRest.Resting || FirstDrop.Running) return; // Ignore damage when invincible
        if (Time.time < invulnerableUntil) return; // Nine Lives grace

        if (Time.time - lastDamageTime > damageCooldown)
        {
            // The Peak's armor eats the hit (still counts as a hit for the cooldown, so one attack = one charge)
            if (_damage > 0f && !dead && RowdyBuffs.TryBlock(this))
            {
                lastDamageTime = Time.time;
                return;
            }

            _damage = BoonRunner.ModifyIncoming(_damage); // Fur Coat, Glass Jaw, werewolf hide

            float before = currentHealth;
            float soaked = Mathf.Min(Overheal, Mathf.Max(0f, _damage));
            // Nine Lives: the hit that would kill him leaves him standing instead
            if (!dead && _damage > 0f && currentHealth - (_damage - soaked) <= 0f && BoonRunner.PreventDeath(this))
            {
                Overheal = 0f;
                lastDamageTime = Time.time;
                RunStats.RecordDamageTaken(before - currentHealth);
                StyleRank.OnPlayerHurt();
                BoonRunner.OnPlayerHurt();
                return;
            }
            Overheal -= soaked;
            currentHealth = Mathf.Clamp(currentHealth - (_damage - soaked), 0, startingHealth);
            RunStats.RecordDamageTaken(before - currentHealth + soaked);
            if (before - currentHealth + soaked > 0f) { StyleRank.OnPlayerHurt(); BoonRunner.OnPlayerHurt(); } // drops two style ranks
            if (currentHealth > 0)
            {
                anim.SetTrigger("hit");
                bloodhit.Play();
                lastDamageTime = Time.time;
                if (TryGetComponent(out PlayerHitReaction reaction)) reaction.OnHit(_damage);
                Blood.Spill(BodyCenter(), -Mathf.Sign(transform.localScale.x), 6);
                Tutorials.Show(Tutorials.Topic.Hurt, null, 0.8f);
            }
            else if (!dead)
            {
                anim.SetTrigger("dead");
                dead = true;
                Overheal = 0f;
                RunStats.Deaths++;
                CatRoster.MarkDied(); // costs a cat once the scene reloads
                anim.SetTrigger("IsDead");
                gameObject.tag = "Untagged";
                Blood.Spill(BodyCenter(), -Mathf.Sign(transform.localScale.x), 16);
                DeathFX.Play(); // slow motion + the world loses its colour
            }
        }
    }

    private Vector3 BodyCenter()
    {
        Collider2D c = GetComponent<Collider2D>();
        return c != null ? c.bounds.center : transform.position + Vector3.up * 0.4f;
    }

    // Boons (Nine Lives): a short window where nothing hurts, and setting the health directly
    private float invulnerableUntil;
    public void GrantInvulnerability(float seconds) => invulnerableUntil = Mathf.Max(invulnerableUntil, Time.time + seconds);
    public void SetHealth(float value) => currentHealth = Mathf.Clamp(value, 1f, startingHealth);

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

        // Overheal drains fast (faster the more there is)
        if (Overheal > 0f && Time.time > overhealHoldUntil)
        {
            Overheal = Mathf.Max(0f, Overheal - (overhealDecay + Overheal * overhealDecayPercent) * Time.deltaTime);
        }
    }
}
