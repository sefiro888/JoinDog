using UnityEngine;
using System.Collections.Generic;

namespace DogCrush.Board
{
    /// <summary>
    /// Builds the board presentation from the logical grid dimensions.
    /// The visual frame is deliberately independent from the piece sprites,
    /// so levels can change rows, columns or aspect without new board artwork.
    /// </summary>
    public class AdaptiveBoardView : MonoBehaviour
    {
        private const string VisualRootName = "[AdaptiveBoardVisual]";
        // The board owns a real portrait viewport between the objective card
        // (roughly 75% of the screen) and the companion tray (roughly 22%).
        // The previous fixed center worked on one phone but let the frame
        // creep underneath the objective on taller/shorter devices.
        private const float PortraitTopViewport = 0.748f;
        private const float PortraitBottomViewport = 0.238f;

        private Transform visualRoot;
        private Sprite roundedSprite;
        private Sprite contourSprite;
        private Texture2D contourTexture;
        private static readonly System.Collections.Generic.Dictionary<int, Sprite> materialSprites =
            new System.Collections.Generic.Dictionary<int, Sprite>();
        private readonly System.Collections.Generic.Dictionary<Vector2Int, SpriteRenderer> cellSurfaces =
            new System.Collections.Generic.Dictionary<Vector2Int, SpriteRenderer>();
        private readonly System.Collections.Generic.Dictionary<SpriteRenderer, Color> previewColors =
            new System.Collections.Generic.Dictionary<SpriteRenderer, Color>();
        private static Sprite gardenFlowerSprite;
        private readonly Dictionary<Vector2Int, SpriteRenderer> gardenMarkers = new Dictionary<Vector2Int, SpriteRenderer>();
        private static Sprite shelterLeafSprite;
        private readonly Dictionary<Vector2Int, SpriteRenderer> shelterMarkers = new Dictionary<Vector2Int, SpriteRenderer>();
        private static Sprite festivalBellSprite;
        private readonly Dictionary<Vector2Int, SpriteRenderer> bellMarkers = new Dictionary<Vector2Int, SpriteRenderer>();
        private static Sprite coastTideSprite;
        private readonly Dictionary<Vector2Int, SpriteRenderer> tideMarkers = new Dictionary<Vector2Int, SpriteRenderer>();
        private static Sprite mountainWarmSprite;
        private static Sprite auroraPrismSprite;
        private static Sprite summitCrystalSprite;
        private static Sprite celestialSproutSprite;
        private static Sprite rubyGeyserSprite;
        private static Sprite sanctuarySealSprite;
        private readonly Dictionary<Vector2Int, SpriteRenderer> sealMarkers = new Dictionary<Vector2Int, SpriteRenderer>();
        private readonly Dictionary<Vector2Int, SpriteRenderer> geyserMarkers = new Dictionary<Vector2Int, SpriteRenderer>();
        private readonly Dictionary<Vector2Int, SpriteRenderer> sproutMarkers = new Dictionary<Vector2Int, SpriteRenderer>();
        private readonly Dictionary<Vector2Int, SpriteRenderer> crystalMarkers = new Dictionary<Vector2Int, SpriteRenderer>();
        private readonly Dictionary<Vector2Int, SpriteRenderer> prismMarkers = new Dictionary<Vector2Int, SpriteRenderer>();
        private readonly Dictionary<Vector2Int, SpriteRenderer> warmthMarkers = new Dictionary<Vector2Int, SpriteRenderer>();

        public void SetSanctuarySealUsed(Vector2Int cell)
        {
            if(sealMarkers.TryGetValue(cell,out var marker) && marker!=null)
                marker.color=new Color(.65f,.59f,.4f,.55f);
        }

        private static Sprite GetSanctuarySealSprite()
        {
            if(sanctuarySealSprite!=null) return sanctuarySealSprite;
            const int size=48;
            var texture=new Texture2D(size,size,TextureFormat.RGBA32,false);
            texture.name="JoinDogSanctuarySeal";texture.filterMode=FilterMode.Bilinear;
            for(int y=0;y<size;y++) for(int x=0;x<size;x++)
            {
                float dx=x+.5f-24f,dy=y+.5f-24f,r=Mathf.Sqrt(dx*dx+dy*dy);
                bool ring=r>16f && r<20f;
                bool star=Mathf.Abs(dx)+Mathf.Abs(dy)<9f;
                texture.SetPixel(x,y,ring||star ? new Color(1f,.85f,.36f) : Color.clear);
            }
            texture.Apply();
            sanctuarySealSprite=Sprite.Create(texture,new Rect(0,0,size,size),new Vector2(.5f,.5f),size);
            return sanctuarySealSprite;
        }

        public void SetGeyserUsed(Vector2Int cell)
        {
            if(geyserMarkers.TryGetValue(cell,out var marker) && marker!=null)
                marker.color=new Color(.65f,.42f,.4f,.55f);
        }

        private static Sprite GetRubyGeyserSprite()
        {
            if(rubyGeyserSprite!=null) return rubyGeyserSprite;
            const int size=48;
            var texture=new Texture2D(size,size,TextureFormat.RGBA32,false);
            texture.name="JoinDogRubyGeyser";texture.filterMode=FilterMode.Bilinear;
            for(int y=0;y<size;y++) for(int x=0;x<size;x++)
            {
                float dx=x+.5f-24f,dy=y+.5f;
                bool baseRing=dy>7f && dy<13f && Mathf.Abs(dx)<18f;
                bool jet=dy>=13f && dy<39f && (Mathf.Abs(dx)<2f || Mathf.Abs(Mathf.Abs(dx)-(dy-13f)*.45f)<2f);
                texture.SetPixel(x,y,baseRing||jet ? new Color(1f,.48f,.3f) : Color.clear);
            }
            texture.Apply();
            rubyGeyserSprite=Sprite.Create(texture,new Rect(0,0,size,size),new Vector2(.5f,.5f),size);
            return rubyGeyserSprite;
        }

        public void SetCelestialSproutUsed(Vector2Int cell)
        {
            if(sproutMarkers.TryGetValue(cell,out var marker) && marker!=null)
                marker.color=new Color(.45f,.6f,.5f,.55f);
        }

        private static Sprite GetCelestialSproutSprite()
        {
            if(celestialSproutSprite!=null) return celestialSproutSprite;
            const int size=48;
            var texture=new Texture2D(size,size,TextureFormat.RGBA32,false);
            texture.name="JoinDogCelestialSprout";texture.filterMode=FilterMode.Bilinear;
            for(int y=0;y<size;y++) for(int x=0;x<size;x++)
            {
                float dx=x+.5f-24f,dy=y+.5f;
                bool stem=Mathf.Abs(dx)<2f && dy>6f && dy<30f;
                bool leaf=(dx-8f)*(dx-8f)/100f+(dy-30f)*(dy-30f)/49f<1f ||
                    (dx+8f)*(dx+8f)/100f+(dy-25f)*(dy-25f)/49f<1f;
                texture.SetPixel(x,y,stem||leaf ? new Color(.52f,1f,.72f) : Color.clear);
            }
            texture.Apply();
            celestialSproutSprite=Sprite.Create(texture,new Rect(0,0,size,size),new Vector2(.5f,.5f),size);
            return celestialSproutSprite;
        }

        public void SetSummitCrystalUsed(Vector2Int cell)
        {
            if(crystalMarkers.TryGetValue(cell,out var marker) && marker!=null)
                marker.color=new Color(.4f,.57f,.65f,.55f);
        }

