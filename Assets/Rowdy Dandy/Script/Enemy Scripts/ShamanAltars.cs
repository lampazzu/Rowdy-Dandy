using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

// THE TWO ALTAR SHAMANS and the PILLAR OF DANDY (the gate by the Jungle, x ~104): they were 1 HP props whose pillar
// flags (EnemyHealth.isPillarA / B, static) survived a reload half-set. Now:
//   - Shaman A stands on the highest platform of the Gloomy Forest (moved there at load), Shaman B where it was.
//   - each altar just breaks when hit (no ward, no wolves). Finding one marks the other on the map (WorldMap.Mark).
//   - each dead shaman lights its pillar of flame; both = the Pillar of Dandy opens (its own Activate clip).
//   - until then the gate is SOLID (a blocker collider over the pillar), and it says what opens it.
//   - progress is saved (RD_AltarA / RD_AltarB): dead shamans stay dead, an open gate stays open.
public class ShamanAltars : MonoBehaviour
{
    private const string KeyA = "RD_AltarA", KeyB = "RD_AltarB";
    private static readonly Color Spirit = new Color(0.55f, 0.85f, 1f);

    private class Altar
    {
        public string key, label;
        public EnemyHealth shaman;
        public Transform pillar;
        public bool awake, dead;
    }

    private readonly Altar[] altars = new Altar[2];
    private Transform gate;
    private BoxCollider2D blocker;
    private bool gateOpen;
    private float hintAt;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Hook()
    {
        Setup();
        SceneManager.sceneLoaded += (s, m) => Setup();
    }

    public static void ResetSaved() { PlayerPrefs.DeleteKey(KeyA); PlayerPrefs.DeleteKey(KeyB); WorldMap.Unmark(KeyA); WorldMap.Unmark(KeyB); }

    private static void Setup()
    {
        EnemyHealth a = null, b = null;
        foreach (EnemyHealth e in FindObjectsByType<EnemyHealth>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (e.name == "RDR_SoulOFShaman_A") a = e;
            else if (e.name == "RDR_SoulOFShaman_B") b = e;
        }
        if (a == null || b == null) return;
        var go = new GameObject("Shaman Altars (auto)");
        var m = go.AddComponent<ShamanAltars>();
        m.Build(a, b);
    }

    private void Build(EnemyHealth a, EnemyHealth b)
    {
        // the pillars and the gate are children of shaman A (and B): free them before anything moves
        Transform pillarA = null, pillarB = null;
        foreach (Transform t in a.GetComponentsInChildren<Transform>(true))
        {
            if (t.name == "RDR_BigWolfBongus_A") pillarA = t;
            else if (t.name == "RDR_BigWolfBongus_B") pillarB = t;
            else if (t.name == "AnimationPillar") gate = t;
        }
        Transform parent = a.transform.parent;
        foreach (Transform child in new List<Transform>(ChildrenOf(a.transform))) child.SetParent(parent, true);
        foreach (Transform child in new List<Transform>(ChildrenOf(b.transform))) child.SetParent(parent, true);

        altars[0] = new Altar { key = KeyA, label = "THE FOREST SHAMAN", shaman = a, pillar = pillarA };
        altars[1] = new Altar { key = KeyB, label = "THE SHORE SHAMAN", shaman = b, pillar = pillarB };

        MoveToForestTop(a);

        bool deadA = PlayerPrefs.GetInt(KeyA, 0) == 1, deadB = PlayerPrefs.GetInt(KeyB, 0) == 1;
        gateOpen = deadA && deadB;
        EnemyHealth.SetAltarState(deadA, deadB, gateOpen); // already open: no camera pan again
        for (int i = 0; i < 2; i++)
        {
            Altar al = altars[i];
            al.dead = i == 0 ? deadA : deadB;
            if (al.dead) al.shaman.gameObject.SetActive(false);
            else al.shaman.SetMaxHealth(60f);
        }

        // the gate is solid until it opens
        if (gate != null && !gateOpen)
        {
            SpriteRenderer sr = gate.GetComponentInChildren<SpriteRenderer>();
            Bounds bb = sr != null ? sr.bounds : new Bounds(gate.position, new Vector3(1.4f, 3f, 0f));
            var bl = new GameObject("Pillar Of Dandy (blocker)");
            bl.transform.position = bb.center;
            GameObject ground = GameObject.Find("BeachMap");
            if (ground != null) { bl.layer = ground.layer; bl.tag = ground.tag; }
            blocker = bl.AddComponent<BoxCollider2D>();
            blocker.size = new Vector2(Mathf.Max(0.8f, bb.size.x * 0.8f), Mathf.Max(3f, bb.size.y));
        }
    }

    private static IEnumerable<Transform> ChildrenOf(Transform t) { foreach (Transform c in t) yield return c; }

