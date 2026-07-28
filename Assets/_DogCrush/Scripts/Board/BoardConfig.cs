using UnityEngine;

namespace DogCrush.Board
{
    [CreateAssetMenu(fileName = "BoardConfig", menuName = "DOGCRUSH/BoardConfig")]
    public class BoardConfig : ScriptableObject
    {
        [Header("Grid Dimensions")]
        [Range(3, 12)] public int columns = 8;
        [Range(3, 12)] public int rows = 8;
        [Range(3, 6)] public int typeCount = 5;
        public DogCrush.Core.BoardShape boardShape = DogCrush.Core.BoardShape.Full;

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
    }
}
