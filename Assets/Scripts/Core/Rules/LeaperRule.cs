using System.Collections.Generic;

namespace Shakki.Core
{
    /// <summary>
    /// Leaper = hypp�� t�sm�lleen annettuihin offsetteihin (dx,dy). Ei v�lit� v�liss� olevista paloista.
    /// Esim:
    ///  - Dabbaba: (�2,0),(0,�2)
    ///  - Alfil:   (�2,�2)
    ///  - Knight:  (�1,�2),(�2,�1)
    /// </summary>
    public sealed class LeaperRule : IMoveRule
    {
        readonly (int dx, int dy)[] _offsets;
        readonly bool _captureOnly;     // jos true: vain sy�v�t siirrot
        readonly bool _nonCaptureOnly;  // jos true: vain tyhj��n ruutuun

        public LeaperRule((int, int)[] offsets, bool captureOnly = false, bool nonCaptureOnly = false)
        {
            _offsets = offsets;
            _captureOnly = captureOnly;
            _nonCaptureOnly = nonCaptureOnly;
        }

        public IEnumerable<Move> Generate(RuleContext ctx)
        {
            var s = ctx.S; var from = ctx.From; var me = s.Get(from);
            if (me == null) yield break;

            foreach (var (dx, dy) in _offsets)
            {
                var to = new Coord(from.X + dx, from.Y + dy);
                if (!s.InBounds(to)) continue;

                var q = s.Get(to);
                bool isCapture = q != null && q.Owner != me.Owner;
                bool isBlockedByOwn = q != null && q.Owner == me.Owner;

                if (isBlockedByOwn) continue;                 // ei voi hyp�t� omaan ruutuun
                if (_captureOnly && !isCapture) continue;     // vaaditaan sy�nti
                if (_nonCaptureOnly && isCapture) continue;   // vaaditaan tyhj� ruutu

                yield return new Move(from, to);
            }
        }
    }
}
