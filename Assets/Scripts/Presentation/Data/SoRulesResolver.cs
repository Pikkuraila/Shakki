using System.Collections.Generic;
using System.Linq;
using Shakki.Core;
using UnityEngine;

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
            // Käytä case-insensitive mapia niin runtimeId:tkin toimii varmasti
            _byType = new Dictionary<string, List<IMoveRule>>(System.StringComparer.OrdinalIgnoreCase);

            if (defs != null)
            {
                foreach (var d in defs)
                    RegisterOrReplace(d);
            }
        }

        /// <summary>
        /// Lisää tai korvaa säännöt tälle typeNamelle. Tätä käytetään myös runtime-amalgamien rekisteröintiin.
        /// </summary>
        public void RegisterOrReplace(PieceDefSO def)
        {
            if (def == null || string.IsNullOrEmpty(def.typeName))
                return;

            var built = def.rules?.Select(r => r != null ? r.Build() : null)
                                 .Where(x => x != null)
                                 .ToList()
                        ?? new List<IMoveRule>();

            _byType[def.typeName] = built;

            Debug.Log($"[SoRulesResolver] RegisterOrReplace type='{def.typeName}' rules={built.Count}");
        }

        public IEnumerable<IMoveRule> GetRulesFor(string typeName)
        {
            if (string.IsNullOrEmpty(typeName))
                return Enumerable.Empty<IMoveRule>();

            return _byType.TryGetValue(typeName, out var exact)
                ? exact
                : Enumerable.Empty<IMoveRule>();
        }
    }
}
