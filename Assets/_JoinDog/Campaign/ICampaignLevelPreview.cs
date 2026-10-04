namespace JoinDog.App
{
    /// <summary>Lets the map describe authored gameplay without an assembly dependency cycle.</summary>
    public interface ICampaignLevelPreview
    {
        CampaignObjectiveKind ObjectiveKind { get; }
        int TargetPiece { get; }
        CampaignObstacleKind ObstacleKind { get; }
        int PreviewObstacleDurability { get; }
        float PreviewDurationSeconds { get; }
        string BuildObjectivePreview(CampaignLevelEntry entry);
    }
}
