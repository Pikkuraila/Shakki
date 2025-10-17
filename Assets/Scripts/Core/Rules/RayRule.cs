using System.Collections.Generic;

namespace Shakki.Core
{
    /// <summary>
    /// RayRule: liikkuu annetuissa suunnissa (esim. torni, lähetti, kuningatar).
    /// Tukee rajoitteita: captureOnly ja nonCaptureOnly.
    /// </summary>
    public sealed class RayRule : IMoveRule
    {
        readonly (int dx, int dy)[] _dirs;
        readonly int _max;
        readonly bool _captureOnly;
        readonly bool _nonCaptureOnly;

        public RayRule((int, int)[] dirs, int maxRange = int.MaxValue,
                       bool captureOnly = false, bool nonCaptureOnly = false)
        {
            _dirs = dirs;
            _max = maxRange;
            _captureOnly = captureOnly;
            _nonCaptureOnly = nonCaptureOnly;
        }

        public IEnumerable<Move> Generate(RuleContext ctx)
        {
            var s = ctx.S;
            var from = ctx.From;
            var me = s.Get(from);
            if (me == null) yield break;

            foreach (var (dx, dy) in _dirs)
            {
                int steps = 0;
                int x = from.X, y = from.Y;

                while (steps < _max)
                {
                    x += dx; y += dy; steps++;
                    var to = new Coord(x, y);
                    if (!s.InBounds(to)) break;

                    var q = s.Get(to);

                    // Tyhjä ruutu → vain jos ei captureOnly
                    if (q == null)
                    {
                        if (!_captureOnly)
                            yield return new Move(from, to);
                        continue;
                    }

                    // Vastustajan nappula → vain jos ei nonCaptureOnly
                    if (q.Owner != me.Owner && !_nonCaptureOnly)
                        yield return new Move(from, to);

                    // Oma tai vihollinen blokkaa aina säteen
                    break;
                }
            }
        }
    }
}
