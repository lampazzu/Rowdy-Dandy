using UnityEngine;

public enum WeaponType { Sword, Axe, Naginata, Cleaver }

public class WeaponDrop : MonoBehaviour
{
    [Header("Drop Options")]
    [SerializeField] public WeaponType weaponType;
    [SerializeField] private float maxDurability = 50f;

    [Header("Highlight / Pop Effect Settings")]
    [SerializeField] private float scaleMultiplier = 1.3f;        // How big it gets when player is close
    [SerializeField] private float animationSpeed = 8f;          // How fast it scales up/down
    [SerializeField] private Color purpleGlowColor = new Color(0.75f, 0.3f, 1f, 1f); // Bright purple color

    private bool playerIsClose = false;
    private Vector3 originalScale;
    private Color originalColor;
    private SpriteRenderer spriteRenderer;

    private void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        if (spriteRenderer == null)
        {
            spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        }

        originalScale = transform.localScale;

        if (spriteRenderer != null)
        {
            originalColor = spriteRenderer.color;
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            playerIsClose = true;
        }
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            playerIsClose = false;
        }
    }

    private void Update()
    {
        // --- POP SCALE & PURPLE GLOW EFFECT ---
        Vector3 targetScale = playerIsClose ? originalScale * scaleMultiplier : originalScale;
        transform.localScale = Vector3.Lerp(transform.localScale, targetScale, Time.deltaTime * animationSpeed);

        if (spriteRenderer != null)
        {
            Color targetColor = playerIsClose ? purpleGlowColor : originalColor;
            spriteRenderer.color = Color.Lerp(spriteRenderer.color, targetColor, Time.deltaTime * animationSpeed);
        }

        // --- PICKUP INPUT ---
        // Press E on Keyboard OR Triangle/Y on Gamepad
        if (playerIsClose && (Input.GetKeyDown(KeyCode.E) || Input.GetKeyDown(KeyCode.JoystickButton3)))
        {
            WeaponManager wm = FindFirstObjectByType<WeaponManager>();
            if (wm != null)
            {
                wm.PickupWeapon(weaponType, maxDurability);
                Destroy(gameObject);
            }
        }
    }
}