using UnityEditor;

namespace JoinDog.Editor
{
    internal sealed class WorldMapArtImporter : AssetPostprocessor
    {
        private const string WorldArtRoot = "Assets/_JoinDog/Resources/Worlds/";

        private void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith(WorldArtRoot, System.StringComparison.Ordinal))
                return;

            TextureImporter importer = (TextureImporter)assetImporter;
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.wrapMode = UnityEngine.TextureWrapMode.Clamp;
            importer.filterMode = UnityEngine.FilterMode.Bilinear;

            TextureImporterPlatformSettings webGl = importer.GetPlatformTextureSettings("WebGL");
            webGl.overridden = true;
            webGl.maxTextureSize = 1024;
            webGl.format = TextureImporterFormat.Automatic;
            webGl.textureCompression = TextureImporterCompression.Compressed;
            webGl.crunchedCompression = true;
            webGl.compressionQuality = 62;
            importer.SetPlatformTextureSettings(webGl);
        }
    }
}
