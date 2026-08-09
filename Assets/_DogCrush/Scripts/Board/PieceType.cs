namespace DogCrush.Board
{
    public enum PieceType
    {
        None = -1,
        Dog = 0,
        Bone = 1,
        Ball = 2,
        Food = 3,
        Collar = 4
    }

    public enum PieceSpecialType
    {
        None = 0,
        RowBlast = 1,
        ColumnBlast = 2,
        AreaBlast = 3,
        ColorBurst = 4,
        MegaBurst = 5
    }

    public enum CellObstacleType
    {
        None = 0,
        Vine = 1,
        Lantern = 2,
        Sand = 3,
        Ice = 4
    }
}
