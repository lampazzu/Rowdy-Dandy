using System.Collections;
using System.IO;
using UnityEngine;

// TEMPORARY smoke test for mega fix 10 (only runs when the scratchpad flag file exists) - delete this file
public class ZZ_TempStunTest : MonoBehaviour
{
    private const string Dir = @"C:\Users\Fabui\AppData\Local\Temp\claude\C--bi-fa-Rowdy-Dandy\53153cc0-0531-4f3c-9d65-1e146820326e\scratchpad\";
    private static bool started;
    private static StreamWriter log;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Go()
    {
        if (started || !File.Exists(Dir + "mf10.flag")) return;
        started = true;
        log = new StreamWriter(Dir + "mf10_log.txt", false) { AutoFlush = true };
        Application.logMessageReceived += (msg, stack, type) =>
        {
            if (type == LogType.Exception || type == LogType.Error) log.WriteLine("[" + type + "] " + msg + "\n" + stack);
        };
        var go = new GameObject("ZZ mf10 test");
        DontDestroyOnLoad(go);
        go.AddComponent<ZZ_TempStunTest>();
    }

    private static void L(string s) { log?.WriteLine(Time.time.ToString("0.00") + " " + s); }

    private void Shot(string name)
    {
        Camera cam = Camera.main;
        if (cam == null) return;
        var canvases = FindObjectsByType<Canvas>(FindObjectsSortMode.None);
        var modes = new RenderMode[canvases.Length];
        for (int i = 0; i < canvases.Length; i++)
        {
            modes[i] = canvases[i].renderMode;
            if (canvases[i].isRootCanvas && canvases[i].renderMode == RenderMode.ScreenSpaceOverlay) { canvases[i].renderMode = RenderMode.ScreenSpaceCamera; canvases[i].worldCamera = cam; canvases[i].planeDistance = 1f; }
        }
        var rt = new RenderTexture(1280, 720, 24);
        cam.targetTexture = rt; cam.Render(); cam.targetTexture = null;
        RenderTexture.active = rt;
        var tex = new Texture2D(1280, 720, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0); tex.Apply();
        RenderTexture.active = null;
        File.WriteAllBytes(Dir + name + ".png", tex.EncodeToPNG());
        for (int i = 0; i < canvases.Length; i++) if (canvases[i] != null) canvases[i].renderMode = modes[i];
    }

    private EnemyHealth Spawn(string prefab, Vector3 at)
    {
        GameObject p = Resources.Load<GameObject>("Enemies Prefab/" + prefab);
        if (p == null) { L("no prefab " + prefab); return null; }
        return Instantiate(p, at, Quaternion.identity).GetComponentInChildren<EnemyHealth>();
    }

    private void Give(string id)
    {
        BoonDef d = BoonCatalog.Get(id);
        if (d == null) { L("NO BOON " + id); return; }
        Boons.DevGive(d, Rarity.Epic);
        L("gave " + id + " owned=" + Boons.Has(id) + " rarity=" + Boons.RarityOf(id));
    }

