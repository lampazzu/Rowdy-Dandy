using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class DamageEntry
{
    public PlayerDamage playerDamage; // The PlayerDamage instance
    public float customDamageBonus; // Custom damage value for this instance
}

public class DamageModifier : MonoBehaviour
{
    [SerializeField] private List<DamageEntry> damageEntries = new List<DamageEntry>(); // List of custom modifiers
    [SerializeField] private bool isModifying = false; // Controls if it's modifying or not

    private void OnEnable()
    {
        if (isModifying)
        {
            foreach (DamageEntry entry in damageEntries)
            {
                if (entry.playerDamage != null)
                    entry.playerDamage.AddFlatDamage(entry.customDamageBonus);
            }
        }
    }

    private void OnDisable()
    {
        foreach (DamageEntry entry in damageEntries)
        {
            if (entry.playerDamage != null)
                entry.playerDamage.RemoveFlatDamage(entry.customDamageBonus);
        }
    }

    public void SetModifying(bool state)
    {
        if (state == isModifying) return; // Prevent duplicate adds/removals

        isModifying = state;

        foreach (DamageEntry entry in damageEntries)
        {
            if (entry.playerDamage != null)
            {
                if (isModifying)
                    entry.playerDamage.AddFlatDamage(entry.customDamageBonus);
                else
                    entry.playerDamage.RemoveFlatDamage(entry.customDamageBonus);
            }
        }
    }
}
