using System.Collections.Generic;
using UnityEngine;

namespace DogCrush.Board
{
    public sealed class MatchResolution
    {
        public readonly List<PieceView> PiecesToRemove = new List<PieceView>();
        public readonly List<PieceView> ActivatedSpecials = new List<PieceView>();
        public PieceView CreatedSpecial;
        public PieceSpecialType CreatedSpecialType;
        public int SpecialsActivated;
        public bool MegaCombo;
        public bool ColorBurstCombo;
        public SpecialComboKind ComboKind;
        public int OriginalMatchCount;
    }

    public class BoardController : MonoBehaviour
    {
        private static readonly Vector2Int[] OrthogonalDirections =
        {
            Vector2Int.up,
            Vector2Int.right,
            Vector2Int.down,
            Vector2Int.left
        };

        public BoardConfig config;
        public PieceSpawner spawner;

        private PieceView[,] grid;
        private Vector3 boardOrigin;
        private float activePieceSpacing;
        private float activeBoardCenterY;
        private AdaptiveBoardView adaptiveView;
        private PieceView lastSwapFirst;
        private PieceView lastSwapSecond;
        private int[,] obstacleHealth;
        private SpriteRenderer[,] obstacleRenderers;
        private Transform obstacleRoot;
        private static Sprite vineObstacleSprite;
        private static Sprite lanternObstacleSprite;
        private static Sprite sandObstacleSprite;
        private static Sprite iceObstacleSprite;

        public PieceView[,] Grid => grid;
        public int Columns => config != null ? config.columns : 8;
        public int Rows => config != null ? config.rows : 8;
        public float ActivePieceSpacing => activePieceSpacing;
        public float ActiveBoardCenterY => activeBoardCenterY;
        public int RemainingObstacleCount { get; private set; }

        public void InitializeBoard()
        {
            if (config == null)
            {
                config = ScriptableObject.CreateInstance<BoardConfig>();
            }

            // A restart replaces the grid array. Recycle the previous grid
            // first, otherwise its active PieceViews remain behind the new
            // board and the scene accumulates duplicate visual pieces.
            if (grid != null)
            {
                ClearBoard();
            }

            grid = new PieceView[config.columns, config.rows];
            adaptiveView = GetComponent<AdaptiveBoardView>();
            if (adaptiveView == null)
            {
                adaptiveView = gameObject.AddComponent<AdaptiveBoardView>();
            }
            CalculateBoardOrigin();
            adaptiveView.Rebuild(this);
            FillInitialBoard();
            BuildObstacles();
        }

        public void CalculateBoardOrigin()
        {
            AdaptiveBoardView.CalculateLayout(
                config.columns,
                config.rows,
                Camera.main,
                config.pieceSpacing,
                out activePieceSpacing,
                out activeBoardCenterY);

            float totalWidth = (config.columns - 1) * activePieceSpacing;
            float totalHeight = (config.rows - 1) * activePieceSpacing;
            // Keep pieces in front of the board frame in URP/WebGL. At z=0
            // both SpriteRenderers can share the same depth buffer value and
            // the opaque frame may hide the pieces despite their sort order.
            boardOrigin = new Vector3(
                -totalWidth / 2f,
                activeBoardCenterY - totalHeight / 2f,
                -1f);
        }

        public Vector3 GridToWorldPosition(int x, int y)
        {
            return boardOrigin + new Vector3(x * activePieceSpacing, y * activePieceSpacing, 0f);
        }

        /// <summary>
        /// Resolves a finger position to the nearest logical cell. Using the
        /// grid layout instead of relying only on a small sprite collider is
        /// much more forgiving on narrow mobile screens.
        /// </summary>
        public bool TryGetGridPosition(Vector2 worldPosition, out int x, out int y)
        {
            x = 0;
            y = 0;
            if (grid == null || activePieceSpacing <= 0.001f) return false;

            float localX = (worldPosition.x - boardOrigin.x) / activePieceSpacing;
            float localY = (worldPosition.y - boardOrigin.y) / activePieceSpacing;
            x = Mathf.RoundToInt(localX);
            y = Mathf.RoundToInt(localY);
            if (!IsValidGridPos(x, y)) return false;

            Vector2 cellCenter = GridToWorldPosition(x, y);
            float hitRadius = activePieceSpacing * 0.54f;
            return Vector2.Distance(worldPosition, cellCenter) <= hitRadius;
        }

        public PieceView GetPieceAtWorldPosition(Vector2 worldPosition)
        {
            return TryGetGridPosition(worldPosition, out int x, out int y)
                ? GetPieceAt(x, y)
                : null;
        }

        public void RefreshAdaptiveLayout()
        {
            if (config == null || grid == null) return;

            CalculateBoardOrigin();
            adaptiveView?.Rebuild(this);

            for (int x = 0; x < config.columns; x++)
            {
                for (int y = 0; y < config.rows; y++)
                {
                    PieceView piece = grid[x, y];
                    if (piece != null)
                    {
                        piece.transform.position = GridToWorldPosition(x, y);
                    }
                    if (obstacleRenderers != null && obstacleRenderers[x, y] != null)
                        obstacleRenderers[x, y].transform.position = GridToWorldPosition(x, y) + Vector3.forward * 0.35f;
                }
            }
        }

        public bool IsValidGridPos(int x, int y)
        {
            return x >= 0 && x < config.columns && y >= 0 && y < config.rows;
        }

