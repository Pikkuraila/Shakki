using System.Collections.Generic;

namespace Shakki.Core
{
    public sealed class Piece
    {
        public string Owner { get; }
        public string TypeName { get; }  // esim. "Rook", "Empress"
        public IReadOnlyList<IMoveRule> Rules { get; }
        public bool HasMoved { get; set; }

        public Piece(string owner, string typeName, IReadOnlyList<IMoveRule> rules)
        {
            Owner = owner; 
            TypeName = typeName; 
            Rules = rules;
            HasMoved = false;
        }
    }
}
