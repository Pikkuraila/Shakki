using System.Collections.Generic;

namespace Shakki.Core
{
    public sealed class Piece
    {
        public string Owner { get; }
        public string TypeName { get; }              // esim. "Rook", "Empress"
        public string InstanceId { get; }
        public IReadOnlyList<IMoveRule> Rules { get; }
        public bool HasMoved { get; set; }

        public PieceTag Tags { get; private set; }

        public Piece(string owner, string typeName, IReadOnlyList<IMoveRule> rules, PieceTag tags = PieceTag.None, string instanceId = null)
        {
            Owner = owner;
            TypeName = typeName;
            InstanceId = instanceId;
            Rules = rules;
            HasMoved = false;
            Tags = tags;
        }

        public bool IsInjured;

        public bool HasTag(PieceTag tag) => (Tags & tag) != 0;

        public void AddTag(PieceTag tag) => Tags |= tag;
        public void RemoveTag(PieceTag tag) => Tags &= ~tag;
    }
}