        public PieceView GetPieceAt(int x, int y)
        {
            if (!IsValidGridPos(x, y)) return null;
            return grid[x, y];
        }

        public bool IsPlayableCell(int x, int y)
        {
            if (!IsValidGridPos(x, y)) return false;
            if (config.boardShape == DogCrush.Core.BoardShape.Full) return true;

            if (config.boardShape == DogCrush.Core.BoardShape.Rounded)
            {
                // A soft octagonal silhouette. Every column keeps one
                // contiguous playable interval, so gravity and refill remain
                // identical to the proven full-board implementation.
                int distanceFromEdge = Mathf.Min(x, Columns - 1 - x);
                int verticalInset = distanceFromEdge <= 0 ? 2 : distanceFromEdge == 1 ? 1 : 0;
                return y >= verticalInset && y < Rows - verticalInset;
            }

            // Diamond rows remain contiguous in each column, so gravity can
            // compact them safely without crossing blocked cells.
            float centerX = (Columns - 1) * 0.5f;
            float centerY = (Rows - 1) * 0.5f;
            float verticalRatio = Mathf.Abs(y - centerY) / Mathf.Max(0.5f, centerY);
            float halfWidth = Mathf.Lerp(0.5f, centerX + 0.5f, 1f - verticalRatio);
            return Mathf.Abs(x - centerX) <= halfWidth;
        }

        public void SetPieceAt(int x, int y, PieceView piece)
        {
            if (IsValidGridPos(x, y))
            {
                grid[x, y] = piece;
                if (piece != null)
                {
                    piece.SetGridPosition(x, y);
                }
            }
        }

        private void FillInitialBoard()
        {
            ClearBoard();

            int availableTypeCount = Mathf.Clamp(config.typeCount, 1, (int)PieceType.Collar + 1);

            for (int x = 0; x < config.columns; x++)
            {
                for (int y = 0; y < config.rows; y++)
                {
                    if (!IsPlayableCell(x, y)) continue;
                    PieceType type = (PieceType)Random.Range(0, availableTypeCount);
                    Vector3 targetWorldPos = GridToWorldPosition(x, y);
                    PieceView piece = spawner.SpawnPiece(type, x, y, targetWorldPos);
                    grid[x, y] = piece;
                }
            }

            EnsureHasValidMoves();
        }

        public void ClearBoard()
        {
            if (grid == null) return;
            ClearLastSwap();
            ClearObstacles();
            int existingColumns = grid.GetLength(0);
            int existingRows = grid.GetLength(1);
            for (int x = 0; x < existingColumns; x++)
            {
                for (int y = 0; y < existingRows; y++)
                {
                    if (grid[x, y] != null)
                    {
                        spawner.RecyclePiece(grid[x, y]);
                        grid[x, y] = null;
                    }
                }
            }
        }

        public bool HasAnyValidMove()
        {
            for (int x = 0; x < config.columns; x++)
            {
                for (int y = 0; y < config.rows; y++)
                {
                    PieceView current = grid[x, y];
                    if (current == null) continue;
                    foreach (Vector2Int direction in OrthogonalDirections)
                    {
                        int nx = x + direction.x;
                        int ny = y + direction.y;
                        if (!IsValidGridPos(nx, ny) || grid[nx, ny] == null) continue;
                        PieceView other = grid[nx, ny];
                        if ((current.IsSpecial && other.IsSpecial) ||
                            current.SpecialType == PieceSpecialType.ColorBurst ||
                            other.SpecialType == PieceSpecialType.ColorBurst)
                        {
                            return true;
                        }
                        grid[x, y] = other;
                        grid[nx, ny] = current;
                        bool createsMatch = FindMatches().Count >= 3;
                        grid[x, y] = current;
                        grid[nx, ny] = other;
                        if (createsMatch)
                        {
                            return true;
                        }
                    }
                }
            }
            return false;
        }

        public void EnsureHasValidMoves()
        {
            int safetyCounter = 0;
            while (!HasAnyValidMove() && safetyCounter < 50)
            {
                ShuffleBoardTypes();
                safetyCounter++;
            }

            // A shuffled distribution can still be unlucky, especially on
            // small/custom boards. Never leave the player with a dead board:
            // create one guaranteed orthogonal triplet as a deterministic
            // fallback after the shuffle budget is exhausted.
            if (!HasAnyValidMove())
            {
                ForceValidMovePattern();
            }
        }

        private void ForceValidMovePattern()
        {
            if (grid == null || spawner == null || config == null ||
                config.columns < 2 || config.rows < 2) return;

            int x = -1;
            int y = -1;
            for (int candidateX = 0; candidateX < config.columns - 1 && x < 0; candidateX++)
            {
                for (int candidateY = 0; candidateY < config.rows - 1; candidateY++)
                {
                    if (IsPlayableCell(candidateX, candidateY) &&
                        IsPlayableCell(candidateX + 1, candidateY) &&
                        IsPlayableCell(candidateX, candidateY + 1))
                    {
                        x = candidateX;
                        y = candidateY;
                        break;
                    }
                }
            }
            if (x < 0) return;
            PieceType forcedType = PieceType.Dog;
            PieceView[] pattern =
            {
                grid[x, y],
                grid[x + 1, y],
                grid[x, y + 1]
            };

            foreach (PieceView piece in pattern)
            {
                if (piece != null)
                {
                    piece.Initialize(
                        forcedType,
                        piece.gridX,
                        piece.gridY,
                        spawner.GetSpriteForType(forcedType),
                        spawner.GetColorForType(forcedType));
                }
            }
        }