        private static Sprite GetSummitCrystalSprite()
        {
            if(summitCrystalSprite!=null) return summitCrystalSprite;
            const int size=48;
            var texture=new Texture2D(size,size,TextureFormat.RGBA32,false);
            texture.name="JoinDogSummitCrystal";texture.filterMode=FilterMode.Bilinear;
            for(int y=0;y<size;y++) for(int x=0;x<size;x++)
            {
                float dx=Mathf.Abs(x+.5f-24f),dy=Mathf.Abs(y+.5f-24f);
                bool star=dx+dy<10f || (Mathf.Abs(dx-dy)<2.5f && dx<18f && dy<18f);
                texture.SetPixel(x,y,!star ? Color.clear : dx+dy<10f ? new Color(.95f,1f,1f) : new Color(.42f,.88f,1f));
            }
            texture.Apply();
            summitCrystalSprite=Sprite.Create(texture,new Rect(0,0,size,size),new Vector2(.5f,.5f),size);
            return summitCrystalSprite;
        }

        public void SetPrismUsed(Vector2Int cell)
        {
            if(prismMarkers.TryGetValue(cell,out var marker) && marker!=null)
                marker.color=new Color(.55f,.47f,.65f,.55f);
        }

        private static Sprite GetAuroraPrismSprite()
        {
            if(auroraPrismSprite!=null) return auroraPrismSprite;
            const int size=48;
            var texture=new Texture2D(size,size,TextureFormat.RGBA32,false);
            texture.name="JoinDogAuroraPrism";texture.filterMode=FilterMode.Bilinear;
            for(int y=0;y<size;y++) for(int x=0;x<size;x++)
            {
                float dx=x+.5f-24f,dy=y+.5f-24f;
                bool gem=Mathf.Abs(dx)/14f+Mathf.Abs(dy)/21f<1f;
                texture.SetPixel(x,y,!gem ? Color.clear : dx<0 ? new Color(.57f,.95f,1f) : new Color(.75f,.43f,1f));
            }
            texture.Apply();
            auroraPrismSprite=Sprite.Create(texture,new Rect(0,0,size,size),new Vector2(.5f,.5f),size);
            return auroraPrismSprite;
        }

        public void SetWarmthUsed(Vector2Int cell)
        {
            if(warmthMarkers.TryGetValue(cell,out var marker) && marker!=null)
                marker.color=new Color(.59f,.49f,.4f,.55f);
        }

        private static Sprite GetMountainWarmSprite()
        {
            if(mountainWarmSprite!=null) return mountainWarmSprite;
            const int size=48;
            var texture=new Texture2D(size,size,TextureFormat.RGBA32,false);
            texture.name="JoinDogMountainWarmth";texture.filterMode=FilterMode.Bilinear;
            for(int y=0;y<size;y++) for(int x=0;x<size;x++)
            {
                float dx=(x+.5f-24f)/24f,dy=(y+.5f-24f)/24f;
                float t=(dy+.78f)/1.65f;
                float shift=Mathf.Sin(t*Mathf.PI)*.15f;
                float width=Mathf.Sin(Mathf.Clamp01(t)*Mathf.PI)*.61f;
                bool flame=t>0f && t<1f && Mathf.Abs(dx-shift)<width;
                bool core=flame && dy<.25f && Mathf.Abs(dx)<width*.4f;
                texture.SetPixel(x,y,!flame ? Color.clear : core ? new Color(1f,.96f,.64f) : new Color(1f,.52f,.18f));
            }
            texture.Apply();
            mountainWarmSprite=Sprite.Create(texture,new Rect(0,0,size,size),new Vector2(.5f,.5f),size);
            return mountainWarmSprite;
        }

        public void SetTideUsed(Vector2Int cell)
        {
            if(tideMarkers.TryGetValue(cell,out var marker) && marker!=null)
                marker.color=new Color(.43f,.57f,.62f,.55f);
        }

        private static Sprite GetCoastTideSprite()
        {
            if(coastTideSprite!=null) return coastTideSprite;
            const int size=48;
            var texture=new Texture2D(size,size,TextureFormat.RGBA32,false);
            texture.name="JoinDogCoastTide";texture.filterMode=FilterMode.Bilinear;
            for(int y=0;y<size;y++) for(int x=0;x<size;x++)
            {
                float wave=Mathf.Sin((x-5f)/38f*Mathf.PI*2f)*5f;
                bool line=x>=5 && x<43 && (Mathf.Abs(y-(17f+wave))<2f || Mathf.Abs(y-(29f+wave))<2f);
                texture.SetPixel(x,y,line ? new Color(.47f,.96f,1f) : Color.clear);
            }
            texture.Apply();
            coastTideSprite=Sprite.Create(texture,new Rect(0,0,size,size),new Vector2(.5f,.5f),size);
            return coastTideSprite;
        }

        public void SetBellRung(Vector2Int cell)
        {
            if(bellMarkers.TryGetValue(cell,out var marker) && marker!=null)
                marker.color=new Color(.62f,.51f,.39f,.55f);
        }

        private static Sprite GetFestivalBellSprite()
        {
            if(festivalBellSprite!=null) return festivalBellSprite;
            const int size=48;
            var texture=new Texture2D(size,size,TextureFormat.RGBA32,false);
            texture.name="JoinDogFestivalBell";texture.filterMode=FilterMode.Bilinear;
            for(int y=0;y<size;y++) for(int x=0;x<size;x++)
            {
                float dx=x+.5f-24f,dy=y+.5f;
                bool body=dy>=13f && dy<=35f && Mathf.Abs(dx)<Mathf.Lerp(15f,6f,(dy-13f)/22f);
                bool rim=dy>=10f && dy<14f && Mathf.Abs(dx)<17f;
                bool clapper=dx*dx+(dy-7f)*(dy-7f)<16f;
                bool handle=dx*dx+(dy-38f)*(dy-38f)<16f;
                texture.SetPixel(x,y,body || rim || clapper || handle ?
                    Mathf.Abs(dx+5f)<2f && body ? new Color(1f,.98f,.72f) : new Color(1f,.75f,.18f) : Color.clear);
            }
            texture.Apply();
            festivalBellSprite=Sprite.Create(texture,new Rect(0,0,size,size),new Vector2(.5f,.5f),size);
            return festivalBellSprite;
        }

        public void SetShelterCollected(Vector2Int cell)
        {
            if (shelterMarkers.TryGetValue(cell,out var marker) && marker != null)
                marker.color = new Color(.46f,.54f,.43f,.55f);
        }

        private static Sprite GetShelterLeafSprite()
        {
            if (shelterLeafSprite != null) return shelterLeafSprite;
            const int size=48;
            var texture=new Texture2D(size,size,TextureFormat.RGBA32,false);
            texture.name="JoinDogShelterLeaf";texture.filterMode=FilterMode.Bilinear;
            for(int y=0;y<size;y++) for(int x=0;x<size;x++)
            {
                float along=(x+y+1-size)*.7071f;
                float across=(y-x)*.7071f;
                bool leaf=along*along/(21f*21f)+across*across/(10f*10f)<1f;
                texture.SetPixel(x,y,!leaf ? Color.clear : Mathf.Abs(across)<1.4f ?
                    new Color(1f,.96f,.63f) : new Color(.48f,.91f,.34f));
            }
            texture.Apply();
            shelterLeafSprite=Sprite.Create(texture,new Rect(0,0,size,size),new Vector2(.5f,.5f),size);
            return shelterLeafSprite;
        }

        public void SetGardenHarvested(Vector2Int cell)
        {
            if (gardenMarkers.TryGetValue(cell, out var marker) && marker != null)
                marker.color = new Color(.5f, .62f, .48f, .65f);
        }

        private static Sprite GetGardenFlowerSprite()
        {
            if (gardenFlowerSprite != null) return gardenFlowerSprite;
            const int size = 48;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            texture.name = "JoinDogGardenFlower";
            texture.filterMode = FilterMode.Bilinear;
            for (int y = 0; y < size; y++) for (int x = 0; x < size; x++)
            {
                var point = new Vector2(x + .5f - size / 2f, y + .5f - size / 2f);
                bool petal = false;
                for (int i = 0; i < 5; i++)
                {
                    float angle = i * Mathf.PI * 2f / 5f;
                    var center = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * 11f;
                    if ((point - center).sqrMagnitude < 64f) petal = true;
                }
                texture.SetPixel(x, y, point.sqrMagnitude < 36f ? new Color(1f,.71f,.2f) :
                    petal ? new Color(1f,.96f,.78f) : Color.clear);
            }
            texture.Apply();
            gardenFlowerSprite = Sprite.Create(texture, new Rect(0,0,size,size), new Vector2(.5f,.5f), size);
            return gardenFlowerSprite;
        }

