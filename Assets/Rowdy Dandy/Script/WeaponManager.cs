using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;

public class WeaponManager : MonoBehaviour
{
    private const string SavedWeaponKey = "SelectedWeapon";

    [Header("Weapon Break")]
    [Tooltip("Fires when a weapon's durability runs out, right before switching back to the Rod.")]
    [SerializeField] private UnityEngine.Events.UnityEvent onWeaponBroken;

    [Header("Weapon Options")]
    [SerializeField] public bool isWK_Axe = true;        // SLOT 0: Default Rod (Indestructible)
    [SerializeField] public bool isWK_Sword = false;    // SLOT 1: Sword (Breakable)
    [SerializeField] public bool isWK_Naginata = false; // SLOT 2: Naginata (Breakable)
    [SerializeField] public bool isWK_Cleaver = false;  // SLOT 3: Cleaver (Breakable)

    [Header("UI References")]
    [SerializeField] private Text weaponTextUI;
    [SerializeField] private TMP_Text weaponTMPTextUI;
    [SerializeField] private Image weaponProfileImage;

    [Header("Filled Image Durability Bar")]
    [SerializeField] private Image durabilityBarImage;

    [Header("UI Weapon Names")]
    [SerializeField] private string rodName = "Rod";
    [SerializeField] private string swordName = "Sword";
    [SerializeField] private string naginataName = "Naginata";
    [SerializeField] private string cleaverName = "Cleaver";

    [Header("Weapon Profile Thumbnails")]
    [SerializeField] private Sprite rodProfile;
    [SerializeField] private Sprite swordProfile;
    [SerializeField] private Sprite naginataProfile;
    [SerializeField] private Sprite cleaverProfile;

    [Header("UI Juice / Polish Settings")]
    [SerializeField] private Color weaponSwitchColor = new Color(0.65f, 0.2f, 1f, 1f);
    [SerializeField] private Color weaponGlowColor = new Color(0.9f, 0.5f, 1f, 1f);
    [SerializeField] private float weaponSwitchEffectDuration = 0.3f;
    [SerializeField] private float weaponSwitchPopScale = 1.25f;

    [Header("Animator Override Controllers")]
    [SerializeField] private AnimatorOverrideController wkAxeAnimator;
    [SerializeField] private AnimatorOverrideController wkSwordAnimator;
    [SerializeField] private AnimatorOverrideController wkNaginataAnimator;
    [SerializeField] private AnimatorOverrideController wkCleaverAnimator;

    [Header("Animator")]
    [SerializeField] private Animator characterAnimator;

    [Header("Weapon Scripts")]
    [SerializeField] private MonoBehaviour axeScript;
    [SerializeField] private MonoBehaviour swordScript;
    [SerializeField] private MonoBehaviour naginataScript;
    [SerializeField] private MonoBehaviour cleaverScript;

    [Header("Weapon Switch Sound")]
    [SerializeField] private AudioSource weaponSwitchAudioSource;
    [SerializeField] private AudioClip weaponSwitchSound;
    [SerializeField, Range(0f, 1f)] private float weaponSwitchVolume = 1f;

    // Index: 0 = Rod (Infinite), 1 = Sword (Durability), 2 = Naginata (Durability), 3 = Cleaver (Durability)
    private bool[] unlockedWeapons = new bool[] { true, false, false, false };
    private float[] currentDurability = new float[4];
    private float[] maxDurability = new float[4];

    private Coroutine weaponUIEffectCoroutine;
    private Vector3 textOriginalScale = Vector3.one;
    private Vector3 imageOriginalScale = Vector3.one;
    private Color textOriginalColor = Color.white;
    private Color imageOriginalColor = Color.white;

