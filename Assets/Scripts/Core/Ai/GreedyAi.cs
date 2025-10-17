using System;
using System.Linq;
using System.Collections.Generic;

namespace Shakki.Core
{
    public sealed class GreedyAi : IAiPlayer
    {
        readonly Random _rng = new();
        readonly Dictionary<string, int> _values = new()
        {
            {"King", 1000}, {"Queen", 9}, {"Rook", 5},
            {"Bishop", 3}, {"Knight", 3}, {"Pawn", 1},
            {"Alfil", 2}, {"Dabbaba", 2}
        };

        public Move ChooseMove(GameState state, string color, IRulesResolver rules)
        {
            var moves = state.AllMoves(color).ToList();
            if (moves.Count == 0) return default;

            // pisteytä siirrot
            var scored = moves.Select(m =>
            {
                var target = state.Get(m.To);
                var val = target == null ? 0 :
                    (_values.TryGetValue(target.TypeName, out int v) ? v : 1);
                return (move: m, score: val);
            }).ToList();

            int max = scored.Max(x => x.score);
            var best = scored.Where(x => x.score == max).Select(x => x.move).ToList();
            return best[_rng.Next(best.Count)];
        }
    }
}
