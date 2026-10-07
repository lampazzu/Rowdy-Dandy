using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using Cinemachine;

public class EnemyHealth : MonoBehaviour
{
    // Who the next TakeDamageEnemy call comes from (kill feed + stats). Set by PlayerDamage, the samurai cat, drowning.
    private static KillCredit nextHitCredit;
    public static void CreditNextHit(KillCredit credit) => nextHitCredit = credit;

    private KillCredit lastCredit;
    private float lastCreditTime = -99f;

    // Stuck-in-GetHit safety net (see UpdateHitRecovery)
    private static readonly int GetHitState = Animator.StringToHash("GetHit");
    private static readonly int WalkState = Animator.StringToHash("Walk");
    private float stuckInHitTimer;

    // Animator parameter names, cached (Animator.parameters allocates a new array on every call)
    private readonly HashSet<string> animatorParameters = new HashSet<string>();
    private RuntimeAnimatorController cachedController;

    [SerializeField] private CinemachineVirtualCamera cinemachineCam;
    [SerializeField] private Transform newFollowTarget;
    [SerializeField] private Transform originalFollowTarget;

    private static GameObject currentActiveHUD;

    [SerializeField] public bool isKickable;
    [SerializeField] public float startingenemyHealth;
    public float currentenemyHealth { get; private set; }
    private Animator anima;
    private SpriteRenderer redco;
    [SerializeField] private AudioSource hitfx;
    [SerializeField] private AudioSource deathSFX;
    public bool enemydead;
    [SerializeField] private bool isBeingHit = false;
    [SerializeField] private bool isRevived = false;
    [SerializeField] private bool backtoidle = false;
    public bool NoPetFollow { get; private set; } = false;
    public bool IsObject => isObject; // plants, statues... (pets can skip these)
    [SerializeField] public UnityEvent onHurtWolf;
    [SerializeField] public UnityEvent onEnemyKill;
    [SerializeField] public UnityEvent onCriticalDamage;
    public Animator animbleed;
    [SerializeField] private bool isParryTime = false;
    [SerializeField] private bool isParryDestructive = false;
    [SerializeField] private bool isPelican = false;
    [SerializeField] private bool isOldMan = false;

    [SerializeField] private bool isPillarFlamed = false;
    public static bool isPillarA = false;
    public static bool isPillarB = false;
    private static bool hasCameraBeenReset = false;

    [SerializeField] private bool isAltarA = false;
    [SerializeField] private bool isAltarB = false;
    [SerializeField] private bool isPillarAlit = false;
    [SerializeField] private bool isPillarBlit = false;
    [SerializeField] private bool isObject = false;

    [SerializeField] private GameObject pelicanHeartPrefab;
    [SerializeField] private GameObject transformWolf;
    [SerializeField] private GameObject bloodPrefab;
    [SerializeField] private float parryDamage = 0f;

    [Header("EXP Drop Settings")]
    [SerializeField] private GameObject expGemPrefab; // Drag EXPGem prefab here
    [SerializeField] private int expGemAmount = 1;     // How many gems drop on death
    [SerializeField] private bool giveDirectEXP = false; // Check true if you want direct EXP instead of dropping gem items
    [SerializeField] private float directEXPValue = 20f;

    [Header("Weapon Drop Settings")]
    [SerializeField] private GameObject weaponDropPrefab; // Drop item prefab
    [SerializeField, Range(0f, 100f)] private float dropChancePercent = 30f; // Drop chance %

    [Header("Floating Damage UI & FX")]
    [SerializeField] private GameObject damageTextPrefab;
    [SerializeField] private GameObject critFXPrefab;
    [Tooltip("Small bar over the head that shows up when hit (look: Resources/EnemyHealthBarStyle). Never shown on objects.")]
    [SerializeField] private bool showHealthBar = true;

    [Header("Controller Rumble Settings (Legacy System)")]
    [SerializeField] private bool enableParryRumble = true;
    [SerializeField, Range(0f, 1f)] private float parryLowFrequency = 0.6f;
    [SerializeField, Range(0f, 1f)] private float parryHighFrequency = 1.0f;
    [SerializeField] private float parryRumbleDuration = 0.2f;

