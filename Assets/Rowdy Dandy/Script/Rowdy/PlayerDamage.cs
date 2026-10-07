using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerDamage : MonoBehaviour
{
    [SerializeField] private float baseDamage = 1f;
    private List<float> damageModifiers = new List<float>();
    private List<float> damageMultipliers = new List<float>();

    [Header("Critical Hit Settings")]
    [SerializeField] private float critChance = 0f;
    [SerializeField] private float critMultiplier = 1.5f;
    private List<float> critChanceModifiers = new List<float>();
    private List<float> critMultiplierModifiers = new List<float>();

    [Header("Debugging")]
    [SerializeField] private float totalDamageDebug;
    [SerializeField] private bool lastHitWasCritDebug;

    public Animator animator;
    public Animator silveranim;
    public static bool hitEnemy = false;

    [SerializeField] private bool isPlayer = false;
    [SerializeField] private bool isSilverWax = false;

    private HashSet<Collider2D> alreadyDamagedEnemies = new HashSet<Collider2D>();

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision.CompareTag("Enemy") && !alreadyDamagedEnemies.Contains(collision))
        {
            EnemyHealth enemy = collision.GetComponent<EnemyHealth>();

            if (enemy != null)
            {
                float finalDamage = CalculateTotalDamage(out bool isCrit);
                enemy.TakeDamageEnemy(finalDamage, isCrit);
                // Weapon durability is spent per swing in PlayerMovement, not per hit (cats share this script too)
            }

            if (isPlayer)
            {
                hitEnemy = true;
                Invoke(nameof(ResetHitEnemy), 0.1f);
            }

            alreadyDamagedEnemies.Add(collision);
            if (animator != null) animator.SetTrigger("Trigger");

            if (isSilverWax && silveranim != null)
            {
                silveranim.ResetTrigger("SilverWax");
                silveranim.SetTrigger("SilverWax");
                silveranim.Play("SilverWax", 0, 0f);
            }
        }
    }

    private void OnEnable() => alreadyDamagedEnemies.Clear();

    private void OnDisable()
    {
        if (animator != null) animator.ResetTrigger("Trigger");
    }

    private void ResetHitEnemy() => hitEnemy = false;

    public float CalculateTotalDamage(out bool isCrit)
    {
        float totalDamage = baseDamage;

        foreach (float mod in damageModifiers)
            totalDamage += mod;

        foreach (float mult in damageMultipliers)
            totalDamage *= mult;

        float totalCritChance = critChance;
        foreach (float mod in critChanceModifiers)
            totalCritChance += mod;

        isCrit = false;
        if (totalCritChance > 0f)
        {
            float roll = Random.Range(0f, 100f);
            if (roll <= totalCritChance)
            {
                isCrit = true;

                float totalCritMultiplier = critMultiplier;
                foreach (float mod in critMultiplierModifiers)
                    totalCritMultiplier += mod;

                totalDamage *= totalCritMultiplier;
            }
        }

        totalDamage = Mathf.Max(0, totalDamage);

        totalDamageDebug = totalDamage;
        lastHitWasCritDebug = isCrit;

        return totalDamage;
    }

    public float CalculateTotalDamage()
    {
        return CalculateTotalDamage(out _);
    }

    public void AddFlatDamage(float amount)
    {
        damageModifiers.Add(amount);
        CalculateTotalDamage();
    }

    public void RemoveFlatDamage(float amount)
    {
        damageModifiers.Remove(amount);
        CalculateTotalDamage();
    }

    public void AddMultiplier(float amount)
    {
        damageMultipliers.Add(amount);
        CalculateTotalDamage();
    }

    public void RemoveMultiplier(float amount)
    {
        damageMultipliers.Remove(amount);
        CalculateTotalDamage();
    }

    public void AddCritChance(float amount)
    {
        critChanceModifiers.Add(amount);
        CalculateTotalDamage();
    }

    public void RemoveCritChance(float amount)
    {
        critChanceModifiers.Remove(amount);
        CalculateTotalDamage();
    }

    public void AddCritMultiplier(float amount)
    {
        critMultiplierModifiers.Add(amount);
        CalculateTotalDamage();
    }

    public void RemoveCritMultiplier(float amount)
    {
        critMultiplierModifiers.Remove(amount);
        CalculateTotalDamage();
    }

    public void ResetModifiers()
    {
        damageModifiers.Clear();
        damageMultipliers.Clear();
        critChanceModifiers.Clear();
        critMultiplierModifiers.Clear();
        CalculateTotalDamage();
    }
}