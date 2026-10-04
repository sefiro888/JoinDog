using System.Collections;
using System.Collections.Generic;
using JoinDog.App;
using UnityEngine;

namespace DogCrush.Board
{
    public sealed class MatchResolution
    {
        public readonly List<PieceView> PiecesToRemove = new List<PieceView>();
        public readonly List<PieceView> ActivatedSpecials = new List<PieceView>();
        public readonly Dictionary<PieceView, List<Vector3>> BallBounceDestinations = new Dictionary<PieceView, List<Vector3>>();
        public PieceView CreatedSpecial;
        public PieceSpecialType CreatedSpecialType;
        public int SpecialsActivated;
        public bool MegaCombo;
        public bool ColorBurstCombo;
        public SpecialComboKind ComboKind;
        public PieceView ComboAnchor;
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
        private readonly HashSet<Vector2Int> harvestedGardenCells = new HashSet<Vector2Int>();
        private readonly HashSet<Vector2Int> collectedShelterCells = new HashSet<Vector2Int>();
        private readonly HashSet<Vector2Int> rungFestivalBells = new HashSet<Vector2Int>();
        private readonly HashSet<Vector2Int> usedCoastTides = new HashSet<Vector2Int>();
        private readonly HashSet<Vector2Int> usedMountainWarmth = new HashSet<Vector2Int>();
        private readonly HashSet<Vector2Int> usedAuroraPrisms = new HashSet<Vector2Int>();
        private readonly HashSet<Vector2Int> usedSummitCrystals = new HashSet<Vector2Int>();
        private readonly HashSet<Vector2Int> usedCelestialSprouts = new HashSet<Vector2Int>();
        private readonly HashSet<Vector2Int> usedRubyGeysers = new HashSet<Vector2Int>();
        private readonly HashSet<Vector2Int> usedSanctuarySeals = new HashSet<Vector2Int>();
        private PieceView lastSwapFirst;
        private PieceView lastSwapSecond;
        private int[,] obstacleHealth;
        private SpriteRenderer[,] obstacleRenderers;
        private Transform obstacleRoot;
        private int initialObstacleCount;
        private static Sprite vineObstacleSprite;
        private static Sprite lanternObstacleSprite;
        private static Sprite sandObstacleSprite;
        private static Sprite iceObstacleSprite;
        private static Sprite puppyCageObstacleSprite;
        private static Sprite obstacleLayerDot;

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
            harvestedGardenCells.Clear();
            collectedShelterCells.Clear();
            rungFestivalBells.Clear();
            usedCoastTides.Clear();
            usedMountainWarmth.Clear();
            usedAuroraPrisms.Clear();
            usedSummitCrystals.Clear();
            usedCelestialSprouts.Clear();
            usedRubyGeysers.Clear();
            usedSanctuarySeals.Clear();
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

        public void ClearSpecialPreview() => adaptiveView?.ClearRangePreview();

        public List<PieceView> GetDirectSpecialPreview(PieceView special)
        {
            var result = new List<PieceView>();
            if (grid == null || special == null || !special.IsSpecial || GetPieceAt(special.gridX, special.gridY) != special) return result;
            // This is the direct footprint at the current cell, not a prediction
            // of a swap, a random bounce or secondary special cascades.
            if (special.SpecialType == PieceSpecialType.BallBounce) return result;
            foreach (var piece in grid)
            {
                if (piece == null) continue;
                int dx = Mathf.Abs(piece.gridX - special.gridX), dy = Mathf.Abs(piece.gridY - special.gridY);
                bool affected = special.SpecialType switch
                {
                    PieceSpecialType.RowBlast => dy == 0,
                    PieceSpecialType.ColumnBlast => dx == 0,
                    PieceSpecialType.AreaBlast => dx <= 1 && dy <= 1,
                    PieceSpecialType.ColorBurst => piece.type == special.type,
                    PieceSpecialType.MegaBurst => dx == 0 || dy == 0 || piece.type == special.type,
                    PieceSpecialType.Comet => dx == dy,
                    PieceSpecialType.Whistle => piece.gridY == Rows / 2 || piece.type == special.type,
                    _ => false
                };
                if (affected || piece == special) result.Add(piece);
            }
            return result;
        }

        public void ShowSpecialPreview(PieceView special) => adaptiveView?.ShowRangePreview(GetDirectSpecialPreview(special));

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
            string[] manualLayout = config.layoutRows;
            if (manualLayout != null && manualLayout.Length == Rows)
            {
                int rowIndex = Rows - 1 - y;
                if (rowIndex >= 0 && rowIndex < manualLayout.Length &&
                    !string.IsNullOrEmpty(manualLayout[rowIndex]) &&
                    manualLayout[rowIndex].Length == Columns &&
                    manualLayout[rowIndex][x] == '#') return false;
            }
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

        public bool IsCompanionGardenCell(int x, int y)
        {
            if (!IsPlayableCell(x, y) || config.companionGardenCells == null) return false;
            foreach (string value in config.companionGardenCells)
            {
                var parts = value?.Split(',');
                if (parts == null || parts.Length != 2) continue;
                if (int.TryParse(parts[0], out int cellX) && int.TryParse(parts[1], out int cellY) &&
                    cellX == x && cellY == y) return true;
            }
            return false;
        }

        public bool IsCompanionGardenHarvested(int x, int y) =>
            harvestedGardenCells.Contains(new Vector2Int(x, y));

        public bool IsFestivalBellCell(int x,int y)
        {
            if(!IsPlayableCell(x,y) || config.festivalBellCells == null) return false;
            foreach(string value in config.festivalBellCells)
            {
                var parts=value?.Split(',');
                if(parts==null || parts.Length!=2) continue;
                if(int.TryParse(parts[0],out int cellX) && int.TryParse(parts[1],out int cellY) && cellX==x && cellY==y)
                    return true;
            }
            return false;
        }

