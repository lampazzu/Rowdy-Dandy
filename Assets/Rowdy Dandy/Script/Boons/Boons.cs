using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

// Level-up boons (Hades style): every level up = pick 1 of 3 boons from the patrons (BoonCatalog). Owned boons are
// saved (PlayerPrefs RD_Boons) and survive death; the dev reset / Reset Level wipe them.
//   - Attack / Dash / Jump / Cats / Special hold one boon each: taking another one for the same slot replaces it.
//   - Passives (and duos) stack.
//   - A boon you already own can come back at a higher rarity: UPGRADE.
//   - Hair Gel (very rare drop from ore rocks) = a reroll of the three cards.
// The picker is BoonPicker, the effects run in BoonRunner (on Rowdy), the werewolf is Werewolf.
public static class Boons
{
    private const string OwnedKey = "RD_Boons";
    private const string PicksKey = "RD_BoonPicks";
    private const string RerollKey = "RD_BoonRerolls";
    private const string FirstOfferKey = "RD_BoonFirstOffer";

    public struct Offer
    {
        public BoonDef def;
        public Rarity rarity;
        public bool upgrade;
        public Rarity oldRarity;
        public BoonDef replaces;
        public Rarity replacesRarity;
    }

    private static Dictionary<string, Rarity> owned;
    private static readonly List<string> order = new List<string>(); // pick order (HUD / build panel)

    public static float OpenNotBefore { get; private set; }

    // ---------------------------------------------------------------- state
    private static Dictionary<string, Rarity> Owned
    {
        get
        {
            if (owned == null) Load();
            return owned;
        }
    }

    public static IReadOnlyList<string> OwnedIds { get { if (owned == null) Load(); return order; } }

    public static bool Has(string id) => Owned.ContainsKey(id);
    public static Rarity RarityOf(string id) => Owned.TryGetValue(id, out Rarity r) ? r : Rarity.Common;
    public static float V(string id, int index) { BoonDef d = BoonCatalog.Get(id); return d != null ? d.Value(index, RarityOf(id)) : 0f; }

    public static int Picks => PlayerPrefs.GetInt(PicksKey, 0);
    public static int Rerolls => PlayerPrefs.GetInt(RerollKey, 0);
    public static int PendingPicks => Mathf.Max(0, (PlayerStats.Level - 1) + Encore.Level - Picks); // Encore levels: Jarvis endgame

    public static BoonDef InSlot(BoonSlot slot)
    {
        if (slot == BoonSlot.Passive) return null;
        foreach (string id in OwnedIds)
        {
            BoonDef d = BoonCatalog.Get(id);
            if (d != null && d.slot == slot) return d;
        }
        return null;
    }

    public static int CountFrom(Patron p)
    {
        int n = 0;
        foreach (string id in OwnedIds)
        {
            BoonDef d = BoonCatalog.Get(id);
            if (d != null && (d.patron == p || d.partner == p)) n++;
        }
        return n;
    }

    private static void Load()
    {
        owned = new Dictionary<string, Rarity>();
        order.Clear();
        int refunds = 0;
        foreach (string entry in PlayerPrefs.GetString(OwnedKey, "").Split(';'))
        {
            if (entry.Length == 0) continue;
            string[] parts = entry.Split(':');
            if (BoonCatalog.Get(parts[0]) == null)
            {
                if (BoonCatalog.Retired.Contains(parts[0])) refunds++; // a boon that left the game: the pick comes back
                continue;
            }
            int r = parts.Length > 1 && int.TryParse(parts[1], out int v) ? v : 0;
            owned[parts[0]] = (Rarity)Mathf.Clamp(r, 0, 4);
            if (!order.Contains(parts[0])) order.Add(parts[0]);
        }
        if (refunds > 0)
        {
            PlayerPrefs.SetInt(PicksKey, Mathf.Max(0, Picks - refunds));
            Save();
        }
    }

    private static void Save()
    {
        var sb = new System.Text.StringBuilder();
        foreach (string id in order) sb.Append(id).Append(':').Append((int)owned[id]).Append(';');
        PlayerPrefs.SetString(OwnedKey, sb.ToString());
        PlayerPrefs.Save();
    }