        public void ShuffleBoardTypes()
        {
            List<PieceType> allTypes = new List<PieceType>();
            for (int x = 0; x < config.columns; x++)
            {
                for (int y = 0; y < config.rows; y++)
                {
                    if (grid[x, y] != null)
                        allTypes.Add(grid[x, y].type);
                }
            }

            // Fisher-Yates shuffle
            for (int i = 0; i < allTypes.Count; i++)
            {
                int rnd = Random.Range(i, allTypes.Count);
                PieceType temp = allTypes[i];
                allTypes[i] = allTypes[rnd];
                allTypes[rnd] = temp;
            }

            int index = 0;
            for (int x = 0; x < config.columns; x++)
            {
                for (int y = 0; y < config.rows; y++)
                {
                    if (grid[x, y] != null)
                    {
                        PieceType newType = allTypes[index++];
                        PieceSpecialType specialType = grid[x, y].SpecialType;
                        grid[x, y].Initialize(newType, x, y, spawner.GetSpriteForType(newType), spawner.GetColorForType(newType));
                        grid[x, y].SetSpecial(specialType);
                    }
                }
            }
        }

        public static bool AreAdjacent(int x1, int y1, int x2, int y2)
        {
            int dx = Mathf.Abs(x1 - x2);
            int dy = Mathf.Abs(y1 - y2);
            return dx + dy == 1;
        }

        public void FillMissingCells()
        {
            if (config == null || grid == null || spawner == null) return;
            int availableTypeCount = Mathf.Clamp(config.typeCount, 1, (int)PieceType.Collar + 1);
            for (int x = 0; x < Columns; x++)
            {
                for (int y = 0; y < Rows; y++)
                {
                    if (grid[x, y] != null) continue;
                    if (!IsPlayableCell(x, y)) continue;
                    PieceType type = (PieceType)Random.Range(0, availableTypeCount);
                    grid[x, y] = spawner.SpawnPiece(type, x, y, GridToWorldPosition(x, y));
                }
            }
        }

        public List<PieceView> GetRowPieces(int row)
        {
            var result = new List<PieceView>();
            if (config == null || row < 0 || row >= Rows) return result;
            for (int x = 0; x < Columns; x++)
            {
                if (grid[x, row] != null) result.Add(grid[x, row]);
            }
            return result;
        }

        public List<PieceView> GetColumnPieces(int column)
        {
            var result = new List<PieceView>();
            if (config == null || column < 0 || column >= Columns) return result;
            for (int y = 0; y < Rows; y++)
            {
                if (grid[column, y] != null) result.Add(grid[column, y]);
            }
            return result;
        }

        public PieceView GetRandomPiece()
        {
            if (grid == null || Columns <= 0 || Rows <= 0) return null;
            var pieces = new List<PieceView>();
            for (int x = 0; x < Columns; x++)
            {
                for (int y = 0; y < Rows; y++)
                {
                    if (grid[x, y] != null) pieces.Add(grid[x, y]);
                }
            }
            return pieces.Count == 0 ? null : pieces[Random.Range(0, pieces.Count)];
        }

        public List<PieceView> GetSpecialPieces()
        {
            var specials = new List<PieceView>();
            if (grid == null) return specials;

            for (int y = Rows - 1; y >= 0; y--)
            {
                for (int x = 0; x < Columns; x++)
                {
                    PieceView piece = grid[x, y];
                    if (piece != null && piece.IsSpecial) specials.Add(piece);
                }
            }

            // Trigger the strongest finale pieces first. Their resolution can
            // naturally enqueue any other special caught by the blast.
            specials.Sort((a, b) => GetSpecialPriority(b.SpecialType).CompareTo(GetSpecialPriority(a.SpecialType)));
            return specials;
        }

        private static int GetSpecialPriority(PieceSpecialType type)
        {
            return type switch
            {
                PieceSpecialType.MegaBurst => 5,
                PieceSpecialType.ColorBurst => 4,
                PieceSpecialType.AreaBlast => 3,
                PieceSpecialType.RowBlast => 2,
                PieceSpecialType.ColumnBlast => 2,
                _ => 0
            };
        }

        public bool TrySwapAndFindMatches(PieceView first, PieceView second, out List<PieceView> matches)
        {
            matches = new List<PieceView>();
            if (first == null || second == null || !AreAdjacent(first.gridX, first.gridY, second.gridX, second.gridY)) return false;
            bool specialPair = first.IsSpecial && second.IsSpecial;
            bool colorBurstSwap = first.SpecialType == PieceSpecialType.ColorBurst ||
                second.SpecialType == PieceSpecialType.ColorBurst;
            int ax = first.gridX, ay = first.gridY, bx = second.gridX, by = second.gridY;
            grid[ax, ay] = second; grid[bx, by] = first;
            first.SetGridPosition(bx, by); second.SetGridPosition(ax, ay);
            matches = FindMatches();
            bool matchCreatedBySwap = false;
            for (int i = 0; i < matches.Count; i++)
            {
                if (matches[i] == first || matches[i] == second)
                {
                    matchCreatedBySwap = true;
                    break;
                }
            }
            if (!specialPair && !colorBurstSwap && (matches.Count < 3 || !matchCreatedBySwap))
            {
                grid[ax, ay] = first; grid[bx, by] = second;
                first.SetGridPosition(ax, ay); second.SetGridPosition(bx, by);
                matches.Clear();
                ClearLastSwap();
                return false;
            }
            if (specialPair || colorBurstSwap)
            {
                matches.Clear();
                matches.Add(first);
                matches.Add(second);
            }
            lastSwapFirst = first;
            lastSwapSecond = second;
            // Keep the logical swap immediate, but animate both views toward
            // the opposite cell so the player clearly sees the exchange.
            first.MoveToWorldPosition(GridToWorldPosition(bx, by), 10f);
            second.MoveToWorldPosition(GridToWorldPosition(ax, ay), 10f);
            return true;
        }

