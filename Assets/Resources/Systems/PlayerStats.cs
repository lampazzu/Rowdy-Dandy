using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

[System.Serializable]
public class LevelStatData
{
    public int levelNumber = 1;
    [Header("Per-Level Stat Increases")]
    public float baseDamageGain = 10f;       // e.g., +10 base damage
    public float critChanceGain = 1f;        // e.g., +1% crit chance
    public float critMultiplierGain = 0.1f;   // e.g., +0.1x (10%) crit damage
    public float expRequiredForNext = 100f;  // XP needed to reach next level
}

public class PlayerStats : MonoBehaviour
{
    public static PlayerStats Instance { get; private set; }

    // Highest level (also the most cats Rowdy can have: Magical Cat Capacity = his level, see CatRoster)
    public const int MaxLevel = 10;
    public static int Level => Instance != null ? Instance.currentLevel : Mathf.Clamp(PlayerPrefs.GetInt("PlayerLevel", 1), 1, MaxLevel);
    public bool IsMaxLevel => currentLevel >= MaxLevel;

    [Header("Placeholder scaling (levels with no Level Progression entry)")]
    [Tooltip("Gains per level for levels the list above doesn't cover (placeholders until the real numbers are tuned)")]
    [SerializeField] private float placeholderDamageGain = 6f;
    [SerializeField] private float placeholderCritChanceGain = 5f;
    [SerializeField] private float placeholderCritMultiplierGain = 0.1f;

    [Header("Target Damage Hitboxes")]
    [SerializeField] private List<PlayerDamage> targetPlayerDamages = new List<PlayerDamage>();

    [Header("Leveling Settings")]
    [SerializeField] private int currentLevel = 1;
    [SerializeField] private float currentEXP = 0f;
    [SerializeField] private float defaultBaseEXP = 100f;        // Fallback if levelProgression list is empty
    [SerializeField] private float expScalingMultiplier = 1.25f; // Fallback scaling per level
    [SerializeField] private List<LevelStatData> levelProgression = new List<LevelStatData>();

    [Header("EXP UI Bar References")]
    [SerializeField] private Image expBarFillImage;              // Drag filled Canvas Image here!
    [SerializeField] private TMP_Text expText;                   // e.g. "150 / 200 XP" (Optional)

    [Header("DevTools / Testing Hotkeys")]
    [SerializeField] private bool enableDevHotkeys = true;
    [SerializeField] private KeyCode levelUpKey = KeyCode.Comma;       // Press "," to Level Up
    [SerializeField] private KeyCode levelDownKey = KeyCode.Period;    // Press "." to Level Down
    [SerializeField] private KeyCode resetLevelKey = KeyCode.Semicolon; // Press ";" to Reset to Level 1

    [Header("UI Settings & References")]
    [SerializeField] private GameObject statsPanel;        // Drag your Stats UI Panel here
    [SerializeField] private KeyCode toggleUIKey = KeyCode.C;  // Key to open/close menu
    [SerializeField] private TMP_Text levelText;
    [SerializeField] private TMP_Text damageBonusText;
    [SerializeField] private TMP_Text critChanceText;
    [SerializeField] private TMP_Text critMultiplierText;

    // Track active applied bonuses
    private float appliedDamageBonus = 0f;
    private float appliedCritChanceBonus = 0f;
    private float appliedCritMultiplierBonus = 0f;

    private bool isUIPanelOpen = false;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void Start()
    {
        if (targetPlayerDamages.Count == 0)
        {
            targetPlayerDamages.AddRange(FindObjectsByType<PlayerDamage>(FindObjectsSortMode.None));
        }

        if (statsPanel != null)
        {
            statsPanel.SetActive(false);
        }

        LoadEXPData();
        ApplyCurrentLevelStats();
    }