        public void ClearRangePreview()
        {
            foreach (var pair in previewColors)
                if (pair.Key != null) pair.Key.color = pair.Value;
            previewColors.Clear();
        }

        public void ShowRangePreview(System.Collections.Generic.IEnumerable<PieceView> pieces)
        {
            ClearRangePreview();
            foreach (var piece in pieces)
            {
                if (piece == null || !cellSurfaces.TryGetValue(new Vector2Int(piece.gridX, piece.gridY), out var surface)) continue;
                if (previewColors.ContainsKey(surface)) continue;
                previewColors.Add(surface, surface.color);
                surface.color = Color.Lerp(surface.color, new Color(.85f, .65f, .28f, 1f), .68f);
            }
        }
        public void ShowCreationPreview(Vector2Int cell)
        {
            ClearRangePreview();
            if(!cellSurfaces.TryGetValue(cell,out var surface)) return;
            previewColors.Add(surface,surface.color);
            surface.color=Color.Lerp(surface.color,new Color(.7f,.48f,1f),.8f);
        }

        private int lastScreenWidth;
        private int lastScreenHeight;

        public Vector2 VisualSize { get; private set; }

        public static void CalculateLayout(
            int columns,
            int rows,
            Camera boardCamera,
            float fallbackSpacing,
            out float spacing,
            out float centerY)
        {
            CalculateLayoutForAspect(columns, rows, boardCamera, fallbackSpacing,
                (float)Screen.width / Mathf.Max(1, Screen.height), out spacing, out centerY);
        }

        public static void CalculateLayoutForAspect(
            int columns, int rows, Camera boardCamera, float fallbackSpacing,
            float aspect, out float spacing, out float centerY)
        {
            if (boardCamera == null || !boardCamera.orthographic || columns <= 0 || rows <= 0)
            {
                spacing = Mathf.Max(0.1f, fallbackSpacing);
                centerY = 0f;
                return;
            }

            float visibleHeight = boardCamera.orthographicSize * 2f;
            aspect = Mathf.Max(0.1f, aspect);
            float visibleWidth = visibleHeight * aspect;
            const float hudAspect = 9f / 19.5f;
            float contentWidth = Mathf.Min(visibleWidth, visibleHeight * hudAspect);
            float contentHeight = Mathf.Min(visibleHeight, visibleWidth / hudAspect);

            // Leave a slim horizontal margin for fingers and reserve the
            // vertical bands occupied by the objective and lower controls.
            float maximumBoardWidth = contentWidth * 0.95f;
            // Keep a deliberate breathing gap above and below the board. The
            // UI cards use the same normalized viewport bands, so the frame
            // cannot visually invade the objective card anymore.
            float maximumBoardHeight = contentHeight * (PortraitTopViewport - PortraitBottomViewport);
            // Include both frame edges, not just the logical cell rectangle.
            float horizontalSpacing = (maximumBoardWidth - 0.48f) / Mathf.Max(1, columns);
            float verticalSpacing = (maximumBoardHeight - 0.48f) / Mathf.Max(1, rows);

            spacing = Mathf.Clamp(
                Mathf.Min(horizontalSpacing, verticalSpacing),
                0.10f,
                0.72f);
            float centerViewport = (PortraitTopViewport + PortraitBottomViewport) * 0.5f;
            centerY = boardCamera.transform.position.y + contentHeight * (centerViewport - 0.5f);
        }

