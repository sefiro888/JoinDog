using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

namespace DogCrush.EditorTool
{
    public static class MagicLocalBuild
    {
        public static void Prepare()
        {
            AssetDatabase.Refresh();
            foreach(string guid in AssetDatabase.FindAssets("t:Texture2D",new[]{"Assets/_JoinDog/Resources/Magic"}))
            {
                var importer=(TextureImporter)AssetImporter.GetAtPath(AssetDatabase.GUIDToAssetPath(guid));
                importer.textureType=TextureImporterType.Sprite;
                importer.spriteImportMode=SpriteImportMode.Single;
                importer.alphaIsTransparency=true;
                importer.mipmapEnabled=false;
                importer.maxTextureSize=2048;
                importer.SaveAndReimport();
            }
            const string target="Assets/_JoinDog/Resources/Fonts/MagicRounded SDF.asset";
            var preparedFont=AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(target);
            if(preparedFont==null)
            {
                var source=AssetDatabase.LoadAssetAtPath<Font>("Assets/_JoinDog/Resources/Fonts/MagicRounded.ttf");
                var font=TMP_FontAsset.CreateFontAsset(source,90,12,GlyphRenderMode.SDFAA,2048,2048,AtlasPopulationMode.Dynamic);
                font.name="MagicRounded SDF";
                string chars="";
                for(int i=32;i<256;i++) chars+=(char)i;
                font.TryAddCharacters(chars+"×→←•",out string missing);
                if(!string.IsNullOrEmpty(missing)) Debug.LogWarning("Magic missing glyphs: "+missing);
                AssetDatabase.CreateAsset(font,target);
                AssetDatabase.AddObjectToAsset(font.material,font);
                foreach(var texture in font.atlasTextures) AssetDatabase.AddObjectToAsset(texture,font);
                EditorUtility.SetDirty(font);
                preparedFont=font;
            }
            preparedFont.atlasPopulationMode=AtlasPopulationMode.Dynamic;
            string glyphs="";
            for(int i=32;i<127;i++) glyphs+=(char)i;
            for(int i=160;i<256;i++) glyphs+=(char)i;
            preparedFont.TryAddCharacters(glyphs+"×→←•",out string missingGlyphs);
            preparedFont.atlasPopulationMode=AtlasPopulationMode.Static;
            EditorUtility.SetDirty(preparedFont);
            foreach(var atlas in preparedFont.atlasTextures) EditorUtility.SetDirty(atlas);
            AssetDatabase.SaveAssets();
        }
        public static void Build()
        {
            Prepare();
            DogCrushProjectSetup.BuildWebGLRelease();
        }
    }
}
