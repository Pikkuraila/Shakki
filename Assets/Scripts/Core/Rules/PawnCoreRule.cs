using System.Collections.Generic;

namespace Shakki.Core
{
    // VAIN perusliike ja diagokaappaus ñ ei tuplahyppy‰, ei en passantia
    public sealed class PawnCoreRule : IMoveRule
    {
        // J‰tet‰‰n vanha ctor yhteensopivuussyist‰, mutta arvoa ei k‰ytet‰
        public PawnCoreRule(int dir) { }
        public PawnCoreRule() { }

        public IEnumerable<Move> Generate(RuleContext ctx)
        {
            var s = ctx.S; var from = ctx.From;
            var me = s.Get(from);
            if (me == null) yield break;

            // SUUNTA OWNERISTA: white = +1, black = -1
            int dir = (me.Owner == "white") ? +1 : -1;

            // eteen 1
            var f1 = new Coord(from.X, from.Y + dir);
            if (s.InBounds(f1) && s.Get(f1) == null)
                yield return new Move(from, f1);

            // diagokaappaukset
            var capR = new Coord(from.X + 1, from.Y + dir);
            var capL = new Coord(from.X - 1, from.Y + dir);

            if (s.InBounds(capR))
            {
                var q = s.Get(capR);
                if (q != null && q.Owner != me.Owner)
                    yield return new Move(from, capR);
            }
            if (s.InBounds(capL))
            {
                var q = s.Get(capL);
                if (q != null && q.Owner != me.Owner)
                    yield return new Move(from, capL);
            }
        }
    }
}
