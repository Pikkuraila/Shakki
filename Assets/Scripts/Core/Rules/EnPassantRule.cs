using System.Collections.Generic;

namespace Shakki.Core
{
    public sealed class EnPassantRule : IMoveRule
    {
        readonly int _dir; // white: +1, black: -1

        public EnPassantRule(int dir) { _dir = dir; }

        public IEnumerable<Move> Generate(RuleContext ctx)
        {
            var s = ctx.S; var from = ctx.From;
            var me = s.Get(from);
            if (me == null) yield break;

            // (valinnainen) vain sotilas saa tarjota en passant -siirtoa
            if (me.TypeName != "Pawn") yield break;

            var last = s.LastMove;
            if (last == null) yield break;

            var moved = s.Get(last.To);
            if (moved == null || moved.Owner == me.Owner) yield break;
            if (moved.TypeName != "Pawn") yield break;

            // viime siirron piti olla tuplahyppy
            int dy = last.To.Y - last.From.Y;
            if (System.Math.Abs(dy) != 2) yield break;

            // meidän sotilas on samassa rivissä kuin vastustajan tuplahypyn loppuruutu
            // ja viereisellä tiedostolla
            bool adjacentFile = System.Math.Abs(last.To.X - from.X) == 1;
            bool sameRank = (last.To.Y == from.Y);
            if (!(adjacentFile && sameRank)) yield break;

            // VÄLIRUUTU = kohde ruutu en passant -siirrolle
            int midY = (last.From.Y + last.To.Y) / 2;  // tai last.From.Y + System.Math.Sign(dy)
            var captureTo = new Coord(last.To.X, midY);

            // pitää olla laudalla ja tyhjä
            if (!s.InBounds(captureTo) || s.Get(captureTo) != null) yield break;

            // Jos käytät lippuja, lisää MoveFlags.EnPassant tms. tähän
            yield return new Move(from, captureTo);
        }
    }
}