    // The highest flat spot of the Gloomy Forest (x 5..60 above y 7): ground or a platform, room for the shaman
    private static void MoveToForestTop(EnemyHealth shaman)
    {
        float bestY = float.MinValue;
        Vector2 best = Vector2.zero;
        for (float x = 6f; x <= 59f; x += 0.25f)
        {
            RaycastHit2D top = default;
            bool found = false;
            foreach (RaycastHit2D h in Physics2D.RaycastAll(new Vector2(x, 45f), Vector2.down, 38f))
            {
                if (!SolidGround.IsGround(h.collider) || h.normal.y < 0.9f) continue;
                top = h; found = true; break; // the topmost surface in this column
            }
            if (!found || top.point.y < 7f || top.point.y <= bestY) continue;
            bool flat = true;
            foreach (float dx in new[] { -0.4f, 0.4f })
                if (!SolidGround.Ray(top.point + new Vector2(dx, 0.3f), Vector2.down, 0.6f, out RaycastHit2D s) || Mathf.Abs(s.point.y - top.point.y) > 0.1f) flat = false;
            if (!flat || SolidGround.Blocked(top.point + new Vector2(0f, 0.9f), new Vector2(0.6f, 1.2f))) continue;
            bestY = top.point.y;
            best = top.point;
        }
        if (bestY == float.MinValue) return;
        Collider2D col = shaman.GetComponent<Collider2D>();
        float feet = col != null ? col.bounds.min.y - shaman.transform.position.y : -0.5f;
        shaman.transform.position = new Vector3(best.x, best.y - feet, shaman.transform.position.z);
    }

    // ---------------------------------------------------------------- every frame
    private void Update()
    {
        Transform rowdy = BoonRunner.Rowdy;
        if (rowdy == null || PauseMenu.IsPaused) return;
        Vector3 r = BoonRunner.RowdyCenter;
        foreach (Altar a in altars) if (a != null && !a.dead) Tick(a, r);

        if (!gateOpen && gate != null && Vector2.Distance(r, gate.position) < 3.2f && Time.time >= hintAt)
        {
            hintAt = Time.time + 3f;
            int down = (altars[0].dead ? 1 : 0) + (altars[1].dead ? 1 : 0);
            IconPopup.Show(gate.position + Vector3.up * 2.4f, null, "TWO SHAMANS HOLD THIS GATE (" + down + "/2)", Spirit, 0.8f, 2.2f);
        }
    }

    // Finding an altar marks the other one on the map. No ward, no fight: just break it.
    private void Tick(Altar a, Vector3 rowdy)
    {
        EnemyHealth s = a.shaman;
        if (s == null) return;
        if (s.enemydead) { Fall(a); return; }
        if (a.awake || Vector2.Distance(rowdy, s.transform.position) > 7f) return;
        a.awake = true;
        Banner.Show(a.label, null, Spirit, 1.6f);
        FXSound.Play("SmokePoof", 0.6f, 0.7f);
        Altar other = altars[0] == a ? altars[1] : altars[0];
        if (other != null && !other.dead && other.shaman != null)
        {
            WorldMap.Mark(other.key, other.shaman.transform.position, Spirit);
            IconPopup.Show(s.transform.position + Vector3.up * 1.6f, null, "THE OTHER ALTAR IS ON YOUR MAP", Spirit, 0.85f, 2.6f);
        }
    }
    private void Fall(Altar a)
    {
        a.dead = true;
        WorldMap.Unmark(a.key);
        PlayerPrefs.SetInt(a.key, 1);
        PlayerPrefs.Save();
        int down = (altars[0].dead ? 1 : 0) + (altars[1].dead ? 1 : 0);
        // its pillar of flame lights up
        if (a.pillar != null)
        {
            GraftFX.Play("BRJ_FireBurst", a.pillar.position + Vector3.up * 0.2f, Color.white, 95, null, false, false, 1f, new Vector2(0.5f, 0f));
            PulseRing.Spawn(a.pillar.position + Vector3.up * 1f, new Color(1f, 0.6f, 0.3f, 1f), 2.2f, 0.5f);
        }
        BoonArt.Play(BoonArt.Get != null ? BoonArt.Get.sunBeam : null, 0.5f, 0.9f);
        Banner.Show(down == 2 ? "THE PILLAR OF DANDY OPENS" : "ALTAR " + down + " / 2 BROKEN", null, new Color(1f, 0.7f, 0.35f), 2f);
        if (down == 2) StartCoroutine(OpenGate());
    }

    private IEnumerator OpenGate()
    {
        gateOpen = true;
        yield return new WaitForSeconds(0.8f);
        ScreenShake.Impulse(0.6f);
        GamepadRumble.Pulse(0.5f, 0.7f, 0.8f);
        BoonArt.Play(BoonArt.Get != null ? BoonArt.Get.rockBreak : null, 0.6f, 0.7f);
        if (gate != null) FXParticle.Burst(gate.position + Vector3.up * 1f, new Color(0.8f, 0.65f, 0.5f), 30, 1f, 4f, 6f, 0.8f, true);
        yield return new WaitForSeconds(2.5f); // the pillar's own clip moves it out of the way
        if (blocker != null) Destroy(blocker.gameObject);
    }
}
