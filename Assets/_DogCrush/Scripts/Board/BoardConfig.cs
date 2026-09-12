using UnityEngine;

namespace DogCrush.Board
{
    [CreateAssetMenu(fileName = "BoardConfig", menuName = "DOGCRUSH/BoardConfig")]
    public class BoardConfig : ScriptableObject
    {
        [Header("Grid Dimensions")]
        [Range(3, 12)] public int columns = 8;
        [Range(3, 12)] public int rows = 8;
        [Range(3, 9)] public int typeCount = 5;
        [Tooltip("Optional thematic pool. When filled, the first typeCount entries are used for new pieces.")]
        public PieceType[] activePieceTypes;
        [Tooltip("Optional manual mask: '.' playable, '#' blocked.")]
        public string[] layoutRows;
        public DogCrush.Core.BoardShape boardShape = DogCrush.Core.BoardShape.Full;
        public DogCrush.Core.BoardTheme boardTheme = DogCrush.Core.BoardTheme.Meadow;

        [Header("World Rules")]
        public CellObstacleType obstacleType = CellObstacleType.None;
        [Range(0, 40)] public int obstacleCount;
        [Range(1, 3)] public int obstacleDurability = 1;
        public string[] obstacleCells;
        [Tooltip("Cells that transform a non-special piece when it lands, formatted as x,y.")]
        public string[] converterCells;

        [Header("Piece Settings")]
        public float pieceSpacing = 0.55f;
        public float fallSpeed = 12.0f;
        public float bounceHeight = 0.2f;
        public float selectionScale = 1.18f;

        [Header("Gameplay Rules")]
        public int minChainLength = 3;
        public float gameDurationSeconds = 60.0f;
        public float streakTimeoutSeconds = 4.0f;

        [Header("Scoring")]
        public int baseScorePerPiece = 100;
        public int bonus4Piece = 200;
        public int bonus5Piece = 400;
        public int bonus6PlusPiece = 800;

        public PieceType[] GetActivePieceTypes()
        {
            int count = Mathf.Clamp(typeCount, 1, (int)PieceType.Rope + 1);
            if (activePieceTypes != null && activePieceTypes.Length >= count)
            {
                var result = new PieceType[count];
                for (int i = 0; i < count; i++) result[i] = activePieceTypes[i];
                return result;
            }
            var fallback = new PieceType[count];
            for (int i = 0; i < count; i++) fallback[i] = (PieceType)i;
            return fallback;
        }

        public PieceType GetRandomActivePieceType()
        {
            PieceType[] pool = GetActivePieceTypes();
            return pool[Random.Range(0, pool.Length)];
        }
    }
}