        public void Rebuild(BoardController board)
        {
            if (board == null || board.config == null) return;

            DisableLegacyBoard();
            ClearRangePreview();
            cellSurfaces.Clear();
            gardenMarkers.Clear();
            shelterMarkers.Clear();
            bellMarkers.Clear();
            tideMarkers.Clear();
            warmthMarkers.Clear();
            prismMarkers.Clear();
            crystalMarkers.Clear();
            sproutMarkers.Clear();
            geyserMarkers.Clear();
            sealMarkers.Clear();
            EnsureVisualRoot();
            ClearVisualRoot();

            float spacing = board.ActivePieceSpacing;
            float gridWidth = board.Columns * spacing;
            float gridHeight = board.Rows * spacing;
            float framePadding = Mathf.Clamp(spacing * 0.33f, 0.16f, 0.24f);
            VisualSize = new Vector2(
                gridWidth + framePadding * 2f,
                gridHeight + framePadding * 2f);

            GetThemeColors(
                board.config.boardTheme,
                out Color frameDark,
                out Color frameBase,
                out Color frameHighlight,
                out Color innerBevel,
                out Color innerPanel,
                out Color cellA,
                out Color cellB,
                out Color blockedCell,
                out Color sheen);

            bool irregular = false;
            for (int x = 0; x < board.Columns; x++)
                for (int y = 0; y < board.Rows; y++)
                    if (!board.IsPlayableCell(x, y)) irregular = true;
            ReleaseContour();
            if (irregular)
            {
                CreateContourFrame(board, framePadding, frameDark, frameBase, frameHighlight, innerBevel, innerPanel);
            }
            else
            {
            CreateLayer(
                "BoardShadow",
                board.ActiveBoardCenterY - 0.10f,
                VisualSize + new Vector2(0.10f, 0.16f),
                new Color(0.045f, 0.018f, 0.01f, 0.52f),
                -40);
            CreateLayer(
                "OuterFrame",
                board.ActiveBoardCenterY,
                VisualSize,
                frameDark,
                -39);
            GameObject materialBase = CreateLayer(
                "WoodBase",
                board.ActiveBoardCenterY + 0.015f,
                VisualSize - Vector2.one * 0.07f,
                frameBase,
                -38);
            materialBase.GetComponent<SpriteRenderer>().sprite = GetMaterialSprite(board.config.boardTheme, true);
            GameObject materialHighlight = CreateLayer(
                "WoodHighlight",
                board.ActiveBoardCenterY + 0.035f,
                VisualSize - Vector2.one * framePadding * 0.58f,
                frameHighlight,
                -37);
            materialHighlight.GetComponent<SpriteRenderer>().sprite = GetMaterialSprite(board.config.boardTheme, true);
            CreateLayer(
                "InnerBevel",
                board.ActiveBoardCenterY + 0.025f,
                new Vector2(gridWidth + spacing * 0.25f, gridHeight + spacing * 0.25f),
                innerBevel,
                -36);
            CreateLayer(
                "InnerPanel",
                board.ActiveBoardCenterY + 0.03f,
                new Vector2(gridWidth + spacing * 0.12f, gridHeight + spacing * 0.12f),
                innerPanel,
                -35);
            }

            Vector2 cellSize = Vector2.one * spacing * 0.94f;
            for (int x = 0; x < board.Columns; x++)
            {
                for (int y = 0; y < board.Rows; y++)
                {
                    if (irregular && !board.IsPlayableCell(x, y)) continue;
                    bool converter = board.IsConverterCell(x, y);
                    Color cellColor = !board.IsPlayableCell(x, y)
                        ? blockedCell
                        : ((x + y) & 1) == 0 ? cellA : cellB;
                    if (converter)
                        cellColor = Color.Lerp(cellColor, new Color(0.82f, 0.28f, 0.92f, 1f), 0.46f);
                    GameObject cell = CreateLayer(
                        converter ? $"ConverterCell_{x}_{y}" : $"Cell_{x}_{y}",
                        0f,
                        cellSize,
                        cellColor,
                        -34);
                    Vector3 gridPosition = board.GridToWorldPosition(x, y);
                    cell.transform.position = new Vector3(
                        gridPosition.x,
                        gridPosition.y,
                        0.2f);
                    if (board.IsPlayableCell(x, y))
                    {
                        // One shared surface adds a soft bevel without extra
                        // renderers or animations over the playable pieces.
                        var surface = cell.GetComponent<SpriteRenderer>();
                        cellSurfaces[new Vector2Int(x, y)] = surface;
                        surface.sprite = GetMaterialSprite(board.config.boardTheme, false);
                        surface.drawMode = SpriteDrawMode.Simple;
                        surface.transform.localScale = new Vector3(cellSize.x, cellSize.y, 1f);
                        if (board.IsCompanionGardenCell(x, y))
                        {
                            var flower = new GameObject($"GardenFlower_{x}_{y}");
                            flower.transform.SetParent(cell.transform, false);
                            flower.transform.localPosition = new Vector3(-.34f, -.34f, -.05f);
                            flower.transform.localScale = new Vector3(.29f, .29f, 1f);
                            var marker = flower.AddComponent<SpriteRenderer>();
                            marker.sprite = GetGardenFlowerSprite();
                            marker.sortingOrder = -30;
                            gardenMarkers[new Vector2Int(x,y)] = marker;
                            if (board.IsCompanionGardenHarvested(x,y)) SetGardenHarvested(new Vector2Int(x,y));
                        }
                        if (board.IsVineShelterCell(x,y))
                        {
                            var leaf = new GameObject($"ShelterLeaf_{x}_{y}");
                            leaf.transform.SetParent(cell.transform,false);
                            leaf.transform.localPosition=new Vector3(-.32f,-.32f,-.05f);
                            leaf.transform.localScale=new Vector3(.34f,.34f,1f);
                            var marker=leaf.AddComponent<SpriteRenderer>();
                            marker.sprite=GetShelterLeafSprite();marker.sortingOrder=-30;
                            shelterMarkers[new Vector2Int(x,y)]=marker;
                            if(board.IsVineShelterCollected(x,y)) SetShelterCollected(new Vector2Int(x,y));
                        }
                        if(board.IsFestivalBellCell(x,y))
                        {
                            var bell=new GameObject($"FestivalBell_{x}_{y}");
                            bell.transform.SetParent(cell.transform,false);
                            bell.transform.localPosition=new Vector3(-.32f,-.32f,-.05f);
                            bell.transform.localScale=new Vector3(.34f,.34f,1f);
                            var marker=bell.AddComponent<SpriteRenderer>();
                            marker.sprite=GetFestivalBellSprite();marker.sortingOrder=-30;
                            bellMarkers[new Vector2Int(x,y)]=marker;
                            if(board.IsFestivalBellRung(x,y)) SetBellRung(new Vector2Int(x,y));
                        }
                        if(board.IsCoastTideCell(x,y))
                        {
                            var tide=new GameObject($"CoastTide_{x}_{y}");
                            tide.transform.SetParent(cell.transform,false);
                            tide.transform.localPosition=new Vector3(-.32f,-.32f,-.05f);
                            tide.transform.localScale=new Vector3(.37f,.37f,1f);
                            var marker=tide.AddComponent<SpriteRenderer>();
                            marker.sprite=GetCoastTideSprite();marker.sortingOrder=-30;
                            tideMarkers[new Vector2Int(x,y)]=marker;
                            if(board.IsCoastTideUsed(x,y)) SetTideUsed(new Vector2Int(x,y));
                        }
                        if(board.IsSanctuarySealCell(x,y))
                        {
                            var seal=new GameObject($"SanctuarySeal_{x}_{y}");
                            seal.transform.SetParent(cell.transform,false);
                            seal.transform.localPosition=new Vector3(-.32f,-.32f,-.05f);
                            seal.transform.localScale=new Vector3(.37f,.37f,1f);
                            var marker=seal.AddComponent<SpriteRenderer>();
                            marker.sprite=GetSanctuarySealSprite();marker.sortingOrder=-30;
                            sealMarkers[new Vector2Int(x,y)]=marker;
                            if(board.IsSanctuarySealUsed(x,y)) SetSanctuarySealUsed(new Vector2Int(x,y));
                        }
                        if(board.IsRubyGeyserCell(x,y))
                        {
                            var geyser=new GameObject($"RubyGeyser_{x}_{y}");
                            geyser.transform.SetParent(cell.transform,false);
                            geyser.transform.localPosition=new Vector3(-.32f,-.32f,-.05f);
                            geyser.transform.localScale=new Vector3(.37f,.37f,1f);
                            var marker=geyser.AddComponent<SpriteRenderer>();
                            marker.sprite=GetRubyGeyserSprite();marker.sortingOrder=-30;
                            geyserMarkers[new Vector2Int(x,y)]=marker;
                            if(board.IsRubyGeyserUsed(x,y)) SetGeyserUsed(new Vector2Int(x,y));
                        }
                        if(board.IsCelestialSproutCell(x,y))
                        {
                            var sprout=new GameObject($"CelestialSprout_{x}_{y}");
                            sprout.transform.SetParent(cell.transform,false);
                            sprout.transform.localPosition=new Vector3(-.32f,-.32f,-.05f);
                            sprout.transform.localScale=new Vector3(.37f,.37f,1f);
                            var marker=sprout.AddComponent<SpriteRenderer>();
                            marker.sprite=GetCelestialSproutSprite();marker.sortingOrder=-30;
                            sproutMarkers[new Vector2Int(x,y)]=marker;
                            if(board.IsCelestialSproutUsed(x,y)) SetCelestialSproutUsed(new Vector2Int(x,y));
                        }
                        if(board.IsSummitCrystalCell(x,y))
                        {
                            var crystal=new GameObject($"SummitCrystal_{x}_{y}");
                            crystal.transform.SetParent(cell.transform,false);
                            crystal.transform.localPosition=new Vector3(-.32f,-.32f,-.05f);
                            crystal.transform.localScale=new Vector3(.37f,.37f,1f);
                            var marker=crystal.AddComponent<SpriteRenderer>();
                            marker.sprite=GetSummitCrystalSprite();marker.sortingOrder=-30;
                            crystalMarkers[new Vector2Int(x,y)]=marker;
                            if(board.IsSummitCrystalUsed(x,y)) SetSummitCrystalUsed(new Vector2Int(x,y));
                        }
                        if(board.IsAuroraPrismCell(x,y))
                        {
                            var prism=new GameObject($"AuroraPrism_{x}_{y}");
                            prism.transform.SetParent(cell.transform,false);
                            prism.transform.localPosition=new Vector3(-.32f,-.32f,-.05f);
                            prism.transform.localScale=new Vector3(.37f,.37f,1f);
                            var marker=prism.AddComponent<SpriteRenderer>();
                            marker.sprite=GetAuroraPrismSprite();marker.sortingOrder=-30;
                            prismMarkers[new Vector2Int(x,y)]=marker;
                            if(board.IsAuroraPrismUsed(x,y)) SetPrismUsed(new Vector2Int(x,y));
                        }
                        if(board.IsMountainWarmCell(x,y))
                        {
                            var warmth=new GameObject($"MountainWarmth_{x}_{y}");
                            warmth.transform.SetParent(cell.transform,false);
                            warmth.transform.localPosition=new Vector3(-.32f,-.32f,-.05f);
                            warmth.transform.localScale=new Vector3(.37f,.37f,1f);
                            var marker=warmth.AddComponent<SpriteRenderer>();
                            marker.sprite=GetMountainWarmSprite();marker.sortingOrder=-30;
                            warmthMarkers[new Vector2Int(x,y)]=marker;
                            if(board.IsMountainWarmthUsed(x,y)) SetWarmthUsed(new Vector2Int(x,y));
                        }
                    }
                }
            }

            if (!irregular)
            {
            CreateLayer(
                "TopRimSheen",
                board.ActiveBoardCenterY + VisualSize.y * 0.5f - 0.08f,
                new Vector2(VisualSize.x - 0.28f, 0.065f),
                sheen,
                -33);

            CreateThemeDecorations(board);
            }

            lastScreenWidth = Screen.width;
            lastScreenHeight = Screen.height;
        }

