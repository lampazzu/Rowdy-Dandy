using UnityEngine;

// Detects this body landing on, jumping out of, or sinking through water, and plays WaterSplashFX effects.
// Added automatically to Rowdy (PlayerMovement) and every enemy with EnemyMovement.
// Add it by hand to any object to splash too, or to tweak the settings for one character.
//
// Two ways water is detected, because the water tiles are solid for Rowdy but drownable enemies pass through them:
//  - Collisions with water (landing on / jumping off the surface)
//  - The body's feet crossing a water surface between physics steps (falling through / leaping out)
public class WaterSplashBody : MonoBehaviour
{
    [Tooltip("SoundManager sound for big entry splashes. Leave empty for none (Rowdy already plays EnterWater from On Enter Water).")]
    [SerializeField] private string splashSound = "EnterWater";
    [Tooltip("Multiplies the splash size for this body.")]
    [SerializeField] private float splashScale = 1f;
    [SerializeField] private float cooldown = 0.2f;
    [Tooltip("Entering the water always splashes at least this big (0 = only fast falls splash). Drownable enemies get 0.7.")]
    [Range(0f, 1f)] [SerializeField] private float minEntryIntensity = 0f;

    [Header("Jumping Out")]
    [SerializeField] private bool splashOnJumpOut = true;
    [SerializeField] private float jumpOutMinSpeed = 4f;

    [Header("Drowning")]
    [Tooltip("Second 'gulp' splash when the body goes fully under.")]
    [SerializeField] private bool gulpSplash = true;
    [SerializeField] private bool drowningBubbles = true;
    [SerializeField] private float bubbleDuration = 2.5f;

    private Rigidbody2D rb;
    private Collider2D bodyCollider;

    private bool hasPrevious;
    private Vector2 previousFoot;
    private float lastSplashTime = -10f;

    // Sinking-through-the-surface state
    private bool isSinking;
    private bool hasGulped;
    private bool hasStartedBubbles;
    private float sinkSurfaceY;
    private float sinkStartTime;
    private float sinkIntensity;
    private Collider2D sinkWater;

