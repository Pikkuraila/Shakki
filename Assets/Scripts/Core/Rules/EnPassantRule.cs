using System.Collections.Generic;

namespace Shakki.Core
{
    public sealed class EnPassantRule : IMoveRule
    {
        // Pelaajan liikesuunta: white +1, black -1
        readonly int _dir;

        public EnPassantRule(int dir) { _dir = dir; }

        public IEnumerable<Move> Generate(RuleContext ctx)
        {
            var s = ctx.S;
            var from = ctx.From;
            var me = s.Get(from);
            if (me == null) yield break;

            // EP vain jos nappulalla on tagi (ei sidottu Pawn-tyyppiin)
            if (!me.HasTag(PieceTag.EnPassant)) yield break;

            // Pitää olla edellinen siirto
            if (!s.LastMove.HasValue) yield break;
            var last = s.LastMove.Value;

            // Vastustajan täytyy olla liikkunut juuri 2 ruutua pystysuuntaan
            var moved = s.Get(last.To);
            if (moved == null || moved.Owner == me.Owner) yield break;

            int dy = last.To.Y - last.From.Y;
            if (System.Math.Abs(dy) != 2) yield break;

            // Olemme viereisellä tiedostolla ja samalla rivillä kuin vastustajan tuplahypyn loppu
            bool adjacentFile = System.Math.Abs(last.To.X - from.X) == 1;
            bool sameRank = (last.To.Y == from.Y);
            if (!(adjacentFile && sameRank)) yield break;

            // EP-kaappauksen kohderuutu on “ohitetun” ruudun suuntaan yhdellä
            var captureTo = new Coord(last.To.X, from.Y + _dir);

            // Sen on oltava laudalla ja tyhjä
            if (!s.InBounds(captureTo) || s.Get(captureTo) != null) yield break;

            // (Jos käytät MoveFlagseja, aseta tähän EnPassant-lippu)
            yield return new Move(from, captureTo);
        }
    }
}