        private void ReleaseContour()
        {
            if (contourSprite != null) Destroy(contourSprite);
            if (contourTexture != null) Destroy(contourTexture);
            contourSprite = null;
            contourTexture = null;
        }

        private void OnDestroy() => ReleaseContour();

        private void CreateContourFrame(BoardController board, float padding, Color dark,
            Color material, Color highlight, Color bevel, Color panel)
        {
            var points = new List<Vector2>();
            float halfCell = board.ActivePieceSpacing * .5f;
            for (int x = 0; x < board.Columns; x++)
                for (int y = 0; y < board.Rows; y++)
                {
                    if (!board.IsPlayableCell(x, y)) continue;
                    Vector3 world = board.GridToWorldPosition(x, y);
                    var center = new Vector2(world.x, world.y - board.ActiveBoardCenterY);
                    points.Add(center + new Vector2(-halfCell, -halfCell));
                    points.Add(center + new Vector2(halfCell, -halfCell));
                    points.Add(center + new Vector2(halfCell, halfCell));
                    points.Add(center + new Vector2(-halfCell, halfCell));
                }
            var hull = ConvexHull(points);
            if (hull.Count < 3) return;
            int width = Mathf.Clamp(board.Columns * 32 + 32, 128, 384);
            int height = Mathf.Clamp(board.Rows * 32 + 32, 128, 384);
            contourTexture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            contourTexture.name = "JoinDogContourMaterial";
            contourTexture.wrapMode = TextureWrapMode.Clamp;
            contourTexture.filterMode = FilterMode.Bilinear;
            var pixels = new Color32[width * height];
            float pixelWorld = Mathf.Max(VisualSize.x / width, VisualSize.y / height);
            for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                {
                    float u = (x + .5f) / width, v = (y + .5f) / height;
                    var point = new Vector2((u - .5f) * VisualSize.x, (v - .5f) * VisualSize.y);
                    float distance = SignedHullDistance(hull, point);
                    Color color;
                    if (distance > padding * .70f) color = dark;
                    else if (distance > padding * .35f) color = Color.Lerp(material, highlight,
                        Mathf.InverseLerp(padding * .70f, padding * .35f, distance));
                    else if (distance > 0f) color = highlight;
                    else if (distance > -board.ActivePieceSpacing * .10f) color = bevel;
                    else color = panel;
                    float relief = distance > 0f ? .88f + MaterialRelief(board.config.boardTheme, u, v) * .12f : 1f;
                    color.r *= relief; color.g *= relief; color.b *= relief;
                    color.a = Mathf.Clamp01((padding - distance) / pixelWorld);
                    pixels[y * width + x] = color;
                }
            contourTexture.SetPixels32(pixels);
            contourTexture.Apply(false, true);
            contourSprite = Sprite.Create(contourTexture, new Rect(0, 0, width, height),
                new Vector2(.5f, .5f), 1f, 0, SpriteMeshType.FullRect);
            contourSprite.name = $"JoinDogContour_{board.config.boardTheme}";
            var scale = new Vector3(VisualSize.x / width, VisualSize.y / height, 1f);
            var shadow = new GameObject("ContourShadow", typeof(SpriteRenderer));
            shadow.transform.SetParent(visualRoot, false);
            shadow.transform.position = new Vector3(0f, board.ActiveBoardCenterY - .08f, .5f);
            shadow.transform.localScale = scale;
            var shadowRenderer = shadow.GetComponent<SpriteRenderer>();
            shadowRenderer.sprite = contourSprite;
            shadowRenderer.color = new Color(0f, 0f, 0f, .45f);
            shadowRenderer.sortingOrder = -40;
            var frame = new GameObject("ContourFrame", typeof(SpriteRenderer));
            frame.transform.SetParent(visualRoot, false);
            frame.transform.position = new Vector3(0f, board.ActiveBoardCenterY, .5f);
            frame.transform.localScale = scale;
            var renderer = frame.GetComponent<SpriteRenderer>();
            renderer.sprite = contourSprite;
            renderer.sortingOrder = -35;
        }

        private static float Cross(Vector2 a, Vector2 b, Vector2 c) =>
            (b.x - a.x) * (c.y - a.y) - (b.y - a.y) * (c.x - a.x);

        private static List<Vector2> ConvexHull(List<Vector2> points)
        {
            points.Sort((a, b) => a.x == b.x ? a.y.CompareTo(b.y) : a.x.CompareTo(b.x));
            var hull = new List<Vector2>();
            foreach (var point in points)
            {
                while (hull.Count >= 2 && Cross(hull[hull.Count - 2], hull[hull.Count - 1], point) <= .00001f)
                    hull.RemoveAt(hull.Count - 1);
                hull.Add(point);
            }
            int lowerCount = hull.Count;
            for (int i = points.Count - 2; i >= 0; i--)
            {
                while (hull.Count > lowerCount && Cross(hull[hull.Count - 2], hull[hull.Count - 1], points[i]) <= .00001f)
                    hull.RemoveAt(hull.Count - 1);
                hull.Add(points[i]);
            }
            if (hull.Count > 1) hull.RemoveAt(hull.Count - 1);
            return hull;
        }

        private static float SignedHullDistance(List<Vector2> hull, Vector2 point)
        {
            bool inside = true;
            float distanceSquared = float.MaxValue;
            for (int i = 0; i < hull.Count; i++)
            {
                Vector2 start = hull[i], end = hull[(i + 1) % hull.Count];
                Vector2 edge = end - start;
                float t = Mathf.Clamp01(Vector2.Dot(point - start, edge) / Mathf.Max(.000001f, edge.sqrMagnitude));
                distanceSquared = Mathf.Min(distanceSquared, (point - start - edge * t).sqrMagnitude);
                if (Cross(start, end, point) < 0f) inside = false;
            }
            return Mathf.Sqrt(distanceSquared) * (inside ? -1f : 1f);
        }

