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

        private const float ContentHeight = 6900f;
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
        private TextMeshProUGUI storeBalanceText;
        private TextMeshProUGUI storeStatusText;
        private readonly Dictionary<BoosterKind, TextMeshProUGUI> storeCountTexts =
            new Dictionary<BoosterKind, TextMeshProUGUI>();
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
                atmosphere.a = index == 0 ? 0.52f : 0.78f;
                Image zonePanel = CreateContentImage($"Zone_{zone.id}",
                    new Vector2(0f, (bottom + top) * 0.5f),
                    new Vector2(ContentWidth + 80f, top - bottom),
                    JoinDogUIFactory.RoundedSprite(), atmosphere);
                zonePanel.type = Image.Type.Sliced;

                CreateZoneAtmosphere(zone, bottom, top, index);
                CreateZoneBanner(zone, first);
                CreateZoneLandmarks(zone, bottom, top, index);
            }
        }

        private void CreateZoneAtmosphere(CampaignZoneEntry zone, float bottom, float top, int zoneIndex)
        {
            Color upperColor = zone.skyColor;
            upperColor.a = zoneIndex == 0 ? 0.22f : 0.42f;
            CreateContentImage($"ZoneSky_{zone.id}",
                new Vector2(0f, Mathf.Lerp(bottom, top, 0.74f)),
                new Vector2(ContentWidth + 90f, (top - bottom) * 0.52f),
                JoinDogUIFactory.RoundedSprite(), upperColor).type = Image.Type.Sliced;

            Color groundColor = zone.groundColor;
            groundColor.a = zoneIndex == 0 ? 0.24f : 0.50f;
            CreateContentImage($"ZoneGround_{zone.id}",
                new Vector2(0f, Mathf.Lerp(bottom, top, 0.25f)),
                new Vector2(ContentWidth + 90f, (top - bottom) * 0.55f),
                JoinDogUIFactory.RoundedSprite(), groundColor).type = Image.Type.Sliced;

            if (zoneIndex == 1)
            {
                Color forestShade = new Color(0.025f, 0.12f, 0.08f, 0.52f);
                CreateContentImage("ForestShadeLeft", new Vector2(-492f, (bottom + top) * 0.5f),
                    new Vector2(180f, top - bottom), JoinDogUIFactory.RoundedSprite(), forestShade).type = Image.Type.Sliced;
                CreateContentImage("ForestShadeRight", new Vector2(492f, (bottom + top) * 0.5f),
                    new Vector2(180f, top - bottom), JoinDogUIFactory.RoundedSprite(), forestShade).type = Image.Type.Sliced;
                for (int i = 0; i < 8; i++)
                {
                    float y = Mathf.Lerp(bottom + 130f, top - 130f, i / 7f);
                    float x = i % 2 == 0 ? -455f : 455f;
                    CreateContentImage($"ForestCanopy_{i}", new Vector2(x, y),
                        new Vector2(245f, 245f), JoinDogUIFactory.CircleSprite(),
                        new Color(0.08f, 0.34f + (i % 3) * 0.035f, 0.16f, 0.78f));
                }
            }
            else if (zoneIndex == 2)
            {
                Color night = new Color(0.16f, 0.10f, 0.38f, 0.46f);
                CreateContentImage("FestivalTwilight", new Vector2(0f, (bottom + top) * 0.5f),
                    new Vector2(ContentWidth + 90f, top - bottom), JoinDogUIFactory.RoundedSprite(), night).type = Image.Type.Sliced;
                for (int i = 0; i < 12; i++)
                {
                    float y = Mathf.Lerp(bottom + 100f, top - 100f, i / 11f);
                    float x = (i % 2 == 0 ? -1f : 1f) * (360f + (i % 3) * 45f);
                    Image light = CreateContentImage($"FestivalLight_{i}", new Vector2(x, y),
                        new Vector2(34f, 34f), JoinDogUIFactory.CircleSprite(),
                        i % 3 == 0 ? new Color(1f, 0.35f, 0.45f, 0.94f) :
                        i % 3 == 1 ? new Color(1f, 0.80f, 0.18f, 0.94f) :
                        new Color(0.35f, 0.78f, 1f, 0.94f));
                    MapAmbientMotion motion = light.gameObject.AddComponent<MapAmbientMotion>();
                    motion.drift = new Vector2(3f, 7f);
                    motion.speed = 0.7f + (i % 4) * 0.08f;
                    motion.phase = i * 0.73f;
                }
            }

            if (zoneIndex > 0)
            {
                float gateY = bottom + 115f;
                CreateContentImage($"ZoneGateShadow_{zone.id}", new Vector2(7f, gateY - 10f),
                    new Vector2(720f, 90f), JoinDogUIFactory.RoundedSprite(), new Color(0.02f, 0.01f, 0.01f, 0.55f)).type = Image.Type.Sliced;
                Image gate = CreateContentImage($"ZoneGate_{zone.id}", new Vector2(0f, gateY),
                    new Vector2(720f, 90f), JoinDogUIFactory.RoundedSprite(),
                    new Color(zone.accentColor.r * 0.48f, zone.accentColor.g * 0.48f, zone.accentColor.b * 0.48f, 0.98f));
                gate.type = Image.Type.Sliced;
                Outline outline = gate.gameObject.AddComponent<Outline>();
                outline.effectColor = zone.accentColor;
                outline.effectDistance = new Vector2(4f, -4f);
                JoinDogUIFactory.Text(gate.rectTransform, "GateTitle", $"ENTRAS EN {zone.displayName}", 27f,
                    Color.white, TextAlignmentOptions.Center, new Vector2(0.05f, 0.12f), new Vector2(0.95f, 0.88f));
            }
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

        private void CreateZoneLandmarks(CampaignZoneEntry zone, float bottom, float top, int zoneIndex)
        {
            Random.State oldState = Random.state;
            Random.InitState(4100 + zoneIndex * 97);
            for (int i = 0; i < 7; i++)
            {
                bool left = i % 2 == 0;
                float x = (left ? -1f : 1f) * Random.Range(405f, 470f);
                float y = Random.Range(bottom + 210f, top - 150f);
                if (zoneIndex == 0) CreateFlowerPatch($"Flowers_{i}", new Vector2(x, y), zone, i);
                else if (zoneIndex == 1) CreateTree($"Tree_{i}", new Vector2(x, y), zone, i);
                else CreateFestivalPost($"Festival_{i}", new Vector2(x, y), zone, i);
            }

            for (int i = 0; i < 4; i++)
            {
                float x = (i % 2 == 0 ? -1f : 1f) * Random.Range(330f, 410f);
                float y = Mathf.Lerp(bottom + 390f, top - 260f, i / 3f);
                CreateSparkCluster(zone, zoneIndex, i, new Vector2(x, y));
            }
            Random.state = oldState;
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
            Outline outline = header.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(1f, 0.66f, 0.14f, 1f);
            outline.effectDistance = new Vector2(4f, -4f);
            JoinDogUIFactory.Panel(header.rectTransform, "HeaderRibbon",
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
                $"{progress.CompletedLevels()}/30   ESTRELLAS {progress.TotalStars()}/90   GALLETAS {progress.Treats}";
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
            mapWorldNameText.color = zone.accentColor;
        }

        private void ShowRewardStore()
        {
            if (storePanel != null) return;
            Canvas canvas = FindAnyObjectByType<Canvas>();
            RectTransform root = canvas.GetComponent<RectTransform>();
            Image shade = JoinDogUIFactory.Image(root, "RewardStore", null, Vector2.zero, Vector2.one,
                new Color(0.01f, 0.02f, 0.04f, 0.78f), true);
            storePanel = shade.gameObject;

            Image cardShadow = JoinDogUIFactory.Panel(shade.rectTransform, "StoreShadow",
                new Vector2(0.07f, 0.13f), new Vector2(0.94f, 0.87f),
                new Color(0.02f, 0.01f, 0.01f, 0.62f));
            cardShadow.rectTransform.anchoredPosition = new Vector2(10f, -12f);
            Image card = JoinDogUIFactory.Panel(shade.rectTransform, "StoreCard",
                new Vector2(0.075f, 0.14f), new Vector2(0.925f, 0.86f),
                new Color(0.12f, 0.035f, 0.018f, 0.995f));
            Outline outline = card.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(1f, 0.66f, 0.14f, 1f);
            outline.effectDistance = new Vector2(5f, -5f);

            JoinDogUIFactory.Panel(card.rectTransform, "StoreRibbon",
                new Vector2(0.10f, 0.84f), new Vector2(0.90f, 0.95f),
                new Color(0.48f, 0.20f, 0.62f, 1f));
            JoinDogUIFactory.Text(card.rectTransform, "StoreTitle", "TIENDA DE PREMIOS", 35f,
                new Color(1f, 0.82f, 0.22f), TextAlignmentOptions.Center,
                new Vector2(0.08f, 0.83f), new Vector2(0.92f, 0.95f));
            storeBalanceText = JoinDogUIFactory.Text(card.rectTransform, "Balance", string.Empty, 27f,
                Color.white, TextAlignmentOptions.Center,
                new Vector2(0.10f, 0.74f), new Vector2(0.90f, 0.83f));

            CreateStoreItem(card.rectTransform, BoosterKind.Paw, "HUELLA", "RENUEVA EL TABLERO",
                PawCost, 0.57f, new Color(0.08f, 0.55f, 0.92f, 1f));
            CreateStoreItem(card.rectTransform, BoosterKind.Bone, "HUESO", "LIMPIA UNA LINEA",
                BoneCost, 0.40f, new Color(0.13f, 0.76f, 0.72f, 1f));
            CreateStoreItem(card.rectTransform, BoosterKind.Food, "PIENSO", "SUMA 10 SEGUNDOS",
                FoodCost, 0.23f, new Color(0.90f, 0.48f, 0.13f, 1f));

            storeStatusText = JoinDogUIFactory.Text(card.rectTransform, "StoreStatus",
                "GANA GALLETAS SUPERANDO NIVELES", 18f,
                new Color(1f, 0.91f, 0.62f), TextAlignmentOptions.Center,
                new Vector2(0.08f, 0.13f), new Vector2(0.92f, 0.21f));
            Button close = JoinDogUIFactory.Button(card.rectTransform, "CloseStore", "VOLVER",
                new Vector2(0.28f, 0.025f), new Vector2(0.72f, 0.12f),
                new Color(0.07f, 0.42f, 0.64f, 1f));
            close.onClick.AddListener(() =>
            {
                Destroy(storePanel);
                storePanel = null;
                storeBalanceText = null;
                storeStatusText = null;
                storeCountTexts.Clear();
            });
            RefreshStore();
        }

        private void CreateStoreItem(RectTransform parent, BoosterKind kind, string title,
            string description, int cost, float bottom, Color color)
        {
            Image row = JoinDogUIFactory.Panel(parent, $"Store_{kind}",
                new Vector2(0.08f, bottom), new Vector2(0.92f, bottom + 0.145f),
                new Color(0.04f, 0.10f, 0.12f, 0.96f));
            Image badge = JoinDogUIFactory.Image(row.rectTransform, "Badge", JoinDogUIFactory.CircleSprite(),
                new Vector2(0.025f, 0.12f), new Vector2(0.20f, 0.88f), color);
            string badgeText = kind == BoosterKind.Paw ? "P" : kind == BoosterKind.Bone ? "H" : "+10";
            JoinDogUIFactory.Text(badge.rectTransform, "BadgeText", badgeText, 27f, Color.white,
                TextAlignmentOptions.Center, Vector2.zero, Vector2.one);
            JoinDogUIFactory.Text(row.rectTransform, "Title", title, 24f,
                new Color(1f, 0.82f, 0.24f), TextAlignmentOptions.Left,
                new Vector2(0.23f, 0.49f), new Vector2(0.55f, 0.88f));
            JoinDogUIFactory.Text(row.rectTransform, "Description", description, 15f,
                new Color(0.78f, 0.88f, 0.90f), TextAlignmentOptions.Left,
                new Vector2(0.23f, 0.15f), new Vector2(0.58f, 0.50f));
            TextMeshProUGUI count = JoinDogUIFactory.Text(row.rectTransform, "Owned", string.Empty, 19f,
                Color.white, TextAlignmentOptions.Center,
                new Vector2(0.57f, 0.18f), new Vector2(0.70f, 0.82f));
            storeCountTexts[kind] = count;
            Button buy = JoinDogUIFactory.Button(row.rectTransform, "Buy", $"{cost}",
                new Vector2(0.71f, 0.14f), new Vector2(0.975f, 0.86f),
                new Color(0.12f, 0.64f, 0.30f, 1f));
            buy.onClick.AddListener(() => PurchaseBooster(kind, cost));
        }

        private void PurchaseBooster(BoosterKind kind, int cost)
        {
            bool purchased = AppServices.Instance.Progress.TryPurchaseBooster(kind, cost);
            if (storeStatusText != null)
            {
                storeStatusText.text = purchased ? "POTENCIADOR GUARDADO" : "NO TIENES SUFICIENTES GALLETAS";
                storeStatusText.color = purchased
                    ? new Color(0.45f, 1f, 0.58f)
                    : new Color(1f, 0.42f, 0.34f);
            }
            RefreshStore();
            RefreshMapProgress();
        }

        private void RefreshStore()
        {
            if (AppServices.Instance == null) return;
            PlayerProgressService progress = AppServices.Instance.Progress;
            if (storeBalanceText != null) storeBalanceText.text = $"GALLETAS DISPONIBLES: {progress.Treats}";
            foreach (KeyValuePair<BoosterKind, TextMeshProUGUI> pair in storeCountTexts)
                if (pair.Value != null) pair.Value.text = $"TIENES\n{progress.GetBoosterCount(pair.Key)}";
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
            Color reachedColor = pathZone != null
                ? Color.Lerp(pathZone.accentColor, new Color(1f, 0.58f, 0.10f, 1f), 0.42f)
                : new Color(0.96f, 0.55f, 0.10f, 1f);
            CreatePathLine($"PathShadow_{from.level}_{to.level}", start, end, 30f,
                new Color(0.07f, 0.035f, 0.02f, 0.48f));
            CreatePathLine($"PathBase_{from.level}_{to.level}", start, end, 19f,
                reached ? reachedColor : new Color(0.24f, 0.23f, 0.20f, 0.82f));
            CreatePathLine($"PathLight_{from.level}_{to.level}", start, end, 5f,
                reached ? new Color(1f, 0.88f, 0.30f, 0.88f) : new Color(0.52f, 0.49f, 0.40f, 0.46f));

            for (int i = 1; i <= 2; i++)
            {
                Vector2 point = Vector2.Lerp(start, end, i / 3f);
                CreateContentImage($"PathDot_{from.level}_{i}", point, new Vector2(13f, 13f),
                    JoinDogUIFactory.CircleSprite(), reached
                        ? new Color(1f, 0.84f, 0.25f, 0.95f)
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
                new Vector2(0.08f, 0.50f), new Vector2(0.92f, 0.66f));
            objective.enableWordWrapping = true;
            string difficulty = new string('|', Mathf.Clamp(entry.difficulty, 1, 5));
            JoinDogUIFactory.Text(card.rectTransform, "Rules",
                $"DIFICULTAD {difficulty}    {entry.columns}x{entry.rows}    {entry.durationSeconds}s",
                20f, new Color(0.62f, 0.88f, 1f), TextAlignmentOptions.Center,
                new Vector2(0.08f, 0.39f), new Vector2(0.92f, 0.50f));
            int stars = AppServices.Instance.Progress.GetStars(level);
            int best = AppServices.Instance.Progress.GetBestScore(level);
            JoinDogUIFactory.Text(card.rectTransform, "Best",
                $"ESTRELLAS {stars}/3    RÉCORD {best:N0}    PREMIO {entry.rewardTreats}",
                22f, new Color(1f, 0.93f, 0.72f), TextAlignmentOptions.Center,
                new Vector2(0.08f, 0.28f), new Vector2(0.92f, 0.39f));
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