        public bool IsFestivalBellRung(int x,int y) => rungFestivalBells.Contains(new Vector2Int(x,y));

        public bool IsCoastTideCell(int x,int y)
        {
            if(!IsPlayableCell(x,y) || config.obstacleType!=CellObstacleType.Sand || config.coastTideCells==null) return false;
            foreach(string value in config.coastTideCells)
            {
                var parts=value?.Split(',');
                if(parts==null || parts.Length!=2) continue;
                if(int.TryParse(parts[0],out int cellX) && int.TryParse(parts[1],out int cellY) && cellX==x && cellY==y)
                    return true;
            }
            return false;
        }

        public bool IsCoastTideUsed(int x,int y) => usedCoastTides.Contains(new Vector2Int(x,y));

        public bool IsCelestialSproutCell(int x,int y)
        {
            if(!IsPlayableCell(x,y) || config.obstacleType!=CellObstacleType.Vine || config.celestialSproutCells==null) return false;
            foreach(string value in config.celestialSproutCells)
            {
                var parts=value?.Split(',');
                if(parts==null || parts.Length!=2) continue;
                if(int.TryParse(parts[0],out int cellX) && int.TryParse(parts[1],out int cellY) && cellX==x && cellY==y) return true;
            }
            return false;
        }

        public bool IsCelestialSproutUsed(int x,int y) => usedCelestialSprouts.Contains(new Vector2Int(x,y));

        // Called for resolution.CreatedSpecial only, never for a falling/activated special.
        public List<Vector2Int> TryCollectCelestialSprout(PieceView created,List<PieceView> removed)
        {
            var extraHits=new List<Vector2Int>();
            if(grid==null || obstacleHealth==null || created==null || !created.IsSpecial || removed==null ||
                GetPieceAt(created.gridX,created.gridY)!=created || !IsCelestialSproutCell(created.gridX,created.gridY) ||
                IsCelestialSproutUsed(created.gridX,created.gridY)) return extraHits;
            foreach(var direction in OrthogonalDirections)
            {
                var cell=new Vector2Int(created.gridX,created.gridY)+direction;
                if(!IsPlayableCell(cell.x,cell.y) || obstacleHealth[cell.x,cell.y]<=0) continue;
                bool alreadyHit=false;
                foreach(var piece in removed)
                    if(piece!=null && piece.gridX==cell.x && piece.gridY==cell.y) {alreadyHit=true;break;}
                if(!alreadyHit) extraHits.Add(cell);
            }
            if(extraHits.Count>0)
            {
                var origin=new Vector2Int(created.gridX,created.gridY);
                usedCelestialSprouts.Add(origin);adaptiveView?.SetCelestialSproutUsed(origin);
            }
            return extraHits;
        }

        public bool IsSanctuarySealCell(int x,int y)
        {
            if(!IsPlayableCell(x,y) || config.obstacleType!=CellObstacleType.Lantern || config.sanctuarySealCells==null) return false;
            foreach(string value in config.sanctuarySealCells)
            {
                var parts=value?.Split(',');
                if(parts==null || parts.Length!=2) continue;
                if(int.TryParse(parts[0],out int cellX) && int.TryParse(parts[1],out int cellY) && cellX==x && cellY==y) return true;
            }
            return false;
        }

        public bool IsSanctuarySealUsed(int x,int y) => usedSanctuarySeals.Contains(new Vector2Int(x,y));

        // Called for resolution.CreatedSpecial only, never for a falling/activated special.
        public List<Vector2Int> TryCollectSanctuarySeal(PieceView created,List<PieceView> removed)
        {
            var extraHits=new List<Vector2Int>();
            if(grid==null || obstacleHealth==null || created==null || !created.IsSpecial || removed==null ||
                GetPieceAt(created.gridX,created.gridY)!=created || !IsSanctuarySealCell(created.gridX,created.gridY) ||
                IsSanctuarySealUsed(created.gridX,created.gridY)) return extraHits;
            int bestDistance=int.MaxValue;
            Vector2Int bestCell=default;
            // Stable x/y order breaks equal-distance ties without consuming gameplay RNG.
            for(int x=0;x<Columns;x++) for(int y=0;y<Rows;y++)
            {
                if(!IsPlayableCell(x,y) || obstacleHealth[x,y]<=0) continue;
                bool alreadyHit=false;
                foreach(var piece in removed)
                    if(piece!=null && piece.gridX==x && piece.gridY==y) {alreadyHit=true;break;}
                if(alreadyHit) continue;
                int distance=Mathf.Abs(x-created.gridX)+Mathf.Abs(y-created.gridY);
                if(distance>=bestDistance) continue;
                bestDistance=distance;bestCell=new Vector2Int(x,y);
            }
            if(bestDistance<int.MaxValue) extraHits.Add(bestCell);
            if(extraHits.Count>0)
            {
                var origin=new Vector2Int(created.gridX,created.gridY);
                usedSanctuarySeals.Add(origin);adaptiveView?.SetSanctuarySealUsed(origin);
            }
            return extraHits;
        }

        public bool IsSummitCrystalCell(int x,int y)
        {
            if(!IsPlayableCell(x,y) || config.obstacleType!=CellObstacleType.Ice || config.summitCrystalCells==null) return false;
            foreach(string value in config.summitCrystalCells)
            {
                var parts=value?.Split(',');
                if(parts==null || parts.Length!=2) continue;
                if(int.TryParse(parts[0],out int cellX) && int.TryParse(parts[1],out int cellY) && cellX==x && cellY==y) return true;
            }
            return false;
        }

