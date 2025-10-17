using System.Collections.Generic;

namespace Shakki.Core
{
    public sealed class KnightJumpRule : IMoveRule
    {
        static readonly (int, int)[] L = new (int, int)[] {
            (1,2),(2,1),(2,-1),(1,-2),(-1,-2),(-2,-1),(-2,1),(-1,2)
        };

        public IEnumerable<Move> Generate(RuleContext ctx)
        {
            var s = ctx.S; var from = ctx.From; var me = s.Get(from);
            if (me == null) yield break;

            foreach (var (dx, dy) in L)
            {
                var to = new Coord(from.X + dx, from.Y + dy);
                if (!s.InBounds(to)) continue;
                var q = s.Get(to);
                if (q == null || q.Owner != me.Owner)
                    yield return new Move(from, to);
            }
        }
    }
}