    // Take an offer: replaces whatever was in its slot, counts as this level's pick
    public static void Take(Offer o)
    {
        if (o.def == null) return;
        if (owned == null) Load();
        if (o.replaces != null && o.replaces != o.def)
        {
            owned.Remove(o.replaces.id);
            order.Remove(o.replaces.id);
        }
        owned[o.def.id] = o.rarity;
        if (!order.Contains(o.def.id)) order.Add(o.def.id);
        PlayerPrefs.SetInt(PicksKey, Picks + 1);
        Save();
        OpenNotBefore = Time.unscaledTime + 1f; // let the in-world celebration show before the next pick
        RunStats.BoonsTaken++;
        BoonRunner.OnBoonTaken(o.def);
    }

    public static void SkipPick()
    {
        PlayerPrefs.SetInt(PicksKey, Picks + 1);
        PlayerPrefs.Save();
    }

    public static void AddRerolls(int n)
    {
        PlayerPrefs.SetInt(RerollKey, Mathf.Max(0, Rerolls + n));
        PlayerPrefs.Save();
    }

    public static bool SpendReroll()
    {
        if (Rerolls <= 0) return false;
        AddRerolls(-1);
        return true;
    }

    // Dev reset / reset level
    public static void ClearAll()
    {
        owned = new Dictionary<string, Rarity>();
        order.Clear();
        PlayerPrefs.DeleteKey(OwnedKey);
        PlayerPrefs.DeleteKey(PicksKey);
        PlayerPrefs.DeleteKey(RerollKey);
        PlayerPrefs.DeleteKey(FirstOfferKey);
        PlayerPrefs.Save();
        Werewolf.ResetCharge();
    }

