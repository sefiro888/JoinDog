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

            Image dog = JoinDogUIFactory.Image(root, "MenuDog", dogSprite,
                new Vector2(0.32f, 0.49f), new Vector2(0.68f, 0.69f), Color.white);
            dog.preserveAspect = true;
            dogRect = dog.rectTransform;

            Button play = JoinDogUIFactory.Button(root, "Play", "JUGAR",
                new Vector2(0.18f, 0.32f), new Vector2(0.82f, 0.43f),
                new Color(0.10f, 0.65f, 0.34f, 1f));
            play.onClick.AddListener(() => AppServices.Instance.GoToWorldMap());

            Button settings = JoinDogUIFactory.Button(root, "Settings", "AJUSTES",
                new Vector2(0.18f, 0.20f), new Vector2(0.49f, 0.285f),
                new Color(0.05f, 0.42f, 0.68f, 1f));
            settings.onClick.AddListener(() => ShowModal("AJUSTES",
                "Los controles de sonido y vibración se conservarán entre pantallas."));

            Button help = JoinDogUIFactory.Button(root, "Help", "CÓMO JUGAR",
                new Vector2(0.51f, 0.20f), new Vector2(0.82f, 0.285f),
                new Color(0.58f, 0.30f, 0.72f, 1f));
            help.onClick.AddListener(() => ShowModal("CÓMO JUGAR",
                "Elige un nivel en el mapa. Intercambia fichas vecinas y cumple el objetivo antes de que termine el tiempo."));

            JoinDogUIFactory.Text(root, "ProgressHint",
                $"PROGRESO  {AppServices.Instance.Progress.UnlockedLevel - 1}/30  ·  " +
                $"ESTRELLAS  {AppServices.Instance.Progress.TotalStars()}/90",
                22f, Color.white, TextAlignmentOptions.Center,
                new Vector2(0.08f, 0.10f), new Vector2(0.92f, 0.16f));
            StartCoroutine(AnimateDog());
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
