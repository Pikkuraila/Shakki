// Shakki.Core/KnightmareRule.cs
using System.Collections.Generic;

namespace Shakki.Core
{
    public static class Dirs
    {
        // 8 knight directions
        public static readonly (int dx, int dy)[] Knight = new[]
        {
            ( 1,  2), ( 2,  1), ( 2, -1), ( 1, -2),
            (-1, -2), (-2, -1), (-2,  1), (-1,  2),
        };
    }

    /// <summary>
    /// Knightmare (nightrider): slides any number of knight-steps
    /// along a single knight direction until blocked. Captures on
    /// first enemy landing square, cannot jump past occupied landings.
    /// </summary>
    public sealed class KnightmareRule : IMoveRule
    {
        private readonly RayRule _ray;

        /// <param name="maxSteps">Optional cap (e.g., 3 for “short rider”). Default unlimited.</param>
        public KnightmareRule(int maxSteps = int.MaxValue)
        {
            _ray = new RayRule(Dirs.Knight, maxSteps);
        }

        public IEnumerable<Move> Generate(RuleContext ctx) => _ray.Generate(ctx);
    }
}