        private void BuildObstacles()
        {
            ClearObstacles();
            if (config == null || config.obstacleType == CellObstacleType.None || config.obstacleCount <= 0)
                return;

            obstacleHealth = new int[Columns, Rows];
            obstacleRenderers = new SpriteRenderer[Columns, Rows];
            GameObject root = new GameObject("[WorldObstacles]");
            obstacleRoot = root.transform;
            obstacleRoot.SetParent(transform, false);

            List<Vector2Int> candidates = new List<Vector2Int>();
            for (int x = 0; x < Columns; x++)
                for (int y = 0; y < Rows; y++)
                    if (IsPlayableCell(x, y)) candidates.Add(new Vector2Int(x, y));

            int seed = Columns * 73856093 ^ Rows * 19349663 ^ config.obstacleCount * 83492791 ^
                (int)config.obstacleType * 7919;
            System.Random random = new System.Random(seed);
            for (int i = candidates.Count - 1; i > 0; i--)
            {
                int swap = random.Next(i + 1);
                (candidates[i], candidates[swap]) = (candidates[swap], candidates[i]);
            }

            int count = Mathf.Min(config.obstacleCount, candidates.Count);
            int durability = Mathf.Clamp(config.obstacleDurability, 1, 3);
            RemainingObstacleCount = count;
            for (int i = 0; i < count; i++)
            {
                Vector2Int cell = candidates[i];
                obstacleHealth[cell.x, cell.y] = durability;
                GameObject visual = new GameObject($"Obstacle_{config.obstacleType}_{cell.x}_{cell.y}");
                visual.transform.SetParent(obstacleRoot, false);
                visual.transform.position = GridToWorldPosition(cell.x, cell.y) + Vector3.forward * 0.35f;
                float obstacleScale = config.obstacleType == CellObstacleType.Vine ? 1.02f :
                    config.obstacleType == CellObstacleType.Lantern ? 0.82f :
                    config.obstacleType == CellObstacleType.Sand ? 0.94f : 1.02f;
                visual.transform.localScale = Vector3.one * ActivePieceSpacing * obstacleScale;
                SpriteRenderer renderer = visual.AddComponent<SpriteRenderer>();
                renderer.sprite = GetObstacleSprite(config.obstacleType);
                renderer.sortingOrder = -16;
                renderer.color = ObstacleColor(config.obstacleType, durability, durability);
                if (config.obstacleType == CellObstacleType.Vine)
                {
                    GameObject shadowObject = new GameObject("VineDepth");
                    shadowObject.transform.SetParent(visual.transform, false);
                    shadowObject.transform.localPosition = new Vector3(0.018f, -0.025f, 0.01f);
                    shadowObject.transform.localScale = Vector3.one * 1.04f;
                    SpriteRenderer shadowRenderer = shadowObject.AddComponent<SpriteRenderer>();
                    shadowRenderer.sprite = renderer.sprite;
                    shadowRenderer.sortingOrder = -17;
                    shadowRenderer.color = new Color(0.025f, 0.12f, 0.035f, 0.72f);
                }
                obstacleRenderers[cell.x, cell.y] = renderer;
            }
        }