    private GameObject bloodKill;
    private GameObject parryFX;
    private GameObject critFX;

    public Vector3 initialpositionenemy;
    public Quaternion initialrotationenemy;
    public AnimatorOverrideController[] hitOverrideControllers;

    private void Awake()
    {
        anima = GetComponent<Animator>();
        redco = GetComponent<SpriteRenderer>();
        currentenemyHealth = startingenemyHealth;
        initialpositionenemy = transform.position;
        initialrotationenemy = transform.rotation;
        enemydead = false;
        SetAnimatorBool("IsBeingHit", false);

        bloodKill = Resources.Load<GameObject>("BloodKill");
        parryFX = Resources.Load<GameObject>("RDR_Exparry");

        if (critFXPrefab != null)
            critFX = critFXPrefab;
        else
            critFX = Resources.Load<GameObject>("RDR_Excrit");

        if (damageTextPrefab == null)
            damageTextPrefab = Resources.Load<GameObject>("DamageTextPrefab");

        if (cinemachineCam != null)
            originalFollowTarget = cinemachineCam.Follow;

        if (showHealthBar && !isObject)
            EnemyHealthBar.Attach(this);
    }

    private void Update()
    {
        if (backtoidle)
        {
            SetAnimatorTrigger("back to idle");
        }

        if (isRevived)
        {
            currentenemyHealth = startingenemyHealth;
            enemydead = false;
            gameObject.tag = "Enemy";
        }

        if (currentenemyHealth == startingenemyHealth)
        {
            isRevived = false;
        }

        if (isPillarA && isPillarAlit)
        {
            SetAnimatorTrigger("PillarFlamed");
        }

        if (isPillarB && isPillarBlit)
        {
            SetAnimatorTrigger("PillarFlamed");
        }

        if (isPillarA && isPillarB)
        {
            ActivatePillarSequence();
        }

        UpdateHitRecovery();
    }

    // GetHit only leaves through 'back to idle' (when not moving) or 'moving'. If neither comes (the hit cut off an
    // attack before its clip re-enabled BackToIdle, and the enemy is standing still), it froze on the last hit frame.
    // Once the hit clip has finished and it's still there, send it on to Walk (Walk drops to idle by itself).
    private void UpdateHitRecovery()
    {
        if (anima == null || enemydead || isBeingHit || isObject || !anima.isActiveAndEnabled || anima.runtimeAnimatorController == null)
        {
            stuckInHitTimer = 0f;
            return;
        }

        AnimatorStateInfo state = anima.GetCurrentAnimatorStateInfo(0);
        if (state.shortNameHash != GetHitState || anima.IsInTransition(0) || state.normalizedTime < 1f)
        {
            stuckInHitTimer = 0f;
            return;
        }

        stuckInHitTimer += Time.deltaTime;
        if (stuckInHitTimer < 0.25f) return;

        stuckInHitTimer = 0f;
        if (anima.HasState(0, WalkState)) anima.Play(WalkState, 0, 0f);
        else SetAnimatorTrigger("back to idle");
    }

    private void ActivatePillarSequence()
    {
        SetAnimatorTrigger("Activate");

        if (cinemachineCam != null && newFollowTarget != null && !hasCameraBeenReset)
        {
            cinemachineCam.Follow = newFollowTarget;
            cinemachineCam.LookAt = newFollowTarget;
            StartCoroutine(ResetCameraAfterDelay(3f));
        }
    }