        private void CreateThemeDecorations(BoardController board)
        {
            float halfWidth = VisualSize.x * 0.5f;
            float halfHeight = VisualSize.y * 0.5f;
            float centerY = board.ActiveBoardCenterY;

            if (board.config.boardTheme == DogCrush.Core.BoardTheme.Forest)
            {
                for (int i = 0; i < 8; i++)
                {
                    float side = i % 2 == 0 ? -1f : 1f;
                    float y = centerY - halfHeight + 0.35f + (i / 2) *
                        Mathf.Max(0.28f, (VisualSize.y - 0.70f) / 3f);
                    GameObject leaf = CreateLayer($"ForestLeaf_{i}", y,
                        new Vector2(0.28f, 0.16f),
                        i % 3 == 0 ? new Color(0.46f, 0.90f, 0.24f, 0.96f) :
                            new Color(0.16f, 0.62f, 0.22f, 0.94f), -31);
                    leaf.transform.position = new Vector3(side * (halfWidth - 0.08f), y, 0.18f);
                    leaf.transform.rotation = Quaternion.Euler(0f, 0f, side * (24f + i * 7f));
                }
                GameObject moss = CreateLayer("ForestMossRim", centerY + halfHeight - 0.12f,
                    new Vector2(VisualSize.x - 0.34f, 0.12f), new Color(0.24f, 0.72f, 0.20f, 0.90f), -31);
                moss.transform.position += Vector3.forward * -0.02f;
            }
            else if (board.config.boardTheme == DogCrush.Core.BoardTheme.Festival)
            {
                Color[] bulbs =
                {
                    new Color(1f, 0.26f, 0.46f, 1f),
                    new Color(1f, 0.82f, 0.18f, 1f),
                    new Color(0.22f, 0.82f, 1f, 1f),
                    new Color(0.52f, 1f, 0.34f, 1f)
                };
                int bulbCount = Mathf.Clamp(board.Columns + 2, 8, 13);
                for (int i = 0; i < bulbCount; i++)
                {
                    float x = Mathf.Lerp(-halfWidth + 0.25f, halfWidth - 0.25f, i / (bulbCount - 1f));
                    for (int edge = 0; edge < 2; edge++)
                    {
                        float y = centerY + (edge == 0 ? halfHeight - 0.08f : -halfHeight + 0.08f);
                        GameObject bulb = CreateLayer($"FestivalBulb_{edge}_{i}", y,
                            new Vector2(0.13f, 0.13f), bulbs[(i + edge) % bulbs.Length], -30);
                        bulb.transform.position = new Vector3(x, y, 0.16f);
                    }
                }
            }
            else if (board.config.boardTheme == DogCrush.Core.BoardTheme.Coast)
            {
                for (int i = 0; i < 7; i++)
                {
                    float x = Mathf.Lerp(-halfWidth + 0.30f, halfWidth - 0.30f, i / 6f);
                    GameObject shell = CreateLayer($"CoastShell_{i}", centerY - halfHeight + 0.09f,
                        new Vector2(0.16f, 0.11f),
                        i % 2 == 0 ? new Color(1f, 0.78f, 0.38f, 0.96f) :
                            new Color(0.26f, 0.92f, 0.88f, 0.96f), -31);
                    shell.transform.position = new Vector3(x, centerY - halfHeight + 0.09f, 0.16f);
                    shell.transform.rotation = Quaternion.Euler(0f, 0f, i % 2 == 0 ? 18f : -18f);
                }
                CreateLayer("CoastWaveRim", centerY + halfHeight - 0.10f,
                    new Vector2(VisualSize.x - 0.30f, 0.10f), new Color(0.30f, 0.94f, 1f, 0.86f), -31);
            }
            else if (board.config.boardTheme == DogCrush.Core.BoardTheme.Mountain)
            {
                for (int i = 0; i < 8; i++)
                {
                    float side = i % 2 == 0 ? -1f : 1f;
                    float y = centerY - halfHeight + 0.30f + (i / 2) *
                        Mathf.Max(0.30f, (VisualSize.y - 0.60f) / 3f);
                    GameObject crystal = CreateLayer($"MountainCrystal_{i}", y,
                        new Vector2(0.13f, 0.30f),
                        i % 3 == 0 ? new Color(0.68f, 0.96f, 1f, 0.96f) :
                            new Color(0.32f, 0.70f, 1f, 0.92f), -31);
                    crystal.transform.position = new Vector3(side * (halfWidth - 0.08f), y, 0.18f);
                    crystal.transform.rotation = Quaternion.Euler(0f, 0f, side * (14f + i * 3f));
                }
                CreateLayer("MountainSnowRim", centerY + halfHeight - 0.10f,
                    new Vector2(VisualSize.x - 0.26f, 0.13f), new Color(0.84f, 0.96f, 1f, 0.94f), -31);
            }
            else if (board.config.boardTheme == DogCrush.Core.BoardTheme.Aurora)
            {
                Color[] auroraColors =
                {
                    new Color(1f, 0.32f, 0.78f, 0.92f),
                    new Color(0.28f, 1f, 0.78f, 0.92f),
                    new Color(0.48f, 0.58f, 1f, 0.92f)
                };
                for (int i = 0; i < 7; i++)
                {
                    float y = centerY - halfHeight + 0.28f + i *
                        Mathf.Max(0.24f, (VisualSize.y - 0.56f) / 6f);
                    float side = i % 2 == 0 ? -1f : 1f;
                    GameObject light = CreateLayer($"AuroraLight_{i}", y,
                        new Vector2(0.12f, 0.34f), auroraColors[i % auroraColors.Length], -31);
                    light.transform.position = new Vector3(side * (halfWidth - 0.08f), y, 0.18f);
                    light.transform.rotation = Quaternion.Euler(0f, 0f, side * (12f + i * 4f));
                }
                CreateLayer("AuroraRim", centerY + halfHeight - 0.10f,
                    new Vector2(VisualSize.x - 0.28f, 0.11f),
                    new Color(1f, 0.38f, 0.76f, 0.90f), -31);
            }
            else if (board.config.boardTheme == DogCrush.Core.BoardTheme.LuminousSummit)
            {
                for (int i = 0; i < 9; i++)
                {
                    float x = Mathf.Lerp(-halfWidth + 0.24f, halfWidth - 0.24f, i / 8f);
                    float height = 0.20f + (i % 3) * 0.08f;
                    GameObject crystal = CreateLayer($"LuminousCrystal_{i}", centerY - halfHeight + 0.12f,
                        new Vector2(0.10f, height),
                        i % 2 == 0 ? new Color(1f, 0.82f, 0.28f, 0.96f) :
                            new Color(0.76f, 0.60f, 1f, 0.94f), -31);
                    crystal.transform.position = new Vector3(x, centerY - halfHeight + 0.12f, 0.18f);
                    crystal.transform.rotation = Quaternion.Euler(0f, 0f, i % 2 == 0 ? -10f : 10f);
                }
                CreateLayer("LuminousCrown", centerY + halfHeight - 0.09f,
                    new Vector2(VisualSize.x - 0.24f, 0.14f),
                    new Color(1f, 0.82f, 0.30f, 0.96f), -31);
            }
            else if (board.config.boardTheme == DogCrush.Core.BoardTheme.CelestialGarden ||
                board.config.boardTheme == DogCrush.Core.BoardTheme.RubyCanyon ||
                board.config.boardTheme == DogCrush.Core.BoardTheme.GoldenSanctuary)
            {
                bool jade = board.config.boardTheme == DogCrush.Core.BoardTheme.CelestialGarden;
                bool ruby = board.config.boardTheme == DogCrush.Core.BoardTheme.RubyCanyon;
                for (int i = 0; i < 4; i++)
                {
                    float side = i % 2 == 0 ? -1f : 1f;
                    float y = centerY + (i < 2 ? -1f : 1f) * (halfHeight - .10f);
                    var seal = CreateLayer($"WorldSeal_{i}", y,
                        jade ? new Vector2(.11f,.22f) : new Vector2(.19f,.19f),
                        jade ? new Color(.73f,.94f,.66f) : ruby ? new Color(1f,.53f,.51f) : new Color(1f,.91f,.54f), -31);
                    seal.transform.position = new Vector3(side * (halfWidth - .10f), y, .18f);
                    seal.transform.rotation = Quaternion.Euler(0f,0f, jade ? side * 35f : ruby ? 45f : 0f);
                    var renderer = seal.GetComponent<SpriteRenderer>();
                    renderer.sprite = GetMaterialSprite(board.config.boardTheme, false);
                    renderer.drawMode = SpriteDrawMode.Simple;
                    seal.transform.localScale = jade ? new Vector3(.11f,.22f,1f) : new Vector3(.19f,.19f,1f);
                }
            }
            else
            {
                for (int i = 0; i < 5; i++)
                {
                    float x = Mathf.Lerp(-halfWidth + 0.35f, halfWidth - 0.35f, i / 4f);
                    GameObject accent = CreateLayer($"MeadowAccent_{i}", centerY - halfHeight + 0.08f,
                        new Vector2(0.14f, 0.14f),
                        i % 2 == 0 ? new Color(1f, 0.80f, 0.18f, 0.96f) :
                            new Color(0.42f, 0.92f, 0.28f, 0.96f), -31);
                    accent.transform.position = new Vector3(x, centerY - halfHeight + 0.08f, 0.16f);
                }
            }
        }

