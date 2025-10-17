using System;
using System.Linq;

namespace Shakki.Core
{
    public sealed class RandomAi : IAiPlayer
    {
        readonly Random _rng = new();

        public Move ChooseMove(GameState state, string color, IRulesResolver rules)
        {
            var moves = state.AllMoves(color).ToList();
            if (moves.Count == 0) return default; // ei siirtoja = luovutus
            return moves[_rng.Next(moves.Count)];
        }
    }
}
