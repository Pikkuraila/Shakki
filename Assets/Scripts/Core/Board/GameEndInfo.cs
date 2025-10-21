namespace Shakki.Core
{
    public enum EndReason { KingCaptured, DoubleKO }

    public readonly struct GameEndInfo
    {
        public string WinnerColor { get; }
        public string LoserColor { get; }
        public EndReason Reason { get; }
        public int PlyCount { get; }

        public GameEndInfo(string winner, string loser, EndReason reason, int plyCount)
        {
            WinnerColor = winner;
            LoserColor = loser;
            Reason = reason;
            PlyCount = plyCount;
        }
    }
}
