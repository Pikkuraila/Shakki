using System.Collections.Generic;
using UnityEngine;

namespace Shakki.Core
{
    public sealed class GameState
    {
        public const int W = 8;
        public const int H = 8;

        private readonly Piece?[,] _board = new Piece?[W, H];
        public string CurrentPlayer { get; private set; } = "white";
        public Move LastMove { get; private set; }
        public List<Move> MoveHistory { get; } = new();
        public event System.Action<Coord, Piece> OnCaptured;
        public event System.Action<string> OnTurnChanged; // uusi pelaaja: "white"/"black"

        public bool InBounds(Coord c) => c.X >= 0 && c.Y >= 0 && c.X < W && c.Y < H;

        public Piece? Get(Coord c) => _board[c.X, c.Y];
        public void Set(Coord c, Piece? p) => _board[c.X, c.Y] = p;

        public IEnumerable<Coord> AllCoords()
        {
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                    yield return new Coord(x, y);
        }



        public bool ApplyMove(Move m, IRulesResolver rules)
        {

            Debug.Log($"[GS] ApplyMove? cur={CurrentPlayer}, from={m.From.X},{m.From.Y} to={m.To.X},{m.To.Y} as={m.AsTypeName ?? "∅"}");

            var piece = Get(m.From);
            if (piece == null) { Debug.Log("[GS] FAIL: no piece @from"); return false; }
            if (piece.Owner != CurrentPlayer) { Debug.Log($"[GS] FAIL: piece.Owner={piece.Owner} != CurrentPlayer={CurrentPlayer}"); return false; }

            // Laillisuustarkistus käyttäen resolveria (JokerRule näkyy)
            bool legal = false;
            foreach (var lm in GenerateLegalMoves(m.From, rules))
                if (lm.To.X == m.To.X && lm.To.Y == m.To.Y) { legal = true; break; }
            if (!legal) return false;

            // --- NORMAALI KAAPPAUS (pidä oma olemassa oleva logiikkasi) ---
            var target = Get(m.To);
            if (target != null && target.Owner != piece.Owner)
            {
                OnCaptured?.Invoke(m.To, target);
            }

            // --- EN PASSANT -POISTO ---
            if (piece.TypeName == "Pawn"
                && m.From.X != m.To.X          // diagonaali
                && Get(m.To) == null           // kohderuutu tyhjä -> EP-ehdokas
                && LastMove != null)
            {
                var last = LastMove;
                var moved = Get(last.To);      // << tämä ON se kaapattava sotilas
                int dy = last.To.Y - last.From.Y;

                bool wasDouble = (moved != null && moved.TypeName == "Pawn"
                                     && moved.Owner != piece.Owner && System.Math.Abs(dy) == 2);
                bool adjacentFile = System.Math.Abs(last.To.X - m.From.X) == 1;
                bool sameRank = (last.To.Y == m.From.Y);

                if (wasDouble && adjacentFile && sameRank)
                {
                    // Poista ohitettu sotilas ja ilmoita kaappaus
                    Set(last.To, null);
                    OnCaptured?.Invoke(last.To, moved);
                }
                System.Console.WriteLine($"[GS] Apply {m.From}->{m.To}, AsType={m.AsTypeName ?? "∅"}");
            }

            // --- TORNITUS: jos kuningas liikkuu 2 ruutua vaakasuunnassa, liikutetaan torni ---
            if (piece.TypeName == "King" && System.Math.Abs(m.To.X - m.From.X) == 2 && m.To.Y == m.From.Y)
            {
                int dir = (m.To.X > m.From.X) ? +1 : -1; // +1 = O-O, -1 = O-O-O
                int y = m.From.Y;

                // Etsi tornin lähtöruutu siltä puolelta
                int rookFromX = -1;
                if (dir == +1)
                {
                    // oikea torni
                    for (int x = m.From.X + 1; x < GameState.W; x++)
                    {
                        var t = Get(new Coord(x, y));
                        if (t == null) continue;
                        if (t.TypeName == "Rook" && t.Owner == piece.Owner) rookFromX = x;
                        break;
                    }
                }
                else
                {
                    // vasen torni
                    for (int x = m.From.X - 1; x >= 0; x--)
                    {
                        var t = Get(new Coord(x, y));
                        if (t == null) continue;
                        if (t.TypeName == "Rook" && t.Owner == piece.Owner) rookFromX = x;
                        break;
                    }
                }

                if (rookFromX >= 0)
                {
                    var rookFrom = new Coord(rookFromX, y);
                    var rook = Get(rookFrom);
                    if (rook != null && rook.TypeName == "Rook" && rook.Owner == piece.Owner)
                    {
                        var rookTo = new Coord(m.To.X - dir, y); // kuninkaan viereen "sisäpuolelle"
                        Set(rookTo, rook);
                        Set(rookFrom, null);
                        rook.HasMoved = true;

                        // Kerro näkymälle, että tämä torni “siirtyi” (valinnainen event)
                        // OnCaptured ei ole tähän sopiva; jos haluat, lisää OnRookCastled(rookFrom, rookTo).
                    }
                }
            }


            // ... siirto on todettu lailliseksi, mahdolliset kaappaukset hoidettu,
            // siirrä nappula laudalla:
            Set(m.To, piece);
            Set(m.From, null);

            // Asettaa nappulan liikkuneeksi
            piece.HasMoved = true;

            // Päivitä viime siirto + tehokas tyyppi
            LastMove = m;
            var effective = string.IsNullOrEmpty(m.AsTypeName) ? piece.TypeName : m.AsTypeName;
            LastMoveEffectiveTypeName = effective;

            Debug.Log($"[GS] OK: moved {piece.Owner} {piece.TypeName} {m.From.X},{m.From.Y}->{m.To.X},{m.To.Y}. Next={(CurrentPlayer == "white" ? "black" : "white")}");

            // VUORONVAIHTO
            CurrentPlayer = (CurrentPlayer == "white") ? "black" : "white";
            Debug.Log($"[GS] {piece.Owner} {m.From}->{m.To} ok. Turn -> {CurrentPlayer}");
            OnTurnChanged?.Invoke(CurrentPlayer);

            // (debug, halutessasi)
            Debug.Log($"[GS] Turn -> {CurrentPlayer} (effective={effective})");

            return true;
        }

