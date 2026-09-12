using System;
using System.Runtime.InteropServices;
using UnityEngine;

namespace JoinDog.App
{
    public sealed class PetPhotoImport : MonoBehaviour
    {
        private const string Key="JoinDog_LocalPetPhoto";
        private static Sprite cached;
        public static bool HasPhoto => PlayerPrefs.HasKey(Key);
        public Action<string> Completed;
#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")] private static extern void JoinDogChoosePet(string receiver);
#endif
        public void Choose()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            JoinDogChoosePet(gameObject.name);
#else
            Completed?.Invoke("La foto se elige desde la versión web local.");
#endif
        }
        public void ReceivePhoto(string encoded)
        {
            if(string.IsNullOrEmpty(encoded) || encoded.Length>400000) { Completed?.Invoke("No se pudo cargar esa imagen."); return; }
            try
            {
                var texture=new Texture2D(2,2);
                if(!texture.LoadImage(Convert.FromBase64String(encoded))) { Destroy(texture); Completed?.Invoke("Imagen no válida."); return; }
                if(texture.width>512 || texture.height>512) { Destroy(texture); Completed?.Invoke("Imagen demasiado grande."); return; }
                var sprite=Sprite.Create(texture,new Rect(0,0,texture.width,texture.height),new Vector2(.5f,.5f),100);
                // Keep previous sprite instances alive while another screen still references them.
                cached=sprite;
                PlayerPrefs.SetString(Key,encoded);
                MapCharacterSelection.Select("local-photo");
                PlayerPrefs.Save();
                Completed?.Invoke(null);
            }
            catch(Exception) { Completed?.Invoke("No se pudo guardar la foto."); }
        }
        public void PhotoError(string message) => Completed?.Invoke(message);
        public static Sprite Load(Sprite fallback=null)
        {
            if(cached!=null) return cached;
            if(!HasPhoto) return fallback;
            try
            {
                var texture=new Texture2D(2,2);
                if(!texture.LoadImage(Convert.FromBase64String(PlayerPrefs.GetString(Key)))) { Destroy(texture); return fallback; }
                cached=Sprite.Create(texture,new Rect(0,0,texture.width,texture.height),new Vector2(.5f,.5f),100);
                return cached;
            }
            catch(Exception) { return fallback; }
        }
    }
}
