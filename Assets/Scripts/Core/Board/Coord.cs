namespace Shakki.Core
{
    public readonly struct Coord
    {
        public readonly int X;
        public readonly int Y;
        public Coord(int x, int y) { X = x; Y = y; }
        public override string ToString() => $"({X},{Y})";
    }
}