        public bool IsSummitCrystalUsed(int x,int y) => usedSummitCrystals.Contains(new Vector2Int(x,y));

        // Called for resolution.CreatedSpecial only, never for a falling/activated special.
        public List<Vector2Int> TryCollectSummitCrystal(PieceView created,List<PieceView> removed)
        {
            var extraHits=new List<Vector2Int>();
            if(grid==null || obstacleHealth==null || created==null || !created.IsSpecial || removed==null ||
                GetPieceAt(created.gridX,created.gridY)!=created || !IsSummitCrystalCell(created.gridX,created.gridY) ||
                IsSummitCrystalUsed(created.gridX,created.gridY)) return extraHits;
            foreach(int dx in new[]{-1,1}) foreach(int dy in new[]{-1,1})
            {
                var cell=new Vector2Int(created.gridX+dx,created.gridY+dy);
                if(!IsPlayableCell(cell.x,cell.y) || obstacleHealth[cell.x,cell.y]<=0) continue;
                bool alreadyHit=false;
                foreach(var piece in removed)
                    if(piece!=null && piece.gridX==cell.x && piece.gridY==cell.y) {alreadyHit=true;break;}
                if(!alreadyHit) extraHits.Add(cell);
            }
            if(extraHits.Count>0)
            {
                var origin=new Vector2Int(created.gridX,created.gridY);
                usedSummitCrystals.Add(origin);adaptiveView?.SetSummitCrystalUsed(origin);
            }
            return extraHits;
        }

        public bool IsRubyGeyserCell(int x,int y)
        {
            if(!IsPlayableCell(x,y) || config.obstacleType!=CellObstacleType.Sand || config.rubyGeyserCells==null) return false;
            foreach(string value in config.rubyGeyserCells)
            {
                var parts=value?.Split(',');
                if(parts==null || parts.Length!=2) continue;
                if(int.TryParse(parts[0],out int cellX) && int.TryParse(parts[1],out int cellY) && cellX==x && cellY==y) return true;
            }
            return false;
        }

        public bool IsRubyGeyserUsed(int x,int y) => usedRubyGeysers.Contains(new Vector2Int(x,y));

        public List<Vector2Int> TryCollectRubyGeyser(IEnumerable<PieceView> activated,List<PieceView> removed)
        {
            var extraHits=new List<Vector2Int>();
            if(grid==null || obstacleHealth==null || activated==null || removed==null) return extraHits;
            foreach(var piece in activated)
            {
                if(piece==null || !piece.IsSpecial || !removed.Contains(piece) || GetPieceAt(piece.gridX,piece.gridY)!=piece ||
                    !IsRubyGeyserCell(piece.gridX,piece.gridY) || IsRubyGeyserUsed(piece.gridX,piece.gridY)) continue;
                foreach(int offset in new[]{-2,2})
                {
                    int y=piece.gridY+offset;
                    if(!IsPlayableCell(piece.gridX,y) || obstacleHealth[piece.gridX,y]<=0) continue;
                    bool alreadyHit=false;
                    foreach(var affected in removed)
                        if(affected!=null && Mathf.Abs(affected.gridX-piece.gridX)+Mathf.Abs(affected.gridY-y)<=1) {alreadyHit=true;break;}
                    if(!alreadyHit) extraHits.Add(new Vector2Int(piece.gridX,y));
                }
                if(extraHits.Count==0) continue;
                var cell=new Vector2Int(piece.gridX,piece.gridY);
                usedRubyGeysers.Add(cell);adaptiveView?.SetGeyserUsed(cell);
                return extraHits;
            }
            return extraHits;
        }

        public bool IsAuroraPrismCell(int x,int y)
        {
            if(!IsPlayableCell(x,y) || config.obstacleType!=CellObstacleType.Lantern || config.auroraPrismCells==null) return false;
            foreach(string value in config.auroraPrismCells)
            {
                var parts=value?.Split(',');
                if(parts==null || parts.Length!=2) continue;
                if(int.TryParse(parts[0],out int cellX) && int.TryParse(parts[1],out int cellY) && cellX==x && cellY==y) return true;
            }
            return false;
        }

        public bool IsAuroraPrismUsed(int x,int y) => usedAuroraPrisms.Contains(new Vector2Int(x,y));

        public List<Vector2Int> TryCollectAuroraPrism(IEnumerable<PieceView> matched,List<PieceView> removed)
        {
            var extraHits=new List<Vector2Int>();
            if(grid==null || obstacleHealth==null || matched==null || removed==null) return extraHits;
            foreach(var piece in matched)
            {
                if(piece==null || !removed.Contains(piece) || GetPieceAt(piece.gridX,piece.gridY)!=piece ||
                    !IsAuroraPrismCell(piece.gridX,piece.gridY) || IsAuroraPrismUsed(piece.gridX,piece.gridY)) continue;
                for(int y=0;y<Rows;y++)
                {
                    if(!IsPlayableCell(piece.gridX,y) || obstacleHealth[piece.gridX,y]<=0) continue;
                    bool alreadyHit=false;
                    foreach(var affected in removed)
                        if(affected!=null && affected.gridX==piece.gridX && affected.gridY==y) {alreadyHit=true;break;}
                    if(!alreadyHit) extraHits.Add(new Vector2Int(piece.gridX,y));
                }
                if(extraHits.Count==0) continue;
                var cell=new Vector2Int(piece.gridX,piece.gridY);
                usedAuroraPrisms.Add(cell);adaptiveView?.SetPrismUsed(cell);
                return extraHits;
            }
            return extraHits;
        }