    private void Update()
    {
        if (PauseMenu.IsPaused) return;

        // C, or the gamepad's L2 / LT (Rowdy Notes are on Select / Tab)
        if (!RowdyNotes.IsOpen && (GameInput.KeyDown(toggleUIKey) || GameInput.PadDown(GameInput.Act.Stats)))
        {
            ToggleStatsUI();
        }

        if (isUIPanelOpen) RunStatsPanel.Refresh();

        if (enableDevHotkeys)
        {
            if (GameInput.KeyDown(levelUpKey))
            {
                LevelUp();
            }

            if (GameInput.KeyDown(levelDownKey))
            {
                LevelDown();
            }

            if (GameInput.KeyDown(resetLevelKey))
            {
                ResetLevelAndEXP();
            }
        }
    }

    // --- EXP SYSTEM PUBLIC METHODS ---

    public void AddEXP(float amount)
    {
        currentEXP += amount;
        float requiredEXP = GetRequiredEXPForCurrentLevel();

        bool leveledUp = false;
        while (currentEXP >= requiredEXP && currentLevel < MaxLevel)
        {
            currentEXP -= requiredEXP;
            currentLevel++;
            leveledUp = true;
            RunStats.LevelUps++;
            Debug.Log($"<color=cyan>[PlayerStats]</color> Leveled UP! Current Level: {currentLevel}");
            requiredEXP = GetRequiredEXPForCurrentLevel();
        }
        if (currentLevel >= MaxLevel) currentEXP = 0f; // maxed out: the bar just stays full

        SaveEXPData();
        ApplyCurrentLevelStats();

        if (leveledUp) LevelUpFX.Play(currentLevel); // full heal + golden light + sound
    }

    public float GetRequiredEXPForCurrentLevel()
    {
        int index = currentLevel - 1;
        if (index >= 0 && index < levelProgression.Count && levelProgression[index].expRequiredForNext > 0)
        {
            return levelProgression[index].expRequiredForNext;
        }

        // Procedural formula calculation if list entry isn't set
        return Mathf.Round(defaultBaseEXP * Mathf.Pow(expScalingMultiplier, currentLevel - 1));
    }

    // --- LEVEL CONTROL METHODS ---

    public void LevelUp()
    {
        if (currentLevel >= MaxLevel) return;
        currentLevel++;
        currentEXP = 0f;
        SaveEXPData();
        Debug.Log($"<color=cyan>[PlayerStats]</color> Dev Leveled UP! Current Level: {currentLevel}");
        ApplyCurrentLevelStats();
        LevelUpFX.Play(currentLevel);
    }

    public void LevelDown()
    {
        if (currentLevel > 1)
        {
            currentLevel--;
            currentEXP = 0f;
            SaveEXPData();
            Debug.Log($"<color=orange>[PlayerStats]</color> Dev Leveled Down. Current Level: {currentLevel}");
            ApplyCurrentLevelStats();
            CatRoster.EnforceCapacity(); // fewer cat slots now
        }
    }

    public void SetLevel(int targetLevel)
    {
        currentLevel = Mathf.Clamp(targetLevel, 1, MaxLevel);
        currentEXP = 0f;
        SaveEXPData();
        Debug.Log($"<color=cyan>[PlayerStats]</color> Level set to: {currentLevel}");
        ApplyCurrentLevelStats();
    }

    public void ResetLevelAndEXP()
    {
        currentLevel = 1;
        currentEXP = 0f;
        SaveEXPData();
        ApplyCurrentLevelStats();
        CatRoster.EnforceCapacity();
    }

    // --- INTERNAL STAT & UI UPDATES ---

