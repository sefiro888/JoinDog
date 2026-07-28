using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DogCrush.UI
{
    public class GameplayUIController : MonoBehaviour
    {
        [Header("HUD Elements")]
        public TextMeshProUGUI scoreText;
        public TextMeshProUGUI highScoreText;
        public TextMeshProUGUI timerText;
        public Image timerBarFill;
        public TextMeshProUGUI chainInfoText;
        public TextMeshProUGUI comboBannerText;

        [Header("Game Over Overlay")]
        public GameObject gameOverPanel;
        public TextMeshProUGUI finalScoreText;
        public TextMeshProUGUI newRecordBanner;
        public Button playAgainButton;
        public Button secondaryRestartButton;
        public Button hudRestartButton;
        public System.Action OnNextLevelRequested;
        public System.Action OnShuffleBoosterRequested;
        public System.Action OnBoneBoosterRequested;
        public System.Action OnFoodBoosterRequested;
        public System.Action<int> OnLevelSelected;
        public System.Action<bool> OnLevelSelectVisibilityChanged;

        [Header("Settings Overlay")]
        public GameObject settingsPanel;
        public Button settingsButton;
        public Button soundToggleButton;
        public Button hapticsToggleButton;
        public Button settingsCloseButton;
        public TextMeshProUGUI soundToggleText;
        public TextMeshProUGUI hapticsToggleText;

        public System.Action OnRestartRequested;
        public System.Action OnSoundToggleRequested;
        public System.Action OnHapticsToggleRequested;
        public System.Action<bool> OnSettingsVisibilityChanged;

        private int targetScore = 0;
        private int displayedScore = 0;
        private int levelTargetScore = 5000;
        private int objectiveProgress = 0;
        private string objectiveLabel = "PUNTOS";
        private bool scoreIsObjective = true;
        private bool lastResultWasVictory;
        private Coroutine comboRoutine;

        private Canvas runtimeCanvas;
        private Image timerBarGlow;
        private Image bottomPillBg;
        private Image chainInfoPanel;
        private TextMeshProUGUI bottomPillText;
        private TextMeshProUGUI levelText;
        private TextMeshProUGUI livesText;
        private Image livesIcon;
        private TextMeshProUGUI resultTitleText;
        private TextMeshProUGUI resultLabelText;
        private TextMeshProUGUI resultButtonText;
        private Button movesBoosterButton;
        private Button boneBoosterButton;
        private Button foodBoosterButton;
        private TextMeshProUGUI movesCountText;
        private TextMeshProUGUI boneCountText;
        private TextMeshProUGUI foodCountText;
        private GameObject levelSelectPanel;
        private GameObject tutorialPanel;
        private readonly List<Button> levelButtons = new List<Button>();
        private readonly List<TextMeshProUGUI> levelButtonLabels = new List<TextMeshProUGUI>();
        private int unlockedLevel = 1;
        private Sprite roundedRectSprite;
        private RectTransform portraitContentRect;
        private RectTransform logoRect;
        private RectTransform chainInfoPanelRect;
        private RectTransform chainInfoTextRect;
        private Coroutine chainPulseRoutine;
        private int lastHudScreenWidth;
        private int lastHudScreenHeight;

        private void Awake()
        {
            // 1. Completely strip and hide ALL old UI elements in Canvas/SafeArea
            CleanOldSceneUI();

            // 2. Build gorgeous reference-matching UI from scratch
            BuildRuntimeUI();

            HideGameOver();
            if (comboBannerText != null) comboBannerText.gameObject.SetActive(false);
        }

        private void CleanOldSceneUI()
        {
            Canvas[] canvases = FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (Canvas canvas in canvases)
            {
                // Disable all pre-existing children in the canvas tree
                for (int i = canvas.transform.childCount - 1; i >= 0; i--)
                {
                    Transform child = canvas.transform.GetChild(i);
                    if (!child.name.EndsWith("_RT"))
                    {
                        DisableAllChildrenRecursive(child);
                        child.gameObject.SetActive(false);
                    }
                }
            }

            // Also find and disable any legacy SpriteRenderers or GameObjects in world space drawing bars
            SpriteRenderer[] srs = FindObjectsByType<SpriteRenderer>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (SpriteRenderer sr in srs)
            {
                string n = sr.gameObject.name.ToLower();
                if (n.Contains("timer") || n.Contains("bar") || n.Contains("header") || n.Contains("bottom") || n.Contains("panel") || n.Contains("hud") || n.Contains("frame"))
                {
                    if (!sr.gameObject.name.EndsWith("_RT") && sr.gameObject.name != "DogParkBackground" && sr.gameObject.name != "BoardFrame")
                    {
                        sr.gameObject.SetActive(false);
                    }
                }
            }
        }

        private void DisableAllChildrenRecursive(Transform parent)
        {
            foreach (Transform t in parent)
            {
                if (!t.name.EndsWith("_RT"))
                {
                    t.gameObject.SetActive(false);
                    DisableAllChildrenRecursive(t);
                }
            }
        }

        private void BuildRuntimeUI()
        {
            runtimeCanvas = FindAnyObjectByType<Canvas>();
            if (runtimeCanvas == null)
            {
                GameObject canvasObj = new GameObject("Canvas_RT", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
                runtimeCanvas = canvasObj.GetComponent<Canvas>();
                runtimeCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
                CanvasScaler scaler = canvasObj.GetComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1080, 1920);
                scaler.matchWidthOrHeight = 0.5f;
            }

            CanvasScaler runtimeScaler = runtimeCanvas.GetComponent<CanvasScaler>();
            if (runtimeScaler == null)
            {
                runtimeScaler = runtimeCanvas.gameObject.AddComponent<CanvasScaler>();
            }
            runtimeScaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            runtimeScaler.referenceResolution = new Vector2(1080, 2340);
            runtimeScaler.matchWidthOrHeight = 0f;

            RectTransform canvasRect = CreatePortraitContent(runtimeCanvas.GetComponent<RectTransform>());

            RectTransform topHudRect = CreateHudShell(
                canvasRect,
                "TopHud_RT",
                new Vector2(0.035f, 0.875f),
                new Vector2(0.965f, 0.985f));

            RectTransform levelSlot = CreateHudSlot(
                topHudRect, "LevelSlot_RT", new Vector2(0.025f, 0.12f), new Vector2(0.245f, 0.88f));
            CreateHudLabel(levelSlot, "LevelLabel_RT", "NIVEL");
            levelText = CreateHudValue(levelSlot, "LevelText_RT", "1", 27f);
            Button levelSelectButton = levelSlot.gameObject.AddComponent<Button>();
            levelSelectButton.transition = Selectable.Transition.None;
            levelSelectButton.onClick.AddListener(ShowLevelSelect);

            RectTransform recordSlot = CreateHudSlot(
                topHudRect, "RecordSlot_RT", new Vector2(0.26f, 0.12f), new Vector2(0.50f, 0.88f));
            CreateHudLabel(recordSlot, "RecordLabel_RT", "RÉCORD");
            highScoreText = CreateHudValue(recordSlot, "HighScoreText_RT", "0", 24f);

            RectTransform timerSlot = CreateHudSlot(
                topHudRect, "TimerSlot_RT", new Vector2(0.515f, 0.12f), new Vector2(0.755f, 0.88f));
            CreateHudLabel(timerSlot, "TimerLabel_RT", "TIEMPO");
            timerText = CreateHudValue(timerSlot, "TimerText_RT", "60s", 27f);

            Image timerTrack = CreatePanelImage(
                timerSlot,
                "TimerTrack_RT",
                new Vector2(0.08f, 0.05f),
                new Vector2(0.92f, 0.13f),
                new Color(0.10f, 0.025f, 0.012f, 0.96f));
            timerBarFill = CreatePanelImage(
                timerTrack.rectTransform,
                "TimerBarFill_RT",
                Vector2.zero,
                Vector2.one,
                new Color(0.25f, 0.9f, 0.35f, 1f));
            timerBarFill.type = Image.Type.Filled;
            timerBarFill.fillMethod = Image.FillMethod.Horizontal;
            timerBarFill.fillAmount = 1f;

            RectTransform livesSlot = CreateHudSlot(
                topHudRect, "LivesSlot_RT", new Vector2(0.77f, 0.12f), new Vector2(0.975f, 0.88f));
            CreateHudLabel(livesSlot, "LivesLabel_RT", "VIDAS");
            livesIcon = CreateImage(
                livesSlot,
                "LivesIcon_RT",
                LoadUISprite("icon-life-heart"),
                new Vector2(0.08f, 0.16f),
                new Vector2(0.48f, 0.72f));
            livesText = CreateText(
                livesSlot,
                "LivesText_RT",
                "5/5",
                22f,
                Color.white,
                TextAlignmentOptions.Center,
                new Vector2(0.44f, 0.14f),
                new Vector2(0.94f, 0.70f),
                Vector2.zero,
                Vector2.zero);
            livesText.fontStyle = FontStyles.Bold;
            livesText.enableAutoSizing = true;
            livesText.fontSizeMin = 11f;
            livesText.fontSizeMax = 22f;
            livesText.overflowMode = TextOverflowModes.Truncate;
            livesText.margin = new Vector4(3f, 0f, 3f, 0f);

            RectTransform bottomPillRect = CreateHudShell(
                canvasRect,
                "BottomHud_RT",
                new Vector2(0.035f, 0.018f),
                new Vector2(0.965f, 0.165f));
            bottomPillBg = bottomPillRect.GetComponent<Image>();

            RectTransform scoreSlot = CreateHudSlot(
                bottomPillRect, "ScoreSlot_RT", new Vector2(0.025f, 0.12f), new Vector2(0.275f, 0.88f));
            CreateHudLabel(scoreSlot, "ScoreLabel_RT", "OBJETIVO");
            scoreText = CreateHudValue(scoreSlot, "ScoreText_RT", "0 / 5.000", 24f);
            scoreText.color = new Color(1f, 0.91f, 0.28f);

            movesBoosterButton = CreateBoosterButton(bottomPillRect, "MovesButton_RT", "button-moves", 0.30f, 0.455f);
            movesBoosterButton.onClick.AddListener(() => OnShuffleBoosterRequested?.Invoke());
            boneBoosterButton = CreateBoosterButton(bottomPillRect, "BoneButton_RT", "button-bone", 0.47f, 0.625f);
            boneBoosterButton.onClick.AddListener(() => OnBoneBoosterRequested?.Invoke());
            foodBoosterButton = CreateBoosterButton(bottomPillRect, "FoodButton_RT", "button-food", 0.64f, 0.795f);
            foodBoosterButton.onClick.AddListener(() => OnFoodBoosterRequested?.Invoke());
            settingsButton = CreateBoosterButton(
                bottomPillRect, "SettingsButton_RT", "button-settings", 0.81f, 0.965f);
            settingsButton.onClick.AddListener(() => SetSettingsVisible(true));

            Image logo = CreateImage(canvasRect, "DogCrushLogo_RT", LoadUISprite("dogcrush-logo"),
                new Vector2(0.23f, 0.675f), new Vector2(0.77f, 0.845f));
            logoRect = logo.rectTransform;
            logo.gameObject.SetActive(false);
            logoRect = null;
            ApplyResponsiveHudLayout();

            // The live chain count gets its own compact badge, clear of the logo.
            chainInfoPanel = CreateImage(canvasRect, "ChainInfoPanel_RT", LoadUISprite("objective-panel"),
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
            chainInfoPanel.sprite = CreateRoundedRectSprite();
            chainInfoPanel.type = Image.Type.Sliced;
            chainInfoPanel.color = new Color(0.20f, 0.09f, 0.025f, 0.96f);
            chainInfoPanelRect = chainInfoPanel.rectTransform;
            chainInfoPanelRect.sizeDelta = new Vector2(146f, 78f);
            chainInfoPanelRect.pivot = new Vector2(0.5f, 0.5f);
            Outline chainOutline = chainInfoPanel.gameObject.AddComponent<Outline>();
            chainOutline.effectColor = new Color(1f, 0.72f, 0.18f, 0.92f);
            chainOutline.effectDistance = new Vector2(3f, -3f);
            chainInfoPanel.gameObject.SetActive(false);

            // === CHAIN SELECTION FLOATING TEXT ===
            chainInfoText = CreateText(canvasRect, "ChainInfoText_RT",
                "", 40f, new Color(1f, 0.94f, 0.48f),
                TextAlignmentOptions.Center,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                Vector2.zero, Vector2.zero);
            chainInfoText.fontStyle = FontStyles.Bold;
            chainInfoText.enableAutoSizing = true;
            chainInfoText.fontSizeMin = 22f;
            chainInfoText.fontSizeMax = 40f;
            chainInfoText.lineSpacing = -18f;
            chainInfoTextRect = chainInfoText.rectTransform;
            chainInfoTextRect.sizeDelta = new Vector2(146f, 78f);
            chainInfoTextRect.pivot = new Vector2(0.5f, 0.5f);
            chainInfoText.gameObject.SetActive(false);

            // === COMBO BANNER ===
            comboBannerText = CreateText(canvasRect, "ComboBannerText_RT",
                "", 64f, new Color(1f, 0.85f, 0.15f),
                TextAlignmentOptions.Center,
                new Vector2(0.1f, 0.45f), new Vector2(0.9f, 0.55f),
                Vector2.zero, Vector2.zero);
            comboBannerText.fontStyle = FontStyles.Bold;
            comboBannerText.gameObject.SetActive(false);

            BuildSettingsPanel(canvasRect);
            BuildLevelSelectPanel(canvasRect);
            BuildTutorialPanel(canvasRect);

            // === GAME OVER OVERLAY ===
            BuildGameOverPanel(canvasRect);
        }

        private RectTransform CreatePortraitContent(RectTransform canvasRect)
        {
            GameObject content = new GameObject(
                "PortraitContent_RT",
                typeof(RectTransform),
                typeof(AspectRatioFitter),
                typeof(SafeAreaHandler));
            content.transform.SetParent(canvasRect, false);

            portraitContentRect = content.GetComponent<RectTransform>();
            portraitContentRect.anchorMin = Vector2.zero;
            portraitContentRect.anchorMax = Vector2.one;
            portraitContentRect.offsetMin = Vector2.zero;
            portraitContentRect.offsetMax = Vector2.zero;

            AspectRatioFitter fitter = content.GetComponent<AspectRatioFitter>();
            fitter.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
            fitter.aspectRatio = 9f / 19.5f;
            return portraitContentRect;
        }

        private RectTransform CreateHudShell(
            RectTransform parent,
            string name,
            Vector2 anchorMin,
            Vector2 anchorMax)
        {
            GameObject shell = new GameObject(name, typeof(RectTransform));
            shell.transform.SetParent(parent, false);
            RectTransform shellRect = shell.GetComponent<RectTransform>();
            shellRect.anchorMin = anchorMin;
            shellRect.anchorMax = anchorMax;
            shellRect.offsetMin = Vector2.zero;
            shellRect.offsetMax = Vector2.zero;

            Image shadow = CreatePanelImage(
                shellRect,
                $"{name}Shadow",
                new Vector2(0.008f, -0.07f),
                new Vector2(0.992f, 0.94f),
                new Color(0.075f, 0.018f, 0.008f, 0.72f));
            shadow.raycastTarget = false;

            Image outer = CreatePanelImage(
                shellRect,
                $"{name}Frame",
                Vector2.zero,
                Vector2.one,
                new Color(0.30f, 0.075f, 0.018f, 1f));
            outer.raycastTarget = false;

            Image wood = CreatePanelImage(
                shellRect,
                $"{name}Wood",
                new Vector2(0.009f, 0.055f),
                new Vector2(0.991f, 0.955f),
                new Color(0.69f, 0.26f, 0.055f, 1f));
            wood.raycastTarget = false;

            Image surface = CreatePanelImage(
                shellRect,
                $"{name}Surface",
                new Vector2(0.018f, 0.10f),
                new Vector2(0.982f, 0.90f),
                new Color(0.47f, 0.14f, 0.035f, 1f));
            surface.raycastTarget = false;

            Image sheen = CreatePanelImage(
                shellRect,
                $"{name}Sheen",
                new Vector2(0.045f, 0.80f),
                new Vector2(0.955f, 0.89f),
                new Color(1f, 0.72f, 0.32f, 0.22f));
            sheen.raycastTarget = false;

            return surface.rectTransform;
        }

        private RectTransform CreateHudSlot(
            RectTransform parent,
            string name,
            Vector2 anchorMin,
            Vector2 anchorMax)
        {
            Image slot = CreatePanelImage(
                parent,
                name,
                anchorMin,
                anchorMax,
                new Color(0.16f, 0.04f, 0.018f, 0.94f));
            slot.raycastTarget = false;

            Image glow = CreatePanelImage(
                slot.rectTransform,
                $"{name}Glow",
                new Vector2(0.035f, 0.68f),
                new Vector2(0.965f, 0.90f),
                new Color(1f, 0.52f, 0.16f, 0.11f));
            glow.raycastTarget = false;
            return slot.rectTransform;
        }

        private Image CreatePanelImage(
            RectTransform parent,
            string name,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Color color)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            RectTransform rect = go.GetComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            Image image = go.GetComponent<Image>();
            image.sprite = CreateRoundedRectSprite();
            image.type = Image.Type.Sliced;
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        private void CreateHudLabel(RectTransform parent, string name, string text)
        {
            TextMeshProUGUI label = CreateText(
                parent,
                name,
                text,
                14f,
                new Color(1f, 0.72f, 0.32f, 0.96f),
                TextAlignmentOptions.Center,
                new Vector2(0.05f, 0.64f),
                new Vector2(0.95f, 0.93f),
                Vector2.zero,
                Vector2.zero);
            label.fontStyle = FontStyles.Bold;
            label.characterSpacing = 1.5f;
            label.enableAutoSizing = true;
            label.fontSizeMin = 8f;
            label.fontSizeMax = 14f;
            label.overflowMode = TextOverflowModes.Truncate;
            label.margin = new Vector4(4f, 0f, 4f, 0f);
            Outline labelOutline = label.gameObject.AddComponent<Outline>();
            labelOutline.effectColor = new Color(0.12f, 0.025f, 0.008f, 0.85f);
            labelOutline.effectDistance = new Vector2(1.2f, -1.2f);
        }

        private TextMeshProUGUI CreateHudValue(
            RectTransform parent,
            string name,
            string text,
            float fontSize)
        {
            TextMeshProUGUI value = CreateText(
                parent,
                name,
                text,
                fontSize,
                Color.white,
                TextAlignmentOptions.Center,
                new Vector2(0.04f, 0.12f),
                new Vector2(0.96f, 0.61f),
                Vector2.zero,
                Vector2.zero);
            value.fontStyle = FontStyles.Bold;
            value.enableAutoSizing = true;
            value.fontSizeMin = 11f;
            value.fontSizeMax = fontSize;
            value.overflowMode = TextOverflowModes.Truncate;
            value.margin = new Vector4(5f, 0f, 5f, 0f);
            Outline valueOutline = value.gameObject.AddComponent<Outline>();
            valueOutline.effectColor = new Color(0.10f, 0.018f, 0.006f, 0.92f);
            valueOutline.effectDistance = new Vector2(1.8f, -1.8f);
            return value;
        }

        private Button CreateBoosterButton(
            RectTransform parent,
            string name,
            string spriteName,
            float anchorMinX,
            float anchorMaxX)
        {
            RectTransform slot = CreateHudSlot(
                parent,
                $"{name}Slot",
                new Vector2(anchorMinX, 0.10f),
                new Vector2(anchorMaxX, 0.90f));
            Button button = CreateIconButton(
                slot,
                name,
                spriteName,
                new Vector2(0.04f, 0.02f),
                new Vector2(0.96f, 0.98f));
            TextMeshProUGUI countText = CreateText(
                slot,
                $"{name}Count_RT",
                "1",
                16f,
                Color.white,
                TextAlignmentOptions.Center,
                new Vector2(0.70f, 0.02f),
                new Vector2(0.98f, 0.28f),
                Vector2.zero,
                Vector2.zero);
            countText.fontStyle = FontStyles.Bold;
            countText.enableAutoSizing = true;
            countText.fontSizeMin = 10f;
            countText.fontSizeMax = 18f;
            countText.outlineWidth = 0.25f;
            countText.raycastTarget = false;

            if (name == "MovesButton_RT") movesCountText = countText;
            else if (name == "BoneButton_RT") boneCountText = countText;
            else if (name == "FoodButton_RT") foodCountText = countText;
            return button;
        }

        private Sprite CreateRoundedRectSprite()
        {
            if (roundedRectSprite != null) return roundedRectSprite;

            const int size = 64;
            const float radius = 18f;
            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            texture.name = "RoundedRectRuntime_RT";
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.filterMode = FilterMode.Bilinear;

            Color32[] pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = Mathf.Max(radius - x, 0f, x - (size - 1 - radius));
                    float dy = Mathf.Max(radius - y, 0f, y - (size - 1 - radius));
                    float distance = Mathf.Sqrt(dx * dx + dy * dy);
                    byte alpha = (byte)Mathf.RoundToInt(
                        255f * Mathf.Clamp01(radius + 1f - distance));
                    pixels[y * size + x] = new Color32(255, 255, 255, alpha);
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            roundedRectSprite = Sprite.Create(
                texture,
                new Rect(0f, 0f, size, size),
                new Vector2(0.5f, 0.5f),
                100f,
                0,
                SpriteMeshType.FullRect,
                new Vector4(radius, radius, radius, radius));
            return roundedRectSprite;
        }

        private void BuildSettingsPanel(RectTransform canvasRect)
        {
            GameObject overlay = new GameObject(
                "SettingsPanel_RT",
                typeof(RectTransform),
                typeof(Image),
                typeof(CanvasGroup));
            overlay.transform.SetParent(canvasRect, false);
            RectTransform overlayRect = overlay.GetComponent<RectTransform>();
            overlayRect.anchorMin = Vector2.zero;
            overlayRect.anchorMax = Vector2.one;
            overlayRect.offsetMin = Vector2.zero;
            overlayRect.offsetMax = Vector2.zero;
            overlay.GetComponent<Image>().color = new Color(0.035f, 0.025f, 0.02f, 0.72f);
            settingsPanel = overlay;

            GameObject card = new GameObject("SettingsCard_RT", typeof(RectTransform), typeof(Image), typeof(Outline));
            card.transform.SetParent(overlayRect, false);
            RectTransform cardRect = card.GetComponent<RectTransform>();
            cardRect.anchorMin = new Vector2(0.12f, 0.34f);
            cardRect.anchorMax = new Vector2(0.88f, 0.66f);
            cardRect.offsetMin = Vector2.zero;
            cardRect.offsetMax = Vector2.zero;
            Image cardImage = card.GetComponent<Image>();
            cardImage.sprite = CreateRoundedRectSprite();
            cardImage.type = Image.Type.Sliced;
            cardImage.color = new Color(0.27f, 0.075f, 0.025f, 0.99f);
            Outline cardOutline = card.GetComponent<Outline>();
            cardOutline.effectColor = new Color(1f, 0.58f, 0.12f, 0.92f);
            cardOutline.effectDistance = new Vector2(4f, -4f);

            TextMeshProUGUI title = CreateText(
                cardRect,
                "SettingsTitle_RT",
                "AJUSTES",
                42f,
                new Color(1f, 0.88f, 0.35f),
                TextAlignmentOptions.Center,
                new Vector2(0.08f, 0.76f),
                new Vector2(0.92f, 0.94f),
                Vector2.zero,
                Vector2.zero);
            title.fontStyle = FontStyles.Bold;

            soundToggleButton = CreateSettingsButton(
                cardRect,
                "SoundToggleButton_RT",
                new Vector2(0.10f, 0.50f),
                new Vector2(0.90f, 0.70f),
                out soundToggleText);
            soundToggleButton.onClick.AddListener(() => OnSoundToggleRequested?.Invoke());

            hapticsToggleButton = CreateSettingsButton(
                cardRect,
                "HapticsToggleButton_RT",
                new Vector2(0.10f, 0.27f),
                new Vector2(0.90f, 0.47f),
                out hapticsToggleText);
            hapticsToggleButton.onClick.AddListener(() => OnHapticsToggleRequested?.Invoke());

            settingsCloseButton = CreateSettingsButton(
                cardRect,
                "SettingsCloseButton_RT",
                new Vector2(0.25f, 0.06f),
                new Vector2(0.75f, 0.21f),
                out TextMeshProUGUI closeText);
            closeText.text = "CONTINUAR";
            settingsCloseButton.onClick.AddListener(() => SetSettingsVisible(false));

            UpdateSettingsState(1f, true);
            settingsPanel.SetActive(false);
        }

        private Button CreateSettingsButton(
            RectTransform parent,
            string name,
            Vector2 anchorMin,
            Vector2 anchorMax,
            out TextMeshProUGUI label)
        {
            GameObject buttonObject = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            buttonObject.transform.SetParent(parent, false);
            RectTransform rect = buttonObject.GetComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            Image image = buttonObject.GetComponent<Image>();
            image.sprite = CreateRoundedRectSprite();
            image.type = Image.Type.Sliced;
            image.color = new Color(0.12f, 0.48f, 0.78f, 1f);

            label = CreateText(
                rect,
                $"{name}Label",
                "",
                27f,
                Color.white,
                TextAlignmentOptions.Center,
                new Vector2(0.04f, 0.06f),
                new Vector2(0.96f, 0.94f),
                Vector2.zero,
                Vector2.zero);
            label.fontStyle = FontStyles.Bold;
            label.enableAutoSizing = true;
            label.fontSizeMin = 16f;
            label.fontSizeMax = 28f;
            return buttonObject.GetComponent<Button>();
        }

        private void BuildLevelSelectPanel(RectTransform canvasRect)
        {
            levelSelectPanel = new GameObject("LevelSelectPanel_RT", typeof(RectTransform), typeof(Image));
            levelSelectPanel.transform.SetParent(canvasRect, false);
            RectTransform overlayRect = levelSelectPanel.GetComponent<RectTransform>();
            overlayRect.anchorMin = Vector2.zero;
            overlayRect.anchorMax = Vector2.one;
            overlayRect.offsetMin = Vector2.zero;
            overlayRect.offsetMax = Vector2.zero;
            levelSelectPanel.GetComponent<Image>().color = new Color(0.035f, 0.025f, 0.02f, 0.78f);

            GameObject card = new GameObject("LevelSelectCard_RT", typeof(RectTransform), typeof(Image));
            card.transform.SetParent(overlayRect, false);
            RectTransform cardRect = card.GetComponent<RectTransform>();
            cardRect.anchorMin = new Vector2(0.10f, 0.16f);
            cardRect.anchorMax = new Vector2(0.90f, 0.84f);
            cardRect.offsetMin = Vector2.zero;
            cardRect.offsetMax = Vector2.zero;
            Image cardImage = card.GetComponent<Image>();
            cardImage.sprite = CreateRoundedRectSprite();
            cardImage.type = Image.Type.Sliced;
            cardImage.color = new Color(0.20f, 0.09f, 0.025f, 0.98f);

            TextMeshProUGUI title = CreateText(cardRect, "LevelSelectTitle_RT", "SELECCIONA NIVEL", 34f,
                new Color(1f, 0.88f, 0.25f), TextAlignmentOptions.Center,
                new Vector2(0.05f, 0.86f), new Vector2(0.95f, 0.97f), Vector2.zero, Vector2.zero);
            title.fontStyle = FontStyles.Bold;

            for (int i = 0; i < 10; i++)
            {
                int level = i + 1;
                GameObject buttonObject = new GameObject($"LevelButton_{level}_RT", typeof(RectTransform), typeof(Image), typeof(Button));
                buttonObject.transform.SetParent(cardRect, false);
                RectTransform buttonRect = buttonObject.GetComponent<RectTransform>();
                float top = 0.78f - i * 0.065f;
                buttonRect.anchorMin = new Vector2(0.14f, top - 0.052f);
                buttonRect.anchorMax = new Vector2(0.86f, top);
                buttonRect.offsetMin = Vector2.zero;
                buttonRect.offsetMax = Vector2.zero;
                Image buttonImage = buttonObject.GetComponent<Image>();
                buttonImage.sprite = CreateRoundedRectSprite();
                buttonImage.type = Image.Type.Sliced;
                buttonImage.color = new Color(0.08f, 0.42f, 0.70f, 1f);
                TextMeshProUGUI label = CreateText(buttonRect, $"LevelButtonLabel_{level}_RT", "", 24f,
                    Color.white, TextAlignmentOptions.Center, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
                label.fontStyle = FontStyles.Bold;
                Button button = buttonObject.GetComponent<Button>();
                button.transition = Selectable.Transition.ColorTint;
                button.onClick.AddListener(() => SelectLevel(level));
                levelButtons.Add(button);
                levelButtonLabels.Add(label);
            }

            Button closeButton = CreateSettingsButton(cardRect, "LevelSelectClose_RT",
                new Vector2(0.25f, 0.045f), new Vector2(0.75f, 0.14f), out TextMeshProUGUI closeLabel);
            closeLabel.text = "CERRAR";
            closeButton.onClick.AddListener(() => SetLevelSelectVisible(false));

            Button helpButton = CreateSettingsButton(cardRect, "TutorialOpen_RT",
                new Vector2(0.30f, 0.79f), new Vector2(0.70f, 0.85f), out TextMeshProUGUI helpLabel);
            helpLabel.text = "¿CÓMO JUGAR?";
            helpLabel.fontSize = 20f;
            helpButton.onClick.AddListener(() => SetTutorialVisible(true));
            levelSelectPanel.SetActive(false);
        }

        private void BuildTutorialPanel(RectTransform canvasRect)
        {
            tutorialPanel = new GameObject("TutorialPanel_RT", typeof(RectTransform), typeof(Image));
            tutorialPanel.transform.SetParent(canvasRect, false);
            RectTransform overlayRect = tutorialPanel.GetComponent<RectTransform>();
            overlayRect.anchorMin = Vector2.zero;
            overlayRect.anchorMax = Vector2.one;
            overlayRect.offsetMin = Vector2.zero;
            overlayRect.offsetMax = Vector2.zero;
            tutorialPanel.GetComponent<Image>().color = new Color(0.035f, 0.025f, 0.02f, 0.82f);

            GameObject card = new GameObject("TutorialCard_RT", typeof(RectTransform), typeof(Image));
            card.transform.SetParent(overlayRect, false);
            RectTransform cardRect = card.GetComponent<RectTransform>();
            cardRect.anchorMin = new Vector2(0.09f, 0.20f);
            cardRect.anchorMax = new Vector2(0.91f, 0.80f);
            cardRect.offsetMin = Vector2.zero;
            cardRect.offsetMax = Vector2.zero;
            Image cardImage = card.GetComponent<Image>();
            cardImage.sprite = CreateRoundedRectSprite();
            cardImage.type = Image.Type.Sliced;
            cardImage.color = new Color(0.20f, 0.09f, 0.025f, 0.98f);

            TextMeshProUGUI title = CreateText(cardRect, "TutorialTitle_RT", "CÓMO JUGAR", 38f,
                new Color(1f, 0.88f, 0.25f), TextAlignmentOptions.Center,
                new Vector2(0.05f, 0.84f), new Vector2(0.95f, 0.96f), Vector2.zero, Vector2.zero);
            title.fontStyle = FontStyles.Bold;

            string instructions =
                "1. Mantén pulsada una ficha y arrastra en horizontal o vertical.\n\n" +
                "2. Une 3 o más fichas iguales para eliminarlas y sumar puntos.\n\n" +
                "3. Pata: tablero nuevo · Hueso: limpia una fila · Bolsa: reorganiza.\n\n" +
                "4. Alcanza el objetivo antes de que termine el tiempo.";
            TextMeshProUGUI body = CreateText(cardRect, "TutorialBody_RT", instructions, 23f,
                Color.white, TextAlignmentOptions.Left,
                new Vector2(0.10f, 0.24f), new Vector2(0.90f, 0.80f), Vector2.zero, Vector2.zero);
            body.enableWordWrapping = true;
            body.textWrappingMode = TextWrappingModes.Normal;
            body.lineSpacing = 4f;

            Button closeButton = CreateSettingsButton(cardRect, "TutorialClose_RT",
                new Vector2(0.25f, 0.07f), new Vector2(0.75f, 0.19f), out TextMeshProUGUI closeLabel);
            closeLabel.text = "ENTENDIDO";
            closeButton.onClick.AddListener(() => SetTutorialVisible(false));
            tutorialPanel.SetActive(false);
        }

        public void SetTutorialVisible(bool visible)
        {
            if (tutorialPanel == null) return;
            tutorialPanel.SetActive(visible);
            tutorialPanel.transform.SetAsLastSibling();
        }

        private void ShowLevelSelect()
        {
            SetLevelSelectVisible(true);
        }

        public void SetUnlockedLevel(int level)
        {
            unlockedLevel = Mathf.Clamp(level, 1, levelButtons.Count > 0 ? levelButtons.Count : 10);
            UpdateLevelButtons();
        }

        private void UpdateLevelButtons()
        {
            for (int i = 0; i < levelButtons.Count; i++)
            {
                int level = i + 1;
                bool available = level <= unlockedLevel;
                levelButtons[i].interactable = available;
                int stars = PlayerPrefs.GetInt("DogCrush_LevelStars_" + level, 0);
                levelButtonLabels[i].text = available
                    ? $"NIVEL {level}   {(stars > 0 ? "ESTRELLAS " + new string('*', stars) : "-")}"
                    : $"NIVEL {level}   BLOQUEADO";
            }
        }

        public void SetLevelSelectVisible(bool visible)
        {
            if (levelSelectPanel == null) return;
            if (visible) UpdateLevelButtons();
            levelSelectPanel.SetActive(visible);
            levelSelectPanel.transform.SetAsLastSibling();
            OnLevelSelectVisibilityChanged?.Invoke(visible);
        }

        private void SelectLevel(int level)
        {
            if (level > unlockedLevel) return;
            SetLevelSelectVisible(false);
            OnLevelSelected?.Invoke(level);
        }

        private void BuildGameOverPanel(RectTransform canvasRect)
        {
            GameObject overlay = new GameObject("GameOverPanel_RT", typeof(RectTransform), typeof(Image), typeof(CanvasGroup));
            overlay.transform.SetParent(canvasRect, false);
            RectTransform overlayRect = overlay.GetComponent<RectTransform>();
            overlayRect.anchorMin = Vector2.zero;
            overlayRect.anchorMax = Vector2.one;
            overlayRect.offsetMin = Vector2.zero;
            overlayRect.offsetMax = Vector2.zero;

            Image overlayImg = overlay.GetComponent<Image>();
            overlayImg.color = new Color(0.04f, 0.06f, 0.12f, 0.92f);
            gameOverPanel = overlay;

            // Central box
            GameObject centerBox = new GameObject("GOCenterBox_RT", typeof(RectTransform), typeof(Image));
            centerBox.transform.SetParent(overlayRect, false);
            RectTransform centerRect = centerBox.GetComponent<RectTransform>();
            centerRect.anchorMin = new Vector2(0.1f, 0.22f);
            centerRect.anchorMax = new Vector2(0.9f, 0.78f);
            centerRect.offsetMin = Vector2.zero;
            centerRect.offsetMax = Vector2.zero;

            Image boxImg = centerBox.GetComponent<Image>();
            boxImg.color = new Color(0.12f, 0.16f, 0.26f, 0.98f);

            // Title
            resultTitleText = CreateText(centerRect, "GOTitle",
                "TIEMPO AGOTADO", 48f, new Color(1f, 0.4f, 0.35f),
                TextAlignmentOptions.Center,
                new Vector2(0.05f, 0.75f), new Vector2(0.95f, 0.95f),
                Vector2.zero, Vector2.zero);
            resultTitleText.fontStyle = FontStyles.Bold;
            resultTitleText.enableAutoSizing = true;
            resultTitleText.fontSizeMin = 28f;
            resultTitleText.fontSizeMax = 48f;

            // Final score label
            resultLabelText = CreateText(centerRect, "FinalLabel",
                "PUNTUACIÓN", 22f, new Color(0.8f, 0.85f, 0.95f),
                TextAlignmentOptions.Center,
                new Vector2(0.05f, 0.58f), new Vector2(0.95f, 0.72f),
                Vector2.zero, Vector2.zero);

            // Final score display
            finalScoreText = CreateText(centerRect, "FinalScoreText_RT",
                "0", 68f, new Color(1f, 0.92f, 0.25f),
                TextAlignmentOptions.Center,
                new Vector2(0.05f, 0.38f), new Vector2(0.95f, 0.58f),
                Vector2.zero, Vector2.zero);
            finalScoreText.fontStyle = FontStyles.Bold;

            // New Record banner
            newRecordBanner = CreateText(centerRect, "NewRecordBanner_RT",
                "¡NUEVO RÉCORD!", 32f, new Color(0.3f, 0.95f, 0.4f),
                TextAlignmentOptions.Center,
                new Vector2(0.05f, 0.25f), new Vector2(0.95f, 0.36f),
                Vector2.zero, Vector2.zero);
            newRecordBanner.fontStyle = FontStyles.Bold;
            newRecordBanner.gameObject.SetActive(false);

            // Play Again button
            GameObject btnObj = new GameObject("PlayAgainBtn_RT", typeof(RectTransform), typeof(Image), typeof(Button));
            btnObj.transform.SetParent(centerRect, false);
            RectTransform btnRect = btnObj.GetComponent<RectTransform>();
            btnRect.anchorMin = new Vector2(0.12f, 0.05f);
            btnRect.anchorMax = new Vector2(0.88f, 0.22f);
            btnRect.offsetMin = Vector2.zero;
            btnRect.offsetMax = Vector2.zero;

            Image btnImg = btnObj.GetComponent<Image>();
            btnImg.color = new Color(0.2f, 0.78f, 0.38f);

            resultButtonText = CreateText(btnRect, "BtnLabel",
                "JUGAR DE NUEVO", 30f, Color.white,
                TextAlignmentOptions.Center,
                Vector2.zero, Vector2.one,
                Vector2.zero, Vector2.zero);
            resultButtonText.fontStyle = FontStyles.Bold;

            playAgainButton = btnObj.GetComponent<Button>();
            playAgainButton.onClick.AddListener(() =>
            {
                if (lastResultWasVictory)
                    OnNextLevelRequested?.Invoke();
                else
                    OnRestartRequested?.Invoke();
            });
        }

        private TextMeshProUGUI CreateText(RectTransform parent, string name,
            string text, float fontSize, Color color, TextAlignmentOptions alignment,
            Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            RectTransform rect = go.GetComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;

            TextMeshProUGUI tmp = go.GetComponent<TextMeshProUGUI>();
            // Do not depend on a project-global TMP default. WebGL builds must
            // carry an explicit font asset or TMP can fail during first layout.
            tmp.font = TMP_Settings.defaultFontAsset;
            if (tmp.font == null)
            {
                tmp.font = Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");
            }
            tmp.text = text;
            tmp.fontSize = fontSize;
            tmp.color = color;
            tmp.alignment = alignment;
            tmp.enableWordWrapping = false;
            tmp.overflowMode = TextOverflowModes.Overflow;
            return tmp;
        }

        private Sprite LoadUISprite(string name)
        {
            return Resources.Load<Sprite>($"UI/{name}");
        }

        private Image CreateImage(RectTransform parent, string name, Sprite sprite, Vector2 anchorMin, Vector2 anchorMax)
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
            image.preserveAspect = true;
            image.raycastTarget = false;
            return image;
        }

        private Button CreateIconButton(RectTransform parent, string name, string spriteName, Vector2 anchorMin, Vector2 anchorMax)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            RectTransform rect = go.GetComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            Image image = go.GetComponent<Image>();
            image.sprite = LoadUISprite(spriteName);
            image.preserveAspect = true;
            image.raycastTarget = true;
            return go.GetComponent<Button>();
        }

        public void SetSettingsVisible(bool visible)
        {
            if (settingsPanel == null)
            {
                return;
            }

            settingsPanel.SetActive(visible);
            settingsPanel.transform.SetAsLastSibling();
            OnSettingsVisibilityChanged?.Invoke(visible);
        }

        public void UpdateSettingsState(float sfxVolume, bool hapticsEnabled)
        {
            if (soundToggleText != null)
            {
                int percentage = Mathf.RoundToInt(Mathf.Clamp01(sfxVolume) * 100f);
                soundToggleText.text = percentage > 0
                    ? $"SONIDO  {percentage}%"
                    : "SONIDO  APAGADO";
            }

            if (hapticsToggleText != null)
            {
                hapticsToggleText.text = hapticsEnabled
                    ? "VIBRACIÓN  SÍ"
                    : "VIBRACIÓN  NO";
            }

            if (soundToggleButton != null)
            {
                soundToggleButton.image.color = sfxVolume > 0.001f
                    ? new Color(0.12f, 0.58f, 0.82f, 1f)
                    : new Color(0.36f, 0.29f, 0.27f, 1f);
            }

            if (hapticsToggleButton != null)
            {
                hapticsToggleButton.image.color = hapticsEnabled
                    ? new Color(0.16f, 0.68f, 0.39f, 1f)
                    : new Color(0.36f, 0.29f, 0.27f, 1f);
            }
        }

        private void ApplyResponsiveHudLayout()
        {
            if (logoRect == null) return;

            float aspect = (float)Screen.width / Mathf.Max(1, Screen.height);
            bool widerViewport = aspect >= 0.52f;
            if (widerViewport)
            {
                logoRect.anchorMin = new Vector2(0.38f, 0.755f);
                logoRect.anchorMax = new Vector2(0.62f, 0.805f);
            }
            else
            {
                // Compact 30%-scale logo: the board is the visual focus.
                logoRect.anchorMin = new Vector2(0.35f, 0.735f);
                logoRect.anchorMax = new Vector2(0.65f, 0.795f);
            }

            logoRect.offsetMin = Vector2.zero;
            logoRect.offsetMax = Vector2.zero;
            lastHudScreenWidth = Screen.width;
            lastHudScreenHeight = Screen.height;
        }

        private void Update()
        {
            if (lastHudScreenWidth != Screen.width || lastHudScreenHeight != Screen.height)
            {
                ApplyResponsiveHudLayout();
            }

            if (displayedScore != targetScore)
            {
                displayedScore = (int)Mathf.MoveTowards(displayedScore, targetScore,
                    Mathf.Max(100f, Mathf.Abs(targetScore - displayedScore) * 10f * Time.deltaTime));
                RefreshObjectiveText();
            }
        }

        public void UpdateScore(int currentScore)
        {
            targetScore = currentScore;
            if (displayedScore == targetScore)
            {
                RefreshObjectiveText();
            }
        }

        public void SetLevelObjective(int level, int objectiveScore)
        {
            scoreIsObjective = true;
            objectiveLabel = "PUNTOS";
            levelTargetScore = Mathf.Max(1, objectiveScore);
            objectiveProgress = 0;
            if (levelText != null)
            {
                levelText.text = Mathf.Max(1, level).ToString();
            }
            RefreshObjectiveText();
        }

        public void SetCustomObjective(int level, string label, int target, int initialProgress = 0)
        {
            scoreIsObjective = false;
            objectiveLabel = string.IsNullOrWhiteSpace(label) ? "OBJETIVO" : label.ToUpperInvariant();
            levelTargetScore = Mathf.Max(1, target);
            objectiveProgress = Mathf.Clamp(initialProgress, 0, levelTargetScore);
            if (levelText != null)
            {
                levelText.text = Mathf.Max(1, level).ToString();
            }
            RefreshObjectiveText();
        }

        public void UpdateObjectiveProgress(int progress)
        {
            objectiveProgress = Mathf.Clamp(progress, 0, levelTargetScore);
            RefreshObjectiveText();
        }

        public void SetBoosterAvailability(bool shuffle, bool bone, bool food)
        {
            if (movesBoosterButton != null) movesBoosterButton.interactable = shuffle;
            if (boneBoosterButton != null) boneBoosterButton.interactable = bone;
            if (foodBoosterButton != null) foodBoosterButton.interactable = food;
        }

        public void SetBoosterCounts(int shuffle, int bone, int food)
        {
            if (movesCountText != null) movesCountText.text = Mathf.Max(0, shuffle).ToString();
            if (boneCountText != null) boneCountText.text = Mathf.Max(0, bone).ToString();
            if (foodCountText != null) foodCountText.text = Mathf.Max(0, food).ToString();
        }

        public void UpdateLives(int currentLives, int maxLives = 5)
        {
            if (livesText == null) return;
            int clampedMax = Mathf.Max(1, maxLives);
            livesText.text = $"{Mathf.Clamp(currentLives, 0, clampedMax)}/{clampedMax}";
            livesText.color = currentLives <= 1
                ? new Color(1f, 0.38f, 0.30f)
                : Color.white;
        }

        private void RefreshObjectiveText()
        {
            if (scoreText != null)
            {
                int progress = scoreIsObjective ? displayedScore : objectiveProgress;
                scoreText.text = $"{objectiveLabel} {progress:N0} / {levelTargetScore:N0}";
            }
        }

        public void UpdateHighScore(int highScore)
        {
            if (highScoreText != null)
                highScoreText.text = $"{highScore:N0}";
        }

        public void UpdateTimer(float remainingSeconds, float progress01)
        {
            if (timerText != null)
            {
                int seconds = Mathf.CeilToInt(remainingSeconds);
                timerText.text = $"{seconds}s";
            }

            if (timerBarFill != null)
            {
                timerBarFill.fillAmount = Mathf.Clamp01(progress01);

                if (remainingSeconds <= 10f)
                {
                    timerBarFill.color = Color.Lerp(
                        new Color(1f, 0.25f, 0.25f),
                        new Color(1f, 0.65f, 0.15f),
                        Mathf.Sin(Time.time * 10f) * 0.5f + 0.5f);
                }
                else if (remainingSeconds <= 30f)
                {
                    timerBarFill.color = new Color(1f, 0.78f, 0.2f);
                }
                else
                {
                    timerBarFill.color = new Color(0.25f, 0.9f, 0.35f);
                }
            }
        }

        public void UpdateChainInfo(int count, string typeName)
        {
            UpdateChainInfo(count, typeName, Vector3.zero);
        }

        public void UpdateChainInfo(int count, string typeName, Vector3 worldPosition)
        {
            if (chainInfoText == null) return;

            if (count > 1)
            {
                if (chainInfoPanel != null) chainInfoPanel.gameObject.SetActive(true);
                chainInfoText.gameObject.SetActive(true);
                chainInfoText.text = $"<size=16>CADENA</size>\n<size=40>x{count}</size>";

                Color badgeColor;
                if (count >= 9)
                    badgeColor = new Color(0.42f, 0.10f, 0.52f, 0.96f);
                else if (count >= 5)
                    badgeColor = new Color(0.58f, 0.20f, 0.025f, 0.96f);
                else if (count >= 3)
                    badgeColor = new Color(0.12f, 0.36f, 0.12f, 0.96f);
                else
                    badgeColor = new Color(0.20f, 0.09f, 0.025f, 0.96f);

                if (chainInfoPanel != null) chainInfoPanel.color = badgeColor;
                chainInfoText.color = count >= 3
                    ? new Color(1f, 0.94f, 0.48f)
                    : new Color(1f, 0.80f, 0.38f);

                if (portraitContentRect != null && Camera.main != null)
                {
                    Vector2 screenPosition = Camera.main.WorldToScreenPoint(worldPosition);
                    Camera eventCamera = runtimeCanvas.renderMode == RenderMode.ScreenSpaceOverlay
                        ? null
                        : runtimeCanvas.worldCamera;
                    if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                        portraitContentRect,
                        screenPosition,
                        eventCamera,
                        out Vector2 localPosition))
                    {
                        localPosition += new Vector2(72f, 72f);
                        Rect contentRect = portraitContentRect.rect;
                        localPosition.x = Mathf.Clamp(
                            localPosition.x,
                            contentRect.xMin + 90f,
                            contentRect.xMax - 90f);
                        localPosition.y = Mathf.Clamp(
                            localPosition.y,
                            contentRect.yMin + 90f,
                            contentRect.yMax - 90f);
                        chainInfoPanelRect.anchoredPosition = localPosition;
                        chainInfoTextRect.anchoredPosition = localPosition;
                    }
                }

                if (chainPulseRoutine != null) StopCoroutine(chainPulseRoutine);
                chainPulseRoutine = StartCoroutine(PulseChainBadge());
            }
            else
            {
                HideChainInfo();
            }
        }

        private void HideChainInfo()
        {
            if (chainPulseRoutine != null)
            {
                StopCoroutine(chainPulseRoutine);
                chainPulseRoutine = null;
            }

            if (chainInfoPanelRect != null) chainInfoPanelRect.localScale = Vector3.one;
            if (chainInfoTextRect != null) chainInfoTextRect.localScale = Vector3.one;
            if (chainInfoPanel != null) chainInfoPanel.gameObject.SetActive(false);
            if (chainInfoText != null) chainInfoText.gameObject.SetActive(false);
        }

        private IEnumerator PulseChainBadge()
        {
            float elapsed = 0f;
            const float duration = 0.20f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float eased = 1f - Mathf.Pow(1f - t, 3f);
                float scale = Mathf.Lerp(1.18f, 1f, eased);
                if (chainInfoPanelRect != null) chainInfoPanelRect.localScale = Vector3.one * scale;
                if (chainInfoTextRect != null) chainInfoTextRect.localScale = Vector3.one * scale;
                yield return null;
            }

            if (chainInfoPanelRect != null) chainInfoPanelRect.localScale = Vector3.one;
            if (chainInfoTextRect != null) chainInfoTextRect.localScale = Vector3.one;
            chainPulseRoutine = null;
        }

        public void ShowComboBanner(string comboText, Color color)
        {
            if (comboBannerText == null) return;

            if (comboRoutine != null) StopCoroutine(comboRoutine);
            comboBannerText.text = comboText;
            comboBannerText.color = color;
            comboBannerText.gameObject.SetActive(true);

            comboRoutine = StartCoroutine(AnimateComboBanner());
        }

        private IEnumerator AnimateComboBanner()
        {
            float duration = 1.4f;
            float elapsed = 0f;
            Transform tr = comboBannerText.transform;
            Vector3 startScale = Vector3.one * 0.5f;
            Vector3 peakScale = Vector3.one * 1.3f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;
                if (t < 0.15f)
                {
                    tr.localScale = Vector3.Lerp(startScale, peakScale, t / 0.15f);
                }
                else if (t < 0.3f)
                {
                    tr.localScale = Vector3.Lerp(peakScale, Vector3.one, (t - 0.15f) / 0.15f);
                }
                else
                {
                    tr.localScale = Vector3.one;
                    float alpha = Mathf.Lerp(1f, 0f, (t - 0.7f) / 0.3f);
                    comboBannerText.alpha = Mathf.Max(0, alpha);
                }
                yield return null;
            }

            comboBannerText.alpha = 1f;
            comboBannerText.gameObject.SetActive(false);
        }

        public void ShowGameOver(int finalScore, bool isNewRecord)
        {
            ShowLevelResult(false, finalScore, isNewRecord, 0, 0);
        }

        public void ShowLevelResult(bool victory, int finalScore, bool isNewRecord, int stars, int remainingLives = 0)
        {
            lastResultWasVictory = victory;
            if (gameOverPanel != null)
            {
                gameOverPanel.SetActive(true);
                gameOverPanel.transform.SetAsLastSibling();
            }
            if (resultTitleText != null)
            {
                resultTitleText.text = victory ? "¡NIVEL SUPERADO!" : "TIEMPO AGOTADO";
                resultTitleText.color = victory
                    ? new Color(1f, 0.88f, 0.20f)
                    : new Color(1f, 0.4f, 0.35f);
            }
            if (resultLabelText != null)
            {
                resultLabelText.text = victory
                    ? $"ESTRELLAS {new string('*', Mathf.Clamp(stars, 1, 3))}\nPUNTUACIÓN"
                    : remainingLives > 0
                        ? $"PUNTUACIÓN\nVIDAS RESTANTES: {remainingLives}"
                        : "PUNTUACIÓN\nSIN VIDAS";
            }
            if (resultButtonText != null)
            {
                resultButtonText.text = victory
                    ? "SIGUIENTE NIVEL"
                    : remainingLives > 0 ? "JUGAR DE NUEVO" : "RECUPERAR VIDAS";
            }
            if (finalScoreText != null)
            {
                StartCoroutine(AnimateScoreCount(finalScore));
            }
            if (newRecordBanner != null) newRecordBanner.gameObject.SetActive(isNewRecord);
        }

        private IEnumerator AnimateScoreCount(int target)
        {
            float duration = 1.5f;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;
                float easedT = 1f - (1f - t) * (1f - t);
                int displayVal = Mathf.RoundToInt(Mathf.Lerp(0, target, easedT));
                if (finalScoreText != null) finalScoreText.text = $"{displayVal:N0}";
                yield return null;
            }
            if (finalScoreText != null) finalScoreText.text = $"{target:N0}";
        }

        public void HideGameOver()
        {
            if (gameOverPanel != null) gameOverPanel.SetActive(false);
        }
    }
}
