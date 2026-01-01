using System;
using System.Collections.Generic;
using UnityEngine;



namespace Shakki.Core
{
    public sealed class GameState
    {
        public BoardState Board { get; }
        public string CurrentPlayer { get; private set; } = "white";

        public Move? LastMove { get; private set; }
        public string LastMoveEffectiveTypeName { get; private set; }
        public List<Move> MoveHistory { get; } = new();

        public event Action<Coord, Piece>? OnCaptured;
        public event Action<string>? OnTurnChanged;

        public int Width => Board.Geometry.Width;
        public int Height => Board.Geometry.Height;

        private static int _lastWidth = 8;
        private static int _lastHeight = 8;

        [System.Obsolete("Use instance property Width")]
        public static int W => _lastWidth;

        [System.Obsolete("Use instance property Height")]
        public static int H => _lastHeight;

        public GameState(GridGeometry geometry, TileTags tags = null)
        {
            Board = new BoardState(geometry, tags);
            _lastWidth = geometry.Width;
            _lastHeight = geometry.Height;
        }

             // Delegoinnit
        public bool InBounds(Coord c) => Board.Contains(c);
        public Piece? Get(Coord c) => Board.Get(c);
        public void Set(Coord c, Piece? p) => Board.Set(c, p);
        public IEnumerable<Coord> AllCoords() => Board.AllCoords();

        public bool ApplyMove(Move m, IRulesResolver rules)
        {
            if (IsGameOver) return false;         // UUSI: ei siirtoja pelin jälkeen

            if (m.From.X == m.To.X && m.From.Y == m.To.Y) return false;

            var piece = Get(m.From);
            if (piece == null || piece.Owner != CurrentPlayer) return false;

            // Legal check (ennallaan)
            bool legal = false;
            foreach (var lm in GenerateLegalMoves(m.From, rules))
            {
                if (lm.To.X == m.To.X && lm.To.Y == m.To.Y) { legal = true; break; }
            }
            if (!legal) return false;

            // Capture (kohderuudussa vihollinen)
            var target = Get(m.To);
            if (target != null && target.Owner != piece.Owner)
                OnCaptured?.Invoke(m.To, target);

            // En passant (ennallaan)
            if (piece.TypeName == "Pawn" && target == null && LastMove.HasValue)
            {
                int dx = m.To.X - m.From.X, dy = m.To.Y - m.From.Y;
                bool diagonal = Math.Abs(dx) == 1 && Math.Abs(dy) == 1;

                var last = LastMove.Value;
                var movedPawn = Get(last.To);
                if (diagonal && movedPawn != null && movedPawn.TypeName == "Pawn" && movedPawn.Owner != piece.Owner)
                {
                    int dyy = last.To.Y - last.From.Y;
                    bool wasDouble = Math.Abs(dyy) == 2;
                    bool adjacentFile = Math.Abs(last.To.X - m.From.X) == 1;
                    bool sameRankAtStart = (last.To.Y == m.From.Y);
                    if (wasDouble && adjacentFile && sameRankAtStart)
                    {
                        Set(last.To, null);
                        OnCaptured?.Invoke(last.To, movedPawn);
                    }
                }
            }

            // Tornitus (ennallaan)
            if (piece.TypeName == "King" && m.To.Y == m.From.Y && Math.Abs(m.To.X - m.From.X) == 2)
            {
                int dir = (m.To.X > m.From.X) ? +1 : -1;
                int y = m.From.Y;

                int x = m.From.X;
                Coord? rookFrom = null;
                while (true)
                {
                    x += dir;
                    var c = new Coord(x, y);
                    if (!InBounds(c)) break;
                    var t = Get(c);
                    if (t == null) continue;
                    if (t.TypeName == "Rook" && t.Owner == piece.Owner) rookFrom = c;
                    break;
                }

                if (rookFrom.HasValue)
                {
                    var rook = Get(rookFrom.Value);
                    if (rook != null && rook.TypeName == "Rook" && rook.Owner == piece.Owner)
                    {
                        var rookTo = new Coord(m.To.X - dir, y);
                        Set(rookTo, rook);
                        Set(rookFrom.Value, null);
                        rook.HasMoved = true;
                    }
                }
            }

            // Siirrä nappula
            Set(m.To, piece);
            Set(m.From, null);
            piece.HasMoved = true;

            // Päivitykset (TÄRKEÄ: kirjaa siirto ennen mahdollisen game overin triggeriä)
            LastMove = m;
            LastMoveEffectiveTypeName = string.IsNullOrEmpty(m.AsTypeName) ? piece.TypeName : m.AsTypeName;
            MoveHistory.Add(m);

            // --- GAME OVER -POLKU ---
            // 1) Nopea haara: jos kohteena oli kuningas, peli loppuu heti
            if (target != null && target.TypeName == "King")
            {
                IsGameOver = true;
                WinnerColor = piece.Owner;
                LoserColor = target.Owner;
                OnGameEnded?.Invoke(new GameEndInfo(WinnerColor, LoserColor, EndReason.KingCaptured, MoveHistory.Count));
                return true;                       // HUOM: ei vuoronvaihtoa, ei OnTurnChanged
            }

            // 2) Varmistus: jos jokin erikoissääntö olisi poistanut kuninkaan muualla, skannaa
            if (CheckGameEnd())
                return true;                       // peli päättyi => ei vuoronvaihtoa

            // Normaalitilanne: vaihda vuoro ja ilmoita
            CurrentPlayer = (CurrentPlayer == "white") ? "black" : "white";
            OnTurnChanged?.Invoke(CurrentPlayer);
            return true;
        }