        public bool IsMountainWarmCell(int x,int y)
        {
            if(!IsPlayableCell(x,y) || config.obstacleType!=CellObstacleType.Ice || config.mountainWarmCells==null) return false;
            foreach(string value in config.mountainWarmCells)
            {
                var parts=value?.Split(',');
                if(parts==null || parts.Length!=2) continue;
                if(int.TryParse(parts[0],out int cellX) && int.TryParse(parts[1],out int cellY) && cellX==x && cellY==y)
                    return true;
            }
            return false;
        }

        public bool IsMountainWarmthUsed(int x,int y) => usedMountainWarmth.Contains(new Vector2Int(x,y));

        public List<Vector2Int> TryCollectMountainWarmth(IEnumerable<PieceView> matched,List<PieceView> removed)
        {
            var extraHits=new List<Vector2Int>();
            if(grid==null || obstacleHealth==null || matched==null || removed==null) return extraHits;
            foreach(var piece in matched)
            {
                if(piece==null || !removed.Contains(piece) || GetPieceAt(piece.gridX,piece.gridY)!=piece ||
                    !IsMountainWarmCell(piece.gridX,piece.gridY) || IsMountainWarmthUsed(piece.gridX,piece.gridY)) continue;
                foreach(var direction in OrthogonalDirections)
                {
                    var target=new Vector2Int(piece.gridX,piece.gridY)+direction;
                    if(!IsPlayableCell(target.x,target.y) || obstacleHealth[target.x,target.y]<=0) continue;
                    bool alreadyHit=false;
                    foreach(var affected in removed)
                        if(affected!=null && affected.gridX==target.x && affected.gridY==target.y) {alreadyHit=true;break;}
                    if(!alreadyHit) extraHits.Add(target);
                }
                if(extraHits.Count==0) continue;
                var cell=new Vector2Int(piece.gridX,piece.gridY);
                usedMountainWarmth.Add(cell);adaptiveView?.SetWarmthUsed(cell);
                return extraHits;
            }
            return extraHits;
        }

        public List<Vector2Int> TryCollectCoastTide(IEnumerable<PieceView> matched,List<PieceView> removed)
        {
            var extraHits=new List<Vector2Int>();
            if(grid==null || obstacleHealth==null || matched==null || removed==null) return extraHits;
            foreach(var piece in matched)
            {
                if(piece==null || !removed.Contains(piece) || GetPieceAt(piece.gridX,piece.gridY)!=piece ||
                    !IsCoastTideCell(piece.gridX,piece.gridY) || IsCoastTideUsed(piece.gridX,piece.gridY)) continue;
                for(int x=0;x<Columns;x++)
                {
                    if(!IsPlayableCell(x,piece.gridY) || obstacleHealth[x,piece.gridY]<=0) continue;
                    bool alreadyHit=false;
                    foreach(var affected in removed)
                        if(affected!=null && Mathf.Abs(affected.gridX-x)+Mathf.Abs(affected.gridY-piece.gridY)<=1)
                        {alreadyHit=true;break;}
                    if(!alreadyHit) extraHits.Add(new Vector2Int(x,piece.gridY));
                }
                if(extraHits.Count==0) continue; // Save a tide that cannot reach any additional sand.
                var cell=new Vector2Int(piece.gridX,piece.gridY);
                usedCoastTides.Add(cell);adaptiveView?.SetTideUsed(cell);
                return extraHits;
            }
            return extraHits;
        }

        public bool TryGetUnrungFestivalBell(IEnumerable<PieceView> affected,out Vector2Int cell)
        {
            cell=default;
            if(grid==null || affected==null) return false;
            foreach(var piece in affected)
            {
                if(piece==null || GetPieceAt(piece.gridX,piece.gridY)!=piece ||
                    !IsFestivalBellCell(piece.gridX,piece.gridY) || IsFestivalBellRung(piece.gridX,piece.gridY)) continue;
                cell=new Vector2Int(piece.gridX,piece.gridY);return true;
            }
            return false;
        }

        public void RingFestivalBell(Vector2Int cell)
        {
            if(!IsFestivalBellCell(cell.x,cell.y) || !rungFestivalBells.Add(cell)) return;
            adaptiveView?.SetBellRung(cell);
        }

        public bool IsVineShelterCell(int x, int y)
        {
            if (!IsPlayableCell(x,y) || config.obstacleType != CellObstacleType.Vine || config.vineShelterCells == null)
                return false;
            foreach (string value in config.vineShelterCells)
            {
                var parts=value?.Split(',');
                if (parts == null || parts.Length != 2) continue;
                if (int.TryParse(parts[0],out int cellX) && int.TryParse(parts[1],out int cellY) && cellX==x && cellY==y)
                    return true;
            }
            return false;
        }

        public bool IsVineShelterCollected(int x,int y) => collectedShelterCells.Contains(new Vector2Int(x,y));

        public bool TryCollectVineShelter(IEnumerable<PieceView> matched, List<PieceView> removed)
        {
            if (matched == null || removed == null) return false;
            foreach (var piece in matched)
            {
                if (piece == null || !removed.Contains(piece) || GetPieceAt(piece.gridX,piece.gridY) != piece ||
                    !IsVineShelterCell(piece.gridX,piece.gridY)) continue;
                var cell=new Vector2Int(piece.gridX,piece.gridY);
                if (!collectedShelterCells.Add(cell)) continue;
                adaptiveView?.SetShelterCollected(cell);
                return true; // Only spend one leaf, even if a match reaches several.
            }
            return false;
        }

