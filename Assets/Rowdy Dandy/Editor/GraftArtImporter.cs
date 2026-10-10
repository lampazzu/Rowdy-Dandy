using UnityEditor;
using UnityEngine;

// Sheets in Assets/Resources/Graft/ (frames cut from the Graft GIFs, side by side, see GraftFX) and the patron
// portraits import as crisp pixel art: point filter, no compression, no mipmaps, no power-of-two resize, big max size
// (a few strips are almost 4000 px wide). Only on their first import, so later Inspector changes stay.
public class GraftArtImporter : AssetPostprocessor
{
    private void OnPreprocessTexture()
    {
        string path = assetPath.Replace('\\', '/');
        if (!path.Contains("/Resources/Graft/") && !path.Contains("/Resources/BoonPortraits/") && !path.Contains("/Resources/CatSkins/")) return;
        if (!assetImporter.importSettingsMissing) return;
        var ti = (TextureImporter)assetImporter;
        ti.textureType = TextureImporterType.Sprite;
        ti.spriteImportMode = SpriteImportMode.Single;
        ti.spritePixelsPerUnit = 64f;
        ti.filterMode = FilterMode.Point;
        ti.textureCompression = TextureImporterCompression.Uncompressed;
        ti.mipmapEnabled = false;
        ti.alphaIsTransparency = true;
        ti.npotScale = TextureImporterNPOTScale.None;
        ti.maxTextureSize = 8192;
    }
}
