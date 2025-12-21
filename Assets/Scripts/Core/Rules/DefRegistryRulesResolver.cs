using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Shakki.Core
{
    public sealed class DefRegistryRulesResolver : IRulesResolver, IRuntimeRulesRegistry
    {
        private readonly Dictionary<string, List<IMoveRule>> _byType = new();

        public IEnumerable<IMoveRule> GetRulesFor(string typeName)
        {
            if (string.IsNullOrEmpty(typeName)) return Enumerable.Empty<IMoveRule>();
            return _byType.TryGetValue(typeName, out var rules) ? rules : Enumerable.Empty<IMoveRule>();
        }

        public void RegisterOrReplace(PieceDefSO def)
        {
            if (def == null || string.IsNullOrEmpty(def.typeName))
            {
                Debug.LogWarning("[Rules] RegisterOrReplace called with null/empty typeName.");
                return;
            }

            var built = def.rules?
                .Where(r => r != null)
                .Select(r => r.Build())
                .ToList()
                ?? new List<IMoveRule>();

            _byType[def.typeName] = built;

            Debug.Log($"[Rules] Registered runtime def: {def.typeName} rules={built.Count}");
        }
    }
}