    private void ApplyCurrentLevelStats()
    {
        float newDamageBonus = 0f;
        float newCritChanceBonus = 0f;
        float newCritMultiplierBonus = 0f;

        for (int i = 0; i < currentLevel - 1; i++)
        {
            if (i < levelProgression.Count)
            {
                newDamageBonus += levelProgression[i].baseDamageGain;
                newCritChanceBonus += levelProgression[i].critChanceGain;
                newCritMultiplierBonus += levelProgression[i].critMultiplierGain;
            }
            else
            {
                // placeholder scaling until the list has entries for these levels
                newDamageBonus += placeholderDamageGain;
                newCritChanceBonus += placeholderCritChanceGain;
                newCritMultiplierBonus += placeholderCritMultiplierGain;
            }
        }

        foreach (PlayerDamage pd in targetPlayerDamages)
        {
            if (pd == null) continue;

            if (appliedDamageBonus != 0f) pd.RemoveFlatDamage(appliedDamageBonus);
            if (appliedCritChanceBonus != 0f) pd.RemoveCritChance(appliedCritChanceBonus);
            if (appliedCritMultiplierBonus != 0f) pd.RemoveCritMultiplier(appliedCritMultiplierBonus);

            if (newDamageBonus != 0f) pd.AddFlatDamage(newDamageBonus);
            if (newCritChanceBonus != 0f) pd.AddCritChance(newCritChanceBonus);
            if (newCritMultiplierBonus != 0f) pd.AddCritMultiplier(newCritMultiplierBonus);
        }

        appliedDamageBonus = newDamageBonus;
        appliedCritChanceBonus = newCritChanceBonus;
        appliedCritMultiplierBonus = newCritMultiplierBonus;

        UpdateUI();
    }

    public void ToggleStatsUI()
    {
        isUIPanelOpen = !isUIPanelOpen;
        if (statsPanel != null) statsPanel.SetActive(isUIPanelOpen);
        RunStatsPanel.SetVisible(isUIPanelOpen, statsPanel != null ? statsPanel.transform as RectTransform : null);

        if (isUIPanelOpen)
        {
            UpdateUI();
        }
    }

    private void OnDestroy()
    {
        if (isUIPanelOpen) RunStatsPanel.SetVisible(false, null); // the overlay outlives the scene
    }

    private void UpdateUI()
    {
        // Update Canvas EXP Fill Bar
        float maxEXP = GetRequiredEXPForCurrentLevel();
        if (expBarFillImage != null)
        {
            expBarFillImage.fillAmount = IsMaxLevel ? 1f : Mathf.Clamp01(currentEXP / maxEXP);
        }

        if (expText != null)
        {
            expText.text = IsMaxLevel ? "MAX" : $"{currentEXP:F0} / {maxEXP:F0} XP";
        }

        // Update Stats UI Panel text: everything here comes from leveling up, so say so
        if (levelText != null)
            levelText.text = IsMaxLevel ? $"Level {currentLevel} (MAX) Bonuses" : $"Level {currentLevel} Bonuses";

        if (damageBonusText != null)
            damageBonusText.text = $"Level Up Damage: +{appliedDamageBonus:F0}";

        if (critChanceText != null)
            critChanceText.text = $"Level Up Crit Chance: +{appliedCritChanceBonus:F1}%";

        if (critMultiplierText != null)
            critMultiplierText.text = $"Level Up Crit Damage: +{appliedCritMultiplierBonus:F2}x";
    }

    // --- SAVE / LOAD DATA ---

    private void SaveEXPData()
    {
        PlayerPrefs.SetInt("PlayerLevel", currentLevel);
        PlayerPrefs.SetFloat("PlayerEXP", currentEXP);
        PlayerPrefs.Save();
    }

    private void LoadEXPData()
    {
        currentLevel = Mathf.Clamp(PlayerPrefs.GetInt("PlayerLevel", 1), 1, MaxLevel);
        currentEXP = PlayerPrefs.GetFloat("PlayerEXP", 0f);
    }

    public bool IsStatsOpen => isUIPanelOpen;
    public int GetCurrentLevel() => currentLevel;
    public float GetCurrentEXP() => currentEXP;
    public float GetAppliedDamage() => appliedDamageBonus;
    public float GetAppliedCritChance() => appliedCritChanceBonus;
    public float GetAppliedCritMultiplier() => appliedCritMultiplierBonus;
}