using System;
using System.Collections.Generic;
using System.Linq;

namespace Shakki.Core
{
    /// <summary>
    /// Jokeri: kopioi viimeksi liikkuneen nappulan tehokkaan tyypin ja käyttää sen sääntöjä.
    /// Jos ei viimeistä siirtoa, käyttää fallback-tyyppiä.
    /// </summary>
    public sealed class JokerRule : IMoveRule
    {
        readonly string _fallbackType;

        public JokerRule(string fallbackType = "King")
        {
            _fallbackType = fallbackType;
        }

        public IEnumerable<Move> Generate(RuleContext ctx)
        {
            var s = ctx.S;
            var from = ctx.From;
            var me = s.Get(from);
            if (me == null) yield break;

            // 1) Päätä kopioitava tyyppi
            var baseType = s.LastMoveEffectiveTypeName;
            if (string.IsNullOrEmpty(baseType) || baseType.Equals(me.TypeName, StringComparison.OrdinalIgnoreCase))
                baseType = _fallbackType;

            // 2) Hae sen säännöt
            var rules = ctx.Rules?.GetRulesFor(baseType) ?? Enumerable.Empty<IMoveRule>();
            if (!rules.Any()) yield break;

            // 3) Delegoi ja tagaa "AsTypeName", jotta Apply() tietää mikä tyyppi oli “tehokas”
            // JokerRule.cs
            foreach (var r in rules)
            {
                foreach (var m in r.Generate(ctx))
                {
                    // tee uusi Move, jossa sama From/To mutta tagi mukana
                    yield return new Move(m.From, m.To, baseType);
                }
            }

        }
    }
}
