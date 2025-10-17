using System.Collections.Generic;

namespace Shakki.Core
{
    public readonly struct RuleContext
    {
        public GameState S { get; }
        public Coord From { get; }
        public IRulesResolver Rules { get; }

        // UUSI: vanhan koodin taaksepäin-yhteensopivuus
        public RuleContext(GameState s, Coord from)
            : this(s, from, null) { }

        // Pääkonstruktori
        public RuleContext(GameState s, Coord from, IRulesResolver rules)
        {
            S = s;
            From = from;
            Rules = rules;
        }
    }

    public interface IMoveRule
    {
        IEnumerable<Move> Generate(RuleContext ctx);
    }
}
