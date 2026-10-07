using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Tools > Rowdy Dandy > Organize HUD
// Rebuilds the in-game HUD of the open scene into a clean, consistent layout using your existing objects
// (so every script reference keeps working), plus the generated pixel-art labels/panels/icons and feedback components.
// Everything is one Undo step: Ctrl+Z puts it all back. Save the scene afterwards to keep it.
public static class HUDOrganizer
{
    private const string MainCanvasName = "UI / User Interface";
    private const string HealthCanvasName = "User Interface";
    private const string HudFolder = "Assets/Rowdy Dandy/HUD and UI/";
    private const string GeneratedFolder = "Assets/Rowdy Dandy/HUD and UI/Generated/";
    private const string ShadowMaterialPath = "Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF - Drop Shadow.mat";
    private const string UndoName = "Organize HUD";

    private const int S = 4; // every art pixel = 4 UI pixels at 1080p

    // Layout grid (UI pixels, from the top-left of the screen)
    private const float RowX = 152f;          // label column
    private const float LabelWidth = 15 * S;  // 60
    private const float BarX = LabelWidth + 4f;
    private const float BarWidth = 76 * S;    // 304
    private const float BarHeight = 8 * S;    // 32
    private const float HealthY = 32f;
    private const float ExpY = 72f;
    private const float WeaponNameY = 112f;
    private const float DurabilityY = 148f;

    private static readonly List<string> report = new List<string>();

    [MenuItem("Tools/Rowdy Dandy/Organize HUD")]
    public static void Organize()
    {
        report.Clear();
        Scene scene = SceneManager.GetActiveScene();
        List<Transform> all = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Transform>(true)).ToList();

        Transform mainCanvas = all.FirstOrDefault(t => t.name == MainCanvasName && t.GetComponent<Canvas>() != null);
        Transform healthCanvas = all.FirstOrDefault(t => t.name == HealthCanvasName && t.GetComponent<Canvas>() != null);

        if (mainCanvas == null)
        {
            EditorUtility.DisplayDialog("Organize HUD", $"Couldn't find the canvas '{MainCanvasName}' in the open scene ({scene.name}).", "OK");
            return;
        }
        if (mainCanvas.Find("HUD") != null)
        {
            EditorUtility.DisplayDialog("Organize HUD", "This HUD is already organized (a 'HUD' object exists under the canvas).\nUndo it or delete 'HUD' first if you want to run it again.", "OK");
            return;
        }

        // ---------- assets ----------
        AssetDatabase.Refresh();
        Sprite frameSprite = LoadSprite(HudFolder + "GradeHealthUI.png");
        Sprite backSprite = LoadSprite(HudFolder + "SubBlueHealthUI.png");
        Sprite greenFill = LoadSprite(GeneratedFolder + "BarFill_Green.png");
        Sprite goldFill = LoadSprite(GeneratedFolder + "BarFill_Gold.png");
        Sprite labelHP = LoadSprite(GeneratedFolder + "Label_HP.png");
        Sprite labelXP = LoadSprite(GeneratedFolder + "Label_XP.png");
        Sprite labelDUR = LoadSprite(GeneratedFolder + "Label_DUR.png");
        Sprite labelStats = LoadSprite(GeneratedFolder + "Label_STATS.png");
        Sprite panelSprite = LoadSprite(GeneratedFolder + "Panel_9Slice.png");
        Sprite iconRod = LoadSprite(GeneratedFolder + "WeaponIcon_Rod.png");
        Sprite iconSword = LoadSprite(GeneratedFolder + "WeaponIcon_Sword.png");
        Sprite iconNaginata = LoadSprite(GeneratedFolder + "WeaponIcon_Naginata.png");
        Sprite iconCleaver = LoadSprite(GeneratedFolder + "WeaponIcon_Cleaver.png");
        Material shadowMaterial = AssetDatabase.LoadAssetAtPath<Material>(ShadowMaterialPath);

        if (frameSprite == null || backSprite == null || greenFill == null || panelSprite == null || labelHP == null)
        {
            EditorUtility.DisplayDialog("Organize HUD", "Some HUD sprites are missing. Make sure 'Assets/Rowdy Dandy/HUD and UI/Generated' finished importing, then try again.", "OK");
            return;
        }