    private IEnumerator ResetCameraAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);

        if (!hasCameraBeenReset && cinemachineCam != null && originalFollowTarget != null)
        {
            cinemachineCam.Follow = originalFollowTarget;
            cinemachineCam.LookAt = originalFollowTarget;
            hasCameraBeenReset = true;
        }
    }

    public void AddHealthEnemy(float _value)
    {
        currentenemyHealth = Mathf.Clamp(currentenemyHealth + _value, 0, startingenemyHealth);
    }

    public void TakeDamageEnemy(float _damage, bool isCritical = false)
    {
        KillCredit credit = nextHitCredit;
        nextHitCredit = null;
        bool byRowdySide = credit != null && credit.kind != KillCredit.Kind.World;
        if (byRowdySide && !enemydead)
        {
            lastCredit = credit;
            lastCreditTime = Time.time;
            RunStats.RecordHit(_damage, credit.with);
            if (isCritical) RunStats.CriticalHits++;
            if (isParryTime && currentenemyHealth > 0f) RunStats.Counters++;
        }

        currentenemyHealth = Mathf.Clamp(currentenemyHealth - _damage, 0, startingenemyHealth);

        SpawnDamageText(_damage);

        if (isCritical)
        {
            SpawnCustomText("CRITICAL!", new Color(1f, 0.2f, 0.2f), 1.6f);
            onCriticalDamage?.Invoke();

            if (critFX != null)
            {
                Instantiate(critFX, transform.position, Quaternion.identity);
            }
        }

        if (currentenemyHealth > 0)
        {
            isBeingHit = true;
            onHurtWolf.Invoke();
            anima.Play("GetHit", 0, 0f);

            if (isParryTime)
            {
                SpawnCustomText("COUNTER!", new Color(0f, 0.9f, 1f), 1.6f);

                if (enableParryRumble)
                {
                    TriggerRumble(parryLowFrequency, parryHighFrequency, parryRumbleDuration);
                }

                if (parryFX != null)
                {
                    Instantiate(parryFX, transform.position, Quaternion.identity);
                    if (isParryDestructive)
                    {
                        currentenemyHealth -= parryDamage;
                        SpawnDamageText(parryDamage);
                    }
                }
            }

            if (bloodPrefab != null)
            {
                GameObject bloodInstance = Instantiate(bloodPrefab, transform.position, Quaternion.identity);
                Vector3 bloodScale = bloodInstance.transform.localScale;
                bloodInstance.transform.localScale = bloodScale;
            }

            StopAllCoroutines();
            StartCoroutine(ResetIsBeingHitAfterDelay(0.15f));

            if (animbleed != null && currentenemyHealth < startingenemyHealth * 100f)
            {
                animbleed.SetTrigger("Bleeding");
            }
        }
        else
        {
            if (!enemydead)
            {
                onEnemyKill.Invoke();
                SetAnimatorTrigger("destroyed");
                enemydead = true;
                if (TryGetComponent(out EnemyCorpse corpse)) corpse.OnKilled();
                ReportKill(credit);

                // --- SPAWN EXP GEMS OR GIVE EXP DIRECTLY ---
                if (giveDirectEXP)
                {
                    if (PlayerStats.Instance != null)
                    {
                        PlayerStats.Instance.AddEXP(directEXPValue);
                    }
                }
                else if (expGemPrefab != null)
                {
                    for (int i = 0; i < expGemAmount; i++)
                    {
                        Vector3 dropOffset = new Vector3(UnityEngine.Random.Range(-0.2f, 0.2f), UnityEngine.Random.Range(-0.2f, 0.2f), 0f);
                        Instantiate(expGemPrefab, transform.position + dropOffset, Quaternion.identity);
                    }
                }

                // --- SPAWN WEAPON DROP ON DEATH ---
                if (weaponDropPrefab != null && UnityEngine.Random.Range(0f, 100f) <= dropChancePercent)
                {
                    Instantiate(weaponDropPrefab, transform.position, Quaternion.identity);
                }

                if (bloodKill != null && !isObject)
                {
                    Instantiate(bloodKill, transform.position, Quaternion.identity);
                }

                if (isOldMan && transformWolf != null)
                {
                    Instantiate(transformWolf, transform.position, Quaternion.identity);
                    Destroy(gameObject);
                }

                if (deathSFX != null) deathSFX.Play();
                StartCoroutine(ChangeTagAfterDelay(0.1f));

                if (isPelican && pelicanHeartPrefab != null)
                {
                    Instantiate(pelicanHeartPrefab, transform.position, Quaternion.identity);
                }

                if (isAltarA) isPillarA = true;
                if (isAltarB) isPillarB = true;
            }
        }

        SetAnimatorBool("IsBeingHit", isBeingHit);
    }

    // Kill feed + stats. Drowned (or otherwise world-killed) soon after Rowdy / a cat hit it = still their kill.
    private void ReportKill(KillCredit credit)
    {
        if (isObject)
        {
            if (credit != null && credit.kind != KillCredit.Kind.World) RunStats.ObjectsSmashed++;
            return;
        }

        KillCredit killer = credit;
        bool worldKill = killer == null || killer.kind == KillCredit.Kind.World;
        if (worldKill && lastCredit != null && Time.time - lastCreditTime < 5f)
        {
            killer = new KillCredit
            {
                kind = lastCredit.kind,
                name = lastCredit.name,
                icon = killer != null && killer.icon != null ? killer.icon : lastCredit.icon,
                with = lastCredit.with,
            };
            worldKill = false;
        }

        try
        {
            KillFeed.Describe(this, out string victimName, out Sprite portrait);
            if (!worldKill)
            {
                RunStats.RecordKill(victimName, killer.kind == KillCredit.Kind.Cat);
                if (killer.execution) RunStats.Executions++;
            }
            KillFeed.Report(killer, victimName, portrait);
        }
        catch (Exception e)
        {
            Debug.LogWarning("Kill feed: " + e.Message, this); // never let the UI break a kill
        }
    }

    private void SpawnDamageText(float damage)
    {
        if (damage <= 0f || !GameSettings.DamageNumbers) return;

        if (damageTextPrefab != null)
        {
            Vector3 spawnPosition = transform.position;
            GameObject textInstance = Instantiate(damageTextPrefab, spawnPosition, Quaternion.identity);

            FloatingDamageText floatingText = textInstance.GetComponent<FloatingDamageText>();
            if (floatingText != null)
            {
                floatingText.Setup(damage);
            }
        }
    }

    // Floating word over the enemy ("EXECUTED!" from the samurai cat, etc.)
    public void ShowCustomText(string text, Color color) => SpawnCustomText(text, color, 1.6f);

    private void SpawnCustomText(string text, Color? customColor = null, float scaleMultiplier = 1.5f)
    {
        if (damageTextPrefab != null)
        {
            Vector3 spawnPosition = transform.position + Vector3.up * 0.4f;
            GameObject textInstance = Instantiate(damageTextPrefab, spawnPosition, Quaternion.identity);

            FloatingDamageText floatingText = textInstance.GetComponent<FloatingDamageText>();
            if (floatingText != null)
            {
                floatingText.SetupCustomText(text, customColor, scaleMultiplier);
            }
        }
    }

    // Rumble runs on GamepadRumble: the old per-enemy coroutine was killed by StopAllCoroutines on the next hit,
    // so the "off" never came and the counter rumble kept going.
    public void TriggerRumble(float lowFreq, float highFreq, float duration)
    {
        GamepadRumble.Pulse(lowFreq, highFreq, duration);
    }

    private IEnumerator ResetIsBeingHitAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        isBeingHit = false;
        SetAnimatorBool("IsBeingHit", false);
    }

    private IEnumerator ChangeTagAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        gameObject.tag = "Untagged";
        NoPetFollow = true;
    }

    private void SetAnimatorBool(string paramName, bool value)
    {
        if (anima != null && AnimatorHasParameter(paramName))
        {
            anima.SetBool(paramName, value);
        }
    }

    private void SetAnimatorTrigger(string paramName)
    {
        if (anima != null && AnimatorHasParameter(paramName))
        {
            anima.SetTrigger(paramName);
        }
    }

    private bool AnimatorHasParameter(string paramName)
    {
        if (anima == null) return false;

        if (cachedController != anima.runtimeAnimatorController)
        {
            cachedController = anima.runtimeAnimatorController;
            animatorParameters.Clear();
            if (cachedController != null)
            {
                foreach (AnimatorControllerParameter param in anima.parameters) animatorParameters.Add(param.name);
            }
        }
        return animatorParameters.Contains(paramName);
    }

    private void SwitchRandomHitAnimation()
    {
        if (hitOverrideControllers.Length == 0) return;
    }
}