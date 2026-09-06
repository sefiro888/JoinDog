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

        // A real illustrated star is much clearer than a font glyph on every
        // device/font fallback, especially in the compact mobile map nodes.
        private static Sprite mapRewardStarSprite;

        // Level 100 ends around y=28160 after the nine gateway plazas. Leave
        // room for the finale, entrance art and bottom safe area on mobile.
        // and the bottom safe area so the final chapter remains reachable on
        // short mobile screens as well as desktop.
        private const float ContentHeight = 29000f;
        private const float ContentWidth = 1080f;
        private const float GatewayArtworkHeight = 600f;
        private const float GatewayNodeClearance = 110f;
        private const float GatewayPlazaHeight = GatewayArtworkHeight + GatewayNodeClearance;
        private CampaignCatalog catalog;
        private ScrollRect scrollRect;
        private RectTransform viewport;
        private RectTransform content;
        private RectTransform dogMarker;
        private RectTransform currentNode;
        private RectTransform selectedNode;
        private GameObject previewPanel;
        private GameObject storePanel;
        private GameObject dailyPanel;
        private TextMeshProUGUI mapProgressText;
        private TextMeshProUGUI mapWorldNameText;
        private Image mapHeaderImage;
        private Image mapHeaderRibbonImage;
        private Image mapZoneEmblem;
        private Image worldTint;
        private TextMeshProUGUI storeBalanceText;
        private TextMeshProUGUI storeStatusText;
        private string visibleZoneId;
        private Coroutine headerTransitionRoutine;
        private Coroutine discoveryRoutine;
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
            worldTint = JoinDogUIFactory.Image(root, "WorldTint", null, Vector2.zero, Vector2.one,
                GetAmbientTint());
            worldTint.raycastTarget = false;

            GameObject scrollObject = new GameObject("MapScroll", typeof(RectTransform), typeof(ScrollRect));
            scrollObject.transform.SetParent(root, false);
            RectTransform scrollTransform = scrollObject.GetComponent<RectTransform>();
            scrollTransform.anchorMin = Vector2.zero;
            scrollTransform.anchorMax = new Vector2(1f, 0.91f);
            scrollTransform.offsetMin = Vector2.zero;
            scrollTransform.offsetMax = Vector2.zero;

            // A transparent graphic is intentional: ScrollRect only receives
            // wheel/drag pointer events when its viewport is a raycast target.
            // Without it, desktop users are forced to use the small map arrows.
            GameObject viewportObject = new GameObject("Viewport", typeof(RectTransform),
                typeof(RectMask2D), typeof(Image));
            viewportObject.transform.SetParent(scrollTransform, false);
            viewport = viewportObject.GetComponent<RectTransform>();
            viewport.anchorMin = Vector2.zero;
            viewport.anchorMax = Vector2.one;
            viewport.offsetMin = Vector2.zero;
            viewport.offsetMax = Vector2.zero;
            Image viewportHitArea = viewportObject.GetComponent<Image>();
            viewportHitArea.color = new Color(1f, 1f, 1f, 0f);
            viewportHitArea.raycastTarget = true;

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
            JoinDogUIFactory.EnsureMinimumTouchTargets(root);
        }

        private void BuildZoneBackdrops()
        {
            for (int index = 0; index < catalog.zones.Count; index++)
            {
                CampaignZoneEntry zone = catalog.zones[index];
                CampaignLevelEntry first = catalog.GetLevel(zone.firstLevel);
                CampaignLevelEntry last = catalog.GetLevel(zone.lastLevel);
                if (first == null || last == null) continue;

                CampaignLevelEntry previousLast = index > 0
                    ? catalog.GetLevel(catalog.zones[index - 1].lastLevel)
                    : null;
                CampaignLevelEntry nextFirst = index + 1 < catalog.zones.Count
                    ? catalog.GetLevel(catalog.zones[index + 1].firstLevel)
                    : null;

                // A gateway belongs almost entirely to its incoming world:
                // only a short lead-in remains after the previous finale, then
                // the full entrance fills the otherwise empty transition.
                float bottom = previousLast == null
                    ? Mathf.Max(0f, first.mapY - 250f)
                    : first.mapY - GatewayPlazaHeight;
                float top = nextFirst == null
                    ? Mathf.Min(ContentHeight, last.mapY + 250f)
                    : nextFirst.mapY - GatewayPlazaHeight;
                Color atmosphere = Color.Lerp(zone.skyColor, zone.groundColor, 0.58f);
                atmosphere.a = index == 0 ? 0.38f : 1f;
                Image zonePanel = CreateContentImage($"Zone_{zone.id}",
                    new Vector2(0f, (bottom + top) * 0.5f),
                    new Vector2(ContentWidth + 180f, top - bottom),
                    JoinDogUIFactory.RoundedSprite(), atmosphere);
                zonePanel.type = Image.Type.Sliced;

                bool hasIllustratedBackground = CreateZoneArtBackground(zone, bottom, top);
                CreateZoneAtmosphere(zone, bottom, top, index, hasIllustratedBackground);
                CreateZoneIdentityMark(zone, bottom, top, index, hasIllustratedBackground);
                CreateZoneLandmarks(zone, bottom, top, index, hasIllustratedBackground);
            }
        }

        private bool CreateZoneArtBackground(CampaignZoneEntry zone, float bottom, float top)
        {
            Sprite sprite = WorldMapArtLibrary.LoadBackground(zone.id);
            if (sprite == null && zone.id == "pradera_feliz") sprite = backgroundSprite;
            if (sprite == null) return false;

            float zoneHeight = top - bottom;
            float aspect = sprite.rect.width / Mathf.Max(1f, sprite.rect.height);
            bool finalZoneArtwork = zone.id == "valle_aurora" || zone.id == "cumbre_luminosa" ||
                zone.id == "jardines_celestes" || zone.id == "canon_rubies" || zone.id == "santuario_dorado";
            float artworkWidth = finalZoneArtwork ? ContentWidth + 260f : zoneHeight * aspect;
            Image artwork = CreateContentImage($"ZoneArtwork_{zone.id}",
                new Vector2(0f, (bottom + top) * 0.5f),
                new Vector2(artworkWidth, zoneHeight), sprite, Color.white);
            // The last two chapters are designed as full-width panoramas on
            // mobile. Stretching only these two keeps their side landmarks
            // visible instead of leaving a narrow portrait strip.
            artwork.preserveAspect = !finalZoneArtwork;

            // A very light glaze unifies dynamic nodes and path colors without
            // hiding the illustration underneath.
            CreateContentImage($"ZoneArtworkGlaze_{zone.id}",
                new Vector2(0f, (bottom + top) * 0.5f),
                new Vector2(ContentWidth + 190f, zoneHeight),
                JoinDogUIFactory.RoundedSprite(), new Color(0.01f, 0.10f, 0.075f, 0.10f)).type = Image.Type.Sliced;
            return true;
        }

        private void CreateZoneIdentityMark(CampaignZoneEntry zone, float bottom, float top, int zoneIndex,
            bool hasIllustratedBackground)
        {
            float centerY = (bottom + top) * 0.5f;
            Color[] haloColors =
            {
                new Color(1f, 0.86f, 0.24f, 0.14f),
                new Color(0.24f, 0.92f, 0.52f, 0.13f),
                new Color(0.92f, 0.34f, 1f, 0.16f),
                new Color(0.18f, 0.94f, 1f, 0.16f),
                new Color(0.62f, 0.88f, 1f, 0.18f),
                new Color(0.98f, 0.34f, 0.72f, 0.18f),
                new Color(1f, 0.76f, 0.20f, 0.20f),
                new Color(0.54f, 1f, 0.88f, 0.20f),
                new Color(1f, 0.30f, 0.24f, 0.20f),
                new Color(1f, 0.82f, 0.24f, 0.22f)
            };
            Color haloColor = haloColors[Mathf.Clamp(zoneIndex, 0, haloColors.Length - 1)];
            // A restrained atmospheric vignette; the former giant circle made
            // the opening world look like unfinished placeholder geometry.
            haloColor.a *= hasIllustratedBackground ? 0.20f : 0.48f;
            CreateContentImage($"ZoneIdentityHalo_{zone.id}", new Vector2(0f, centerY),
                new Vector2(680f, 420f), JoinDogUIFactory.CircleSprite(), haloColor);

            // Zone names already live in the fixed top header. Keeping them
            // off the map makes the gateway plaza an uninterrupted piece of
            // scenery instead of a route passing behind a sign.

            if (hasIllustratedBackground)
            {
                return;
            }

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
                CreateFestivalAtmosphere(zone, bottom, top, hasIllustratedBackground);
            }
            else if (zoneIndex == 3)
            {
                CreateCoastAtmosphere(zone, bottom, top, hasIllustratedBackground);
            }
            else if (zoneIndex == 4)
            {
                CreateMountainAtmosphere(zone, bottom, top, hasIllustratedBackground);
            }
            else if (zoneIndex == 5)
            {
                CreateAuroraAtmosphere(zone, bottom, top);
            }
            else if (zoneIndex == 6)
            {
                CreateLuminousSummitAtmosphere(zone, bottom, top);
            }
            else if (zoneIndex == 7)
            {
                CreateCelestialAtmosphere(zone, bottom, top);
            }
            else if (zoneIndex == 8)
            {
                CreateRubyAtmosphere(zone, bottom, top);
            }
            else if (zoneIndex == 9)
            {
                CreateSanctuaryAtmosphere(zone, bottom, top);
            }

            if (zoneIndex > 0)
            {
                // The entrance belongs to the first pixels of its own zone.
                // Aligning the bottom of every arch with the zone boundary
                // prevents the artwork from spilling into the previous world.
                CreateZoneEntrance(zone, bottom, zoneIndex);
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

        private void CreateFestivalAtmosphere(CampaignZoneEntry zone, float bottom, float top,
            bool hasIllustratedBackground)
        {
            CreateContentImage("FestivalNight", new Vector2(0f, (bottom + top) * 0.5f),
                new Vector2(ContentWidth + 190f, top - bottom), JoinDogUIFactory.RoundedSprite(),
                new Color(0.08f, 0.035f, 0.20f, hasIllustratedBackground ? 0.08f : 0.58f)).type = Image.Type.Sliced;
            CreateContentImage("FestivalHorizonGlow", new Vector2(0f, bottom + 420f),
                new Vector2(980f, 520f), JoinDogUIFactory.CircleSprite(),
                new Color(0.68f, 0.22f, 0.76f, hasIllustratedBackground ? 0.07f : 0.18f));

            int garlandCount = hasIllustratedBackground ? 2 : 6;
            for (int garland = 0; garland < garlandCount; garland++)
            {
                float denominator = Mathf.Max(1f, garlandCount - 1f);
                float y = Mathf.Lerp(bottom + 520f, top - 300f, garland / denominator);
                CreateFestivalGarland(garland, y, garland % 2 == 0);
            }

            Color[] confettiColors =
            {
                new Color(1f, 0.30f, 0.45f, 0.95f),
                new Color(1f, 0.78f, 0.18f, 0.95f),
                new Color(0.25f, 0.80f, 1f, 0.95f),
                new Color(0.48f, 1f, 0.48f, 0.95f)
            };
            int confettiCount = hasIllustratedBackground ? 16 : 34;
            for (int i = 0; i < confettiCount; i++)
            {
                float denominator = Mathf.Max(1f, confettiCount - 1f);
                float y = Mathf.Lerp(bottom + 140f, top - 90f, i / denominator);
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

        private void CreateCoastAtmosphere(CampaignZoneEntry zone, float bottom, float top,
            bool hasIllustratedBackground)
        {
            CreateContentImage("CoastOcean", new Vector2(0f, (bottom + top) * 0.5f),
                new Vector2(ContentWidth + 190f, top - bottom), JoinDogUIFactory.RoundedSprite(),
                new Color(0.02f, 0.46f, 0.68f, hasIllustratedBackground ? 0.055f : 0.24f)).type = Image.Type.Sliced;
            int foamCount = hasIllustratedBackground ? 4 : 10;
            for (int i = 0; i < foamCount; i++)
            {
                float denominator = Mathf.Max(1f, foamCount - 1f);
                float y = Mathf.Lerp(bottom + 280f, top - 180f, i / denominator);
                Image foam = CreateContentImage($"CoastFoam_{i}",
                    new Vector2(i % 2 == 0 ? -130f : 145f, y),
                    new Vector2(880f, 34f), JoinDogUIFactory.RoundedSprite(),
                    new Color(0.82f, 1f, 1f, 0.18f));
                MapAmbientMotion motion = foam.gameObject.AddComponent<MapAmbientMotion>();
                motion.drift = new Vector2(55f, 2f);
                motion.speed = 0.12f + (i % 4) * 0.025f;
                motion.phase = i * 0.71f;
            }
            int bubbleCount = hasIllustratedBackground ? 10 : 18;
            for (int i = 0; i < bubbleCount; i++)
            {
                float denominator = Mathf.Max(1f, bubbleCount - 1f);
                float y = Mathf.Lerp(bottom + 160f, top - 120f, i / denominator);
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

        private void CreateMountainAtmosphere(CampaignZoneEntry zone, float bottom, float top,
            bool hasIllustratedBackground)
        {
            CreateContentImage("MountainNight", new Vector2(0f, (bottom + top) * 0.5f),
                new Vector2(ContentWidth + 190f, top - bottom), JoinDogUIFactory.RoundedSprite(),
                new Color(0.035f, 0.075f, 0.19f, hasIllustratedBackground ? 0.07f : 0.48f)).type = Image.Type.Sliced;
            int snowCount = hasIllustratedBackground ? 18 : 26;
            for (int i = 0; i < snowCount; i++)
            {
                float denominator = Mathf.Max(1f, snowCount - 1f);
                float y = Mathf.Lerp(bottom + 110f, top - 90f, i / denominator);
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
            int auroraCount = hasIllustratedBackground ? 2 : 3;
            for (int aurora = 0; aurora < auroraCount; aurora++)
            {
                Image ribbon = CreateContentImage($"Aurora_{aurora}",
                    new Vector2(-120f + aurora * 120f, bottom + 560f + aurora * 410f),
                    new Vector2(920f, 80f), JoinDogUIFactory.RoundedSprite(),
                    aurora % 2 == 0 ? new Color(0.24f, 1f, 0.72f, hasIllustratedBackground ? 0.045f : 0.12f) :
                        new Color(0.46f, 0.58f, 1f, hasIllustratedBackground ? 0.045f : 0.12f));
                ribbon.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -8f + aurora * 7f);
                MapAmbientMotion motion = ribbon.gameObject.AddComponent<MapAmbientMotion>();
                motion.drift = new Vector2(35f, 8f);
                motion.speed = 0.11f + aurora * 0.02f;
                motion.phase = aurora * 1.3f;
            }
        }

        private void CreateAuroraAtmosphere(CampaignZoneEntry zone, float bottom, float top)
        {
            CreateContentImage("AuroraValleyNight", new Vector2(0f, (bottom + top) * 0.5f),
                new Vector2(ContentWidth + 190f, top - bottom), JoinDogUIFactory.RoundedSprite(),
                new Color(0.06f, 0.08f, 0.24f, 0.26f)).type = Image.Type.Sliced;

            Color[] ribbonColors =
            {
                new Color(0.94f, 0.26f, 0.72f, 0.16f),
                new Color(0.24f, 0.96f, 0.84f, 0.14f),
                new Color(0.48f, 0.48f, 1f, 0.16f)
            };
            for (int i = 0; i < ribbonColors.Length; i++)
            {
                Image ribbon = CreateContentImage($"AuroraValleyRibbon_{i}",
                    new Vector2(-120f + i * 120f, bottom + 520f + i * 360f),
                    new Vector2(980f, 92f), JoinDogUIFactory.RoundedSprite(), ribbonColors[i]);
                ribbon.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -10f + i * 8f);
                MapAmbientMotion motion = ribbon.gameObject.AddComponent<MapAmbientMotion>();
                motion.drift = new Vector2(42f, 10f);
                motion.speed = 0.10f + i * 0.025f;
                motion.phase = i * 1.1f;
            }

            for (int i = 0; i < 20; i++)
            {
                float y = Mathf.Lerp(bottom + 130f, top - 120f, i / 19f);
                float x = ((i * 211) % 900) - 450f;
                Image star = CreateContentImage($"AuroraValleyStar_{i}", new Vector2(x, y),
                    Vector2.one * (8f + i % 3 * 4f), JoinDogUIFactory.CircleSprite(),
                    i % 2 == 0 ? new Color(1f, 0.82f, 0.42f, 0.82f) :
                    new Color(0.62f, 0.92f, 1f, 0.78f));
                MapAmbientMotion motion = star.gameObject.AddComponent<MapAmbientMotion>();
                motion.drift = new Vector2(4f, 14f);
                motion.speed = 0.24f + (i % 4) * 0.04f;
                motion.phase = i * 0.47f;
            }
        }

        private void CreateLuminousSummitAtmosphere(CampaignZoneEntry zone, float bottom, float top)
        {
            CreateContentImage("LuminousSummitSky", new Vector2(0f, (bottom + top) * 0.5f),
                new Vector2(ContentWidth + 190f, top - bottom), JoinDogUIFactory.RoundedSprite(),
                new Color(0.05f, 0.10f, 0.30f, 0.34f)).type = Image.Type.Sliced;
            CreateContentImage("LuminousSummitHalo", new Vector2(0f, top - 520f),
                new Vector2(820f, 620f), JoinDogUIFactory.CircleSprite(),
                new Color(1f, 0.70f, 0.18f, 0.18f));

            for (int i = 0; i < 7; i++)
            {
                float x = -420f + i * 140f;
                float y = bottom + 260f + (i % 3) * 110f;
                Image beam = CreateContentImage($"LuminousSummitBeam_{i}", new Vector2(x, y),
                    new Vector2(56f, 720f), JoinDogUIFactory.RoundedSprite(),
                    new Color(1f, 0.82f, 0.34f, 0.08f));
                beam.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -16f + i * 5f);
                MapAmbientMotion motion = beam.gameObject.AddComponent<MapAmbientMotion>();
                motion.drift = new Vector2(10f, 3f);
                motion.speed = 0.08f + i * 0.012f;
                motion.phase = i * 0.6f;
            }

            for (int i = 0; i < 12; i++)
            {
                float y = Mathf.Lerp(bottom + 120f, top - 160f, i / 11f);
                float x = ((i * 173) % 860) - 430f;
                CreateContentImage($"LuminousSummitSpark_{i}", new Vector2(x, y),
                    Vector2.one * (10f + i % 3 * 5f), JoinDogUIFactory.CircleSprite(),
                    new Color(1f, 0.88f, 0.36f, 0.78f));
            }
        }

        private void CreateCelestialAtmosphere(CampaignZoneEntry zone, float bottom, float top)
        {
            for (int i = 0; i < 16; i++)
            {
                float y = Mathf.Lerp(bottom + 120f, top - 100f, i / 15f);
                float x = ((i * 191) % 900) - 450f;
                Image mote = CreateContentImage($"CelestialMote_{i}", new Vector2(x, y),
                    Vector2.one * (9f + i % 3 * 5f), JoinDogUIFactory.CircleSprite(),
                    i % 2 == 0 ? new Color(0.86f, 1f, 1f, 0.72f) : new Color(0.56f, 1f, 0.78f, 0.66f));
                MapAmbientMotion motion = mote.gameObject.AddComponent<MapAmbientMotion>();
                motion.drift = new Vector2(14f, 20f);
                motion.speed = 0.16f + i % 4 * 0.03f;
                motion.phase = i * 0.4f;
            }
        }

        private void CreateRubyAtmosphere(CampaignZoneEntry zone, float bottom, float top)
        {
            for (int i = 0; i < 15; i++)
            {
                float y = Mathf.Lerp(bottom + 130f, top - 110f, i / 14f);
                float x = ((i * 233) % 880) - 440f;
                Image spark = CreateContentImage($"RubySpark_{i}", new Vector2(x, y),
                    Vector2.one * (10f + i % 3 * 5f), JoinDogUIFactory.CircleSprite(),
                    i % 2 == 0 ? new Color(1f, 0.32f, 0.22f, 0.70f) : new Color(1f, 0.72f, 0.20f, 0.72f));
                MapAmbientMotion motion = spark.gameObject.AddComponent<MapAmbientMotion>();
                motion.drift = new Vector2(8f, 16f);
                motion.speed = 0.20f + i % 4 * 0.03f;
                motion.phase = i * 0.45f;
            }
        }

        private void CreateSanctuaryAtmosphere(CampaignZoneEntry zone, float bottom, float top)
        {
            CreateContentImage("SanctuaryHalo", new Vector2(0f, top - 470f), new Vector2(780f, 520f),
                JoinDogUIFactory.CircleSprite(), new Color(1f, 0.78f, 0.20f, 0.14f));
            for (int i = 0; i < 18; i++)
            {
                float y = Mathf.Lerp(bottom + 110f, top - 100f, i / 17f);
                float x = ((i * 167) % 900) - 450f;
                Image star = CreateContentImage($"SanctuaryStar_{i}", new Vector2(x, y),
                    Vector2.one * (9f + i % 3 * 5f), JoinDogUIFactory.CircleSprite(),
                    new Color(1f, 0.86f, 0.38f, 0.76f));
                MapAmbientMotion motion = star.gameObject.AddComponent<MapAmbientMotion>();
                motion.drift = new Vector2(5f, 15f);
                motion.speed = 0.18f + i % 4 * 0.03f;
                motion.phase = i * 0.35f;
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

        private void CreateZoneEntrance(CampaignZoneEntry zone, float boundaryY, int zoneIndex)
        {
            Sprite illustratedEntrance = WorldMapArtLibrary.LoadEntrance(zone.id);
            if (illustratedEntrance != null)
            {
                const float entranceWidth = 880f;
                const float entranceHeight = GatewayArtworkHeight;
                float entranceCenterY = boundaryY + entranceHeight * 0.5f;
                CreateContentImage($"ZoneGateGlow_{zone.id}", new Vector2(0f, entranceCenterY),
                    new Vector2(980f, 650f), JoinDogUIFactory.CircleSprite(),
                    new Color(zone.accentColor.r, zone.accentColor.g, zone.accentColor.b, 0.18f));
                Image entrance = CreateContentImage($"ZoneGateArtwork_{zone.id}",
                    new Vector2(0f, entranceCenterY), new Vector2(entranceWidth, entranceHeight),
                    illustratedEntrance, Color.white);
                // The original source art is portrait-shaped. The map needs a
                // grand, screen-filling gate, so it deliberately uses the full
                // entrance frame rather than shrinking into a narrow column.
                entrance.preserveAspect = false;
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
            float gateY = boundaryY + 300f;
            CreateContentImage($"ZoneGateShadow_{zone.id}", new Vector2(9f, gateY - 16f),
                new Vector2(930f, 164f), JoinDogUIFactory.RoundedSprite(),
                new Color(0.01f, 0.005f, 0.02f, 0.66f)).type = Image.Type.Sliced;
            Image gate = CreateContentImage($"ZoneGate_{zone.id}", new Vector2(0f, gateY),
                new Vector2(930f, 164f), JoinDogUIFactory.RoundedSprite(), dark);
            gate.type = Image.Type.Sliced;
            Outline outline = gate.gameObject.AddComponent<Outline>();
            outline.effectColor = zone.accentColor;
            outline.effectDistance = new Vector2(6f, -6f);
            CreateContentImage($"ZonePostLeft_{zone.id}", new Vector2(-470f, gateY - 18f),
                new Vector2(90f, 300f), JoinDogUIFactory.RoundedSprite(), dark).type = Image.Type.Sliced;
            CreateContentImage($"ZonePostRight_{zone.id}", new Vector2(470f, gateY - 18f),
                new Vector2(90f, 300f), JoinDogUIFactory.RoundedSprite(), dark).type = Image.Type.Sliced;
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
            float y = first.mapY + 35f;
            CreateContentImage($"ZoneBannerGlow_{zone.id}", new Vector2(x, y),
                new Vector2(430f, 132f), JoinDogUIFactory.CircleSprite(),
                new Color(zone.accentColor.r, zone.accentColor.g, zone.accentColor.b, 0.18f));
            Image banner = CreateContentImage($"ZoneBanner_{zone.id}", new Vector2(x, y),
                new Vector2(380f, 86f), JoinDogUIFactory.CircleSprite(),
                new Color(zone.groundColor.r, zone.groundColor.g, zone.groundColor.b, 0.82f));
            Outline outline = banner.gameObject.AddComponent<Outline>();
            outline.effectColor = zone.accentColor;
            outline.effectDistance = new Vector2(3f, -3f);
            Image crest = JoinDogUIFactory.Image(banner.rectTransform, "ZoneCrest",
                JoinDogUIFactory.CircleSprite(), new Vector2(0.03f, 0.25f),
                new Vector2(0.17f, 0.75f), zone.accentColor);
            crest.raycastTarget = false;
            JoinDogUIFactory.Text(banner.rectTransform, "ZoneTitle", zone.displayName, 24f,
                Color.white, TextAlignmentOptions.Center,
                new Vector2(0.15f, 0.43f), new Vector2(0.98f, 0.90f));
            JoinDogUIFactory.Text(banner.rectTransform, "ZoneSubtitle", zone.subtitle.ToUpperInvariant(), 13f,
                new Color(1f, 0.94f, 0.78f, 1f), TextAlignmentOptions.Center,
                new Vector2(0.15f, 0.17f), new Vector2(0.98f, 0.42f));
            int zoneFigures = 0;
            int zoneDiscovered = 0;
            int earnedLevel = AppServices.Instance != null ? AppServices.Instance.Progress.EarnedUnlockedLevel : 1;
            foreach (ToyCollectionCatalog.Figure figure in ToyCollectionCatalog.Figures)
            {
                if (figure.Level >= zone.firstLevel && figure.Level <= zone.lastLevel)
                {
                    zoneFigures++;
                    if (earnedLevel >= figure.Level) zoneDiscovered++;
                }
            }
            JoinDogUIFactory.Text(banner.rectTransform, "ZoneCollection",
                zoneFigures > 0 ? $"FIGURAS DEL MUNDO  {zoneDiscovered}/{zoneFigures}" : "MUNDO DE AVENTURA",
                12f, new Color(1f, 0.82f, 0.35f, 1f), TextAlignmentOptions.Center,
                new Vector2(0.15f, 0.02f), new Vector2(0.98f, 0.17f));
        }

        private void CreateZoneLandmarks(CampaignZoneEntry zone, float bottom, float top, int zoneIndex,
            bool hasIllustratedBackground)
        {
            Random.State oldState = Random.state;
            Random.InitState(4100 + zoneIndex * 97);
            int landmarkCount = hasIllustratedBackground ? 0 : zoneIndex == 0 ? 7 : 11;
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
            else if (zoneIndex == 2 && !hasIllustratedBackground)
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
            mapZoneEmblem = JoinDogUIFactory.Image(header.rectTransform, "ZoneEmblem",
                null, new Vector2(0.19f, 0.70f), new Vector2(0.29f, 0.90f), Color.white);
            mapZoneEmblem.preserveAspect = true;
            mapZoneEmblem.raycastTarget = false;

            Button back = JoinDogUIFactory.Button(header.rectTransform, "Back", "<",
                new Vector2(0.025f, 0.15f), new Vector2(0.16f, 0.83f),
                new Color(0.06f, 0.42f, 0.64f, 1f));
            back.onClick.AddListener(() => AppServices.Instance.GoToMainMenu());

            JoinDogUIFactory.Text(header.rectTransform, "WorldName", catalog.displayName, 33f,
                new Color(1f, 0.78f, 0.18f), TextAlignmentOptions.Center,
                new Vector2(0.29f, 0.42f), new Vector2(0.83f, 0.82f));
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

            Button daily = JoinDogUIFactory.Button(header.rectTransform, "Daily", "RETOS",
                new Vector2(0.025f, 0.15f), new Vector2(0.16f, 0.40f),
                new Color(0.11f, 0.62f, 0.38f, 1f));
            daily.onClick.AddListener(ShowDailyMissions);
        }

        private void RefreshMapProgress()
        {
            if (mapProgressText == null || AppServices.Instance == null) return;
            PlayerProgressService progress = AppServices.Instance.Progress;
            mapProgressText.text =
                $"{progress.CompletedLevels()}/{CampaignCatalog.MaxLevel}   " +
                $"ESTRELLAS {progress.TotalStars()}/{CampaignCatalog.MaxLevel * 3}   " +
                $"HUELLAS {progress.PawPrints}   GALLETAS {progress.Treats}";
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
            bool changed = visibleZoneId != zone.id;
            visibleZoneId = zone.id;
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
            if (mapZoneEmblem != null)
            {
                mapZoneEmblem.sprite = WorldMapArtLibrary.LoadEntrance(zone.id);
                mapZoneEmblem.color = mapZoneEmblem.sprite != null ? Color.white : zone.accentColor;
            }
            if (changed && mapHeaderImage != null)
            {
                if (headerTransitionRoutine != null) StopCoroutine(headerTransitionRoutine);
                headerTransitionRoutine = StartCoroutine(AnimateHeaderTransition());
                if (!PlayerPrefs.HasKey("JoinDog.ZoneSeen." + zone.id))
                {
                    PlayerPrefs.SetInt("JoinDog.ZoneSeen." + zone.id, 1);
                    PlayerPrefs.Save();
                    if (discoveryRoutine != null) StopCoroutine(discoveryRoutine);
                    discoveryRoutine = StartCoroutine(ShowZoneDiscovery(zone));
                }
            }
        }

        private IEnumerator ShowZoneDiscovery(CampaignZoneEntry zone)
        {
            if (zone == null || mapHeaderImage == null) yield break;
            RectTransform parent = mapHeaderImage.transform.parent as RectTransform;
            if (parent == null) yield break;
            Image panel = JoinDogUIFactory.Panel(parent, "ZoneDiscovery_" + zone.id,
                new Vector2(.12f, .70f), new Vector2(.88f, .87f),
                new Color(zone.groundColor.r, zone.groundColor.g, zone.groundColor.b, .96f));
            panel.raycastTarget = false;
            Outline outline = panel.gameObject.AddComponent<Outline>();
            outline.effectColor = Color.Lerp(zone.accentColor, Color.white, .25f);
            outline.effectDistance = new Vector2(3f, -3f);
            TextMeshProUGUI title = JoinDogUIFactory.Text(panel.rectTransform, "DiscoveryTitle",
                "NUEVA ZONA", 14f, Color.Lerp(zone.accentColor, Color.white, .42f),
                TextAlignmentOptions.Center, new Vector2(.05f, .54f), new Vector2(.95f, .86f));
            title.fontStyle = FontStyles.Bold;
            JoinDogUIFactory.Text(panel.rectTransform, "DiscoveryWorld", zone.displayName.ToUpperInvariant(),
                25f, Color.white, TextAlignmentOptions.Center, new Vector2(.04f, .16f), new Vector2(.96f, .62f));
            CanvasGroup group = panel.gameObject.AddComponent<CanvasGroup>();
            Vector3 start = panel.rectTransform.localScale;
            panel.rectTransform.localScale = start * .86f;
            group.alpha = 0f;
            float elapsed = 0f;
            while (elapsed < 2.5f && panel != null)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / .28f);
                float fadeOut = Mathf.Clamp01((2.5f - elapsed) / .34f);
                group.alpha = Mathf.Min(1f, t) * fadeOut;
                panel.rectTransform.localScale = Vector3.Lerp(start * .86f, start, Mathf.SmoothStep(0f, 1f, t));
                yield return null;
            }
            if (panel != null) Destroy(panel.gameObject);
            discoveryRoutine = null;
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
            StartCoroutine(AnimatePanelEntry(card.rectTransform));
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

        private void ShowDailyMissions()
        {
            if (dailyPanel != null) return;
            Canvas canvas = FindAnyObjectByType<Canvas>();
            if (canvas == null || AppServices.Instance == null) return;
            RectTransform root = canvas.GetComponent<RectTransform>();
            Image shade = JoinDogUIFactory.Image(root, "DailyMissions", null, Vector2.zero, Vector2.one,
                new Color(0.005f, 0.02f, 0.04f, 0.82f), true);
            dailyPanel = shade.gameObject;
            Image card = JoinDogUIFactory.Panel(shade.rectTransform, "DailyCard",
                new Vector2(0.07f, 0.15f), new Vector2(0.93f, 0.85f),
                new Color(0.025f, 0.12f, 0.12f, 0.99f));
            Outline outline = card.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(1f, 0.68f, 0.12f, 1f);
            outline.effectDistance = new Vector2(4f, -4f);
            JoinDogUIFactory.Text(card.rectTransform, "Title", "PASEO DIARIO", 36f,
                new Color(1f, 0.82f, 0.24f), TextAlignmentOptions.Center,
                new Vector2(0.08f, 0.84f), new Vector2(0.92f, 0.96f));
            PlayerProgressService progress = AppServices.Instance.Progress;
            JoinDogUIFactory.Text(card.rectTransform, "Missions", progress.GetDailyMissionSummary(), 21f,
                Color.white, TextAlignmentOptions.Center,
                new Vector2(0.10f, 0.64f), new Vector2(0.90f, 0.81f));
            JoinDogUIFactory.Text(card.rectTransform, "Hint",
                progress.IsDailyComplete() ? "RECOMPENSA LISTA: 45 GALLETAS + HUESO" :
                "COMPLETA LOS TRES RETOS PARA CONSEGUIR UN PREMIO",
                18f, new Color(0.56f, 0.92f, 1f), TextAlignmentOptions.Center,
                new Vector2(0.10f, 0.56f), new Vector2(0.90f, 0.64f));
            JoinDogUIFactory.Text(card.rectTransform, "YourStats",
                $"TUS HUELLAS {progress.PawPrints:N0}  •  MEJOR CASCADA x{progress.DeepestCascade}  •  {progress.TotalSpecialsCreated:N0} ESPECIALES",
                16f, new Color(1f, 0.82f, 0.30f), TextAlignmentOptions.Center,
                new Vector2(0.10f, 0.49f), new Vector2(0.90f, 0.56f));
            JoinDogUIFactory.Text(card.rectTransform, "CompanionEnergy",
                $"ENERGÍA DEL COMPAÑERO  {progress.DogEnergy}/5" +
                (progress.SecondsUntilDogEnergyRecovery > 0
                    ? $"  •  SIGUIENTE HUELLA EN {Mathf.CeilToInt(progress.SecondsUntilDogEnergyRecovery / 60f)} MIN"
                    : "  •  LISTO PARA PASEAR"),
                16f, new Color(0.56f, 0.96f, 0.70f), TextAlignmentOptions.Center,
                new Vector2(0.10f, 0.42f), new Vector2(0.90f, 0.49f));
            JoinDogUIFactory.Text(card.rectTransform, "DailyStreak",
                $"RACHA DIARIA  {progress.DailyStreak} DÍAS  •  CADA DÍA MANTIENE TU RACHA",
                16f, new Color(1f, 0.58f, 0.76f), TextAlignmentOptions.Center,
                new Vector2(0.10f, 0.35f), new Vector2(0.90f, 0.42f));
            JoinDogUIFactory.Text(card.rectTransform, "SessionStars", progress.GetSessionStarSummary(),
                17f, new Color(1f, 0.82f, 0.30f), TextAlignmentOptions.Center,
                new Vector2(0.08f, 0.28f), new Vector2(0.92f, 0.35f));

            CampaignZoneEntry rewardZone = catalog.zones.Find(zone => zone != null && zone.id == visibleZoneId)
                ?? catalog.GetZoneForLevel(progress.CurrentLevel);
            string rewardZoneId = rewardZone != null ? rewardZone.id : string.Empty;
            int zoneStars = rewardZone != null ? progress.GetZoneStars(rewardZone.id) : 0;
            bool zoneClaimed = rewardZone != null && progress.IsZoneStarRewardClaimed(rewardZone.id);
            bool zoneReady = rewardZone != null && progress.CanClaimZoneStarReward(rewardZone.id);
            JoinDogUIFactory.Text(card.rectTransform, "ZoneReward",
                rewardZone == null ? "PREMIO DE ZONA NO DISPONIBLE" :
                    $"{rewardZone.displayName}  •  {zoneStars}/{PlayerProgressService.ZoneStarRewardTarget} ESTRELLAS  •  " +
                    (zoneClaimed ? "PREMIO CONSEGUIDO" : "PREMIO: GALLETAS + TIEMPO"),
                17f, zoneReady ? new Color(1f, 0.84f, 0.26f) : new Color(0.72f, 0.86f, 0.90f),
                TextAlignmentOptions.Center, new Vector2(0.08f, 0.21f), new Vector2(0.92f, 0.28f));
            Button claim = JoinDogUIFactory.Button(card.rectTransform, "Claim", "RECLAMAR",
                new Vector2(0.32f, 0.07f), new Vector2(0.61f, 0.22f),
                progress.IsDailyComplete() ? new Color(0.10f, 0.68f, 0.33f, 1f) : new Color(0.25f, 0.29f, 0.31f, 1f));
            claim.interactable = progress.IsDailyComplete();
            claim.onClick.AddListener(() =>
            {
                int treats = progress.ClaimDailyReward();
                if (treats > 0)
                {
                    Destroy(dailyPanel);
                    dailyPanel = null;
                    RefreshMapProgress();
                }
            });
            Button zoneClaim = JoinDogUIFactory.Button(card.rectTransform, "ClaimZone",
                zoneClaimed ? "CONSEGUIDO" : "PREMIO ZONA",
                new Vector2(0.63f, 0.07f), new Vector2(0.92f, 0.22f),
                zoneReady ? new Color(0.68f, 0.38f, 0.10f, 1f) : new Color(0.25f, 0.29f, 0.31f, 1f));
            zoneClaim.interactable = zoneReady;
            zoneClaim.onClick.AddListener(() =>
            {
                if (progress.ClaimZoneStarReward(rewardZoneId) > 0)
                {
                    Destroy(dailyPanel);
                    dailyPanel = null;
                    RefreshMapProgress();
                }
            });
            Button close = JoinDogUIFactory.Button(card.rectTransform, "Close", "<",
                new Vector2(0.08f, 0.07f), new Vector2(0.28f, 0.22f),
                new Color(0.08f, 0.42f, 0.64f, 1f));
            close.onClick.AddListener(() => { Destroy(dailyPanel); dailyPanel = null; });
        }

        private IEnumerator AnimateHeaderTransition()
        {
            RectTransform rect = mapHeaderImage.rectTransform;
            const float duration = 0.24f;
            float elapsed = 0f;
            while (elapsed < duration && rect != null)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float pulse = Mathf.Sin(t * Mathf.PI);
                rect.localScale = Vector3.one * (1f + pulse * 0.018f);
                if (mapWorldNameText != null)
                {
                    Color color = mapWorldNameText.color;
                    color.a = Mathf.Lerp(0.62f, 1f, Mathf.SmoothStep(0f, 1f, t));
                    mapWorldNameText.color = color;
                }
                yield return null;
            }
            if (rect != null) rect.localScale = Vector3.one;
            headerTransitionRoutine = null;
        }

        private static IEnumerator AnimatePanelEntry(RectTransform panel)
        {
            if (panel == null) yield break;
            CanvasGroup group = panel.gameObject.AddComponent<CanvasGroup>();
            Vector3 startScale = Vector3.one * 0.88f;
            const float duration = 0.24f;
            float elapsed = 0f;
            panel.localScale = startScale;
            group.alpha = 0f;
            while (elapsed < duration && panel != null)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float eased = 1f - Mathf.Pow(1f - t, 3f);
                panel.localScale = Vector3.LerpUnclamped(startScale, Vector3.one, eased);
                group.alpha = eased;
                yield return null;
            }
            if (panel != null)
            {
                panel.localScale = Vector3.one;
                group.alpha = 1f;
            }
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

            // Chapter entrances are visual landmarks. Route the chapter
            // transition around the outside of the arch instead of drawing a
            // bright diagonal through its opening and its title.
            if (catalog.GetZoneForLevel(from.level) != pathZone)
            {
                // A crossing is not a rectangular detour. The route reaches
                // the portal threshold, vanishes within the opening and
                // resumes on the other side. The empty middle is intentional:
                // it makes the gate feel like a real passage between worlds.
                float boundaryY = end.y - GatewayPlazaHeight;
                Vector2 approach = new Vector2(0f, boundaryY - 46f);
                Vector2 exit = new Vector2(0f, boundaryY + GatewayArtworkHeight + 46f);
                CreatePathSegment($"PathToGate_{from.level}_{to.level}", start, approach,
                    reached, reachedColor, pathLight);
                CreatePathSegment($"PathFromGate_{from.level}_{to.level}", exit, end,
                    reached, reachedColor, pathLight);
                return;
            }

            CreatePathSegment($"Path_{from.level}_{to.level}", start, end,
                reached, reachedColor, pathLight);
        }

        private void CreatePathSegment(string prefix, Vector2 start, Vector2 end,
            bool reached, Color reachedColor, Color pathLight)
        {
            CreatePathLine($"{prefix}_Shadow", start, end, 30f,
                new Color(0.07f, 0.035f, 0.02f, 0.48f));
            CreatePathLine($"{prefix}_Base", start, end, 19f,
                reached ? reachedColor : new Color(0.24f, 0.23f, 0.20f, 0.82f));
            CreatePathLine($"{prefix}_Light", start, end, 5f,
                reached ? pathLight : new Color(0.52f, 0.49f, 0.40f, 0.46f));
            CreateContentImage($"{prefix}_Dot", (start + end) * 0.5f,
                new Vector2(13f, 13f), JoinDogUIFactory.CircleSprite(),
                reached ? Color.Lerp(reachedColor, Color.white, 0.28f) :
                new Color(0.35f, 0.37f, 0.34f, 0.75f));
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
            CampaignZoneEntry nodeZone = catalog.GetZoneForLevel(entry.level);
            Color zoneAccent = nodeZone != null ? nodeZone.accentColor : new Color(1f, 0.64f, 0.16f, 1f);
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
            ring.color = unlocked
                ? Color.Lerp(zoneAccent, new Color(1f, 0.88f, 0.35f, 1f), 0.46f)
                : new Color(0.24f, 0.27f, 0.27f, 1f);
            Image inner = JoinDogUIFactory.Image(node, "Inner", JoinDogUIFactory.CircleSprite(),
                new Vector2(0.10f, 0.10f), new Vector2(0.90f, 0.90f),
                NodeColor(entry, unlocked, stars, current, zoneAccent));
            inner.raycastTarget = false;
            Image shine = JoinDogUIFactory.Image(node, "Shine", JoinDogUIFactory.CircleSprite(),
                new Vector2(0.25f, 0.60f), new Vector2(0.65f, 0.85f),
                new Color(1f, 1f, 1f, unlocked ? 0.24f : 0.06f));
            shine.raycastTarget = false;

            string label = entry.level.ToString();
            JoinDogUIFactory.Text(node, "Number", label, 48f, Color.white,
                TextAlignmentOptions.Center, new Vector2(0.12f, 0.24f), new Vector2(0.88f, 0.82f));
            string footer = !unlocked ? "CERRADO" : NodeCaption(entry);
            JoinDogUIFactory.Text(node, "Footer", footer, 16f,
                new Color(1f, 0.93f, 0.48f), TextAlignmentOptions.Center,
                new Vector2(0.03f, 0.05f), new Vector2(0.97f, 0.24f));

            // Keep the level number clean and make earned stars readable at a
            // glance, outside the node. This is also visible while scrolling.
            CreateNodeStars(node, stars, unlocked, size);

            if (entry.nodeKind == MapNodeKind.Reward)
            {
                bool claimed = AppServices.Instance.Progress.IsMapChestClaimed(entry.level);
                Image chest = CreateChildPanel(node, "ChestBadge", new Vector2(0.08f, 0.84f),
                    new Vector2(0.92f, 1.10f), claimed
                        ? new Color(0.16f, 0.27f, 0.29f, 0.94f)
                        : new Color(0.66f, 0.30f, 0.74f, 1f));
                chest.raycastTarget = false;
                JoinDogUIFactory.Text(chest.rectTransform, "ChestLabel", claimed ? "COFRE ABIERTO" : "COFRE",
                    15f, claimed ? new Color(0.73f, 0.84f, 0.84f) : new Color(1f, 0.91f, 0.42f),
                    TextAlignmentOptions.Center, new Vector2(0.04f, 0.08f), new Vector2(0.96f, 0.94f));
            }

            if (current)
            {
                Image badge = CreateChildPanel(node, "CurrentBadge", new Vector2(0.05f, 0.82f),
                    new Vector2(0.95f, 1.10f), new Color(0.08f, 0.45f, 0.68f, 1f));
                JoinDogUIFactory.Text(badge.rectTransform, "CurrentLabel", "AQUI",
                    18f, Color.white, TextAlignmentOptions.Center,
                    new Vector2(0.06f, 0.06f), new Vector2(0.94f, 0.94f));
            }

            if (AppServices.Instance.Progress.IsFavorite(entry.level))
            {
                Image favorite = CreateChildPanel(node, "FavoriteBadge", new Vector2(-.04f, .76f),
                    new Vector2(.25f, 1.03f), new Color(.48f, .18f, .72f, .98f));
                JoinDogUIFactory.Text(favorite.rectTransform, "FavoriteIcon", "★", 24f,
                    new Color(1f, .84f, .24f), TextAlignmentOptions.Center,
                    new Vector2(.04f, .04f), new Vector2(.96f, .96f));
                favorite.raycastTarget = false;
            }

            Button button = nodeObject.GetComponent<Button>();
            button.interactable = unlocked;
            int levelNumber = entry.level;
            button.onClick.AddListener(() => StartCoroutine(SelectNodeAndPreview(levelNumber)));
        }

        private static Color NodeColor(CampaignLevelEntry entry, bool unlocked, int stars, bool current,
            Color zoneAccent)
        {
            if (!unlocked) return new Color(0.17f, 0.20f, 0.20f, 1f);
            if (current) return Color.Lerp(zoneAccent, new Color(0.10f, 0.55f, 0.98f, 1f), 0.50f);
            if (stars > 0) return Color.Lerp(zoneAccent, new Color(0.10f, 0.58f, 0.30f, 1f), 0.42f);
            if (entry.nodeKind == MapNodeKind.Finale) return new Color(0.83f, 0.18f, 0.20f, 1f);
            if (entry.nodeKind == MapNodeKind.Hard) return new Color(0.91f, 0.31f, 0.14f, 1f);
            if (entry.nodeKind == MapNodeKind.Reward) return new Color(0.62f, 0.25f, 0.79f, 1f);
            return Color.Lerp(zoneAccent, new Color(0.96f, 0.50f, 0.10f, 1f), 0.28f);
        }

        private static void CreateNodeStars(RectTransform node, int stars, bool unlocked, float nodeSize)
        {
            Sprite starSprite = GetMapRewardStarSprite();
            for (int i = 0; i < 3; i++)
            {
                float left = 0.025f + i * 0.325f;
                bool earned = unlocked && i < stars;
                Vector2 min = new Vector2(left, -0.44f);
                Vector2 max = new Vector2(left + 0.30f, -0.08f);

                // The soft halo is deliberately behind the star. There is no
                // dark square/card left underneath the reward indicator.
                Image halo = JoinDogUIFactory.Image(node, $"StarHalo_{i + 1}",
                    JoinDogUIFactory.CircleSprite(), min - new Vector2(0.025f, 0.025f),
                    max + new Vector2(0.025f, 0.025f), earned
                        ? new Color(1f, 0.67f, 0.08f, 0.38f)
                        : new Color(0.20f, 0.38f, 0.48f, unlocked ? 0.22f : 0.14f));
                halo.raycastTarget = false;

                Image star = JoinDogUIFactory.Image(node, $"RewardStar_{i + 1}", starSprite,
                    min, max, earned
                        ? Color.white
                        : new Color(0.30f, 0.43f, 0.52f, unlocked ? 0.68f : 0.42f));
                star.preserveAspect = true;
                star.raycastTarget = false;
                Shadow shadow = star.gameObject.AddComponent<Shadow>();
                shadow.effectColor = earned
                    ? new Color(0.32f, 0.10f, 0.005f, 0.72f)
                    : new Color(0.005f, 0.025f, 0.05f, 0.72f);
                shadow.effectDistance = new Vector2(2.5f, -3f);

                if (earned)
                {
                    MapAmbientMotion shimmer = star.gameObject.AddComponent<MapAmbientMotion>();
                    shimmer.drift = new Vector2(1.5f, 2.5f);
                    shimmer.speed = 0.70f + i * 0.12f;
                    shimmer.phase = i * 0.9f;
                    shimmer.rotationAmplitude = 2.2f;
                }
            }
        }

        private static Sprite GetMapRewardStarSprite()
        {
            if (mapRewardStarSprite == null)
                mapRewardStarSprite = Resources.Load<Sprite>("UI/icon-score-star");
            return mapRewardStarSprite != null ? mapRewardStarSprite : JoinDogUIFactory.CircleSprite();
        }

        private static string NodeCaption(CampaignLevelEntry entry)
        {
            if (entry.nodeKind == MapNodeKind.Finale) return "FINAL";
            if (entry.nodeKind == MapNodeKind.Reward) return "REGALO";
            if (entry.nodeKind == MapNodeKind.Hard) return "DIFICIL";
            return string.Empty;
        }

        private static Color GetAmbientTint()
        {
            int hour = System.DateTime.Now.Hour;
            int month = System.DateTime.Now.Month;
            Color seasonal = month == 12 || month <= 2
                ? new Color(0.15f, 0.34f, 0.58f, 0.10f)
                : month >= 9 && month <= 11
                    ? new Color(0.62f, 0.25f, 0.05f, 0.09f)
                    : month >= 6 && month <= 8
                        ? new Color(1f, 0.62f, 0.10f, 0.055f)
                        : new Color(0.08f, 0.50f, 0.22f, 0.05f);
            if (hour >= 20 || hour < 6) return new Color(0.02f, 0.06f, 0.20f, 0.28f);
            if (hour < 8 || hour >= 18) return new Color(0.88f, 0.28f, 0.08f, 0.13f);
            return seasonal;
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
            // Character art is independent from the map background and node
            // graphics. Swapping the dog never requires rebuilding a world.
            Sprite mapDogSprite = MapCharacterSelection.LoadSelectedSprite(dogSprite);
            Image dog = JoinDogUIFactory.Image(dogMarker, "Dog", mapDogSprite,
                new Vector2(0.03f, 0.03f), new Vector2(0.97f, 1.00f), Color.white);
            dog.preserveAspect = true;
            if (AppServices.Instance.Progress.HasStarAura)
            {
                Image aura = JoinDogUIFactory.Image(dogMarker, "StarAura", JoinDogUIFactory.CircleSprite(),
                    new Vector2(0.08f, 0.04f), new Vector2(0.92f, 0.96f),
                    new Color(1f, 0.72f, 0.18f, 0.18f));
                aura.raycastTarget = false;
                aura.transform.SetAsFirstSibling();
            }
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
            if(previewPanel!=null) Destroy(previewPanel);
            var entry=catalog.GetLevel(level); var zone=catalog.GetZoneForLevel(level);
            if(entry==null || zone==null) return;
            var root=FindAnyObjectByType<Canvas>().GetComponent<RectTransform>();
            var shade=JoinDogUIFactory.Image(root,"LevelPreview",null,Vector2.zero,Vector2.one,new Color(.07f,.03f,.15f,.78f),true);
            previewPanel=shade.gameObject;
            // La tarjeta hereda el mundo real del nivel: cada bloque de diez
            // conserva su paisaje, luz y color en vez de usar una ventana genérica.
            Sprite zoneArt = WorldMapArtLibrary.LoadBackground(zone.id);
            if (zoneArt != null)
            {
                Image backdrop = JoinDogUIFactory.Image(shade.rectTransform, "ZonePreviewBackdrop",
                    zoneArt, Vector2.zero, Vector2.one, new Color(1f, 1f, 1f, .42f), false);
                backdrop.preserveAspect = false;
                backdrop.raycastTarget = false;
            }
            var card=MagicUI.Card(shade.rectTransform,"MagicLevelCard",new Vector2(.07f,.10f),new Vector2(.93f,.91f)).rectTransform;
            Image cardSurface = card.GetComponent<Image>();
            if (cardSurface != null) cardSurface.color = Color.Lerp(MagicUI.Pearl, zone.skyColor, .16f);
            CreateChapterCardMotif(card, zone, Mathf.Max(0, catalog.zones.IndexOf(zone)));
            Outline cardTheme = card.gameObject.AddComponent<Outline>();
            cardTheme.effectColor = Color.Lerp(zone.accentColor, Color.white, .18f);
            cardTheme.effectDistance = new Vector2(4f, -4f);
            // Cada capítulo tiene una firma visual propia en la tarjeta: una
            // banda de acento y el emblema de su puerta, no solo un cambio de
            // texto. El arte queda decorativo y no interfiere con los botones.
            JoinDogUIFactory.Image(card, "ChapterAccent", null,
                new Vector2(.018f, .12f), new Vector2(.035f, .88f), zone.accentColor);
            Sprite chapterEmblem = WorldMapArtLibrary.LoadEntrance(zone.id);
            if (chapterEmblem != null)
            {
                Image emblem = JoinDogUIFactory.Image(card, "ChapterEmblem", chapterEmblem,
                    new Vector2(.02f, .885f), new Vector2(.16f, 1.02f), Color.white);
                emblem.preserveAspect = true;
                emblem.raycastTarget = false;
            }
            var ribbon=JoinDogUIFactory.Panel(card,"WorldRibbon",new Vector2(.09f,.91f),new Vector2(.91f,.99f),Color.Lerp(MagicUI.Purple,zone.accentColor,.58f));
            MagicUI.PolishButton(ribbon);
            JoinDogUIFactory.Text(ribbon.rectTransform,"World",zone.displayName,30,Color.white,TextAlignmentOptions.Center,new Vector2(.04f,.38f),new Vector2(.96f,.96f));
            JoinDogUIFactory.Text(ribbon.rectTransform,"WorldSubtitle",zone.subtitle,14,
                new Color(1f, .91f, .72f), TextAlignmentOptions.Center,
                new Vector2(.04f,.06f), new Vector2(.96f,.40f));
            MagicUI.Heading(card,"Level",$"NIVEL {level}",92,new Vector2(.07f,.79f),new Vector2(.93f,.91f));
            JoinDogUIFactory.Text(card,"Title",entry.level==11 ? "¡ESTRENAS EL PATITO!" : entry.title,36,MagicUI.Ink,TextAlignmentOptions.Center,new Vector2(.06f,.73f),new Vector2(.94f,.80f));
            int stars=AppServices.Instance.Progress.GetStars(level);
            for(int i=0;i<3;i++)
            {
                float x=.19f+i*.225f;
                var star=JoinDogUIFactory.Image(card,"Star"+i,Resources.Load<Sprite>("UI/icon-score-star"),
                    new Vector2(x,.60f),new Vector2(x+.18f,.735f),i<stars ? Color.white : new Color(.53f,.46f,.64f,.8f));
                star.preserveAspect=true;
            }
            var goal=MagicUI.Card(card,"Goal",new Vector2(.07f,.40f),new Vector2(.93f,.59f)).rectTransform;
            var goalIcon=JoinDogUIFactory.Image(goal,"Icon",Resources.Load<Sprite>("UI/icon-score-star"),new Vector2(.03f,.40f),new Vector2(.18f,.93f),Color.white);
            goalIcon.preserveAspect=true;
            JoinDogUIFactory.Text(goal,"Caption","OBJETIVO",24,MagicUI.Ink,TextAlignmentOptions.Center,new Vector2(.20f,.72f),new Vector2(.96f,.98f));
            var objective=JoinDogUIFactory.Text(goal,"Objective",CampaignCatalog.BuildObjectivePreview(entry),38,MagicUI.Ink,TextAlignmentOptions.Center,new Vector2(.20f,.38f),new Vector2(.96f,.77f));
            objective.enableWordWrapping=true;
            string rule=entry.obstacleType==CampaignObstacleKind.Ice ? "Rompe el hielo de 3 golpes" :
                entry.obstacleType==CampaignObstacleKind.Vine ? "Combina sobre las enredaderas" :
                entry.obstacleType==CampaignObstacleKind.Lantern ? "Enciende los faroles de 2 golpes" :
                entry.obstacleType==CampaignObstacleKind.Sand ? "Limpia la arena combinando cerca" :
                entry.obstacleType==CampaignObstacleKind.PuppyCage ? "Rompe las jaulas para liberar a los cachorros" :
                entry.objectiveKind==CampaignObjectiveKind.DeliverToy ? "Lleva el juguete a la casilla de salida" :
                "Crea especiales con combinaciones grandes";
            JoinDogUIFactory.Text(goal,"Rule",rule,26,MagicUI.Ink,TextAlignmentOptions.Center,new Vector2(.05f,.06f),new Vector2(.95f,.35f));
            string difficulty=entry.difficulty>=4 ? "DIFÍCIL" : entry.difficulty>=2 ? "MEDIO" : "SUAVE";
            var timeIcon=JoinDogUIFactory.Image(card,"TimeIcon",
                Resources.Load<Sprite>(entry.moveLimit > 0 ? "UI/icon-score-paw" : "UI/icon-life-heart"),
                new Vector2(.12f,.315f),new Vector2(.19f,.385f),Color.white);
            timeIcon.preserveAspect=true;
            string pacing = entry.moveLimit > 0
                ? $"{entry.moveLimit} MOVIMIENTOS"
                : $"{entry.durationSeconds} s";
            JoinDogUIFactory.Text(card,"Time",$"{pacing}  ·  {difficulty}",44,MagicUI.Ink,TextAlignmentOptions.Center,new Vector2(.07f,.32f),new Vector2(.93f,.39f));
            var rewardIcon=JoinDogUIFactory.Image(card,"RewardIcon",Resources.Load<Sprite>("UI/icon-score-star"),new Vector2(.16f,.255f),new Vector2(.22f,.315f),Color.white);
            rewardIcon.preserveAspect=true;
            JoinDogUIFactory.Text(card,"Reward",$"PREMIO {entry.rewardTreats} GALLETAS",31,MagicUI.Ink,TextAlignmentOptions.Center,new Vector2(.07f,.26f),new Vector2(.93f,.32f));
            JoinDogUIFactory.Text(card,"Record",$"RÉCORD  {AppServices.Instance.Progress.GetBestScore(level):N0}",23,MagicUI.Ink,TextAlignmentOptions.Center,new Vector2(.07f,.215f),new Vector2(.93f,.26f));
            bool favoriteLevel = AppServices.Instance.Progress.IsFavorite(level);
            Button favoriteButton = JoinDogUIFactory.Button(card, "FavoriteLevel",
                favoriteLevel ? "★ FAVORITO" : "☆ AÑADIR A FAVORITOS",
                new Vector2(.52f, .16f), new Vector2(.93f, .215f),
                favoriteLevel ? new Color(.55f, .28f, .76f, 1f) : new Color(.10f, .48f, .58f, 1f));
            favoriteButton.GetComponentInChildren<TextMeshProUGUI>().fontSizeMax = 22f;
            favoriteButton.onClick.AddListener(() =>
            {
                bool nowFavorite = AppServices.Instance.Progress.ToggleFavorite(level);
                TextMeshProUGUI label = favoriteButton.GetComponentInChildren<TextMeshProUGUI>();
                if (label != null) label.text = nowFavorite ? "★ FAVORITO" : "☆ AÑADIR A FAVORITOS";
                Image buttonImage = favoriteButton.GetComponent<Image>();
                if (buttonImage != null) buttonImage.color = nowFavorite
                    ? new Color(.55f, .28f, .76f, 1f) : new Color(.10f, .48f, .58f, 1f);
            });
            if (entry.level == zone.lastLevel)
            {
                PlayerProgressService progress = AppServices.Instance.Progress;
                bool claimed = progress.IsZoneMemoryClaimed(zone.id);
                bool claimable = progress.CanClaimZoneMemory(zone.id);
                var memory = JoinDogUIFactory.Button(card, "WorldMemory",
                    claimed ? "RECUERDO CONSEGUIDO" : claimable ? "RECLAMAR RECUERDO · 120 GALLETAS" : "RECUERDO DEL MUNDO",
                    new Vector2(.07f, .16f), new Vector2(.47f, .215f), new Color(.58f, .29f, .72f));
                memory.interactable = claimable;
                memory.onClick.AddListener(() =>
                {
                    if (progress.ClaimZoneMemory(zone.id) > 0)
                    {
                        RefreshMapProgress();
                        ShowLevelPreview(level);
                    }
                });
            }
            else if(entry.nodeKind==MapNodeKind.Reward)
            {
                bool claimable=AppServices.Instance.Progress.CanClaimMapChest(level);
                var chest=JoinDogUIFactory.Button(card,"Chest",AppServices.Instance.Progress.IsMapChestClaimed(level) ? "COFRE ABIERTO" : "COFRE DE RECOMPENSAS",new Vector2(.07f,.16f),new Vector2(.47f,.215f),new Color(.12f,.55f,.72f));
                chest.interactable=claimable;
                chest.onClick.AddListener(()=>{if(AppServices.Instance.Progress.ClaimMapChest(level)>0){RefreshMapProgress();ShowLevelPreview(level);}});
            }
            var play=JoinDogUIFactory.Button(card,"PlayLevel","JUGAR",new Vector2(.29f,.045f),new Vector2(.91f,.145f),MagicUI.Purple);
            play.GetComponentInChildren<TextMeshProUGUI>().fontSizeMax=54;
            play.onClick.AddListener(()=>AppServices.Instance.StartLevel(level));
            var close=JoinDogUIFactory.Button(card,"ClosePreview","<",new Vector2(.07f,.045f),new Vector2(.23f,.145f),new Color(.05f,.58f,.77f));
            close.onClick.AddListener(()=>{if(selectedNode!=null) selectedNode.localScale=Vector3.one; selectedNode=null;Destroy(previewPanel);});
        }

        private void CreateChapterCardMotif(RectTransform card, CampaignZoneEntry zone, int chapterIndex)
        {
            if (card == null || zone == null) return;
            // A compact signature makes each ten-level chapter feel authored
            // without competing with the objective or adding another bitmap.
            int beads = 3 + chapterIndex % 4;
            Color accent = Color.Lerp(zone.accentColor, Color.white, .12f);
            for (int i = 0; i < beads; i++)
            {
                float size = i == 0 ? .060f : .036f;
                float x = .72f + i * .055f;
                float y = .932f + (i % 2 == 0 ? .012f : -.006f);
                Image bead = JoinDogUIFactory.Image(card, "ChapterBead_" + chapterIndex + "_" + i,
                    JoinDogUIFactory.CircleSprite(), new Vector2(x, y),
                    new Vector2(x + size, y + size), new Color(accent.r, accent.g, accent.b,
                        i == 0 ? .92f : .48f));
                bead.raycastTarget = false;
            }
            Image glow = JoinDogUIFactory.Image(card, "ChapterGlow_" + chapterIndex,
                JoinDogUIFactory.CircleSprite(), new Vector2(.80f, .72f), new Vector2(1.02f, .94f),
                new Color(accent.r, accent.g, accent.b, .07f));
            glow.raycastTarget = false;
            glow.transform.SetAsFirstSibling();
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
                float duration = AccessibilitySettings.ReducedMotion ? 0f : 1.45f;
                if (duration <= 0f)
                {
                    dogMarker.anchoredPosition = end;
                    yield return CenterOnLevel(pending + 1, 0f);
                    yield break;
                }
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

            if (!AccessibilitySettings.ReducedMotion) StartCoroutine(BobDog());
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
            if (AccessibilitySettings.ReducedMotion) yield break;
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