        private static void GetThemeColors(
            DogCrush.Core.BoardTheme theme,
            out Color frameDark,
            out Color frameBase,
            out Color frameHighlight,
            out Color innerBevel,
            out Color innerPanel,
            out Color cellA,
            out Color cellB,
            out Color blockedCell,
            out Color sheen)
        {
            if (theme == DogCrush.Core.BoardTheme.Forest)
            {
                // Forest boards remain wooden and readable; vegetation is an
                // accent around the frame instead of a green colour wash.
                frameDark = new Color(0.19f, 0.075f, 0.025f, 1f);
                frameBase = new Color(0.51f, 0.22f, 0.065f, 1f);
                frameHighlight = new Color(0.73f, 0.37f, 0.10f, 1f);
                innerBevel = new Color(0.075f, 0.20f, 0.105f, 1f);
                innerPanel = new Color(0.030f, 0.095f, 0.060f, 1f);
                cellA = new Color(.36f,.29f,.22f);
                cellB = new Color(.30f,.24f,.18f);
                blockedCell = new Color(0.025f, 0.070f, 0.045f, 0.94f);
                sheen = new Color(1f, 0.72f, 0.28f, 0.62f);
                return;
            }

            if (theme == DogCrush.Core.BoardTheme.Festival)
            {
                frameDark = new Color(0.12f, 0.055f, 0.27f, 1f);
                frameBase = new Color(0.27f, 0.14f, 0.50f, 1f);
                frameHighlight = new Color(0.55f, 0.29f, 0.72f, 1f);
                innerBevel = new Color(0.10f, 0.08f, 0.27f, 1f);
                innerPanel = new Color(0.035f, 0.045f, 0.14f, 1f);
                cellA = new Color(0.15f, 0.15f, 0.37f, 1f);
                cellB = new Color(0.11f, 0.11f, 0.31f, 1f);
                blockedCell = new Color(0.035f, 0.035f, 0.12f, 0.94f);
                sheen = new Color(1f, 0.75f, 0.22f, 0.72f);
                return;
            }

            if (theme == DogCrush.Core.BoardTheme.Coast)
            {
                frameDark = new Color(0.025f, 0.20f, 0.28f, 1f);
                frameBase = new Color(0.06f, 0.48f, 0.58f, 1f);
                frameHighlight = new Color(0.18f, 0.78f, 0.82f, 1f);
                innerBevel = new Color(0.68f, 0.44f, 0.16f, 1f);
                innerPanel = new Color(0.055f, 0.17f, 0.20f, 1f);
                cellA = new Color(0.10f, 0.34f, 0.36f, 1f);
                cellB = new Color(0.08f, 0.28f, 0.32f, 1f);
                blockedCell = new Color(0.025f, 0.10f, 0.13f, 0.94f);
                sheen = new Color(0.48f, 1f, 0.94f, 0.68f);
                return;
            }

            if (theme == DogCrush.Core.BoardTheme.Mountain)
            {
                frameDark = new Color(0.055f, 0.10f, 0.23f, 1f);
                frameBase = new Color(0.14f, 0.30f, 0.52f, 1f);
                frameHighlight = new Color(0.34f, 0.62f, 0.82f, 1f);
                innerBevel = new Color(0.11f, 0.19f, 0.36f, 1f);
                innerPanel = new Color(0.025f, 0.06f, 0.14f, 1f);
                cellA = new Color(.25f,.38f,.49f);
                cellB = new Color(.20f,.31f,.42f);
                blockedCell = new Color(0.02f, 0.045f, 0.12f, 0.95f);
                sheen = new Color(0.72f, 0.95f, 1f, 0.76f);
                return;
            }

            if (theme == DogCrush.Core.BoardTheme.Aurora)
            {
                frameDark = new Color(0.10f, 0.035f, 0.22f, 1f);
                frameBase = new Color(0.34f, 0.12f, 0.48f, 1f);
                frameHighlight = new Color(0.88f, 0.30f, 0.72f, 1f);
                innerBevel = new Color(0.08f, 0.22f, 0.30f, 1f);
                innerPanel = new Color(0.025f, 0.055f, 0.14f, 1f);
                cellA = new Color(0.12f, 0.28f, 0.34f, 1f);
                cellB = new Color(0.09f, 0.20f, 0.30f, 1f);
                blockedCell = new Color(0.025f, 0.045f, 0.12f, 0.95f);
                sheen = new Color(0.42f, 1f, 0.80f, 0.78f);
                return;
            }

            if (theme == DogCrush.Core.BoardTheme.LuminousSummit)
            {
                frameDark = new Color(0.10f, 0.06f, 0.22f, 1f);
                frameBase = new Color(0.34f, 0.22f, 0.56f, 1f);
                frameHighlight = new Color(0.88f, 0.66f, 0.24f, 1f);
                innerBevel = new Color(0.18f, 0.15f, 0.34f, 1f);
                innerPanel = new Color(0.035f, 0.035f, 0.13f, 1f);
                cellA = new Color(0.20f, 0.18f, 0.40f, 1f);
                cellB = new Color(0.14f, 0.12f, 0.34f, 1f);
                blockedCell = new Color(0.04f, 0.03f, 0.12f, 0.96f);
                sheen = new Color(1f, 0.82f, 0.32f, 0.82f);
                return;
            }

            if (theme == DogCrush.Core.BoardTheme.CelestialGarden)
            {
                frameDark = new Color(.04f,.18f,.16f);
                frameBase = new Color(.28f,.53f,.40f);
                frameHighlight = new Color(.58f,.77f,.54f);
                innerBevel = new Color(.64f,.57f,.32f);
                innerPanel = new Color(.035f,.10f,.10f);
                cellA = new Color(.20f,.34f,.30f);
                cellB = new Color(.16f,.28f,.26f);
                blockedCell = new Color(.025f,.065f,.06f);
                sheen = new Color(.90f,1f,.76f,.72f);
                return;
            }
            if (theme == DogCrush.Core.BoardTheme.RubyCanyon)
            {
                frameDark = new Color(.20f,.055f,.085f);
                frameBase = new Color(.54f,.18f,.22f);
                frameHighlight = new Color(.78f,.37f,.31f);
                innerBevel = new Color(.68f,.45f,.29f);
                innerPanel = new Color(.075f,.045f,.09f);
                cellA = new Color(.30f,.23f,.31f);
                cellB = new Color(.25f,.18f,.25f);
                blockedCell = new Color(.07f,.035f,.06f);
                sheen = new Color(1f,.70f,.49f,.72f);
                return;
            }
            if (theme == DogCrush.Core.BoardTheme.GoldenSanctuary)
            {
                frameDark = new Color(.22f,.15f,.06f);
                frameBase = new Color(.68f,.48f,.19f);
                frameHighlight = new Color(.94f,.76f,.35f);
                innerBevel = new Color(.32f,.25f,.17f);
                innerPanel = new Color(.04f,.055f,.095f);
                cellA = new Color(.22f,.26f,.34f);
                cellB = new Color(.17f,.21f,.29f);
                blockedCell = new Color(.025f,.04f,.065f);
                sheen = new Color(1f,.93f,.66f,.82f);
                return;
            }

            frameDark = new Color(.13f,.11f,.07f);
            frameBase = new Color(.48f,.32f,.14f);
            frameHighlight = new Color(.72f,.55f,.29f);
            innerBevel = new Color(.15f,.23f,.16f);
            innerPanel = new Color(.045f,.075f,.052f);
            cellA = new Color(.29f,.40f,.28f);
            cellB = new Color(.23f,.33f,.22f);
            blockedCell = new Color(.025f,.05f,.035f,.94f);
            sheen = new Color(1f, 0.86f, 0.58f, 0.72f);
        }

        private void LateUpdate()
        {
            if (lastScreenWidth == 0 || lastScreenHeight == 0) return;
            if (lastScreenWidth == Screen.width && lastScreenHeight == Screen.height) return;

            BoardController board = GetComponent<BoardController>();
            board?.RefreshAdaptiveLayout();
        }