        // ---------- find the existing HUD pieces ----------
        Image expBack = FindChild<Image>(mainCanvas, "Exp");
        TMP_Text levelText = FindChild<TMP_Text>(mainCanvas, "Exp");
        Image expFill = FindChild<Image>(mainCanvas, "ExpBar");
        Image durBack = FindChild<Image>(mainCanvas, "DurabilityBarImage (1)");
        Image durFill = FindChild<Image>(mainCanvas, "DurabilityBarImage");
        TMP_Text weaponText = FindChild<TMP_Text>(mainCanvas, "Weapon Type");
        TMP_Text damageText = FindChild<TMP_Text>(mainCanvas, "Damage");
        TMP_Text critChanceText = FindChild<TMP_Text>(mainCanvas, "Crit Chance");
        TMP_Text critDamageText = FindChild<TMP_Text>(mainCanvas, "Crti dmg");

        Transform oldHealthBar = mainCanvas.Cast<Transform>().FirstOrDefault(t => t.name == "HealthBar");
        Image portrait = oldHealthBar != null
            ? oldHealthBar.GetComponentsInChildren<Image>(true).FirstOrDefault(i => i.sprite != null && i.sprite.name.StartsWith("RDR_HUD"))
            : null;

        Transform liveHealthBar = healthCanvas != null ? healthCanvas.Cast<Transform>().FirstOrDefault(t => t.name == "HealthBar") : null;
        Image healthBack = liveHealthBar != null ? FindChild<Image>(liveHealthBar, "SubHealth") : null;
        Image healthFill = liveHealthBar != null ? FindChild<Image>(liveHealthBar, "Health") : null;
        Image healthFrame = liveHealthBar != null ? FindChild<Image>(liveHealthBar, "Front") : null;

        Undo.IncrementCurrentGroup();
        int undoGroup = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName(UndoName);

        // ---------- canvas scaling: same look on every resolution ----------
        CanvasScaler scaler = mainCanvas.GetComponent<CanvasScaler>();
        float referencePPU = 100f;
        if (scaler != null)
        {
            Undo.RecordObject(scaler, UndoName);
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 1f;
            referencePPU = scaler.referencePixelsPerUnit;
            report.Add("Canvas now scales with screen size (1920x1080 reference).");
        }
        float slicedMultiplier = referencePPU / (panelSprite.pixelsPerUnit * S);

        // ---------- structure ----------
        RectTransform hud = CreateUI("HUD", mainCanvas);
        Stretch(hud);

        RectTransform playerPanel = CreateUI("PlayerPanel", hud);
        Place(playerPanel, 0f, 0f, 760f, 232f);
        UIShaker shaker = playerPanel.gameObject.AddComponent<UIShaker>();

        // Portrait (Rowdy's face), head lined up with the 24px screen margin
        if (portrait != null)
        {
            Reparent(portrait.transform, playerPanel);
            Rename(portrait, "Portrait");
            Place(portrait.rectTransform, 24f - 14 * S, 16f, 106 * S, 40 * S);
            SetupImage(portrait, portrait.sprite, Color.white, true);
        }
        else report.Add("WARNING: portrait (RDR_HUD) not found.");

        // Weapon slot under the portrait
        RectTransform weaponSlot = CreateUI("WeaponSlot", playerPanel);
        Place(weaponSlot, 40f, 120f, 22 * S, 22 * S);
        Image slotImage = AddImage(weaponSlot, panelSprite, Color.white);
        slotImage.type = Image.Type.Sliced;
        slotImage.pixelsPerUnitMultiplier = slicedMultiplier;
        RectTransform iconRect = CreateUI("WeaponIcon", weaponSlot);
        Center(iconRect, 16 * S, 16 * S);
        Image weaponIcon = AddImage(iconRect, iconNaginata, Color.white);
        weaponIcon.preserveAspect = true;

