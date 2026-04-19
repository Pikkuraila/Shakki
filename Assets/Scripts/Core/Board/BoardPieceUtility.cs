namespace Shakki.Core
{
    public static class BoardPieceUtility
    {
        public static bool IsBlockingObstacle(Piece piece)
        {
            return piece != null && piece.HasTag(PieceTag.Obstacle);
        }

        public static bool CanCapture(Piece mover, Piece target)
        {
            if (mover == null || target == null)
                return false;

            if (IsBlockingObstacle(target))
                return false;

            return target.Owner != mover.Owner;
        }
    }
}
