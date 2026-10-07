using UnityEngine;
using UnityEngine.UI;

// Button badge on the weapon slot's corner, drawn like the HUD's own pink tags: "L1" (gamepad) or "Q" (keyboard).
// Only there when another weapon can be switched to; it blinks in when one becomes available, then breathes.
// Built at runtime by WeaponManager on the HUD's WeaponSlot (the parent of the weapon icon).
public class WeaponSwitchPrompt : MonoBehaviour
{
    private WeaponManager weapons;
    private RectTransform rect;
    private Image badge;
    private bool showingGamepad;
    private bool wasVisible;
    private float shownAt = -10f;

    public static void Attach(WeaponManager weapons, RectTransform weaponIcon)
    {
        if (weaponIcon == null || weaponIcon.parent == null) return;
        var go = new GameObject("Weapon Switch Badge", typeof(RectTransform), typeof(Image));
        go.transform.SetParent(weaponIcon.parent, false); // the slot frame
        var prompt = go.AddComponent<WeaponSwitchPrompt>();
        prompt.weapons = weapons;
        prompt.Build();
    }

    private void Build()
    {
        rect = (RectTransform)transform;
        rect.anchorMin = rect.anchorMax = new Vector2(1f, 0f); // bottom-right corner of the slot
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = new Vector2(-2f * HudTag.ArtScale, 1f * HudTag.ArtScale);
        badge = GetComponent<Image>();
        badge.raycastTarget = false;
        SetKey(LastInputDevice.UsingGamepad);
        badge.enabled = false;
    }

    private void SetKey(bool gamepad)
    {
        showingGamepad = gamepad;
        badge.sprite = HudTag.Make(gamepad ? "L1" : "Q");
        rect.sizeDelta = HudTag.UISize(badge.sprite);
    }

    private void Update()
    {
        // Gone, or left over from a recompile during Play mode (fields not built): remove
        if (weapons == null || badge == null || rect == null) { Destroy(gameObject); return; }

        bool gamepad = LastInputDevice.UsingGamepad;
        if (gamepad != showingGamepad) SetKey(gamepad);

        bool visible = weapons.NextWeaponProfile != null;
        if (visible && !wasVisible) shownAt = Time.unscaledTime;
        wasVisible = visible;
        badge.enabled = visible;
        if (!visible) return;

        // Pops and blinks three times when it appears
        float t = Time.unscaledTime - shownAt;
        bool blinkOff = t < 0.6f && Mathf.Repeat(t, 0.2f) > 0.1f;
        badge.color = blinkOff ? new Color(1f, 1f, 1f, 0.25f) : Color.white;
        float pop = t < 0.25f ? Mathf.Sin(t / 0.25f * Mathf.PI) * 0.3f : 0f;
        rect.localScale = Vector3.one * (1f + pop);
    }
}

// Which device Rowdy was last controlled with (for button prompts)
public static class LastInputDevice
{
    private static bool gamepad;
    private static int checkedFrame = -1;

    public static bool UsingGamepad
    {
        get
        {
            if (checkedFrame == Time.frameCount) return gamepad;
            checkedFrame = Time.frameCount;

            for (KeyCode k = KeyCode.JoystickButton0; k <= KeyCode.JoystickButton19; k++)
                if (Input.GetKeyDown(k)) { gamepad = true; return gamepad; }
            if (PadInput.L2Down) { gamepad = true; return gamepad; }
            if (Input.anyKeyDown) { gamepad = false; return gamepad; }
            float h = 0f, v = 0f;
            try
            {
                if (Input.GetAxisRaw("Mouse X") != 0f) { gamepad = false; return gamepad; }
                h = Input.GetAxisRaw("Horizontal");
                v = Input.GetAxisRaw("Vertical");
            }
            catch (System.ArgumentException) { }
            // stick / d-pad moved with no keyboard key held = gamepad
            if ((Mathf.Abs(h) > 0.5f || Mathf.Abs(v) > 0.5f) && !Input.anyKey) gamepad = true;
            return gamepad;
        }
    }
}
