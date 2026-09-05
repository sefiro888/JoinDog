using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace JoinDog.App
{
    public sealed class MainMenuScreenController : MonoBehaviour
    {
        public Sprite backgroundSprite;
        public Sprite dogSprite;
        private RectTransform dogRect;
        private Image dogImage;
        private GameObject modal;

        private void Awake()
        {
            Build();
        }

        private void Build()
        {
            Canvas canvas = JoinDogUIFactory.CreateCanvas("MainMenuCanvas");
            RectTransform root = canvas.GetComponent<RectTransform>();
            Image background = JoinDogUIFactory.Image(root, "ParkBackground", backgroundSprite,
                Vector2.zero, Vector2.one, Color.white);
            background.preserveAspect = false;
            JoinDogUIFactory.Image(root, "MenuTint", null, Vector2.zero, Vector2.one,
                new Color(0.02f, 0.16f, 0.23f, 0.26f));

            Image header = JoinDogUIFactory.Panel(root, "TitleCloud",
                new Vector2(0.09f, 0.67f), new Vector2(0.91f, 0.91f),
                new Color(0.18f, 0.055f, 0.018f, 0.94f));
            Outline headerOutline = header.gameObject.AddComponent<Outline>();
            headerOutline.effectColor = new Color(1f, 0.68f, 0.18f, 1f);
            headerOutline.effectDistance = new Vector2(5f, -5f);
            JoinDogUIFactory.Text(header.rectTransform, "Title", "JOIN DOG", 84f,
                new Color(1f, 0.72f, 0.12f), TextAlignmentOptions.Center,
                new Vector2(0.04f, 0.40f), new Vector2(0.96f, 0.88f));
            JoinDogUIFactory.Text(header.rectTransform, "Subtitle", "UNA AVENTURA DE PUZZLES", 24f,
                new Color(1f, 0.95f, 0.78f), TextAlignmentOptions.Center,
                new Vector2(0.08f, 0.16f), new Vector2(0.92f, 0.40f));

            dogImage = JoinDogUIFactory.Image(root, "MenuDog",
                MapCharacterSelection.LoadSelectedSprite(dogSprite),
                new Vector2(0.32f, 0.49f), new Vector2(0.68f, 0.69f), Color.white);
            dogImage.preserveAspect = true;
            dogRect = dogImage.rectTransform;

            Button play = JoinDogUIFactory.Button(root, "Play", "JUGAR",
                new Vector2(0.18f, 0.32f), new Vector2(0.82f, 0.43f),
                new Color(0.10f, 0.65f, 0.34f, 1f));
            play.onClick.AddListener(() => AppServices.Instance.GoToWorldMap());

            Button settings = JoinDogUIFactory.Button(root, "Settings", "AJUSTES",
                new Vector2(0.18f, 0.20f), new Vector2(0.49f, 0.285f),
                new Color(0.05f, 0.42f, 0.68f, 1f));
            settings.onClick.AddListener(ShowSettingsModal);

            Button help = JoinDogUIFactory.Button(root, "Help", "CÓMO JUGAR",
                new Vector2(0.51f, 0.20f), new Vector2(0.82f, 0.285f),
                new Color(0.58f, 0.30f, 0.72f, 1f));
            help.onClick.AddListener(() => ShowModal("CÓMO JUGAR",
                "Elige un nivel en el mapa. Intercambia fichas vecinas y cumple el objetivo antes de que termine el tiempo."));

            Button collection = JoinDogUIFactory.Button(root, "FigureAlbum", "ÁLBUM DE FIGURAS",
                new Vector2(.18f, .105f), new Vector2(.82f, .18f), new Color(.035f, .48f, .45f));
            collection.onClick.AddListener(ShowFigureAlbum);

            JoinDogUIFactory.Text(root, "ProgressHint",
                $"PROGRESO  {AppServices.Instance.Progress.CompletedLevels()}/{CampaignCatalog.MaxLevel}  ·  " +
                $"ESTRELLAS  {AppServices.Instance.Progress.TotalStars()}/{CampaignCatalog.MaxLevel * 3}",
                22f, Color.white, TextAlignmentOptions.Center,
                new Vector2(0.08f, 0.035f), new Vector2(0.92f, 0.085f));
            StartCoroutine(AnimateDog());
        }

        private void ShowFigureAlbum()
        {
            if (modal != null) Destroy(modal);
            RectTransform root = GetComponentInParent<Canvas>()?.GetComponent<RectTransform>();
            if (root == null) root = FindAnyObjectByType<Canvas>().GetComponent<RectTransform>();
            Image shade = JoinDogUIFactory.Image(root, "FigureAlbumModal", null, Vector2.zero, Vector2.one,
                new Color(.01f, .08f, .09f, .88f), true);
            modal = shade.gameObject;
            Color ink = new Color(.035f, .30f, .29f);
            RectTransform card = JoinDogUIFactory.Panel(shade.rectTransform, "AlbumCard",
                new Vector2(.05f, .08f), new Vector2(.95f, .92f), new Color(1f, .95f, .81f)).rectTransform;
            int earnedLevel = AppServices.Instance.Progress.EarnedUnlockedLevel;
            int discovered = ToyCollectionCatalog.DiscoveredCount(earnedLevel);
            JoinDogUIFactory.Text(card, "Title", "MI COLECCIÓN", 58f, ink, TextAlignmentOptions.Center,
                new Vector2(.06f, .89f), new Vector2(.94f, .98f));
            JoinDogUIFactory.Text(card, "Count", $"{discovered} / 6 FIGURAS DESCUBIERTAS", 30f, ink,
                TextAlignmentOptions.Center, new Vector2(.06f, .83f), new Vector2(.94f, .89f));

            for (int i = 0; i < ToyCollectionCatalog.Figures.Length; i++)
            {
                var figure = ToyCollectionCatalog.Figures[i];
                bool unlocked = earnedLevel >= figure.Level;
                float x = .05f + (i % 2) * .47f;
                float y = .625f - (i / 2) * .205f;
                RectTransform tile = JoinDogUIFactory.Panel(card, "Figure" + i,
                    new Vector2(x, y), new Vector2(x + .43f, y + .19f),
                    unlocked ? new Color(.86f, .92f, .83f) : new Color(.76f, .79f, .75f)).rectTransform;
                Image art = JoinDogUIFactory.Image(tile, "Art", Resources.Load<Sprite>(figure.Resource),
                    new Vector2(.22f, .30f), new Vector2(.78f, .96f),
                    unlocked ? Color.white : new Color(.16f, .27f, .27f, .65f));
                art.preserveAspect = true;
                JoinDogUIFactory.Text(tile, "Name", figure.Name, 34f, ink, TextAlignmentOptions.Center,
                    new Vector2(.03f, .15f), new Vector2(.97f, .32f));
                JoinDogUIFactory.Text(tile, "State", unlocked ? "DESCUBIERTA" : $"NIVEL {figure.Level}",
                    23f, ink, TextAlignmentOptions.Center, new Vector2(.03f, .02f), new Vector2(.97f, .15f));
            }
            JoinDogUIFactory.Text(card, "NextFigure", ToyCollectionCatalog.NextHint(earnedLevel), 28f,
                ink, TextAlignmentOptions.Center, new Vector2(.04f, .135f), new Vector2(.96f, .20f));
            Button close = JoinDogUIFactory.Button(card, "CloseAlbum", "SEGUIR JUGANDO",
                new Vector2(.15f, .035f), new Vector2(.85f, .125f), new Color(.035f, .48f, .45f));
            close.onClick.AddListener(() => Destroy(modal));
        }

        private void ShowModal(string title, string body)
        {
            if (modal != null) Destroy(modal);
            Canvas canvas = FindAnyObjectByType<Canvas>();
            RectTransform root = canvas.GetComponent<RectTransform>();
            Image shade = JoinDogUIFactory.Image(root, "MenuModal", null, Vector2.zero, Vector2.one,
                new Color(0.01f, 0.02f, 0.04f, 0.78f), true);
            modal = shade.gameObject;
            Image card = JoinDogUIFactory.Panel(shade.rectTransform, "Card",
                new Vector2(0.10f, 0.29f), new Vector2(0.90f, 0.71f),
                new Color(0.18f, 0.055f, 0.018f, 0.99f));
            JoinDogUIFactory.Text(card.rectTransform, "Title", title, 42f,
                new Color(1f, 0.75f, 0.20f), TextAlignmentOptions.Center,
                new Vector2(0.08f, 0.70f), new Vector2(0.92f, 0.91f));
            TextMeshProUGUI description = JoinDogUIFactory.Text(card.rectTransform, "Body", body, 26f,
                Color.white, TextAlignmentOptions.Center,
                new Vector2(0.10f, 0.30f), new Vector2(0.90f, 0.68f));
            description.enableWordWrapping = true;
            Button close = JoinDogUIFactory.Button(card.rectTransform, "Close", "CERRAR",
                new Vector2(0.23f, 0.07f), new Vector2(0.77f, 0.24f),
                new Color(0.08f, 0.48f, 0.70f, 1f));
            close.onClick.AddListener(() => Destroy(modal));
        }

        private void ShowSettingsModal()
        {
            if (modal != null) Destroy(modal);
            Canvas canvas = FindAnyObjectByType<Canvas>();
            RectTransform root = canvas.GetComponent<RectTransform>();
            Image shade = JoinDogUIFactory.Image(root, "SettingsModal", null, Vector2.zero, Vector2.one,
                new Color(0.01f, 0.02f, 0.04f, 0.82f), true);
            modal = shade.gameObject;
            Image card = JoinDogUIFactory.Panel(shade.rectTransform, "SettingsCard",
                new Vector2(0.07f, 0.20f), new Vector2(0.93f, 0.80f),
                new Color(0.18f, 0.055f, 0.018f, 0.99f));
            Outline cardOutline = card.gameObject.AddComponent<Outline>();
            cardOutline.effectColor = new Color(1f, 0.68f, 0.18f, 1f);
            cardOutline.effectDistance = new Vector2(5f, -5f);

            JoinDogUIFactory.Text(card.rectTransform, "Title", "COLECCIÓN DE COMPAÑEROS", 39f,
                new Color(1f, 0.75f, 0.20f), TextAlignmentOptions.Center,
                new Vector2(0.06f, 0.84f), new Vector2(0.94f, 0.96f));
            JoinDogUIFactory.Text(card.rectTransform, "Hint",
                $"{MapCharacterSelection.Characters.Length}/{MapCharacterSelection.Characters.Length} COMPAÑEROS · ELIGE QUIÉN TE ACOMPAÑA",
                20f,
                new Color(1f, 0.94f, 0.76f), TextAlignmentOptions.Center,
                new Vector2(0.08f, 0.76f), new Vector2(0.92f, 0.84f));

            for (int i = 0; i < MapCharacterSelection.Characters.Length; i++)
            {
                MapCharacterSelection.Character character = MapCharacterSelection.Characters[i];
                float left = i == 0 ? 0.08f : 0.52f;
                float right = i == 0 ? 0.48f : 0.92f;
                bool selected = character.Id == MapCharacterSelection.SelectedId;
                Button choice = JoinDogUIFactory.Button(card.rectTransform, "Character_" + character.Id,
                    character.DisplayName, new Vector2(left, 0.31f), new Vector2(right, 0.72f),
                    selected ? new Color(0.10f, 0.63f, 0.34f, 1f) : new Color(0.05f, 0.34f, 0.48f, 1f));
                RectTransform choiceRect = choice.GetComponent<RectTransform>();
                TextMeshProUGUI label = choice.GetComponentInChildren<TextMeshProUGUI>();
                label.rectTransform.anchorMin = new Vector2(0.05f, 0.02f);
                label.rectTransform.anchorMax = new Vector2(0.95f, 0.20f);
                Image portrait = JoinDogUIFactory.Image(choiceRect, "Portrait",
                    MapCharacterSelection.LoadSprite(character, dogSprite),
                    new Vector2(0.10f, 0.22f), new Vector2(0.90f, 0.94f), Color.white);
                portrait.preserveAspect = true;
                portrait.raycastTarget = false;
                string characterId = character.Id;
                choice.onClick.AddListener(() => SelectCharacter(characterId));
            }

            Button close = JoinDogUIFactory.Button(card.rectTransform, "Close", "LISTO",
                new Vector2(0.23f, 0.08f), new Vector2(0.77f, 0.22f),
                new Color(0.08f, 0.48f, 0.70f, 1f));
            close.onClick.AddListener(() => Destroy(modal));
        }

        private void SelectCharacter(string characterId)
        {
            MapCharacterSelection.Select(characterId);
            if (dogImage != null)
                dogImage.sprite = MapCharacterSelection.LoadSelectedSprite(dogSprite);
            ShowSettingsModal();
        }

        private IEnumerator AnimateDog()
        {
            float time = 0f;
            while (dogRect != null)
            {
                time += Time.unscaledDeltaTime;
                dogRect.localScale = Vector3.one * (1f + Mathf.Sin(time * 2.2f) * 0.025f);
                dogRect.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(time * 1.3f) * 1.6f);
                yield return null;
            }
        }
    }
}