        private void DisableLegacyBoard()
        {
            GameObject legacyFrame = GameObject.Find("BoardFrame");
            if (legacyFrame != null) legacyFrame.SetActive(false);

            GameObject legacyPanel = GameObject.Find("BoardPanel");
            if (legacyPanel != null) legacyPanel.SetActive(false);
        }

        private void EnsureVisualRoot()
        {
            if (visualRoot != null) return;

            Transform existing = transform.Find(VisualRootName);
            if (existing != null)
            {
                visualRoot = existing;
                return;
            }

            GameObject root = new GameObject(VisualRootName);
            visualRoot = root.transform;
            visualRoot.SetParent(transform, false);
        }

        private void ClearVisualRoot()
        {
            for (int i = visualRoot.childCount - 1; i >= 0; i--)
            {
                Destroy(visualRoot.GetChild(i).gameObject);
            }
        }

        private GameObject CreateLayer(
            string objectName,
            float centerY,
            Vector2 size,
            Color color,
            int sortingOrder)
        {
            GameObject layer = new GameObject(objectName);
            layer.transform.SetParent(visualRoot, false);
            layer.transform.position = new Vector3(0f, centerY, 0.5f);

            SpriteRenderer renderer = layer.AddComponent<SpriteRenderer>();
            renderer.sprite = GetRoundedSprite();
            renderer.drawMode = SpriteDrawMode.Sliced;
            renderer.size = size;
            renderer.color = color;
            renderer.sortingOrder = sortingOrder;
            return layer;
        }

        private static Sprite GetMaterialSprite(DogCrush.Core.BoardTheme theme, bool frame)
        {
            int key = (int)theme * 2 + (frame ? 1 : 0);
            if (materialSprites.TryGetValue(key, out var cached) && cached != null) return cached;
            int size = frame ? 128 : 64;
            float radius = frame ? 18f : theme == DogCrush.Core.BoardTheme.Coast ||
                theme == DogCrush.Core.BoardTheme.CelestialGarden ? 15f : 8f;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            texture.name = $"JoinDogMaterial_{theme}_{(frame ? "Frame" : "Cell")}";
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.filterMode = FilterMode.Bilinear;
            var pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float dx = Mathf.Max(radius - x, 0f, x - (size - 1f - radius));
                    float dy = Mathf.Max(radius - y, 0f, y - (size - 1f - radius));
                    float distance = Mathf.Sqrt(dx * dx + dy * dy);
                    bool faceted = !frame && (theme == DogCrush.Core.BoardTheme.Mountain ||
                        theme == DogCrush.Core.BoardTheme.Aurora || theme == DogCrush.Core.BoardTheme.RubyCanyon);
                    float alpha = faceted ? Mathf.Clamp01(radius + .5f - dx - dy) :
                        Mathf.Clamp01(radius + .5f - distance);
                    float edge = Mathf.Min(x, y, size - 1f - x, size - 1f - y);
                    float u = x / (size - 1f), v = y / (size - 1f);
                    float relief = MaterialRelief(theme, u, v);
                    // Texture fades towards the toy silhouette, while the
                    // frame carries a stronger, completely static material.
                    float rimWeight = Mathf.Lerp(.22f, 1f, Mathf.Clamp01(Mathf.Abs(u - .5f) + Mathf.Abs(v - .5f)));
                    float tone = Mathf.Lerp(.68f, .97f, v) + relief * (frame ? .15f : .16f * rimWeight);
                    if (edge < 2f || (faceted ? dx + dy > radius - 2f : distance > radius - 2f))
                        tone = v > .5f ? 1f : .43f;
                    tone = Mathf.Clamp01(tone);
                    pixels[y * size + x] = new Color(tone, tone, tone, alpha);
                }
            texture.SetPixels(pixels);
            texture.Apply(false, true);
            Sprite sprite = Sprite.Create(texture, new Rect(0, 0, size, size),
                new Vector2(.5f, .5f), size, 0, SpriteMeshType.FullRect,
                frame ? Vector4.one * radius : Vector4.zero);
            sprite.name = texture.name;
            materialSprites[key] = sprite;
            return sprite;
        }

        private static float MaterialRelief(DogCrush.Core.BoardTheme theme, float u, float v)
        {
            switch (theme)
            {
                case DogCrush.Core.BoardTheme.Meadow: // Woven garden felt.
                    return Mathf.Sin(u * 94f) * Mathf.Sin(v * 94f) * .55f;
                case DogCrush.Core.BoardTheme.Forest: // Curved timber grain.
                    return Mathf.Sin(v * 74f + Mathf.Sin(u * 9f) * 3f) * .75f +
                        Mathf.Sin(v * 155f + u * 6f) * .2f;
                case DogCrush.Core.BoardTheme.Festival: // Tufted velvet diamonds.
                    return Mathf.Cos((u + v) * 22f) * Mathf.Cos((u - v) * 22f);
                case DogCrush.Core.BoardTheme.Coast: // Water ripples, quiet centre.
                    return Mathf.Sin(v * 38f + Mathf.Sin(u * 11f) * 2f);
                case DogCrush.Core.BoardTheme.Mountain: // Ice planes with fine fractures.
                    return (u + v > 1f ? .65f : -.35f) +
                        Mathf.Pow(Mathf.Max(0f, Mathf.Cos((u - v) * 22f)), 18f) * .3f;
                case DogCrush.Core.BoardTheme.Aurora: // Cut prism faces.
                    return (u > v ? .55f : -.4f) + (u + v > 1.25f ? .4f : -.15f);
                case DogCrush.Core.BoardTheme.LuminousSummit: // Marble veining.
                    return Mathf.Sin((u + v * .6f) * 31f + Mathf.Sin(v * 8f)) * .65f;
                case DogCrush.Core.BoardTheme.CelestialGarden: // Leaf veins in jade.
                    return Mathf.Cos((v - Mathf.Abs(u - .5f)) * 43f) * .65f;
                case DogCrush.Core.BoardTheme.RubyCanyon: // Layered canyon stone.
                    return Mathf.Sin(v * 34f + u * 7f) * .7f + Mathf.Sin(v * 83f) * .2f;
                default: // Engraved sanctuary metal.
                    float radius = Vector2.Distance(new Vector2(u, v), new Vector2(.5f, .5f));
                    return Mathf.Cos(radius * 65f) * .6f;
            }
        }

        private Sprite GetRoundedSprite()
        {
            if (roundedSprite != null) return roundedSprite;

            const int textureSize = 64;
            const float radius = 14f;
            Texture2D texture = new Texture2D(
                textureSize,
                textureSize,
                TextureFormat.RGBA32,
                false);
            texture.name = "AdaptiveBoardRoundedRect";
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.filterMode = FilterMode.Bilinear;

            Color32[] pixels = new Color32[textureSize * textureSize];
            for (int y = 0; y < textureSize; y++)
            {
                for (int x = 0; x < textureSize; x++)
                {
                    float dx = Mathf.Max(radius - x, 0f, x - (textureSize - 1f - radius));
                    float dy = Mathf.Max(radius - y, 0f, y - (textureSize - 1f - radius));
                    float distance = Mathf.Sqrt(dx * dx + dy * dy);
                    byte alpha = (byte)Mathf.RoundToInt(
                        255f * Mathf.Clamp01(radius + 1f - distance));
                    pixels[y * textureSize + x] = new Color32(255, 255, 255, alpha);
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            roundedSprite = Sprite.Create(
                texture,
                new Rect(0f, 0f, textureSize, textureSize),
                new Vector2(0.5f, 0.5f),
                100f,
                0,
                SpriteMeshType.FullRect,
                new Vector4(radius, radius, radius, radius));
            roundedSprite.name = "AdaptiveBoardRoundedRect";
            return roundedSprite;
        }
    }
}
