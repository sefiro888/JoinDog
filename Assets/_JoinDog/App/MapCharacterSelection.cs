using UnityEngine;

namespace JoinDog.App
{
    /// <summary>
    /// Central catalogue for map characters. Adding another dog only requires
    /// one catalogue entry and its sprite under Resources/Characters.
    /// </summary>
    public static class MapCharacterSelection
    {
        public readonly struct Character
        {
            public Character(string id, string displayName, string resourcePath)
            {
                Id = id;
                DisplayName = displayName;
                ResourcePath = resourcePath;
            }

            public string Id { get; }
            public string DisplayName { get; }
            public string ResourcePath { get; }
        }

        private const string PreferenceKey = "JoinDog_SelectedMapCharacter";
        private const string DefaultCharacterId = "yorkshire";

        public static readonly Character[] Characters =
        {
            new Character("yorkshire", "YORKSHIRE", "Characters/yorkshire-map-character-v1"),
            new Character("pitbull", "PITBULL", "Characters/pitbull-map-character-v1")
        };

        public static string SelectedId => PlayerPrefs.GetString(PreferenceKey, DefaultCharacterId);

        public static Character Selected
        {
            get
            {
                string id = SelectedId;
                foreach (Character character in Characters)
                    if (character.Id == id) return character;
                return Characters[0];
            }
        }

        public static void Select(string id)
        {
            if(id=="local-photo" && PetPhotoImport.HasPhoto)
            {
                PlayerPrefs.SetString(PreferenceKey,id);PlayerPrefs.Save();return;
            }
            foreach (Character character in Characters)
            {
                if (character.Id != id) continue;
                PlayerPrefs.SetString(PreferenceKey, id);
                PlayerPrefs.Save();
                return;
            }
        }

        public static Sprite LoadSelectedSprite(Sprite fallback = null)
        {
            if(SelectedId=="local-photo") return PetPhotoImport.Load(fallback);
            return LoadSprite(Selected.ResourcePath, fallback);
        }

        public static Sprite LoadSprite(Character character, Sprite fallback = null)
        {
            return LoadSprite(character.ResourcePath, fallback);
        }

        private static Sprite LoadSprite(string resourcePath, Sprite fallback)
        {
            Sprite sprite = Resources.Load<Sprite>(resourcePath);
            if (sprite == null)
            {
                Sprite[] sprites = Resources.LoadAll<Sprite>(resourcePath);
                if (sprites != null && sprites.Length > 0) sprite = sprites[0];
            }

            if (sprite != null) return sprite;
            Texture2D texture = Resources.Load<Texture2D>(resourcePath);
            if (texture == null) return fallback;
            sprite = Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height),
                new Vector2(0.5f, 0.5f), 100f);
            sprite.name = Selected.Id + "MapCharacterRuntime";
            return sprite;
        }
    }
}