        // Called only for the newly created special, never for a falling or activated special.
        public bool TryHarvestCompanionGarden(PieceView created)
        {
            if (grid == null || created == null || !created.IsSpecial ||
                GetPieceAt(created.gridX, created.gridY) != created ||
                !IsCompanionGardenCell(created.gridX, created.gridY)) return false;
            var cell = new Vector2Int(created.gridX, created.gridY);
            if (!harvestedGardenCells.Add(cell)) return false;
            adaptiveView?.SetGardenHarvested(cell);
            return true;
        }

        public bool IsConverterCell(int x, int y)
        {
            if (!IsPlayableCell(x, y) || config.converterCells == null) return false;
            foreach (string value in config.converterCells)
            {
                string[] parts = value == null ? null : value.Split(',');
                if (parts == null || parts.Length != 2) continue;
                if (int.TryParse(parts[0], out int cellX) && int.TryParse(parts[1], out int cellY) &&
                    cellX == x && cellY == y) return true;
            }
            return false;
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
            var activeTypes=config.GetActivePieceTypes();

            for (int x = 0; x < config.columns; x++)
            {
                for (int y = 0; y < config.rows; y++)
                {
                    if (!IsPlayableCell(x, y)) continue;
                    PieceType type=PieceType.None;
                    var opening=config.initialPieceRows;
                    int sourceRow=Rows-1-y;
                    if(opening!=null && opening.Length==Rows && opening[sourceRow]!=null && opening[sourceRow].Length==Columns)
                    {
                        int authored=opening[sourceRow][x]-'0';
                        if(authored>=0 && authored<=8 && System.Array.IndexOf(activeTypes,(PieceType)authored)>=0)
                            type=(PieceType)authored;
                    }
                    if(type==PieceType.None) type = config.GetRandomActivePieceType();
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
            return TryFindHintMove(out _, out _);
        }

        public bool TryFindHintMove(out PieceView first, out PieceView second)
        {
            return TryFindHintMoveForObjective(PieceType.None, false, out first, out second);
        }

        public bool TryFindHintMoveForObjective(PieceType preferredType, bool prioritizeObstacles,
            out PieceView first, out PieceView second)
        {
            first = null;
            second = null;
            if (grid == null || config == null || config.columns < 1 || config.rows < 1) return false;

            int cellCount = config.columns * config.rows;
            int scanOffset = Random.Range(0, cellCount);
            int bestScore = int.MinValue;

            for (int step = 0; step < cellCount; step++)
            {
                int index = (scanOffset + step) % cellCount;
                int x = index % config.columns;
                int y = index / config.columns;

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
                        first = current;
                        second = other;
                        return true;
                    }
                    grid[x, y] = other;
                    grid[nx, ny] = current;
                    current.SetGridPosition(nx, ny);
                    other.SetGridPosition(x, y);
                    List<PieceView> matches = FindMatches();
                    if (matches.Count >= 3 && (matches.Contains(current) || matches.Contains(other)))
                    {
                        int score = matches.Count * 5;
                        if (preferredType != PieceType.None)
                        {
                            foreach (PieceView match in matches)
                                if (match != null && match.type == preferredType) score += 18;
                        }
                        if (prioritizeObstacles)
                        {
                            foreach (PieceView match in matches)
                                if (match != null && HasObstacleAtOrNear(match.gridX, match.gridY)) score += 24;
                        }
                        if (score > bestScore)
                        {
                            bestScore = score;
                            first = current;
                            second = other;
                        }
                    }
                    grid[x, y] = current;
                    grid[nx, ny] = other;
                    current.SetGridPosition(x, y);
                    other.SetGridPosition(nx, ny);
                }
            }
            return first != null && second != null;
        }

