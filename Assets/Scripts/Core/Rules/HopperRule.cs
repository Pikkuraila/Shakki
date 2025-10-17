using System.Collections.Generic;

namespace Shakki.Core
{
    /// Hopper: hypp‰‰ yli l‰himm‰n nappulan annetussa suunnassa ja yritt‰‰ laskeutua sen j‰lkeen.
    /// Esim:
    ///  - Grasshopper: queen-dirs, hopDistance=1, canLandEmpty=true
    ///  - Cannon capture: rook-dirs, hopDistance=1, captureOnlyAfterJump=true, canLandEmpty=false
    public sealed class HopperRule : IMoveRule
    {
        readonly (int dx, int dy)[] _dirs;
        readonly int _hopDistance;
        readonly bool _captureOnlyAfterJump;
        readonly bool _canLandEmpty;

        public HopperRule(
            (int, int)[] dirs,
            int hopDistance = 1,
            bool captureOnlyAfterJump = false,
            bool canLandEmpty = true)
        {
            _dirs = dirs;
            _hopDistance = hopDistance;
            _captureOnlyAfterJump = captureOnlyAfterJump;
            _canLandEmpty = canLandEmpty;
        }

        public IEnumerable<Move> Generate(RuleContext ctx)
        {
            var s = ctx.S;
            var from = ctx.From;
            var me = s.Get(from);
            if (me == null) yield break;

            foreach (var (dx, dy) in _dirs)
            {
                int x = from.X, y = from.Y;
                bool foundHurdle = false;

                while (true)
                {
                    x += dx; y += dy;
                    var here = new Coord(x, y);
                    if (!s.InBounds(here)) break;

                    var q = s.Get(here);

                    if (!foundHurdle)
                    {
                        // Etsit‰‰n ensimm‰inen este (oma tai vihollinen)
                        if (q != null) foundHurdle = true;
                        continue;
                    }

                    // Laskeutumisruutu on HETI esteen takana *hopDistance* askelta
                    int lx = (from.X) + dx;  // ensimm‰inen ruutu l‰hdˆn j‰lkeen
                    int ly = (from.Y) + dy;

                    // mutta me ollaan nyt esteen ruudussa (here = hurdle),
                    // joten lasketaan laskeutuminen sen takaa:
                    lx = x + dx * _hopDistance;
                    ly = y + dy * _hopDistance;

                    var land = new Coord(lx, ly);
                    if (!s.InBounds(land)) break;

                    var target = s.Get(land);

                    if (target == null)
                    {
                        if (_canLandEmpty)
                            yield return new Move(from, land);
                    }
                    else if (target.Owner != me.Owner)
                    {
                        // Cannon-capture (tai muut hopper-capturet)
                        if (!_captureOnlyAfterJump || foundHurdle)
                            yield return new Move(from, land);
                    }
                    // Hopper tekee vain yhden laskeutumisen per suunta
                    break;
                }
            }
        }
    }
}
