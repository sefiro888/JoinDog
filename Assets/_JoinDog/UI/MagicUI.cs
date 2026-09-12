using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace JoinDog.App
{
    public static class MagicUI
    {
        public static readonly Color Ink = new Color(.18f,.07f,.32f);
        public static readonly Color Pearl = new Color(.96f,.92f,1f);
        public static readonly Color Purple = new Color(.58f,.24f,.86f);
        private static TMP_FontAsset font;
        private static Sprite jewel;
        public static TMP_FontAsset Font => font != null ? font : (font = Resources.Load<TMP_FontAsset>("Fonts/MagicRounded SDF"));

        public static void Style(TextMeshProUGUI text, bool display = false)
        {
            if (Font != null) text.font = Font;
            text.raycastTarget = false;
            if (!display) return;
            text.color = Color.white;
            text.enableVertexGradient = true;
            text.colorGradient = new VertexGradient(new Color(.85f,1f,1f), Color.white,
                new Color(.80f,.48f,1f), new Color(1f,.66f,.94f));
            text.outlineColor = Ink;
            text.outlineWidth = .2f;
            Material mat = text.fontMaterial;
            mat.EnableKeyword("UNDERLAY_ON");
            mat.SetColor("_UnderlayColor", new Color(.24f,.04f,.46f,.95f));
            mat.SetFloat("_UnderlayOffsetY", -.65f);
            mat.SetFloat("_UnderlayDilate", .15f);
            mat.SetFloat("_UnderlaySoftness", .05f);
        }

        public static Image Card(RectTransform root, string name, Vector2 min, Vector2 max)
        {
            Image image = JoinDogUIFactory.Panel(root,name,min,max,Pearl);
            var outline = image.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(.72f,.58f,.88f);
            outline.effectDistance = new Vector2(3,-3);
            var shadow = image.gameObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(.13f,.03f,.23f,.45f);
            shadow.effectDistance = new Vector2(0,-9);
            return image;
        }

        public static TextMeshProUGUI Heading(RectTransform root,string name,string value,float size,Vector2 min,Vector2 max)
        {
            var text = JoinDogUIFactory.Text(root,name,value,size,Color.white,TextAlignmentOptions.Center,min,max);
            Style(text,true);
            return text;
        }

        public static void PolishButton(Image image)
        {
            image.sprite = JewelSprite();
            image.type = Image.Type.Sliced;
            var shadow = image.gameObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(.15f,.03f,.28f,.6f);
            shadow.effectDistance = new Vector2(0,-7);
        }

        private static Sprite JewelSprite()
        {
            if (jewel != null) return jewel;
            const int w=256,h=128;
            var texture = new Texture2D(w,h,TextureFormat.RGBA32,false);
            var pixels = new Color[w*h];
            for(int y=0;y<h;y++) for(int x=0;x<w;x++)
            {
                Vector2 q = new Vector2(Mathf.Abs(x-w*.5f)-(w*.5f-40),Mathf.Abs(y-h*.5f)-(h*.5f-40));
                float distance = new Vector2(Mathf.Max(q.x,0),Mathf.Max(q.y,0)).magnitude + Mathf.Min(Mathf.Max(q.x,q.y),0)-38;
                float alpha = Mathf.Clamp01(-distance);
                float v=y/(float)h;
                float light=.68f+.30f*v + .28f*Mathf.Exp(-Mathf.Pow((v-.78f)/.10f,2));
                if(distance > -5) light = v>.5f ? 1.35f : .48f;
                else if(distance > -9) light = v>.5f ? .7f : 1.1f;
                pixels[y*w+x]=new Color(light,light,light,alpha);
            }
            texture.SetPixels(pixels); texture.Apply(false,true);
            jewel=Sprite.Create(texture,new Rect(0,0,w,h),new Vector2(.5f,.5f),100,0,SpriteMeshType.FullRect,new Vector4(45,45,45,45));
            return jewel;
        }
    }
}
