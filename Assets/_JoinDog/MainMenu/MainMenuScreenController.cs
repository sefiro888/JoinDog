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
            help.onClick.AddListener(()=>ShowGuidePage(0));
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
            JoinDogUIFactory.Text(card, "Title", "MI COLECCIÓN", 58f, ink, TextAlignmentOptions.Center,
                new Vector2(.06f, .89f), new Vector2(.83f, .98f));
            Button dismissAlbum = JoinDogUIFactory.Button(card, "DismissAlbum", "×",
                new Vector2(.85f, .90f), new Vector2(.97f, .98f), MagicUI.Purple);
            dismissAlbum.onClick.AddListener(() => { Destroy(modal); modal = null; });
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
                JoinDogUIFactory.Text(tile, "Name", figure.Name, 30f, ink, TextAlignmentOptions.Center,
                    new Vector2(.03f, .15f), new Vector2(.97f, .32f));
                JoinDogUIFactory.Text(tile, "State", unlocked ? "DESCUBIERTA" : $"NIVEL {figure.Level}",
                    26f, ink, TextAlignmentOptions.Center, new Vector2(.03f, .02f), new Vector2(.97f, .15f));
                if (figure.Level > 1)
                    JoinDogUIFactory.Text(tile, "Kind", figure.Rarity, 20f,
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

        private void ShowGuidePage(int page)
        {
            string[] titles = { "TU PRIMERA JUGADA", "CREA ESPECIALES", "GRANDES COMBINACIONES", "FRISBEE COMETA", "TUS POTENCIADORES", "CUMPLE TU MISIÓN" };
            string[] bodies = {
                "<b>ARRASTRA O TOCA DOS FICHAS</b>\nIntercambia vecinas para juntar 3 iguales.\n\n<b>TÚ ELIGES</b>\nToca de nuevo para cancelar. Un intercambio sin combinación no gasta movimiento.\n\n<b>ALCANCE DIRECTO</b>\nAl seleccionar un especial, el dorado muestra su alcance aquí, sin cascadas. Se retira al preparar un intercambio. La pelota tiene destinos variables.",
                "<b>3 IGUALES</b>\nElimina esas tres; los destellos no golpean vecinas.\n\n<b>4 / 5 EN LÍNEA</b>\n4 crea fila o columna. 5 crea color: intercambia con el color que necesitas. Una T o L crea área.\n\n<b>PREPARA LA SIGUIENTE JUGADA</b>\nEl violeta anticipa dónde nacerá al preparar el intercambio. La especial queda ahí. Especiales y cascadas cargan al compañero.",
                "<b>6 EN LÍNEA · SUPERNOVA</b>\nRecoge su color y limpia los dos ejes de su casilla.\n\n<b>7 O MÁS · PELOTA REBOTE</b>\nSalta a casillas variables y limpia 3×3 alrededor de cada llegada.\n\n<b>CASCADAS Y FUSIONES</b>\nLa caída puede combinar otra vez. Intercambia dos especiales para unir sus poderes. El reloj se pausa durante la resolución.",
                "<b>4 FRISBEES EN LÍNEA</b>\nCrean un Cometa: limpia sus dos diagonales.\n\n<b>UNE DOS ESPECIALES</b>\nCometa + rayo conserva diagonales y línea. Dos rayos activan sus ejes; pueden compartir fila o columna.\n\n<b>AMPLÍA EL ÁREA</b>\nÁrea + rayo barre 3 filas o columnas. Dos áreas limpian 5×5 alrededor de cada una, hasta el borde del tablero.",
                "<b>PATA · MEZCLAR</b>\nCrea un tablero nuevo.\n\n<b>HUESO · LÍNEA</b>\nLimpia la fila o columna central.\n\n<b>BOLSA · +10 S</b>\nAñade hasta 10 segundos, sin superar el tiempo inicial. La cantidad junto a cada botón indica tus usos disponibles.",
                "<b>LA MISIÓN MANDA</b>\nMira el objetivo y el límite de tiempo o movimientos. Los puntos solos no completan todos los niveles.\n\n<b>OBSTÁCULOS Y COMPAÑERO</b>\nEn obstáculos de varias capas, cada punto claro es un golpe pendiente. Cascadas y especiales cargan a tu mascota: al estar lista, limpia una fila."
            };
            string[] illustrations = { "Pieces/piece-bone-v2", "Pieces/piece-ball-v2", "UI/icon-score-star", "Magic/frisbee-coral-v2", "UI/icon-score-paw", "Pieces/piece-dog-v2" };
            page = Mathf.Clamp(page, 0, titles.Length - 1);
            ShowModal(titles[page], bodies[page]);
            RectTransform card = modal.transform.Find("Card").GetComponent<RectTransform>();
            var border = card.gameObject.AddComponent<Outline>();
            border.effectColor = new Color(.72f,.58f,.88f);
            border.effectDistance = new Vector2(3f,-3f);
            var shadow = card.gameObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(.13f,.03f,.23f,.35f);
            shadow.effectDistance = new Vector2(0f,-9f);
            JoinDogUIFactory.Anchors(card, new Vector2(.06f,.10f), new Vector2(.94f,.90f));
            JoinDogUIFactory.Anchors(card.Find("Title").GetComponent<RectTransform>(),
                new Vector2(.06f,.86f),new Vector2(.94f,.95f));
            Image illustration = JoinDogUIFactory.Image(card,"GuideIllustration",Resources.Load<Sprite>(illustrations[page]),
                new Vector2(.36f,.71f),new Vector2(.64f,.84f),Color.white);
            illustration.preserveAspect = true;
            TextMeshProUGUI body = card.Find("Body").GetComponent<TextMeshProUGUI>();
            JoinDogUIFactory.Anchors(body.rectTransform,new Vector2(.09f,.25f),new Vector2(.91f,.69f));
            body.fontStyle = FontStyles.Normal;
            body.alignment = TextAlignmentOptions.TopLeft;
            body.fontSize = body.fontSizeMax = 44f;
            body.fontSizeMin = 32f;
            body.lineSpacing = 3f;
            var close = card.Find("Close").GetComponent<UnityEngine.UI.Button>();
            JoinDogUIFactory.Anchors(close.GetComponent<RectTransform>(),new Vector2(.88f,.94f),new Vector2(.98f,.995f));
            close.GetComponentInChildren<TextMeshProUGUI>().text = "×";
            JoinDogUIFactory.Text(card,"GuidePage",$"{page+1} / {titles.Length}",34f,MagicUI.Ink,
                TextAlignmentOptions.Center,new Vector2(.30f,.19f),new Vector2(.70f,.25f));
            Button previous = JoinDogUIFactory.Button(card,"GuidePrevious","ANTERIOR",
                new Vector2(.06f,.06f),new Vector2(.47f,.17f),new Color(.08f,.48f,.70f));
            previous.interactable = page > 0;
            previous.GetComponentInChildren<TextMeshProUGUI>().fontSizeMax = 42f;
            previous.onClick.AddListener(()=>ShowGuidePage(page-1));
            Button next = JoinDogUIFactory.Button(card,"GuideNext",page == titles.Length-1 ? "¡A JUGAR!" : "SIGUIENTE",
                new Vector2(.53f,.06f),new Vector2(.94f,.17f),MagicUI.Purple);
            next.GetComponentInChildren<TextMeshProUGUI>().fontSizeMax = 42f;
            next.onClick.AddListener(()=> { if (page == titles.Length-1) Destroy(modal); else ShowGuidePage(page+1); });
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
            TextMeshProUGUI description = JoinDogUIFactory.Text(card.rectTransform, "Body", body, 32f,
                MagicUI.Ink, TextAlignmentOptions.Center,
                new Vector2(0.10f, 0.30f), new Vector2(0.90f, 0.68f));
            description.enableWordWrapping = true;
            description.fontSizeMin = 28f;
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
                if (AccessibilitySettings.ReducedMotion)
                {
                    dogRect.localScale = Vector3.one;
                    dogRect.localRotation = Quaternion.identity;
                    yield return null;
                    continue;
                }
                time += Time.unscaledDeltaTime;
                dogRect.localScale = Vector3.one * (1f + Mathf.Sin(time * 2.2f) * 0.025f);
                dogRect.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(time * 1.3f) * 1.6f);
                yield return null;
            }
        }
    }
}