        public int DamageObstacles(IReadOnlyList<PieceView> affectedPieces, bool specialImpact = false)
        {
            if (affectedPieces == null || obstacleHealth == null) return 0;
            HashSet<Vector2Int> hitCells = new HashSet<Vector2Int>();
            for (int i = 0; i < affectedPieces.Count; i++)
            {
                PieceView piece = affectedPieces[i];
                if (piece == null) continue;
                hitCells.Add(new Vector2Int(piece.gridX, piece.gridY));
                // Loose sand is cleared by matching on it or directly beside it.
                if (config.obstacleType == CellObstacleType.Sand)
                {
                    hitCells.Add(new Vector2Int(piece.gridX + 1, piece.gridY));
                    hitCells.Add(new Vector2Int(piece.gridX - 1, piece.gridY));
                    hitCells.Add(new Vector2Int(piece.gridX, piece.gridY + 1));
                    hitCells.Add(new Vector2Int(piece.gridX, piece.gridY - 1));
                }
                // Festival lanterns react to the shockwave of a special piece,
                // so a special combo also reaches their four neighbouring cells.
                if (specialImpact && (config.obstacleType == CellObstacleType.Lantern ||
                    config.obstacleType == CellObstacleType.Ice))
                {
                    hitCells.Add(new Vector2Int(piece.gridX + 1, piece.gridY));
                    hitCells.Add(new Vector2Int(piece.gridX - 1, piece.gridY));
                    hitCells.Add(new Vector2Int(piece.gridX, piece.gridY + 1));
                    hitCells.Add(new Vector2Int(piece.gridX, piece.gridY - 1));
                }
            }

            int cleared = 0;
            int damage = specialImpact &&
                (config.obstacleType == CellObstacleType.Lantern || config.obstacleType == CellObstacleType.Ice)
                ? 2 : 1;
            foreach (Vector2Int cell in hitCells)
            {
                if (!IsValidGridPos(cell.x, cell.y) || obstacleHealth[cell.x, cell.y] <= 0) continue;
                int maximum = Mathf.Clamp(config.obstacleDurability, 1, 3);
                obstacleHealth[cell.x, cell.y] -= damage;
                SpriteRenderer renderer = obstacleRenderers[cell.x, cell.y];
                if (obstacleHealth[cell.x, cell.y] <= 0)
                {
                    cleared++;
                    RemainingObstacleCount = Mathf.Max(0, RemainingObstacleCount - 1);
                    if (renderer != null) Destroy(renderer.gameObject);
                    obstacleRenderers[cell.x, cell.y] = null;
                }
                else if (renderer != null)
                {
                    renderer.color = ObstacleColor(config.obstacleType, obstacleHealth[cell.x, cell.y], maximum);
                    renderer.transform.localScale *= 0.88f;
                }
            }
            return cleared;
        }

        private void ClearObstacles()
        {
            if (obstacleRoot != null) Destroy(obstacleRoot.gameObject);
            obstacleRoot = null;
            obstacleHealth = null;
            obstacleRenderers = null;
            RemainingObstacleCount = 0;
        }

        private static Color ObstacleColor(CellObstacleType type, int health, int maximum)
        {
            float strength = Mathf.Clamp01(health / (float)Mathf.Max(1, maximum));
            switch (type)
            {
                case CellObstacleType.Vine:
                    return new Color(0.16f + strength * 0.06f, 0.43f + strength * 0.12f, 0.08f, 0.70f + strength * 0.12f);
                case CellObstacleType.Lantern:
                    return new Color(1f, 0.62f + strength * 0.18f, 0.12f, 0.72f + strength * 0.14f);
                case CellObstacleType.Sand:
                    return new Color(0.96f, 0.67f + strength * 0.12f, 0.27f, 0.46f + strength * 0.16f);
                case CellObstacleType.Ice:
                    return new Color(0.46f + strength * 0.20f, 0.82f + strength * 0.12f, 1f, 0.52f + strength * 0.18f);
                default:
                    return Color.clear;
            }
        }

