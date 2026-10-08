using System.Collections;
using System.IO;
using UnityEngine;

// TEMPORARY test (deleted after the session)
public class ZZ_TempStunTest : MonoBehaviour
{
    private const string Dir = @"C:\Users\Fabui\AppData\Local\Temp\claude\C--bi-fa-Rowdy-Dandy\dd177f07-2f01-4028-9459-571ea161bbf0\scratchpad\";
    private static bool started;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Go()
    {
        if (started || !File.Exists(Dir + "stuntest.flag")) return;
        started = true;
        var go = new GameObject("ZZ stun test");
        DontDestroyOnLoad(go);
        go.AddComponent<ZZ_TempStunTest>();
    }

    private void Shot(string name)
    {
        Camera cam = Camera.main;
        var rt = new RenderTexture(960, 540, 24);
        cam.targetTexture = rt; cam.Render(); cam.targetTexture = null;
        RenderTexture.active = rt;
        var tex = new Texture2D(960, 540, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, 960, 540), 0, 0); tex.Apply();
        RenderTexture.active = null;
        File.WriteAllBytes(Dir + name + ".png", tex.EncodeToPNG());
    }

    private static string State(Animator a)
    {
        if (a == null) return "-";
        var info = a.GetCurrentAnimatorClipInfo(0);
        return (info.Length > 0 ? info[0].clip.name : "?") + " speed=" + a.speed;
    }

    private IEnumerator Start()
    {
        yield return new WaitForSecondsRealtime(4f);
        DevTools.GodMode = true;
        Transform rowdy = FindFirstObjectByType<Health>().transform;
        GameObject prefab = Resources.Load<GameObject>("Enemies Prefab/Enemy_GnollWarrior");
        GameObject wolf = Resources.Load<GameObject>("Enemies Prefab/Enemy_BigWerewolf");
        var list = new System.Collections.Generic.List<EnemyHealth>();
        for (int i = 0; i < 2; i++) list.Add(Instantiate(prefab, rowdy.position + new Vector3(2.5f + i, 0.8f, 0f), Quaternion.identity).GetComponentInChildren<EnemyHealth>());
        list.Add(Instantiate(wolf, rowdy.position + new Vector3(-3f, 0.8f, 0f), Quaternion.identity).GetComponentInChildren<EnemyHealth>());
        yield return new WaitForSecondsRealtime(1.5f);
        Vector3 ground = rowdy.position;
        if (SolidGround.Ray(rowdy.position + Vector3.up * 0.5f, Vector2.down, 5f, out RaycastHit2D hit)) ground = hit.point;
        Debug.Log("[ZZSTUN] stunned " + CatPowers.Stomp(ground, null));
        yield return new WaitForSecondsRealtime(0.15f);
        Shot("s1_shock");
        yield return new WaitForSecondsRealtime(0.2f);
        foreach (EnemyHealth e in list) if (e != null) Debug.Log("[ZZSTUN] " + e.name + " stunned=" + StatusEffects.IsStunned(e.gameObject) + " anim=" + State(e.GetComponent<Animator>()));
        EnemyHealth victim = list[0];
        EnemyHealth.CreditNextHit(KillCredit.Rowdy());
        victim.TakeDamageEnemy(9999f);
        yield return new WaitForSecondsRealtime(0.6f);
        if (victim != null) Debug.Log("[ZZSTUN] victim after death anim=" + State(victim.GetComponent<Animator>()));
        yield return new WaitForSecondsRealtime(1.2f);
        foreach (EnemyHealth e in list) if (e != null && !e.enemydead) Debug.Log("[ZZSTUN] after stun " + e.name + " stunned=" + StatusEffects.IsStunned(e.gameObject) + " anim=" + State(e.GetComponent<Animator>()));
        DevTools.KillNearby();
        DevTools.GodMode = false;
        File.Delete(Dir + "stuntest.flag");
        Debug.Log("[ZZSTUN] DONE");
    }
}
