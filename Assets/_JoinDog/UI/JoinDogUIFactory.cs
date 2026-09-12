using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace JoinDog.App
{
    public static class JoinDogUIFactory
    {
        private static Sprite roundedSprite;
        private static Sprite circleSprite;

        public static Canvas CreateCanvas(string name)
        {
            GameObject root = new GameObject(name, typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            Canvas canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 200;
            CanvasScaler scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080f, 2340f);
            scaler.matchWidthOrHeight = 0.5f;
            return canvas;
        }

        public static Image Image(RectTransform parent, string name, Sprite sprite,
            Vector2 anchorMin, Vector2 anchorMax, Color color, bool raycast = false)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            RectTransform rect = go.GetComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            Image image = go.GetComponent<Image>();
            image.sprite = sprite;
            image.color = color;
            image.raycastTarget = raycast;
            return image;
        }

        public static Image Panel(RectTransform parent, string name, Vector2 anchorMin,
            Vector2 anchorMax, Color color)
        {
            Image image = Image(parent, name, RoundedSprite(), anchorMin, anchorMax, color);
            image.type = UnityEngine.UI.Image.Type.Sliced;
            return image;
        }

        public static TextMeshProUGUI Text(RectTransform parent, string name, string value,
            float size, Color color, TextAlignmentOptions alignment, Vector2 anchorMin, Vector2 anchorMax)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            RectTransform rect = go.GetComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            TextMeshProUGUI text = go.GetComponent<TextMeshProUGUI>();
            text.font = TMP_Settings.defaultFontAsset;
            if (text.font == null)
                text.font = Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");
            text.text = value;
            text.fontSize = size;
            text.color = color;
            text.alignment = alignment;
            text.fontStyle = FontStyles.Bold;
            text.enableAutoSizing = true;
            text.fontSizeMin = Mathf.Max(14f, size * 0.55f);
            text.fontSizeMax = size;
            text.enableWordWrapping = false;
            text.raycastTarget = false;
            MagicUI.Style(text);
            return text;
        }

        public static Button Button(RectTransform parent, string name, string label,
            Vector2 anchorMin, Vector2 anchorMax, Color color)
        {
            Image image = Panel(parent, name, anchorMin, anchorMax, color);
            image.raycastTarget = true;
            MagicUI.PolishButton(image);
            Outline outline = image.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(.73f,.81f,1f,.95f);
            outline.effectDistance = new Vector2(3f, -3f);
            Button button = image.gameObject.AddComponent<Button>();
            ColorBlock colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1.08f, 1.08f, 1.08f, 1f);
            colors.pressedColor = new Color(0.78f, 0.78f, 0.78f, 1f);
            colors.disabledColor = new Color(0.45f, 0.45f, 0.45f, 0.75f);
            colors.fadeDuration = 0.08f;
            button.colors = colors;
            Text(image.rectTransform, name + "Label", label, 32f, Color.white,
                TextAlignmentOptions.Center, new Vector2(0.05f, 0.08f), new Vector2(0.95f, 0.92f));
            image.gameObject.AddComponent<JoinDogButtonFeedback>();
            EnsureMinimumTouchTarget(button);
            return button;
        }

        public static void EnsureMinimumTouchTargets(Transform root)
        {
            if (root == null) return;
            foreach (Button button in root.GetComponentsInChildren<Button>(true))
                EnsureMinimumTouchTarget(button);
        }

        public static void EnsureMinimumTouchTarget(Button button, float minimumSize = 56f)
        {
            if (button == null || button.transform.Find("MinimumTouchTarget") != null) return;
            GameObject target = new GameObject("MinimumTouchTarget", typeof(RectTransform), typeof(Image));
            target.transform.SetParent(button.transform, false);
            target.transform.SetAsFirstSibling();
            RectTransform rect = target.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(minimumSize, minimumSize);
            Image hitArea = target.GetComponent<Image>();
            hitArea.color = new Color(1f, 1f, 1f, 0f);
            hitArea.raycastTarget = true;
        }

        public static Sprite RoundedSprite()
        {
            if (roundedSprite != null) return roundedSprite;
            roundedSprite = CreateShapeSprite("JoinDogRounded", false);
            return roundedSprite;
        }

        public static Sprite CircleSprite()
        {
            if (circleSprite != null) return circleSprite;
            circleSprite = CreateShapeSprite("JoinDogCircle", true);
            return circleSprite;
        }

        private static Sprite CreateShapeSprite(string name, bool circle)
        {
            const int size = 96;
            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            texture.name = name;
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.filterMode = FilterMode.Bilinear;
            Color32[] pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float alpha;
                    if (circle)
                    {
                        float distance = Vector2.Distance(new Vector2(x, y), new Vector2(47.5f, 47.5f));
                        alpha = Mathf.Clamp01(48.5f - distance);
                    }
                    else
                    {
                        const float radius = 22f;
                        float dx = Mathf.Max(radius - x, 0f, x - (size - 1f - radius));
                        float dy = Mathf.Max(radius - y, 0f, y - (size - 1f - radius));
                        alpha = Mathf.Clamp01(radius + 1f - Mathf.Sqrt(dx * dx + dy * dy));
                    }
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)(alpha * 255f));
                }
            }
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f),
                100f, 0, SpriteMeshType.FullRect, circle ? Vector4.zero : new Vector4(22f, 22f, 22f, 22f));
        }
    }
}