    // Dev tools: offer one more pick right now
    public static void DevOfferPick()
    {
        PlayerPrefs.SetInt(PicksKey, Picks - 1);
        PlayerPrefs.Save();
        OpenNotBefore = Time.unscaledTime + 0.3f;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetSession()
    {
        owned = null;
        order.Clear();
        OpenNotBefore = 0f;
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private static void OnSceneLoaded(Scene s, LoadSceneMode m) => OpenNotBefore = Time.unscaledTime + 1.6f;

    // LevelUpFX: the picker opens once the level-up moment has played
    public static void OnLevelUp() => OpenNotBefore = Mathf.Max(OpenNotBefore, Time.unscaledTime + 1.35f);

    // ---------------------------------------------------------------- offers
    private static bool Eligible(BoonDef d)
    {
        if (d.condition != null && !d.condition()) return false;
        if (d.IsDuo)
        {
            if (Has(d.id)) return false;
            foreach (string group in d.requires)
            {
                bool any = false;
                foreach (string id in group.Split(',')) if (Has(id)) { any = true; break; }
                if (!any) return false;
            }
        }
        return true;
    }

    private static Rarity RollRarity(BoonDef d)
    {
        if (d.legendaryOnly) return Rarity.Legendary;
        if (d.IsDuo) return Rarity.Duo;
        float luck = DropLuck.Bonus; // ore gems nudge rarities up a little
        float r = Random.value;
        if (r < 0.1f + luck * 0.06f) return Rarity.Epic;
        if (r < 0.38f + luck * 0.1f) return Rarity.Rare;
        return Rarity.Common;
    }

    public static List<Offer> MakeOffer(List<Offer> previous = null)
    {
        var result = new List<Offer>();
        var usedPatrons = new HashSet<Patron>();
        var usedBoons = new HashSet<string>();

        // Candidates: boons not owned, or owned at a lower rarity (upgrade)
        List<BoonDef> Candidates(Patron p)
        {
            var c = new List<BoonDef>();
            foreach (BoonDef d in BoonCatalog.All)
            {
                if (d.IsDuo || d.patron != p || usedBoons.Contains(d.id) || !Eligible(d)) continue;
                if (Has(d.id) && (d.legendaryOnly || RarityOf(d.id) >= Rarity.Epic)) continue;
                c.Add(d);
            }
            return c;
        }

        // The very first offer of a save shows the werewolf: the patron's signature move
        bool first = PlayerPrefs.GetInt(FirstOfferKey, 0) == 0;
        if (first && !Has("moon"))
        {
            PlayerPrefs.SetInt(FirstOfferKey, 1);
            BoonDef moon = BoonCatalog.Get("moon");
            result.Add(Build(moon));
            usedPatrons.Add(Patron.Lycanthropy);
            usedBoons.Add("moon");
        }

        // Patrons: favour the ones you already follow a little (builds come together), avoid a repeat of the last hand
        var patrons = new List<Patron>((Patron[])System.Enum.GetValues(typeof(Patron)));
        int guard = 0;
        while (result.Count < 3 && guard++ < 60)
        {
            float total = 0f;
            var weights = new List<float>();
            foreach (Patron p in patrons)
            {
                float w = usedPatrons.Contains(p) || Candidates(p).Count == 0 ? 0f : 1f + 0.35f * CountFrom(p);
                if (previous != null) foreach (Offer o in previous) if (o.def != null && o.def.patron == p) w *= 0.5f;
                weights.Add(w);
                total += w;
            }
            if (total <= 0f) break;
            float roll = Random.value * total;
            Patron pick = patrons[0];
            for (int i = 0; i < patrons.Count; i++) { roll -= weights[i]; if (roll <= 0f) { pick = patrons[i]; break; } }
            usedPatrons.Add(pick);

            List<BoonDef> c = Candidates(pick);
            BoonDef d = c[Random.Range(0, c.Count)];
            usedBoons.Add(d.id);
            result.Add(Build(d));
        }

        // A duo sometimes takes the place of a card (the moon card on the first offer stays)
        var duos = new List<BoonDef>();
        foreach (BoonDef d in BoonCatalog.All) if (d.IsDuo && Eligible(d)) duos.Add(d);
        if (duos.Count > 0 && result.Count > 0 && Random.value < 0.4f)
        {
            int slot = result.Count - 1 - Random.Range(0, first ? result.Count - 1 : result.Count);
            result[Mathf.Clamp(slot, 0, result.Count - 1)] = Build(duos[Random.Range(0, duos.Count)]);
        }
        return result;
    }

    private static Offer Build(BoonDef d)
    {
        var o = new Offer { def = d, rarity = RollRarity(d) };
        if (Has(d.id))
        {
            o.upgrade = true;
            o.oldRarity = RarityOf(d.id);
            if (o.rarity <= o.oldRarity) o.rarity = (Rarity)Mathf.Min((int)o.oldRarity + 1, (int)Rarity.Epic);
        }
        else
        {
            BoonDef current = InSlot(d.slot);
            if (current != null) { o.replaces = current; o.replacesRarity = RarityOf(current.id); }
        }
        return o;
    }

    // ---------------------------------------------------------------- numbers the game asks for
    public static int CatsWithRowdy
    {
        get
        {
            int n = 0;
            foreach (PetFollower p in PetFollower.Pets) if (p != null && p.IsCollected) n++;
            return n;
        }
    }

    // Day / night boons (Eclipse: both, all the time)
    public static bool NightBoons => DayNight.IsNight || Has("eclipse");
    public static bool DayBoons => !DayNight.IsNight || Has("eclipse");

    // Everything that scales Rowdy's own hits (not cats)
    public static float OutgoingMultiplier
    {
        get
        {
            float m = 1f;
            if (Has("mainchar")) m *= 1f + V("mainchar", 0) * StyleRank.Rank / 100f;
            if (Has("moonrage") && NightBoons) m *= 1f + V("moonrage", 0) / 100f;
            if (Has("daybreak") && DayBoons) m *= 1f + V("daybreak", 0) / 100f;
            if (Has("spotlight") && BoonRunner.InSpotlight) m *= 1f + V("spotlight", 0) / 100f;
            if (Has("flawless") && BoonRunner.AtFullHealth) m *= 1f + V("flawless", 0) / 100f;
            if (BoonRunner.SaltyActive) m *= 1.2f;
            if (BoonRunner.WellFed) m *= 1.2f; // Sandwich Time
            if (Werewolf.Active) m *= Werewolf.DamageMultiplier;
            m *= Encore.DamageMultiplier;
            return m;
        }
    }

    // Rowdy's hit on this particular enemy (Hair Spray gloss, Fester, Hunters Mark, Butcher Block)
    public static float TargetMultiplier(EnemyHealth e)
    {
        if (e == null) return 1f;
        float m = 1f;
        if (Has("hairspray") && Glossed.On(e)) m *= 1f + V("hairspray", 0) / 100f;
        if (Has("fester") && e.TryGetComponent(out StatusEffects s) && s.IsPoisoned) m *= 1f + V("fester", 0) / 100f;
        if (Has("huntmark") && HuntersMark.Target == e) m *= 1f + V("huntmark", 0) / 100f;
        if (Has("butcher") && ActiveWeapon == 3 && e.currentenemyHealth <= e.startingenemyHealth * 0.4f) m *= V("butcher", 0);
        return m;
    }

    // 0 Rod, 1 Sword, 2 Naginata, 3 Cleaver
    public static int ActiveWeapon => WeaponManager.Instance != null ? WeaponManager.Instance.GetActiveWeaponIndex() : 0;

    public static float IncomingMultiplier
    {
        get
        {
            float m = 1f;
            if (Has("furcoat")) m *= 1f - Mathf.Min(CatsWithRowdy, 8) * V("furcoat", 0) / 100f;
            if (Has("silverfur") && NightBoons) m *= 1f - V("silverfur", 0) / 100f;
            if (Werewolf.Active) m *= 0.6f;
            return m;
        }
    }

    public static bool CatsFeral => Has("wolfpack") && (Werewolf.Active || NightBoons);

    public static float CatDamageMultiplier
    {
        get
        {
            float m = 1f;
            if (Has("packleader")) m *= 1f + V("packleader", 0) / 100f;
            if (Has("catfood")) m *= 1.4f;
            if (CatsFeral) m *= 2f;
            return m;
        }
    }

    // One cat's damage: everything above, plus Top Cat on the party leader
    public static float CatDamageFor(PetFollower cat)
    {
        float m = CatDamageMultiplier;
        if (Has("topcat") && cat != null && CatRoster.IsLeader(cat)) m *= 1f + V("topcat", 0) / 100f;
        return m;
    }

    public static float CatCooldownMultiplier
    {
        get
        {
            float m = 1f;
            if (Has("catnip")) m *= 1f - V("catnip", 0) / 100f;
            if (CatsFeral) m *= 0.5f;
            return m;
        }
    }

    public static float ExecuteThresholdMultiplier => Has("packleader") ? 2f : 1f;

    public static float CritChanceBonus
    {
        get
        {
            float c = 0f;
            if (Has("glamourpuss")) c += 3f * CatsWithRowdy;
            if (Has("openwounds")) c += 10f;
            if (Has("cheese")) c += V("cheese", 0);
            if (Has("spotlight") && BoonRunner.InSpotlight) c += 15f;
            return c;
        }
    }

    public static float CritMultiplierBonus => Has("cheese") ? 0.2f : 0f;

    public static float SpeedMultiplier
    {
        get
        {
            float m = 1f;
            if (Has("moonrage") && NightBoons) m *= 1.15f;
            if (Werewolf.Active) m *= Werewolf.SpeedMultiplier;
            return m;
        }
    }

    // Whetstone: the attack clips play faster (WeaponManager scales its attack speed floats)
    public static float AttackSpeedMultiplier => Has("whetstone") ? 1f + V("whetstone", 0) / 100f : 1f;

    // Weapon wear per swing: werewolf claws are free, Weapon Snob doubles it, Tempered Steel may skip it, Forged In Sunlight -25%
    public static float DurabilityCost
    {
        get
        {
            if (Werewolf.Active) return 0f;
            if (Has("tempered") && Random.value < V("tempered", 0) / 100f) { BoonRunner.OnDurabilitySaved(); return 0f; }
            float cost = Has("weaponsnob") ? 2f : 1f;
            if (Has("forgefire")) cost *= 0.75f;
            return cost;
        }
    }

    // Tempered Steel: weapons picked up come with more durability
    public static float PickupDurabilityMultiplier => Has("tempered") ? 1.25f : 1f;

    // Secret Sauce: every heal is bigger
    public static float HealMultiplier => Has("secretsauce") ? 1f + V("secretsauce", 0) / 100f : 1f;

    // Moon Feast: the moon meter fills faster. Eclipse: always at the night rate
    public static float MoonChargeMultiplier => (Has("feast") ? 1f + V("feast", 0) / 100f : 1f) * (Has("eclipse") || DayNight.IsNight ? 2f : 1f);
}