        public List<Move> AllMoves(string color, IRulesResolver rules = null)
        {
            var moves = new List<Move>();
            foreach (var c in AllCoords())
            {
                var p = Get(c);
                if (p == null || p.Owner != color) continue;

                if (rules != null)
                {
                    var ctx = new RuleContext(this, c, rules);
                    foreach (var rule in rules.GetRulesFor(p.TypeName))
                        foreach (var m in rule.Generate(ctx))
                            moves.Add(m);
                }
                else
                {
                    var ctx = new RuleContext(this, c);
                    foreach (var rule in p.Rules)
                        foreach (var m in rule.Generate(ctx))
                            moves.Add(m);
                }
            }
            return moves;
        }

        public bool IsSquareAttacked(Coord sq, string byColor)
        {
            // Dummy-uhri – tuottaa myös sotilaiden kaappausdiagonaalit oikein
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
            finally { Set(sq, saved); }

            return false;
        }

        private static string OwnerOpposite(string color) => (color == "white") ? "black" : "white";

        public IEnumerable<Move> GenerateLegalMoves(Coord from, IRulesResolver rules)
        {
            var me = Get(from);
            if (me == null) yield break;

            if (rules != null)
            {
                var ctx = new RuleContext(this, from, rules);
                foreach (var rule in rules.GetRulesFor(me.TypeName))
                    foreach (var m in rule.Generate(ctx))
                        yield return m;
                yield break;
            }

            var ctx2 = new RuleContext(this, from);
            foreach (var rule in me.Rules)
                foreach (var m in rule.Generate(ctx2))
                    yield return m;
        }

        public event Action<GameEndInfo>? OnGameEnded;

        public bool IsGameOver { get; private set; }
        public string? WinnerColor { get; private set; }
        public string? LoserColor { get; private set; }

        private bool HasKing(string color)
        {
            foreach (var c in AllCoords())
            {
                var p = Get(c);
                if (p != null && p.Owner == color && p.TypeName == "King")
                    return true;
            }
            return false;
        }

        /// <summary>
        /// Skannaa tilanteen ja päättää pelin, jos toisen (tai molempien) kuningas puuttuu.
        /// Palauttaa true jos peli päättyi tässä kutsussa.
        /// </summary>
        public bool CheckGameEnd()
        {
            if (IsGameOver) return true;

            bool whiteHas = HasKing("white");
            bool blackHas = HasKing("black");
            if (whiteHas && blackHas) return false;

            IsGameOver = true;

            if (whiteHas && !blackHas)
            {
                WinnerColor = "white"; LoserColor = "black";
                OnGameEnded?.Invoke(new GameEndInfo(WinnerColor, LoserColor, EndReason.KingCaptured, MoveHistory.Count));
            }
            else if (!whiteHas && blackHas)
            {
                WinnerColor = "black"; LoserColor = "white";
                OnGameEnded?.Invoke(new GameEndInfo(WinnerColor, LoserColor, EndReason.KingCaptured, MoveHistory.Count));
            }
            else
            {
                WinnerColor = null; LoserColor = null;
                OnGameEnded?.Invoke(new GameEndInfo(null, null, EndReason.DoubleKO, MoveHistory.Count));
            }
            return true;
        }


    }
}
