using System.Collections.Generic;
using System.Linq;
using Shakki.Core;

namespace Shakki.Presentation
{
    /// <summary>
    /// Adapteri, joka lukee PieceDefSO-m��ritykset ja rakentaa niist� Core-s��nt�j�.
    /// </summary>
    public sealed class SoRulesResolver : IRulesResolver
    {
        private readonly Dictionary<string, List<IMoveRule>> _byType;

        public SoRulesResolver(IEnumerable<PieceDefSO> defs)
        {
            _byType = defs.ToDictionary(
                d => d.typeName, // huolehdi ett� t�m� t�sm�� Piece.TypeNameen
                d => (d.rules?.Select(r => r.Build()).ToList()
                    ?? new List<IMoveRule>())
            );
        }

        public IEnumerable<IMoveRule> GetRulesFor(string typeName)
        {
            // Case-insensitive haku on usein k�yt�nn�llinen:
            if (typeName == null) return Enumerable.Empty<IMoveRule>();

            // yrit� exact
            if (_byType.TryGetValue(typeName, out var exact))
                return exact;

            // yrit� case-insensitive
            var kv = _byType.FirstOrDefault(kv =>
                kv.Key.Equals(typeName, System.StringComparison.OrdinalIgnoreCase));
            return kv.Value ?? Enumerable.Empty<IMoveRule>();
        }
    }
}
