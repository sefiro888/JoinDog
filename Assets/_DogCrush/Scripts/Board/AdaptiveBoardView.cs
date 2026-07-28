using UnityEngine;

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
        private const float PortraitCenterY = -1.30f;
        private const float LandscapeCenterY = 0f;

        private Transform visualRoot;
        private Sprite roundedSprite;
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
            if (boardCamera == null || !boardCamera.orthographic || columns <= 0 || rows <= 0)
            {
                spacing = Mathf.Max(0.1f, fallbackSpacing);
                centerY = 0f;
                return;
            }

            float visibleHeight = boardCamera.orthographicSize * 2f;
            float aspect = Mathf.Max(0.1f, (float)Screen.width / Mathf.Max(1, Screen.height));
            float visibleWidth = visibleHeight * aspect;
            bool portrait = aspect < 0.8f;

            // Leave a slim horizontal margin for fingers and reserve the
            // vertical bands occupied by the logo and lower controls.
            float maximumBoardWidth = visibleWidth * (portrait ? 0.95f : 0.74f);
            // The previous portrait cap made the board feel like a small
            // widget on tall phones. Reclaim the logo gap for the play area.
            float maximumBoardHeight = visibleHeight * (portrait ? 0.60f : 0.70f);
            float horizontalSpacing = maximumBoardWidth / Mathf.Max(1, columns);
            float verticalSpacing = maximumBoardHeight / Mathf.Max(1, rows);

            spacing = Mathf.Clamp(
                Mathf.Min(horizontalSpacing, verticalSpacing),
                0.42f,
                0.72f);
            centerY = portrait ? PortraitCenterY : LandscapeCenterY;
        }

        public void Rebuild(BoardController board)
        {
            if (board == null || board.config == null) return;

            DisableLegacyBoard();
            EnsureVisualRoot();
            ClearVisualRoot();

            float spacing = board.ActivePieceSpacing;
            float gridWidth = board.Columns * spacing;
            float gridHeight = board.Rows * spacing;
            float framePadding = Mathf.Clamp(spacing * 0.33f, 0.16f, 0.24f);
            VisualSize = new Vector2(
                gridWidth + framePadding * 2f,
                gridHeight + framePadding * 2f);

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
                new Color(0.28f, 0.075f, 0.018f, 1f),
                -39);
            CreateLayer(
                "WoodBase",
                board.ActiveBoardCenterY + 0.015f,
                VisualSize - Vector2.one * 0.07f,
                new Color(0.67f, 0.25f, 0.055f, 1f),
                -38);
            CreateLayer(
                "WoodHighlight",
                board.ActiveBoardCenterY + 0.035f,
                VisualSize - Vector2.one * framePadding * 0.58f,
                new Color(0.82f, 0.39f, 0.09f, 1f),
                -37);
            CreateLayer(
                "InnerBevel",
                board.ActiveBoardCenterY + 0.025f,
                new Vector2(gridWidth + spacing * 0.25f, gridHeight + spacing * 0.25f),
                new Color(0.20f, 0.052f, 0.018f, 1f),
                -36);
            CreateLayer(
                "InnerPanel",
                board.ActiveBoardCenterY + 0.03f,
                new Vector2(gridWidth + spacing * 0.12f, gridHeight + spacing * 0.12f),
                new Color(0.105f, 0.035f, 0.018f, 1f),
                -35);

            Vector2 cellSize = Vector2.one * spacing * 0.90f;
            for (int x = 0; x < board.Columns; x++)
            {
                for (int y = 0; y < board.Rows; y++)
                {
                    GameObject cell = CreateLayer(
                        $"Cell_{x}_{y}",
                        0f,
                        cellSize,
                        !board.IsPlayableCell(x, y)
                            ? new Color(0.075f, 0.025f, 0.018f, 0.92f)
                            : ((x + y) & 1) == 0
                                ? new Color(0.31f, 0.12f, 0.055f, 1f)
                                : new Color(0.265f, 0.09f, 0.04f, 1f),
                        -34);
                    Vector3 gridPosition = board.GridToWorldPosition(x, y);
                    cell.transform.position = new Vector3(
                        gridPosition.x,
                        gridPosition.y,
                        0.2f);
                }
            }

            CreateLayer(
                "TopRimSheen",
                board.ActiveBoardCenterY + VisualSize.y * 0.5f - 0.08f,
                new Vector2(VisualSize.x - 0.28f, 0.065f),
                new Color(1f, 0.72f, 0.30f, 0.62f),
                -33);

            lastScreenWidth = Screen.width;
            lastScreenHeight = Screen.height;
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
