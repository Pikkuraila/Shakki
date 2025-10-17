using System.Collections.Generic;
using System.Linq;
using Shakki.Core;

namespace Shakki.Presentation
{
    /// <summary>
    /// Adapteri, joka lukee PieceDefSO-määritykset ja rakentaa niistä Core-sääntöjä.
    /// </summary>
    public sealed class SoRulesResolver : IRulesResolver
    {
        private readonly Dictionary<string, List<IMoveRule>> _byType;

        public SoRulesResolver(IEnumerable<PieceDefSO> defs)
        {
            _byType = defs.ToDictionary(
                d => d.typeName, // huolehdi että tämä täsmää Piece.TypeNameen
                d => (d.rules?.Select(r => r.Build()).ToList()
                    ?? new List<IMoveRule>())
            );
        }

        public IEnumerable<IMoveRule> GetRulesFor(string typeName)
        {
            // Case-insensitive haku on usein käytännöllinen:
            if (typeName == null) return Enumerable.Empty<IMoveRule>();

            // yritä exact
            if (_byType.TryGetValue(typeName, out var exact))
                return exact;

            // yritä case-insensitive
            var kv = _byType.FirstOrDefault(kv =>
                kv.Key.Equals(typeName, System.StringComparison.OrdinalIgnoreCase));
            return kv.Value ?? Enumerable.Empty<IMoveRule>();
        }
    }
}
