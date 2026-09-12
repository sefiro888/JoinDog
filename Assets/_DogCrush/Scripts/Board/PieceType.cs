namespace DogCrush.Board
{
    public enum PieceType
    {
        None = -1,
        Dog = 0,
        Bone = 1,
        Ball = 2,
        Food = 3,
        Collar = 4,
        Duck = 5,
        Rope = 6,
        Frisbee = 7,
        Penguin = 8
    }

    public enum PieceSpecialType
    {
        None = 0,
        RowBlast = 1,
        ColumnBlast = 2,
        AreaBlast = 3,
        ColorBurst = 4,
        MegaBurst = 5,
        BallBounce = 6,
        Whistle = 7
    }

    public enum SpecialComboKind
    {
        None = 0,
        DoubleRow = 1,
        DoubleColumn = 2,
        CrossBlast = 3,
        WideRow = 4,
        WideColumn = 5,
        DoubleArea = 6,
        ColorSweep = 7,
        BoardNova = 8
    }

    public enum CellObstacleType
    {
        None = 0,
        Vine = 1,
        Lantern = 2,
        Sand = 3,
        Ice = 4,
        PuppyCage = 5
    }
}
