using System.Collections.Generic;

namespace Shakki.Core
{
    // Tuplahyppy omana sääntönä: vain starttiriviltä JA vain jos sotilas ei ole liikkunut aiemmin
    public sealed class PawnDoubleStepRule : IMoveRule
    {
        // Jätetään vanha ctor yhteensopivuuden vuoksi (ei käytetä arvoja)
        public PawnDoubleStepRule(int dir, int startRank) { }
        public PawnDoubleStepRule() { }

        public IEnumerable<Move> Generate(RuleContext ctx)
        {
            var s = ctx.S;
            var from = ctx.From;
            var me = s.Get(from);
            if (me == null) yield break;

            // SUUNTA ja STARTTIRIVI OWNERISTA
            int dir = (me.Owner == "white") ? +1 : -1;
            bool onStartRank = (me.Owner == "white" && from.Y == 1)
                               || (me.Owner == "black" && from.Y == 6);

            if (!onStartRank || me.HasMoved) yield break;

            var f1 = new Coord(from.X, from.Y + dir);
            var f2 = new Coord(from.X, from.Y + 2 * dir);

            if (s.InBounds(f1) && s.Get(f1) == null &&
                s.InBounds(f2) && s.Get(f2) == null)
            {
                yield return new Move(from, f2);
            }
        }
    }
}