        private bool HasObstacleAtOrNear(int x, int y)
        {
            if (obstacleHealth == null) return false;
            if (IsValidGridPos(x, y) && obstacleHealth[x, y] > 0) return true;
            foreach (Vector2Int direction in OrthogonalDirections)
            {
                int nx = x + direction.x;
                int ny = y + direction.y;
                if (IsValidGridPos(nx, ny) && obstacleHealth[nx, ny] > 0) return true;
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
            for (int x = 0; x < Columns; x++)
            {
                for (int y = 0; y < Rows; y++)
                {
                    if (grid[x, y] != null) continue;
                    if (!IsPlayableCell(x, y)) continue;
                    PieceType type = config.GetRandomActivePieceType();
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
                PieceSpecialType.BallBounce => 7,
                PieceSpecialType.MegaBurst => 6,
                PieceSpecialType.Whistle => 5,
                PieceSpecialType.Comet => 3,
                PieceSpecialType.ColorBurst => 4,
                PieceSpecialType.AreaBlast => 3,
                PieceSpecialType.RowBlast => 2,
                PieceSpecialType.ColumnBlast => 2,
                _ => 0
            };
        }

        public bool TrySwapAndFindMatches(PieceView first, PieceView second, out List<PieceView> matches)
        {
            ClearSpecialPreview();
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
            List<Vector2Int> manualCells = new List<Vector2Int>();
            if (config.obstacleCells != null)
            {
                foreach (string value in config.obstacleCells)
                {
                    string[] parts = value == null ? null : value.Split(',');
                    if (parts == null || parts.Length != 2) continue;
                    if (int.TryParse(parts[0], out int x) && int.TryParse(parts[1], out int y) && IsPlayableCell(x, y))
                        manualCells.Add(new Vector2Int(x, y));
                }
            }
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

            List<Vector2Int> selected = new List<Vector2Int>();
            foreach (Vector2Int cell in manualCells)
                if (!selected.Contains(cell)) selected.Add(cell);
            int count = Mathf.Min(config.obstacleCount, candidates.Count);
            for (int i = 0; selected.Count < count && i < candidates.Count; i++)
                if (!selected.Contains(candidates[i])) selected.Add(candidates[i]);
            count = selected.Count;
            int durability = Mathf.Clamp(config.obstacleDurability, 1, 3);
            RemainingObstacleCount = count;
            initialObstacleCount = count;
            for (int i = 0; i < count; i++)
            {
                Vector2Int cell = selected[i];
                obstacleHealth[cell.x, cell.y] = durability;
                obstacleRenderers[cell.x, cell.y] = CreateObstacleVisual(cell, durability);
            }
        }

        private SpriteRenderer CreateObstacleVisual(Vector2Int cell, int durability)
        {
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
            renderer.color = ObstacleColor(config.obstacleType, durability,
                Mathf.Clamp(config.obstacleDurability, 1, 3));
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
            UpdateObstacleLayerMeter(renderer,durability);
            return renderer;
        }

        private void UpdateObstacleLayerMeter(SpriteRenderer obstacle,int health)
        {
            int maximum=Mathf.Clamp(config.obstacleDurability,1,3);
            if(obstacle==null || maximum<=1) return;
            var meter=obstacle.transform.Find("LayerMeter");
            if(meter==null)
            {
                var root=new GameObject("LayerMeter");root.transform.SetParent(obstacle.transform,false);
                meter=root.transform;
                float scale=obstacle.transform.localScale.x/ActivePieceSpacing;
                meter.localPosition=new Vector3(.22f/scale,-.36f/scale,-.36f);
                meter.localScale=Vector3.one/scale;
                // Separate child backdrop keeps dot placement in cell units.
                var plate=new GameObject("LayerPlate");plate.transform.SetParent(meter,false);
                plate.transform.localScale=new Vector3(maximum*.11f+.07f,.16f,1f);
                var plateRenderer=plate.AddComponent<SpriteRenderer>();plateRenderer.sprite=GetObstacleLayerDot();
                plateRenderer.color=new Color(.07f,.09f,.13f,.95f);plateRenderer.sortingOrder=21;
                for(int i=0;i<maximum;i++)
                {
                    var dot=new GameObject("Layer_"+i);dot.transform.SetParent(meter,false);
                    dot.transform.localPosition=new Vector3((i-(maximum-1)*.5f)*.11f,0f,-.01f);
                    dot.transform.localScale=Vector3.one*.085f;
                    var marker=dot.AddComponent<SpriteRenderer>();marker.sprite=GetObstacleLayerDot();marker.sortingOrder=22;
                }
            }
            for(int i=0;i<maximum;i++)
                meter.Find("Layer_"+i).GetComponent<SpriteRenderer>().color=i<health?Color.white:new Color(.3f,.35f,.4f);
        }

        private static Sprite GetObstacleLayerDot()
        {
            if(obstacleLayerDot!=null) return obstacleLayerDot;
            const int size=32;var texture=new Texture2D(size,size,TextureFormat.RGBA32,false);
            texture.name="ObstacleLayerDot";texture.filterMode=FilterMode.Bilinear;
            var pixels=new Color[size*size];
            for(int y=0;y<size;y++) for(int x=0;x<size;x++)
            {
                float radius=new Vector2((x+.5f)/size*2f-1f,(y+.5f)/size*2f-1f).magnitude;
                pixels[y*size+x]=new Color(1f,1f,1f,Mathf.Clamp01((1f-radius)*size*.5f));
            }
            texture.SetPixels(pixels);texture.Apply();
            obstacleLayerDot=Sprite.Create(texture,new Rect(0,0,size,size),new Vector2(.5f,.5f),size);
            return obstacleLayerDot;
        }

        public bool TrySpreadVines()
        {
            if (config == null || config.obstacleType != CellObstacleType.Vine ||
                obstacleHealth == null || obstacleRenderers == null || obstacleRoot == null ||
                RemainingObstacleCount <= 0 || RemainingObstacleCount >= initialObstacleCount + 4)
                return false;

            List<Vector2Int> candidates = new List<Vector2Int>();
            for (int x = 0; x < Columns; x++)
                for (int y = 0; y < Rows; y++)
                {
                    if (obstacleHealth[x, y] <= 0) continue;
                    foreach (Vector2Int direction in OrthogonalDirections)
                    {
                        Vector2Int cell = new Vector2Int(x + direction.x, y + direction.y);
                        if (!IsPlayableCell(cell.x, cell.y) || obstacleHealth[cell.x, cell.y] > 0 ||
                            candidates.Contains(cell)) continue;
                        candidates.Add(cell);
                    }
                }
            if (candidates.Count == 0) return false;
            Vector2Int selected = candidates[(RemainingObstacleCount * 7) % candidates.Count];
            obstacleHealth[selected.x, selected.y] = 1;
            obstacleRenderers[selected.x, selected.y] = CreateObstacleVisual(selected, 1);
            RemainingObstacleCount++;
            return true;
        }

        public int DamageObstacles(IReadOnlyList<PieceView> affectedPieces, bool specialImpact = false,
            IReadOnlyList<Vector2Int> extraHits = null)
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

            if(extraHits!=null) foreach(var cell in extraHits) hitCells.Add(cell);
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
                    UpdateObstacleLayerMeter(renderer,obstacleHealth[cell.x,cell.y]);
                    renderer.color = ObstacleColor(config.obstacleType, obstacleHealth[cell.x, cell.y], maximum);
                    StartCoroutine(AnimateObstacleDamage(renderer, config.obstacleType,
                        obstacleHealth[cell.x, cell.y], maximum));
                }
            }
            return cleared;
        }