        public string LastMoveEffectiveTypeName { get; private set; }

        public void Apply(Move m)
        {
            // ... sinun normaali siirron soveltaminen

            var mover = Get(m.To); // siirron jälkeen siirtäjä on m.To:ssa
            var effective = string.IsNullOrEmpty(m.AsTypeName) ? mover?.TypeName : m.AsTypeName;
            LastMoveEffectiveTypeName = effective;
        }

        public struct CaptureInfo
        {
            public bool DidCapture;
            public Coord At;
            public Piece Piece;
        }

        public List<Move> AllMoves(string color, IRulesResolver rules = null)
        {
            var moves = new List<Move>();

            foreach (var c in AllCoords())
            {
                var p = Get(c);
                if (p == null || p.Owner != color) continue;

                var ctx = new RuleContext(this, c, rules);
                foreach (var rule in p.Rules)
                    foreach (var m in rule.Generate(ctx))
                        moves.Add(m);
            }

            return moves;
        }

        public bool IsSquareAttacked(Coord sq, string byColor)
        {
            // 1) Sotilaiden hyökkäykset käsin (koska niiden liikesäännöt eivät tuota diagonaalia ellei nappulaa ole)
            int pawnDir = (byColor == "white") ? +1 : -1;
            var p1 = new Coord(sq.X - 1, sq.Y - pawnDir); // huom: sq on kohde; hyökkääjä olisi -dir suunnassa
            var p2 = new Coord(sq.X + 1, sq.Y - pawnDir);

            if (InBounds(p1))
            {
                var p = Get(p1);
                if (p != null && p.Owner == byColor && p.TypeName == "Pawn") return true;
            }
            if (InBounds(p2))
            {
                var p = Get(p2);
                if (p != null && p.Owner == byColor && p.TypeName == "Pawn") return true;
            }

            // 2) Muut nappulat: tarkista, voiko ne siirtyä tähän ruutuun CAPTURENA
            //    Trikki: laita ruutuun hetkeksi "uhri", jotta kaappausreitit syntyvät
            var saved = Get(sq);
            var dummyVictim = new Piece(OwnerOpposite(byColor), "Dummy", System.Array.Empty<IMoveRule>());
            Set(sq, dummyVictim);

            try
            {
                foreach (var c in AllCoords())
                {
                    var q = Get(c);
                    if (q == null || q.Owner != byColor) continue;

                    var ctx = new RuleContext(this, c);
                    foreach (var rule in q.Rules)
                        foreach (var m in rule.Generate(ctx))
                            if (m.To.X == sq.X && m.To.Y == sq.Y)
                                return true;
                }
            }
            finally
            {
                Set(sq, saved); // palauta asema
            }

            return false;
        }

        private static string OwnerOpposite(string color) => (color == "white") ? "black" : "white";

        public IEnumerable<Move> GenerateLegalMoves(Coord from, IRulesResolver rules)
        {
            // 1) Resolveripolku (JokerRule mukana)
            if (rules != null)
            {
                var me = Get(from);
                if (me == null) yield break;

                var ctx = new RuleContext(this, from, rules);
                foreach (var rule in rules.GetRulesFor(me.TypeName))
                {
                    foreach (var m in rule.Generate(ctx))
                    {
                        // Jos sinulla on erillinen laillisuussuodatus, käytä sitä tässä:
                        // if (IsLegalMove(m)) yield return m;
                        yield return m;
                    }
                }
                yield break;
            }

            // 2) Legacy-reitti: EI resolveria → käytä vanhaa 1-param. logiikkaasi
            // Jos sinulla on vielä olemassa se alkuperäinen GenerateLegalMoves(from)-metodin
            // runko, kopioi sen sisus tähän. Alla on tyypillinen muoto:
            {
                var me = Get(from);
                if (me == null) yield break;

                var ctx = new RuleContext(this, from); // ilman resolveria
                                                       // HUOM: korvaa 'me.Rules' sillä nimellä, jolla nappulasi säilyttää sääntölistan
                foreach (var rule in me.Rules)
                {
                    foreach (var m in rule.Generate(ctx))
                    {
                        // if (IsLegalMove(m)) yield return m;
                        yield return m;
                    }
                }
            }
        }
    }
}