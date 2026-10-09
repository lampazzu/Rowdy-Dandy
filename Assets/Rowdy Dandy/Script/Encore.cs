using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Endgame: beating Pelich unlocks the ENCORE (and points Rowdy at The Frontier, the island past Pelich).
//   - Encore levels: past level 10, EXP keeps filling the bar. Every Encore level = a boon pick, +3% damage, full heal.
//   - Blood Moons: every night after that is a Blood Moon - tougher tiers spawn (+2), more elites, +30% enemy health,
//     x1.5 EXP, and the screen takes a red haze. Days stay as they are, so there's always a breather.
// Saved: RD_Encore (unlocked), RD_EncoreLevel, RD_EncoreEXP. Dev Tools can unlock it; Reset Game / Reset Level clear it.
public class Encore : MonoBehaviour
{
    private const string UnlockedKey = "RD_Encore", LevelKey = "RD_EncoreLevel", ExpKey = "RD_EncoreEXP";

    public const float ExpPerLevel = 600f;
    public const float DamagePerLevel = 0.03f;
    public const int MaxLevel = 30;

    public const float BloodMoonHealth = 1.3f;
    public const int BloodMoonTiers = 2;
    public const float BloodMoonEliteChance = 0.35f;
    public const float BloodMoonExp = 1.5f;

    private static readonly Color Blood = new Color(0.95f, 0.15f, 0.22f);

    public static bool Unlocked => PlayerPrefs.GetInt(UnlockedKey, 0) == 1;
    public static bool Active => Unlocked;
    public static bool BloodMoon => Active && DayNight.IsNight;

    public static int Level => Active ? PlayerPrefs.GetInt(LevelKey, 0) : 0;
    public static float Exp => PlayerPrefs.GetFloat(ExpKey, 0f);
    public static float Progress => Mathf.Clamp01(Exp / ExpPerLevel);
    public static bool Maxed => Level >= MaxLevel;
    public static float DamageMultiplier => 1f + DamagePerLevel * Level;

    // ---------------------------------------------------------------- unlocking
    public static void Unlock()
    {
        if (Unlocked) return;
        PlayerPrefs.SetInt(UnlockedKey, 1);
        PlayerPrefs.Save();
        Transform rowdy = Rowdy;
        if (rowdy == null) return;
        Vector3 head = rowdy.position + Vector3.up * 1.4f;
        IconPopup.Show(head + Vector3.up * 0.5f, null, "ENCORE!", new Color(1f, 0.82f, 0.3f), 1.6f, 3f);
        IconPopup.Show(head, null, "THE BLOOD MOONS RISE...", Blood, 0.9f, 3.5f);
        IconPopup.Show(head + Vector3.down * 0.5f, null, "SURF EAST: THE FRONTIER AWAITS", new Color(1f, 0.8f, 0.4f), 0.8f, 4f);
        ScreenShake.Impulse(0.6f);
    }

    public static void DevUnlock(bool on)
    {
        PlayerPrefs.SetInt(UnlockedKey, on ? 1 : 0);
        PlayerPrefs.Save();
    }

    public static void ResetProgress()
    {
        PlayerPrefs.DeleteKey(LevelKey);
        PlayerPrefs.DeleteKey(ExpKey);
        PlayerPrefs.Save();
    }

    public static void ResetAll()
    {
        PlayerPrefs.DeleteKey(UnlockedKey);
        ResetProgress();
    }

    // ---------------------------------------------------------------- EXP past level 10 (PlayerStats.AddEXP)
    public static void AddExp(float amount)
    {
        if (!Active || Maxed || amount <= 0f) return;
        float exp = Exp + amount;
        int level = PlayerPrefs.GetInt(LevelKey, 0);
        bool up = false;
        while (exp >= ExpPerLevel && level < MaxLevel)
        {
            exp -= ExpPerLevel;
            level++;
            up = true;
        }
        if (level >= MaxLevel) exp = 0f;
        PlayerPrefs.SetInt(LevelKey, level);
        PlayerPrefs.SetFloat(ExpKey, exp);
        PlayerPrefs.Save();
        if (up)
        {
            RunStats.LevelUps++;
            LevelUpFX.Play(PlayerStats.MaxLevel + level, "ENCORE!\n+" + level); // full heal, gold pillar, the boon picker follows
        }
    }

    private static Transform Rowdy
    {
        get
        {
            Health h = FindFirstObjectByType<Health>();
            return h != null ? h.transform : null;
        }
    }

    // ---------------------------------------------------------------- the Blood Moon look
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Hook()
    {
        Attach();
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => Attach();

    private static void Attach()
    {
        if (FindFirstObjectByType<Encore>() != null) return;
        new GameObject("Encore (auto)").AddComponent<Encore>();
    }

    private Image haze;
    private bool announced;

    private void Start()
    {
        var canvasObject = new GameObject("Blood Moon Haze");
        canvasObject.transform.SetParent(transform, false);
        var canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = -100; // over the world, under every HUD canvas
        var image = new GameObject("Haze").AddComponent<Image>();
        image.transform.SetParent(canvasObject.transform, false);
        image.raycastTarget = false;
        RectTransform r = image.rectTransform;
        r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one; r.offsetMin = r.offsetMax = Vector2.zero;
        image.color = Color.clear;
        haze = image;
    }

    private void Update()
    {
        if (haze == null) return;
        float night = Active ? DayNight.NightAmount : 0f;
        float pulse = 0.85f + 0.15f * Mathf.Sin(Time.time * 0.9f);
        haze.color = new Color(0.55f, 0.02f, 0.06f, 0.16f * night * pulse);

        if (night > 0.9f && !announced)
        {
            announced = true;
            Transform rowdy = Rowdy;
            if (rowdy != null)
            {
                IconPopup.Show(rowdy.position + Vector3.up * 1.6f, null, "BLOOD MOON", Blood, 1.3f, 2.6f);
                ScreenShake.Impulse(0.3f);
            }
        }
        else if (night < 0.1f) announced = false;
    }
}
