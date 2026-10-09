using System.Collections;
using UnityEngine;

// A string of explosions from the gnoll archers' explosion art (Resources/VFX/Explosion) with sound and shake:
//   ExplosionChain.Play(position, count: 24, radius: 1.6f, duration: 1.2f)
// They come faster and faster, and the last one is the biggest. Optional damage to Rowdy (the Elder's Moon Nova).
public class ExplosionChain : MonoBehaviour
{
    private static GameObject explosionPrefab;
    private static AudioClip explosionSound;
    private static GameObject holder;

    public static GameObject Prefab
    {
        get
        {
            if (explosionPrefab == null) explosionPrefab = Resources.Load<GameObject>("VFX/Explosion");
            return explosionPrefab;
        }
    }

    public static void Play(Vector3 center, int count, float radius, float duration, float scale = 0.4f, bool finale = true)
    {
        var go = new GameObject("ExplosionChain");
        go.transform.position = center;
        go.AddComponent<ExplosionChain>().StartCoroutine(Run(go, center, count, radius, duration, scale, finale));
    }

    // One explosion (+ sound + small shake)
    public static GameObject Boom(Vector3 at, float scale, float volume = 0.35f, float shake = 0.15f)
    {
        GameObject b = null;
        if (Prefab != null)
        {
            // The prefab carries an enabled Vanish (Destroy on OnEnable), which killed every explosion the frame it
            // was spawned. Made under an inactive holder, Vanish switched off, then let loose with a timed cleanup.
            if (holder == null) { holder = new GameObject("Explosion Holder (inactive)"); holder.SetActive(false); DontDestroyOnLoad(holder); }
            b = Instantiate(Prefab, at, Quaternion.Euler(0f, 0f, Random.Range(0, 4) * 90f), holder.transform);
            foreach (Vanish v in b.GetComponentsInChildren<Vanish>(true)) v.enabled = false;
            b.transform.SetParent(null, true);
            b.transform.localScale = Prefab.transform.localScale * scale;
            Destroy(b, 1.5f);
        }
        if (explosionSound == null) explosionSound = Resources.Load<AudioClip>("Sounds/Explosion");
        SoundManager.PlaySfx(explosionSound, volume);
        ScreenShake.Impulse(shake);
        return b;
    }

    private static IEnumerator Run(GameObject owner, Vector3 center, int count, float radius, float duration, float scale, bool finale)
    {
        // Gaps shrink as it goes (accelerating crackle)
        float total = 0f;
        var gaps = new float[count];
        for (int i = 0; i < count; i++) { gaps[i] = Mathf.Lerp(1.6f, 0.35f, i / (float)Mathf.Max(1, count - 1)); total += gaps[i]; }
        float k = duration / Mathf.Max(0.01f, total);

        for (int i = 0; i < count; i++)
        {
            Vector2 offset = Random.insideUnitCircle * radius * Mathf.Lerp(0.5f, 1f, i / (float)count);
            float s = scale * Random.Range(0.6f, 1.15f);
            Boom(center + (Vector3)offset, s, 0.18f + 0.1f * Random.value, 0.08f);
            if (i % 6 == 5) GamepadRumble.Pulse(0.4f, 0.7f, 0.08f);
            yield return new WaitForSecondsRealtime(gaps[i] * k);
        }

        if (finale)
        {
            Boom(center, scale * 2.2f, 0.6f, 0.7f);
            TimeSlowController.HitStop(0.08f, 0.05f);
            GamepadRumble.Pulse(0.8f, 1f, 0.25f);
        }
        Destroy(owner, 0.1f);
    }
}