        // Health row (moves the live health bar, with its HealthBar script, out of the second canvas)
        Image healthFrameImage = null;
        RectTransform healthRow = CreateRow("HealthRow", playerPanel, HealthY, labelHP, out _);
        if (liveHealthBar != null && healthFill != null)
        {
            Reparent(liveHealthBar, healthRow);
            Place((RectTransform)liveHealthBar, BarX, 0f, BarWidth, BarHeight);
            if (healthBack != null) { Stretch(healthBack.rectTransform); SetupImage(healthBack, backSprite, Color.white, false); }
            Stretch(healthFill.rectTransform); SetupFill(healthFill, healthFill.sprite);
            if (healthFrame != null) { Stretch(healthFrame.rectTransform); SetupImage(healthFrame, frameSprite, Color.white, false); healthFrameImage = healthFrame; }
            report.Add("Health bar moved into the HUD (HealthBar script and references kept).");
        }
        else report.Add("WARNING: live health bar (User Interface/HealthBar) not found.");

        // EXP row; its label plate shows the level ("LV3")
        RectTransform expRow = CreateRow("ExpRow", playerPanel, ExpY, labelXP, out Image expLabel);
        if (expFill != null)
        {
            BuildBar(expRow, expBack, expFill, greenFill, backSprite, frameSprite);
        }
        else report.Add("WARNING: ExpBar not found.");
        if (levelText != null)
        {
            Reparent(levelText.transform, playerPanel);
            MoveLevelIntoLabel(expLabel, levelText);
        }

        // Weapon name + durability row
        if (weaponText != null)
        {
            Reparent(weaponText.transform, playerPanel);
            Rename(weaponText, "WeaponName");
            Place(weaponText.rectTransform, RowX, WeaponNameY, BarX + BarWidth, BarHeight);
            StyleText(weaponText, 24f, Color.white, TextAlignmentOptions.MidlineLeft, shadowMaterial);
        }
        RectTransform durRow = CreateRow("DurabilityRow", playerPanel, DurabilityY, labelDUR, out Image durLabel);
        Image durFrame = null;
        if (durFill != null)
        {
            durFrame = BuildBar(durRow, durBack, durFill, goldFill, backSprite, frameSprite);
            UIActiveMirror mirror = durRow.gameObject.AddComponent<UIActiveMirror>();
            SetRef(mirror, "source", durFill.gameObject);
            var targets = new List<Object> { durLabel.gameObject };
            if (durBack != null) targets.Add(durBack.gameObject);
            if (durFrame != null) targets.Add(durFrame.gameObject);
            SetRefArray(mirror, "targets", targets);
        }
        else report.Add("WARNING: DurabilityBarImage not found.");

        // Stats panel (PlayerStats shows/hides it with C)
        RectTransform statsPanel = CreateUI("StatsPanel", hud);
        Place(statsPanel, 24f, 248f, 100 * S, 43 * S);
        Image statsBackground = AddImage(statsPanel, panelSprite, Color.white);
        statsBackground.type = Image.Type.Sliced;
        statsBackground.pixelsPerUnitMultiplier = slicedMultiplier;
        RectTransform statsTitle = CreateUI("Title", statsPanel);
        Place(statsTitle, 20f, 16f, 23 * S, 8 * S);
        AddImage(statsTitle, labelStats, Color.white);
        float statY = 60f;
        foreach (TMP_Text stat in new[] { damageText, critChanceText, critDamageText })
        {
            if (stat == null) continue;
            Reparent(stat.transform, statsPanel);
            Place(stat.rectTransform, 24f, statY, 352f, 30f);
            StyleText(stat, 20f, Color.white, TextAlignmentOptions.MidlineLeft, shadowMaterial);
            statY += 32f;
        }

        // ---------- hook up scripts on Rowdy ----------
        int statsHooked = 0;
        foreach (PlayerStats stats in Object.FindObjectsByType<PlayerStats>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (SetRef(stats, "statsPanel", statsPanel.gameObject)) statsHooked++;
        }
        report.Add(statsHooked > 0 ? "Stats panel assigned to PlayerStats (press C in game to show/hide)." : "WARNING: no PlayerStats found to assign the stats panel.");

