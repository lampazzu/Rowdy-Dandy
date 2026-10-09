using System.Collections;
using Cinemachine;
using UnityEngine;

// The first spawn of a play session: Rowdy drops out of the sky onto his spawn point. The camera waits at the
// landing spot, he streaks down with a whistle and a trail, and lands with a slam (shake, hit-stop, shockwave,
// dust, squash). Later reloads (deaths, rests, dev keys) spawn normally.
public class FirstDrop : MonoBehaviour
{
    public static bool Running { get; private set; }
    private static bool done;

    private const float DropHeight = 13f;
    private const float FallTime = 0.85f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetSession() { done = false; Running = false; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void OnFirstLoad()
    {
        if (done) return;
        done = true;
        Health rowdy = FindFirstObjectByType<Health>();
        if (rowdy == null) return;
        var go = new GameObject("First Drop (auto)");
        go.AddComponent<FirstDrop>().StartCoroutine(go.GetComponent<FirstDrop>().Drop(rowdy));
    }

    // A reload in the middle of the drop (dev reset, death, rest) destroys this object with the scene and the coroutine
    // never reaches its end: Running stayed true for the whole session, which kept the boon picker from ever opening,
    // made Rowdy immune to damage and blocked checkpoints and tutorials.
    private void OnDestroy() => Running = false;

    private IEnumerator Drop(Health rowdy)
    {
        Running = true;
        yield return null; // let PlayerRespawn / the camera settle on the spawn point

        Transform body = rowdy.transform;
        var rb = body.GetComponent<Rigidbody2D>();
        var move = body.GetComponent<PlayerMovement>();
        var anim = body.GetComponent<Animator>();
        Collider2D col = body.GetComponent<Collider2D>();

        // Where he lands: the floor under the spawn point
        Vector3 spawn = body.position;
        Vector3 land = spawn;
        float feet = col != null ? body.position.y - col.bounds.min.y : 0f;
        if (SolidGround.Ray((Vector2)spawn + Vector2.up * 0.5f, Vector2.down, 6f, out RaycastHit2D hit)) land = new Vector3(spawn.x, hit.point.y + feet + 0.02f, spawn.z);

        // Camera holds on the landing spot meanwhile
        CinemachineVirtualCamera cam = FindFirstObjectByType<CinemachineVirtualCamera>();
        Transform oldFollow = cam != null ? cam.Follow : null;
        Transform anchor = new GameObject("First Drop Camera Anchor").transform;
        anchor.position = land;
        if (cam != null && oldFollow == body) cam.Follow = anchor;

        if (move != null) move.enabled = false;
        RigidbodyType2D oldType = rb != null ? rb.bodyType : RigidbodyType2D.Dynamic;
        if (rb != null) { rb.linearVelocity = Vector2.zero; rb.bodyType = RigidbodyType2D.Kinematic; }
        if (anim != null) { anim.SetBool("IsGrounded", false); anim.SetBool("IsJumping", true); }

        // Up in the sky while the level finishes loading (the first frames hitch) and the respawn swirl
        // (RowdyRespawn clip, 1.25 s) plays out, then a beat with an empty spot, then the whistle
        Vector3 top = land + Vector3.up * DropHeight;
        body.position = top;
        float waited = 0f, realWaited = 0f;
        int smooth = 0;
        while ((waited < 1.4f || smooth < 10) && realWaited < 6f)
        {
            waited += Time.deltaTime;
            realWaited += Mathf.Min(Time.unscaledDeltaTime, 0.1f); // the first frame's delta includes the whole load
            smooth = Time.unscaledDeltaTime < 0.05f ? smooth + 1 : 0;
            body.position = top;
            yield return null;
        }
        yield return new WaitForSeconds(0.3f);
        // a cinematic whoosh (Resources/Sounds/FX/SkyFall); the old synth whistle only if it's missing
        AudioClip fall = FXSound.Clip("SkyFall");
        FXSound.Play(fall != null ? fall : Whistle(), fall != null ? 0.8f : 0.45f, 1f);

        float trail = 0f;
        for (float t = 0f; t < FallTime; t += Time.deltaTime)
        {
            float k = t / FallTime;
            body.position = Vector3.Lerp(top, land, k * k); // accelerating
            trail -= Time.deltaTime;
            if (trail <= 0f)
            {
                trail = 0.02f;
                Vector3 c = col != null ? col.bounds.center : body.position;
                FXParticle.Burst(c + Vector3.up * 0.3f, new Color(1f, 0.75f, 0.95f), 2, 0.1f, 0.5f, 0f, 0.35f);
                FXParticle.Burst(c + Vector3.up * 0.5f, Color.white, 1, 0.1f, 0.3f, 0f, 0.25f);
            }
            yield return null;
        }
        body.position = land;

        // SLAM
        if (rb != null) rb.bodyType = oldType;
        if (anim != null) { anim.SetBool("IsJumping", false); anim.SetBool("IsGrounded", true); }
        TimeSlowController.HitStop(0.09f, 0.05f);
        ScreenShake.Impulse(1.1f);
        GamepadRumble.Pulse(0.7f, 0.9f, 0.25f);
        FXSound.Play("Slam", 0.7f, 1f);
        FXSound.Play("SkyLand", 0.9f, 1f); // meteor impact
        Vector3 ground = new Vector3(land.x, land.y - feet, 0f);
        GroundShock.Spawn(ground, 3.2f, new Color(1f, 0.85f, 0.95f), new Color(0.85f, 0.75f, 0.65f));
        PulseRing.Spawn(ground + Vector3.up * 0.2f, new Color(1f, 0.6f, 0.9f, 0.9f), 1.6f, 0.45f, 90, true);
        FXParticle.Burst(ground + Vector3.up * 0.05f, new Color(0.85f, 0.75f, 0.65f), 24, 1.5f, 4.5f, 8f, 0.7f, true);
        FXParticle.Burst(ground + Vector3.up * 0.3f, Color.white, 10, 2f, 5f, 3f, 0.3f);

        // Squash and stretch back (keeps his facing sign)
        Vector3 baseScale = body.localScale;
        for (float t = 0f; t < 1f; t += Time.unscaledDeltaTime / 0.3f)
        {
            float s = Mathf.Sin(t * Mathf.PI * 2f) * (1f - t) * 0.3f;
            body.localScale = new Vector3(baseScale.x * (1f + s), baseScale.y * (1f - s), baseScale.z);
            yield return null;
        }
        body.localScale = baseScale;

        if (cam != null && cam.Follow == anchor) cam.Follow = oldFollow;
        Destroy(anchor.gameObject);
        if (move != null) move.enabled = true;
        Running = false;
        Destroy(gameObject);
    }

    // Falling whistle: a sine sliding down
    private static AudioClip whistle;
    private static AudioClip Whistle()
    {
        if (whistle != null) return whistle;
        const int rate = 44100;
        int length = (int)(rate * FallTime);
        var data = new float[length];
        float phase = 0f;
        for (int i = 0; i < length; i++)
        {
            float t = i / (float)length;
            float freq = Mathf.Lerp(1900f, 500f, t * t);
            phase += 2f * Mathf.PI * freq / rate;
            float env = Mathf.Clamp01(t * 8f) * Mathf.Clamp01((1f - t) * 12f);
            data[i] = Mathf.Sin(phase) * 0.35f * env;
        }
        whistle = AudioClip.Create("FirstDropWhistle", length, 1, rate, false);
        whistle.SetData(data, 0);
        return whistle;
    }
}
