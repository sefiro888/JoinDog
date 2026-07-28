using UnityEditor;

namespace DogCrush.Editor
{
    /// <summary>
    /// Keeps the canonical mobile board import deterministic. The previous
    /// board assets carried stale sprite rectangles, which made Unity crop and
    /// stretch a different portion of the PNG depending on the cached import.
    /// </summary>
    public sealed class DogCrushVisualAssetImporter : AssetPostprocessor
    {
        private const string MobileBoardPath =
            "Assets/_DogCrush/Art/UI/board-frame-8x8-v2.png";
        private const string MobilePieceFolder =
            "Assets/_DogCrush/Resources/Pieces/";

        private void OnPreprocessTexture()
        {
            bool isMobileBoard = assetPath == MobileBoardPath;
            bool isMobilePiece = assetPath.StartsWith(MobilePieceFolder) &&
                                 assetPath.EndsWith("-v2.png");
            if (!isMobileBoard && !isMobilePiece)
            {
                return;
            }

            TextureImporter importer = (TextureImporter)assetImporter;
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = isMobileBoard ? 154f : 100f;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.wrapMode = UnityEngine.TextureWrapMode.Clamp;
            importer.filterMode = UnityEngine.FilterMode.Bilinear;
            importer.textureCompression = TextureImporterCompression.Compressed;
            importer.maxTextureSize = 2048;
        }
    }
}
