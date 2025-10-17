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

        // UUSI: parametriton ctor käyttää RookDirs
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

                // 1) Normaalit liikkeet kuin tornilla, kunnes törmätään ruutuun jossa on nappula
                while (true)
                {
                    x += dx; y += dy;
                    var here = new Coord(x, y);
                    if (!s.InBounds(here)) break;

                    var q = s.Get(here);
                    if (q == null)
                    {
                        // Tyhjä ruutu: cannon saa liikkua tähän (ei kaappausta)
                        yield return new Move(from, here);
                        continue;
                    }

                    // Osui ensimmäiseen nappulaan = SCREEN löytyi
                    // 2) Etsi seuraava nappula samassa suunnassa — vain se voi olla kaappauskohde
                    while (true)
                    {
                        x += dx; y += dy;
                        var land = new Coord(x, y);
                        if (!s.InBounds(land)) break;

                        var t = s.Get(land);
                        if (t == null) continue; // saa olla monta tyhjää välissä

                        // Ensimmäinen nappula screenin jälkeen ratkaisee
                        if (t.Owner != me.Owner)
                            yield return new Move(from, land); // kaappaus sallittu

                        // Oli se oma tai vihollinen, pysähdytään joka tapauksessa
                        break;
                    }

                    // Suunta käsitelty loppuun
                    break;
                }
            }
        }
    }
}
