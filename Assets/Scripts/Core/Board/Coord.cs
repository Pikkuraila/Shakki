namespace Shakki.Core
{
    public readonly struct Coord : System.IEquatable<Coord>
    {
        public int X { get; }
        public int Y { get; }
        public Coord(int x, int y) { X = x; Y = y; }

        public bool Equals(Coord other) => X == other.X && Y == other.Y;
        public override bool Equals(object obj) => obj is Coord c && Equals(c);
        public override int GetHashCode() => unchecked((X * 397) ^ Y);
        public override string ToString() => $"({X},{Y})";
    }
}