    // Adds the component with defaults unless one was already added by hand (then its Inspector settings win)
    public static WaterSplashBody AttachTo(GameObject target, bool player, bool drownable = false)
    {
        WaterSplashBody body = target.GetComponent<WaterSplashBody>();
        if (body == null)
        {
            body = target.AddComponent<WaterSplashBody>();
            if (player)
            {
                body.splashSound = "";
                body.gulpSplash = false;
                body.drowningBubbles = false;
            }
            if (drownable)
            {
                body.minEntryIntensity = 0.7f;
            }
        }
        return body;
    }

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        if (rb == null) rb = GetComponentInParent<Rigidbody2D>();
        bodyCollider = PickBodyCollider();
    }

    private void OnEnable()
    {
        hasPrevious = false;
        isSinking = false;
    }

    private void OnDisable()
    {
        WaterSplashFX fx = WaterSplashFX.Existing;
        if (fx != null) fx.StopBubbles(this);
    }

    // Prefer the main solid collider (not hitboxes/triggers)
    private Collider2D PickBodyCollider()
    {
        foreach (Collider2D col in GetComponents<Collider2D>())
        {
            if (!col.isTrigger) return col;
        }
        foreach (Collider2D col in GetComponentsInChildren<Collider2D>(true))
        {
            if (!col.isTrigger) return col;
        }
        return GetComponentInChildren<Collider2D>(true);
    }

    private Bounds GetBodyBounds()
    {
        if (bodyCollider != null && bodyCollider.enabled && bodyCollider.gameObject.activeInHierarchy)
        {
            return bodyCollider.bounds;
        }
        return new Bounds(transform.position, new Vector3(0.5f, 0.5f, 0f));
    }

    private float GetWidthScale(Bounds bounds)
    {
        return Mathf.Clamp(bounds.size.x / 0.75f, 0.6f, 2.5f) * splashScale;
    }

    // =========================================================================
    // FALLING THROUGH / LEAPING OUT OF THE SURFACE
    // =========================================================================

    private void FixedUpdate()
    {
        WaterSplashFX fx = WaterSplashFX.Instance;
        if (fx == null) return;

        Bounds bounds = GetBodyBounds();
        Vector2 foot = new Vector2(bounds.center.x, bounds.min.y);

        if (!hasPrevious)
        {
            previousFoot = foot;
            hasPrevious = true;
            return;
        }

        int waterMask = fx.WaterMask;
        float dy = foot.y - previousFoot.y;
        float velocityY = rb != null ? rb.linearVelocity.y : dy / Time.fixedDeltaTime;

        if (dy < -0.0001f)
        {
            // Moving down: did the feet cross the top of a water collider since last step?
            if (Physics2D.OverlapPoint(previousFoot, waterMask) == null)
            {
                RaycastHit2D hit = Physics2D.Linecast(previousFoot, foot, waterMask);
                if (hit.collider != null && hit.normal.y > 0.5f)
                {
                    float speed = Mathf.Max(-velocityY, -dy / Time.fixedDeltaTime);
                    TrySplash(new Vector2(foot.x, hit.point.y), speed, WaterSplashFX.SplashKind.Entry, hit.collider, bounds);
                    BeginSinking(hit.collider, hit.point.y, fx.GetIntensity(speed));
                }
            }
        }
        else if (dy > 0.0001f && splashOnJumpOut)
        {
            // Moving up: did the feet leave through the top of a water collider?
            if (Physics2D.OverlapPoint(foot, waterMask) == null)
            {
                RaycastHit2D hit = Physics2D.Linecast(foot, previousFoot, waterMask);
                if (hit.collider != null && hit.normal.y > 0.5f)
                {
                    float speed = Mathf.Max(velocityY, dy / Time.fixedDeltaTime);
                    TrySplash(new Vector2(foot.x, hit.point.y), speed, WaterSplashFX.SplashKind.Exit, hit.collider, bounds);
                    StopSinking();
                }
            }
        }

        UpdateSinking(fx, bounds);

        previousFoot = foot;
    }

    private void BeginSinking(Collider2D water, float surfaceY, float intensity)
    {
        isSinking = true;
        hasGulped = false;
        hasStartedBubbles = false;
        sinkWater = water;
        sinkSurfaceY = surfaceY;
        sinkStartTime = Time.time;
        sinkIntensity = intensity;
    }

    private void StopSinking()
    {
        if (!isSinking) return;
        isSinking = false;
        WaterSplashFX fx = WaterSplashFX.Existing;
        if (fx != null) fx.StopBubbles(this);
    }

    private void UpdateSinking(WaterSplashFX fx, Bounds bounds)
    {
        if (!isSinking) return;

        float elapsed = Time.time - sinkStartTime;

        // Only touched the surface and stayed on top (e.g. landed on solid water) -> not drowning
        if (bounds.center.y > sinkSurfaceY + 0.05f && elapsed > 0.3f)
        {
            StopSinking();
            return;
        }

        // Half under -> start the bubble trail
        if (!hasStartedBubbles && bounds.center.y < sinkSurfaceY)
        {
            hasStartedBubbles = true;
            if (drowningBubbles)
            {
                fx.StartBubbles(this, bodyCollider, bubbleDuration, GetWidthScale(bounds));
            }
        }

        // Fully under -> "gulp" splash closing over the body
        if (!hasGulped && bounds.max.y < sinkSurfaceY)
        {
            hasGulped = true;
            if (gulpSplash)
            {
                float gulpIntensity = Mathf.Max(0.25f, sinkIntensity * 0.6f);
                fx.PlaySplash(new Vector2(bounds.center.x, sinkSurfaceY), gulpIntensity, GetWidthScale(bounds), WaterSplashFX.SplashKind.Gulp, sinkWater);
            }
        }

        if (elapsed > bubbleDuration + 1f)
        {
            isSinking = false;
        }
    }

    // =========================================================================
    // LANDING ON / JUMPING OFF SOLID WATER (Rowdy, non-drownable enemies)
    // =========================================================================

    private void OnCollisionEnter2D(Collision2D collision)
    {
        WaterSplashFX fx = WaterSplashFX.Instance;
        if (fx == null || !fx.IsWater(collision.collider)) return;

        Bounds bounds = GetBodyBounds();
        float speed = Mathf.Abs(collision.relativeVelocity.y);
        float x = collision.contactCount > 0 ? collision.GetContact(0).point.x : bounds.center.x;
        float surfaceY = collision.collider.bounds.max.y;

        TrySplash(new Vector2(x, surfaceY), speed, WaterSplashFX.SplashKind.Entry, collision.collider, bounds);
    }

    private void OnCollisionExit2D(Collision2D collision)
    {
        if (!splashOnJumpOut || rb == null) return;

        WaterSplashFX fx = WaterSplashFX.Instance;
        if (fx == null || !fx.IsWater(collision.collider)) return;

        float velocityY = rb.linearVelocity.y;
        Bounds bounds = GetBodyBounds();
        TrySplash(new Vector2(bounds.center.x, collision.collider.bounds.max.y), velocityY, WaterSplashFX.SplashKind.Exit, collision.collider, bounds);
    }

    // =========================================================================

    private void TrySplash(Vector2 surfacePoint, float speed, WaterSplashFX.SplashKind kind, Collider2D water, Bounds bounds)
    {
        WaterSplashFX fx = WaterSplashFX.Instance;
        if (fx == null) return;

        bool forcedEntry = kind == WaterSplashFX.SplashKind.Entry && minEntryIntensity > 0f;
        float minSpeed = kind == WaterSplashFX.SplashKind.Exit ? jumpOutMinSpeed : fx.MinImpactSpeed;
        if (speed < minSpeed && !forcedEntry) return;
        if (Time.time - lastSplashTime < cooldown) return;
        lastSplashTime = Time.time;

        float intensity = fx.GetIntensity(speed);
        if (kind == WaterSplashFX.SplashKind.Exit) intensity *= 0.6f;
        if (forcedEntry) intensity = Mathf.Max(intensity, minEntryIntensity);

        fx.PlaySplash(surfacePoint, intensity, GetWidthScale(bounds), kind, water);

        if (kind == WaterSplashFX.SplashKind.Entry && !string.IsNullOrEmpty(splashSound) && SoundManager.Instance != null)
        {
            SoundManager.Instance.PlaySound(splashSound);
        }
    }
}
