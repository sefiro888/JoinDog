using UnityEngine;
using DogCrush.Board;
using JoinDog.App;

namespace DogCrush.Core
{
    /// <summary>Hand-authored level data; missing assets use the campaign fallback.</summary>
    [CreateAssetMenu(menuName = "JoinDog/Level Design", fileName = "LevelDesign")]
    public sealed class LevelDesignAsset : ScriptableObject, ICampaignLevelPreview
    {
        public int level = 1;
        public string title = "PRIMERAS HUELLAS";
        [TextArea(2, 12)] public string[] layoutRows =
        {
            "........", "........", "........", "........",
            "........", "........", "........", "........"
        };
        [Header("Board")]
        [TextArea(2,12)] public string[] initialPieceRows;
        [Tooltip("Flower cells (x,y): first special created here grants one extra companion charge per match.")]
        public string[] companionGardenCells;
        [Tooltip("Leaf cells (x,y): clear one to stop vine growth for this turn, once per leaf per match.")]
        public string[] vineShelterCells;
        [Tooltip("Bell cells (x,y): reach one with an activated special for +2 seconds, once per bell per match.")]
        public string[] festivalBellCells;
        public string[] coastTideCells;
        public string[] mountainWarmCells;
        public string[] auroraPrismCells;
        public string[] summitCrystalCells;
        public string[] celestialSproutCells;
        public string[] rubyGeyserCells;
        public string[] sanctuarySealCells;
        [TextArea(2,4)] public string openingStrategyTip;
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
        [Tooltip("Optional sequential collection: primary type first, then secondary. Zero preserves the shared counter.")]
        public int firstCollectionPhaseAmount;
        public int minChainLength = 3;
        public CellObstacleType obstacleType = CellObstacleType.None;
        public int obstacleCount;
        public int obstacleDurability = 1;
        public string[] obstacleCells;
        public string[] converterCells;
        public int secondaryTargetScore;
        public int pawBoosterCount = 1;
        public int boneBoosterCount = 1;
        public int foodBoosterCount = 1;

        public CampaignObjectiveKind ObjectiveKind => objectiveType == LevelObjectiveType.DeliverToy
            ? CampaignObjectiveKind.CollectTwoTypes : (CampaignObjectiveKind)(int)objectiveType;
        public int TargetPiece => (int)targetPieceType;
        public CampaignObstacleKind ObstacleKind => objectiveType == LevelObjectiveType.RescuePuppies
            ? CampaignObstacleKind.PuppyCage : (CampaignObstacleKind)(int)obstacleType;
        public int PreviewObstacleDurability => Mathf.Clamp(obstacleDurability, 1, 3);
        public float PreviewDurationSeconds => Mathf.Max(15f, durationSeconds);

        public string BuildObjectivePreview(CampaignLevelEntry entry)
        {
            var definition = new LevelDefinition
            {
                secondaryTargetPieceType = (PieceType)Mathf.Clamp((int)entry.secondaryTargetPiece, 0, 8)
            };
            ApplyTo(definition);
            if (definition.objectiveType == LevelObjectiveType.DeliverToy)
                definition.objectiveType = LevelObjectiveType.CollectTwoTypes;
            return GameBootstrap.BuildObjectiveIntroText(definition);
        }

        public void ApplyTo(LevelDefinition definition)
        {
            if (definition == null) return;
            definition.level = level;
            definition.rows = Mathf.Max(3, rows);
            definition.columns = Mathf.Max(3, columns);
            definition.layoutRows = layoutRows;
            definition.initialPieceRows = initialPieceRows;
            definition.companionGardenCells = companionGardenCells;
            definition.vineShelterCells = vineShelterCells;
            definition.festivalBellCells = festivalBellCells;
            definition.coastTideCells = coastTideCells;
            definition.mountainWarmCells = mountainWarmCells;
            definition.auroraPrismCells = auroraPrismCells;
            definition.summitCrystalCells = summitCrystalCells;
            definition.celestialSproutCells = celestialSproutCells;
            definition.rubyGeyserCells = rubyGeyserCells;
            definition.sanctuarySealCells = sanctuarySealCells;
            definition.openingStrategyTip = openingStrategyTip;
            definition.typeCount = Mathf.Clamp(typeCount, 1, 9);
            definition.boardShape = boardShape;
            definition.boardTheme = boardTheme;
            definition.durationSeconds = Mathf.Max(15f, durationSeconds);
            definition.targetScore = Mathf.Max(100, targetScore);
            definition.objectiveType = objectiveType;
            definition.targetPieceType = targetPieceType;
            if ((objectiveType == LevelObjectiveType.CollectTwoTypes || objectiveType == LevelObjectiveType.DeliverToy) &&
                definition.secondaryTargetPieceType == targetPieceType)
                definition.secondaryTargetPieceType = targetPieceType == PieceType.Bone ? PieceType.Dog : PieceType.Bone;
            definition.targetAmount = Mathf.Max(1, targetAmount);
            definition.firstCollectionPhaseAmount = firstCollectionPhaseAmount;
            definition.minChainLength = Mathf.Clamp(minChainLength, 3, 5);
            definition.obstacleType = obstacleType;
            definition.obstacleCount = Mathf.Max(0, obstacleCount);
            definition.obstacleDurability = Mathf.Clamp(obstacleDurability, 1, 3);
            definition.obstacleCells = obstacleCells;
            definition.converterCells = converterCells;
            definition.secondaryTargetScore = Mathf.Max(0, secondaryTargetScore);
            definition.pawBoosterCount = Mathf.Max(0, pawBoosterCount);
            definition.boneBoosterCount = Mathf.Max(0, boneBoosterCount);
            definition.foodBoosterCount = Mathf.Max(0, foodBoosterCount);
        }
    }
}
