using UnityEngine;
using DogCrush.Board;

namespace DogCrush.Core
{
    /// <summary>Hand-authored level data; missing assets use the campaign fallback.</summary>
    [CreateAssetMenu(menuName = "JoinDog/Level Design", fileName = "LevelDesign")]
    public sealed class LevelDesignAsset : ScriptableObject
    {
        public int level = 1;
        public string title = "PRIMERAS HUELLAS";
        [TextArea(2, 12)] public string[] layoutRows =
        {
            "........", "........", "........", "........",
            "........", "........", "........", "........"
        };
        [Header("Board")]
        [Min(3)] public int rows = 8;
        [Min(3)] public int columns = 8;
        public int typeCount = 5;
        public BoardShape boardShape = BoardShape.Full;
        public BoardTheme boardTheme = BoardTheme.Meadow;
        [Header("Rules")]
        public float durationSeconds = 61.2f;
        public int targetScore = 14400;
        public LevelObjectiveType objectiveType = LevelObjectiveType.Score;
        public PieceType targetPieceType = PieceType.Dog;
        public int targetAmount = 15;
        public int minChainLength = 3;
        public CellObstacleType obstacleType = CellObstacleType.None;
        public int obstacleCount;
        public int obstacleDurability = 1;
        public int pawBoosterCount = 1;
        public int boneBoosterCount = 1;
        public int foodBoosterCount = 1;

        public void ApplyTo(LevelDefinition definition)
        {
            if (definition == null) return;
            definition.level = level;
            definition.rows = Mathf.Max(3, rows);
            definition.columns = Mathf.Max(3, columns);
            definition.typeCount = Mathf.Clamp(typeCount, 1, 5);
            definition.boardShape = boardShape;
            definition.boardTheme = boardTheme;
            definition.durationSeconds = Mathf.Max(15f, durationSeconds);
            definition.targetScore = Mathf.Max(100, targetScore);
            definition.objectiveType = objectiveType;
            definition.targetPieceType = targetPieceType;
            definition.targetAmount = Mathf.Max(1, targetAmount);
            definition.minChainLength = Mathf.Clamp(minChainLength, 3, 5);
            definition.obstacleType = obstacleType;
            definition.obstacleCount = Mathf.Max(0, obstacleCount);
            definition.obstacleDurability = Mathf.Clamp(obstacleDurability, 1, 3);
            definition.pawBoosterCount = Mathf.Max(0, pawBoosterCount);
            definition.boneBoosterCount = Mathf.Max(0, boneBoosterCount);
            definition.foodBoosterCount = Mathf.Max(0, foodBoosterCount);
        }
    }
}
