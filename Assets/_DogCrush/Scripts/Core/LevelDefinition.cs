using System;
using DogCrush.Board;

namespace DogCrush.Core
{
    public enum LevelObjectiveType
    {
        Score,
        CollectPieces,
        LongChain
    }

    public enum BoardShape
    {
        Full,
        Diamond
    }

    /// <summary>
    /// Data for one playable level. Keeping this separate from the game loop
    /// lets new levels change dimensions, pacing and goals without code edits.
    /// </summary>
    [Serializable]
    public class LevelDefinition
    {
        public int level = 1;
        public int rows = 8;
        public int columns = 8;
        public float durationSeconds = 60f;
        public int targetScore = 5000;
        public int typeCount = 5;
        public int minChainLength = 3;
        public LevelObjectiveType objectiveType = LevelObjectiveType.Score;
        public PieceType targetPieceType = PieceType.Dog;
        public int targetAmount = 5000;
        public BoardShape boardShape = BoardShape.Full;
        public int pawBoosterCount = 1;
        public int boneBoosterCount = 1;
        public int foodBoosterCount = 1;
    }
}
