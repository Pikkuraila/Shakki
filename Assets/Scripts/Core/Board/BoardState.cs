using System.Collections.Generic;

namespace Shakki.Core
{
    public sealed class BoardState
    {
        public GridGeometry Geometry { get; }
        public TileTags Tags { get; } // valinnainen käyttö UI/logiikalle

        private readonly Piece?[,] _board;

        public BoardState(GridGeometry geometry, TileTags tags = null)
        {
            Geometry = geometry;
            _board = new Piece?[geometry.Width, geometry.Height];
            Tags = tags ?? new TileTags(geometry.Width, geometry.Height);
        }

        public bool Contains(Coord c) => Geometry.Contains(c);

        public Piece? Get(Coord c) => _board[c.X, c.Y];

        public void Set(Coord c, Piece? p)
        {
            if (!Contains(c))
                throw new System.ArgumentOutOfRangeException(nameof(c), "Coord not on allowed board");
            _board[c.X, c.Y] = p;
        }

        public IEnumerable<Coord> AllCoords() => Geometry.Cells();
    }
}
