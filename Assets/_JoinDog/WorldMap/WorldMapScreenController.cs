using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace JoinDog.App
{
    public sealed class WorldMapScreenController : MonoBehaviour
    {
        public Sprite backgroundSprite;
        public Sprite dogSprite;

        private CampaignCatalog catalog;
        private ScrollRect scrollRect;
        private RectTransform viewport;
        private RectTransform content;
        private RectTransform dogMarker;
        private GameObject previewPanel;
        private readonly Dictionary<int, RectTransform> nodeRects = new Dictionary<int, RectTransform>();
        private const float ContentHeight = 6800f;

        private void Awake()
        {
            catalog = CampaignCatalog.LoadOrCreateRuntime();
            Build();
        }

        private void Start()
        {
            StartCoroutine(FocusAndAnimate());
        }

        private void Build()
        {
            Canvas canvas = JoinDogUIFactory.CreateCanvas("WorldMapCanvas");
            RectTransform root = canvas.GetComponent<RectTransform>();
            Image background = JoinDogUIFactory.Image(root, "WorldBackground", backgroundSprite,
                Vector2.zero, Vector2.one, Color.white);
            background.preserveAspect = false;
            JoinDogUIFactory.Image(root, "WorldTint", null, Vector2.zero, Vector2.one,
                new Color(0.02f, 0.16f, 0.12f, 0.12f));

            GameObject scrollObject = new GameObject("MapScroll", typeof(RectTransform), typeof(ScrollRect));
            scrollObject.transform.SetParent(root, false);
            RectTransform scrollTransform = scrollObject.GetComponent<RectTransform>();
            scrollTransform.anchorMin = new Vector2(0f, 0f);
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
            scrollRect.elasticity = 0.08f;
            scrollRect.inertia = true;
            scrollRect.decelerationRate = 0.12f;
            scrollRect.scrollSensitivity = 40f;
            BuildPathAndNodes();
            BuildHeader(root);
            BuildDogMarker();
        }

        private void BuildHeader(RectTransform root)
        {
            Image header = JoinDogUIFactory.Panel(root, "MapHeader",
                new Vector2(0.035f, 0.905f), new Vector2(0.965f, 0.985f),
                new Color(0.18f, 0.055f, 0.018f, 0.98f));
            Outline outline = header.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(1f, 0.68f, 0.18f, 1f);
            outline.effectDistance = new Vector2(4f, -4f);

            Button back = JoinDogUIFactory.Button(header.rectTransform, "Back", "<",
                new Vector2(0.025f, 0.16f), new Vector2(0.16f, 0.84f),
                new Color(0.08f, 0.42f, 0.62f, 1f));
            back.onClick.AddListener(() => AppServices.Instance.GoToMainMenu());

            JoinDogUIFactory.Text(header.rectTransform, "WorldName", catalog.displayName, 34f,
                new Color(1f, 0.80f, 0.24f), TextAlignmentOptions.Center,
                new Vector2(0.18f, 0.48f), new Vector2(0.80f, 0.90f));
            JoinDogUIFactory.Text(header.rectTransform, "Progress",
                $"NIVEL {AppServices.Instance.Progress.CurrentLevel}  -  " +
                $"* {AppServices.Instance.Progress.TotalStars()}/90",
                21f, Color.white, TextAlignmentOptions.Center,
                new Vector2(0.18f, 0.10f), new Vector2(0.80f, 0.47f));

            Button home = JoinDogUIFactory.Button(header.rectTransform, "Home", "MENU",
                new Vector2(0.82f, 0.16f), new Vector2(0.975f, 0.84f),
                new Color(0.58f, 0.30f, 0.68f, 1f));
            home.onClick.AddListener(() => AppServices.Instance.GoToMainMenu());
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
                if (entry == null) continue;
                CreateNode(entry);
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
            Vector2 delta = end - start;
            GameObject lineObject = new GameObject($"Path_{from.level}_{to.level}", typeof(RectTransform), typeof(Image));
            lineObject.transform.SetParent(content, false);
            RectTransform line = lineObject.GetComponent<RectTransform>();
            line.anchorMin = new Vector2(0.5f, 0f);
            line.anchorMax = new Vector2(0.5f, 0f);
            line.pivot = new Vector2(0.5f, 0.5f);
            line.anchoredPosition = (start + end) * 0.5f;
            line.sizeDelta = new Vector2(delta.magnitude, 22f);
            line.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);
            Image image = lineObject.GetComponent<Image>();
            bool reached = to.level <= AppServices.Instance.Progress.UnlockedLevel;
            image.color = reached
                ? new Color(1f, 0.69f, 0.18f, 0.95f)
                : new Color(0.26f, 0.30f, 0.28f, 0.82f);
            image.raycastTarget = false;
        }

        private void CreateNode(CampaignLevelEntry entry)
        {
            GameObject nodeObject = new GameObject($"LevelNode_{entry.level}", typeof(RectTransform), typeof(Image), typeof(Button));
            nodeObject.transform.SetParent(content, false);
            RectTransform node = nodeObject.GetComponent<RectTransform>();
            node.anchorMin = new Vector2(0.5f, 0f);
            node.anchorMax = new Vector2(0.5f, 0f);
            node.pivot = new Vector2(0.5f, 0.5f);
            node.anchoredPosition = ToContentPoint(entry);
            float size = entry.nodeKind == MapNodeKind.Finale ? 178f : entry.nodeKind == MapNodeKind.Reward ? 158f : 144f;
            node.sizeDelta = new Vector2(size, size);
            nodeRects[entry.level] = node;

            int stars = AppServices.Instance.Progress.GetStars(entry.level);
            bool unlocked = AppServices.Instance.Progress.IsUnlocked(entry.level);
            bool current = entry.level == AppServices.Instance.Progress.CurrentLevel;
            Image image = nodeObject.GetComponent<Image>();
            image.sprite = JoinDogUIFactory.CircleSprite();
            image.color = !unlocked ? new Color(0.30f, 0.33f, 0.32f, 0.96f) :
                current ? new Color(0.10f, 0.62f, 0.94f, 1f) :
                stars > 0 ? new Color(0.16f, 0.70f, 0.34f, 1f) :
                entry.nodeKind == MapNodeKind.Hard ? new Color(0.88f, 0.25f, 0.20f, 1f) :
                entry.nodeKind == MapNodeKind.Reward ? new Color(0.68f, 0.30f, 0.82f, 1f) :
                new Color(1f, 0.58f, 0.12f, 1f);
            Outline outline = nodeObject.AddComponent<Outline>();
            outline.effectColor = unlocked ? new Color(1f, 0.86f, 0.34f, 1f) : new Color(0.12f, 0.14f, 0.13f, 1f);
            outline.effectDistance = new Vector2(5f, -5f);

            string label = unlocked ? entry.level.ToString() : "X";
            JoinDogUIFactory.Text(node, "Number", label, 48f, Color.white,
                TextAlignmentOptions.Center, new Vector2(0.12f, 0.25f), new Vector2(0.88f, 0.83f));
            JoinDogUIFactory.Text(node, "Stars", stars > 0 ? new string('*', stars) : NodeCaption(entry),
                19f, new Color(1f, 0.90f, 0.32f), TextAlignmentOptions.Center,
                new Vector2(0.04f, 0.04f), new Vector2(0.96f, 0.29f));

            Button button = nodeObject.GetComponent<Button>();
            button.interactable = unlocked;
            int levelNumber = entry.level;
            button.onClick.AddListener(() => ShowLevelPreview(levelNumber));
        }

        private static string NodeCaption(CampaignLevelEntry entry)
        {
            return entry.nodeKind == MapNodeKind.Finale ? "FINAL" :
                entry.nodeKind == MapNodeKind.Reward ? "REGALO" :
                entry.nodeKind == MapNodeKind.Hard ? "DIFICIL" : string.Empty;
        }

        private void BuildDogMarker()
        {
            int pending = AppServices.Instance.PendingMapAdvanceFromLevel;
            int level = pending > 0 ? pending : AppServices.Instance.Progress.CurrentLevel;
            level = Mathf.Clamp(level, 1, CampaignCatalog.MaxLevel);
            if (!nodeRects.TryGetValue(level, out RectTransform node)) return;
            Image dog = JoinDogUIFactory.Image(content, "MapDog", dogSprite,
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), Color.white);
            dog.preserveAspect = true;
            dogMarker = dog.rectTransform;
            dogMarker.sizeDelta = new Vector2(150f, 150f);
            dogMarker.pivot = new Vector2(0.5f, 0f);
            dogMarker.anchoredPosition = node.anchoredPosition + new Vector2(0f, 66f);
            dogMarker.SetAsLastSibling();
        }

        private void ShowLevelPreview(int level)
        {
            if (previewPanel != null) Destroy(previewPanel);
            CampaignLevelEntry entry = catalog.GetLevel(level);
            if (entry == null) return;
            Canvas canvas = FindAnyObjectByType<Canvas>();
            RectTransform root = canvas.GetComponent<RectTransform>();
            Image shade = JoinDogUIFactory.Image(root, "LevelPreview", null, Vector2.zero, Vector2.one,
                new Color(0.01f, 0.02f, 0.04f, 0.76f), true);
            previewPanel = shade.gameObject;
            Image card = JoinDogUIFactory.Panel(shade.rectTransform, "LevelCard",
                new Vector2(0.09f, 0.27f), new Vector2(0.91f, 0.73f),
                new Color(0.18f, 0.055f, 0.018f, 0.99f));
            Outline outline = card.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(1f, 0.72f, 0.20f, 1f);
            outline.effectDistance = new Vector2(5f, -5f);
            JoinDogUIFactory.Text(card.rectTransform, "Title", entry.title, 48f,
                new Color(1f, 0.77f, 0.20f), TextAlignmentOptions.Center,
                new Vector2(0.08f, 0.72f), new Vector2(0.92f, 0.91f));
            TextMeshProUGUI objective = JoinDogUIFactory.Text(card.rectTransform, "Objective",
                entry.objectivePreview, 28f, Color.white, TextAlignmentOptions.Center,
                new Vector2(0.08f, 0.47f), new Vector2(0.92f, 0.69f));
            objective.enableWordWrapping = true;
            int stars = AppServices.Instance.Progress.GetStars(level);
            int best = AppServices.Instance.Progress.GetBestScore(level);
            JoinDogUIFactory.Text(card.rectTransform, "Best",
                $"ESTRELLAS  {(stars > 0 ? new string('*', stars) : "-")}\nRECORD  {best:N0}",
                24f, new Color(1f, 0.93f, 0.72f), TextAlignmentOptions.Center,
                new Vector2(0.10f, 0.26f), new Vector2(0.90f, 0.47f));
            Button play = JoinDogUIFactory.Button(card.rectTransform, "PlayLevel", "JUGAR",
                new Vector2(0.36f, 0.06f), new Vector2(0.90f, 0.23f),
                new Color(0.12f, 0.66f, 0.34f, 1f));
            play.onClick.AddListener(() => AppServices.Instance.StartLevel(level));
            Button close = JoinDogUIFactory.Button(card.rectTransform, "ClosePreview", "<",
                new Vector2(0.08f, 0.06f), new Vector2(0.30f, 0.23f),
                new Color(0.08f, 0.42f, 0.62f, 1f));
            close.onClick.AddListener(() => Destroy(previewPanel));
        }

        private IEnumerator FocusAndAnimate()
        {
            yield return null;
            Canvas.ForceUpdateCanvases();
            int pending = AppServices.Instance.ConsumePendingMapAdvance();
            int focusLevel = pending > 0 ? pending : AppServices.Instance.Progress.CurrentLevel;
            CampaignLevelEntry focusEntry = catalog.GetLevel(focusLevel);
            if (focusEntry != null)
            {
                float viewportHeight = viewport.rect.height;
                float range = Mathf.Max(1f, ContentHeight - viewportHeight);
                scrollRect.verticalNormalizedPosition = Mathf.Clamp01((focusEntry.mapY - viewportHeight * 0.46f) / range);
            }

            if (pending > 0 && pending < CampaignCatalog.MaxLevel && dogMarker != null &&
                nodeRects.TryGetValue(pending + 1, out RectTransform target))
            {
                Vector2 start = dogMarker.anchoredPosition;
                Vector2 end = target.anchoredPosition + new Vector2(0f, 66f);
                float duration = 1.35f;
                float elapsed = 0f;
                while (elapsed < duration)
                {
                    elapsed += Time.unscaledDeltaTime;
                    float t = Mathf.Clamp01(elapsed / duration);
                    float eased = t * t * (3f - 2f * t);
                    Vector2 position = Vector2.Lerp(start, end, eased);
                    position.y += Mathf.Sin(t * Mathf.PI * 5f) * 24f;
                    dogMarker.anchoredPosition = position;
                    yield return null;
                }
                dogMarker.anchoredPosition = end;
            }

            StartCoroutine(BobDog());
        }

        private IEnumerator BobDog()
        {
            if (dogMarker == null) yield break;
            Vector2 basePosition = dogMarker.anchoredPosition;
            float time = 0f;
            while (dogMarker != null)
            {
                time += Time.unscaledDeltaTime;
                dogMarker.anchoredPosition = basePosition + Vector2.up * (Mathf.Sin(time * 2.5f) * 8f);
                yield return null;
            }
        }

        private void OnDisable()
        {
            if (scrollRect != null && AppServices.Instance != null)
                AppServices.Instance.Progress.SetMapScroll(scrollRect.verticalNormalizedPosition);
        }
    }
}
