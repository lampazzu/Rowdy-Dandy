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
    [Tooltip("The old man (object named ...OldMan...): hurting him wakes the Moonbound Elder (CursedElder) instead of spawning Transform Wolf above. Other objects using Is Old Man keep spawning their Transform Wolf object.")]
    [SerializeField] private bool oldManBecomesElder = true;
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

    // When this enemy woke up (Spike uses it to tell contact boxes from attack hitboxes)
    public bool HasAwoken { get; private set; }
    public int AwakeFrame { get; private set; }

    // The old man is parked for now: switched off on load. Set to true to bring him (and the Moonbound Elder) back.
    public static bool OldManEnabled = false;

    private void Start()
    {
        WereKnightWalk.TryAttach(this); // the knights walk with the shadow-wolf run cycle
        EnemyTeleport.TryAttach(this);  // archers / bombers blink to a better spot
        if (!OldManEnabled && isOldMan && name.IndexOf("OldMan", System.StringComparison.OrdinalIgnoreCase) >= 0)
            gameObject.SetActive(false);
    }

    // Every enemy that has woken up and isn't destroyed (instead of FindObjectsByType, which was run on every death).
    // Inactive ones stay in it: check activeInHierarchy.
    public static readonly List<EnemyHealth> All = new List<EnemyHealth>();
    private void OnDestroy() => All.Remove(this);

    private void Awake()
    {
        All.Add(this);
        HasAwoken = true;
        AwakeFrame = Time.frameCount;
        // enemies built from code (Stephmoss) have no serialized events
        if (onHurtWolf == null) onHurtWolf = new UnityEvent();
        if (onEnemyKill == null) onEnemyKill = new UnityEvent();
        if (onCriticalDamage == null) onCriticalDamage = new UnityEvent();
        anima = GetComponent<Animator>();
        redco = GetComponent<SpriteRenderer>();
        startingenemyHealth = Balance.EnemyHealth(this, startingenemyHealth); // Jarvis balance (Lamp: unchanged)
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
        UpdateDeathRecovery();
    }

    // Killed while stunned / mid-hit, some enemies stayed frozen on their GetHit pose instead of dying (the death
    // trigger got eaten by the hit restart, or a stun had the animator paused). For a moment after death: if it's
    // still sitting in GetHit (or paused), the animator is unpaused and the death trigger fires again.
    private float diedAt = -1f;
    private int deathRetries;

    private void UpdateDeathRecovery()
    {
        if (!enemydead) { diedAt = -1f; deathRetries = 0; return; }
        if (diedAt < 0f) diedAt = Time.time;
        if (anima == null || isObject || deathRetries >= 4 || !anima.isActiveAndEnabled || anima.runtimeAnimatorController == null) return;
        if (Time.time - diedAt < 0.3f + 0.35f * deathRetries) return;
        if (anima.speed <= 0.01f) anima.speed = 1f;
        AnimatorStateInfo state = anima.GetCurrentAnimatorStateInfo(0);
        if (state.shortNameHash != GetHitState || anima.IsInTransition(0)) { deathRetries = 4; return; } // it got there
        deathRetries++;
        if (AnimatorHasParameter("IsBeingHit")) anima.SetBool("IsBeingHit", false);
        SetAnimatorTrigger("destroyed");
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

    // ShamanAltars: the pillar flags are static (they used to survive a reload half-set); set from the save at load
    public static void SetAltarState(bool a, bool b, bool cameraDone)
    {
        isPillarA = a;
        isPillarB = b;
        hasCameraBeenReset = cameraDone;
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

    // Runtime setup for enemies configured from code (e.g. PelichBoss): new max HP, starting full
    public void SetMaxHealth(float health)
    {
        startingenemyHealth = health;
        currentenemyHealth = health;
    }

    // EXP gems to drop on death, if none were set in the Inspector
    public void SetExpDropIfMissing(GameObject gemPrefab, int amount)
    {
        if (expGemPrefab != null || giveDirectEXP || gemPrefab == null) return;
        expGemPrefab = gemPrefab;
        expGemAmount = amount;
    }

    // HYPER ARMOR (some elites, the Moonbound Elder, the Red Jelly): hits don't make it flinch - unless it's stunned,
    // charmed or rooted (StatusEffects), or the hit is a counter. See TakeDamageEnemy.
    public bool HyperArmor { get; set; }
    private float armorFxAt = -10f;
    public bool ArmorHolds => HyperArmor && !isParryTime && !isObject && !StatusEffects.IsStunned(gameObject);

    // Night elite (WaveEnemySpawner): tougher, drops more EXP and more often a weapon
    public bool IsElite { get; private set; }
    public void MakeElite(float healthMultiplier, float gemMultiplier, float dropBonusPercent)
    {
        if (IsElite || isObject) return;
        IsElite = true;
        startingenemyHealth *= healthMultiplier;
        currentenemyHealth = startingenemyHealth;
        expGemAmount = Mathf.CeilToInt(Mathf.Max(1, expGemAmount) * gemMultiplier);
        directEXPValue *= gemMultiplier;
        dropChancePercent = Mathf.Min(100f, dropChancePercent + dropBonusPercent);
    }

    public void AddHealthEnemy(float _value)
    {
        currentenemyHealth = Mathf.Clamp(currentenemyHealth + _value, 0, startingenemyHealth);
    }

    // quiet: damage over time (poison ticks) - no flinch animation, no hurt sounds, no blood burst
    public void TakeDamageEnemy(float _damage, bool isCritical = false, bool quiet = false)
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
            StyleRank.OnHit(this, _damage, isCritical, isParryTime, credit);
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

        if (currentenemyHealth > 0 && quiet)
        {
            // poison tick: just the number
        }
        else if (currentenemyHealth > 0 && ArmorHolds)
        {
            // HYPER ARMOR: takes the damage without flinching (no GetHit clip = no stagger, no knockback). The white
            // flash lives in that clip, so it flashes from code instead. Counters, stuns and charms still break through.
            if (!isObject && _damage > 0f) Blood.Spill(EnemyFairness.BodyCenter(this), AwayFromRowdy(), isCritical ? 6 : 3);
            HitFlash.Flash(this);
            if (Time.time - armorFxAt > 0.25f)
            {
                armorFxAt = Time.time;
                Vector3 c = EnemyFairness.BodyCenter(this);
                SheetFX clank = GraftFX.Play("MarhgHit", c + (Vector3)UnityEngine.Random.insideUnitCircle * 0.15f, new Color(1f, 0.85f, 0.55f), 96);
                if (clank != null && UnityEngine.Random.value < 0.5f) clank.transform.localScale = new Vector3(-1f, 1f, 1f);
                BoonArt.Play(BoonArt.Get != null ? BoonArt.Get.clang : null, 0.22f, UnityEngine.Random.Range(1.5f, 1.8f));
            }
            onHurtWolf.Invoke();
            SoundEffect.PlayUnboundSounds(onHurtWolf);
        }
        else if (currentenemyHealth > 0)
        {
            if (!isObject && _damage > 0f) Blood.Spill(EnemyFairness.BodyCenter(this), AwayFromRowdy(), isCritical ? 9 : 5);
            isBeingHit = true;
            onHurtWolf.Invoke();
            SoundEffect.PlayUnboundSounds(onHurtWolf);
            if (anima != null) anima.Play("GetHit", 0, 0f);
            else HitFlash.Flash(this); // code-animated enemies (Red Jelly) still flash

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

            if (bloodPrefab != null && GameSettings.BloodOn)
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
                if (TryGetComponent(out StatusEffects status)) status.EndStun(); // a stunned enemy still plays its death
                onEnemyKill.Invoke();
                SoundEffect.PlayUnboundSounds(onEnemyKill);
                SetAnimatorTrigger("destroyed");
                enemydead = true;
                if (TryGetComponent(out EnemyCorpse corpse)) corpse.OnKilled();
                else if (!isObject && TryGetComponent(out Rigidbody2D body) && body.bodyType == RigidbodyType2D.Dynamic && body.gravityScale >= 0.5f
                         && GetComponent<EnemyMovement>() != null && GetComponentInChildren<Effector2D>(true) == null)
                    gameObject.AddComponent<EnemyCorpse>().OnKilled(); // walker without one (spawned oddly): still drops to the floor
                KillCredit.Finish finish = isParryTime ? KillCredit.Finish.Counter : isCritical ? KillCredit.Finish.Critical : KillCredit.Finish.Normal;
                ReportKill(credit, finish);
                if (isObject) PlantJuice.OnCut(this, credit); // Blue / Orange / Purple plants: crunchy cut feedback
                StyleRank.OnKill(this, credit, finish);
                if (byRowdySide) RareDrops.OnEnemyKilled(this); // flying rat / cat treat fish, very rarely
                if (!isObject) Blood.Spill(EnemyFairness.BodyCenter(this), AwayFromRowdy(), 14);
                RowdyBuffs.OnEnemyKilled(this, byRowdySide); // Lallo's decay explosion, if armed
                BoonRunner.OnEnemyKilled(this, credit, byRowdySide); // boons: Bloodthirst, Earth Erupt, Plague, Sandwich Time...

                // Countering a Waterviva Rider to death sets off its jelly: a long, accelerating string of explosions
                if (finish == KillCredit.Finish.Counter && byRowdySide)
                {
                    EnemyCatalog.Entry kind = EnemyCatalog.Identify(this);
                    if (kind != null && kind.id == "watervivarider")
                    {
                        TimeSlowController.HitStop(0.12f, 0.05f);
                        ExplosionChain.Play(transform.position, 30, 1.7f, 1.6f, 0.42f);
                    }
                }

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
                if (weaponDropPrefab != null && UnityEngine.Random.Range(0f, 100f) <= dropChancePercent * DropLuck.Multiplier) // ore gems raise the luck
                {
                    Instantiate(weaponDropPrefab, transform.position, Quaternion.identity);
                }
                if (!isObject) WeaponBoons.ExtraDrop(transform.position); // boon weapons drop more often

                if (bloodKill != null && !isObject && GameSettings.BloodOn)
                {
                    Instantiate(bloodKill, transform.position, Quaternion.identity);
                }

                if (isOldMan)
                {
                    // isOldMan + transformWolf is also used on other things (WereKnight, SharkWolf, arrows, bombs...) as
                    // "spawn this on death and vanish" - only the real old man wakes the Elder
                    EnemyCatalog.Entry who = EnemyCatalog.Identify(this);
                    bool realOldMan = who != null && who.id == "oldman";
                    if (oldManBecomesElder && realOldMan && CursedElder.Spawn(transform.position) != null) Destroy(gameObject);
                    else if (transformWolf != null)
                    {
                        Instantiate(transformWolf, transform.position, Quaternion.identity);
                        Destroy(gameObject);
                    }
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
    private void ReportKill(KillCredit credit, KillCredit.Finish finish)
    {
        if (isObject)
        {
            if (credit != null && credit.kind != KillCredit.Kind.World)
            {
                RunStats.ObjectsSmashed++;
                if (name.IndexOf("Statue", StringComparison.OrdinalIgnoreCase) >= 0) RunStats.StatuesSmashed++;
                RowdyNotes.RecordKill(this); // statues have a page too (plants don't match any entry)
            }
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
                RowdyNotes.RecordKill(this);
            }
            KillFeed.Report(killer, victimName, portrait, finish);
        }
        catch (Exception e)
        {
            Debug.LogWarning("Kill feed: " + e.Message, this); // never let the UI break a kill
        }
    }

    private static Transform rowdyCache;
    // Blood flies away from Rowdy
    private float AwayFromRowdy()
    {
        if (rowdyCache == null)
        {
            GameObject p = GameObject.FindGameObjectWithTag("Player");
            if (p != null) rowdyCache = p.transform;
        }
        if (rowdyCache == null) return 0f;
        return transform.position.x >= rowdyCache.position.x ? 1f : -1f;
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