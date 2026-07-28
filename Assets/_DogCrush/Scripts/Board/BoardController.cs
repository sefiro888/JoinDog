using System.Collections.Generic;
using UnityEngine;

namespace DogCrush.Board
{
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

        public PieceView[,] Grid => grid;
        public int Columns => config != null ? config.columns : 8;
        public int Rows => config != null ? config.rows : 8;
        public float ActivePieceSpacing => activePieceSpacing;
        public float ActiveBoardCenterY => activeBoardCenterY;

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
                        grid[x, y].Initialize(newType, x, y, spawner.GetSpriteForType(newType), spawner.GetColorForType(newType));
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

        public bool TrySwapAndFindMatches(PieceView first, PieceView second, out List<PieceView> matches)
        {
            matches = new List<PieceView>();
            if (first == null || second == null || !AreAdjacent(first.gridX, first.gridY, second.gridX, second.gridY)) return false;
            int ax = first.gridX, ay = first.gridY, bx = second.gridX, by = second.gridY;
            grid[ax, ay] = second; grid[bx, by] = first;
            first.SetGridPosition(bx, by); second.SetGridPosition(ax, ay);
            matches = FindMatches();
            if (matches.Count < 3)
            {
                grid[ax, ay] = first; grid[bx, by] = second;
                first.SetGridPosition(ax, ay); second.SetGridPosition(bx, by);
                matches.Clear();
                return false;
            }
            // Keep the logical swap immediate, but animate both views toward
            // the opposite cell so the player clearly sees the exchange.
            first.MoveToWorldPosition(GridToWorldPosition(bx, by), 10f);
            second.MoveToWorldPosition(GridToWorldPosition(ax, ay), 10f);
            return true;
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
