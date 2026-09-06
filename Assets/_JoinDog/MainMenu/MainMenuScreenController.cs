using System.Collections;
using System.Collections.Generic;
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
        private PetPhotoImport photoImport;

        private void Awake()
        {
            Build();
        }

        private void Build()
        {
            photoImport=gameObject.AddComponent<PetPhotoImport>();
            photoImport.Completed=message=>{ if(message!=null) ShowModal("TU MASCOTA",message); else { dogImage.sprite=MapCharacterSelection.LoadSelectedSprite(dogSprite); ShowSettingsModal(); } };
            Canvas canvas=JoinDogUIFactory.CreateCanvas("MainMenuCanvas");
            RectTransform root=canvas.GetComponent<RectTransform>();
            JoinDogUIFactory.Image(root,"MagicPark",Resources.Load<Sprite>("Magic/park") ?? backgroundSprite,
                Vector2.zero,Vector2.one,Color.white);
            var logo=JoinDogUIFactory.Image(root,"MagicLogo",Resources.Load<Sprite>("Magic/logo"),
                new Vector2(.10f,.65f),new Vector2(.90f,.98f),Color.white);
            logo.preserveAspect=true;
            JoinDogUIFactory.Text(root,"Subtitle","UNA AVENTURA DE PUZZLES",28,MagicUI.Ink,
                TextAlignmentOptions.Center,new Vector2(.08f,.625f),new Vector2(.92f,.67f));
            dogImage=JoinDogUIFactory.Image(root,"MenuDog",MapCharacterSelection.LoadSelectedSprite(dogSprite),
                new Vector2(.23f,.39f),new Vector2(.77f,.635f),Color.white);
            dogImage.preserveAspect=true; dogRect=dogImage.rectTransform;
            var play=JoinDogUIFactory.Button(root,"Play","JUGAR",new Vector2(.16f,.31f),new Vector2(.84f,.41f),MagicUI.Purple);
            play.onClick.AddListener(()=>AppServices.Instance.GoToWorldMap());
            play.GetComponentInChildren<TextMeshProUGUI>().fontSizeMax=58;
            var pets=JoinDogUIFactory.Button(root,"Settings","MASCOTAS",new Vector2(.08f,.22f),new Vector2(.48f,.29f),new Color(.04f,.62f,.82f));
            pets.onClick.AddListener(ShowSettingsModal);
            var help=JoinDogUIFactory.Button(root,"Help","CÓMO JUGAR",new Vector2(.52f,.22f),new Vector2(.92f,.29f),new Color(.04f,.62f,.82f));
            help.onClick.AddListener(()=>ShowModal("CÓMO JUGAR","Intercambia fichas vecinas. Combina tres o más y cumple el objetivo. Las cascadas y los especiales cargan la ayuda de tu mascota."));
            var album=JoinDogUIFactory.Button(root,"FigureAlbum","MI COLECCIÓN",new Vector2(.13f,.13f),new Vector2(.87f,.20f),new Color(.04f,.62f,.82f));
            album.onClick.AddListener(ShowFigureAlbum);
            MenuStat(root,"Levels","UI/icon-score-paw",$"{AppServices.Instance.Progress.CompletedLevels()} / {CampaignCatalog.MaxLevel}","NIVELES",.055f,.49f);
            MenuStat(root,"Stars","UI/icon-score-star",$"{AppServices.Instance.Progress.TotalStars()} / {CampaignCatalog.MaxLevel*3}","ESTRELLAS",.51f,.945f);
            StartCoroutine(AnimateDog());
        }

        private static void MenuStat(RectTransform root,string id,string icon,string value,string caption,float left,float right)
        {
            var card=MagicUI.Card(root,id,new Vector2(left,.02f),new Vector2(right,.105f)).rectTransform;
            var image=JoinDogUIFactory.Image(card,"Icon",Resources.Load<Sprite>(icon),new Vector2(.04f,.17f),new Vector2(.27f,.83f),Color.white);
            image.preserveAspect=true;
            JoinDogUIFactory.Text(card,"Value",value,40,MagicUI.Ink,TextAlignmentOptions.Center,new Vector2(.28f,.38f),new Vector2(.96f,.88f));
            JoinDogUIFactory.Text(card,"Caption",caption,24,MagicUI.Ink,TextAlignmentOptions.Center,new Vector2(.28f,.08f),new Vector2(.96f,.41f));
        }


        private void ShowFigureAlbum()
        {
            if (modal != null) Destroy(modal);
            RectTransform root = GetComponentInParent<Canvas>()?.GetComponent<RectTransform>();
            if (root == null) root = FindAnyObjectByType<Canvas>().GetComponent<RectTransform>();
            Image shade = JoinDogUIFactory.Image(root, "FigureAlbumModal", null, Vector2.zero, Vector2.one,
                new Color(.01f, .08f, .09f, .88f), true);
            modal = shade.gameObject;
            Color ink = MagicUI.Ink;
            RectTransform card = MagicUI.Card(shade.rectTransform, "AlbumCard",
                new Vector2(.05f, .08f), new Vector2(.95f, .92f)).rectTransform;
            int earnedLevel = AppServices.Instance.Progress.EarnedUnlockedLevel;
            int discovered = ToyCollectionCatalog.DiscoveredCount(earnedLevel);
            MagicUI.Heading(card, "Title", "MI COLECCIÓN", 58f,
                new Vector2(.06f, .89f), new Vector2(.94f, .98f));
            JoinDogUIFactory.Text(card, "Count", $"{discovered} / {ToyCollectionCatalog.Figures.Length} FIGURAS Y RECUERDOS", 30f, ink,
                TextAlignmentOptions.Center, new Vector2(.06f, .83f), new Vector2(.94f, .89f));
            List<RectTransform> albumTiles = new List<RectTransform>();

            for (int i = 0; i < ToyCollectionCatalog.Figures.Length; i++)
            {
                var figure = ToyCollectionCatalog.Figures[i];
                bool unlocked = earnedLevel >= figure.Level;
                // Nueve figuras necesitan una cuadrícula 3x3 para que la
                // última fila no tape el mensaje ni el botón inferior.
                float x = .04f + (i % 3) * .32f;
                float y = .655f - (i / 3) * .17f;
                RectTransform tile = JoinDogUIFactory.Panel(card, "Figure" + i,
                    new Vector2(x, y), new Vector2(x + .30f, y + .145f),
                    unlocked ? new Color(.86f, .92f, .83f) : new Color(.76f, .79f, .75f)).rectTransform;
                Outline tileOutline = tile.gameObject.AddComponent<Outline>();
                tileOutline.effectColor = unlocked ? new Color(.55f, .32f, .86f, .8f) : new Color(.30f, .36f, .38f, .7f);
                tileOutline.effectDistance = new Vector2(2f, -2f);
                Image art = JoinDogUIFactory.Image(tile, "Art", Resources.Load<Sprite>(figure.Resource),
                    new Vector2(.18f, .28f), new Vector2(.82f, .96f),
                    unlocked ? Color.white : new Color(.16f, .27f, .27f, .65f));
                art.preserveAspect = true;
                JoinDogUIFactory.Text(tile, "Name", figure.Name, 25f, ink, TextAlignmentOptions.Center,
                    new Vector2(.03f, .15f), new Vector2(.97f, .32f));
                JoinDogUIFactory.Text(tile, "State", unlocked ? "DESCUBIERTA" : $"NIVEL {figure.Level}",
                    17f, ink, TextAlignmentOptions.Center, new Vector2(.03f, .02f), new Vector2(.97f, .15f));
                if (figure.Level > 1)
                    JoinDogUIFactory.Text(tile, "Kind", $"{figure.Rarity} · {(figure.Playable ? "FICHA NUEVA" : "RECUERDO")}", 11f,
                        figure.Rarity == "ÉPICA" ? new Color(.62f, .22f, .82f) :
                        figure.Rarity == "ESPECIAL" ? new Color(.06f, .52f, .68f) : MagicUI.Purple,
                        TextAlignmentOptions.Center, new Vector2(.04f, .82f), new Vector2(.96f, .99f));
                albumTiles.Add(tile);
            }
            JoinDogUIFactory.Text(card, "NextFigure", ToyCollectionCatalog.NextHint(earnedLevel), 28f,
                ink, TextAlignmentOptions.Center, new Vector2(.04f, .135f), new Vector2(.96f, .20f));
            PlayerProgressService progress = AppServices.Instance.Progress;
            bool collectionComplete = discovered >= ToyCollectionCatalog.Figures.Length;
            bool hasMilestone = progress.CanClaimNextCollectionMilestone(discovered, out int milestoneThreshold, out int milestoneReward);
            bool hasCosmetic = progress.CanClaimStarAura();
            string collectionButtonLabel = hasCosmetic
                ? "DESBLOQUEAR AURA ESTELAR · 30★"
                : hasMilestone
                ? $"RECLAMAR GRUPO {milestoneThreshold} · {milestoneReward} GALLETAS"
                : collectionComplete && progress.CanClaimCollectionReward() ? "RECLAMAR 250 GALLETAS"
                : collectionComplete ? "PREMIO DE COLECCIÓN CONSEGUIDO" : "SEGUIR JUGANDO";
            Button close = JoinDogUIFactory.Button(card, "CloseAlbum",
                collectionButtonLabel,
                new Vector2(.15f, .035f), new Vector2(.85f, .125f), new Color(.035f, .48f, .45f));
            close.interactable = true;
            close.onClick.AddListener(() =>
            {
                if (hasCosmetic)
                {
                    progress.ClaimStarAura();
                    Destroy(modal);
                    modal = null;
                    ShowFigureAlbum();
                    return;
                }
                if (hasMilestone)
                {
                    progress.ClaimCollectionMilestone(milestoneThreshold);
                    Destroy(modal);
                    modal = null;
                    ShowFigureAlbum();
                    return;
                }
                if (collectionComplete && progress.CanClaimCollectionReward())
                {
                    progress.ClaimCollectionReward();
                    Destroy(modal);
                    modal = null;
                    ShowFigureAlbum();
                    return;
                }
                Destroy(modal);
                modal = null;
                AppServices.Instance.StartLevel(progress.CurrentLevel);
            });
            StartCoroutine(AnimateAlbumTiles(albumTiles));
        }

        private static IEnumerator AnimateAlbumTiles(List<RectTransform> tiles)
        {
            if (tiles == null || AccessibilitySettings.ReducedMotion) yield break;
            for (int i = 0; i < tiles.Count; i++)
            {
                RectTransform tile = tiles[i];
                if (tile == null) continue;
                tile.localScale = Vector3.one * .82f;
                CanvasGroup group = tile.gameObject.AddComponent<CanvasGroup>();
                group.alpha = 0f;
                float elapsed = 0f;
                const float duration = .18f;
                while (elapsed < duration && tile != null)
                {
                    elapsed += Time.unscaledDeltaTime;
                    float t = Mathf.Clamp01(elapsed / duration);
                    float eased = 1f - Mathf.Pow(1f - t, 3f);
                    tile.localScale = Vector3.LerpUnclamped(Vector3.one * .82f, Vector3.one, eased);
                    group.alpha = eased;
                    yield return null;
                }
                if (tile != null)
                {
                    tile.localScale = Vector3.one;
                    group.alpha = 1f;
                }
                yield return new WaitForSecondsRealtime(.035f);
            }
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
                MagicUI.Pearl);
            JoinDogUIFactory.Text(card.rectTransform, "Title", title, 42f,
                MagicUI.Ink, TextAlignmentOptions.Center,
                new Vector2(0.08f, 0.70f), new Vector2(0.92f, 0.91f));
            TextMeshProUGUI description = JoinDogUIFactory.Text(card.rectTransform, "Body", body, 26f,
                MagicUI.Ink, TextAlignmentOptions.Center,
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
                MagicUI.Pearl);
            Outline cardOutline = card.gameObject.AddComponent<Outline>();
            cardOutline.effectColor = new Color(.7f,.54f,.9f);
            cardOutline.effectDistance = new Vector2(5f, -5f);

            JoinDogUIFactory.Text(card.rectTransform, "Title", "ELIGE TU COMPAÑERO", 39f,
                MagicUI.Ink, TextAlignmentOptions.Center,
                new Vector2(0.06f, 0.84f), new Vector2(0.94f, 0.96f));
            JoinDogUIFactory.Text(card.rectTransform, "Hint",
                "FOTO LOCAL: USA UN RETRATO CENTRADO",
                20f,
                MagicUI.Ink, TextAlignmentOptions.Center,
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

            Button photo = JoinDogUIFactory.Button(card.rectTransform,"PetPhoto","ELEGIR FOTO LOCAL",
                new Vector2(.10f,.225f),new Vector2(.90f,.30f),MagicUI.Purple);
            photo.onClick.AddListener(()=>photoImport.Choose());
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