        private IEnumerator AnimateObstacleDamage(
            SpriteRenderer renderer,
            CellObstacleType type,
            int health,
            int maximum)
        {
            if (renderer == null) yield break;
            Transform target = renderer.transform;
            Vector3 baseScale = target.localScale;
            Color settledColor = ObstacleColor(type, health, maximum);
            const float duration = 0.22f;
            float elapsed = 0f;
            while (elapsed < duration && renderer != null && !AccessibilitySettings.ReducedMotion)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float impact = Mathf.Sin(t * Mathf.PI);
                target.localScale = baseScale * (1f - impact * 0.18f);
                target.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(t * Mathf.PI * 4f) * 5f * impact);
                renderer.color = Color.Lerp(settledColor, Color.white, impact * 0.72f);
                yield return null;
            }
            if (renderer == null) yield break;
            target.localScale = baseScale;
            target.localRotation = Quaternion.identity;
            renderer.color = settledColor;
        }

        private void ClearObstacles()
        {
            if (obstacleRoot != null) Destroy(obstacleRoot.gameObject);
            obstacleRoot = null;
            obstacleHealth = null;
            obstacleRenderers = null;
            RemainingObstacleCount = 0;
            initialObstacleCount = 0;
        }

        private static Color ObstacleColor(CellObstacleType type, int health, int maximum)
        {
            float strength = Mathf.Clamp01(health / (float)Mathf.Max(1, maximum));
            if (AccessibilitySettings.HighContrastObstacles)
            {
                switch (type)
                {
                    case CellObstacleType.Vine: return new Color(0.05f, 0.78f, 0.12f, 1f);
                    case CellObstacleType.Lantern: return new Color(1f, 0.72f, 0.02f, 1f);
                    case CellObstacleType.Sand: return new Color(1f, 0.40f, 0.05f, 1f);
                case CellObstacleType.Ice: return new Color(0.12f, 0.82f, 1f, 1f);
                    case CellObstacleType.PuppyCage: return new Color(1f, 0.55f, 0.22f, 1f);
                }
            }
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
                case CellObstacleType.PuppyCage:
                    return new Color(1f, 0.42f + strength * 0.28f, 0.18f, 0.72f + strength * 0.20f);
                default:
                    return Color.clear;
            }
        }

        public void RefreshObstacleContrast()
        {
            if (obstacleHealth == null || obstacleRenderers == null || config == null) return;
            int maximum = Mathf.Clamp(config.obstacleDurability, 1, 3);
            for (int x = 0; x < Columns; x++)
                for (int y = 0; y < Rows; y++)
                    if (obstacleHealth[x, y] > 0 && obstacleRenderers[x, y] != null)
                        obstacleRenderers[x, y].color = ObstacleColor(
                            config.obstacleType, obstacleHealth[x, y], maximum);
        }

        private static Sprite GetObstacleSprite(CellObstacleType type)
        {
            if (type == CellObstacleType.Vine && vineObstacleSprite != null) return vineObstacleSprite;
            if (type == CellObstacleType.Lantern && lanternObstacleSprite != null) return lanternObstacleSprite;
            if (type == CellObstacleType.Sand && sandObstacleSprite != null) return sandObstacleSprite;
            if (type == CellObstacleType.Ice && iceObstacleSprite != null) return iceObstacleSprite;
            if (type == CellObstacleType.PuppyCage && puppyCageObstacleSprite != null) return puppyCageObstacleSprite;
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
                    else if (type == CellObstacleType.PuppyCage)
                    {
                        // Jaula cálida y reconocible: hocico central, dos ojos
                        // y barrotes exteriores para que el rescate se entienda.
                        float face = Mathf.Clamp01(1f - (nx * nx * 1.6f + (ny + 0.04f) * (ny + 0.04f) * 1.8f) / 0.42f);
                        float eyeA = Mathf.Clamp01(1f - (((nx - 0.27f) * (nx - 0.27f)) + (ny - 0.22f) * (ny - 0.22f)) / 0.025f);
                        float eyeB = Mathf.Clamp01(1f - (((nx + 0.27f) * (nx + 0.27f)) + (ny - 0.22f) * (ny - 0.22f)) / 0.025f);
                        float frame = Mathf.Max(Mathf.Abs(nx), Mathf.Abs(ny));
                        float bars = Mathf.Pow(Mathf.Abs(Mathf.Sin((nx + 1f) * 7f)), 18f) * Mathf.Clamp01(frame - 0.35f);
                        float rim = Mathf.Clamp01(1f - Mathf.Abs(frame - 0.78f) / 0.055f);
                        alpha = Mathf.Clamp01(face * 0.72f + eyeA * 0.95f + eyeB * 0.95f + bars * 0.85f + rim * 0.75f);
                    }
                    else
                    {
                        // Frosted rim with thin cracks instead of an opaque ice
                        // plate covering the underlying piece.
                        float square = Mathf.Max(Mathf.Abs(nx), Mathf.Abs(ny));
                        float rim = Mathf.Clamp01(1f - Mathf.Abs(square - 0.78f) / 0.035f);
                        float cracks = Mathf.Pow(Mathf.Abs(Mathf.Sin(angle * 3f + radius * 9f)), 24f) *
                            Mathf.Clamp01((0.72f - radius) * 2.2f);
                        float cornerFrost = Mathf.Pow(Mathf.Clamp01((square - 0.48f) / 0.32f), 2f);
                        alpha = Mathf.Clamp01(rim * 0.32f + cracks * 0.65f + cornerFrost * 0.14f);
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
            else if (type == CellObstacleType.Ice) iceObstacleSprite = sprite;
            else puppyCageObstacleSprite = sprite;
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
                result.ComboAnchor = lastSwapFirst.SpecialType == PieceSpecialType.AreaBlast ? lastSwapFirst : lastSwapSecond;
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
                else if (special.SpecialType == PieceSpecialType.BallBounce)
                {
                    // The ball jumps to a handful of distant tiles. It gives
                    // every activation a different, readable route.
                    int bounces = Mathf.Clamp(4 + (Columns * Rows) / 36, 5, 8);
                    var destinations = new List<Vector3>();
                    result.BallBounceDestinations[special] = destinations;
                    for (int bounce = 0; bounce < bounces; bounce++)
                    {
                        PieceView target = GetRandomPiece();
                        if (target == null) break;
                        destinations.Add(GridToWorldPosition(target.gridX, target.gridY));
                        AddPiece(target);
                        for (int x = target.gridX - 1; x <= target.gridX + 1; x++)
                            for (int y = target.gridY - 1; y <= target.gridY + 1; y++)
                                AddPiece(GetPieceAt(x, y));
                    }
                }
                else if (special.SpecialType == PieceSpecialType.Comet)
                {
                    // Traverse every diagonal cell, including irregular-board gaps.
                    for (int step = -Mathf.Max(Columns, Rows); step <= Mathf.Max(Columns, Rows); step++)
                    {
                        AddPiece(GetPieceAt(special.gridX + step, special.gridY + step));
                        AddPiece(GetPieceAt(special.gridX + step, special.gridY - step));
                    }
                }
                else if (special.SpecialType == PieceSpecialType.Whistle)
                {
                    // A whistle gathers one type in the centre lane before
                    // it pops: visually and mechanically unlike a ray.
                    PieceType targetType = special.type;
                    int centerRow = Rows / 2;
                    foreach (PieceView piece in GetRowPieces(centerRow)) AddPiece(piece);
                    for (int x = 0; x < Columns; x++)
                        for (int y = 0; y < Rows; y++)
                        {
                            PieceView piece = GetPieceAt(x, y);
                            if (piece != null && piece.type == targetType) AddPiece(piece);
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
            return SpecialComboKind.CombinedPowers;
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
            return ClassifySpecialForPiece(piece.type, horizontal, vertical);
        }

        public static PieceSpecialType ClassifySpecialForPiece(PieceType type, int horizontal, int vertical)
        {
            PieceSpecialType ordinary = ClassifySpecialForRuns(horizontal, vertical);
            if (type == PieceType.Frisbee &&
                (ordinary == PieceSpecialType.RowBlast || ordinary == PieceSpecialType.ColumnBlast))
                return PieceSpecialType.Comet;
            return ordinary;
        }

        public static PieceSpecialType ClassifySpecialForRuns(int horizontal, int vertical)
        {
            horizontal = Mathf.Max(1, horizontal);
            vertical = Mathf.Max(1, vertical);
            if (horizontal >= 7 || vertical >= 7)
                return PieceSpecialType.BallBounce;
            if (horizontal >= 6 || vertical >= 6)
                return PieceSpecialType.MegaBurst;
            if ((horizontal >= 5 && vertical >= 3) || (vertical >= 5 && horizontal >= 3))
                return PieceSpecialType.Whistle;
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
            ClearSpecialPreview();
            if(TryGetSpecialCreationPreview(first,second,out var cell,out _)) adaptiveView?.ShowCreationPreview(cell);
            first.MoveToWorldPosition(GridToWorldPosition(second.gridX, second.gridY), 14f);
            second.MoveToWorldPosition(GridToWorldPosition(first.gridX, first.gridY), 14f);
        }

        public void RestorePreviewSwap(PieceView first, PieceView second)
        {
            ClearSpecialPreview();
            if (first == null || second == null) return;
            first.MoveToWorldPosition(GridToWorldPosition(first.gridX, first.gridY), 14f);
            second.MoveToWorldPosition(GridToWorldPosition(second.gridX, second.gridY), 14f);
        }

        // Reads a virtual swap; never changes the grid, coordinates, RNG or last swap.
        public bool TryGetSpecialCreationPreview(PieceView first,PieceView second,out Vector2Int cell,out PieceSpecialType kind)
        {
            cell=default;kind=PieceSpecialType.None;
            if(first==null || second==null || first.IsSpecial || second.IsSpecial ||
                GetPieceAt(first.gridX,first.gridY)!=first || GetPieceAt(second.gridX,second.gridY)!=second ||
                !AreAdjacent(first.gridX,first.gridY,second.gridX,second.gridY)) return false;
            var a=new Vector2Int(first.gridX,first.gridY);var b=new Vector2Int(second.gridX,second.gridY);
            PieceView At(Vector2Int p) => p==a?second:p==b?first:GetPieceAt(p.x,p.y);
            int Run(Vector2Int p,Vector2Int direction)
            {
                var origin=At(p);if(origin==null) return 0;
                int count=0;var next=p+direction;
                while(IsValidGridPos(next.x,next.y) && At(next)!=null && At(next).type==origin.type)
                {count++;next+=direction;}
                return count;
            }
            PieceSpecialType Classify(Vector2Int p) => ClassifySpecialForPiece(At(p).type,
                1+Run(p,Vector2Int.left)+Run(p,Vector2Int.right),1+Run(p,Vector2Int.down)+Run(p,Vector2Int.up));
            // A matched existing special activates instead of creating a new one.
            for(int x=0;x<Columns;x++) for(int y=0;y<Rows;y++)
            {
                var p=new Vector2Int(x,y);var piece=At(p);
                if(piece!=null && piece.IsSpecial &&
                    (1+Run(p,Vector2Int.left)+Run(p,Vector2Int.right)>=3 ||
                     1+Run(p,Vector2Int.down)+Run(p,Vector2Int.up)>=3)) return false;
            }
            kind=Classify(b);cell=b;
            if(kind==PieceSpecialType.None) {kind=Classify(a);cell=a;}
            return kind!=PieceSpecialType.None;
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
