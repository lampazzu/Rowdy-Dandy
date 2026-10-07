using UnityEditor;
using UnityEngine;

// Creates Assets/Resources/Soundtrack.asset (the music sections) the first time this compiles, so the
// soundtrack works without any setup. Also: Tools > Rowdy Dandy > Soundtrack Sections (select / recreate).
[InitializeOnLoad]
public static class SoundtrackSetup
{
    private const string AssetPath = "Assets/Resources/Soundtrack.asset";
    private const string MainScene = "The great scene version 2";

    // Songs by GUID, so renaming / moving the files doesn't break this
    private const string FromWaveToGrave = "897e788f551fbb34c83d9a2e6c5014c9";
    private const string BizzareJungle = "eab88747a24d39c419a2fe4931332fc6";
    private const string PelichSong = "a4f28c05a95d7bf4a9728efc5b4364a1"; // EstouradinhaVersão2

    static SoundtrackSetup()
    {
        EditorApplication.delayCall += () =>
        {
            if (AssetDatabase.LoadAssetAtPath<SoundtrackConfig>(AssetPath) == null) Create();
        };
    }

    [MenuItem("Tools/Rowdy Dandy/Soundtrack Sections")]
    private static void SelectOrCreate()
    {
        SoundtrackConfig config = AssetDatabase.LoadAssetAtPath<SoundtrackConfig>(AssetPath);
        if (config == null) config = Create();
        Selection.activeObject = config;
        EditorGUIUtility.PingObject(config);
    }

    private static SoundtrackConfig Create()
    {
        var config = ScriptableObject.CreateInstance<SoundtrackConfig>();

        // Default split at checkpoints: beach start -> jungle from the checkpoint at x ~105 -> Pelich's corner
        // from just before the checkpoint next to him (x ~242). Adjust freely in the asset.
        config.sections.Add(new SoundtrackConfig.Section { name = "Beach (From Wave To Grave)", scene = MainScene, fromX = -100000f, toX = 104f, music = Load(FromWaveToGrave), volume = 1f });
        config.sections.Add(new SoundtrackConfig.Section { name = "Jungle (Bizzare Occurences)", scene = MainScene, fromX = 104f, toX = 236f, music = Load(BizzareJungle), volume = 1f });
        config.sections.Add(new SoundtrackConfig.Section { name = "Pelich", scene = MainScene, fromX = 236f, toX = 100000f, music = Load(PelichSong), volume = 1f });

        if (!AssetDatabase.IsValidFolder("Assets/Resources")) AssetDatabase.CreateFolder("Assets", "Resources");
        AssetDatabase.CreateAsset(config, AssetPath);
        AssetDatabase.SaveAssets();
        Debug.Log("Soundtrack sections created at " + AssetPath + " (edit the X ranges there).", config);
        return config;
    }

    private static AudioClip Load(string guid)
    {
        string path = AssetDatabase.GUIDToAssetPath(guid);
        return string.IsNullOrEmpty(path) ? null : AssetDatabase.LoadAssetAtPath<AudioClip>(path);
    }
}
