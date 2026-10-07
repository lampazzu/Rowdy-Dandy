using System.Collections.Generic;
using UnityEngine;

// Anything with this pushes interactive grass / bushes / trees when it moves through them at ground level.
// Added automatically to Rowdy (PlayerMovement) and every enemy with EnemyMovement.
// Rowdy's attack hitboxes get one too, as "slashes": while an attack is active it whips the plants it cuts through.
public class VegetationInteractor : MonoBehaviour
{
    public static readonly List<VegetationInteractor> All = new List<VegetationInteractor>();

    [Tooltip("How hard this body pushes plants (Rowdy 1, enemies a bit less).")]
    [SerializeField] private float strength = 1f;
    [Tooltip("Attack hitbox: whips plants and throws leaves when it cuts through them.")]
    [SerializeField] private bool isSlash;
    [Tooltip("For slashes: the body that swings it (used to know where the ground is).")]
    [SerializeField] private VegetationInteractor owner;

    private Rigidbody2D rb;
    private Collider2D bodyCollider;
    private Vector2 lastPosition;
    private Vector2 fallbackVelocity;

    public float Strength => strength;
    public bool IsSlash => isSlash;
    public VegetationInteractor Owner => owner;
    public Bounds BodyBounds { get; private set; }
    public Vector2 Velocity => rb != null ? rb.linearVelocity : fallbackVelocity;
    public bool IsActive => bodyCollider == null || bodyCollider.enabled;

    public static VegetationInteractor AttachTo(GameObject target, float defaultStrength)
    {
        VegetationInteractor interactor = target.GetComponent<VegetationInteractor>();
        if (interactor == null)
        {
            interactor = target.AddComponent<VegetationInteractor>();
            interactor.strength = defaultStrength;
        }
        return interactor;
    }

    // Rowdy's attack hitboxes (objects with PlayerDamage under him) become slashes
    public static void AttachSlashes(GameObject body, VegetationInteractor bodyInteractor)
    {
        foreach (PlayerDamage hitbox in body.GetComponentsInChildren<PlayerDamage>(true))
        {
            if (hitbox.gameObject == body) continue;
            VegetationInteractor slash = AttachTo(hitbox.gameObject, 1.5f);
            slash.isSlash = true;
            slash.owner = bodyInteractor;
        }
    }

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        if (rb == null && isSlash) rb = GetComponentInParent<Rigidbody2D>();
        foreach (Collider2D col in GetComponents<Collider2D>())
        {
            if (!col.isTrigger || isSlash) { bodyCollider = col; break; }
        }
        if (bodyCollider == null) bodyCollider = GetComponentInChildren<Collider2D>();
    }

    private void OnEnable()
    {
        All.Add(this);
        lastPosition = transform.position;
        RefreshBounds();
    }

    private void OnDisable()
    {
        All.Remove(this);
    }

    private void Update()
    {
        RefreshBounds();
    }

    private void RefreshBounds()
    {
        // Slashes are flagged after AddComponent, so pick up the parent body here if Awake missed it
        if (isSlash && rb == null) rb = GetComponentInParent<Rigidbody2D>();

        Vector2 position = transform.position;
        if (Time.deltaTime > 0f) fallbackVelocity = (position - lastPosition) / Time.deltaTime;
        lastPosition = position;

        BodyBounds = bodyCollider != null && bodyCollider.enabled
            ? bodyCollider.bounds
            : new Bounds(position, new Vector3(0.5f, 1f, 0f));
    }
}
