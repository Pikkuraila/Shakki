using System.Collections.Generic;

namespace Shakki.Core
{
    /// <summary>
    /// Vastaa nappulatyypin (TypeName) siirtos��nt�jen toimittamisesta.
    /// </summary>
    public interface IRulesResolver
    {
        IEnumerable<IMoveRule> GetRulesFor(string typeName);
    }
}
