using System.Collections.Generic;

namespace Shakki.Core
{
    public sealed class CannonRule : IMoveRule
    {
        readonly (int dx, int dy)[] _dirs;

        // UUSI: oletus tornin suunnat
        static readonly (int, int)[] RookDirs = new (int, int)[]
        {
        (1,0), (-1,0), (0,1), (0,-1)
        };

        public CannonRule((int, int)[] dirs) { _dirs = dirs; }

        // UUSI: parametriton ctor k�ytt�� RookDirs
        public CannonRule() : this(RookDirs) { }

        public IEnumerable<Move> Generate(RuleContext ctx)
        {
            var s = ctx.S;
            var from = ctx.From;
            var me = s.Get(from);
            if (me == null) yield break;

            foreach (var (dx, dy) in _dirs)
            {
                int x = from.X, y = from.Y;

                // 1) Normaalit liikkeet kuin tornilla, kunnes t�rm�t��n ruutuun jossa on nappula
                while (true)
                {
                    x += dx; y += dy;
                    var here = new Coord(x, y);
                    if (!s.InBounds(here)) break;

                    var q = s.Get(here);
                    if (q == null)
                    {
                        // Tyhj� ruutu: cannon saa liikkua t�h�n (ei kaappausta)
                        yield return new Move(from, here);
                        continue;
                    }

                    // Osui ensimm�iseen nappulaan = SCREEN l�ytyi
                    // 2) Etsi seuraava nappula samassa suunnassa � vain se voi olla kaappauskohde
                    while (true)
                    {
                        x += dx; y += dy;
                        var land = new Coord(x, y);
                        if (!s.InBounds(land)) break;

                        var t = s.Get(land);
                        if (t == null) continue; // saa olla monta tyhj�� v�liss�

                        // Ensimm�inen nappula screenin j�lkeen ratkaisee
                        if (t.Owner != me.Owner)
                            yield return new Move(from, land); // kaappaus sallittu

                        // Oli se oma tai vihollinen, pys�hdyt��n joka tapauksessa
                        break;
                    }

                    // Suunta k�sitelty loppuun
                    break;
                }
            }
        }
    }
}