        int weaponsHooked = 0;
        foreach (WeaponManager weapons in Object.FindObjectsByType<WeaponManager>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            SetRef(weapons, "weaponProfileImage", weaponIcon);
            SetRef(weapons, "rodProfile", iconRod);
            SetRef(weapons, "swordProfile", iconSword);
            SetRef(weapons, "naginataProfile", iconNaginata);
            SetRef(weapons, "cleaverProfile", iconCleaver);
            weaponsHooked++;
        }
        report.Add(weaponsHooked > 0 ? "Weapon icons assigned to WeaponManager." : "WARNING: no WeaponManager found for the weapon icons.");

        // ---------- feedback / juice ----------
        if (healthFill != null)
        {
            HUDBarFeedback hp = Undo.AddComponent<HUDBarFeedback>(healthFill.gameObject);
            SetRef(hp, "shakeOnLoss", shaker);
            SetFloat(hp, "lowThreshold", 0.25f);
            SetFloat(hp, "punchScale", 0.08f);
        }
        if (expFill != null)
        {
            HUDBarFeedback xp = Undo.AddComponent<HUDBarFeedback>(expFill.gameObject);
            SetBool(xp, "treatBigDropAsLevelUp", true);
            SetFloat(xp, "punchScale", 0.05f);
            SetFloat(xp, "fillSpeed", 0.8f);
        }
        if (durFill != null)
        {
            HUDBarFeedback dur = Undo.AddComponent<HUDBarFeedback>(durFill.gameObject);
            SetFloat(dur, "lowThreshold", 0.25f);
            SetFloat(dur, "punchScale", 0.04f);
            SetColor(dur, "lossGhostColor", new Color(1f, 0.95f, 0.7f, 1f));
        }
        foreach (TMP_Text stat in new[] { damageText, critChanceText, critDamageText })
        {
            if (stat != null) Undo.AddComponent<HUDTextFeedback>(stat.gameObject);
        }
        report.Add("Feedback added: bar ghost/flash/punch, low HP & durability pulse, HUD shake on damage, level-up burst, text pops.");

        // ---------- retire the leftovers ----------
        if (oldHealthBar != null)
        {
            Undo.RecordObject(oldHealthBar.gameObject, UndoName);
            oldHealthBar.name = "HealthBar (old, unused)";
            oldHealthBar.gameObject.SetActive(false);
        }
        if (healthCanvas != null && healthCanvas.Cast<Transform>().All(t => !t.gameObject.activeSelf))
        {
            Undo.RecordObject(healthCanvas.gameObject, UndoName);
            healthCanvas.name = "User Interface (merged into HUD)";
            healthCanvas.gameObject.SetActive(false);
            report.Add("Second canvas 'User Interface' is now empty and was disabled.");
        }

        Undo.CollapseUndoOperations(undoGroup);
        EditorSceneManager.MarkSceneDirty(scene);
        Selection.activeGameObject = hud.gameObject;

