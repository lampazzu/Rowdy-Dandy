using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Bonfire rest: standing on a checkpoint, the interact button (Y / E) rests Rowdy. Same as dev key 1 (the level
// reloads at this checkpoint: full health, enemies + spawns reset) and the sun comes back up (DayNight morning).
// Cats stay. A button prompt bobs over the checkpoint while he's on it. Added by RespawnTrigger automatically.
public class CheckpointRest : MonoBehaviour
{
    public static bool Resting { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetSession() => Resting = false;

    private Collider2D area;
    private Health rowdy;
    private SpriteRenderer prompt;
    private float searchTimer;

    private void Awake() => area = GetComponent<Collider2D>();

    private bool RowdyHere()
    {
        if (rowdy == null)
        {
            searchTimer -= Time.unscaledDeltaTime;
            if (searchTimer > 0f) return false;
            searchTimer = 0.5f;
            rowdy = FindFirstObjectByType<Health>();
            if (rowdy == null) return false;
        }
        if (area == null || !area.enabled || rowdy.IsDead) return false;
        Bounds b = area.bounds;
        b.Expand(new Vector3(0.6f, 1f, 0f));
        b.extents = new Vector3(b.extents.x, b.extents.y, 1000f);
        return b.Contains(rowdy.transform.position);
    }

    // LateUpdate: weapon pickups / cat swaps / rats read the same button in Update and win (Interact.Use)
    private void LateUpdate()
    {
        bool here = !Resting && !FirstDrop.Running && !PauseMenu.IsPaused && RowdyHere();
        UpdatePrompt(here);
        if (here && Interact.Pressed && !Interact.UsedThisFrame)
        {
            Interact.Use();
            // carrying legendary rats: a small menu (rest / drop a rat as cat bait); otherwise rest right away
            if (CatRoster.Rats > 0) CheckpointMenu.Open(Rest, new Vector3(area.bounds.center.x, area.bounds.min.y, 0f));
            else Rest();
        }
    }

    private void Rest()
    {
        if (Resting || rowdy == null) return;
        Resting = true;
        RestRunner.Run(rowdy, area.bounds.center, area.bounds.max.y);
    }

    private void UpdatePrompt(bool show)
    {
        if (show && prompt == null)
        {
            var go = new GameObject("Rest Prompt");
            prompt = go.AddComponent<SpriteRenderer>();
            prompt.sortingLayerName = "Default";
            prompt.sortingOrder = 120;
            if (CatFX.Unlit != null) prompt.sharedMaterial = CatFX.Unlit;
        }
        if (prompt == null) return;
        prompt.enabled = show;
        if (!show) return;
        prompt.sprite = WeaponDrop.GetInteractPrompt();
        float bob = Mathf.Round(Mathf.Sin(Time.unscaledTime * 4f) * 2f) / 64f;
        prompt.transform.position = new Vector3(area.bounds.center.x, area.bounds.max.y + 0.45f + bob, 0f);
    }

    private void OnDestroy()
    {
        if (prompt != null) Destroy(prompt.gameObject);
    }

    // Lives across the reload: sparkle, fade out, reload at the checkpoint, fade back in
    private class RestRunner : MonoBehaviour
    {
        public static void Run(Health rowdy, Vector3 center, float top)
        {
            var go = new GameObject("Checkpoint Rest (auto)");
            DontDestroyOnLoad(go);
            go.AddComponent<RestRunner>().StartCoroutine(go.GetComponent<RestRunner>().Rest(rowdy, center, top));
        }

        private IEnumerator Rest(Health rowdy, Vector3 center, float top)
        {
            // Rowdy sits still (no damage, see Health.TakeDamage)
            if (rowdy != null)
            {
                if (rowdy.TryGetComponent(out PlayerMovement move)) move.enabled = false;
                if (rowdy.TryGetComponent(out Rigidbody2D body)) body.linearVelocity = new Vector2(0f, Mathf.Min(0f, body.linearVelocity.y));
                rowdy.AddHealth(rowdy.startingHealth);
            }
            Color gold = new Color(1f, 0.85f, 0.35f);
            FXSound.Play("Checkpoint", 0.8f, 0.85f);
            FXSound.Play("Heal", 0.7f, 1f);
            PulseRing.Spawn(center, gold, 1.6f, 0.6f);
            FXParticle.Burst(new Vector3(center.x, top, 0f), gold, 24, 1f, 3f, -1.5f, 1.2f, true);
            GamepadRumble.Pulse(0.2f, 0.3f, 0.2f);

            // Fade to a warm night-purple, then the level comes back in the morning
            Image fade = OverlayUI.MakeImage("Rest Fade", OverlayUI.Root, new Color(0.08f, 0.02f, 0.1f, 0f), OverlayUI.WhiteSprite);
            fade.rectTransform.anchorMin = Vector2.zero;
            fade.rectTransform.anchorMax = Vector2.one;
            fade.rectTransform.offsetMin = fade.rectTransform.offsetMax = Vector2.zero;
            fade.transform.SetAsLastSibling();
            PixelText text = PixelText.Create(fade.transform, "BEAUTY SLEEP...", 5, new Color(1f, 0.85f, 0.95f, 0f), 0.5f);
            text.Rect.anchorMin = text.Rect.anchorMax = new Vector2(0.5f, 0.5f);
            text.Rect.anchoredPosition = Vector2.zero;

            for (float t = 0f; t < 1f; t += Time.unscaledDeltaTime / 0.7f)
            {
                fade.color = new Color(fade.color.r, fade.color.g, fade.color.b, t);
                text.Color = new Color(1f, 0.85f, 0.95f, Mathf.Clamp01(t * 2f - 0.6f));
                yield return null;
            }
            fade.color = new Color(fade.color.r, fade.color.g, fade.color.b, 1f);
            text.Color = new Color(1f, 0.85f, 0.95f, 1f);

            // Same as dev key 1: reload at this checkpoint (where Rowdy stands)
            if (rowdy != null)
            {
                Vector2 at = rowdy.transform.position;
                PlayerPrefs.SetFloat("RespawnX", at.x);
                PlayerPrefs.SetFloat("RespawnY", at.y);
                PlayerPrefs.Save();
            }
            DayNight.ResetToMorningOnNextLoad();
            yield return new WaitForSecondsRealtime(0.5f);
            Scene scene = SceneManager.GetActiveScene();
            SceneManager.LoadScene(scene.buildIndex >= 0 ? scene.buildIndex : 0);
            yield return null;
            yield return null;

            for (float t = 0f; t < 1f; t += Time.unscaledDeltaTime / 0.9f)
            {
                fade.color = new Color(fade.color.r, fade.color.g, fade.color.b, 1f - t);
                text.Color = new Color(1f, 0.85f, 0.95f, 1f - t * 1.5f);
                yield return null;
            }
            Destroy(fade.gameObject);
            Resting = false;
            Destroy(gameObject);
        }
    }
}