    private IEnumerator Start()
    {
        yield return new WaitForSecondsRealtime(5f);
        L("start");
        DevTools.GodMode = true;
        Health h = FindFirstObjectByType<Health>();
        Transform rowdy = h.transform;
        PlayerStats.Instance?.SetLevel(6);
        Boons.ClearAll();
        Boons.Unlock();
        L("catalog count " + BoonCatalog.All.Count + " rarity values hairflip C/U/R/E/L = " + BoonCatalog.Get("hairflip").Value(0, Rarity.Common) + "/" + BoonCatalog.Get("hairflip").Value(0, Rarity.Uncommon) + "/" + BoonCatalog.Get("hairflip").Value(0, Rarity.Rare) + "/" + BoonCatalog.Get("hairflip").Value(0, Rarity.Epic) + "/" + BoonCatalog.Get("hairflip").Value(0, Rarity.Legendary));
        for (int i = 0; i < 6; i++) { var o = Boons.MakeOffer(); string s = ""; foreach (var x in o) s += x.def.id + ":" + x.rarity + " "; L("offer " + s); }

        // --- wereknights walk
        Vector3 r = rowdy.position;
        EnemyHealth k = Spawn("Enemy_WereKnight", r + new Vector3(4f, 1f, 0f));
        yield return new WaitForSecondsRealtime(1.5f);
        if (k != null) L("wereknight walk comp=" + (k.GetComponent<WereKnightWalk>() != null) + " sprite=" + (k.GetComponent<SpriteRenderer>()?.sprite?.name));
        Shot("t1_wereknight");
        yield return new WaitForSecondsRealtime(0.5f);
        Shot("t1b_wereknight");
        DevTools.KillNearby();
        yield return new WaitForSecondsRealtime(1f);

        // --- cats + jelly buddies + cat loyalty
        int got = 0;
        foreach (PetFollower p in new System.Collections.Generic.List<PetFollower>(PetFollower.Pets)) if (p != null && !p.IsCollected && got < 3 && CatRoster.HasRoom) { p.transform.position = rowdy.position + Vector3.up; p.Collect(rowdy); got++; }
        L("cats " + Boons.CatsWithRowdy + " cap " + CatRoster.Capacity);
        Give("catloyalty");
        Give("wipeout");
        Give("jellypals");
        yield return new WaitForSecondsRealtime(3f);
        L("after loyalty cats " + Boons.CatsWithRowdy + " jellies " + FindObjectsByType<AllyJelly>(FindObjectsSortMode.None).Length);
        for (int i = 0; i < 4; i++) Spawn("Enemy_BigWerewolf", rowdy.position + new Vector3(2.5f + i * 0.8f, 1f, 0f));
        yield return new WaitForSecondsRealtime(2.5f);
        Shot("t2_jellies");

        // --- leviathan
        Give("leviathan");
        h.SetHealth(h.startingHealth * 0.2f);
        yield return new WaitForSecondsRealtime(1.5f);
        L("leviathan arms " + (FindFirstObjectByType<LeviathanArms>() != null));
        Shot("t3_leviathan");
        yield return new WaitForSecondsRealtime(0.4f);
        Shot("t3b_leviathan");
        h.AddHealth(200f);
        yield return new WaitForSecondsRealtime(1f);

        // --- eggs + food stock
        Give("eggs");
        Give("foodstock");
        BoonRunner.OnSurfDash();
        yield return new WaitForSecondsRealtime(0.5f);
        Shot("t4_eggs");
        yield return new WaitForSecondsRealtime(1f);

        // --- ingredient rain
        Give("sandwichrain");
        var sb = rowdy.GetComponent<SpecialBoons>();
        L("special ready " + SpecialBoons.Ready);
        sb.StartCoroutine("IngredientRain");
        yield return new WaitForSecondsRealtime(1.2f);
        Shot("t5_rain");
        yield return new WaitForSecondsRealtime(2.5f);
        Shot("t5b_rain");

        // --- armory
        Give("armory");
        for (int i = 0; i < 3; i++) Spawn("Enemy_BigWerewolf", rowdy.position + new Vector3(-2.5f - i, 1f, 0f));
        sb.StartCoroutine("Armory");
        yield return new WaitForSecondsRealtime(1.5f);
        Shot("t6_armory");
        yield return new WaitForSecondsRealtime(2f);

        // --- werewolf
        Give("moon");
        yield return new WaitForSecondsRealtime(0.5f);
        var ww = rowdy.GetComponent<Werewolf>();
        Werewolf.Fill();
        ww.StartCoroutine("Transform");
        yield return new WaitForSecondsRealtime(1.2f);
        L("werewolf active " + Werewolf.Active + " credit=" + KillCredit.Rowdy().name);
        for (int i = 0; i < 3; i++) Spawn("Enemy_BigWerewolf", rowdy.position + new Vector3(2f + i, 1f, 0f));
        yield return new WaitForSecondsRealtime(0.8f);
        Werewolf.Attack(true, false);
        yield return new WaitForSecondsRealtime(0.4f);
        Werewolf.Dash(1f);
        yield return new WaitForSecondsRealtime(0.08f);
        Shot("t7_wolfdash");
        yield return new WaitForSecondsRealtime(0.8f);
        Werewolf.Attack(true, true);
        yield return new WaitForSecondsRealtime(0.1f);
        Shot("t7b_roar");
        yield return new WaitForSecondsRealtime(4.5f);

        // --- weapon tricks smoke
        WeaponTricks.DropEveryWeapon(rowdy.position + Vector3.right * 2f);
        Give("sharp");
        WeaponManager.Instance.PickupWeapon(WeaponType.Sword, 50f);
        WeaponManager.Instance.PickupWeapon(WeaponType.Sword, 50f);
        L("sharp sword " + WeaponSharpness.Bonus01(1) + " active " + Boons.ActiveWeapon + " weaponMult " + Boons.WeaponMultiplier);
        Give("skybeam");
        SkyBeam.Fire(rowdy.position + Vector3.up * 0.6f, 1f, 40f);
        yield return new WaitForSecondsRealtime(0.4f);
        Shot("t8_skybeam");
        Give("satisfied");
        h.AddHealth(500f, true);
        yield return new WaitForSecondsRealtime(0.3f);
        L("overheal " + h.Overheal + " / " + h.MaxOverheal);
        Shot("t9_overheal");
        yield return new WaitForSecondsRealtime(1f);

        DevTools.KillNearby();
        DevTools.GodMode = false;
        L("DONE");
        File.Delete(Dir + "mf10.flag");
        log.Flush();
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#endif
    }
}