    void Start()
    {
        if (weaponTMPTextUI != null)
        {
            textOriginalScale = weaponTMPTextUI.rectTransform.localScale;
            textOriginalColor = weaponTMPTextUI.color;
        }
        else if (weaponTextUI != null)
        {
            textOriginalScale = weaponTextUI.rectTransform.localScale;
            textOriginalColor = weaponTextUI.color;
        }

        if (weaponProfileImage != null)
        {
            imageOriginalScale = weaponProfileImage.rectTransform.localScale;
            imageOriginalColor = weaponProfileImage.color;
        }

        LoadWeaponData();
        UpdateWeapon();
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Alpha7) && unlockedWeapons[0]) SetWeaponToAxe();
        if (Input.GetKeyDown(KeyCode.Alpha8) && unlockedWeapons[1]) SetWeaponToSword();
        if (Input.GetKeyDown(KeyCode.Alpha9) && unlockedWeapons[2]) SetWeaponToNaginata();
        if (Input.GetKeyDown(KeyCode.Alpha0) && unlockedWeapons[3]) SetWeaponToCleaver();

        if (Input.GetKeyDown(KeyCode.LeftControl) || Input.GetKeyDown(KeyCode.JoystickButton4))
        {
            CycleWeapon();
        }

        if (isWK_Axe)
        {
            characterAnimator.SetFloat("JumpAttackSpeed", 1.0f);
            characterAnimator.SetFloat("DuckAttackSpeed", 1.0f);
            characterAnimator.SetFloat("NeutralAttackSpeed", 1.0f);
        }
        if (isWK_Sword)
        {
            characterAnimator.SetFloat("JumpAttackSpeed", 1.5f);
            characterAnimator.SetFloat("DuckAttackSpeed", 1.5f);
            characterAnimator.SetFloat("NeutralAttackSpeed", 1.8f);
        }
        if (isWK_Naginata)
        {
            characterAnimator.SetFloat("JumpAttackSpeed", 3f);
            characterAnimator.SetFloat("DuckAttackSpeed", 1.0f);
            characterAnimator.SetFloat("NeutralAttackSpeed", 1.0f);
        }
        if (isWK_Cleaver)
        {
            characterAnimator.SetFloat("JumpAttackSpeed", 1.5f);
            characterAnimator.SetFloat("DuckAttackSpeed", 1.8f);
            characterAnimator.SetFloat("NeutralAttackSpeed", 1.0f);
        }
    }

    public void UpdateWeapon()
    {
        if (isWK_Axe)
        {
            characterAnimator.runtimeAnimatorController = wkAxeAnimator;
            axeScript.enabled = true;
            UpdateUI(rodName, rodProfile);
        }
        if (isWK_Sword)
        {
            characterAnimator.runtimeAnimatorController = wkSwordAnimator;
            swordScript.enabled = true;
            UpdateUI(swordName, swordProfile);
        }
        if (isWK_Naginata)
        {
            characterAnimator.runtimeAnimatorController = wkNaginataAnimator;
            naginataScript.enabled = true;
            UpdateUI(naginataName, naginataProfile);
        }
        if (isWK_Cleaver)
        {
            characterAnimator.runtimeAnimatorController = wkCleaverAnimator;
            cleaverScript.enabled = true;
            UpdateUI(cleaverName, cleaverProfile);
        }

        if (!isWK_Axe) axeScript.enabled = false;
        if (!isWK_Sword) swordScript.enabled = false;
        if (!isWK_Naginata) naginataScript.enabled = false;
        if (!isWK_Cleaver) cleaverScript.enabled = false;

        UpdateDurabilityUI();
    }

    public void DepleteActiveWeaponDurability(float amount = 1f)
    {
        int activeIndex = GetActiveWeaponIndex();

        // Slot 0 (Rod/Axe) is infinite and never breaks
        if (activeIndex == 0) return;

        currentDurability[activeIndex] -= amount;
        SaveWeaponData();
        UpdateDurabilityUI();

        if (currentDurability[activeIndex] <= 0f)
        {
            currentDurability[activeIndex] = 0f;
            unlockedWeapons[activeIndex] = false; // Relock broken weapon
            SaveWeaponData();
            PlayBreakMoment(activeIndex); // before the switch, while the HUD still shows the broken weapon
            onWeaponBroken?.Invoke();
            SetWeaponToAxe(); // Auto-switch back to starting Rod
        }
    }

    // Shards, slow-mo, "BROKE!" and the HUD icon splitting apart (see WeaponBreakFX)
    private void PlayBreakMoment(int brokenIndex)
    {
        WeaponBreakFX fx = WeaponBreakFX.Instance;
        if (fx == null) return;

        string[] names = { rodName, swordName, naginataName, cleaverName };
        Collider2D body = GetComponent<Collider2D>();
        Vector3 position = body != null ? body.bounds.center : transform.position;
        float facing = transform.localScale.x >= 0f ? 1f : -1f;
        Sprite brokenIcon = weaponProfileImage != null ? weaponProfileImage.sprite : null;

        fx.Play(position, facing, names[Mathf.Clamp(brokenIndex, 0, names.Length - 1)], weaponProfileImage, brokenIcon);
    }

    public void PickupWeapon(WeaponType type, float durabilityMax)
    {
        int index = GetIndexFromType(type);
        unlockedWeapons[index] = true;
        maxDurability[index] = durabilityMax;
        currentDurability[index] = durabilityMax;

        SaveWeaponData();

        switch (type)
        {
            case WeaponType.Sword: SetWeaponToSword(); break;
            case WeaponType.Axe: SetWeaponToAxe(); break;
            case WeaponType.Naginata: SetWeaponToNaginata(); break;
            case WeaponType.Cleaver: SetWeaponToCleaver(); break;
        }
    }

    public int GetActiveWeaponIndex()
    {
        if (isWK_Sword) return 1;
        if (isWK_Naginata) return 2;
        if (isWK_Cleaver) return 3;
        return 0; // Default Rod / Axe
    }

    private int GetIndexFromType(WeaponType type)
    {
        switch (type)
        {
            case WeaponType.Axe: return 0;
            case WeaponType.Sword: return 1;
            case WeaponType.Naginata: return 2;
            case WeaponType.Cleaver: return 3;
            default: return 0;
        }
    }

    private void UpdateDurabilityUI()
    {
        if (durabilityBarImage == null) return;

        int activeIndex = GetActiveWeaponIndex();

        if (activeIndex == 0)
        {
            // Hide bar for default infinite Rod
            durabilityBarImage.gameObject.SetActive(false);
        }
        else
        {
            durabilityBarImage.gameObject.SetActive(true);

            float fillRatio = maxDurability[activeIndex] > 0 ? currentDurability[activeIndex] / maxDurability[activeIndex] : 0f;
            durabilityBarImage.fillAmount = Mathf.Clamp01(fillRatio);
        }
    }

    private void UpdateUI(string currentWeaponName, Sprite currentWeaponProfile)
    {
        if (weaponTextUI != null) weaponTextUI.text = currentWeaponName;
        if (weaponTMPTextUI != null) weaponTMPTextUI.text = currentWeaponName;

        if (weaponProfileImage != null && currentWeaponProfile != null)
        {
            weaponProfileImage.sprite = currentWeaponProfile;
        }

        if (weaponUIEffectCoroutine != null) StopCoroutine(weaponUIEffectCoroutine);
        weaponUIEffectCoroutine = StartCoroutine(JuicyUIEffect());
    }

    private IEnumerator JuicyUIEffect()
    {
        RectTransform textRect = weaponTMPTextUI != null ? weaponTMPTextUI.rectTransform : (weaponTextUI != null ? weaponTextUI.rectTransform : null);
        RectTransform imageRect = weaponProfileImage != null ? weaponProfileImage.rectTransform : null;

        float elapsedTime = 0f;

        while (elapsedTime < weaponSwitchEffectDuration)
        {
            elapsedTime += Time.deltaTime;
            float progress = Mathf.Clamp01(elapsedTime / weaponSwitchEffectDuration);

            float scaleMultiplier = 1f + Mathf.Sin(progress * Mathf.PI) * (weaponSwitchPopScale - 1f);
            Color currentTextColor = Color.Lerp(textOriginalColor, weaponGlowColor, Mathf.Sin(progress * Mathf.PI));

            if (textRect != null) textRect.localScale = textOriginalScale * scaleMultiplier;
            if (imageRect != null) imageRect.localScale = imageOriginalScale * scaleMultiplier;

            if (weaponTMPTextUI != null) weaponTMPTextUI.color = currentTextColor;
            if (weaponTextUI != null) weaponTextUI.color = currentTextColor;

            if (weaponProfileImage != null)
            {
                weaponProfileImage.color = Color.Lerp(imageOriginalColor, weaponSwitchColor, Mathf.Sin(progress * Mathf.PI));
            }

            yield return null;
        }

        if (textRect != null) textRect.localScale = textOriginalScale;
        if (imageRect != null) imageRect.localScale = imageOriginalScale;

        if (weaponTMPTextUI != null) weaponTMPTextUI.color = textOriginalColor;
        if (weaponTextUI != null) weaponTextUI.color = textOriginalColor;
        if (weaponProfileImage != null) weaponProfileImage.color = imageOriginalColor;

        weaponUIEffectCoroutine = null;
    }

    public void CycleWeapon()
    {
        int currentIndex = GetActiveWeaponIndex();
        int nextIndex = currentIndex;

        for (int i = 1; i <= 4; i++)
        {
            int checkIndex = (currentIndex + i) % 4;
            if (unlockedWeapons[checkIndex])
            {
                nextIndex = checkIndex;
                break;
            }
        }

        switch (nextIndex)
        {
            case 0: SetWeaponToAxe(); break;
            case 1: SetWeaponToSword(); break;
            case 2: SetWeaponToNaginata(); break;
            case 3: SetWeaponToCleaver(); break;
        }
    }

    public void SetWeaponToAxe()
    {
        if (!unlockedWeapons[0]) return;
        isWK_Axe = true;
        isWK_Sword = false;
        isWK_Naginata = false;
        isWK_Cleaver = false;
        SaveAndApply(0);
    }

    public void SetWeaponToSword()
    {
        if (!unlockedWeapons[1]) return;
        isWK_Sword = true;
        isWK_Axe = false;
        isWK_Naginata = false;
        isWK_Cleaver = false;
        SaveAndApply(1);
    }

    public void SetWeaponToNaginata()
    {
        if (!unlockedWeapons[2]) return;
        isWK_Naginata = true;
        isWK_Axe = false;
        isWK_Sword = false;
        isWK_Cleaver = false;
        SaveAndApply(2);
    }

    public void SetWeaponToCleaver()
    {
        if (!unlockedWeapons[3]) return;
        isWK_Cleaver = true;
        isWK_Axe = false;
        isWK_Sword = false;
        isWK_Naginata = false;
        SaveAndApply(3);
    }

    private void SaveAndApply(int index)
    {
        SaveWeaponData();
        UpdateWeapon();
        PlaySwitchSound();
    }

    private void SaveWeaponData()
    {
        PlayerPrefs.SetInt(SavedWeaponKey, GetActiveWeaponIndex());

        // Save unlock state and remaining durability for breakable weapons
        for (int i = 1; i < 4; i++)
        {
            PlayerPrefs.SetInt("WeaponUnlocked_" + i, unlockedWeapons[i] ? 1 : 0);
            PlayerPrefs.SetFloat("WeaponCurDurability_" + i, currentDurability[i]);
            PlayerPrefs.SetFloat("WeaponMaxDurability_" + i, maxDurability[i]);
        }

        PlayerPrefs.Save();
    }

    private void LoadWeaponData()
    {
        unlockedWeapons[0] = true; // Rod is always unlocked

        for (int i = 1; i < 4; i++)
        {
            unlockedWeapons[i] = PlayerPrefs.GetInt("WeaponUnlocked_" + i, 0) == 1;
            currentDurability[i] = PlayerPrefs.GetFloat("WeaponCurDurability_" + i, 50f);
            maxDurability[i] = PlayerPrefs.GetFloat("WeaponMaxDurability_" + i, 50f);
        }

        if (PlayerPrefs.HasKey(SavedWeaponKey))
        {
            int savedIndex = PlayerPrefs.GetInt(SavedWeaponKey);
            if (unlockedWeapons[savedIndex])
            {
                isWK_Axe = savedIndex == 0;
                isWK_Sword = savedIndex == 1;
                isWK_Naginata = savedIndex == 2;
                isWK_Cleaver = savedIndex == 3;
            }
        }
    }

    private void PlaySwitchSound()
    {
        if (weaponSwitchAudioSource != null && weaponSwitchSound != null)
        {
            weaponSwitchAudioSource.PlayOneShot(weaponSwitchSound, weaponSwitchVolume);
        }
    }
}