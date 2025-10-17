using System.Collections.Generic;

namespace Shakki.Core
{
    public sealed class PawnRule : IMoveRule
    {
        // Säilytetään kentät vain taaksepäin yhteensopivuutta varten (ei käytetä).
        readonly int _dir;
        readonly int _startRank;
        readonly bool _canDouble;

        // Vanha ctor (ok jättää, mutta arvoja ei käytetä suunnan/aloitusrivin määrittelyyn)
        public PawnRule(int dir, int startRank, bool canDouble = true)
        {
            _dir = dir;
            _startRank = startRank;
            _canDouble = canDouble;
        }

        // Uusi oletus-ctor: jos SO:si kutsuu parameterless Buildiä
        public PawnRule() : this(0, 0, true) { }

        public IEnumerable<Move> Generate(RuleContext ctx)
        {
            var s = ctx.S; var from = ctx.From;
            var me = s.Get(from);
            if (me == null) yield break;

            // PÄÄKOHTA: suunta omistajasta (täsmää sinun IsSquareAttacked-logiikkaan)
            int dir = (me.Owner == "white") ? +1 : -1;

            // Aloitusrivi omistajasta
            bool onStartRank = (me.Owner == "white" && from.Y == 1)
                               || (me.Owner == "black" && from.Y == 6);

            // 1) Yksi askel eteen (jos tyhjä)
            var f1 = new Coord(from.X, from.Y + dir);
            if (s.InBounds(f1) && s.Get(f1) == null)
            {
                yield return new Move(from, f1);

                // 2) Tupla-askel aloitusriviltä (jos molemmat tyhjät)
                if (_canDouble && onStartRank)
                {
                    var f2 = new Coord(from.X, from.Y + 2 * dir);
                    if (s.InBounds(f2) && s.Get(f2) == null)
                        yield return new Move(from, f2);
                }
            }

            // 3) Diagonaalikaappaukset
            var capL = new Coord(from.X - 1, from.Y + dir);
            var capR = new Coord(from.X + 1, from.Y + dir);

            if (s.InBounds(capL))
            {
                var q = s.Get(capL);
                if (q != null && q.Owner != me.Owner)
                    yield return new Move(from, capL);
            }

            if (s.InBounds(capR))
            {
                var q = s.Get(capR);
                if (q != null && q.Owner != me.Owner)
                    yield return new Move(from, capR);
            }

            // En passant hoidetaan sinulla ApplyMove:ssa → ei generoida tässä.
        }
    }
}