        string summary = "HUD organized. Ctrl+Z undoes everything. Save the scene to keep it.\n\n- " + string.Join("\n- ", report);
        Debug.Log(summary);
        EditorUtility.DisplayDialog("Organize HUD", summary, "Nice");
    }

    [MenuItem("Tools/Rowdy Dandy/Organize HUD", true)]
    private static bool ValidateOrganize() => !EditorApplication.isPlaying;

    // For a HUD organized before the level moved into the XP label
    [MenuItem("Tools/Rowdy Dandy/HUD - Level Into XP Label")]
    public static void LevelIntoXpLabel()
    {
        Scene scene = SceneManager.GetActiveScene();
        Transform hud = scene.GetRootGameObjects()
            .SelectMany(r => r.GetComponentsInChildren<Transform>(true))
            .FirstOrDefault(t => t.name == "HUD" && t.parent != null && t.parent.name == MainCanvasName);
        Transform panel = hud != null ? hud.Find("PlayerPanel") : null;
        Transform label = panel != null ? panel.Find("ExpRow/Label") : null;
        TMP_Text levelText = panel != null
            ? panel.GetComponentsInChildren<TMP_Text>(true).FirstOrDefault(t => t.name.StartsWith("LevelText"))
            : null;

        if (label == null || levelText == null || !label.TryGetComponent(out Image labelImage))
        {
            EditorUtility.DisplayDialog("HUD", "Couldn't find HUD/PlayerPanel/ExpRow/Label and the LevelText. Run Organize HUD first.", "OK");
            return;
        }
        if (label.GetComponent<PixelLevelLabel>() != null)
        {
            EditorUtility.DisplayDialog("HUD", "The level is already in the XP label.", "OK");
            return;
        }

        Undo.IncrementCurrentGroup();
        int group = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Level Into XP Label");
        MoveLevelIntoLabel(labelImage, levelText);
        Undo.CollapseUndoOperations(group);
        EditorSceneManager.MarkSceneDirty(scene);
        Selection.activeGameObject = label.gameObject;
        Debug.Log("Level now shows in the XP label (\"LV3\") in Play mode. Ctrl+Z to undo; save the scene to keep it.");
    }

    [MenuItem("Tools/Rowdy Dandy/HUD - Level Into XP Label", true)]
    private static bool ValidateLevelIntoXpLabel() => !EditorApplication.isPlaying;

    // The XP label plate draws the level in pixel letters; PlayerStats' level text stays as the hidden source
    private static void MoveLevelIntoLabel(Image label, TMP_Text levelText)
    {
        PixelLevelLabel pixelLabel = Undo.AddComponent<PixelLevelLabel>(label.gameObject);
        SetRef(pixelLabel, "source", levelText);

        Rename(levelText, "LevelText (hidden source)");
        Undo.RecordObject(levelText, UndoName);
        levelText.enabled = false;

        HUDTextFeedback oldPop = levelText.GetComponent<HUDTextFeedback>();
        if (oldPop != null) Undo.DestroyObjectImmediate(oldPop);
    }

    // =========================================================================
    // BUILDING BLOCKS
    // =========================================================================

    // [label][bar] row; the bar itself is added by BuildBar / the health bar
    private static RectTransform CreateRow(string rowName, Transform parent, float y, Sprite label, out Image labelImage)
    {
        RectTransform row = CreateUI(rowName, parent);
        Place(row, RowX, y, BarX + BarWidth, BarHeight);
        RectTransform labelRect = CreateUI("Label", row);
        Place(labelRect, 0f, 0f, LabelWidth, BarHeight);
        labelImage = AddImage(labelRect, label, Color.white);
        return row;
    }

    // Same design for every bar: dark back, colored fill, purple frame on top. Returns the frame.
    private static Image BuildBar(RectTransform row, Image back, Image fill, Sprite fillSprite, Sprite backSprite, Sprite frameSprite)
    {
        RectTransform bar = CreateUI("Bar", row);
        Place(bar, BarX, 0f, BarWidth, BarHeight);

        if (back != null)
        {
            Reparent(back.transform, bar);
            Rename(back, "Back");
            Stretch(back.rectTransform);
            SetupImage(back, backSprite, Color.white, false);
        }

        Reparent(fill.transform, bar);
        Stretch(fill.rectTransform);
        SetupFill(fill, fillSprite);

        RectTransform frameRect = CreateUI("Frame", bar);
        Stretch(frameRect);
        return AddImage(frameRect, frameSprite, Color.white);
    }

    private static RectTransform CreateUI(string objectName, Transform parent)
    {
        var go = new GameObject(objectName, typeof(RectTransform));
        Undo.RegisterCreatedObjectUndo(go, UndoName);
        go.layer = parent.gameObject.layer;
        go.transform.SetParent(parent, false);
        return (RectTransform)go.transform;
    }

    private static Image AddImage(RectTransform rect, Sprite sprite, Color color)
    {
        Image image = rect.gameObject.AddComponent<Image>();
        image.sprite = sprite;
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    private static void Rename(Component component, string newName)
    {
        Undo.RecordObject(component.gameObject, UndoName);
        component.gameObject.name = newName;
    }

    private static void Reparent(Transform child, Transform parent)
    {
        Undo.SetTransformParent(child, parent, UndoName);
        child.SetAsLastSibling();
    }

    // Top-left anchored rect, y measured downward
    private static void Place(RectTransform rect, float x, float y, float width, float height)
    {
        Undo.RecordObject(rect, UndoName);
        rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = new Vector2(x, -y);
        rect.sizeDelta = new Vector2(width, height);
        ResetTransform(rect);
    }

    private static void Stretch(RectTransform rect)
    {
        Undo.RecordObject(rect, UndoName);
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        ResetTransform(rect);
    }

    private static void Center(RectTransform rect, float width, float height)
    {
        Undo.RecordObject(rect, UndoName);
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = new Vector2(width, height);
        ResetTransform(rect);
    }

    private static void ResetTransform(RectTransform rect)
    {
        rect.localScale = Vector3.one;
        rect.localRotation = Quaternion.identity;
        Vector3 p = rect.localPosition;
        rect.localPosition = new Vector3(p.x, p.y, 0f);
    }

    private static void SetupImage(Image image, Sprite sprite, Color color, bool preserveAspect)
    {
        Undo.RecordObject(image, UndoName);
        image.sprite = sprite;
        image.color = color;
        image.type = Image.Type.Simple;
        image.preserveAspect = preserveAspect;
        image.raycastTarget = false;
    }

    private static void SetupFill(Image image, Sprite sprite)
    {
        Undo.RecordObject(image, UndoName);
        image.sprite = sprite;
        image.color = Color.white; // color now comes from the sprite itself
        image.type = Image.Type.Filled;
        image.fillMethod = Image.FillMethod.Horizontal;
        image.fillOrigin = (int)Image.OriginHorizontal.Left;
        image.preserveAspect = false;
        image.raycastTarget = false;
    }

    private static void StyleText(TMP_Text text, float size, Color color, TextAlignmentOptions alignment, Material material)
    {
        Undo.RecordObject(text, UndoName);
        text.enableAutoSizing = false;
        text.fontSize = size;
        text.fontStyle = FontStyles.Bold;
        text.alignment = alignment;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.overflowMode = TextOverflowModes.Overflow;
        text.color = color;
        text.raycastTarget = false;
        if (material != null) text.fontSharedMaterial = material;
    }

    // =========================================================================
    // HELPERS
    // =========================================================================

    private static Sprite LoadSprite(string path)
    {
        Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        if (sprite == null) report.Add("WARNING: missing sprite " + path);
        return sprite;
    }

    // Direct child with this name that has component T (names repeat across the HUD, e.g. two "Exp" objects)
    private static T FindChild<T>(Transform parent, string childName) where T : Component
    {
        foreach (Transform child in parent)
        {
            if (child.name == childName && child.TryGetComponent(out T component)) return component;
        }
        return null;
    }

    private static bool SetRef(Object target, string property, Object value)
    {
        var so = new SerializedObject(target);
        SerializedProperty prop = so.FindProperty(property);
        if (prop == null) return false;
        prop.objectReferenceValue = value;
        so.ApplyModifiedProperties();
        return true;
    }

    private static void SetRefArray(Object target, string property, List<Object> values)
    {
        var so = new SerializedObject(target);
        SerializedProperty prop = so.FindProperty(property);
        if (prop == null) return;
        prop.arraySize = values.Count;
        for (int i = 0; i < values.Count; i++) prop.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
        so.ApplyModifiedProperties();
    }

    private static void SetFloat(Object target, string property, float value)
    {
        var so = new SerializedObject(target);
        SerializedProperty prop = so.FindProperty(property);
        if (prop == null) return;
        prop.floatValue = value;
        so.ApplyModifiedProperties();
    }

    private static void SetBool(Object target, string property, bool value)
    {
        var so = new SerializedObject(target);
        SerializedProperty prop = so.FindProperty(property);
        if (prop == null) return;
        prop.boolValue = value;
        so.ApplyModifiedProperties();
    }

    private static void SetColor(Object target, string property, Color value)
    {
        var so = new SerializedObject(target);
        SerializedProperty prop = so.FindProperty(property);
        if (prop == null) return;
        prop.colorValue = value;
        so.ApplyModifiedProperties();
    }
}
