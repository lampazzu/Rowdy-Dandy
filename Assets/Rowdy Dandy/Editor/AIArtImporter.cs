using UnityEditor;
using UnityEngine;

// New images in Assets/Resources/AI Placeholders/ (exported placeholders, or your own redraws dropped in) import as crisp
// pixel art: sprite, point filter, no compression, no mipmaps, 64 pixels per unit. Only on their first import, so
// anything you change by hand in the Inspector afterwards stays.
public class AIArtImporter : AssetPostprocessor
{
    private void OnPreprocessTexture()
    {
        if (!assetPath.Replace('\\', '/').Contains("/AI Placeholders/") || !assetImporter.importSettingsMissing) return;
        var ti = (TextureImporter)assetImporter;
        ti.textureType = TextureImporterType.Sprite;
        ti.spriteImportMode = SpriteImportMode.Single;
        ti.spritePixelsPerUnit = 64f;
        ti.filterMode = FilterMode.Point;
        ti.textureCompression = TextureImporterCompression.Uncompressed;
        ti.mipmapEnabled = false;
        ti.alphaIsTransparency = true;
        ti.npotScale = TextureImporterNPOTScale.None;
        ti.maxTextureSize = 4096;
    }
}
