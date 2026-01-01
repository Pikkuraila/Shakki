using System.Collections.Generic;

namespace Shakki.Meta.Bestiary
{
    public sealed class IntelResolver
    {
        private readonly List<IIntelSource> _sources = new();

        public IntelResolver(IEnumerable<IIntelSource> sources)
        {
            if (sources != null) _sources.AddRange(sources);
        }

        public IntelProfile Resolve(EnemyArchetypeSO archetype, in IntelContext ctx)
        {
            var p = IntelProfile.Default;
            for (int i = 0; i < _sources.Count; i++)
                _sources[i].Apply(ref p, archetype, ctx);
            return p;
        }
    }
}
