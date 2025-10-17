using System.Collections.Generic;

namespace Shakki.Core
{
    /// Kuninkaalle: generoi O-O ja O-O-O jos ehdot t‰yttyv‰t.
    public sealed class CastlingRule : IMoveRule
    {
        public IEnumerable<Move> Generate(RuleContext ctx)
        {
            var s = ctx.S;
            var from = ctx.From;
            var king = s.Get(from);
            if (king == null) yield break;
            if (king.TypeName != "King") yield break;
            if (king.HasMoved) yield break;

            string me = king.Owner;
            string opp = (me == "white") ? "black" : "white";

            // Kuningas ei saa olla shakissa
            if (s.IsSquareAttacked(from, opp)) yield break;

            // Etsit‰‰n tornit samalta rivilt‰: vasen (queenside) ja oikea (kingside)
            int y = from.Y;

            // oikea torni: skannaa oikealle
            int rxRight = -1;
            for (int x = from.X + 1; x < GameState.W; x++)
            {
                var p = s.Get(new Coord(x, y));
                if (p == null) continue;
                if (p.TypeName == "Rook" && p.Owner == me && !p.HasMoved) { rxRight = x; }
                break; // pys‰hdy ensimm‰iseen nappulaan
            }

            // vasen torni: skannaa vasemmalle
            int rxLeft = -1;
            for (int x = from.X - 1; x >= 0; x--)
            {
                var p = s.Get(new Coord(x, y));
                if (p == null) continue;
                if (p.TypeName == "Rook" && p.Owner == me && !p.HasMoved) { rxLeft = x; }
                break; // pys‰hdy ensimm‰iseen nappulaan
            }

            // Yhteinen tarkastusfunktio
            bool CanCastle(int rookX, int dir) // dir: +1 oikea (O-O), -1 vasen (O-O-O)
            {
                if (rookX < 0) return false;

                // V‰liss‰ ei nappuloita
                for (int x = from.X + dir; x != rookX; x += dir)
                    if (s.Get(new Coord(x, y)) != null) return false;

                // Kuningas ei saa kulkea/saapua kontrolloituihin ruutuihin
                var oppColor = opp;
                var step1 = new Coord(from.X + dir, y);
                var step2 = new Coord(from.X + 2 * dir, y);

                if (s.IsSquareAttacked(step1, oppColor)) return false;
                if (s.IsSquareAttacked(step2, oppColor)) return false;

                return true;
            }

            // Kingside (O-O): kuningas -> X+2
            if (rxRight >= 0 && CanCastle(rxRight, +1))
                yield return new Move(from, new Coord(from.X + 2, y));

            // Queenside (O-O-O): kuningas -> X-2
            if (rxLeft >= 0 && CanCastle(rxLeft, -1))
                yield return new Move(from, new Coord(from.X - 2, y));
        }
    }
}