        private static Sprite GetObstacleSprite(CellObstacleType type)
        {
            if (type == CellObstacleType.Vine && vineObstacleSprite != null) return vineObstacleSprite;
            if (type == CellObstacleType.Lantern && lanternObstacleSprite != null) return lanternObstacleSprite;
            if (type == CellObstacleType.Sand && sandObstacleSprite != null) return sandObstacleSprite;
            if (type == CellObstacleType.Ice && iceObstacleSprite != null) return iceObstacleSprite;
            const int size = 64;
            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = $"JoinDog{type}Obstacle",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
            Color[] pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float nx = (x + 0.5f) / size * 2f - 1f;
                    float ny = (y + 0.5f) / size * 2f - 1f;
                    float radius = Mathf.Sqrt(nx * nx + ny * ny);
                    float angle = Mathf.Atan2(ny, nx);
                    float alpha;
                    if (type == CellObstacleType.Vine)
                    {
                        // Asymmetric stems and leaves read as a real obstacle,
                        // unlike the old full ring which resembled selection.
                        float leftStem = Mathf.Abs(nx - (-0.70f + Mathf.Sin(ny * 4.0f) * 0.045f));
                        float bottomStem = Mathf.Abs(ny - (-0.70f + Mathf.Sin(nx * 3.7f) * 0.045f));
                        float stems = 0f;
                        if (ny > -0.68f && ny < 0.38f) stems = Mathf.Max(stems, Mathf.Clamp01(1f - leftStem / 0.065f));
                        if (nx > -0.68f && nx < 0.28f) stems = Mathf.Max(stems, Mathf.Clamp01(1f - bottomStem / 0.065f));

                        float curlRadius = Mathf.Sqrt((nx - 0.18f) * (nx - 0.18f) + (ny + 0.54f) * (ny + 0.54f));
                        float curl = nx > 0.05f
                            ? Mathf.Clamp01(1f - Mathf.Abs(curlRadius - 0.17f) / 0.055f)
                            : 0f;
                        float leafA = Mathf.Clamp01(1f - (((nx + 0.58f) * (nx + 0.58f)) / 0.055f +
                            ((ny - 0.18f) * (ny - 0.18f)) / 0.018f));
                        float leafB = Mathf.Clamp01(1f - (((nx + 0.42f) * (nx + 0.42f)) / 0.048f +
                            ((ny + 0.68f) * (ny + 0.68f)) / 0.016f));
                        float leafC = Mathf.Clamp01(1f - (((nx + 0.68f) * (nx + 0.68f)) / 0.042f +
                            ((ny + 0.10f) * (ny + 0.10f)) / 0.022f));
                        alpha = Mathf.Clamp01(stems * 0.90f + curl * 0.82f +
                            Mathf.Max(leafA, Mathf.Max(leafB, leafC)));
                    }
                    else if (type == CellObstacleType.Lantern)
                    {
                        // Compact hanging lamp. It remains visible around the
                        // piece without drawing a large warning diamond over it.
                        float body = 1f - Mathf.Clamp01((((nx) * (nx)) / 0.22f +
                            ((ny + 0.20f) * (ny + 0.20f)) / 0.18f - 0.68f) / 0.22f);
                        float rim = Mathf.Clamp01(1f - Mathf.Abs(ny + 0.54f) / 0.055f) *
                            Mathf.Clamp01(1f - Mathf.Abs(nx) / 0.42f);
                        float cap = Mathf.Clamp01(1f - Mathf.Abs(ny - 0.30f) / 0.065f) *
                            Mathf.Clamp01(1f - Mathf.Abs(nx) / 0.28f);
                        float hookRadius = Mathf.Sqrt(nx * nx + (ny - 0.47f) * (ny - 0.47f));
                        float hook = Mathf.Clamp01(1f - Mathf.Abs(hookRadius - 0.15f) / 0.045f) *
                            Mathf.Clamp01((ny - 0.40f) * 12f);
                        alpha = Mathf.Clamp01(body * 0.48f + rim * 0.94f + cap * 0.92f + hook * 0.84f);
                    }
                    else if (type == CellObstacleType.Sand)
                    {
                        // A low dune and a handful of grains: the piece stays
                        // readable and the obstacle reads from the lower edge.
                        float duneLine = -0.38f + Mathf.Sin(nx * 3.4f) * 0.10f;
                        float dune = ny < duneLine
                            ? Mathf.Clamp01((duneLine - ny) * 2.8f) : 0f;
                        float mound = ny < -0.08f
                            ? Mathf.Clamp01(1f - (nx * nx * 0.74f + (ny + 0.42f) * (ny + 0.42f)) / 0.78f)
                            : 0f;
                        float grainMask = Mathf.Pow(Mathf.Max(0f,
                            Mathf.Sin((x * 17 + y * 11) * 0.37f)), 28f);
                        float grains = grainMask * Mathf.Clamp01(1f - radius) * (ny < 0.25f ? 1f : 0f);
                        alpha = Mathf.Clamp01(dune * 0.46f + mound * 0.24f + grains * 0.66f);
                    }
                    else
                    {
                        // Frosted rim with thin cracks instead of an opaque ice
                        // plate covering the underlying piece.
                        float square = Mathf.Max(Mathf.Abs(nx), Mathf.Abs(ny));
                        float rim = Mathf.Clamp01(1f - Mathf.Abs(square - 0.78f) / 0.075f);
                        float cracks = Mathf.Pow(Mathf.Abs(Mathf.Sin(angle * 3f + radius * 9f)), 24f) *
                            Mathf.Clamp01((0.72f - radius) * 2.2f);
                        float cornerFrost = Mathf.Pow(Mathf.Clamp01((square - 0.48f) / 0.32f), 2f);
                        alpha = Mathf.Clamp01(rim * 0.82f + cracks * 0.55f + cornerFrost * 0.18f);
                    }
                    pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
                }
            }
            texture.SetPixels(pixels);
            texture.Apply(false, true);
            Sprite sprite = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), size);
            sprite.name = $"JoinDog{type}ObstacleSprite";
            if (type == CellObstacleType.Vine) vineObstacleSprite = sprite;
            else if (type == CellObstacleType.Lantern) lanternObstacleSprite = sprite;
            else if (type == CellObstacleType.Sand) sandObstacleSprite = sprite;
            else iceObstacleSprite = sprite;
            return sprite;
        }

        public MatchResolution BuildMatchResolution(List<PieceView> matches)
        {
            MatchResolution result = new MatchResolution
            {
                OriginalMatchCount = matches != null ? matches.Count : 0
            };
            HashSet<PieceView> removal = new HashSet<PieceView>();
            Queue<PieceView> specialsToActivate = new Queue<PieceView>();
            HashSet<PieceView> queuedSpecials = new HashSet<PieceView>();

            void AddPiece(PieceView piece)
            {
                if (piece == null) return;
                removal.Add(piece);
                if (piece.IsSpecial && queuedSpecials.Add(piece))
                    specialsToActivate.Enqueue(piece);
            }

            bool hasLastSwap = lastSwapFirst != null && lastSwapSecond != null;
            bool specialPair = hasLastSwap && lastSwapFirst.IsSpecial && lastSwapSecond.IsSpecial;
            bool doubleColorBurst = hasLastSwap &&
                lastSwapFirst.SpecialType == PieceSpecialType.ColorBurst &&
                lastSwapSecond.SpecialType == PieceSpecialType.ColorBurst;
            bool colorBurstCombo = hasLastSwap && !doubleColorBurst &&
                (lastSwapFirst.SpecialType == PieceSpecialType.ColorBurst ||
                 lastSwapSecond.SpecialType == PieceSpecialType.ColorBurst);
            bool megaCombo = doubleColorBurst || (specialPair &&
                (lastSwapFirst.SpecialType == PieceSpecialType.MegaBurst ||
                 lastSwapSecond.SpecialType == PieceSpecialType.MegaBurst));
            PieceType colorTarget = PieceType.None;
            if (megaCombo)
            {
                result.MegaCombo = true;
                result.ComboKind = SpecialComboKind.BoardNova;
                for (int x = 0; x < Columns; x++)
                    for (int y = 0; y < Rows; y++)
                        AddPiece(GetPieceAt(x, y));
            }
            else if (colorBurstCombo)
            {
                result.ColorBurstCombo = true;
                result.ComboKind = SpecialComboKind.ColorSweep;
                PieceView burst = lastSwapFirst.SpecialType == PieceSpecialType.ColorBurst
                    ? lastSwapFirst
                    : lastSwapSecond;
                PieceView target = burst == lastSwapFirst ? lastSwapSecond : lastSwapFirst;
                colorTarget = target.type;
                AddPiece(burst);
                for (int x = 0; x < Columns; x++)
                    for (int y = 0; y < Rows; y++)
                    {
                        PieceView piece = GetPieceAt(x, y);
                        if (piece != null && piece.type == colorTarget) AddPiece(piece);
                    }
            }
            else if (specialPair)
            {
                AddPiece(lastSwapFirst);
                AddPiece(lastSwapSecond);
                result.ComboKind = ClassifySpecialPair(lastSwapFirst.SpecialType, lastSwapSecond.SpecialType);
                ExpandSpecialPair(result.ComboKind, lastSwapFirst, lastSwapSecond, AddPiece);
            }
            else if (matches != null)
            {
                foreach (PieceView piece in matches) AddPiece(piece);
            }

            while (specialsToActivate.Count > 0)
            {
                PieceView special = specialsToActivate.Dequeue();
                result.SpecialsActivated++;
                result.ActivatedSpecials.Add(special);
                if (special.SpecialType == PieceSpecialType.RowBlast)
                {
                    foreach (PieceView piece in GetRowPieces(special.gridY)) AddPiece(piece);
                }
                else if (special.SpecialType == PieceSpecialType.ColumnBlast)
                {
                    foreach (PieceView piece in GetColumnPieces(special.gridX)) AddPiece(piece);
                }
                else if (special.SpecialType == PieceSpecialType.AreaBlast)
                {
                    for (int x = special.gridX - 1; x <= special.gridX + 1; x++)
                        for (int y = special.gridY - 1; y <= special.gridY + 1; y++)
                            AddPiece(GetPieceAt(x, y));
                }
                else if (special.SpecialType == PieceSpecialType.ColorBurst)
                {
                    PieceType targetType = colorTarget != PieceType.None ? colorTarget : special.type;
                    for (int x = 0; x < Columns; x++)
                        for (int y = 0; y < Rows; y++)
                        {
                            PieceView piece = GetPieceAt(x, y);
                            if (piece != null && piece.type == targetType) AddPiece(piece);
                        }
                }
                else if (special.SpecialType == PieceSpecialType.MegaBurst)
                {
                    // A six-piece match creates a supernova: it clears every
                    // piece of its colour and both axes through its origin.
                    foreach (PieceView piece in GetRowPieces(special.gridY)) AddPiece(piece);
                    foreach (PieceView piece in GetColumnPieces(special.gridX)) AddPiece(piece);
                    for (int x = 0; x < Columns; x++)
                        for (int y = 0; y < Rows; y++)
                        {
                            PieceView piece = GetPieceAt(x, y);
                            if (piece != null && piece.type == special.type) AddPiece(piece);
                        }
                }
            }

            if (!megaCombo && result.SpecialsActivated == 0 && matches != null)
            {
                PieceView candidate = ChooseSpecialCandidate(matches);
                PieceSpecialType specialType = DetermineSpecialType(candidate);
                if (candidate != null && specialType != PieceSpecialType.None)
                {
                    removal.Remove(candidate);
                    candidate.SetSelected(false);
                    candidate.SetSpecial(specialType);
                    result.CreatedSpecial = candidate;
                    result.CreatedSpecialType = specialType;
                }
            }

            result.PiecesToRemove.AddRange(removal);
            ClearLastSwap();
            return result;
        }

        public static SpecialComboKind ClassifySpecialPair(PieceSpecialType first, PieceSpecialType second)
        {
            if (first == PieceSpecialType.RowBlast && second == PieceSpecialType.RowBlast)
                return SpecialComboKind.DoubleRow;
            if (first == PieceSpecialType.ColumnBlast && second == PieceSpecialType.ColumnBlast)
                return SpecialComboKind.DoubleColumn;
            if ((first == PieceSpecialType.RowBlast && second == PieceSpecialType.ColumnBlast) ||
                (first == PieceSpecialType.ColumnBlast && second == PieceSpecialType.RowBlast))
                return SpecialComboKind.CrossBlast;
            if (first == PieceSpecialType.AreaBlast && second == PieceSpecialType.AreaBlast)
                return SpecialComboKind.DoubleArea;
            if ((first == PieceSpecialType.AreaBlast && second == PieceSpecialType.RowBlast) ||
                (second == PieceSpecialType.AreaBlast && first == PieceSpecialType.RowBlast))
                return SpecialComboKind.WideRow;
            if ((first == PieceSpecialType.AreaBlast && second == PieceSpecialType.ColumnBlast) ||
                (second == PieceSpecialType.AreaBlast && first == PieceSpecialType.ColumnBlast))
                return SpecialComboKind.WideColumn;
            return SpecialComboKind.CrossBlast;
        }

        private void ExpandSpecialPair(
            SpecialComboKind comboKind,
            PieceView first,
            PieceView second,
            System.Action<PieceView> addPiece)
        {
            if (addPiece == null || first == null || second == null) return;

            if (comboKind == SpecialComboKind.WideRow)
            {
                PieceView area = first.SpecialType == PieceSpecialType.AreaBlast ? first : second;
                for (int y = area.gridY - 1; y <= area.gridY + 1; y++)
                    foreach (PieceView piece in GetRowPieces(y)) addPiece(piece);
            }
            else if (comboKind == SpecialComboKind.WideColumn)
            {
                PieceView area = first.SpecialType == PieceSpecialType.AreaBlast ? first : second;
                for (int x = area.gridX - 1; x <= area.gridX + 1; x++)
                    foreach (PieceView piece in GetColumnPieces(x)) addPiece(piece);
            }
            else if (comboKind == SpecialComboKind.DoubleArea)
            {
                foreach (PieceView center in new[] { first, second })
                    for (int x = center.gridX - 2; x <= center.gridX + 2; x++)
                        for (int y = center.gridY - 2; y <= center.gridY + 2; y++)
                            addPiece(GetPieceAt(x, y));
            }
        }

        private PieceView ChooseSpecialCandidate(List<PieceView> matches)
        {
            if (matches == null || matches.Count < 4) return null;
            if (lastSwapFirst != null && matches.Contains(lastSwapFirst) && DetermineSpecialType(lastSwapFirst) != PieceSpecialType.None)
                return lastSwapFirst;
            if (lastSwapSecond != null && matches.Contains(lastSwapSecond) && DetermineSpecialType(lastSwapSecond) != PieceSpecialType.None)
                return lastSwapSecond;
            foreach (PieceView piece in matches)
                if (DetermineSpecialType(piece) != PieceSpecialType.None) return piece;
            return null;
        }

        private PieceSpecialType DetermineSpecialType(PieceView piece)
        {
            if (piece == null) return PieceSpecialType.None;
            int horizontal = CountRun(piece, Vector2Int.left) + CountRun(piece, Vector2Int.right) + 1;
            int vertical = CountRun(piece, Vector2Int.down) + CountRun(piece, Vector2Int.up) + 1;
            return ClassifySpecialForRuns(horizontal, vertical);
        }

        public static PieceSpecialType ClassifySpecialForRuns(int horizontal, int vertical)
        {
            horizontal = Mathf.Max(1, horizontal);
            vertical = Mathf.Max(1, vertical);
            if (horizontal >= 6 || vertical >= 6)
                return PieceSpecialType.MegaBurst;
            if (horizontal >= 5 || vertical >= 5)
                return PieceSpecialType.ColorBurst;
            if (horizontal >= 3 && vertical >= 3)
                return PieceSpecialType.AreaBlast;
            if (horizontal >= 4) return PieceSpecialType.RowBlast;
            if (vertical >= 4) return PieceSpecialType.ColumnBlast;
            return PieceSpecialType.None;
        }

        private int CountRun(PieceView origin, Vector2Int direction)
        {
            int count = 0;
            int x = origin.gridX + direction.x;
            int y = origin.gridY + direction.y;
            while (IsValidGridPos(x, y))
            {
                PieceView piece = GetPieceAt(x, y);
                if (piece == null || piece.type != origin.type) break;
                count++;
                x += direction.x;
                y += direction.y;
            }
            return count;
        }

        private void ClearLastSwap()
        {
            lastSwapFirst = null;
            lastSwapSecond = null;
        }

        public void PreviewSwap(PieceView first, PieceView second)
        {
            if (first == null || second == null) return;
            first.MoveToWorldPosition(GridToWorldPosition(second.gridX, second.gridY), 14f);
            second.MoveToWorldPosition(GridToWorldPosition(first.gridX, first.gridY), 14f);
        }

        public void RestorePreviewSwap(PieceView first, PieceView second)
        {
            if (first == null || second == null) return;
            first.MoveToWorldPosition(GridToWorldPosition(first.gridX, first.gridY), 14f);
            second.MoveToWorldPosition(GridToWorldPosition(second.gridX, second.gridY), 14f);
        }

        public List<PieceView> FindMatches()
        {
            var result = new HashSet<PieceView>();
            for (int y = 0; y < Rows; y++)
            {
                int runStart = 0;
                while (runStart < Columns)
                {
                    PieceView start = GetPieceAt(runStart, y);
                    if (start == null) { runStart++; continue; }
                    int end = runStart + 1;
                    while (end < Columns && GetPieceAt(end, y) != null && GetPieceAt(end, y).type == start.type) end++;
                    if (end - runStart >= 3) for (int x = runStart; x < end; x++) result.Add(GetPieceAt(x, y));
                    runStart = end;
                }
            }
            for (int x = 0; x < Columns; x++)
            {
                int runStart = 0;
                while (runStart < Rows)
                {
                    PieceView start = GetPieceAt(x, runStart);
                    if (start == null) { runStart++; continue; }
                    int end = runStart + 1;
                    while (end < Rows && GetPieceAt(x, end) != null && GetPieceAt(x, end).type == start.type) end++;
                    if (end - runStart >= 3) for (int y = runStart; y < end; y++) result.Add(GetPieceAt(x, y));
                    runStart = end;
                }
            }
            return new List<PieceView>(result);
        }
    }
}
