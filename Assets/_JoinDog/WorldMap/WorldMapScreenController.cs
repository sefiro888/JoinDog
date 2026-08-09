using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace JoinDog.App
{
    /// <summary>
    /// Data-driven, vertical campaign map. Art, path, nodes and ambience are
    /// separate layers so new worlds can be added without baking UI into a
    /// single background image.
    /// </summary>
    public sealed class WorldMapScreenController : MonoBehaviour
    {
        public Sprite backgroundSprite;
        public Sprite dogSprite;

        private const float ContentHeight = 11200f;
        private const float ContentWidth = 1080f;
        private CampaignCatalog catalog;
        private ScrollRect scrollRect;
        private RectTransform viewport;
        private RectTransform content;
        private RectTransform dogMarker;
        private RectTransform currentNode;
        private RectTransform selectedNode;
        private GameObject previewPanel;
        private GameObject storePanel;
        private TextMeshProUGUI mapProgressText;
        private TextMeshProUGUI mapWorldNameText;
        private Image mapHeaderImage;
        private Image mapHeaderRibbonImage;
        private TextMeshProUGUI storeBalanceText;
        private TextMeshProUGUI storeStatusText;
        private readonly Dictionary<BoosterKind, TextMeshProUGUI> storeCountTexts =
            new Dictionary<BoosterKind, TextMeshProUGUI>();
        private readonly Dictionary<BoosterKind, Button> storeBuyButtons =
            new Dictionary<BoosterKind, Button>();
        private readonly Dictionary<int, RectTransform> nodeRects = new Dictionary<int, RectTransform>();
        private const int PawCost = 60;
        private const int BoneCost = 75;
        private const int FoodCost = 50;

        private void Awake()
        {
            catalog = CampaignCatalog.LoadOrCreateRuntime();
            Build();
        }

        private void Start()
        {
            StartCoroutine(FocusAndAnimate());
            if (currentNode != null) StartCoroutine(PulseCurrentNode());
        }

        private void Build()
        {
            Canvas canvas = JoinDogUIFactory.CreateCanvas("WorldMapCanvas");
            RectTransform root = canvas.GetComponent<RectTransform>();
            Image background = JoinDogUIFactory.Image(root, "WorldBackground", backgroundSprite,
                Vector2.zero, Vector2.one, Color.white);
            background.preserveAspect = false;
            JoinDogUIFactory.Image(root, "WorldTint", null, Vector2.zero, Vector2.one,
                new Color(0.02f, 0.13f, 0.17f, 0.18f));

            GameObject scrollObject = new GameObject("MapScroll", typeof(RectTransform), typeof(ScrollRect));
            scrollObject.transform.SetParent(root, false);
            RectTransform scrollTransform = scrollObject.GetComponent<RectTransform>();
            scrollTransform.anchorMin = Vector2.zero;
            scrollTransform.anchorMax = new Vector2(1f, 0.91f);
            scrollTransform.offsetMin = Vector2.zero;
            scrollTransform.offsetMax = Vector2.zero;

            GameObject viewportObject = new GameObject("Viewport", typeof(RectTransform), typeof(RectMask2D));
            viewportObject.transform.SetParent(scrollTransform, false);
            viewport = viewportObject.GetComponent<RectTransform>();
            viewport.anchorMin = Vector2.zero;
            viewport.anchorMax = Vector2.one;
            viewport.offsetMin = Vector2.zero;
            viewport.offsetMax = Vector2.zero;

            GameObject contentObject = new GameObject("MapContent", typeof(RectTransform));
            contentObject.transform.SetParent(viewport, false);
            content = contentObject.GetComponent<RectTransform>();
            content.anchorMin = new Vector2(0f, 0f);
            content.anchorMax = new Vector2(1f, 0f);
            content.pivot = new Vector2(0.5f, 0f);
            content.sizeDelta = new Vector2(0f, ContentHeight);
            content.anchoredPosition = Vector2.zero;

            scrollRect = scrollObject.GetComponent<ScrollRect>();
            scrollRect.viewport = viewport;
            scrollRect.content = content;
            scrollRect.horizontal = false;
            scrollRect.vertical = true;
            scrollRect.movementType = ScrollRect.MovementType.Elastic;
            scrollRect.elasticity = 0.07f;
            scrollRect.inertia = true;
            scrollRect.decelerationRate = 0.13f;
            scrollRect.scrollSensitivity = 48f;
            scrollRect.onValueChanged.AddListener(_ => RefreshVisibleZoneTitle());

            BuildZoneBackdrops();
            BuildPathAndNodes();
            BuildDogMarker();
            BuildHeader(root);
        }

        private void BuildZoneBackdrops()
        {
            for (int index = 0; index < catalog.zones.Count; index++)
            {
                CampaignZoneEntry zone = catalog.zones[index];
                CampaignLevelEntry first = catalog.GetLevel(zone.firstLevel);
                CampaignLevelEntry last = catalog.GetLevel(zone.lastLevel);
                if (first == null || last == null) continue;

                float bottom = Mathf.Max(0f, first.mapY - 250f);
                float top = Mathf.Min(ContentHeight, last.mapY + 260f);
                Color atmosphere = Color.Lerp(zone.skyColor, zone.groundColor, 0.58f);
                atmosphere.a = index == 0 ? 0.94f : 1f;
                Image zonePanel = CreateContentImage($"Zone_{zone.id}",
                    new Vector2(0f, (bottom + top) * 0.5f),
                    new Vector2(ContentWidth + 180f, top - bottom + 12f),
                    JoinDogUIFactory.RoundedSprite(), atmosphere);
                zonePanel.type = Image.Type.Sliced;

                bool hasIllustratedBackground = CreateZoneArtBackground(zone, bottom, top);
                CreateZoneAtmosphere(zone, bottom, top, index, hasIllustratedBackground);
                CreateZoneIdentityMark(zone, bottom, top, index);
                CreateZoneBanner(zone, first);
                CreateZoneLandmarks(zone, bottom, top, index, hasIllustratedBackground);
            }
        }

        private bool CreateZoneArtBackground(CampaignZoneEntry zone, float bottom, float top)
        {
            Sprite sprite = WorldMapArtLibrary.LoadBackground(zone.id);
            if (sprite == null) return false;

            float zoneHeight = top - bottom;
            float aspect = sprite.rect.width / Mathf.Max(1f, sprite.rect.height);
            Image artwork = CreateContentImage($"ZoneArtwork_{zone.id}",
                new Vector2(0f, (bottom + top) * 0.5f),
                new Vector2(zoneHeight * aspect, zoneHeight), sprite, Color.white);
            artwork.preserveAspect = true;

            // A very light glaze unifies dynamic nodes and path colors without
            // hiding the illustration underneath.
            CreateContentImage($"ZoneArtworkGlaze_{zone.id}",
                new Vector2(0f, (bottom + top) * 0.5f),
                new Vector2(ContentWidth + 190f, zoneHeight),
                JoinDogUIFactory.RoundedSprite(), new Color(0.01f, 0.10f, 0.075f, 0.10f)).type = Image.Type.Sliced;
            return true;
        }

        private void CreateZoneIdentityMark(CampaignZoneEntry zone, float bottom, float top, int zoneIndex)
        {
            float centerY = (bottom + top) * 0.5f;
            Color[] haloColors =
            {
                new Color(1f, 0.86f, 0.24f, 0.14f),
                new Color(0.24f, 0.92f, 0.52f, 0.13f),
                new Color(0.92f, 0.34f, 1f, 0.16f),
                new Color(0.18f, 0.94f, 1f, 0.16f),
                new Color(0.62f, 0.88f, 1f, 0.18f)
            };
            Color haloColor = haloColors[Mathf.Clamp(zoneIndex, 0, haloColors.Length - 1)];
            CreateContentImage($"ZoneIdentityHalo_{zone.id}", new Vector2(0f, centerY),
                new Vector2(900f, 900f), JoinDogUIFactory.CircleSprite(), haloColor);

            string[] chapters =
            {
                "CAPITULO I  -  PRADERA", "CAPITULO II  -  BOSQUE", "CAPITULO III  -  FESTIVAL",
                "CAPITULO IV  -  COSTA", "CAPITULO V  -  CUMBRES"
            };
            string chapter = chapters[Mathf.Clamp(zoneIndex, 0, chapters.Length - 1)];
            TextMeshProUGUI watermark = JoinDogUIFactory.Text(content, $"ZoneIdentity_{zone.id}", chapter,
                64f, new Color(1f, 1f, 1f, 0.12f), TextAlignmentOptions.Center,
                new Vector2(0.08f, 0f), new Vector2(0.92f, 0f));
            watermark.rectTransform.anchorMin = new Vector2(0.5f, 0f);
            watermark.rectTransform.anchorMax = new Vector2(0.5f, 0f);
            watermark.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            watermark.rectTransform.sizeDelta = new Vector2(900f, 100f);
            watermark.rectTransform.anchoredPosition = new Vector2(0f, centerY + 620f);

            if (zoneIndex == 0)
            {
                CreateContentImage("MeadowSun", new Vector2(385f, centerY + 520f),
                    new Vector2(210f, 210f), JoinDogUIFactory.CircleSprite(),
                    new Color(1f, 0.84f, 0.18f, 0.82f));
                CreateContentImage("MeadowPond", new Vector2(-330f, centerY - 420f),
                    new Vector2(390f, 150f), JoinDogUIFactory.CircleSprite(),
                    new Color(0.18f, 0.72f, 0.92f, 0.50f));
            }
            else if (zoneIndex == 1)
            {
                CreateContentImage("ForestMoon", new Vector2(-365f, centerY + 540f),
                    new Vector2(175f, 175f), JoinDogUIFactory.CircleSprite(),
                    new Color(0.72f, 0.95f, 0.78f, 0.48f));
                for (int ray = 0; ray < 3; ray++)
                {
                    Image shaft = CreateContentImage($"ForestLightShaft_{ray}",
                        new Vector2(-270f + ray * 260f, centerY + 150f),
                        new Vector2(90f, 760f), JoinDogUIFactory.RoundedSprite(),
                        new Color(0.62f, 1f, 0.72f, 0.07f));
                    shaft.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -12f + ray * 10f);
                }
            }
            else if (zoneIndex == 2)
            {
                CreateContentImage("FestivalStageGlow", new Vector2(0f, centerY - 470f),
                    new Vector2(720f, 410f), JoinDogUIFactory.CircleSprite(),
                    new Color(1f, 0.34f, 0.72f, 0.17f));
                for (int beam = 0; beam < 4; beam++)
                {
                    Image spotlight = CreateContentImage($"FestivalSpotlight_{beam}",
                        new Vector2(-360f + beam * 240f, centerY + 120f),
                        new Vector2(74f, 900f), JoinDogUIFactory.RoundedSprite(),
                        new Color(0.42f + beam * 0.12f, 0.72f, 1f, 0.08f));
                    spotlight.rectTransform.localRotation = Quaternion.Euler(0f, 0f, beam % 2 == 0 ? -17f : 17f);
                }
            }
            else if (zoneIndex == 3)
            {
                CreateContentImage("CoastSun", new Vector2(350f, centerY + 520f),
                    new Vector2(190f, 190f), JoinDogUIFactory.CircleSprite(),
                    new Color(1f, 0.86f, 0.26f, 0.80f));
                for (int wave = 0; wave < 4; wave++)
                {
                    Image water = CreateContentImage($"CoastWave_{wave}",
                        new Vector2(wave % 2 == 0 ? -95f : 110f, centerY - 420f + wave * 72f),
                        new Vector2(940f, 54f), JoinDogUIFactory.RoundedSprite(),
                        new Color(0.22f, 0.86f, 0.96f, 0.15f + wave * 0.035f));
                    MapAmbientMotion motion = water.gameObject.AddComponent<MapAmbientMotion>();
                    motion.drift = new Vector2(42f, 2f);
                    motion.speed = 0.16f + wave * 0.025f;
                    motion.phase = wave * 0.8f;
                }
            }
            else
            {
                CreateContentImage("MountainMoon", new Vector2(-350f, centerY + 520f),
                    new Vector2(180f, 180f), JoinDogUIFactory.CircleSprite(),
                    new Color(0.78f, 0.94f, 1f, 0.68f));
                for (int peak = 0; peak < 4; peak++)
                {
                    Image mountain = CreateContentImage($"MountainPeak_{peak}",
                        new Vector2(-360f + peak * 240f, centerY - 380f + (peak % 2) * 95f),
                        new Vector2(360f, 520f), JoinDogUIFactory.RoundedSprite(),
                        new Color(0.16f + peak * 0.025f, 0.30f + peak * 0.035f, 0.52f + peak * 0.04f, 0.42f));
                    mountain.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 45f);
                }
            }
        }

        private void CreateZoneAtmosphere(CampaignZoneEntry zone, float bottom, float top, int zoneIndex,
            bool hasIllustratedBackground)
        {
            if (!hasIllustratedBackground)
                CreateZoneColorBands(zone, bottom, top, zoneIndex);

            if (zoneIndex == 1)
            {
                CreateForestAtmosphere(zone, bottom, top, hasIllustratedBackground);
            }
            else if (zoneIndex == 2)
            {
                CreateFestivalAtmosphere(zone, bottom, top);
            }
            else if (zoneIndex == 3)
            {
                CreateCoastAtmosphere(zone, bottom, top);
            }
            else if (zoneIndex == 4)
            {
                CreateMountainAtmosphere(zone, bottom, top);
            }

            if (zoneIndex > 0)
            {
                CreateZoneEntrance(zone, bottom + 130f, zoneIndex);
            }
        }

        private void CreateZoneColorBands(CampaignZoneEntry zone, float bottom, float top, int zoneIndex)
        {
            const int bandCount = 9;
            float height = (top - bottom) / bandCount + 4f;
            for (int i = 0; i < bandCount; i++)
            {
                float t = i / (bandCount - 1f);
                Color color;
                if (zoneIndex == 1)
                {
                    Color forestFloor = new Color(0.025f, 0.15f, 0.095f, 1f);
                    Color forestSky = new Color(0.08f, 0.34f, 0.31f, 1f);
                    color = Color.Lerp(forestFloor, forestSky, t);
                }
                else if (zoneIndex == 2)
                {
                    Color festivalFloor = new Color(0.11f, 0.055f, 0.24f, 1f);
                    Color festivalSky = new Color(0.26f, 0.20f, 0.53f, 1f);
                    color = Color.Lerp(festivalFloor, festivalSky, t);
                }
                else if (zoneIndex == 3)
                {
                    Color coastFloor = new Color(0.82f, 0.54f, 0.18f, 1f);
                    Color coastSky = new Color(0.08f, 0.64f, 0.82f, 1f);
                    color = Color.Lerp(coastFloor, coastSky, t);
                }
                else if (zoneIndex == 4)
                {
                    Color mountainFloor = new Color(0.18f, 0.28f, 0.48f, 1f);
                    Color mountainSky = new Color(0.48f, 0.68f, 0.88f, 1f);
                    color = Color.Lerp(mountainFloor, mountainSky, t);
                }
                else
                {
                    color = Color.Lerp(zone.groundColor, zone.skyColor, t);
                    color.a = 0.34f;
                }
                CreateContentImage($"ZoneBand_{zone.id}_{i}",
                    new Vector2(0f, bottom + height * (i + 0.5f)),
                    new Vector2(ContentWidth + 190f, height + 6f),
                    JoinDogUIFactory.RoundedSprite(), color).type = Image.Type.Sliced;
            }
        }

        private void CreateForestAtmosphere(CampaignZoneEntry zone, float bottom, float top,
            bool hasIllustratedBackground)
        {
            Color edge = new Color(0.008f, 0.075f, 0.052f, 0.95f);
            Color edgeColor = hasIllustratedBackground
                ? new Color(edge.r, edge.g, edge.b, 0.18f)
                : edge;
            CreateContentImage("ForestVignetteLeft", new Vector2(-505f, (bottom + top) * 0.5f),
                new Vector2(220f, top - bottom), JoinDogUIFactory.RoundedSprite(), edgeColor).type = Image.Type.Sliced;
            CreateContentImage("ForestVignetteRight", new Vector2(505f, (bottom + top) * 0.5f),
                new Vector2(220f, top - bottom), JoinDogUIFactory.RoundedSprite(), edgeColor).type = Image.Type.Sliced;

            if (!hasIllustratedBackground)
            {
                for (int i = 0; i < 12; i++)
                {
                    float y = Mathf.Lerp(bottom + 80f, top - 80f, i / 11f);
                    float x = (i % 2 == 0 ? -1f : 1f) * (445f + (i % 3) * 24f);
                    CreateContentImage($"ForestTrunk_{i}", new Vector2(x, y - 45f),
                        new Vector2(70f, 260f), JoinDogUIFactory.RoundedSprite(),
                        new Color(0.20f, 0.09f, 0.045f, 0.96f)).type = Image.Type.Sliced;
                    CreateContentImage($"ForestCrownBack_{i}", new Vector2(x, y + 78f),
                        new Vector2(270f, 235f), JoinDogUIFactory.CircleSprite(),
                        new Color(0.025f, 0.20f + (i % 3) * 0.025f, 0.10f, 0.98f));
                    CreateContentImage($"ForestCrownLight_{i}", new Vector2(x - 34f, y + 112f),
                        new Vector2(150f, 125f), JoinDogUIFactory.CircleSprite(),
                        new Color(0.10f, 0.42f, 0.18f, 0.72f));
                }
            }

            int mistCount = hasIllustratedBackground ? 3 : 5;
            for (int fog = 0; fog < mistCount; fog++)
            {
                float denominator = Mathf.Max(1f, mistCount - 1f);
                float y = Mathf.Lerp(bottom + 340f, top - 250f, fog / denominator);
                Image mist = CreateContentImage($"ForestMist_{fog}",
                    new Vector2(fog % 2 == 0 ? -120f : 150f, y),
                    new Vector2(850f, 85f), JoinDogUIFactory.RoundedSprite(),
                    new Color(0.55f, 0.88f, 0.72f, hasIllustratedBackground ? 0.055f : 0.10f));
                mist.type = Image.Type.Sliced;
                MapAmbientMotion motion = mist.gameObject.AddComponent<MapAmbientMotion>();
                motion.drift = new Vector2(34f, 3f);
                motion.speed = 0.13f + fog * 0.018f;
                motion.phase = fog * 1.2f;
            }

            for (int i = 0; i < 22; i++)
            {
                float y = Mathf.Lerp(bottom + 170f, top - 120f, i / 21f);
                float x = ((i * 137) % 820) - 410f;
                Color glow = i % 3 == 0
                    ? new Color(0.55f, 1f, 0.42f, 0.92f)
                    : new Color(1f, 0.84f, 0.24f, 0.84f);
                Image firefly = CreateContentImage($"Firefly_{i}", new Vector2(x, y),
                    new Vector2(13f, 13f), JoinDogUIFactory.CircleSprite(), glow);
                MapAmbientMotion motion = firefly.gameObject.AddComponent<MapAmbientMotion>();
                motion.drift = new Vector2(10f, 18f);
                motion.speed = 0.45f + (i % 5) * 0.07f;
                motion.phase = i * 0.53f;
            }
        }

        private void CreateFestivalAtmosphere(CampaignZoneEntry zone, float bottom, float top)
        {
            CreateContentImage("FestivalNight", new Vector2(0f, (bottom + top) * 0.5f),
                new Vector2(ContentWidth + 190f, top - bottom), JoinDogUIFactory.RoundedSprite(),
                new Color(0.08f, 0.035f, 0.20f, 0.58f)).type = Image.Type.Sliced;
            CreateContentImage("FestivalHorizonGlow", new Vector2(0f, bottom + 420f),
                new Vector2(980f, 520f), JoinDogUIFactory.CircleSprite(),
                new Color(0.68f, 0.22f, 0.76f, 0.18f));

            for (int garland = 0; garland < 6; garland++)
            {
                float y = Mathf.Lerp(bottom + 280f, top - 180f, garland / 5f);
                CreateFestivalGarland(garland, y, garland % 2 == 0);
            }

            Color[] confettiColors =
            {
                new Color(1f, 0.30f, 0.45f, 0.95f),
                new Color(1f, 0.78f, 0.18f, 0.95f),
                new Color(0.25f, 0.80f, 1f, 0.95f),
                new Color(0.48f, 1f, 0.48f, 0.95f)
            };
            for (int i = 0; i < 34; i++)
            {
                float y = Mathf.Lerp(bottom + 140f, top - 90f, i / 33f);
                float x = ((i * 173) % 900) - 450f;
                Image confetti = CreateContentImage($"FestivalConfetti_{i}", new Vector2(x, y),
                    new Vector2(i % 2 == 0 ? 10f : 18f, i % 2 == 0 ? 24f : 10f),
                    JoinDogUIFactory.RoundedSprite(), confettiColors[i % confettiColors.Length]);
                MapAmbientMotion motion = confetti.gameObject.AddComponent<MapAmbientMotion>();
                motion.drift = new Vector2(6f, 12f);
                motion.speed = 0.30f + (i % 6) * 0.04f;
                motion.phase = i * 0.41f;
                motion.rotationAmplitude = 12f;
            }
        }

        private void CreateCoastAtmosphere(CampaignZoneEntry zone, float bottom, float top)
        {
            CreateContentImage("CoastOcean", new Vector2(0f, (bottom + top) * 0.5f),
                new Vector2(ContentWidth + 190f, top - bottom), JoinDogUIFactory.RoundedSprite(),
                new Color(0.02f, 0.46f, 0.68f, 0.24f)).type = Image.Type.Sliced;
            for (int i = 0; i < 10; i++)
            {
                float y = Mathf.Lerp(bottom + 120f, top - 100f, i / 9f);
                Image foam = CreateContentImage($"CoastFoam_{i}",
                    new Vector2(i % 2 == 0 ? -130f : 145f, y),
                    new Vector2(880f, 34f), JoinDogUIFactory.RoundedSprite(),
                    new Color(0.82f, 1f, 1f, 0.18f));
                MapAmbientMotion motion = foam.gameObject.AddComponent<MapAmbientMotion>();
                motion.drift = new Vector2(55f, 2f);
                motion.speed = 0.12f + (i % 4) * 0.025f;
                motion.phase = i * 0.71f;
            }
            for (int i = 0; i < 18; i++)
            {
                float y = Mathf.Lerp(bottom + 160f, top - 120f, i / 17f);
                float x = ((i * 149) % 880) - 440f;
                Image bubble = CreateContentImage($"CoastBubble_{i}", new Vector2(x, y),
                    Vector2.one * (12f + i % 3 * 5f), JoinDogUIFactory.CircleSprite(),
                    new Color(0.72f, 1f, 0.98f, 0.42f));
                MapAmbientMotion motion = bubble.gameObject.AddComponent<MapAmbientMotion>();
                motion.drift = new Vector2(8f, 24f);
                motion.speed = 0.28f + (i % 5) * 0.04f;
                motion.phase = i * 0.52f;
            }
        }

        private void CreateMountainAtmosphere(CampaignZoneEntry zone, float bottom, float top)
        {
            CreateContentImage("MountainNight", new Vector2(0f, (bottom + top) * 0.5f),
                new Vector2(ContentWidth + 190f, top - bottom), JoinDogUIFactory.RoundedSprite(),
                new Color(0.035f, 0.075f, 0.19f, 0.48f)).type = Image.Type.Sliced;
            for (int i = 0; i < 26; i++)
            {
                float y = Mathf.Lerp(bottom + 110f, top - 90f, i / 25f);
                float x = ((i * 181) % 920) - 460f;
                float size = 10f + (i % 4) * 5f;
                Image snow = CreateContentImage($"MountainSnow_{i}", new Vector2(x, y),
                    Vector2.one * size, JoinDogUIFactory.CircleSprite(),
                    new Color(0.86f, 0.97f, 1f, 0.76f));
                MapAmbientMotion motion = snow.gameObject.AddComponent<MapAmbientMotion>();
                motion.drift = new Vector2(18f, -28f);
                motion.speed = 0.22f + (i % 6) * 0.035f;
                motion.phase = i * 0.37f;
            }
            for (int aurora = 0; aurora < 3; aurora++)
            {
                Image ribbon = CreateContentImage($"Aurora_{aurora}",
                    new Vector2(-120f + aurora * 120f, bottom + 560f + aurora * 410f),
                    new Vector2(920f, 80f), JoinDogUIFactory.RoundedSprite(),
                    aurora % 2 == 0 ? new Color(0.24f, 1f, 0.72f, 0.12f) :
                        new Color(0.46f, 0.58f, 1f, 0.12f));
                ribbon.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -8f + aurora * 7f);
                MapAmbientMotion motion = ribbon.gameObject.AddComponent<MapAmbientMotion>();
                motion.drift = new Vector2(35f, 8f);
                motion.speed = 0.11f + aurora * 0.02f;
                motion.phase = aurora * 1.3f;
            }
        }

        private void CreateFestivalGarland(int index, float y, bool rising)
        {
            Vector2 start = new Vector2(-530f, y + (rising ? -25f : 25f));
            Vector2 end = new Vector2(530f, y + (rising ? 25f : -25f));
            CreatePathLine($"GarlandWire_{index}", start, end, 7f,
                new Color(0.035f, 0.025f, 0.08f, 0.88f));
            Color[] bulbs =
            {
                new Color(1f, 0.25f, 0.42f, 1f),
                new Color(1f, 0.80f, 0.20f, 1f),
                new Color(0.25f, 0.78f, 1f, 1f),
                new Color(0.42f, 1f, 0.48f, 1f)
            };
            for (int bulb = 0; bulb < 9; bulb++)
            {
                float t = (bulb + 0.5f) / 9f;
                Vector2 position = Vector2.Lerp(start, end, t) + Vector2.down * 18f;
                CreateContentImage($"GarlandGlow_{index}_{bulb}", position,
                    new Vector2(52f, 52f), JoinDogUIFactory.CircleSprite(),
                    new Color(bulbs[bulb % bulbs.Length].r, bulbs[bulb % bulbs.Length].g,
                        bulbs[bulb % bulbs.Length].b, 0.16f));
                Image light = CreateContentImage($"GarlandBulb_{index}_{bulb}", position,
                    new Vector2(23f, 30f), JoinDogUIFactory.CircleSprite(), bulbs[bulb % bulbs.Length]);
                MapAmbientMotion motion = light.gameObject.AddComponent<MapAmbientMotion>();
                motion.drift = new Vector2(1.5f, 4f);
                motion.speed = 0.55f + bulb * 0.025f;
                motion.phase = index + bulb * 0.44f;
            }
        }

        private void CreateZoneEntrance(CampaignZoneEntry zone, float y, int zoneIndex)
        {
            Sprite illustratedEntrance = WorldMapArtLibrary.LoadEntrance(zone.id);
            if (illustratedEntrance != null)
            {
                CreateContentImage($"ZoneGateGlow_{zone.id}", new Vector2(0f, y + 35f),
                    new Vector2(910f, 650f), JoinDogUIFactory.CircleSprite(),
                    new Color(zone.accentColor.r, zone.accentColor.g, zone.accentColor.b, 0.13f));
                Image entrance = CreateContentImage($"ZoneGateArtwork_{zone.id}", new Vector2(0f, y + 15f),
                    new Vector2(940f, 940f), illustratedEntrance, Color.white);
                entrance.preserveAspect = true;
                MapAmbientMotion motion = entrance.gameObject.AddComponent<MapAmbientMotion>();
                motion.drift = new Vector2(0f, 3f);
                motion.speed = 0.24f;
                motion.phase = zoneIndex * 0.8f;
                motion.rotationAmplitude = 0.25f;
                return;
            }

            Color dark = zoneIndex == 1 ? new Color(0.07f, 0.20f, 0.10f, 1f) :
                zoneIndex == 2 ? new Color(0.24f, 0.10f, 0.40f, 1f) :
                zoneIndex == 3 ? new Color(0.04f, 0.34f, 0.42f, 1f) :
                new Color(0.10f, 0.18f, 0.38f, 1f);
            CreateContentImage($"ZoneGateShadow_{zone.id}", new Vector2(9f, y - 16f),
                new Vector2(800f, 126f), JoinDogUIFactory.RoundedSprite(),
                new Color(0.01f, 0.005f, 0.02f, 0.66f)).type = Image.Type.Sliced;
            Image gate = CreateContentImage($"ZoneGate_{zone.id}", new Vector2(0f, y),
                new Vector2(800f, 126f), JoinDogUIFactory.RoundedSprite(), dark);
            gate.type = Image.Type.Sliced;
            Outline outline = gate.gameObject.AddComponent<Outline>();
            outline.effectColor = zone.accentColor;
            outline.effectDistance = new Vector2(6f, -6f);
            CreateContentImage($"ZonePostLeft_{zone.id}", new Vector2(-408f, y - 18f),
                new Vector2(78f, 210f), JoinDogUIFactory.RoundedSprite(), dark).type = Image.Type.Sliced;
            CreateContentImage($"ZonePostRight_{zone.id}", new Vector2(408f, y - 18f),
                new Vector2(78f, 210f), JoinDogUIFactory.RoundedSprite(), dark).type = Image.Type.Sliced;
            JoinDogUIFactory.Text(gate.rectTransform, "GateEyebrow", "NUEVA ZONA", 17f,
                new Color(1f, 0.90f, 0.58f, 1f), TextAlignmentOptions.Center,
                new Vector2(0.08f, 0.59f), new Vector2(0.92f, 0.88f));
            JoinDogUIFactory.Text(gate.rectTransform, "GateTitle", zone.displayName, 34f,
                Color.white, TextAlignmentOptions.Center,
                new Vector2(0.05f, 0.16f), new Vector2(0.95f, 0.64f));
        }

        private void CreateZoneBanner(CampaignZoneEntry zone, CampaignLevelEntry first)
        {
            float x = first.mapX < 0.5f ? 275f : -275f;
            float y = first.mapY + 22f;
            Image shadow = CreateContentImage($"ZoneBannerShadow_{zone.id}", new Vector2(x + 7f, y - 8f),
                new Vector2(460f, 104f), JoinDogUIFactory.RoundedSprite(), new Color(0.08f, 0.03f, 0.02f, 0.42f));
            shadow.type = Image.Type.Sliced;
            Image banner = CreateContentImage($"ZoneBanner_{zone.id}", new Vector2(x, y),
                new Vector2(460f, 104f), JoinDogUIFactory.RoundedSprite(), new Color(0.16f, 0.05f, 0.025f, 0.95f));
            banner.type = Image.Type.Sliced;
            Outline outline = banner.gameObject.AddComponent<Outline>();
            outline.effectColor = zone.accentColor;
            outline.effectDistance = new Vector2(4f, -4f);
            JoinDogUIFactory.Text(banner.rectTransform, "ZoneTitle", zone.displayName, 27f,
                zone.accentColor, TextAlignmentOptions.Center,
                new Vector2(0.05f, 0.42f), new Vector2(0.95f, 0.90f));
            JoinDogUIFactory.Text(banner.rectTransform, "ZoneSubtitle", zone.subtitle.ToUpperInvariant(), 16f,
                new Color(1f, 0.94f, 0.78f, 1f), TextAlignmentOptions.Center,
                new Vector2(0.08f, 0.12f), new Vector2(0.92f, 0.44f));
        }

        private void CreateZoneLandmarks(CampaignZoneEntry zone, float bottom, float top, int zoneIndex,
            bool hasIllustratedBackground)
        {
            Random.State oldState = Random.state;
            Random.InitState(4100 + zoneIndex * 97);
            int landmarkCount = zoneIndex == 1 && hasIllustratedBackground ? 0 : zoneIndex == 0 ? 7 : 11;
            for (int i = 0; i < landmarkCount; i++)
            {
                bool left = i % 2 == 0;
                float x = (left ? -1f : 1f) * Random.Range(405f, 470f);
                float y = Random.Range(bottom + 210f, top - 150f);
                if (zoneIndex == 0) CreateFlowerPatch($"Flowers_{i}", new Vector2(x, y), zone, i);
                else if (zoneIndex == 1) CreateTree($"Tree_{i}", new Vector2(x, y), zone, i);
                else if (zoneIndex == 2) CreateFestivalPost($"Festival_{i}", new Vector2(x, y), zone, i);
                else if (zoneIndex == 3) CreateCoastMarker($"Coast_{i}", new Vector2(x, y), i);
                else CreateMountainMarker($"Mountain_{i}", new Vector2(x, y), i);
            }

            if (zoneIndex == 1 && !hasIllustratedBackground)
            {
                for (int i = 0; i < 6; i++)
                {
                    float x = (i % 2 == 0 ? -1f : 1f) * Random.Range(300f, 405f);
                    float y = Mathf.Lerp(bottom + 300f, top - 240f, i / 5f);
                    CreateForestCluster($"ForestCluster_{i}", new Vector2(x, y), i);
                }
            }
            else if (zoneIndex == 2)
            {
                for (int i = 0; i < 5; i++)
                {
                    float x = (i % 2 == 0 ? -1f : 1f) * Random.Range(325f, 415f);
                    float y = Mathf.Lerp(bottom + 350f, top - 270f, i / 4f);
                    CreateFestivalTent($"FestivalTent_{i}", new Vector2(x, y), i);
                }
            }

            for (int i = 0; i < 4; i++)
            {
                float x = (i % 2 == 0 ? -1f : 1f) * Random.Range(330f, 410f);
                float y = Mathf.Lerp(bottom + 390f, top - 260f, i / 3f);
                CreateSparkCluster(zone, zoneIndex, i, new Vector2(x, y));
            }
            Random.state = oldState;
        }

        private void CreateForestCluster(string name, Vector2 position, int index)
        {
            RectTransform cluster = CreateContentContainer(name, position, new Vector2(170f, 120f));
            JoinDogUIFactory.Image(cluster, "RockBack", JoinDogUIFactory.CircleSprite(),
                new Vector2(0.06f, 0.05f), new Vector2(0.62f, 0.60f),
                new Color(0.20f, 0.30f, 0.25f, 0.96f));
            JoinDogUIFactory.Image(cluster, "RockLight", JoinDogUIFactory.CircleSprite(),
                new Vector2(0.16f, 0.18f), new Vector2(0.46f, 0.48f),
                new Color(0.43f, 0.58f, 0.42f, 0.68f));
            for (int i = 0; i < 3; i++)
            {
                float x = 0.46f + i * 0.15f;
                JoinDogUIFactory.Panel(cluster, $"MushroomStem_{i}",
                    new Vector2(x + 0.04f, 0.06f), new Vector2(x + 0.10f, 0.40f),
                    new Color(0.92f, 0.76f, 0.50f, 0.96f));
                JoinDogUIFactory.Image(cluster, $"MushroomCap_{i}", JoinDogUIFactory.CircleSprite(),
                    new Vector2(x, 0.30f), new Vector2(x + 0.16f, 0.58f),
                    i % 2 == 0 ? new Color(0.92f, 0.26f, 0.20f, 1f) : new Color(0.90f, 0.58f, 0.14f, 1f));
            }
            AddLandmarkMotion(cluster, index, 2f, 3f, 0.8f);
        }

        private void CreateFestivalTent(string name, Vector2 position, int index)
        {
            RectTransform tent = CreateContentContainer(name, position, new Vector2(210f, 170f));
            Color main = index % 2 == 0
                ? new Color(0.88f, 0.16f, 0.35f, 0.98f)
                : new Color(0.22f, 0.58f, 0.94f, 0.98f);
            Color stripe = new Color(1f, 0.82f, 0.22f, 0.98f);
            JoinDogUIFactory.Panel(tent, "TentBody", new Vector2(0.08f, 0.05f),
                new Vector2(0.92f, 0.62f), new Color(main.r * 0.78f, main.g * 0.78f, main.b * 0.78f, 1f));
            for (int i = 0; i < 5; i++)
            {
                float min = 0.08f + i * 0.168f;
                JoinDogUIFactory.Panel(tent, $"Awning_{i}", new Vector2(min, 0.54f),
                    new Vector2(min + 0.17f, 0.74f), i % 2 == 0 ? main : stripe);
            }
            JoinDogUIFactory.Panel(tent, "Roof", new Vector2(0.19f, 0.68f),
                new Vector2(0.81f, 0.92f), main);
            JoinDogUIFactory.Image(tent, "TopLight", JoinDogUIFactory.CircleSprite(),
                new Vector2(0.43f, 0.82f), new Vector2(0.57f, 0.99f), stripe);
            AddLandmarkMotion(tent, index, 3f, 4f, 1.2f);
        }

        private void CreateTree(string name, Vector2 position, CampaignZoneEntry zone, int index)
        {
            RectTransform tree = CreateContentContainer(name, position, new Vector2(180f, 230f));
            Image trunk = JoinDogUIFactory.Panel(tree, "Trunk", new Vector2(0.42f, 0.02f),
                new Vector2(0.58f, 0.55f), new Color(0.38f, 0.16f, 0.07f, 0.90f));
            trunk.raycastTarget = false;
            Color crown = Color.Lerp(zone.groundColor, new Color(0.12f, 0.58f, 0.22f), 0.45f);
            crown.a = 0.92f;
            JoinDogUIFactory.Image(tree, "CrownLeft", JoinDogUIFactory.CircleSprite(),
                new Vector2(0.02f, 0.38f), new Vector2(0.60f, 0.90f), crown);
            JoinDogUIFactory.Image(tree, "CrownRight", JoinDogUIFactory.CircleSprite(),
                new Vector2(0.40f, 0.36f), new Vector2(0.98f, 0.88f), crown);
            JoinDogUIFactory.Image(tree, "CrownTop", JoinDogUIFactory.CircleSprite(),
                new Vector2(0.22f, 0.52f), new Vector2(0.78f, 1.0f), Color.Lerp(crown, Color.white, 0.10f));
            AddLandmarkMotion(tree, index, 5f, 5f, 1.8f);
        }

        private void CreateFlowerPatch(string name, Vector2 position, CampaignZoneEntry zone, int index)
        {
            RectTransform patch = CreateContentContainer(name, position, new Vector2(150f, 110f));
            Color[] colors =
            {
                zone.accentColor,
                new Color(0.95f, 0.28f, 0.48f, 1f),
                new Color(0.45f, 0.28f, 0.90f, 1f)
            };
            for (int i = 0; i < 3; i++)
            {
                float x = 0.12f + i * 0.29f;
                Image stem = JoinDogUIFactory.Panel(patch, $"Stem_{i}", new Vector2(x + 0.10f, 0.06f),
                    new Vector2(x + 0.14f, 0.58f), new Color(0.12f, 0.52f, 0.18f, 0.90f));
                stem.raycastTarget = false;
                JoinDogUIFactory.Image(patch, $"Flower_{i}", JoinDogUIFactory.CircleSprite(),
                    new Vector2(x, 0.46f), new Vector2(x + 0.25f, 0.82f), colors[(index + i) % colors.Length]);
                JoinDogUIFactory.Image(patch, $"Center_{i}", JoinDogUIFactory.CircleSprite(),
                    new Vector2(x + 0.085f, 0.57f), new Vector2(x + 0.165f, 0.69f),
                    new Color(1f, 0.80f, 0.14f, 1f));
            }
            AddLandmarkMotion(patch, index, 3f, 4f, 2.5f);
        }

        private void CreateFestivalPost(string name, Vector2 position, CampaignZoneEntry zone, int index)
        {
            RectTransform post = CreateContentContainer(name, position, new Vector2(155f, 225f));
            Image pole = JoinDogUIFactory.Panel(post, "Pole", new Vector2(0.46f, 0.03f),
                new Vector2(0.54f, 0.90f), new Color(0.74f, 0.45f, 0.12f, 0.94f));
            pole.raycastTarget = false;
            Image flag = JoinDogUIFactory.Panel(post, "Flag", new Vector2(0.50f, 0.56f),
                new Vector2(0.98f, 0.84f), index % 2 == 0
                    ? new Color(0.86f, 0.18f, 0.32f, 0.95f)
                    : new Color(0.20f, 0.58f, 0.92f, 0.95f));
            flag.raycastTarget = false;
            JoinDogUIFactory.Image(post, "Lamp", JoinDogUIFactory.CircleSprite(),
                new Vector2(0.34f, 0.80f), new Vector2(0.66f, 1f), zone.accentColor);
            AddLandmarkMotion(post, index, 4f, 5f, 2f);
        }

        private void CreateCoastMarker(string name, Vector2 position, int index)
        {
            RectTransform marker = CreateContentContainer(name, position, new Vector2(180f, 170f));
            JoinDogUIFactory.Panel(marker, "PalmTrunk", new Vector2(0.46f, 0.05f),
                new Vector2(0.57f, 0.72f), new Color(0.62f, 0.34f, 0.12f, 0.96f));
            for (int leaf = 0; leaf < 5; leaf++)
            {
                Image palmLeaf = JoinDogUIFactory.Image(marker, $"PalmLeaf_{leaf}", JoinDogUIFactory.RoundedSprite(),
                    new Vector2(0.34f, 0.63f), new Vector2(0.78f, 0.78f),
                    leaf % 2 == 0 ? new Color(0.10f, 0.70f, 0.38f, 1f) : new Color(0.18f, 0.82f, 0.48f, 1f));
                palmLeaf.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -62f + leaf * 31f);
            }
            JoinDogUIFactory.Image(marker, "Shell", JoinDogUIFactory.CircleSprite(),
                new Vector2(0.08f, 0.04f), new Vector2(0.36f, 0.25f),
                new Color(1f, 0.74f, 0.42f, 0.96f));
            AddLandmarkMotion(marker, index, 5f, 4f, 2.5f);
        }

        private void CreateMountainMarker(string name, Vector2 position, int index)
        {
            RectTransform marker = CreateContentContainer(name, position, new Vector2(190f, 190f));
            Image peak = JoinDogUIFactory.Panel(marker, "Peak", new Vector2(0.18f, 0.08f),
                new Vector2(0.82f, 0.76f), new Color(0.18f, 0.32f, 0.56f, 0.96f));
            peak.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 45f);
            Image snow = JoinDogUIFactory.Panel(marker, "SnowCap", new Vector2(0.34f, 0.58f),
                new Vector2(0.68f, 0.91f), new Color(0.86f, 0.97f, 1f, 0.98f));
            snow.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 45f);
            for (int crystal = 0; crystal < 3; crystal++)
            {
                Image shard = JoinDogUIFactory.Panel(marker, $"Crystal_{crystal}",
                    new Vector2(0.10f + crystal * 0.18f, 0.03f), new Vector2(0.20f + crystal * 0.18f, 0.34f),
                    new Color(0.40f, 0.88f, 1f, 0.94f));
                shard.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -18f + crystal * 15f);
            }
            AddLandmarkMotion(marker, index, 3f, 5f, 1.2f);
        }

        private static void AddLandmarkMotion(RectTransform rect, int index,
            float driftX, float driftY, float rotation)
        {
            MapAmbientMotion motion = rect.gameObject.AddComponent<MapAmbientMotion>();
            motion.drift = new Vector2(driftX, driftY);
            motion.speed = 0.35f + (index % 3) * 0.08f;
            motion.phase = index * 0.81f;
            motion.rotationAmplitude = rotation;
        }

        private void CreateSparkCluster(CampaignZoneEntry zone, int zoneIndex, int index, Vector2 center)
        {
            for (int dot = 0; dot < 3; dot++)
            {
                float angle = (dot / 3f) * Mathf.PI * 2f;
                Vector2 position = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * 24f;
                Image image = CreateContentImage($"Spark_{zoneIndex}_{index}_{dot}", position,
                    new Vector2(18f, 18f), JoinDogUIFactory.CircleSprite(), zone.accentColor);
                MapAmbientMotion motion = image.gameObject.AddComponent<MapAmbientMotion>();
                motion.drift = new Vector2(4f, 8f);
                motion.speed = 0.8f + dot * 0.13f;
                motion.phase = index + dot * 1.7f;
                motion.rotationAmplitude = 0f;
            }
        }

        private void BuildHeader(RectTransform root)
        {
            Image shadow = JoinDogUIFactory.Panel(root, "MapHeaderShadow",
                new Vector2(0.035f, 0.901f), new Vector2(0.965f, 0.979f),
                new Color(0.03f, 0.02f, 0.02f, 0.45f));
            shadow.rectTransform.anchoredPosition = new Vector2(8f, -12f);
            Image header = JoinDogUIFactory.Panel(root, "MapHeader",
                new Vector2(0.035f, 0.908f), new Vector2(0.965f, 0.986f),
                new Color(0.12f, 0.035f, 0.018f, 0.98f));
            mapHeaderImage = header;
            Outline outline = header.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(1f, 0.66f, 0.14f, 1f);
            outline.effectDistance = new Vector2(4f, -4f);
            mapHeaderRibbonImage = JoinDogUIFactory.Panel(header.rectTransform, "HeaderRibbon",
                new Vector2(0.17f, 0.70f), new Vector2(0.83f, 0.88f),
                new Color(0.10f, 0.46f, 0.50f, 1f));

            Button back = JoinDogUIFactory.Button(header.rectTransform, "Back", "<",
                new Vector2(0.025f, 0.15f), new Vector2(0.16f, 0.83f),
                new Color(0.06f, 0.42f, 0.64f, 1f));
            back.onClick.AddListener(() => AppServices.Instance.GoToMainMenu());

            JoinDogUIFactory.Text(header.rectTransform, "WorldName", catalog.displayName, 33f,
                new Color(1f, 0.78f, 0.18f), TextAlignmentOptions.Center,
                new Vector2(0.17f, 0.42f), new Vector2(0.83f, 0.82f));
            mapProgressText = JoinDogUIFactory.Text(header.rectTransform, "Progress", string.Empty,
                19f, Color.white, TextAlignmentOptions.Center,
                new Vector2(0.18f, 0.12f), new Vector2(0.82f, 0.43f));
            RefreshMapProgress();

            mapWorldNameText = header.rectTransform.Find("WorldName")?.GetComponent<TextMeshProUGUI>();
            RefreshVisibleZoneTitle(AppServices.Instance.Progress.CurrentLevel);

            Button store = JoinDogUIFactory.Button(header.rectTransform, "Rewards", "TIENDA",
                new Vector2(0.84f, 0.15f), new Vector2(0.975f, 0.83f),
                new Color(0.60f, 0.27f, 0.68f, 1f));
            store.onClick.AddListener(ShowRewardStore);
        }

        private void RefreshMapProgress()
        {
            if (mapProgressText == null || AppServices.Instance == null) return;
            PlayerProgressService progress = AppServices.Instance.Progress;
            mapProgressText.text =
                $"{progress.CompletedLevels()}/{CampaignCatalog.MaxLevel}   " +
                $"ESTRELLAS {progress.TotalStars()}/{CampaignCatalog.MaxLevel * 3}   GALLETAS {progress.Treats}";
        }

        private void RefreshVisibleZoneTitle()
        {
            if (viewport == null || scrollRect == null || viewport.rect.height <= 0f) return;
            float viewportHeight = viewport.rect.height;
            float range = Mathf.Max(1f, ContentHeight - viewportHeight);
            float visibleY = scrollRect.verticalNormalizedPosition * range + viewportHeight * 0.46f;
            CampaignZoneEntry bestZone = null;
            float bestDistance = float.MaxValue;
            foreach (CampaignZoneEntry zone in catalog.zones)
            {
                CampaignLevelEntry first = catalog.GetLevel(zone.firstLevel);
                CampaignLevelEntry last = catalog.GetLevel(zone.lastLevel);
                if (first == null || last == null) continue;
                float center = (first.mapY + last.mapY) * 0.5f;
                float distance = Mathf.Abs(visibleY - center);
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    bestZone = zone;
                }
            }
            ApplyVisibleZoneTitle(bestZone);
        }

        private void RefreshVisibleZoneTitle(int level)
        {
            ApplyVisibleZoneTitle(catalog.GetZoneForLevel(level));
        }

        private void ApplyVisibleZoneTitle(CampaignZoneEntry zone)
        {
            if (mapWorldNameText == null || zone == null) return;
            mapWorldNameText.text = zone.displayName;
            mapWorldNameText.color = Color.Lerp(zone.accentColor, Color.white, 0.22f);
            if (mapHeaderImage != null)
            {
                Color header = Color.Lerp(zone.groundColor, new Color(0.015f, 0.025f, 0.04f, 1f), 0.74f);
                header.a = 0.99f;
                mapHeaderImage.color = header;
            }
            if (mapHeaderRibbonImage != null)
            {
                Color ribbon = Color.Lerp(zone.accentColor, zone.skyColor, 0.28f);
                ribbon.a = 1f;
                mapHeaderRibbonImage.color = ribbon;
            }
        }

        private void ShowRewardStore()
        {
            if (storePanel != null) return;
            Canvas canvas = FindAnyObjectByType<Canvas>();
            RectTransform root = canvas.GetComponent<RectTransform>();
            Image shade = JoinDogUIFactory.Image(root, "RewardStore", null, Vector2.zero, Vector2.one,
                new Color(0.005f, 0.015f, 0.035f, 0.88f), true);
            storePanel = shade.gameObject;

            Image cardShadow = JoinDogUIFactory.Panel(shade.rectTransform, "StoreShadow",
                new Vector2(0.045f, 0.055f), new Vector2(0.965f, 0.945f),
                new Color(0.01f, 0.005f, 0.02f, 0.72f));
            cardShadow.rectTransform.anchoredPosition = new Vector2(12f, -16f);
            Image card = JoinDogUIFactory.Panel(shade.rectTransform, "StoreCard",
                new Vector2(0.05f, 0.065f), new Vector2(0.95f, 0.94f),
                new Color(0.025f, 0.12f, 0.18f, 0.998f));
            Outline outline = card.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(1f, 0.70f, 0.18f, 1f);
            outline.effectDistance = new Vector2(6f, -6f);

            JoinDogUIFactory.Panel(card.rectTransform, "StoreInner",
                new Vector2(0.025f, 0.025f), new Vector2(0.975f, 0.975f),
                new Color(0.015f, 0.055f, 0.095f, 0.98f));
            CreateStoreCanopy(card.rectTransform);

            JoinDogUIFactory.Text(card.rectTransform, "StoreEyebrow", "PREMIOS DEL PARQUE", 18f,
                new Color(0.58f, 0.90f, 1f), TextAlignmentOptions.Left,
                new Vector2(0.07f, 0.855f), new Vector2(0.57f, 0.90f));
            TextMeshProUGUI storeTitle = JoinDogUIFactory.Text(card.rectTransform, "StoreTitle", "TIENDA JOIN DOG", 42f,
                new Color(1f, 0.82f, 0.22f), TextAlignmentOptions.Left,
                new Vector2(0.07f, 0.785f), new Vector2(0.66f, 0.86f));
            storeTitle.characterSpacing = 1.8f;

            Image balancePill = JoinDogUIFactory.Panel(card.rectTransform, "BalancePill",
                new Vector2(0.64f, 0.79f), new Vector2(0.93f, 0.895f),
                new Color(0.11f, 0.30f, 0.34f, 1f));
            Outline balanceOutline = balancePill.gameObject.AddComponent<Outline>();
            balanceOutline.effectColor = new Color(1f, 0.72f, 0.18f, 0.90f);
            balanceOutline.effectDistance = new Vector2(3f, -3f);
            Image coin = JoinDogUIFactory.Image(balancePill.rectTransform, "Coin", JoinDogUIFactory.CircleSprite(),
                new Vector2(0.05f, 0.17f), new Vector2(0.30f, 0.83f),
                new Color(1f, 0.65f, 0.10f, 1f));
            JoinDogUIFactory.Text(coin.rectTransform, "CoinMark", "G", 24f,
                new Color(0.42f, 0.18f, 0.03f, 1f), TextAlignmentOptions.Center, Vector2.zero, Vector2.one);
            storeBalanceText = JoinDogUIFactory.Text(balancePill.rectTransform, "Balance", string.Empty, 28f,
                Color.white, TextAlignmentOptions.Center,
                new Vector2(0.30f, 0.08f), new Vector2(0.96f, 0.92f));

            JoinDogUIFactory.Text(card.rectTransform, "StoreIntro",
                "ELIGE UNA AYUDA Y GUARDALA PARA TU PROXIMA PARTIDA", 18f,
                new Color(0.76f, 0.88f, 0.92f), TextAlignmentOptions.Center,
                new Vector2(0.08f, 0.735f), new Vector2(0.92f, 0.775f));

            CreateStoreItem(card.rectTransform, BoosterKind.Paw, "HUELLA MAGICA", "RENUEVA TODAS LAS FICHAS",
                PawCost, 0.545f, new Color(0.08f, 0.57f, 0.94f, 1f));
            CreateStoreItem(card.rectTransform, BoosterKind.Bone, "HUESO COHETE", "LIMPIA UNA FILA O COLUMNA",
                BoneCost, 0.355f, new Color(0.12f, 0.76f, 0.72f, 1f));
            CreateStoreItem(card.rectTransform, BoosterKind.Food, "SACO DE PIENSO", "ANADIR 10 SEGUNDOS AL RELOJ",
                FoodCost, 0.165f, new Color(0.92f, 0.48f, 0.12f, 1f));

            Image statusPanel = JoinDogUIFactory.Panel(card.rectTransform, "StoreStatusPanel",
                new Vector2(0.09f, 0.075f), new Vector2(0.80f, 0.145f),
                new Color(0.07f, 0.21f, 0.25f, 1f));
            storeStatusText = JoinDogUIFactory.Text(statusPanel.rectTransform, "StoreStatus",
                "GANA GALLETAS SUPERANDO NIVELES", 18f,
                new Color(1f, 0.91f, 0.62f), TextAlignmentOptions.Center,
                new Vector2(0.04f, 0.08f), new Vector2(0.96f, 0.92f));
            Button close = JoinDogUIFactory.Button(card.rectTransform, "CloseStore", "X",
                new Vector2(0.82f, 0.066f), new Vector2(0.93f, 0.148f),
                new Color(0.78f, 0.19f, 0.24f, 1f));
            close.onClick.AddListener(() =>
            {
                Destroy(storePanel);
                storePanel = null;
                storeBalanceText = null;
                storeStatusText = null;
                storeCountTexts.Clear();
                storeBuyButtons.Clear();
            });
            RefreshStore();
        }

        private void CreateStoreCanopy(RectTransform parent)
        {
            JoinDogUIFactory.Panel(parent, "CanopyBase", new Vector2(0.025f, 0.90f),
                new Vector2(0.975f, 0.985f), new Color(0.47f, 0.08f, 0.13f, 1f));
            for (int i = 0; i < 10; i++)
            {
                float min = 0.025f + i * 0.095f;
                float max = min + 0.096f;
                JoinDogUIFactory.Panel(parent, $"CanopyStripe_{i}", new Vector2(min, 0.91f),
                    new Vector2(max, 0.985f), i % 2 == 0
                        ? new Color(0.94f, 0.20f, 0.25f, 1f)
                        : new Color(1f, 0.75f, 0.18f, 1f));
            }
            JoinDogUIFactory.Panel(parent, "CanopyTrim", new Vector2(0.025f, 0.897f),
                new Vector2(0.975f, 0.918f), new Color(1f, 0.82f, 0.24f, 1f));
        }

        private void CreateStoreItem(RectTransform parent, BoosterKind kind, string title,
            string description, int cost, float bottom, Color color)
        {
            Image shadow = JoinDogUIFactory.Panel(parent, $"StoreShadow_{kind}",
                new Vector2(0.065f, bottom - 0.008f), new Vector2(0.945f, bottom + 0.163f),
                new Color(0.005f, 0.012f, 0.025f, 0.72f));
            shadow.rectTransform.anchoredPosition = new Vector2(7f, -7f);
            Image row = JoinDogUIFactory.Panel(parent, $"Store_{kind}",
                new Vector2(0.06f, bottom), new Vector2(0.94f, bottom + 0.17f),
                new Color(0.035f, 0.115f, 0.16f, 1f));
            Outline rowOutline = row.gameObject.AddComponent<Outline>();
            rowOutline.effectColor = new Color(color.r, color.g, color.b, 0.78f);
            rowOutline.effectDistance = new Vector2(3f, -3f);
            JoinDogUIFactory.Panel(row.rectTransform, "ColorRail", new Vector2(0f, 0.08f),
                new Vector2(0.018f, 0.92f), color);

            Image iconRing = JoinDogUIFactory.Image(row.rectTransform, "IconRing", JoinDogUIFactory.CircleSprite(),
                new Vector2(0.025f, 0.11f), new Vector2(0.205f, 0.89f),
                new Color(1f, 0.73f, 0.17f, 1f));
            Image iconPlate = JoinDogUIFactory.Image(iconRing.rectTransform, "IconPlate", JoinDogUIFactory.CircleSprite(),
                new Vector2(0.08f, 0.08f), new Vector2(0.92f, 0.92f), color);
            Sprite iconSprite = Resources.Load<Sprite>(kind == BoosterKind.Paw
                ? "UI/button-moves"
                : kind == BoosterKind.Bone ? "UI/button-bone" : "UI/button-food");
            Image icon = JoinDogUIFactory.Image(iconPlate.rectTransform, "ProductIcon", iconSprite,
                new Vector2(0.10f, 0.10f), new Vector2(0.90f, 0.90f), Color.white);
            icon.preserveAspect = true;

            JoinDogUIFactory.Text(row.rectTransform, "Title", title, 25f,
                new Color(1f, 0.84f, 0.28f), TextAlignmentOptions.Left,
                new Vector2(0.235f, 0.55f), new Vector2(0.61f, 0.88f));
            JoinDogUIFactory.Text(row.rectTransform, "Description", description, 15f,
                new Color(0.78f, 0.90f, 0.94f), TextAlignmentOptions.Left,
                new Vector2(0.235f, 0.28f), new Vector2(0.63f, 0.58f));
            Image ownedPill = JoinDogUIFactory.Panel(row.rectTransform, "OwnedPill",
                new Vector2(0.235f, 0.08f), new Vector2(0.55f, 0.28f),
                new Color(0.08f, 0.26f, 0.29f, 1f));
            TextMeshProUGUI count = JoinDogUIFactory.Text(ownedPill.rectTransform, "Owned", string.Empty, 16f,
                Color.white, TextAlignmentOptions.Center, new Vector2(0.04f, 0.06f), new Vector2(0.96f, 0.94f));
            storeCountTexts[kind] = count;
            Button buy = JoinDogUIFactory.Button(row.rectTransform, "Buy", "COMPRAR",
                new Vector2(0.665f, 0.15f), new Vector2(0.965f, 0.85f),
                new Color(0.12f, 0.64f, 0.30f, 1f));
            TextMeshProUGUI buyLabel = buy.transform.Find("BuyLabel")?.GetComponent<TextMeshProUGUI>();
            if (buyLabel != null)
            {
                buyLabel.fontSize = 19f;
                buyLabel.rectTransform.anchorMin = new Vector2(0.05f, 0.48f);
                buyLabel.rectTransform.anchorMax = new Vector2(0.95f, 0.90f);
            }
            JoinDogUIFactory.Text(buy.GetComponent<RectTransform>(), "Price", $"{cost} GALLETAS", 16f,
                new Color(1f, 0.94f, 0.64f), TextAlignmentOptions.Center,
                new Vector2(0.06f, 0.08f), new Vector2(0.94f, 0.48f));
            buy.onClick.AddListener(() => PurchaseBooster(kind, cost));
            storeBuyButtons[kind] = buy;
        }

        private void PurchaseBooster(BoosterKind kind, int cost)
        {
            bool purchased = AppServices.Instance.Progress.TryPurchaseBooster(kind, cost);
            if (storeStatusText != null)
            {
                storeStatusText.text = purchased ? "COMPRA LISTA - GUARDADO EN TU MOCHILA" : "TE FALTAN GALLETAS PARA ESTA COMPRA";
                storeStatusText.color = purchased
                    ? new Color(0.45f, 1f, 0.58f)
                    : new Color(1f, 0.42f, 0.34f);
                StartCoroutine(PulseStoreFeedback());
            }
            RefreshStore();
            RefreshMapProgress();
        }

        private void RefreshStore()
        {
            if (AppServices.Instance == null) return;
            PlayerProgressService progress = AppServices.Instance.Progress;
            if (storeBalanceText != null) storeBalanceText.text = progress.Treats.ToString("N0");
            foreach (KeyValuePair<BoosterKind, TextMeshProUGUI> pair in storeCountTexts)
                if (pair.Value != null) pair.Value.text = $"EN MOCHILA: {progress.GetBoosterCount(pair.Key)}";
            foreach (KeyValuePair<BoosterKind, Button> pair in storeBuyButtons)
            {
                if (pair.Value != null) pair.Value.interactable = progress.Treats >= StoreCost(pair.Key);
            }
        }

        private static int StoreCost(BoosterKind kind)
        {
            return kind == BoosterKind.Paw ? PawCost : kind == BoosterKind.Bone ? BoneCost : FoodCost;
        }

        private IEnumerator PulseStoreFeedback()
        {
            if (storeStatusText == null) yield break;
            RectTransform rect = storeStatusText.rectTransform;
            float elapsed = 0f;
            while (elapsed < 0.30f && rect != null)
            {
                elapsed += Time.unscaledDeltaTime;
                float pulse = 1f + Mathf.Sin(Mathf.Clamp01(elapsed / 0.30f) * Mathf.PI) * 0.10f;
                rect.localScale = Vector3.one * pulse;
                yield return null;
            }
            if (rect != null) rect.localScale = Vector3.one;
        }

        private void BuildPathAndNodes()
        {
            CampaignLevelEntry previous = null;
            foreach (CampaignLevelEntry entry in catalog.levels)
            {
                if (entry == null) continue;
                if (previous != null) CreateConnection(previous, entry);
                previous = entry;
            }

            foreach (CampaignLevelEntry entry in catalog.levels)
            {
                if (entry != null) CreateNode(entry);
            }
        }

        private Vector2 ToContentPoint(CampaignLevelEntry entry)
        {
            return new Vector2((entry.mapX - 0.5f) * 980f, entry.mapY);
        }

        private void CreateConnection(CampaignLevelEntry from, CampaignLevelEntry to)
        {
            Vector2 start = ToContentPoint(from);
            Vector2 end = ToContentPoint(to);
            bool reached = to.level <= AppServices.Instance.Progress.UnlockedLevel;
            CampaignZoneEntry pathZone = catalog.GetZoneForLevel(to.level);
            Color zoneAccent = pathZone != null ? pathZone.accentColor : new Color(0.96f, 0.55f, 0.10f, 1f);
            Color reachedColor = Color.Lerp(zoneAccent, new Color(0.20f, 0.12f, 0.04f, 1f), 0.18f);
            reachedColor.a = 1f;
            Color pathLight = Color.Lerp(zoneAccent, Color.white, 0.42f);
            pathLight.a = 0.92f;
            CreatePathLine($"PathShadow_{from.level}_{to.level}", start, end, 30f,
                new Color(0.07f, 0.035f, 0.02f, 0.48f));
            CreatePathLine($"PathBase_{from.level}_{to.level}", start, end, 19f,
                reached ? reachedColor : new Color(0.24f, 0.23f, 0.20f, 0.82f));
            CreatePathLine($"PathLight_{from.level}_{to.level}", start, end, 5f,
                reached ? pathLight : new Color(0.52f, 0.49f, 0.40f, 0.46f));

            for (int i = 1; i <= 2; i++)
            {
                Vector2 point = Vector2.Lerp(start, end, i / 3f);
                CreateContentImage($"PathDot_{from.level}_{i}", point, new Vector2(13f, 13f),
                    JoinDogUIFactory.CircleSprite(), reached
                        ? Color.Lerp(zoneAccent, Color.white, 0.24f + i * 0.12f)
                        : new Color(0.35f, 0.37f, 0.34f, 0.75f));
            }
        }

        private void CreatePathLine(string name, Vector2 start, Vector2 end, float width, Color color)
        {
            Vector2 delta = end - start;
            Image image = CreateContentImage(name, (start + end) * 0.5f,
                new Vector2(delta.magnitude, width), JoinDogUIFactory.RoundedSprite(), color);
            image.type = Image.Type.Sliced;
            image.rectTransform.localRotation = Quaternion.Euler(0f, 0f,
                Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);
        }

        private void CreateNode(CampaignLevelEntry entry)
        {
            bool unlocked = AppServices.Instance.Progress.IsUnlocked(entry.level);
            int stars = AppServices.Instance.Progress.GetStars(entry.level);
            bool current = entry.level == AppServices.Instance.Progress.CurrentLevel;
            float size = entry.nodeKind == MapNodeKind.Finale ? 168f :
                entry.nodeKind == MapNodeKind.Reward ? 150f : unlocked ? 140f : 128f;
            Vector2 position = ToContentPoint(entry);

            CreateContentImage($"NodeShadow_{entry.level}", position + new Vector2(9f, -13f),
                new Vector2(size + 18f, size + 18f), JoinDogUIFactory.CircleSprite(),
                new Color(0.05f, 0.025f, 0.015f, 0.38f));

            GameObject nodeObject = new GameObject($"LevelNode_{entry.level}",
                typeof(RectTransform), typeof(Image), typeof(Button));
            nodeObject.transform.SetParent(content, false);
            RectTransform node = nodeObject.GetComponent<RectTransform>();
            node.anchorMin = new Vector2(0.5f, 0f);
            node.anchorMax = new Vector2(0.5f, 0f);
            node.pivot = new Vector2(0.5f, 0.5f);
            node.anchoredPosition = position;
            node.sizeDelta = new Vector2(size, size);
            nodeRects[entry.level] = node;
            if (current) currentNode = node;

            Image ring = nodeObject.GetComponent<Image>();
            ring.sprite = JoinDogUIFactory.CircleSprite();
            ring.color = unlocked ? new Color(1f, 0.74f, 0.18f, 1f) : new Color(0.24f, 0.27f, 0.27f, 1f);
            Image inner = JoinDogUIFactory.Image(node, "Inner", JoinDogUIFactory.CircleSprite(),
                new Vector2(0.10f, 0.10f), new Vector2(0.90f, 0.90f), NodeColor(entry, unlocked, stars, current));
            inner.raycastTarget = false;
            Image shine = JoinDogUIFactory.Image(node, "Shine", JoinDogUIFactory.CircleSprite(),
                new Vector2(0.25f, 0.60f), new Vector2(0.65f, 0.85f),
                new Color(1f, 1f, 1f, unlocked ? 0.24f : 0.06f));
            shine.raycastTarget = false;

            string label = entry.level.ToString();
            JoinDogUIFactory.Text(node, "Number", label, 48f, Color.white,
                TextAlignmentOptions.Center, new Vector2(0.12f, 0.24f), new Vector2(0.88f, 0.82f));
            string footer = !unlocked ? "BLOQ." : stars > 0 ? $"{stars}/3" : NodeCaption(entry);
            JoinDogUIFactory.Text(node, "Footer", footer, 18f,
                new Color(1f, 0.93f, 0.48f), TextAlignmentOptions.Center,
                new Vector2(0.03f, 0.04f), new Vector2(0.97f, 0.28f));

            if (current)
            {
                Image badge = CreateChildPanel(node, "CurrentBadge", new Vector2(0.05f, 0.82f),
                    new Vector2(0.95f, 1.10f), new Color(0.08f, 0.45f, 0.68f, 1f));
                JoinDogUIFactory.Text(badge.rectTransform, "CurrentLabel", "AQUI",
                    18f, Color.white, TextAlignmentOptions.Center,
                    new Vector2(0.06f, 0.06f), new Vector2(0.94f, 0.94f));
            }

            Button button = nodeObject.GetComponent<Button>();
            button.interactable = unlocked;
            int levelNumber = entry.level;
            button.onClick.AddListener(() => StartCoroutine(SelectNodeAndPreview(levelNumber)));
        }

        private static Color NodeColor(CampaignLevelEntry entry, bool unlocked, int stars, bool current)
        {
            if (!unlocked) return new Color(0.17f, 0.20f, 0.20f, 1f);
            if (current) return new Color(0.08f, 0.58f, 0.93f, 1f);
            if (stars > 0) return new Color(0.12f, 0.64f, 0.31f, 1f);
            if (entry.nodeKind == MapNodeKind.Finale) return new Color(0.83f, 0.18f, 0.20f, 1f);
            if (entry.nodeKind == MapNodeKind.Hard) return new Color(0.91f, 0.31f, 0.14f, 1f);
            if (entry.nodeKind == MapNodeKind.Reward) return new Color(0.62f, 0.25f, 0.79f, 1f);
            return new Color(0.96f, 0.50f, 0.10f, 1f);
        }

        private static string NodeCaption(CampaignLevelEntry entry)
        {
            if (entry.nodeKind == MapNodeKind.Finale) return "FINAL";
            if (entry.nodeKind == MapNodeKind.Reward) return "REGALO";
            if (entry.nodeKind == MapNodeKind.Hard) return "DIFICIL";
            return string.Empty;
        }

        private void BuildDogMarker()
        {
            int pending = AppServices.Instance.PendingMapAdvanceFromLevel;
            int level = pending > 0 ? pending : AppServices.Instance.Progress.CurrentLevel;
            level = Mathf.Clamp(level, 1, CampaignCatalog.MaxLevel);
            if (!nodeRects.TryGetValue(level, out RectTransform node)) return;

            GameObject markerObject = new GameObject("MapDogMarker", typeof(RectTransform));
            markerObject.transform.SetParent(content, false);
            dogMarker = markerObject.GetComponent<RectTransform>();
            dogMarker.anchorMin = new Vector2(0.5f, 0f);
            dogMarker.anchorMax = new Vector2(0.5f, 0f);
            dogMarker.pivot = new Vector2(0.5f, 0f);
            dogMarker.sizeDelta = new Vector2(190f, 205f);
            dogMarker.anchoredPosition = node.anchoredPosition + new Vector2(0f, 67f);

            JoinDogUIFactory.Image(dogMarker, "DogShadow", JoinDogUIFactory.CircleSprite(),
                new Vector2(0.22f, 0.02f), new Vector2(0.78f, 0.20f), new Color(0.04f, 0.02f, 0.01f, 0.36f));
            Image halo = JoinDogUIFactory.Image(dogMarker, "DogHalo", JoinDogUIFactory.CircleSprite(),
                new Vector2(0.05f, 0.08f), new Vector2(0.95f, 0.92f), new Color(1f, 0.80f, 0.20f, 0.24f));
            halo.raycastTarget = false;
            Image dog = JoinDogUIFactory.Image(dogMarker, "Dog", dogSprite,
                new Vector2(0.08f, 0.08f), new Vector2(0.92f, 0.98f), Color.white);
            dog.preserveAspect = true;
            dogMarker.SetAsLastSibling();
        }

        private IEnumerator SelectNodeAndPreview(int level)
        {
            if (!nodeRects.TryGetValue(level, out RectTransform node)) yield break;
            if (selectedNode != null) selectedNode.localScale = Vector3.one;
            selectedNode = node;
            float elapsed = 0f;
            float duration = 0.28f;
            Vector3 startScale = node.localScale;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                node.localScale = Vector3.Lerp(startScale, Vector3.one * 1.12f,
                    Mathf.Sin(t * Mathf.PI * 0.5f));
                yield return null;
            }
            yield return CenterOnLevel(level, 0.34f);
            ShowLevelPreview(level);
        }

        private void ShowLevelPreview(int level)
        {
            if (previewPanel != null) Destroy(previewPanel);
            CampaignLevelEntry entry = catalog.GetLevel(level);
            CampaignZoneEntry zone = catalog.GetZoneForLevel(level);
            if (entry == null || zone == null) return;
            Canvas canvas = FindAnyObjectByType<Canvas>();
            RectTransform root = canvas.GetComponent<RectTransform>();
            Image shade = JoinDogUIFactory.Image(root, "LevelPreview", null, Vector2.zero, Vector2.one,
                new Color(0.01f, 0.02f, 0.04f, 0.74f), true);
            previewPanel = shade.gameObject;
            Image cardShadow = JoinDogUIFactory.Panel(shade.rectTransform, "CardShadow",
                new Vector2(0.075f, 0.205f), new Vector2(0.935f, 0.795f),
                new Color(0.02f, 0.01f, 0.01f, 0.58f));
            cardShadow.rectTransform.anchoredPosition = new Vector2(10f, -12f);
            Image card = JoinDogUIFactory.Panel(shade.rectTransform, "LevelCard",
                new Vector2(0.08f, 0.215f), new Vector2(0.92f, 0.785f),
                new Color(0.16f, 0.045f, 0.02f, 0.99f));
            Outline outline = card.gameObject.AddComponent<Outline>();
            outline.effectColor = zone.accentColor;
            outline.effectDistance = new Vector2(5f, -5f);
            JoinDogUIFactory.Panel(card.rectTransform, "ZoneRibbon",
                new Vector2(0.10f, 0.82f), new Vector2(0.90f, 0.94f),
                new Color(zone.accentColor.r * 0.65f, zone.accentColor.g * 0.65f,
                    zone.accentColor.b * 0.65f, 1f));
            JoinDogUIFactory.Text(card.rectTransform, "Zone", zone.displayName, 20f,
                Color.white, TextAlignmentOptions.Center,
                new Vector2(0.12f, 0.83f), new Vector2(0.88f, 0.93f));
            JoinDogUIFactory.Text(card.rectTransform, "Title", entry.title, 43f,
                zone.accentColor, TextAlignmentOptions.Center,
                new Vector2(0.08f, 0.66f), new Vector2(0.92f, 0.82f));
            TextMeshProUGUI objective = JoinDogUIFactory.Text(card.rectTransform, "Objective",
                CampaignCatalog.BuildObjectivePreview(entry), 27f, Color.white, TextAlignmentOptions.Center,
                new Vector2(0.08f, 0.53f), new Vector2(0.92f, 0.66f));
            objective.enableWordWrapping = true;
            string zoneRule = entry.obstacleType == CampaignObstacleKind.Vine
                ? "REGLA: COMBINA SOBRE LAS ENREDADERAS PARA ROMPERLAS"
                : entry.obstacleType == CampaignObstacleKind.Lantern
                    ? "REGLA: FAROLES DE 2 GOLPES · LOS ESPECIALES DAÑAN ALREDEDOR"
                    : entry.obstacleType == CampaignObstacleKind.Sand
                        ? "REGLA: LIMPIA LA ARENA COMBINANDO ENCIMA O A SU LADO"
                        : entry.obstacleType == CampaignObstacleKind.Ice
                            ? "REGLA: HIELO DE 3 GOLPES · LOS ESPECIALES DAÑAN ALREDEDOR"
                            : "REGLA: CREA COMBOS LARGOS PARA GANAR MÁS PUNTOS";
            JoinDogUIFactory.Text(card.rectTransform, "WorldRule", zoneRule, 17f,
                new Color(1f, 0.82f, 0.30f), TextAlignmentOptions.Center,
                new Vector2(0.07f, 0.45f), new Vector2(0.93f, 0.53f));
            string difficulty = new string('|', Mathf.Clamp(entry.difficulty, 1, 5));
            JoinDogUIFactory.Text(card.rectTransform, "Rules",
                $"DIFICULTAD {difficulty}    {entry.columns}x{entry.rows}    {entry.durationSeconds}s",
                20f, new Color(0.62f, 0.88f, 1f), TextAlignmentOptions.Center,
                new Vector2(0.08f, 0.36f), new Vector2(0.92f, 0.45f));
            int stars = AppServices.Instance.Progress.GetStars(level);
            int best = AppServices.Instance.Progress.GetBestScore(level);
            JoinDogUIFactory.Text(card.rectTransform, "Best",
                $"ESTRELLAS {stars}/3    RÉCORD {best:N0}    PREMIO {entry.rewardTreats}",
                22f, new Color(1f, 0.93f, 0.72f), TextAlignmentOptions.Center,
                new Vector2(0.08f, 0.26f), new Vector2(0.92f, 0.35f));
            Button play = JoinDogUIFactory.Button(card.rectTransform, "PlayLevel", "JUGAR",
                new Vector2(0.36f, 0.07f), new Vector2(0.90f, 0.24f),
                new Color(0.10f, 0.67f, 0.33f, 1f));
            play.onClick.AddListener(() => AppServices.Instance.StartLevel(level));
            Button close = JoinDogUIFactory.Button(card.rectTransform, "ClosePreview", "<",
                new Vector2(0.08f, 0.07f), new Vector2(0.30f, 0.24f),
                new Color(0.07f, 0.42f, 0.64f, 1f));
            close.onClick.AddListener(() =>
            {
                if (selectedNode != null) selectedNode.localScale = Vector3.one;
                selectedNode = null;
                Destroy(previewPanel);
            });
        }

        private IEnumerator FocusAndAnimate()
        {
            yield return null;
            Canvas.ForceUpdateCanvases();
            int pending = AppServices.Instance.ConsumePendingMapAdvance();
            int focusLevel = pending > 0 ? pending : AppServices.Instance.Progress.CurrentLevel;
            yield return CenterOnLevel(focusLevel, 0f);

            if (pending > 0 && pending < CampaignCatalog.MaxLevel && dogMarker != null &&
                nodeRects.TryGetValue(pending + 1, out RectTransform target))
            {
                Vector2 start = dogMarker.anchoredPosition;
                Vector2 end = target.anchoredPosition + new Vector2(0f, 67f);
                float duration = 1.45f;
                float elapsed = 0f;
                while (elapsed < duration)
                {
                    elapsed += Time.unscaledDeltaTime;
                    float t = Mathf.Clamp01(elapsed / duration);
                    float eased = t * t * (3f - 2f * t);
                    Vector2 position = Vector2.Lerp(start, end, eased);
                    position.y += Mathf.Sin(t * Mathf.PI * 6f) * 20f;
                    dogMarker.anchoredPosition = position;
                    dogMarker.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(t * Mathf.PI * 8f) * 4f);
                    yield return null;
                }
                dogMarker.localRotation = Quaternion.identity;
                dogMarker.anchoredPosition = end;
                yield return CenterOnLevel(pending + 1, 0.50f);
            }

            StartCoroutine(BobDog());
        }

        private IEnumerator CenterOnLevel(int level, float duration)
        {
            CampaignLevelEntry entry = catalog.GetLevel(level);
            if (entry == null || viewport == null || scrollRect == null) yield break;
            Canvas.ForceUpdateCanvases();
            float viewportHeight = viewport.rect.height;
            float range = Mathf.Max(1f, ContentHeight - viewportHeight);
            float target = Mathf.Clamp01((entry.mapY - viewportHeight * 0.46f) / range);
            if (duration <= 0f)
            {
                scrollRect.verticalNormalizedPosition = target;
                yield break;
            }

            float start = scrollRect.verticalNormalizedPosition;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float eased = t * t * (3f - 2f * t);
                scrollRect.verticalNormalizedPosition = Mathf.Lerp(start, target, eased);
                yield return null;
            }
            scrollRect.verticalNormalizedPosition = target;
        }

        private IEnumerator PulseCurrentNode()
        {
            float time = 0f;
            while (currentNode != null)
            {
                time += Time.unscaledDeltaTime;
                if (selectedNode != currentNode)
                {
                    float scale = 1f + Mathf.Sin(time * 3.1f) * 0.045f;
                    currentNode.localScale = Vector3.one * scale;
                }
                yield return null;
            }
        }

        private IEnumerator BobDog()
        {
            if (dogMarker == null) yield break;
            Vector2 basePosition = dogMarker.anchoredPosition;
            float time = 0f;
            while (dogMarker != null)
            {
                time += Time.unscaledDeltaTime;
                dogMarker.anchoredPosition = basePosition + Vector2.up * (Mathf.Sin(time * 2.5f) * 9f);
                dogMarker.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(time * 1.35f) * 1.8f);
                yield return null;
            }
        }

        private Image CreateContentImage(string name, Vector2 position, Vector2 size, Sprite sprite, Color color)
        {
            Image image = JoinDogUIFactory.Image(content, name, sprite,
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), color);
            image.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            image.rectTransform.anchoredPosition = position;
            image.rectTransform.sizeDelta = size;
            image.raycastTarget = false;
            return image;
        }

        private RectTransform CreateContentContainer(string name, Vector2 position, Vector2 size)
        {
            GameObject container = new GameObject(name, typeof(RectTransform));
            container.transform.SetParent(content, false);
            RectTransform rect = container.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0f);
            rect.anchorMax = new Vector2(0.5f, 0f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            return rect;
        }

        private static Image CreateChildPanel(RectTransform parent, string name,
            Vector2 anchorMin, Vector2 anchorMax, Color color)
        {
            Image panel = JoinDogUIFactory.Panel(parent, name, anchorMin, anchorMax, color);
            panel.raycastTarget = false;
            return panel;
        }

        private void OnDisable()
        {
            if (scrollRect != null && AppServices.Instance != null)
                AppServices.Instance.Progress.SetMapScroll(scrollRect.verticalNormalizedPosition);
        }
    }
}
