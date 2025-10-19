using System.Collections.Generic;

namespace Shakki.Core
{
    public sealed class GridGeometry
    {
        public int Width { get; }
        public int Height { get; }

        // true = pelikelpoinen; null = kaikki pelikelpoisia
        readonly bool[,] _allowedMask; // voi olla null

        public GridGeometry(int width, int height, bool[,] allowedMask = null)
        {
            Width = width; Height = height;
            _allowedMask = allowedMask;
        }

        public bool Contains(Coord c)
        {
            if (c.X < 0 || c.Y < 0 || c.X >= Width || c.Y >= Height) return false;
            return _allowedMask == null || _allowedMask[c.X, c.Y];
        }

        public IEnumerable<Coord> Cells()
        {
            for (int y = 0; y < Height; y++)
                for (int x = 0; x < Width; x++)
                    if (_allowedMask == null || _allowedMask[x, y])
                        yield return new Coord(x, y);
        }

        public bool TryTranslate(Coord from, int dx, int dy, out Coord to)
        {
            to = new Coord(from.X + dx, from.Y + dy);
            return Contains(to);
        }

        public IEnumerable<Coord> Ray(Coord from, int dx, int dy)
        {
            int x = from.X, y = from.Y;
            while (true)
            {
                x += dx; y += dy;
                var c = new Coord(x, y);
                if (!Contains(c)) yield break;
                yield return c;
            }
        }
    }
}
