namespace Shakki.Core
{
    [System.Flags]
    public enum TileTag
    {
        None = 0,
        Glass = 1 << 0,
        Steel = 1 << 1,
        Hole = 1 << 2,
        // Lisää mitä haluat (Trap, Shop, Teleport, jne.)
    }

    public sealed class TileTags
    {
        readonly TileTag[,] _tags;
        public int Width { get; }
        public int Height { get; }

        public TileTags(int width, int height)
        {
            Width = width; Height = height;
            _tags = new TileTag[width, height];
        }

        public TileTag Get(Coord c) => _tags[c.X, c.Y];
        public void Set(Coord c, TileTag tag) => _tags[c.X, c.Y] = tag;
        public void Add(Coord c, TileTag tag) => _tags[c.X, c.Y] |= tag;
        public void Remove(Coord c, TileTag tag) => _tags[c.X, c.Y] &= ~tag;
    }
}
