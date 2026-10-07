using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.Events;
using Cinemachine;

public class EnemyHealth : MonoBehaviour
{
    [StructLayout(LayoutKind.Sequential)]
    private struct XINPUT_VIBRATION
    {
        public ushort wLeftMotorSpeed;
        public ushort wRightMotorSpeed;
    }

    [DllImport("xinput1_4.dll", EntryPoint = "XInputSetState")]
    private static extern int XInputSetState1_4(int dwUserIndex, ref XINPUT_VIBRATION pVibration);

    [DllImport("xinput9_1_0.dll", EntryPoint = "XInputSetState")]
    private static extern int XInputSetState9_1_0(int dwUserIndex, ref XINPUT_VIBRATION pVibration);

    private static bool useXInput14 = true;

    private static void SetXInputVibration(int userIndex, float leftMotor, float rightMotor)
    {
        XINPUT_VIBRATION vibration = new XINPUT_VIBRATION
        {
            wLeftMotorSpeed = (ushort)(Mathf.Clamp01(leftMotor) * 65535),
            wRightMotorSpeed = (ushort)(Mathf.Clamp01(rightMotor) * 65535)
        };

        try
        {
            if (useXInput14)
                XInputSetState1_4(userIndex, ref vibration);
            else
                XInputSetState9_1_0(userIndex, ref vibration);
        }
        catch (DllNotFoundException)
        {
            try
            {
                useXInput14 = false;
                XInputSetState9_1_0(userIndex, ref vibration);
            }
            catch { }
        }
        catch { }
    }

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

    public void TriggerRumble(float lowFreq, float highFreq, float duration)
    {
        if (!GameSettings.Vibration) return;
        StartCoroutine(LegacyRumbleRoutine(lowFreq, highFreq, duration));
    }

    private IEnumerator LegacyRumbleRoutine(float lowFreq, float highFreq, float duration)
    {
        SetXInputVibration(0, lowFreq, highFreq);
        yield return new WaitForSecondsRealtime(duration);
        SetXInputVibration(0, 0f, 0f);
    }

    private void OnDisable()
    {
        SetXInputVibration(0, 0f, 0f);
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

        foreach (AnimatorControllerParameter param in anima.parameters)
        {
            if (param.name == paramName)
            {
                return true;
            }
        }
        return false;
    }

    private void SwitchRandomHitAnimation()
    {
        if (hitOverrideControllers.Length == 0) return;
    }
}